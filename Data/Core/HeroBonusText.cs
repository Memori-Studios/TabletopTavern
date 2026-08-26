using Memori.Localization;

namespace TJ
{
    // Builds the "<bonus name>: <bonus description>" line shown for each of a hero's two battle
    // bonuses. A hero stores only the description keys (Hero.HeroBonusDescription); the matching
    // title key is derived from it by naming convention, so this is the one place that convention
    // lives. Previously duplicated verbatim across PlayPanel, HeroDetailsPanel and HUDPanel.
    public static class HeroBonusText
    {
        private const string DescriptionKeyMarker = "heroBonusDescription";
        private const string TitleKeyMarker = "heroBonusTitle";

        // Index is 0 or 1. Returns an empty string rather than throwing when a hero_overrides.json
        // mod supplies a short or partly-empty heroBonusDescription array - HeroOverrideLoader
        // rejects those, but the display layer must not depend on that for a UI panel not to break.
        public static string Get(Hero hero, int index)
        {
            if (hero.HeroBonusDescription == null) return string.Empty;
            if (index < 0 || index >= hero.HeroBonusDescription.Length) return string.Empty;

            string descriptionKey = hero.HeroBonusDescription[index];
            if (string.IsNullOrEmpty(descriptionKey)) return string.Empty;

            string description = LocalizationManager.Instance.GetText(descriptionKey);
            string titleKey = descriptionKey.Replace(DescriptionKeyMarker, TitleKeyMarker);

            // An unchanged key means the description key doesn't follow the convention (a mod's own
            // invented key), so there is no title sibling to look up. Show the description alone
            // instead of rendering the same string twice as "description: description".
            if (titleKey == descriptionKey) return description;

            return LocalizationManager.Instance.GetText(titleKey) + ": " + description;
        }
    }
}
