using System;
using System.Collections.Generic;
using Memori.Localization;
using Memori.SaveData;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The warband recruit list: the faction's units grouped by rarity, one named row each, with the price on the
    /// group header like the armory. Hovering a row shows Squad Battle Info; its + adds the unit.
    /// Player text names the rarity (Common, Uncommon, Rare), never the tier number.
    /// </summary>
    public class WarbandRecruitList : MonoBehaviour
    {
        [Serializable]
        private struct TierGroup
        {
            public int tier;
            public GameObject root;
            public CollectionGroupHeader header;
            public TMP_Text price;
            public RectTransform rows;
        }

        [SerializeField] private WarbandRecruitRow rowPrefab;
        [SerializeField] private TierGroup[] groups;

        [Header("Budget")]
        [SerializeField] private TMP_Text budgetLabelText;
        [SerializeField] private TMP_Text budgetValueText;
        [SerializeField] private TMP_Text budgetNoteText;

        [Header("Columns")]
        [SerializeField] private TMP_Text sizeColumnText;
        [SerializeField] private TMP_Text costColumnText;

        private StartingArmyManager manager;
        private readonly List<WarbandRecruitRow> rows = new();

        /// <summary>Rebuilds the rows for a faction and fills <paramref name="offered"/> with the units that can be recruited.</summary>
        public void Build(StartingArmyManager _manager, Race race, ICollection<UnitName> discovered, List<UnitName> offered)
        {
            manager = _manager;
            foreach (WarbandRecruitRow old in rows)
            {
                if (old == null) continue;
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            rows.Clear();

            int[] perTier = new int[groups.Length];
            int[] foundPerTier = new int[groups.Length];
            foreach (UnitName unit in TabletopTavernData.Instance.GetUnitsOfRace(race))
            {
                int tier = TabletopTavernData.Instance.GetUnitTierFromUnitName(unit);
                int group = Array.FindIndex(groups, g => g.tier == tier);
                if (group < 0) continue;

                bool found = discovered.Contains(unit);
                if (found)
                {
                    offered.Add(unit);
                    foundPerTier[group]++;
                }

                SquadToLoad squad = new SquadToLoad(unit, 0, 0);
                int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(unit);
                int hitPointsPerUnit = TabletopTavernData.Instance.GetHitPointsPerUnit(unit);
                squad.SquadCurrentHealth = baseUnitCount * hitPointsPerUnit;
                squad.maxUnitCount = baseUnitCount;
                squad.HitPointsPerUnit = hitPointsPerUnit;

                WarbandRecruitRow row = Instantiate(rowPrefab, groups[group].rows);
                row.Set(squad, found, TabletopTavernData.Instance.GetUnitCost(unit), manager.AddTroop, RemoveOne,
                        manager.PointerOverTroop, manager.PointerOffTroop);
                rows.Add(row);
                perTier[group]++;
            }

            for (int i = 0; i < groups.Length; i++)
            {
                UnitRarity rarity = (UnitRarity)(groups[i].tier - 1);
                groups[i].header.Set((Color)ColorData.GetRarityTierColor(rarity), T(rarity.ToString()),
                    string.Format(T("CollectionFoundCount"), foundPerTier[i], perTier[i]));
                groups[i].root.SetActive(perTier[i] > 0);
            }
            Refresh();
        }

        /// <summary>Re-reads the gold left and the army, so prices, dimming and the ×N badges stay current.</summary>
        public void Refresh()
        {
            if (manager == null) return;

            int gold = manager.remainingTreasury.Value;
            SquadToLoad[] army = manager.SelectedArmy;
            bool armyFull = army.Length >= StartingArmyManager.MaxStartingArmySize;

            foreach (WarbandRecruitRow row in rows)
            {
                if (row == null) continue;
                int inArmy = 0;
                foreach (SquadToLoad squad in army)
                    if (squad.UnitName == row.Squad.UnitName) inArmy++;
                row.SetState(inArmy, row.Cost <= gold, armyFull);
            }

            foreach (TierGroup group in groups)
            {
                int cost = TabletopTavernConstants.GetUnitCost(group.tier);
                group.price.text = $"<color={(cost > gold ? ColorData.Error : ColorData.Gold)}>{cost}</color> <sprite name=GoldSprite>";
            }

            budgetLabelText.text = T("WarbandRecruitBudget");
            budgetValueText.text = $"<color={(gold < 0 ? ColorData.Error : ColorData.Gold)}>{gold}</color> <sprite name=GoldSprite>";
            budgetNoteText.text = string.Format(T("WarbandRecruitSquads"), army.Length, StartingArmyManager.MaxStartingArmySize);
            sizeColumnText.text = T("WarbandRecruitColSize");
            costColumnText.text = T("Cost");
        }

        /// <summary>Removes the most recently added squad of this unit, so starting squads go last.</summary>
        private void RemoveOne(SquadToLoad squad)
        {
            SquadToLoad[] army = manager.SelectedArmy;
            for (int i = army.Length - 1; i >= 0; i--)
            {
                if (army[i].UnitName != squad.UnitName) continue;
                manager.RemoveTroop(i);
                return;
            }
        }

        private static string T(string key) => LocalizationManager.Instance.GetText(key);
    }
}
