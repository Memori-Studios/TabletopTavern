using System;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// Applies the UI Scale setting to every screen-space root canvas by shrinking its reference resolution.
    /// </summary>
    public static class UIScaler
    {
        public const string PrefKey = "UIScale";
        public const float Min = 1f;
        // 150% leaves the map node panels 347 units of height between the top bar and the army panel; nothing fits there.
        public const float Max = 1.25f;
        // The Game Scale stepper offers these; the pref stores the value, so adding a step never shifts a saved choice.
        public static readonly float[] Steps = { 1f, 1.25f };
        public static int StepIndex(float scale) { int best = 0; for (int i = 1; i < Steps.Length; i++) if (Mathf.Abs(Steps[i] - scale) < Mathf.Abs(Steps[best] - scale)) best = i; return best; }
        const float SteamDeckDefault = 1.25f;
        static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        public static bool IsSteamDeck => Memori.Steamworks.SteamStatic.IsSteamDeck;
        public static float Default => IsSteamDeck ? SteamDeckDefault : 1f;

        public static void Apply(float scale)
        {
            scale = Mathf.Clamp(scale, Min, Max);
            // Scale With Screen Size ignores scaleFactor, so the reference resolution is the only lever.
            foreach (CanvasScaler scaler in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (scaler.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace) continue;
                scaler.referenceResolution = ReferenceResolution / scale;
            }
        }
    }
}
