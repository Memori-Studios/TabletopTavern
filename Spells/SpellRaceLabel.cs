using Memori.Localization;
using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// Faction display name for the spell UI.
    ///
    /// Race.Special reads as "Common" here rather than "Special". The rename is scoped to spells on
    /// purpose: in the spell pool that enum value is only ever the four Lesser spells, but the same
    /// value covers the Gate and Archers of Apollo elsewhere in the game, where "Common" would be
    /// wrong. Renaming the Race table entry itself would leak into those.
    /// </summary>
    public static class SpellRaceLabel
    {
        public static string Get(Race race)
        {
            // Reuses the existing rarity key rather than adding one. Worth knowing if a translation
            // ever reads oddly: some locales inflect an adjective differently as a band heading than
            // as a rarity tier, and this shares one string with the rarity UI.
            string key = race == Race.Special ? "Common" : race.ToString();

            // The Editor preview reaches this outside Play mode, where LocalizationManager.Instance
            // would auto-create a phantom manager GameObject in the open scene.
            return Application.isPlaying ? LocalizationManager.Instance.GetText(key) : key;
        }
    }
}
