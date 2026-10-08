using System.Collections.Generic;
using Memori.Utilities;
using Shapes;
using UnityEngine;

namespace TJ
{
    /// <summary>Read by the battle's immediate-mode marker drawers; true while a clean view is wanted.</summary>
    public static class BattleMarkers
    {
        public static bool Hidden;
    }

    /// <summary>
    /// Hides everything that is not the battle itself and remembers exactly what it changed, so Restore puts back
    /// that and nothing else. Markers are hidden without touching their active state, which their own code keeps
    /// changing while the battle runs.
    /// </summary>
    public class BattleViewHider
    {
        private readonly List<Canvas> _hiddenCanvases = new();
        private readonly HashSet<Renderer> _hiddenMarkers = new();
        private readonly HashSet<Renderer> _hiddenFlags = new();
        private Canvas _keepCanvas;
        private Canvas _slidingHud;
        private bool _hidden;
        private bool _showFlags;
        private bool _savedCursorVisible;

        /// <param name="slidingHud">The battle HUD root, left on because UIManager slides it away and switches it off itself.</param>
        public void Hide(Canvas keepCanvas, bool showFlags, Canvas slidingHud = null)
        {
            if (_hidden) return;
            _hidden = true;
            _keepCanvas = keepCanvas;
            _slidingHud = slidingHud;
            _showFlags = showFlags;

            HideCanvases();

            _savedCursorVisible = Cursor.visible;
            Cursor.visible = true;
            BattleMarkers.Hidden = true;
            Rescan();
        }

        /// <summary>Catches HUD canvases that switched on since the view was hidden, for a battle that keeps running.</summary>
        public void RescanCanvases()
        {
            if (_hidden) HideCanvases();
        }

        private void HideCanvases()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled || KeepVisible(canvas)) continue;
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        public void SetFlagsVisible(bool showFlags)
        {
            if (!_hidden || showFlags == _showFlags) return;
            _showFlags = showFlags;
            if (showFlags) Release(_hiddenFlags);
            else Rescan();
        }

        /// <summary>Catches markers and flags that appeared since the last scan, such as during a time step.</summary>
        public void Rescan()
        {
            if (!_hidden) return;
            HideAll<ArcherRangeDrawer>(_hiddenMarkers);
            HideAll<GarrisonGateRangeDrawer>(_hiddenMarkers);
            HideAll<AttackArrowDrawer>(_hiddenMarkers);
            HideAll<PositionDrawer>(_hiddenMarkers);
            // A battlefield bonus also owns scenery, so only its drawn rings go.
            foreach (BattlefieldBonusGameObject bonus in Object.FindObjectsByType<BattlefieldBonusGameObject>(FindObjectsSortMode.None))
                foreach (ShapeRenderer shape in bonus.GetComponentsInChildren<ShapeRenderer>(true))
                    if (shape.TryGetComponent(out Renderer ring)) HideRenderer(ring, _hiddenMarkers);
            if (!_showFlags) HideAll<SquadFlagGameObject>(_hiddenFlags);
        }

        /// <param name="sceneClosing">True when the battle scene is being torn down: only state that outlives it is restored.</param>
        public void Restore(bool sceneClosing = false)
        {
            if (!_hidden) return;
            _hidden = false;
            BattleMarkers.Hidden = false;
            Cursor.visible = _savedCursorVisible;
            foreach (Canvas canvas in _hiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            _hiddenCanvases.Clear();
            if (sceneClosing)
            {
                _hiddenMarkers.Clear();
                _hiddenFlags.Clear();
                return;
            }
            Release(_hiddenMarkers);
            Release(_hiddenFlags);
        }

        // The door wipe, Settings and the bug reporter must keep working over a hidden battle.
        private bool KeepVisible(Canvas canvas)
        {
            Canvas root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace) return true;
            if (root == _keepCanvas || root == _slidingHud) return true;
            if (root.sortingOrder >= TransitionSortingOrder) return true;
            if (root.transform.IsChildOf(SettingsManager.Instance.transform)) return true;
            return root.GetComponentInChildren<ReportABugScreen>(true) != null;
        }
        private const int TransitionSortingOrder = 1000;

        private static void HideAll<T>(HashSet<Renderer> record) where T : Component
        {
            foreach (T owner in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
                    HideRenderer(renderer, record);
        }
        private static void HideRenderer(Renderer renderer, HashSet<Renderer> record)
        {
            if (renderer.forceRenderingOff) return;
            renderer.forceRenderingOff = true;
            record.Add(renderer);
        }
        private static void Release(HashSet<Renderer> record)
        {
            foreach (Renderer renderer in record)
                if (renderer != null) renderer.forceRenderingOff = false;
            record.Clear();
        }
    }
}
