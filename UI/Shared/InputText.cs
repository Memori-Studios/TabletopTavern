using System.Collections.Generic;
using Memori.Input;
using Memori.Localization;

namespace TJ
{
    /// <summary>
    /// Hint text that names a mouse button or key. On a gamepad it reads the key's "_Pad" twin, which may use
    /// {0} A, {1} X, {2} the left stick, {3} and {4} the shoulder buttons.
    /// </summary>
    public static class InputText
    {
        public const string PadSuffix = "_Pad";

        // Tutorial steps whose words name a key or mouse button; the rest read the same on any device.
        private static readonly HashSet<string> TutorialKeysWithPadTwin = new()
        {
            "tutorialStep10Desc", "tutorialStep11Desc", "tutorialStep13Desc", "tutorialStep14Desc", "tutorialStep15Desc", "tutorialStep39Desc",
        };

        public static string Get(string key)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            if (!InputDevices.UsingGamepad) return localization.GetText(key);
            return string.Format(localization.GetText(key + PadSuffix),
                InputGlyphs.ForMouse(0), InputGlyphs.ForMouse(1),
                InputGlyphs.PadLabel("<Gamepad>/leftStick"),
                InputGlyphs.PadLabel("<Gamepad>/leftShoulder"), InputGlyphs.PadLabel("<Gamepad>/rightShoulder"));
        }

        /// <summary>A tutorial step's text, in pad words when it has a twin and a pad is in use.</summary>
        public static string Tutorial(string key)
        {
            return TutorialKeysWithPadTwin.Contains(key) ? Get(key) : LocalizationManager.Instance.GetText(key);
        }
    }
}
