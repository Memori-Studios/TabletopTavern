using System;
using System.Collections.Generic;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// The fixed panel on the right of the Collection. It shows the picked gear, potion, unit, hero or faction,
    /// so nothing in the Collection needs a hover to be read.
    /// </summary>
    public class CollectionDetailPanel : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;

        [Header("Item")]
        [SerializeField] private GameObject itemView;
        [SerializeField] private Image itemGlow;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private CollectionChip itemRarity;
        [SerializeField] private TMP_Text itemKind;
        [SerializeField] private TMP_Text itemBody;
        [SerializeField] private TMP_Text itemFlavour;

        [Header("Unit")]
        [SerializeField] private GameObject unitView;
        [SerializeField] private TMP_Text unitName;
        [SerializeField] private Image unitTypeIcon;
        [SerializeField] private TMP_Text unitType;
        [SerializeField] private CollectionChip unitTier;
        [SerializeField] private TMP_Text unitNotFound;
        [SerializeField] private RectTransform unitChips;
        [SerializeField] private TMP_Text[] unitStripValues;
        [SerializeField] private TMP_Text[] unitStripLabels;
        [SerializeField] private TMP_Text unitStatsLabel;
        [SerializeField] private Transform unitStats;
        [SerializeField] private GameObject unitSpellRow;
        [SerializeField] private Image unitSpellIcon;
        [SerializeField] private TMP_Text unitSpellLabel;
        [SerializeField] private TMP_Text unitSpellName;
        [SerializeField] private TMP_Text unitFactionLabel;
        [SerializeField] private TMP_Text unitFactionText;
        [SerializeField] private MemoriTooltipTrigger unitFactionTooltip;

        [Header("Hero")]
        [SerializeField] private GameObject heroView;
        [SerializeField] private TMP_Text heroName;
        [SerializeField] private TMP_Text heroEyebrow;
        [SerializeField] private GameObject heroLockedRow;
        [SerializeField] private CollectionChip heroLocked;
        [SerializeField] private TMP_Text heroLockedText;
        [SerializeField] private TMP_Text[] heroStripValues;
        [SerializeField] private TMP_Text[] heroStripLabels;
        [SerializeField] private TMP_Text heroEffectsLabel;
        [SerializeField] private Transform heroEffects;
        [SerializeField] private CollectionMiniUnit heroSignatureUnit;
        [SerializeField] private TMP_Text heroSignatureLabel;
        [SerializeField] private TMP_Text heroSignatureName;
        [SerializeField] private Button heroSignatureLink;
        [SerializeField] private TMP_Text heroSignatureLinkText;
        [SerializeField] private GameObject heroSpellRow;
        [SerializeField] private Image heroSpellIcon;
        [SerializeField] private TMP_Text heroSpellLabel;
        [SerializeField] private TMP_Text heroSpellName;
        [SerializeField] private TMP_Text heroArmyLabel;
        [SerializeField] private Transform heroArmy;

        [Header("Faction")]
        [SerializeField] private GameObject factionView;
        [SerializeField] private TMP_Text factionEffectsLabel;
        [SerializeField] private Transform factionEffects;
        [SerializeField] private TMP_Text commandersLabel;
        [SerializeField] private Transform commanders;
        [SerializeField] private TMP_Text factionFootLabel;
        [SerializeField] private TMP_Text factionFootValue;

        [Header("Templates")]
        [SerializeField] private CollectionStatRow statRowTemplate;
        [SerializeField] private CollectionChip chipTemplate;
        [SerializeField] private HorizontalLayoutGroup chipRowTemplate;
        [SerializeField] private CollectionEffectBlock effectTemplate;
        [SerializeField] private CollectionMiniUnit miniUnitTemplate;
        [SerializeField] private CollectionCommanderRow commanderTemplate;

        [Header("Colours")]
        [SerializeField] private Color gold = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color body = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color muted = new(0.56f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color brass = new(0.69f, 0.54f, 0.24f, 1f);
        [SerializeField] private Color traitColour = new(0.85f, 0.77f, 0.56f, 1f);
        [SerializeField] private Color lockedColour = new(0.85f, 0.53f, 0.42f, 1f);
        [SerializeField] private Color lockedIcon = new(0f, 0f, 0f, 0.55f);

        // Highest value each stat reaches across every faction roster, so bars compare across the whole game.
        private static Dictionary<UnitStat, float> _statMax;

        private readonly List<CollectionStatRow> _statRows = new();
        private readonly List<CollectionEffectBlock> _heroEffectBlocks = new();
        private readonly List<CollectionEffectBlock> _factionEffectBlocks = new();
        private readonly List<CollectionCommanderRow> _commanderRows = new();

        private static string T(string key) => LocalizationManager.Instance.GetText(key);

        /// <summary>Fills every fixed label. Call once after the panel is built.</summary>
        public void Localize()
        {
            unitStatsLabel.text = T("Stats");
            unitStripLabels[0].text = T("CollectionSquadSize");
            unitStripLabels[1].text = T("CollectionTotalHealth");
            unitStripLabels[2].text = T("CollectionYourKills");
            unitSpellLabel.text = T("Spell");
            unitFactionLabel.text = T("Campaign Bonus");
            unitNotFound.text = T("CollectionUnitNotFound");
            heroStripLabels[0].text = T("Treasury");
            heroStripLabels[1].text = T("CollectionStartingArmy");
            heroEffectsLabel.text = T("Hero Effects");
            heroSignatureLabel.text = T("SignatureUnit");
            heroSignatureLinkText.text = T("CollectionViewInRoster") + " ›";
            heroSpellLabel.text = T("CollectionSignatureSpell");
            heroArmyLabel.text = T("CollectionStartingArmy");
            factionEffectsLabel.text = T("CollectionFactionEffects");
            commandersLabel.text = T("Heroes");
            factionFootLabel.text = T("CollectionRoster");
        }

        private void Show(GameObject view)
        {
            itemView.SetActive(view == itemView);
            unitView.SetActive(view == unitView);
            heroView.SetActive(view == heroView);
            factionView.SetActive(view == factionView);
            scroll.verticalNormalizedPosition = 1f;
        }

        #region Items

        public void ShowGear(GearID id, bool found)
        {
            Gear gear = GearData.GetGear(id);
            Color rarity = (Color)ColorData.GetGearRarityColor(gear.GearRarity);
            string text = T("Obtain in Campaign");
            if (found) text = string.Format(T(id + "Desc"), gear.GearModifierValue);
            ShowItem(SpriteData.GetSprite(gear.GearName), found ? T(id + "Name") : T("CollectionNotFound"),
                rarity, T(gear.GearRarity.ToString()), T("CollectionGear"), text, found ? T(id + "Flavor") : string.Empty, found);
        }

        public void ShowPotion(ConsumableEnum id, bool found)
        {
            Consumable potion = ConsumableData.GetConsumable(id);
            Color rarity = (Color)ColorData.GetRarityTierColor((UnitRarity)(int)potion.ConsumableRarity);
            string text = found ? ConsumableData.FormatDescription(id, T(id + "Desc")) : T("Obtain in Campaign");
            ShowItem(SpriteData.GetSprite(id.ToString()), found ? T(id + "Name") : T("CollectionNotFound"),
                rarity, T(potion.ConsumableRarity.ToString()), T("CollectionPotion"), text, string.Empty, found);
        }

        private void ShowItem(Sprite sprite, string name, Color rarity, string rarityName, string kind, string text, string flavour, bool found)
        {
            Show(itemView);
            itemIcon.sprite = sprite;
            itemIcon.color = found ? Color.white : lockedIcon;
            itemGlow.color = found ? new Color(rarity.r, rarity.g, rarity.b, 0.3f) : Color.clear;
            itemName.text = name;
            itemName.color = found ? gold : muted;
            itemRarity.gameObject.SetActive(found);
            if (found) itemRarity.Set(rarityName, rarity);
            itemKind.text = kind;
            KeywordText.Apply(itemBody, text);
            itemBody.color = found ? body : muted;
            itemFlavour.text = flavour;
            itemFlavour.gameObject.SetActive(!string.IsNullOrEmpty(flavour));
        }

        #endregion

        #region Units

        public void ShowUnit(UnitName unit, Race race, bool found)
        {
            Show(unitView);
            SquadStats stats = TabletopTavernData.Instance.GetSquadStats(unit);
            unitName.text = T(unit.ToString());
            unitTypeIcon.sprite = TabletopTavernData.Instance.GetSquadTypeIcon(unit);
            string size = stats.unitSize != UnitSize.Artillery && stats.unitType != UnitType.Structure ? " " + T(stats.unitSize.ToString()) : string.Empty;
            unitType.text = T(stats.unitType.ToString()) + size;
            unitTier.Set(T(stats.RarityTier.ToString()), (Color)ColorData.GetRarityTierColor(stats.RarityTier));
            unitNotFound.gameObject.SetActive(!found);

            BuildChips(unit);

            unitStripValues[0].text = stats.baseUnitCount.ToString();
            unitStripValues[1].text = (stats.baseUnitCount * stats.HitPointsPerUnit).ToString("N0");
            unitStripValues[2].text = SaveDataHandler.GetUnitNameHistoricalKillCount(unit).ToString("N0");

            BuildStats(unit);

            TJ.Spells.SpellData spell = null;
            if (TabletopTavernConstants.Casts(stats.unitType) && TabletopTavernData.Instance.SquadAssetsDictionary.TryGetValue(unit, out SquadAssets assets))
                spell = assets.mageSpell;
            unitSpellRow.SetActive(spell != null);
            if (spell != null)
            {
                unitSpellIcon.sprite = spell.SpellSprite;
                unitSpellName.text = T(spell.Spell.ToString());
            }

            string passive = T(race + "PassiveName");
            string passiveText = RacePassiveInfo.GetDescription(race);
            unitFactionText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(gold)}>{passive}.</color> {KeywordText.Render(passiveText, false)}";
            unitFactionTooltip.SetUpToolTip(passive, KeywordText.ForTooltip(passiveText));
        }

        private void BuildChips(UnitName unit)
        {
            foreach (Transform old in unitChips)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            List<UnitAttribute> attributes = TabletopTavernData.Instance.GetUnitAttributesForDisplay(unit);
            unitChips.gameObject.SetActive(attributes.Count > 0);
            float available = unitChips.rect.width > 0f ? unitChips.rect.width : 370f;

            RectTransform row = null;
            float used = 0f;
            foreach (UnitAttribute attribute in attributes)
            {
                if (row == null) row = NewChipRow();
                string label = T(attribute.ToString());
                CollectionChip chip = Instantiate(chipTemplate, row);
                chip.gameObject.SetActive(true);
                chip.Set(label, traitColour, label, KeywordText.ForTooltip(T(attribute + "Desc"), attribute.ToString()));
                RectTransform chipRect = (RectTransform)chip.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(chipRect);
                float width = LayoutUtility.GetPreferredWidth(chipRect);
                if (used > 0f && used + width > available)
                {
                    row = NewChipRow();
                    chip.transform.SetParent(row, false);
                    used = 0f;
                }
                used += width + chipRowTemplate.spacing;
            }
        }

        private RectTransform NewChipRow()
        {
            HorizontalLayoutGroup row = Instantiate(chipRowTemplate, unitChips);
            row.gameObject.SetActive(true);
            return (RectTransform)row.transform;
        }

        private void BuildStats(UnitName unit)
        {
            List<UnitStatValue> values = TabletopTavernData.Instance.GetUnitStatsForDisplay(unit);
            EnsureStatMax();
            while (_statRows.Count < values.Count)
            {
                CollectionStatRow row = Instantiate(statRowTemplate, unitStats);
                _statRows.Add(row);
            }
            for (int i = 0; i < _statRows.Count; i++)
            {
                bool used = i < values.Count;
                _statRows[i].gameObject.SetActive(used);
                if (!used) continue;
                UnitStat stat = values[i].unitStat;
                float max = _statMax.TryGetValue(stat, out float highest) && highest > 0f ? highest : 1f;
                _statRows[i].Set(SpriteData.GetSprite(stat.ToString()), (Color)ColorData.GetUnitStatColor(stat),
                    T(stat.ToString()), values[i].Value, values[i].Value / max, KeywordText.Render(T(stat + "Desc"), false));
            }
        }

        private static void EnsureStatMax()
        {
            if (_statMax != null) return;
            _statMax = new Dictionary<UnitStat, float>();
            foreach (Race race in Enum.GetValues(typeof(Race)))
            {
                if (race == Race.Special) continue;
                foreach (UnitName unit in TabletopTavernData.Instance.GetUnitsOfRace(race))
                    foreach (UnitStatValue value in TabletopTavernData.Instance.GetUnitStatsForDisplay(unit))
                        if (!_statMax.TryGetValue(value.unitStat, out float highest) || value.Value > highest)
                            _statMax[value.unitStat] = value.Value;
            }
        }

        #endregion

        #region Heroes

        public void ShowHero(Hero hero, Action<UnitName> onSignatureUnit)
        {
            Show(heroView);
            heroName.text = T(hero.HeroName);
            heroEyebrow.text = $"{T("Leader")} · {T(hero.Race.ToString())}";

#if DEMO
            UnlockCondition condition = hero.DemoUnlockCondition;
#else
            UnlockCondition condition = hero.UnlockCondition;
#endif
            bool unlocked = SaveDataHandler.IsUnlockConditionUnlocked(condition, hero.HeroID);
            heroLockedRow.SetActive(!unlocked);
            if (!unlocked)
            {
                heroLocked.Set(T("Locked"), lockedColour);
                heroLockedText.text = HeroBonusManager.GetLocalizedHeroUnlockDescription(hero, condition);
            }

            UnitName[] army = hero.StartingArmyUnits ?? Array.Empty<UnitName>();
            heroStripValues[0].text = $"{hero.StartingGold} <sprite name=GoldSprite>";
            heroStripValues[1].text = army.Length.ToString();

            for (int i = 0; i < 2; i++)
            {
                string line = HeroBonusText.Get(hero, i);
                CollectionEffectBlock.Split(line, out string name, out string text);
                CollectionEffectBlock block = Block(_heroEffectBlocks, heroEffects, i);
                block.gameObject.SetActive(!string.IsNullOrEmpty(line));
                block.Set(name, null, text);
            }

            UnitName signature = hero.SignatureUnit;
            heroSignatureUnit.Set(TabletopTavernData.Instance.GetUnitIcon(signature), TierColour(signature));
            heroSignatureName.text = T(signature.ToString());
            heroSignatureLink.onClick.RemoveAllListeners();
            heroSignatureLink.onClick.AddListener(() => onSignatureUnit(signature));

#if SPELLS
            TJ.Spells.SpellData spell = TJ.Spells.SpellRegistry.Get(hero.SignatureSpell);
            heroSpellRow.SetActive(spell != null);
            if (spell != null)
            {
                heroSpellIcon.sprite = spell.SpellSprite;
                heroSpellName.text = T(spell.Spell.ToString());
            }
#else
            heroSpellRow.SetActive(false);
#endif

            BuildArmy(army);
        }

        private void BuildArmy(UnitName[] army)
        {
            foreach (Transform child in heroArmy)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            // Repeats collapse into one portrait with a count, in the order the hero brings them.
            var order = new List<UnitName>();
            var counts = new Dictionary<UnitName, int>();
            foreach (UnitName unit in army)
            {
                if (counts.TryGetValue(unit, out int count)) counts[unit] = count + 1;
                else
                {
                    counts[unit] = 1;
                    order.Add(unit);
                }
            }
            foreach (UnitName unit in order)
            {
                CollectionMiniUnit mini = Instantiate(miniUnitTemplate, heroArmy);
                mini.gameObject.SetActive(true);
                mini.Set(TabletopTavernData.Instance.GetUnitIcon(unit), TierColour(unit), counts[unit]);
            }
        }

        #endregion

        #region Faction

        public void ShowFaction(Race race, IReadOnlyList<Hero> heroes, int found, int total, Action<int> onCommander)
        {
            Show(factionView);

            string battle = RacePassiveInfo.GetDescription(race);
            Block(_factionEffectBlocks, factionEffects, 0).Set(T(race + "PassiveName"), T("BattleEffectLabel"), battle);

            string campaign = T(race + "BonusDescription");
            CollectionEffectBlock.Split(campaign, out string campaignName, out string campaignText);
            Block(_factionEffectBlocks, factionEffects, 1).Set(campaignName, T("CampaignEffectLabel"), campaignText);

            while (_commanderRows.Count < heroes.Count)
            {
                CollectionCommanderRow row = Instantiate(commanderTemplate, commanders);
                row.gameObject.SetActive(true);
                _commanderRows.Add(row);
            }
            for (int i = 0; i < _commanderRows.Count; i++)
            {
                bool used = i < heroes.Count;
                _commanderRows[i].gameObject.SetActive(used);
                if (!used) continue;
                int index = i;
                Hero hero = heroes[i];
                _commanderRows[i].Set(T(hero.HeroName), $"{T("Treasury")} {hero.StartingGold} <sprite name=GoldSprite>", brass, () => onCommander(index));
                SetHeroPortrait(_commanderRows[i].Portrait.Portrait, hero.HeroID);
            }

            factionFootValue.text = string.Format(T("CollectionFoundCount"), found, total);
        }

        #endregion

        #region Helpers

        private CollectionEffectBlock Block(List<CollectionEffectBlock> pool, Transform parent, int index)
        {
            while (pool.Count <= index)
            {
                CollectionEffectBlock block = Instantiate(effectTemplate, parent);
                block.gameObject.SetActive(true);
                pool.Add(block);
            }
            return pool[index];
        }

        public static Color TierColour(UnitName unit) =>
            (Color)ColorData.GetRarityTierColor(TabletopTavernData.Instance.GetSquadStats(unit).RarityTier);

        /// <summary>Hero portraits are Addressable sprites cached on their asset reference.</summary>
        public static async void SetHeroPortrait(Image image, int heroID)
        {
            Sprite sprite = await TabletopTavernData.Instance.LoadHeroSpriteAsync(heroID);
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        #endregion
    }
}
