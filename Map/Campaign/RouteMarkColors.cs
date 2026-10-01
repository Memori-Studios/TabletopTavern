using System;
using Memori.Utilities;
using UnityEngine;

namespace TJ.Map
{
    /// <summary>The colours a player can give their route marks, and the one they picked.</summary>
    public static class RouteMarkColors
    {
        // The pick is stored by index in PlayerPrefs, so entries are append-only.
        public static readonly Color[] Palette =
        {
            new Color32(0xD9, 0x33, 0x29, 0xFF), // Red
            new Color32(0xF0, 0x8A, 0x24, 0xFF), // Orange
            new Color32(0x4F, 0xC3, 0x4A, 0xFF), // Green
            new Color32(0x2E, 0xC4, 0xB6, 0xFF), // Teal
            new Color32(0x29, 0x8F, 0xD9, 0xFF), // Blue
            new Color32(0x9B, 0x5B, 0xD9, 0xFF), // Purple
            new Color32(0xE8, 0x5A, 0xA8, 0xFF), // Pink
        };
        // Hue in degrees of each colour's glow on the map. ACES pushes bright red and orange toward gold, so those two
        // start nearer magenta to land on the swatch's hue.
        private static readonly float[] GlowHues = { 351f, 16f, 118f, 174f, 205f, 271f, 327f };
        private const string PrefKey = "RouteMarkColor";
        private const int RedIndex = 0, BlueIndex = 4;
        // Just past the map's 1.75 bloom threshold, so the marks glow a little and keep their colour.
        private const float GlowPeak = 1.8f;

        public static event Action Changed;

        /// <summary>The picked colour. Until the player picks, red, or blue in Colorblind Mode, where red reads close to the travelled gold.</summary>
        public static int Index
        {
            get
            {
                int fallback = ColorVision.IsOn ? BlueIndex : RedIndex;
                int index = PlayerPrefs.GetInt(PrefKey, fallback);
                return index >= 0 && index < Palette.Length ? index : fallback;
            }
            set
            {
                if (value < 0 || value >= Palette.Length || (PlayerPrefs.HasKey(PrefKey) && value == Index)) return;
                PlayerPrefs.SetInt(PrefKey, value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static Color Current => Palette[Index];

        /// <summary>The picked colour as HDR, bright enough for the map camera's bloom to catch.</summary>
        public static Color Glow => Color.HSVToRGB(GlowHues[Index] / 360f, 1f, GlowPeak, true);
    }
}
