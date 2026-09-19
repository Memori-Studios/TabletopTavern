using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// Disables UI Animators whose graphics nobody can see (alpha-hidden or mask-culled) and wakes
    /// them when they become visible again. Panels here are hidden by alpha, not deactivated, so
    /// without this every HUD animator ticks and dirties its canvas forever.
    /// Only CanvasGroups above the Animator count as hiding it. A group inside its own subtree may
    /// be the thing the animation fades in, and reading it would keep the animator asleep forever.
    /// </summary>
    public class UIAnimatorSleeper : MonoBehaviour
    {
        [SerializeField] private int checkEveryFrames = 5;
        [SerializeField] private float rescanSeconds = 2f;

        private readonly List<Animator> _animators = new();
        private readonly List<Graphic> _firstGraphics = new();
        private readonly List<CanvasGroup[]> _parentGroups = new();
        private float _nextRescan;

        private void OnEnable()
        {
            Rescan();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _animators.Count; i++)
                if (_animators[i] != null && !_animators[i].enabled) _animators[i].enabled = true;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRescan) Rescan();
            if (Time.frameCount % checkEveryFrames != 0) return;

            for (int i = 0; i < _animators.Count; i++)
            {
                Animator animator = _animators[i];
                Graphic graphic = _firstGraphics[i];
                if (animator == null || graphic == null) continue;

                bool visible = !graphic.canvasRenderer.cull && ParentAlpha(_parentGroups[i]) > 0.001f;
                if (animator.enabled == visible) continue;

                animator.keepAnimatorStateOnDisable = true;
                animator.enabled = visible;
            }
        }

        // Cards and rows are instantiated during play, so the list is rebuilt on a timer.
        private void Rescan()
        {
            _nextRescan = Time.unscaledTime + rescanSeconds;
            _animators.Clear();
            _firstGraphics.Clear();
            _parentGroups.Clear();
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                Graphic graphic = animator.GetComponentInChildren<Graphic>(true);
                if (graphic == null) continue;
                _animators.Add(animator);
                _firstGraphics.Add(graphic);
                _parentGroups.Add(ParentGroups(animator));
            }
        }

        private static CanvasGroup[] ParentGroups(Animator animator)
        {
            var groups = new List<CanvasGroup>();
            for (Transform t = animator.transform.parent; t != null; t = t.parent)
            {
                if (!t.TryGetComponent(out CanvasGroup group)) continue;
                groups.Add(group);
                // A group that ignores its parents ends the alpha chain, same as Unity's own inheritance.
                if (group.ignoreParentGroups) break;
            }
            return groups.ToArray();
        }

        private static float ParentAlpha(CanvasGroup[] groups)
        {
            float alpha = 1f;
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null) alpha *= groups[i].alpha;
            return alpha;
        }
    }
}
