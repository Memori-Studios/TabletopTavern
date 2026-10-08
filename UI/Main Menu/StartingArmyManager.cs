using System.Collections;
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
        List<WarbandArmyTile> armyTiles = new();
        [SerializeField] private WarbandArmyTile armyTilePrefab;
        
        [Header("Select Starting Army")]
        [SerializeField] private WarbandRecruitList recruitList;

        [Header("Gear")]
        [SerializeField] private WarbandGearArmory gearArmory;
        [SerializeField] private WarbandGearSlot gearSlot;
        ArmySaveData armySaveData;
        public ArmySaveData ArmySaveData => armySaveData;

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
        public GearID StartingGearID => startingGearID;
        // What a gear pick may cost: the treasury left plus the refund from swapping out the equipped item.
        public int GearBudget => remainingTreasury.Value + gearGoldSpend;
        // Units the player could add from the pickers, for analytics pick rates. Undiscovered units are not offered.
        private readonly List<UnitName> offeredUnits = new();
        public IReadOnlyList<UnitName> OfferedUnits => offeredUnits;
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
            HeroBonusManager.ApplyHeroBaseUnitCount(ref newSquad, playPanel.hero.HeroID);

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
            foreach (var tile in armyTiles)
            {
                if(tile == null) continue;
                // Destroy waits for the frame's end; switched off now, the grid lays out only the new tiles.
                tile.gameObject.SetActive(false);
                Destroy(tile.gameObject);
            }
            armyTiles.Clear();
            foreach (var squad in _squadsToLoad)
            {
                WarbandArmyTile tile = Instantiate(armyTilePrefab, startingUnitsParent);
                tile.Set(squad);
                tile.gameObject.AddComponent<TroopHoverPlayPanel>().SetUp(armyIndex, playPanel);
                tile.gameObject.AddComponent<StartingTroopDoubleClickHandler>().SetUp(armyIndex, this);
                tile.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                    LocalizationManager.Instance.GetText(squad.UnitName.ToString()),
                    InputText.Get("DoubleClickRemoveTroop")
                );
                armyTiles.Add(tile);
                armyIndex++;
            }
            OnStartingArmyLengthChanged?.Invoke(_squadsToLoad.Length);
            CalculateRemainingTreasury();
        }
        private void LoadStartingGear()
        {
            if(gearArmory == null || gearSlot == null)
            {
                Debug.LogError("[StartingArmyManager] gearArmory or gearSlot is not wired; the starting gear picker will not show.");
                return;
            }
            gearArmory.SetUp(this);
            EquipGear(playPanel.StartingGearID, playFeedback: false);
        }

        /// <summary>Equips a starting gear item, or clears the slot with GearID.None.</summary>
        public void EquipGear(GearID _gear, bool playFeedback = true)
        {
            startingGearID = _gear;
            #region Metaprogression
            if(gameObject.GetComponentInChildren<MetaprogressionLockedButton>() != null) {
                gameObject.GetComponentInChildren<MetaprogressionLockedButton>().CheckLockedState();
            }
            #endregion
            playPanel.SetStartingGear(_gear);
            // Taking gear off has its own sound, so removing never sounds like equipping.
            if(playFeedback) IAudioRequester.Instance.PlaySFX(_gear == GearID.None ? SFXData.CollectItem : SFXData.AddGear);
            CalculateRemainingTreasury();
            gearSlot.Show(_gear, GearCost(_gear), playPanel.StartingGearLocked, () => EquipGear(GearID.None), playFeedback);
        }

        /// <summary>Starting price of a gear item, after the Renown discount. None costs nothing.</summary>
        public int GearCost(GearID _gear) => _gear == GearID.None ? 0 : GearCost(GearData.GetGear(_gear).GearRarity);

        public int GearCost(GearRarity _rarity)
        {
            int gearCost = GearData.GearCost(_rarity);
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingGearReducedCostMetaprogressionModel)) {
                gearCost = Mathf.Max(0, gearCost - _startingGearReducedCostMetaprogressionModel.NodeValue);
            }
            return gearCost;
        }

        public void CalculateRemainingTreasury()
        {
            gearGoldSpend = GearCost(startingGearID);

            //get cost of each unit in army
            armyGoldSpend = 0;
            foreach (var squad in _squadsToLoad)
            {
                armyGoldSpend += TabletopTavernData.Instance.GetUnitCost(squad.UnitName);
            }

            remainingTreasury.Value = startingGold - (armyGoldSpend + gearGoldSpend);
            // The armory dims what the warband cannot afford, and army changes move that line too.
            if(gearArmory != null) gearArmory.Refresh();
            if(recruitList != null) recruitList.Refresh();
        }
        public void PointerOverTroop(int _index)
        {
            if(_index == PlayPanel.SIGNATURE_UNIT_HOVER_INDEX)
            {
                commanderSquadBattleInfo.SetUpCampaign(playPanel.uniqueSquad, Team.Player);
                return;
            }
            if(_index < 0 || _index >= _squadsToLoad.Length) return;
            ShowWarbandUnitInfo(_squadsToLoad[_index]);
        }
        public void PointerOverTroop(SquadToLoad _squadToLoad)
        {
            ShowWarbandUnitInfo(_squadToLoad);
        }
        private void ShowWarbandUnitInfo(SquadToLoad _squad)
        {
            // Saved inactive in the scene, and its fade cannot run until it is active.
            if(!squadBattleInfo.gameObject.activeSelf) squadBattleInfo.gameObject.SetActive(true);
            squadBattleInfo.SetUpCampaign(_squad, Team.Player);
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
            List<Vector2> oldPositions = TilePositions();
            LeaveGhost(_index);
            RefreshArmyDisplay();
            SlideTilesFrom(oldPositions, _index);
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
        }

        #region Army tile motion
        private const float TileSlideTime = 0.15f;

        private List<Vector2> TilePositions()
        {
            List<Vector2> positions = new();
            foreach (WarbandArmyTile tile in armyTiles)
                positions.Add(tile != null ? ((RectTransform)tile.transform).anchoredPosition : Vector2.zero);
            return positions;
        }

        // The removed squad's tile stays where it was and shrinks away, out of the layout and out of reach of clicks.
        private void LeaveGhost(int index)
        {
            if (index < 0 || index >= armyTiles.Count || armyTiles[index] == null) return;
            WarbandArmyTile ghost = armyTiles[index];
            armyTiles.RemoveAt(index);
            GetOrAdd<UnityEngine.UI.LayoutElement>(ghost.gameObject).ignoreLayout = true;
            CanvasGroup group = GetOrAdd<CanvasGroup>(ghost.gameObject);
            group.blocksRaycasts = false;
            ghost.PlayRemoved();
        }

        // The squads after a removed one slide one slot back instead of jumping.
        private void SlideTilesFrom(List<Vector2> oldPositions, int removedIndex)
        {
            if (!isActiveAndEnabled) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)startingUnitsParent);
            for (int i = removedIndex; i < armyTiles.Count && i + 1 < oldPositions.Count; i++)
                StartCoroutine(Slide((RectTransform)armyTiles[i].transform, oldPositions[i + 1]));
        }

        private static IEnumerator Slide(RectTransform tile, Vector2 from)
        {
            Vector2 to = tile.anchoredPosition;
            for (float t = 0f; t < 1f && tile != null; t += Mathf.Min(Time.unscaledDeltaTime, UIJuice.MaxStep) / TileSlideTime)
            {
                tile.anchoredPosition = Vector2.LerpUnclamped(from, to, UIJuice.EaseOutCubic(t));
                yield return null;
            }
            if (tile != null) tile.anchoredPosition = to;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T found = go.GetComponent<T>();
            return found != null ? found : go.AddComponent<T>();
        }

        // The first + a key press should land on: the top recruit row that can take a squad.
        public UnityEngine.UI.Selectable FirstRecruitControl() => recruitList != null ? recruitList.FirstAddButton() : null;
        #endregion
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
            // Every tile is rebuilt in the same frame, so only the new last one is seen to land.
            if (armyTiles.Count > 0 && armyTiles[^1] != null) armyTiles[^1].PlayLanded();
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
        }
        public void LoadUnitsOfRace(Race race)
        {
            offeredUnits.Clear();
            recruitList.Build(this, race, troopsRecruitied, offeredUnits);
        }
        private void OnDestroy()
        {
            if(playPanel != null) remainingTreasury.OnValueChanged -= playPanel.RemainingTreasuryChanged;
        }
    }
}