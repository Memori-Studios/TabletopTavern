using System;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Scale for the in-battle world elements the UI Scale setting does not reach: squad banners and their health bars, unit markers, order arrows and range rings.
    /// </summary>
    public static class BattlefieldMarkerScale
    {
        public const string PrefKey = "BattlefieldMarkersScale";
        public static readonly float[] Steps = { 1f, 1.25f, 1.5f };
        const float SteamDeckDefault = 1.25f;

        public static float Default => UIScaler.IsSteamDeck ? SteamDeckDefault : 1f;
        public static float Current { get; private set; } = 1f;
        public static event Action<float> Changed;

        public static int StepIndex(float scale)
        {
            int best = 0;
            for (int i = 1; i < Steps.Length; i++) if (Mathf.Abs(Steps[i] - scale) < Mathf.Abs(Steps[best] - scale)) best = i;
            return best;
        }

        public static void Apply(float scale)
        {
            scale = Mathf.Clamp(scale, Steps[0], Steps[Steps.Length - 1]);
            if (Mathf.Approximately(Current, scale)) return;
            Current = scale;
            Changed?.Invoke(scale);
        }
    }
}
