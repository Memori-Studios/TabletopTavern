using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
    /// <summary>
    /// Custom-battle sandbox for trying spells: a mana pool that never runs out, one-second cooldowns
    /// and every registered spell castable from the grimoire. Players toggle it on the custom battle
    /// panel; Tabletop Tavern > Spell Test Mode flips the same PlayerPrefs value in the Editor.
    /// </summary>
    public static class SpellTestMode
    {
        public const int ManaPool = 999;
        public const float CooldownSeconds = 1f;

        private const int GridColumns = 6;
        // Lifts the grid clear of the bottom bar so squad cards never cover it.
        private const float GridRiseAboveHotbar = 160f;

        public const string PrefsKey = "TabletopTavern.SpellTestMode";
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefsKey, 0) == 1;
            set => PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
        }

        /// <summary>Custom battle only. A campaign battle keeps its real numbers even with the toggle on.</summary>
        public static bool Active => Enabled && Memori.SaveData.SaveDataHandler.IsCustomBattle();

        /// <summary>
        /// A dimmed panel with a grid layout, stacked above the spell bar's top-left corner so it
        /// follows the hotbar and stays clear of the squad cards, the hover info panel and the custom
        /// battle spawn panel. Cell size comes from the hotbar button it will hold clones of.
        /// </summary>
        public static RectTransform CreateGrid(RectTransform cellTemplate)
        {
            GameObject panel = new("Spell Test Grid", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(cellTemplate.parent, false);
            // The spell bar is a HorizontalLayoutGroup; this must not be laid out as a fifth slot.
            panel.GetComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, GridRiseAboveHotbar);

            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            GridLayoutGroup grid = panel.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellTemplate.sizeDelta;
            grid.spacing = new Vector2(5f, 5f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.childAlignment = TextAnchor.LowerLeft;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }
    }
}
