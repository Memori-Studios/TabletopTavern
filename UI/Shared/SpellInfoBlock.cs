using Memori.Localization;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// The faction-coloured "what this unit casts" block: race rail, gradient wash, tinted spell
    /// icon, spell name and description.
    ///
    /// Shared so a mage's spell paints identically wherever it appears. Every field is optional, so
    /// a surface that only wants part of it (no icon, no tooltip) simply leaves those unassigned
    /// rather than needing its own variant.
    /// </summary>
    public class SpellInfoBlock : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image icon;
        [Tooltip("The glow disc behind the icon. Tinted to the faction, not left on its authored colour.")]
        [SerializeField] private Image accentImage;
        [SerializeField] private Image raceRailImage;
        [SerializeField] private Image raceGradientImage;
        [SerializeField] private MemoriTooltipTrigger tooltipTrigger;

        // Matches SpellLoadoutSlot / SpellBrowseSlot, so a spell reads the same here, in the
        // grimoire, in its loadout slot and on the battle hotbar.
        private const float RAIL_ALPHA = 0.9f;

        /// <summary>Localized spell name from the last successful <see cref="Load"/>.</summary>
        public string SpellName { get; private set; }

        /// <summary>
        /// Paints the block for a unit, or hides it and returns false for anything that is not a
        /// caster. The caller uses the return value to drive whatever else it shows alongside.
        /// </summary>
        public bool Load(UnitName unitName, UnitType unitType)
        {
            // Gate on the predicate rather than a bare unitType comparison, so a future caster
            // type is covered for free.
            if (!TabletopTavernConstants.Casts(unitType)) return Hide();

            if (!TabletopTavernData.Instance.SquadAssetsDictionary.TryGetValue(unitName, out SquadAssets assets))
                return Hide();

            TJ.Spells.SpellData spell = assets.mageSpell;
            // EntityWatcher already logs this authoring error loudly at spawn, so stay quiet here.
            if (spell == null) return Hide();

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            SpellName = LocalizationManager.Instance.GetText(spell.Spell.ToString());
            // The block has its own spell tooltip, which lists the keywords, so the text is not hoverable.
            string description = KeywordText.Render(spell.GetLocalizedSpellDescription(), false);

            if (titleText != null)
                titleText.text = $"{LocalizationManager.Instance.GetText("Spell")} - {SpellName}";
            if (descriptionText != null) descriptionText.text = description;

            // Display pair, not the passive pair: the icons are white sprites and the passive
            // colours are banner fills, four of which are too dark to read as a glyph.
            Color factionColour = ColorData.GetRaceDisplayColor(spell.Race);

            if (icon != null)
            {
                icon.sprite = spell.SpellSprite;
                icon.color = factionColour;
            }
            if (accentImage != null) accentImage.color = factionColour;
            if (raceRailImage != null)
                raceRailImage.color = ColorData.WithAlpha255(factionColour, RAIL_ALPHA * 255f);
            if (raceGradientImage != null)
                raceGradientImage.color = ColorData.GetRaceDisplayTint(spell.Race);

            if (tooltipTrigger != null)
                tooltipTrigger.SetContentProvider(() => Spells.SpellTooltip.Build(spell));

            return true;
        }

        private bool Hide()
        {
            SpellName = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return false;
        }
    }
}
