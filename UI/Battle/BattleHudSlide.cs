using System;
using System.Collections;
using System.Collections.Generic;
using Memori.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// Slides every battle HUD panel off its nearest screen edge and back, for the Hide UI key. Panels move by an
    /// offset added to wherever they sit, so code that places a panel while it is away is kept.
    /// </summary>
    public class BattleHudSlide
    {
        public const float OutTime = 0.25f;
        public const float InTime = 0.3f;
        // Extra distance past the edge, so drop shadows and glows leave the screen too.
        private const float Margin = 40f;
        private const float MaxStep = 1f / 30f;

        private sealed class Panel
        {
            public RectTransform Rect;
            public Vector2 Out;
            public Vector2 Applied;
        }

        private readonly RectTransform _root;
        private readonly Transform[] _skip;
        private readonly List<Panel> _panels = new();
        private static readonly Vector3[] Corners = new Vector3[4];
        // 0 = every panel in place, 1 = every panel off screen.
        private float _amount;

        public BattleHudSlide(RectTransform root, params Transform[] skip)
        {
            _root = root;
            _skip = skip;
        }

        /// <summary>Starts from wherever the last slide stopped, so pressing the key mid-slide turns it around smoothly.</summary>
        public IEnumerator Slide(bool hide)
        {
            if (hide && _panels.Count == 0) Gather(_root);
            float from = _amount;
            float to = hide ? 1f : 0f;
            float duration = (hide ? OutTime : InTime) * Mathf.Abs(to - from);
            // A long frame (a battle still loading) advances one short step, so the motion is never skipped.
            for (float t = 0f; t < 1f && duration > 0f; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep) / duration)
            {
                // Leaving speeds up, arriving settles.
                float e = hide ? t * t : UIJuice.EaseOutCubic(t);
                Apply(Mathf.Lerp(from, to, e));
                yield return null;
            }
            Apply(to);
            if (!hide) _panels.Clear();
        }

        /// <summary>Puts every panel off screen at once, behind a transition that will reveal them with a slide in.</summary>
        public void SnapOut()
        {
            if (_panels.Count == 0) Gather(_root);
            Apply(1f);
        }

        private void Apply(float amount)
        {
            _amount = amount;
            foreach (Panel panel in _panels)
            {
                if (panel.Rect == null) continue;
                Vector2 offset = panel.Out * amount;
                panel.Rect.anchoredPosition += offset - panel.Applied;
                panel.Applied = offset;
            }
        }

        // Full-screen containers are opened up, so each corner piece inside leaves by its own edge.
        private void Gather(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy || Array.IndexOf(_skip, child) >= 0) continue;
                if (child is not RectTransform rect) continue;
                if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
                {
                    Gather(rect);
                    continue;
                }
                _panels.Add(new Panel { Rect = rect, Out = OffNearestEdge(rect) });
            }
        }

        // The panel's own rect picks the edge (a long dropdown inside must not drag it downward); its children only set
        // how far it travels. Vertical edges win a tie, so a full-width bar leaves up or down, never sideways.
        private Vector2 OffNearestEdge(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            Vector3 min = _root.InverseTransformPoint(Corners[0]);
            Vector3 max = _root.InverseTransformPoint(Corners[2]);
            Rect area = _root.rect;
            float up = area.yMax - max.y;
            float down = min.y - area.yMin;
            float left = min.x - area.xMin;
            float right = area.xMax - max.x;
            float nearest = Mathf.Min(Mathf.Min(up, down), Mathf.Min(left, right));

            Rect b = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Encapsulate(rect, ref b);

            Vector2 move;
            if (nearest == up) move = new Vector2(0f, area.yMax - b.min.y + Margin);
            else if (nearest == down) move = new Vector2(0f, area.yMin - b.max.y - Margin);
            else if (nearest == left) move = new Vector2(area.xMin - b.max.x - Margin, 0f);
            else move = new Vector2(area.xMax - b.min.x + Margin, 0f);

            return rect.parent.InverseTransformVector(_root.TransformVector(move));
        }

        // A mask's children count only as far as the mask, so a squad card's oversized faction art does not stretch the trip.
        private void Encapsulate(RectTransform rect, ref Rect bounds)
        {
            if (!rect.gameObject.activeInHierarchy) return;
            rect.GetWorldCorners(Corners);
            foreach (Vector3 corner in Corners)
            {
                Vector2 p = _root.InverseTransformPoint(corner);
                bounds.xMin = Mathf.Min(bounds.xMin, p.x);
                bounds.xMax = Mathf.Max(bounds.xMax, p.x);
                bounds.yMin = Mathf.Min(bounds.yMin, p.y);
                bounds.yMax = Mathf.Max(bounds.yMax, p.y);
            }
            if (rect.TryGetComponent(out Mask _) || rect.TryGetComponent(out RectMask2D _)) return;
            foreach (Transform child in rect)
                if (child is RectTransform childRect) Encapsulate(childRect, ref bounds);
        }
    }
}
