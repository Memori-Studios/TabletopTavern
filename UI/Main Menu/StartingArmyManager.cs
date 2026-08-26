using UnityEngine;
using TMPro;
using Memori.SaveData;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.Localization;
using Memori.Tooltip;
using Memori.Audio;
using Memori.Core;
using Memori.UI;
using UnityEngine.Playables;
using Memori.Notifications;
using Memori.Metaprogression;
using System;

namespace TJ.MainMenu
{
    public class StartingArmyManager : MonoBehaviour
    {
        [Header("Selected Army")]
        [SerializeField] private SquadBattleInfo squadBattleInfo;
        // The commander screen needs its own inspector: squadBattleInfo lives on the warband
        // screen, which is inactive while the commander screen is up, so the signature-unit hover
        // had nowhere to render.
        [SerializeField] private SquadBattleInfo commanderSquadBattleInfo;
        [SerializeField] private Transform startingUnitsParent;
        [SerializeField] private SquadToLoad[] _squadsToLoad;
        public SquadToLoad[] SelectedArmy => _squadsToLoad;
        List<SquadDisplayCardMenu> squadDisplayCards = new();
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenu;
        
        [Header("Select Starting Army")]
        [SerializeField] private GameObject lockedBlocker;
        [SerializeField] private TMP_Text tier1CostText;
        [SerializeField] private Transform tier1UnitsParent;
        [SerializeField] private TMP_Text tier2CostText;
        [SerializeField] private Transform tier2UnitsParent;
        [SerializeField] private TMP_Text tier3CostText;
        [SerializeField] private Transform tier3UnitsParent;

        [Header("Gear")]
        [SerializeField] private Transform gearCardParent;
        [SerializeField] private CollectionGearCard[] gearCards;
        [SerializeField] private GearCard selectedGearCard;
        [SerializeField] private MemoriTooltipTrigger gearCardOptionTooltipTrigger;
        [SerializeField] private StartingGearDoubleClickHandler doubleClickHandler;
        ArmySaveData armySaveData;
        public ArmySaveData ArmySaveData => armySaveData;

        [Header("Treasury")]
        [SerializeField] private TMP_Text gearCostTextRare;
        [SerializeField] private TMP_Text gearCostTextUncommon, gearCostTextCommon;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _startingGoldMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _startingGoldMetaprogressionModel2;
        [SerializeField] private MetaprogressionModel _startingGearReducedCostMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _thirdReserveSlotMetaprogressionModel;

        // The starting army is fixed at 10 deployed slots. Reserve slots sold by the metaprogression
        // tree fill during a run, not at setup, so they deliberately do not raise this.
        public const int MaxStartingArmySize = 10;

        GearID startingGearID;
        PlayPanel playPanel;
        public MonitoredData<int> remainingTreasury = new (0);
        List<UnitName> troopsRecruitied = new ();
        int startingGold;
        int startingGoldBonusFromMetaprogression;
        int armyGoldSpend;
        int gearGoldSpend;
        public int StartingGoldBonusFromMetaprogression => startingGoldBonusFromMetaprogression;
        // Read by the warband purse so the breakdown never has to recompute the same costs.
        public int StartingGold => startingGold;
        public int ArmyGoldSpend => armyGoldSpend;
        public int GearGoldSpend => gearGoldSpend;
        public Action<int> OnStartingArmyLengthChanged;

        public void SetUp(PlayPanel _playPanel)
        {
            playPanel = _playPanel;
            // SetUp runs again on every hero change, so -= before += or these stack one subscription
            // per hero selected (see the memory-leak checklist in CLAUDE.md).
            remainingTreasury.OnValueChanged -= playPanel.RemainingTreasuryChanged;
            remainingTreasury.OnValueChanged += playPanel.RemainingTreasuryChanged;
            startingGold = playPanel.hero.StartingGold;
            
            startingGoldBonusFromMetaprogression = 0;
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingGoldMetaprogressionModel)) {
                startingGoldBonusFromMetaprogression += _startingGoldMetaprogressionModel.NodeValue;
                // Debug.Log($"Increased starting gold to: {startingGold}");
            }
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingGoldMetaprogressionModel2)) {
                startingGoldBonusFromMetaprogression += _startingGoldMetaprogressionModel2.NodeValue;
                // Debug.Log($"Increased starting gold to: {startingGold}");
            }
            startingGold += startingGoldBonusFromMetaprogression;

            remainingTreasury.Value = startingGold;

            LoadStartingGear();
            LoadStartingArmy();
            RefreshArmyDisplay();
            tier1CostText.text = TabletopTavernConstants.GetUnitCost(1).ToString() + " <sprite name=GoldSprite>";
            tier2CostText.text = TabletopTavernConstants.GetUnitCost(2).ToString() + " <sprite name=GoldSprite>";
            tier3CostText.text = TabletopTavernConstants.GetUnitCost(3).ToString() + " <sprite name=GoldSprite>";

            int commonCost = GearData.GearCost(GearRarity.Common);
            int uncommonCost = GearData.GearCost(GearRarity.Uncommon);
            int rareCost = GearData.GearCost(GearRarity.Rare);

            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingGearReducedCostMetaprogressionModel)) {
                commonCost = Mathf.Max(0, commonCost - _startingGearReducedCostMetaprogressionModel.NodeValue);
                uncommonCost = Mathf.Max(0, uncommonCost - _startingGearReducedCostMetaprogressionModel.NodeValue);
                rareCost = Mathf.Max(0, rareCost - _startingGearReducedCostMetaprogressionModel.NodeValue);
                // Debug.Log($"Reduced starting gear costs to: Common {commonCost}, Uncommon {uncommonCost}, Rare {rareCost}");
            }

            gearCostTextCommon.text = commonCost.ToString() + " <sprite name=GoldSprite>";
            gearCostTextUncommon.text = uncommonCost.ToString() + " <sprite name=GoldSprite>";
            gearCostTextRare.text = rareCost.ToString() + " <sprite name=GoldSprite>";

            troopsRecruitied = SaveDataHandler.LoadPlayerSaveData().troopsRecruited;
            OnStartingArmyLengthChanged?.Invoke(_squadsToLoad.Length);
        }
        private void AddUnitToArmy(UnitName unitName)
        {
            List<SquadToLoad> updatedSquads = new List<SquadToLoad>(_squadsToLoad);
            SquadToLoad newSquad = new SquadToLoad(
                unitName, 
                _prestige: 0, 
                _unitIndex: updatedSquads.Count
            );

            //int get base unit count
            int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(newSquad.UnitName);
            int hitpointsPerUnit = TabletopTavernData.Instance.GetHitPointsPerUnit(newSquad.UnitName);
            
            newSquad.SquadCurrentHealth = baseUnitCount * hitpointsPerUnit;
            newSquad.maxUnitCount = baseUnitCount;
            newSquad.HitPointsPerUnit = hitpointsPerUnit;

            updatedSquads.Add(newSquad);
            _squadsToLoad = updatedSquads.ToArray();
        }
        private void LoadStartingArmy()
        {
            _squadsToLoad = new SquadToLoad[0];
            UnitName[] unitNames = playPanel.hero.StartingArmyUnits;

            for (int j = 0; j < unitNames.Length; j++) 
            {
                AddUnitToArmy(unitNames[j]);
            }
        }
        public void RefreshArmyDisplay()
        {
            int armyIndex = 0;
            foreach (var card in squadDisplayCards)
            {
                if(card != null)
                    Destroy(card.gameObject);
            }
            squadDisplayCards.Clear();
            foreach (var squad in _squadsToLoad)
            {
                SquadDisplayCardMenu squadDisplayCard = Instantiate(squadDisplayCardMenu, startingUnitsParent);
                squadDisplayCard.SetUp(squad, false, _isEnemy: true);
                squadDisplayCard.LockCard(true);
                squadDisplayCard.InheritCanvasSorting();
                squadDisplayCard.gameObject.AddComponent<TroopHoverPlayPanel>().SetUp(armyIndex, playPanel);
                squadDisplayCard.gameObject.AddComponent<StartingTroopDoubleClickHandler>().SetUp(armyIndex, this);
                squadDisplayCard.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                    LocalizationManager.Instance.GetText(squad.UnitName.ToString()),
                    LocalizationManager.Instance.GetText("DoubleClickRemoveTroop")
                );
                squadDisplayCards.Add(squadDisplayCard);
                armyIndex++;
            }
            OnStartingArmyLengthChanged?.Invoke(_squadsToLoad.Length);
            CalculateRemainingTreasury();
        }
        private void LoadStartingGear()
        {
            gearCards = gearCardParent.GetComponentsInChildren<CollectionGearCard>();
            GearID[] allGear = GearData.GetGearIDs();
            //sort gear by rariity: Common, Uncommon, Rare
            System.Array.Sort(allGear, (b, a) => 
                GearData.GetGear(a).GearRarity.CompareTo(GearData.GetGear(b).GearRarity)
            );
            List<int> gearIdsAsInts = SaveDataHandler.GetGearIDsCollected();
            List<int> gearIdsAcknowledged = SaveDataHandler.GetGearIDsAcknowledged();

            // Adding a GearID without adding a card to gearCardParent used to throw here. Report the
            // shortfall once instead, so new gear is merely invisible rather than breaking the panel.
            if(gearCards.Length < allGear.Length)
            {
                Debug.LogError($"[StartingArmyManager] {allGear.Length} gear IDs but only {gearCards.Length} gear cards under {gearCardParent.name} - the last {allGear.Length - gearCards.Length} will not be shown.");
            }

            int cardCount = Mathf.Min(gearCards.Length, allGear.Length);
            for (int i = 0; i < cardCount; i++)
            {
                bool isCollected = gearIdsAsInts.Contains((int)allGear[i]);
                bool acknowledged = gearIdsAcknowledged.Contains((int)allGear[i]);
                gearCards[i].LoadGearCard(allGear[i], isCollected, acknowledged, _startingGear: this);
            }
            SelectGearCard(playPanel.StartingGearID);
            doubleClickHandler.SetStartingArmyManager(this);
        }

        public void SelectGearCard(GearID _gear)
        {
            startingGearID = _gear;
            selectedGearCard.LoadGearCard(_gear);
            #region Metaprogression
            if(gameObject.GetComponentInChildren<MetaprogressionLockedButton>() != null) {
                gameObject.GetComponentInChildren<MetaprogressionLockedButton>().CheckLockedState();
            }
            #endregion
            selectedGearCard.PlayPurchaseFeedbacks();
            playPanel.SetStartingGear(_gear);
            IAudioRequester.Instance.PlaySFX(SFXData.AddGear);
            SetStartingGearTitle(_gear);
            CalculateRemainingTreasury();
        }
        public void SetStartingGearTitle(GearID gearID)
        {
            string gearNameLocalized = LocalizationManager.Instance.GetText(gearID + "Name");
            string startingGearTitleLocalized = LocalizationManager.Instance.GetText("Starting Gear");
            string startingGearTitle = $"<color {ColorData.Secondary}>{startingGearTitleLocalized}:</color> <color {ColorData.Primary}>{gearNameLocalized}</color>";
            string gearOptionDescLocalized = LocalizationManager.Instance.GetText("DoubleClickRemove");
            gearCardOptionTooltipTrigger.SetUpToolTip(startingGearTitle, gearOptionDescLocalized);
        }
        public void CalculateRemainingTreasury()
        {
            gearGoldSpend = 0;
            if(startingGearID != GearID.None) {
                int gearCost = GearData.GearCost(GearData.GetGear(startingGearID).GearRarity);
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingGearReducedCostMetaprogressionModel)) {
                    gearCost -= _startingGearReducedCostMetaprogressionModel.NodeValue;
                    gearCost = Mathf.Max(0, gearCost);
                }
                gearGoldSpend = gearCost;
            }

            //get cost of each unit in army
            armyGoldSpend = 0;
            foreach (var squad in _squadsToLoad)
            {
                armyGoldSpend += TabletopTavernData.Instance.GetUnitCost(squad.UnitName);
            }

            remainingTreasury.Value = startingGold - (armyGoldSpend + gearGoldSpend);
        }
        public void PointerOverTroop(int _index)
        {
            if(_index == PlayPanel.SIGNATURE_UNIT_HOVER_INDEX)
            {
                commanderSquadBattleInfo.SetUpCampaign(playPanel.uniqueSquad, Team.Player);
                return;
            }
            if(_index < 0 || _index >= _squadsToLoad.Length) return;
            squadBattleInfo.SetUpCampaign(_squadsToLoad[_index], Team.Player);
        }
        public void PointerOverTroop(SquadToLoad _squadToLoad)
        {
            squadBattleInfo.SetUpCampaign(_squadToLoad, Team.Player);
        }
        public void PointerOffTroop()
        {
            // Unhover early-outs when already faded, so hitting both is cheaper than tracking
            // which screen the pointer left.
            squadBattleInfo.Unhover();
            commanderSquadBattleInfo.Unhover();
        }
        public void RemoveTroop(int _index)
        {
            if(_index < 0 || _index >= _squadsToLoad.Length) return;

            //here
            if(playPanel.StartingArmyLockedForHero) {
                NotificationManager.Instance.ErrorNotification(
                    LocalizationManager.Instance.GetText("OneCompletionRequired")
                );
                return;
            }

            List<SquadToLoad> updatedSquads = new (_squadsToLoad);
            updatedSquads.RemoveAt(_index);
            _squadsToLoad = updatedSquads.ToArray();
            PointerOffTroop();
            RefreshArmyDisplay();
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
        }
        public void AddTroop(SquadToLoad _squadToAdd)
        {
            if(_squadsToLoad.Length >= MaxStartingArmySize)
            {
                NotificationManager.Instance.ErrorNotification(
                    string.Format(LocalizationManager.Instance.GetText("MaxStartingArmyError"), MaxStartingArmySize)
                );
                return;
            }

            List<SquadToLoad> updatedSquads = new(_squadsToLoad)
            {
                _squadToAdd
            };
            _squadsToLoad = updatedSquads.ToArray();
            RefreshArmyDisplay();
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
        }
        public void LoadUnitsOfRace(Race race)
        {
            // Clear existing unit cards
            foreach (Transform child in tier1UnitsParent)
            {
                Destroy(child.gameObject);
            }
            foreach (Transform child in tier2UnitsParent)
            {
                Destroy(child.gameObject);
            }
            foreach (Transform child in tier3UnitsParent)
            {
                Destroy(child.gameObject);
            }

            // Get units of the specified race
            UnitName[] unitsOfRace = TabletopTavernData.Instance.GetUnitsOfRace(race);
            foreach (var unitName in unitsOfRace)
            {
                int unitTier = TabletopTavernData.Instance.GetUnitTierFromUnitName(unitName);
                Transform tierParent = unitTier switch
                {
                    1 => tier1UnitsParent,
                    2 => tier2UnitsParent,
                    3 => tier3UnitsParent,
                    _ => null,
                };
                if (tierParent == null) continue;

                // Parented at instantiation rather than at the end of the loop: Unity forces
                // overrideSorting true on a root Canvas, so InheritCanvasSorting below is discarded
                // if the card is still unparented when it runs, and reparenting never re-applies it.
                SquadDisplayCardMenu unitCard = Instantiate(squadDisplayCardMenu, tierParent);

                SquadToLoad newSquad = new SquadToLoad(unitName, 0, 0);
                int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(newSquad.UnitName);
                int hitpointsPerUnit = TabletopTavernData.Instance.GetHitPointsPerUnit(newSquad.UnitName);
                
                newSquad.SquadCurrentHealth = baseUnitCount * hitpointsPerUnit;
                newSquad.maxUnitCount = baseUnitCount;
                newSquad.HitPointsPerUnit = hitpointsPerUnit;
                
                unitCard.SetUp(newSquad, false, _isEnemy: true);
                unitCard.LockCard(true);
                unitCard.InheritCanvasSorting();
                unitCard.gameObject.AddComponent<TroopHoverPlayPanel>().SetUp(-1, playPanel);
                
                //check if squad is discovered
                if(troopsRecruitied.Contains(unitName))
                {
                    unitCard.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                        LocalizationManager.Instance.GetText(unitName.ToString()),
                        LocalizationManager.Instance.GetText("DoubleClickAddTroop")
                    );
                    unitCard.gameObject.AddComponent<StartingTroopDoubleClickHandler>().SetUp(-1, this);
                } 
                else
                {
                    //instantiate locked blocker
                    Instantiate(lockedBlocker, unitCard.transform);
                    unitCard.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                        LocalizationManager.Instance.GetText("Locked"),
                        LocalizationManager.Instance.GetText("UnitNotDiscoveredDesc")
                    );
                }
            }
        }
        private void OnDestroy()
        {
            if(playPanel != null) remainingTreasury.OnValueChanged -= playPanel.RemainingTreasuryChanged;
        }
    }
}