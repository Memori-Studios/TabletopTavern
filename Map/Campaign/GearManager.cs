using System.Collections;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Scenes;
using UnityEngine;
using Memori.Localization;

namespace TJ
{
    public class GearManager : MonoBehaviour
    {
        [SerializeField] private List<GearID> activeGearItems = new();
        public void LoadAllGear()
        {
            //dont load if custom battle
            if (SaveDataHandler.LoadPlayerSaveData().customBattle)
            {
                activeGearItems.Clear();
            }
            else
            {
                // Debug.Log($"Loading gear from save file");
                LoadGearFromSaveFile();
            }
        }
        public bool CheckForGear(GearID _gearID)
        {
            return activeGearItems.Contains(_gearID);
        }
        public List<UnitStatBonus> GetGearStatBonus(UnitStat _unitStat, UnitName _requestingUnit, UnitAttribute _prestigeTrait = UnitAttribute.None)
        {
            List<UnitStatBonus> unitStatBonuses = new();

            // if(team.Enemy == DataTypes.GetteamFromUnitName(_requestingUnit)) return unitStatBonuses;

            SquadStats ApplyUnitAttributeBonuesToSquadStats(SquadStats _squadStats)
            {
                List<UnitAttributeBonus> unitAttributeBonuses = GetGearAttributeBonus(_squadStats.unitName, _prestigeTrait);

                foreach(UnitAttributeBonus bonus in unitAttributeBonuses) {
                    switch(bonus.UnitAttribute)
                    {
                        case UnitAttribute.ArmorPiercing:
                            _squadStats.SquadAttributes.ArmorPiercing = true;
                            break;
                        case UnitAttribute.AntiLarge:
                            _squadStats.SquadAttributes.AntiLarge = true;
                            break;
                    }
                }

                // Prestige-granted trait is intrinsic to this squad instance, not a gear bonus,
                // so gear that keys off an attribute (e.g. Glaives -> AntiLarge) must see it too.
                if (_prestigeTrait != UnitAttribute.None)
                    TabletopTavernConstants.SetAttribute(ref _squadStats.SquadAttributes, _prestigeTrait);

                return _squadStats;
            }

            SquadStats squadStats = ApplyUnitAttributeBonuesToSquadStats(TabletopTavernData.Instance.GetSquadStats(_requestingUnit));

            if(activeGearItems.Contains(GearID.ArmingSwords)) {
                if(_unitStat == UnitStat.MeleeAttack && TabletopTavernConstants.FightsInMelee(squadStats.unitType)) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.ArmingSwords.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.ArmingSwords).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.BucklerShields)) {
                if(_unitStat == UnitStat.MeleeDefense && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields)) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.BucklerShields.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.BucklerShields).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.Longbows)) {
                if(_unitStat == UnitStat.Range && squadStats.unitType == UnitType.Ranged) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.Longbows.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.Longbows).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.Glaives)) {
                if(_unitStat == UnitStat.WeaponStrength && squadStats.SquadAttributes.AntiLarge) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.Glaives.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.Glaives).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.ConscriptionOrders)) {
                string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.ConscriptionOrders.ToString()+"Name");
                if(_unitStat == UnitStat.MeleeAttack && squadStats.RarityTier == UnitRarity.Common) {
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue));
                }
                if(_unitStat == UnitStat.MeleeDefense && squadStats.RarityTier == UnitRarity.Common) {
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.TexanBBQ)) {
                if(_unitStat == UnitStat.WeaponStrength && TabletopTavernConstants.FightsInMelee(squadStats.unitType)) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.TexanBBQ.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.TexanBBQ).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.BallisticCharts) && squadStats.unitType == UnitType.Ranged) {
                if(_unitStat == UnitStat.Accuracy) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.BallisticCharts.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.BallisticCharts).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.JoustingLances) && squadStats.unitSize == UnitSize.Cavalry) {
                if(_unitStat == UnitStat.WeaponStrength) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.JoustingLances.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.JoustingLances).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.GnomishArmorers)) {
                if(_unitStat == UnitStat.Armor && squadStats.RarityTier == UnitRarity.Uncommon) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.GnomishArmorers.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.GnomishArmorers).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.WellHonedAxes)) {
                if(_unitStat == UnitStat.MeleeAttack && squadStats.SquadAttributes.ArmorPiercing) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.WellHonedAxes.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.WellHonedAxes).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.RavensEye)) {
                if(_unitStat == UnitStat.Accuracy && squadStats.RarityTier != UnitRarity.Common && squadStats.unitType == UnitType.Ranged) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.RavensEye.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.RavensEye).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.RingoftheElvenKing)) {
                if(_unitStat == UnitStat.MissileStrength && squadStats.unitType == UnitType.Ranged) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.RingoftheElvenKing.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.RingoftheElvenKing).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.QuantitativeEasingPolicy)) {
                if(_unitStat == UnitStat.Leadership) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.QuantitativeEasingPolicy.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.QuantitativeEasingPolicy).GearModifierValue));
                }
            }
            if(activeGearItems.Contains(GearID.Shungite)) {
                if(_unitStat == UnitStat.MeleeAttack && squadStats.RarityTier == UnitRarity.Uncommon) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.Shungite.ToString()+"Name");
                    unitStatBonuses.Add(new UnitStatBonus(_unitStat, gearNameLocalized, GearData.GetGear(GearID.Shungite).GearModifierValue));
                }
            }

            return unitStatBonuses;
        }
        public List<UnitAttributeBonus> GetGearAttributeBonus(UnitName _requestingUnit, UnitAttribute _prestigeTrait = UnitAttribute.None)
        {
            List<UnitAttributeBonus> unitAttributeBonuses = new();

            SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(_requestingUnit);
            if (_prestigeTrait != UnitAttribute.None)
                TabletopTavernConstants.SetAttribute(ref squadStats.SquadAttributes, _prestigeTrait);

            if(activeGearItems.Contains(GearID.DiamondTippedArrows)) {
                if(squadStats.unitType == UnitType.Ranged && !squadStats.SquadAttributes.ArmorPiercing) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.DiamondTippedArrows.ToString()+"Name");

                    unitAttributeBonuses.Add(new UnitAttributeBonus(UnitAttribute.ArmorPiercing, gearNameLocalized, GearData.GetGear(GearID.DiamondTippedArrows).GearModifierValue));
                    squadStats.SquadAttributes.ArmorPiercing = true;
                }
            }
            if(activeGearItems.Contains(GearID.Turkey)) {
                if(squadStats.unitType == UnitType.Ranged && !squadStats.SquadAttributes.AntiLarge) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.Turkey.ToString()+"Name");

                    unitAttributeBonuses.Add(new UnitAttributeBonus(UnitAttribute.AntiLarge, gearNameLocalized, GearData.GetGear(GearID.Turkey).GearModifierValue));
                    squadStats.SquadAttributes.AntiLarge = true;
                }
            }
            if(activeGearItems.Contains(GearID.HeavyWeapons)) {
                if(squadStats.RarityTier == UnitRarity.Rare && !squadStats.SquadAttributes.ArmorPiercing) {
                    string gearNameLocalized = LocalizationManager.Instance.GetText(GearID.HeavyWeapons.ToString()+"Name");

                    unitAttributeBonuses.Add(new UnitAttributeBonus(UnitAttribute.ArmorPiercing, gearNameLocalized, GearData.GetGear(GearID.HeavyWeapons).GearModifierValue));
                    squadStats.SquadAttributes.ArmorPiercing = true;
                }
            }

            return unitAttributeBonuses;
        }
        private void LoadGearFromSaveFile()
        {
            List<GearID> gearNameList = SaveDataHandler.Load().Gear;
            foreach (GearID gearName in gearNameList) {
                activeGearItems.Add(gearName);
            }
        }
        public void AquireGear(GearID _gearName)
        {
            activeGearItems.Add(_gearName);
            CampaignManager.Instance.ArmyJuiceManager.MarkGearAsNew(_gearName);
        }
        public void UnAquireGear(GearID _gearName)
        {
            activeGearItems.Remove(_gearName);
        }
    }
}