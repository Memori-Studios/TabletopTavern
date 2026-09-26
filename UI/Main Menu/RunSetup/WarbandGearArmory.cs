using System;
using System.Collections.Generic;
using System.Linq;
using Memori.Localization;
using Memori.Notifications;
using Memori.SaveData;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The warband armory: every gear item as a Collection tile, grouped by rarity with the price on
    /// each group. Items the warband cannot afford are dimmed; details live in each tile's tooltip.
    /// </summary>
    public class WarbandGearArmory : MonoBehaviour
    {
        [Serializable]
        private struct RarityGroup
        {
            public GearRarity rarity;
            public CollectionGroupHeader header;
            public TMP_Text price;
            public RectTransform grid;
        }

        [SerializeField] private WarbandGearTile tilePrefab;
        [SerializeField] private RarityGroup[] groups;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text budgetLabelText;
        [SerializeField] private TMP_Text budgetValueText;
        [SerializeField] private TMP_Text budgetNoteText;

        private StartingArmyManager manager;
        private readonly List<WarbandGearTile> tiles = new();

        public void SetUp(StartingArmyManager _manager)
        {
            manager = _manager;
            if (isActiveAndEnabled) Build();
            Refresh();
        }

        // Tiles are built on first show, never under an inactive parent.
        private void OnEnable()
        {
            if (manager == null) return;
            Build();
            Refresh();
        }

        private void Build()
        {
            if (tiles.Count > 0) return;

            HashSet<int> found = new(SaveDataHandler.GetGearIDsCollected());
            HashSet<int> seen = new(SaveDataHandler.GetGearIDsAcknowledged());
            // Rarest first, like the Collection; OrderByDescending is stable, so each rarity keeps its authored order.
            GearID[] allGear = GearData.GetGearIDs().OrderByDescending(g => GearData.GetGear(g).GearRarity).ToArray();

            foreach (RarityGroup group in groups)
            {
                int total = 0, owned = 0;
                foreach (GearID gearID in allGear)
                {
                    if (GearData.GetGear(gearID).GearRarity != group.rarity) continue;
                    bool isFound = found.Contains((int)gearID);
                    total++;
                    if (isFound) owned++;

                    WarbandGearTile tile = Instantiate(tilePrefab, group.grid);
                    GearID id = gearID;
                    tile.Set(id, isFound, !seen.Contains((int)id), () => TooltipFor(id), OnTileClicked);
                    tiles.Add(tile);
                }
                group.header.Set((Color)ColorData.GetGearRarityColor(group.rarity), T(group.rarity.ToString()),
                    string.Format(T("CollectionFoundCount"), owned, total));
            }
        }

        public void Refresh()
        {
            if (manager == null) return;

            GearID equipped = manager.StartingGearID;
            int budget = manager.GearBudget;
            foreach (WarbandGearTile tile in tiles)
                tile.SetState(tile.GearID == equipped, manager.GearCost(tile.GearID) <= budget);

            foreach (RarityGroup group in groups)
                group.price.text = PriceText(manager.GearCost(group.rarity), budget);

            titleText.text = T("WarbandGearTitle");
            hintText.text = T("WarbandGearHint");
            budgetLabelText.text = T("WarbandGearBudget");
            budgetValueText.text = $"{budget} <sprite name=GoldSprite>";
            budgetNoteText.text = equipped == GearID.None
                ? string.Empty
                : string.Format(T("WarbandGearBudgetNote"), manager.remainingTreasury.Value, manager.GearCost(equipped), T(equipped + "Name"));
        }

        private void OnTileClicked(GearID gearID)
        {
            WarbandGearTile tile = tiles.Find(t => t.GearID == gearID);
            if (tile == null || !tile.Found)
            {
                NotificationManager.Instance.ErrorNotification(T("Gear Not Discoverd"));
                return;
            }

            manager.EquipGear(gearID == manager.StartingGearID ? GearID.None : gearID);
            tile.RefreshTooltip();
        }

        private Memori.Tooltip.TooltipContent TooltipFor(GearID gearID)
        {
            WarbandGearTile tile = tiles.Find(t => t.GearID == gearID);
            bool isFound = tile != null && tile.Found;
            int cost = manager.GearCost(gearID);
            GearID equipped = manager.StartingGearID;

            string footer;
            if (gearID == equipped)
            {
                footer = T("WarbandGearClickToRemove");
            }
            else
            {
                footer = T("WarbandGearClickToEquip");
                if (equipped != GearID.None)
                    footer += "\n" + string.Format(T("WarbandGearReplaces"), T(equipped + "Name"));
                int shortfall = cost - manager.GearBudget;
                if (shortfall > 0)
                    footer += $"\n<color={ColorData.Error}>{string.Format(T("WarbandGearNeedGold"), shortfall)}</color>";
            }
            return WarbandGearTile.BuildTooltip(gearID, isFound, cost, isFound ? footer : string.Empty);
        }

        private static string PriceText(int cost, int budget)
        {
            string colour = cost > budget ? ColorData.Error : ColorData.Gold;
            return $"<color={colour}>{cost}</color> <sprite name=GoldSprite>";
        }

        private static string T(string key) => LocalizationManager.Instance.GetText(key);
    }
}
