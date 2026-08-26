using UnityEngine;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Utilities;
using System;
using TJ.Map;
using Unity.Mathematics;
using Memori.Scenes;
using System.Linq;
using Memori.Steamworks;
using Memori.Metaprogression;
// using TabletopAnalytics;
using Memori.Localization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ
{
    public class CampaignSaveManager : MonoBehaviour
    {
        public delegate void ChapterCompleted(int activeLayerIndex);
        public event ChapterCompleted OnChapterCompleted;
        public delegate void UnitHealthChanged();
        public event UnitHealthChanged OnUnitHealthChanged;
        public delegate void GearChanged();
        public event GearChanged OnGearChanged;
        public delegate void ArmyStructureChanged();
        public event ArmyStructureChanged OnArmyStructureChanged;
        public delegate void ConsumablesChanged();
        public event ConsumablesChanged OnConsumablesChanged;
        public delegate void GameSaved();
        public event GameSaved OnGameSaved;
        
        public static float healAmount = 0.25f;
        readonly string[] townNames = new string[] { "townName1", "townName2", "townName3", "townName4", "townName5", "townName6", "townName7", "townName8", "townName9", "townName10" };

        [Header("Campaign Save Data")]
        CampaignSaveData saveData;
        public CampaignSaveData SaveData => saveData;

        [Header("Testing Map Scene")]
        [SerializeField] private int testHeroId;
        [SerializeField] private TT_Difficulty testDifficulty;
        [SerializeField] private GearID testStartingGear;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _consumableCapacityMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _gearSlot4MetaprogressionModel, _gearSlot5MetaprogressionModel, _interestBaseMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _reservesHealMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _thirdReserveSlotMetaprogressionModel;

        [Header("Devtools")]
#if UNITY_EDITOR
        [SerializeField] private bool _disableSaving;
        private bool DisableSaving => _disableSaving;
#else
        private bool DisableSaving => false;
#endif

        private int consumableCapacity, maxGear, goldRequiredToGenerateInterest = 5;
        // Metaprogression-derived values are meta-level (fixed between runs), so they are snapshotted
        // once in Load() rather than re-reading playerSaveData on every property get. Defaults match
        // the "not unlocked" case for any access before Load() runs.
        private int reservesHealMultiplier = 1, maxReserveSlots = 2;
        public int GoldRequiredToGenerateInterest => goldRequiredToGenerateInterest;
        public int ConsumableCapacity => consumableCapacity;
        public int MaxGear => maxGear;
        public int ReservesHealMultiplier => reservesHealMultiplier;
        public int MaxReserveSlots => maxReserveSlots;

        public void Init(GameStateEnum _previousGameState)
        {
            if (_previousGameState == GameStateEnum.MainMenu)
            {
                saveData = SaveDataHandler.LoadSnapshot();
                if (!DisableSaving)
                {
                    SaveDataHandler.SaveCampaign(saveData); // syncs active file with snapshot; sets saveData.snapShot = false as a side effect
                    saveData.snapShot = true; // restore: CompleteLoad() uses this to decide between SnapshotLoad() and other paths
                }
            }
            else
            {
                saveData = SaveDataHandler.Load();
            }

            // MaxReserveSlots is metaprogression-derived and must be known here: MapSceneUIManager.SetUp()
            // renders the troops panel before Load() runs. Resolve it and expand a save array that predates
            // the third-reserve-slot unlock, otherwise RefreshTroopsPanel (which only draws
            // min(playerArmy.Length, 10 + MaxReserveSlots) slots) never creates the third reserve slot.
            maxReserveSlots = SaveDataHandler.IsMetaprogressionNodeUnlocked(_thirdReserveSlotMetaprogressionModel) ? 3 : 2;
            EnsureArmyCapacity();
        }
        public void Load()
        {
            // Debug.Log($"Loading campaign save data...");
            CampaignManager.Instance.GoldManager.LoadGold();
            OnChapterCompleted?.Invoke(saveData.activeMapLayer);
            OnGearChanged?.Invoke();
            OnArmyStructureChanged += SavePlayerArmy;
            OnArmyStructureChanged += EvaluateArmyRunStats;
            EvaluateArmyRunStats(); // sample the starting/loaded army once (covers runs where the army never changes)

            consumableCapacity = 2;
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_consumableCapacityMetaprogressionModel)) {
                consumableCapacity += 1;
                Debug.Log($"Increased consumable capacity to: {consumableCapacity}");
            }

            maxGear = 3;
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_gearSlot4MetaprogressionModel)) {
                maxGear += 1;
                // Debug.Log($"Unlocked gear slot 4");
            }
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_gearSlot5MetaprogressionModel)) {
                maxGear += 1;
                // Debug.Log($"Unlocked gear slot 5");
            }

            goldRequiredToGenerateInterest = 5;
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_interestBaseMetaprogressionModel)) {
                goldRequiredToGenerateInterest -= _interestBaseMetaprogressionModel.NodeValue;
                Debug.Log($"Reduced gold required to generate interest to: {goldRequiredToGenerateInterest}");
            }

            reservesHealMultiplier = SaveDataHandler.IsMetaprogressionNodeUnlocked(_reservesHealMetaprogressionModel) ? 2 : 1;
            maxReserveSlots = SaveDataHandler.IsMetaprogressionNodeUnlocked(_thirdReserveSlotMetaprogressionModel) ? 3 : 2;
        }
        public void SaveCampaign()
        {
            if (DisableSaving) return;
            // saveData is the authoritative in-memory copy (mutated in place); write through, no reload.
            SaveDataHandler.SaveCampaign(saveData);
        }
        public void SaveCampaignSnapshot()
        {
            if (DisableSaving) return;
            OnGameSaved?.Invoke();
            // SaveDataHandler.DepositGold(); // legacy deposited-gold flush, disabled - see Renown system
            SaveDataHandler.SaveCampaignSnapshot(saveData);
        }

        #region Set Up

        [ContextMenu("Override Campaign Save")]
        public void OverrideCampaignSave()
        {
            DeleteCampaignSave();
            // Debug.Log($"Overriding campaign save data");
            Hero hero = HeroData.GetHeroByID(testHeroId);
            // HeroName is a localization key everywhere else, but doubles as a Resources path here.
            // A hero_overrides.json mod can change it, which breaks only this editor-only path.
            string starterArmyPath = "Armies/Heroes/" + hero.HeroName + "StarterArmy";
            ArmySaveData armySaveData = Resources.Load<ArmySaveData>(starterArmyPath);
            if (armySaveData == null)
                Debug.LogWarning($"No starter army at '{starterArmyPath}'. If a mod overrides this hero's heroName, that rename breaks this lookup.");

            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            Guid runUUID = Guid.NewGuid();
            SaveDataHandler.CreateCampaign(hero, armySaveData, testDifficulty, testStartingGear, runUUID, 99);

            saveData = SaveDataHandler.Load();

            playerSaveData.campaignsStarted++;
            SaveDataHandler.SavePlayerSaveData(playerSaveData);
        }
        public void QuickRestartCampaign()
        {
            DeleteCampaignSave();
            
            saveData = SaveDataHandler.Load();

            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            Guid runUUID = Guid.NewGuid();

            SaveDataHandler.CreateCampaign(
                HeroData.GetHeroByID(playerSaveData.lastHeroID),
                playerSaveData.lastArmySaveData,
                playerSaveData.lastDifficultyLevelSelected,
                playerSaveData.lastStartingGearId,
                runUUID,
                playerSaveData.lastStartingGold
            );

            // AnalyticsManager.Instance.LogRunStart(
            //     runUUID.ToString(),
            //     playerSaveData.lastHeroID,
            //     (int)playerSaveData.lastDifficultyLevelSelected,
            //     playerSaveData.lastStartingGearId.ToString(),
            //     playerSaveData.lastArmySaveData
            // );
            
            saveData = SaveDataHandler.Load();
            playerSaveData.campaignsStarted++;
            SaveDataHandler.SavePlayerSaveData(playerSaveData);
        }
        private void Start()
        {
            SceneHandler.Instance.OnRequestSceneCleanUp += OnRequestSceneCleanUp;
            CheckGodkingCompletionAchievements();
        }
        public void OnRequestSceneCleanUp()
        {
            SceneHandler.Instance.SceneCleanUpComplete();
        }

        public void DeleteCampaignSave()
        {
            Debug.Log($"Erasing campaign save data");
            saveData = null;
            SaveDataHandler.DeleteCampaignSave();
        }
        private void OnDestroy()
        {
            Debug.Log($"Destroying campaign save manager...");
            OnArmyStructureChanged -= SavePlayerArmy;
            OnArmyStructureChanged -= EvaluateArmyRunStats;
            if (SceneHandler.HasInstance)
                SceneHandler.Instance.OnRequestSceneCleanUp -= OnRequestSceneCleanUp;
        }
        #endregion

        #region Get Methods
        public int GetInterestEarned()
        {
            int interestEarned = (int)((float)saveData.goldAmount / goldRequiredToGenerateInterest);
            //if saveData.goldAmount is less than 5, interestEarned will be 0
            return saveData.goldAmount < goldRequiredToGenerateInterest ? 0 : interestEarned;
        }
        public bool CheckForGear(GearID _gearID)
        {
            return saveData.Gear.Contains(_gearID);
        }
        public List<SquadKillsStored> GetSquadIdKillCounter()
        {
            return saveData.SquadKillsStore;
        }
        public List<SquadLossesStored> GetSquadIdLossCounter()
        {
            return saveData.SquadLossesStore;
        }
        public bool CheckForRoomToRecruit()
        {
            // Third reserve slot may have been unlocked mid-run; army array hasn't expanded yet
            if (saveData.playerArmy.Length < 10 + MaxReserveSlots) return true;

            for(int i = 0; i < saveData.playerArmy.Length; i++) {
                if(saveData.playerArmy[i].UnitIndex == -1) {
                    return true;
                }
            }
            return false;
        }
        public int GetArmySize()
        {
            int armySize = 0;
            for(int i = 0; i < saveData.playerArmy.Length; i++) {
                if(saveData.playerArmy[i].UnitIndex != -1) {
                    armySize++;
                }
            }
            return armySize;
        }
        public string GetUnitNameOrUnitNameOverride(string _uniqueID)
        {
            for(int i = 0; i < saveData.unitNameOverrides.Count; i++) {
                if(saveData.unitNameOverrides[i].unitGUID == _uniqueID) {
                    return saveData.unitNameOverrides[i].unitNameOverride;
                }
            }

            //go through player army, find the squad with the unique ID and return the unit name
            for(int i = 0; i < saveData.playerArmy.Length; i++) {
                if(saveData.playerArmy[i].UniqueID == _uniqueID) {
                    return LocalizationManager.Instance.GetText(saveData.playerArmy[i].UnitName.ToString());
                }
            }
            return null;
        }
        public bool CheckForUnitNameOverride(string _uniqueID, out string unitNameOverride)
        {
            for(int i = 0; i < saveData.unitNameOverrides.Count; i++) {
                if(saveData.unitNameOverrides[i].unitGUID == _uniqueID) {
                    unitNameOverride = saveData.unitNameOverrides[i].unitNameOverride;
                    return true;
                }
            }
            unitNameOverride = string.Empty; // No override, set to empty string
            return false;
        }
        public SquadToLoad[] GetWithdrawnSquads()
        {
            return saveData.withdrawnSquads.ToArray();
        }
        // The campaign-seeded stream. GetSeededRandom's raw value steps in small increments between
        // adjacent nodes and layers, which System.Random turns into identical first draws, so it is mixed
        // first. Callers that feed the raw seed to UnityEngine.Random.InitState instead do not need this.
        public System.Random GetCampaignRandom()
        {
            return new System.Random(MathUtilities.MixSeed(GetSeededRandom()));
        }
        public int GetHeroID()
        {
            return saveData.heroID;
        }
        #endregion

        #region Map Stuff
        public void RecordSelectedNode(int _selectedNodeIndex)
        {
            saveData.SetSelectedNodeIndex(_selectedNodeIndex);
            saveData.nodePath.Add(_selectedNodeIndex);
            SaveCampaign();
        }
        public void CompleteChapter()
        {
            // Debug.Log($"Completing chapter {_selectedNodeIndex}");
            saveData.SetSelectedNodeIndex(-1);
            saveData.activeMapLayer++;
            saveData.RunStats.chaptersCompleted++;
            saveData.nodeGenerated = false;
            saveData.Rolls = 0;
            saveData.townData = new TownSaveData();

            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.ThePotato))
            {
                saveData.turnsSincePotato++;
                saveData.turnsSincePotato++;
                OnGearChanged?.Invoke();
            }

            OnChapterCompleted?.Invoke(saveData.activeMapLayer);
            // SaveCampaign();
        }
        public void CompleteBook()
        {
            saveData.bookNumber++;
            saveData.BattlesFought = 0;
            saveData.activeMapLayer = -1;
            saveData.nodePath.Clear();
            saveData.nodesRevealed = false;
            SaveCampaign();
            SaveCampaignSnapshot();
            // Debug.Log($"Completing book, moving to book {saveData.bookNumber}");
            OnChapterCompleted?.Invoke(saveData.activeMapLayer);
        }
        public void MarkEngagementComplete(bool garrisonFight)
        {
            if(saveData == null) return;
            // Debug.Log($"Marking engagement complete");
            saveData.battleCompleted = false;
            if(!garrisonFight) saveData.BattlesFought++;
            SaveCampaign();
        }
        public void MarkGarrisonBattleComplete()
        {
            // Debug.Log($"Marking garrison battle complete");
            saveData.townData.townInteractionStatus = TownInteractionStatus.Sacked;
            SaveCampaign();
        }
        public void SetTownData(TownSaveData _townData)
        {
            saveData.townData = _townData;
            saveData.nodeGenerated = true;
            // Debug.Log($"Setting town data to {saveData.townData.townInteractionStatus}");
        }
        public void SaveRecruitableUnits(UnitName[] _recruitableUnits)
        {
            saveData.recruitableUnits = _recruitableUnits;
            saveData.nodeGenerated = true;
        }
        public void SaveRecruitableGear(GearID[] _recruitableGear)
        {
            saveData.recruitableGear = _recruitableGear;
            saveData.nodeGenerated = true;
        }
        public void IncrementRerollCount(int _incrementValue = 1)
        {
            saveData.Rolls += _incrementValue;
        }
        public void IncrementSignatureUnitPackPurchaseCount()
        {
            saveData.signatureUnitPacksPurchased++;
        }
        #endregion

        #region Army Management
        public void SavePlayerArmy()
        {
            if (DisableSaving) return;
            // saveData is authoritative; persist it directly instead of reloading and overlaying fields.
            SaveDataHandler.SaveCampaign(saveData);
        }
        public void RemoveZeroHealthSquads()
        {
            // Debug.Log($"Removing squads with 0 health from player and enemy armies");
            //filter out any squads with 0 unit count
            if (saveData.playerArmy != null)
            {
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].SquadCurrentHealth == 0)
                    {
                        saveData.playerArmy[i] = new SquadToLoad
                        {
                            UnitIndex = -1
                        };
                    }
                    else
                    {
                        // Debug.Log($"Keeping squad {saveData.playerArmy[i].UnitName} with {saveData.playerArmy[i].SquadCurrentHealth} health");
                    }
                }
                saveData.playerArmy = ResetIndexes(saveData.playerArmy);
            }

            if (saveData.enemyArmy != null)
            for (int i = 0; i < saveData.enemyArmy.Length; i++)
            {
                if (saveData.enemyArmy[i].SquadCurrentHealth == 0)
                {
                    saveData.enemyArmy[i] = new SquadToLoad
                    {
                        UnitIndex = -1
                    };
                }
            }
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.HideZeroHealthSquads();
        }
        public void HandleSpecialSquadsOnChapterEnd()
        {
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;

                SquadAttributes attrs = TabletopTavernData.Instance.GetSquadStats(saveData.playerArmy[i].UnitName).SquadAttributes;
                if (attrs.Unstoppable)
                {
                    ModifySpecificUnitHealth(0.25f, saveData.playerArmy[i].UniqueID);
                }
            }
        }
        public void HandleSpecialSquadsOnBattleEnd()
        {
            if(saveData == null) return;
            
            if(saveData.heroID == 15) //Skrix the Swarmcaller: If your army contains 5 or more Kobold units, a random Kobold will prestige on turn end
            {
                int koboldCount = 0;
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitName == UnitName.KoboldBrawlers || saveData.playerArmy[i].UnitName == UnitName.ScalebowKobolds)
                    {
                        koboldCount++;
                    }
                }
                if(koboldCount >= 5) {
                    List<int> koboldIndexes = new List<int>();
                    for (int i = 0; i < saveData.playerArmy.Length; i++)
                    {
                        if (saveData.playerArmy[i].UnitName == UnitName.KoboldBrawlers || saveData.playerArmy[i].UnitName == UnitName.ScalebowKobolds)
                        {
                            if(saveData.playerArmy[i].UnitPrestige < 2) // Only add kobolds that can still prestige
                                koboldIndexes.Add(i);
                        }
                    }
                    if(koboldIndexes.Count > 0)
                    {
                        int randomKoboldIndex = koboldIndexes[UnityEngine.Random.Range(0, koboldIndexes.Count)];
                        PrestigeSpecificUnit(saveData.playerArmy[randomKoboldIndex]);
                        // Debug.Log($"Prestiging Kobold at index {randomKoboldIndex} to prestige level {saveData.playerArmy[randomKoboldIndex].UnitPrestige}");
                    }
                }
            }
        }
        public void HealTroopsOnTownEntry()
        {
            float modifiedHealAmount = healAmount;

            //The Light of Nytherial: Units recieve 2x Healing from all sources,
            if (HeroBonusManager.Instance.ActiveHeroID == 8)
            {
                modifiedHealAmount *= 2f;
            }
            
            //DifficultyMod 11
            if(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.King) {
                modifiedHealAmount *= 0.5f;
            }

            //The Pumpkin Pie: Units heal to full health after entering a town
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.PumpkinPie))
            {
                modifiedHealAmount = 1f;
            }

            ModifyTroopHealth(modifiedHealAmount);
        }
        /// <summary>
        /// Modifies the health of all troops in the player's army by total health * _modificationAmount.
        /// </summary>
        /// <param name="_modificationAmount"></param>
        public void ModifyTroopHealth(float _modificationAmount)
        {
            float modificationAmount = _modificationAmount;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;
                    if (saveData.playerArmy[i].SquadCurrentHealth == 0) continue;

                    int troopsToHeal = (int)(saveData.playerArmy[i].SquadMaxHealth * modificationAmount);
                    // Debug.Log($"Healing {saveData.playerArmy[i].UnitName} for {troopsToHeal} health.");
                    int clampedHealth = math.clamp(saveData.playerArmy[i].SquadCurrentHealth + troopsToHeal, 0, saveData.playerArmy[i].SquadMaxHealth);
                    saveData.playerArmy[i].SquadCurrentHealth = clampedHealth;
                } 
            OnUnitHealthChanged?.Invoke();
        }
        public void ModifyGruntkinTroopHealth(float _modificationAmount)
        {
            float modificationAmount = _modificationAmount;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;
                if (saveData.playerArmy[i].SquadCurrentHealth == 0) continue;

                //Only modify Gruntkin units
                if(TabletopTavernData.Instance.GetRaceFromUnitName(saveData.playerArmy[i].UnitName) != Race.Gruntkin) continue;

                int troopsToHeal = (int)(saveData.playerArmy[i].SquadMaxHealth * modificationAmount);
                // Debug.Log($"Healing {saveData.playerArmy[i].UnitName} for {troopsToHeal} health.");
                int clampedHealth = math.clamp(saveData.playerArmy[i].SquadCurrentHealth + troopsToHeal, 0, saveData.playerArmy[i].SquadMaxHealth);
                saveData.playerArmy[i].SquadCurrentHealth = clampedHealth;
            } 
            OnUnitHealthChanged?.Invoke();
        }
        public void ModifyTroopHealth(float _modificationAmount, Race race)
        {
            float modificationAmount = _modificationAmount;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;
                    if (saveData.playerArmy[i].SquadCurrentHealth == 0) continue;

                    Race unitRace = TabletopTavernData.Instance.GetRaceFromUnitName(saveData.playerArmy[i].UnitName);
                    if(unitRace != race) continue;
                    
                    //Sister Morvayne: Common Units gain 3x Healing from all sources
                    // if (HeroBonusManager.Instance.ActiveHeroID == 9 && TabletopTavernData.Instance.GetUnitTierFromUnitName(saveData.playerArmy[i].UnitName) == 1)
                    // {
                    //     modificationAmount = _modificationAmount * 3f;
                    // }

                    int troopsToHeal = (int)(saveData.playerArmy[i].SquadMaxHealth * modificationAmount);
                    // Debug.Log($"Healing {saveData.playerArmy[i].UnitName} for {troopsToHeal} health.");
                    int clampedHealth = math.clamp(saveData.playerArmy[i].SquadCurrentHealth + troopsToHeal, 0, saveData.playerArmy[i].SquadMaxHealth);
                    saveData.playerArmy[i].SquadCurrentHealth = clampedHealth;
                } 
            OnUnitHealthChanged?.Invoke();
        }
            public void ModifySpecificUnitHealth(float _modificationAmount, string _uniqueID)
            {
                SquadToLoad squadToModify = Array.Find(saveData.playerArmy, x => x.UniqueID == _uniqueID);
                if (squadToModify.UniqueID == null) return;

                int healthToChange = (int)(_modificationAmount * squadToModify.SquadMaxHealth);
                int clampedTroops = math.clamp(squadToModify.SquadCurrentHealth + healthToChange, 0, squadToModify.SquadMaxHealth);
                Debug.Log($"Modifying {squadToModify.UnitName} health from {squadToModify.SquadCurrentHealth} to {clampedTroops}");
                squadToModify.SquadCurrentHealth = clampedTroops;
                saveData.playerArmy[squadToModify.UnitIndex] = squadToModify;
                //get how many were actually healed
                // int actualHealed = squadToModify.SquadCurrentHealth / squadToModify.HitPointsPerUnit;
                // int healedTroops = actualHealed - origionalTroopCount;
                // Debug.Log($"Healed {squadToModify.UnitName} for {healedTroops} troops");
                // CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(new ArmyJuice {
                //     uniqueID = squadToModify.UniqueID,
                //     armyJuiceEnum = ArmyJuiceEnum.Health,
                //     value = healedTroops
                // });
                OnUnitHealthChanged?.Invoke();
            }
        // Sole path for gaining a unit mid-run. _viaRaiseDead flags the Sanguine Court post-battle
        // reward so everything else can disqualify DeadShallServe.
        public void RecruitSquad(SquadStats _squadsStats, float healthOfSquad = 1f, bool _viaRaiseDead = false)
        {
            EnsureArmyCapacity();
            int nextEmptyUnitIndex = GetNextEmptyUnitIndex(saveData.playerArmy);
            SquadToLoad newSquad = new (
                _squadsStats.unitName,
                0, 
                _unitIndex: nextEmptyUnitIndex, 
                _modifiedHealthValueByAmount: healthOfSquad
            );

            saveData.playerArmy[nextEmptyUnitIndex] = newSquad;

            for(int i = 0; i < saveData.playerArmy.Length; i++) {
                if(saveData.playerArmy[i].UnitIndex != -1) {
                    saveData.playerArmy[i].UnitIndex = i;
                }
            }

            SaveDataHandler.AquiredTroop(_squadsStats.unitName);

            CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(new ArmyJuice {
                uniqueID = saveData.playerArmy[nextEmptyUnitIndex].UniqueID,
                armyJuiceEnum = ArmyJuiceEnum.SpawnIn,
            });

            if(_squadsStats.RarityTier == UnitRarity.Legendary) {
                SteamAchievements.Unlock(AchievementId.Tier5);
            }

            if(_squadsStats.unitSize == UnitSize.Artillery) {
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.Artillery });
            }

            Debug.Log($"[Unit] Recruited {_squadsStats.unitName} ({_squadsStats.RarityTier} {_squadsStats.unitType}) at slot {nextEmptyUnitIndex}");
            saveData.RunStats.unitsRecruited++;
            if (!_viaRaiseDead) saveData.RunStats.gainedUnitOutsideRaiseDead = true;
            OnArmyStructureChanged?.Invoke();
        }
        public void MoveUnitToIndex(string _uniqueID, int _index)
        {
            SquadToLoad squadToMoveTo = Array.Find(saveData.playerArmy, x => x.UniqueID == _uniqueID);
            int ogIndex = squadToMoveTo.UnitIndex;
            // Debug.Log($"Moving unit from {ogIndex} to {_index}");
            //check if there is a unit at the index we are moving to
            if(saveData.playerArmy[_index].UnitIndex != -1) {
                SquadToLoad squadToMoveToReserves = saveData.playerArmy[_index];
                saveData.playerArmy[_index] = squadToMoveTo;
                saveData.playerArmy[_index].UnitIndex = _index;
                saveData.playerArmy[ogIndex] = squadToMoveToReserves;
                saveData.playerArmy[ogIndex].UnitIndex = ogIndex;
            } else {
                saveData.playerArmy[_index] = squadToMoveTo;
                saveData.playerArmy[_index].UnitIndex = _index;
                saveData.playerArmy[ogIndex] = new SquadToLoad {
                    UnitIndex = -1
                };
            }
            OnArmyStructureChanged?.Invoke();
        }
        // Moves a unit to _index, shifting every slot between its current index and _index over by one
        // (rather than trading places with whatever is at _index). Caller is responsible for only using
        // this within a single contiguous block (e.g. the deployed 0-9 range) - it does not know about
        // the deployed/reserve boundary and will happily shift across it if asked to.
        public void ShiftUnitToIndex(string _uniqueID, int _targetIndex)
        {
            SquadToLoad squadToMove = Array.Find(saveData.playerArmy, x => x.UniqueID == _uniqueID);
            int ogIndex = squadToMove.UnitIndex;
            if (ogIndex == _targetIndex) return;

            if (_targetIndex > ogIndex)
            {
                for (int i = ogIndex; i < _targetIndex; i++)
                {
                    saveData.playerArmy[i] = saveData.playerArmy[i + 1];
                    saveData.playerArmy[i].UnitIndex = i;
                }
            }
            else
            {
                for (int i = ogIndex; i > _targetIndex; i--)
                {
                    saveData.playerArmy[i] = saveData.playerArmy[i - 1];
                    saveData.playerArmy[i].UnitIndex = i;
                }
            }

            saveData.playerArmy[_targetIndex] = squadToMove;
            saveData.playerArmy[_targetIndex].UnitIndex = _targetIndex;

            OnArmyStructureChanged?.Invoke();
        }
        private int GetNextEmptyUnitIndex(SquadToLoad[] _playerArmy)
        {
            for(int i = 0; i < _playerArmy.Length; i++) {
                if(_playerArmy[i].UnitIndex == -1) {
                    return i;
                }
            }
            return -1;
        }
        // Expands playerArmy to 10 + MaxReserveSlots if the third reserve slot was unlocked mid-run.
        // Does not fire OnArmyStructureChanged — callers handle that themselves.
        private void EnsureArmyCapacity()
        {
            int targetLength = 10 + MaxReserveSlots;
            if (saveData.playerArmy.Length >= targetLength) return;

            SquadToLoad[] expanded = new SquadToLoad[targetLength];
            Array.Copy(saveData.playerArmy, expanded, saveData.playerArmy.Length);
            for (int i = saveData.playerArmy.Length; i < targetLength; i++)
                expanded[i] = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };
            saveData.playerArmy = expanded;
        }
        public void DisbandMultipleSquads(List<string> _uniqueIDs)
        {
            if (saveData == null) return;
            foreach (string uid in _uniqueIDs)
            {
                int idx = Array.FindIndex(saveData.playerArmy, x => x.UniqueID == uid);
                if (idx >= 0)
                {
                    Debug.Log($"[Unit] Disbanding {saveData.playerArmy[idx].UnitName} (prestige {saveData.playerArmy[idx].UnitPrestige}) at slot {idx}");
                    saveData.playerArmy[idx].UnitIndex = -1;
                }
            }
            ReorderUnits();
        }
        public void DisbandSquad(string _uniqueID)
        {
            if (saveData == null) return;
            int unitIndex = Array.FindIndex(saveData.playerArmy, x => x.UniqueID == _uniqueID);
            if(unitIndex < 0) {
                Debug.LogError($"Could not find unit with unique ID {_uniqueID} to disband.");
                return;
            }
            Debug.Log($"[Unit] Disbanding {saveData.playerArmy[unitIndex].UnitName} (prestige {saveData.playerArmy[unitIndex].UnitPrestige}) at slot {unitIndex}");
            saveData.playerArmy[unitIndex].UnitIndex = -1;

            // ReorderUnits() consolidates the deployed (<10) and reserve (>=10) sections independently,
            // so a freed deployed slot never gets backfilled by a reserve unit crossing the boundary.
            ReorderUnits();
        }
        public void MergeSquads(List<string> _guidsByPriority)
        {
            List<SquadToLoad> squads = new();
            foreach (string guid in _guidsByPriority)
            {
                SquadToLoad found = Array.Find(saveData.playerArmy, x => x.UniqueID == guid);
                if (found.UniqueID != null) squads.Add(found);
            }
            squads.Sort((a, b) => a.UnitIndex.CompareTo(b.UnitIndex));
            if (squads.Count < 2) return;

            int totalHealth = 0;
            foreach (SquadToLoad s in squads) totalHealth += s.SquadCurrentHealth;

            int remainder = totalHealth;
            for (int i = 0; i < squads.Count; i++)
            {
                int idx = Array.FindIndex(saveData.playerArmy, x => x.UniqueID == squads[i].UniqueID);
                if (idx < 0) continue;
                if (remainder > 0)
                {
                    saveData.playerArmy[idx].SquadCurrentHealth = Mathf.Min(remainder, saveData.playerArmy[idx].SquadMaxHealth);
                    remainder -= saveData.playerArmy[idx].SquadCurrentHealth;
                }
                else
                {
                    saveData.playerArmy[idx].UnitIndex = -1;
                }
            }

            Debug.Log($"[Unit] Merging squads. Total health: {totalHealth}, leftover after fill: {remainder}");

            ReorderUnits();
        }
        public void RenameSquad(string _uniqueID, string _newName)
        {
            bool overrideExists = false;
            for(int i = 0; i < saveData.unitNameOverrides.Count; i++) {
                if(saveData.unitNameOverrides[i].unitGUID == _uniqueID) {
                    saveData.unitNameOverrides[i] = new UnitNameOverrides(_uniqueID, _newName);
                    overrideExists = true;
                    break;
                }
            }
            if(!overrideExists)
            {
                saveData.unitNameOverrides.Add(new UnitNameOverrides(_uniqueID, _newName));
            }
            
            CampaignSaveData snapshot = SaveDataHandler.LoadSnapshot();
            snapshot.unitNameOverrides = saveData.unitNameOverrides;
            if (!DisableSaving) SaveDataHandler.SaveCampaignSnapshot(snapshot);
            OnArmyStructureChanged?.Invoke();
        }
        public void SavePlayerArmy(SquadToLoad[] _playerArmy)
        {
            saveData.playerArmy = _playerArmy;
        }
        public void SaveEnemyArmy(SquadToLoad[] _enemyArmy)
        {
            saveData.enemyArmy = _enemyArmy;
            SaveCampaign();
        }
            public void PrestigeAndCombineUnits(string _uniqueID)
            {
                for (int i = 0; i < saveData.playerArmy.Length; i++) {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;
                    saveData.playerArmy[i].UnitIndex = i;
                }
                SquadToLoad squadToPrestige = Array.Find(saveData.playerArmy, x => x.UniqueID == _uniqueID);
                SquadToLoad[] unitsToCombine = Array.FindAll(saveData.playerArmy,
                    x => x.UnitName == squadToPrestige.UnitName &&
                    x.UnitPrestige == squadToPrestige.UnitPrestige
                );

                List<int> unitsMerged = new();

                for (int i = 0; i < unitsToCombine.Length && unitsMerged.Count < 2; i++)
                {
                    if (unitsToCombine[i].UniqueID != _uniqueID && unitsToCombine[i].UnitIndex != -1)
                    {
                        unitsMerged.Add(unitsToCombine[i].UnitIndex);
                    }
                }
                if (unitsMerged.Count < 2)
                {
                    Debug.LogError("Not enough units to combine for prestige.");
                    return;
                }

                squadToPrestige = PrestigeUnit(squadToPrestige);

                // Debug.Log($"units prestiged: {squadToPrestige.UnitIndex}, removing {unitsMerged[0]} and {unitsMerged[1]}");

                saveData.playerArmy[squadToPrestige.UnitIndex] = squadToPrestige;
                saveData.playerArmy[unitsMerged[0]] = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };
                saveData.playerArmy[unitsMerged[1]] = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };

                ReorderUnits();
            }
            public void PrestigeAndCombineSpecificUnits(string _targetUID, string _consumeUID1, string _consumeUID2)
            {
                for (int i = 0; i < saveData.playerArmy.Length; i++) {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;
                    saveData.playerArmy[i].UnitIndex = i;
                }

                SquadToLoad squadToPrestige = Array.Find(saveData.playerArmy, x => x.UniqueID == _targetUID);
                SquadToLoad consume1 = Array.Find(saveData.playerArmy, x => x.UniqueID == _consumeUID1);
                SquadToLoad consume2 = Array.Find(saveData.playerArmy, x => x.UniqueID == _consumeUID2);

                if (squadToPrestige.UnitIndex == -1 || consume1.UnitIndex == -1 || consume2.UnitIndex == -1)
                {
                    Debug.LogError($"PrestigeAndCombineSpecificUnits: one or more units not found. target={_targetUID} consume1={_consumeUID1} consume2={_consumeUID2}");
                    return;
                }

                squadToPrestige = PrestigeUnit(squadToPrestige);

                saveData.playerArmy[squadToPrestige.UnitIndex] = squadToPrestige;
                saveData.playerArmy[consume1.UnitIndex] = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };
                saveData.playerArmy[consume2.UnitIndex] = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };

                ReorderUnits();
            }
        public void PrestigeAndCombineWithRecruit(string _consumeUID1, string _consumeUID2)
        {
            for (int i = 0; i < saveData.playerArmy.Length; i++) {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;
                saveData.playerArmy[i].UnitIndex = i;
            }

            SquadToLoad target  = Array.Find(saveData.playerArmy, x => x.UniqueID == _consumeUID1);
            SquadToLoad consume = Array.Find(saveData.playerArmy, x => x.UniqueID == _consumeUID2);

            if (target.UnitIndex == -1 || consume.UnitIndex == -1)
            {
                Debug.LogError($"PrestigeAndCombineWithRecruit: unit not found. uid1={_consumeUID1} uid2={_consumeUID2}");
                return;
            }

            target = PrestigeUnit(target);

            saveData.playerArmy[target.UnitIndex]   = target;
            saveData.playerArmy[consume.UnitIndex]  = new SquadToLoad { UnitIndex = -1, UniqueID = Guid.NewGuid().ToString() };

            ReorderUnits();
        }
        public void ReorderUnits()
            {
                // Get first 10 units
                List<SquadToLoad> deployedUnits = new();
                List<SquadToLoad> reserveUnits = new();

                // Iterate through player army, as long as the index is not -1 add it to the list based on i
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;

                    if (i < 10)
                    {
                        deployedUnits.Add(saveData.playerArmy[i]);
                    }
                    else
                    {
                        reserveUnits.Add(saveData.playerArmy[i]);
                    }
                }

                // Add new squads to load to both lists until they are 10 long
                while (deployedUnits.Count < 10)
                {
                    deployedUnits.Add(new SquadToLoad
                    {
                        UnitIndex = -1,
                        UniqueID = Guid.NewGuid().ToString()
                    });
                }
                while (reserveUnits.Count < MaxReserveSlots)
                {
                    reserveUnits.Add(new SquadToLoad
                    {
                        UnitIndex = -1,
                        UniqueID = Guid.NewGuid().ToString()
                    });
                }

                // Combine both and set as player army
                deployedUnits.AddRange(reserveUnits);
                saveData.playerArmy = deployedUnits.ToArray();

                saveData.playerArmy = ResetIndexes(saveData.playerArmy);
                OnArmyStructureChanged?.Invoke();
            }
        // Update UnitIndex to match new array positions (skip blanks)
        private SquadToLoad[] ResetIndexes(SquadToLoad[] playerArmy)
        {
            for (int i = 0; i < playerArmy.Length; i++)
            {
                if (playerArmy[i].UnitIndex != -1)
                {
                    playerArmy[i].UnitIndex = i;
                }
            }
            return playerArmy;
        }
        private SquadToLoad PrestigeUnit(SquadToLoad _squadToPrestige)
        {
            _squadToPrestige.SquadCurrentHealth = _squadToPrestige.SquadMaxHealth;
            _squadToPrestige.UnitPrestige++;
            Debug.Log($"[Unit] Prestiged {_squadToPrestige.UnitName} to prestige {_squadToPrestige.UnitPrestige}");

            if (_squadToPrestige.UnitPrestige == 1) {
                SteamAchievements.Unlock(AchievementId.Prestige2);
            }
            if (_squadToPrestige.UnitPrestige == 2) {
                SteamAchievements.Unlock(AchievementId.Prestige3);
            }
            saveData.RunStats.unitsPrestiged++;

            //achievement check - living legend (3+ units at max prestige simultaneously).
            //Max UnitPrestige is 2. The just-prestiged struct isn't written back to playerArmy yet,
            //so count the array excluding this unit, then add it via _squadToPrestige.
            int maxPrestigeUnits = _squadToPrestige.UnitPrestige >= 2 ? 1 : 0;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (saveData.playerArmy[i].UniqueID == _squadToPrestige.UniqueID) continue;
                if (saveData.playerArmy[i].UnitPrestige >= 2) maxPrestigeUnits++;
            }
            if (maxPrestigeUnits >= 3) SteamAchievements.Unlock(AchievementId.LivingLegend);

            return _squadToPrestige;
        }
        public List<UnitAttribute> GetEligiblePrestigeTraitsForUnit(UnitName _unitName) =>
            TabletopTavernConstants.GetEligiblePrestigeTraits(TabletopTavernData.Instance.GetSquadStats(_unitName));

        public List<UnitAttribute> GetPrestigeTraitOptions(UnitName _unitName, int _count = 3)
        {
            List<UnitAttribute> pool = GetEligiblePrestigeTraitsForUnit(_unitName);
            List<UnitAttribute> options = new();
            while (options.Count < _count && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                options.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return options;
        }

        public bool TryGetNextPendingPrestigeTraitChoice(out SquadToLoad pending)
        {
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                SquadToLoad squad = saveData.playerArmy[i];
                if (squad.UnitIndex == -1 || squad.UnitPrestige != 2 || squad.PrestigeTrait != UnitAttribute.None) continue;
                if (GetEligiblePrestigeTraitsForUnit(squad.UnitName).Count == 0) continue;

                pending = squad;
                return true;
            }
            pending = default;
            return false;
        }
        public void ResolvePrestigeTraitChoice(string _uniqueID, UnitAttribute _chosenTrait)
        {
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (saveData.playerArmy[i].UniqueID != _uniqueID) continue;
                saveData.playerArmy[i].PrestigeTrait = _chosenTrait;
                break;
            }
            OnArmyStructureChanged?.Invoke();
        }
            public void PrestigeSpecificUnit(SquadToLoad _squadToPrestige)
            {
                SquadToLoad[] squadToLoads = saveData.playerArmy;
                for(int i = 0; i < squadToLoads.Length; i++) {
                    if(squadToLoads[i].UnitIndex == -1) continue;
                    squadToLoads[i].UnitIndex = i;
                }
                SquadToLoad targetedSquad = squadToLoads.Where(squad => squad.UniqueID == _squadToPrestige.UniqueID).FirstOrDefault();
                string targetSquadID = targetedSquad.UniqueID;
                int newPrestige = targetedSquad.UnitPrestige + 1;


                for (int i = 0; i < squadToLoads.Length; i++)
                {
                    SquadToLoad squad = squadToLoads[i];
                    if (squad.UniqueID == targetedSquad.UniqueID)
                    {
                        squadToLoads[i] = PrestigeUnit(targetedSquad);
                    }
                }
                CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(new ArmyJuice {
                    uniqueID = targetSquadID,
                    armyJuiceEnum = ArmyJuiceEnum.Prestige,
                    value = newPrestige
                });

                saveData.playerArmy = squadToLoads;
                OnArmyStructureChanged?.Invoke();
            }
        public void TrialOfGrassesPrestigeSpecificUnit(SquadToLoad _squadToPrestige)
        {
            SquadToLoad[] squadToLoads = saveData.playerArmy;
            for(int i = 0; i < squadToLoads.Length; i++) {
                if(squadToLoads[i].UnitIndex == -1) continue;
                squadToLoads[i].UnitIndex = i;
            }
            SquadToLoad targetedSquad = squadToLoads.Where(squad => squad.UniqueID == _squadToPrestige.UniqueID).FirstOrDefault();

            for(int i = 0; i < squadToLoads.Length; i++) {
                SquadToLoad squad = squadToLoads[i];
                if (squad.UniqueID == targetedSquad.UniqueID)
                {
                    SquadToLoad tog = squadToLoads[i];
                    tog.SquadCurrentHealth = _squadToPrestige.SquadMaxHealth / 10;
                    tog.UnitPrestige = 2;
                    squadToLoads[i] = tog;
                }
            }
            string targetSquadID = targetedSquad.UniqueID;
            CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(
                new ArmyJuice
                {
                    uniqueID = targetSquadID,
                    armyJuiceEnum = ArmyJuiceEnum.Prestige,
                    value = 3
                }
            );

            SteamAchievements.Unlock(AchievementId.Prestige3);

            saveData.playerArmy = squadToLoads;
            OnArmyStructureChanged?.Invoke();
        }
            public string[] PrestigeRandomUnits2()
            {
                List<SquadToLoad> prestigeTargets = new();
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;

                    if (saveData.playerArmy[i].UnitPrestige < 2)
                    {
                        prestigeTargets.Add(saveData.playerArmy[i]);
                    }
                }
                if (prestigeTargets.Count == 0) return null;

                //check through the targets, grab a random one and prestige it
                SquadToLoad squadToPrestige = prestigeTargets[UnityEngine.Random.Range(0, prestigeTargets.Count)];
                squadToPrestige = PrestigeUnit(squadToPrestige);
                saveData.playerArmy[squadToPrestige.UnitIndex] = squadToPrestige;

                CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(
                    new ArmyJuice
                    {
                        uniqueID = squadToPrestige.UniqueID,
                        armyJuiceEnum = ArmyJuiceEnum.Prestige,
                        value = squadToPrestige.UnitPrestige
                    }
                );

                //if there are more than 1 unit in the prestigeTargets, prestige another one, preventing the same unit from being prestiged twice
                SquadToLoad secondSquadToPrestige = default;
                if (prestigeTargets.Count > 1)
                {
                    secondSquadToPrestige = prestigeTargets[UnityEngine.Random.Range(0, prestigeTargets.Count)];
                    while (secondSquadToPrestige.UniqueID == squadToPrestige.UniqueID)
                    {
                        secondSquadToPrestige = prestigeTargets[UnityEngine.Random.Range(0, prestigeTargets.Count)];
                    }
                    secondSquadToPrestige = PrestigeUnit(secondSquadToPrestige);
                    saveData.playerArmy[secondSquadToPrestige.UnitIndex] = secondSquadToPrestige;

                    CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(
                        new ArmyJuice
                        {
                            uniqueID = secondSquadToPrestige.UniqueID,
                            armyJuiceEnum = ArmyJuiceEnum.Prestige,
                            value = secondSquadToPrestige.UnitPrestige
                        }
                    );
                }

                OnArmyStructureChanged?.Invoke();

                return new string[] { squadToPrestige.UnitName.ToString(), prestigeTargets.Count > 1 ? secondSquadToPrestige.UnitName.ToString() : null };
            
            }
            public string HealRandomUnitToFull()
            {
                List<int> eligibleIndices = new();
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;
                    if (saveData.playerArmy[i].SquadCurrentHealth == 0) continue;
                    if (saveData.playerArmy[i].SquadCurrentHealth >= saveData.playerArmy[i].SquadMaxHealth) continue;
                    eligibleIndices.Add(i);
                }
                if (eligibleIndices.Count == 0) return string.Empty;

                int idx = eligibleIndices[UnityEngine.Random.Range(0, eligibleIndices.Count)];
                saveData.playerArmy[idx].SquadCurrentHealth = saveData.playerArmy[idx].SquadMaxHealth;
                OnUnitHealthChanged?.Invoke();

                return saveData.playerArmy[idx].UnitName.ToString();
            }
            public string PrestigeRandomUnit()
            {
                List<SquadToLoad> prestigeTargets = new();
                for (int i = 0; i < saveData.playerArmy.Length; i++)
                {
                    if (saveData.playerArmy[i].UnitIndex == -1) continue;

                    if (saveData.playerArmy[i].UnitPrestige < 2)
                    {
                        prestigeTargets.Add(saveData.playerArmy[i]);
                    }
                }
                if (prestigeTargets.Count == 0) return "No units to prestige";

                SquadToLoad squadToPrestige = prestigeTargets[UnityEngine.Random.Range(0, prestigeTargets.Count)];
                squadToPrestige = PrestigeUnit(squadToPrestige);
                saveData.playerArmy[squadToPrestige.UnitIndex] = squadToPrestige;
                OnArmyStructureChanged?.Invoke();

                return squadToPrestige.UnitName.ToString();
            }
        public bool CheckForPrestigeAvailability(UnitName _unitName, int _unitLevel)
        {
            int unitsWithSameNameAndLevel = 0;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;
                if (saveData.playerArmy[i].SquadCurrentHealth == 0) continue;

                if (saveData.playerArmy[i].UnitName == _unitName &&
                    saveData.playerArmy[i].UnitPrestige == _unitLevel &&
                    saveData.playerArmy[i].UnitPrestige < 2)
                {
                    unitsWithSameNameAndLevel++;
                }
            }
            return unitsWithSameNameAndLevel > 2;
        }
            #endregion
        public void SaveBattlefieldPreset(BattleFieldPreset _battleFieldPreset)
        {
            // Debug.Log($"saving battlefield preset with biome: {_battleFieldPreset.biome}");
            saveData.battleFieldPreset = _battleFieldPreset;
            SaveCampaign();
        }
        public void AddEventReward(EventReward _eventReward)
        {
            foreach (EventOutcomeModifier eventOutcomeModifier in _eventReward.EventOutcome.EventOutcomeModifiers)
            {
                switch (eventOutcomeModifier.EventOutcomeModifierEnum)
                {
                    case EventOutcomeModifierEnum.Gold:
                        string localizedString = LocalizationManager.Instance.GetText("Rewards");
                        CampaignManager.Instance.GoldManager.ModifyGold((int)eventOutcomeModifier.Value, localizedString);
                        break;
                    case EventOutcomeModifierEnum.UnitHealth:
                        if (saveData.Gear.Contains(GearID.MichaelsSecretStuff) && eventOutcomeModifier.Value < 0)
                        {
                            continue;
                        }
                        ModifyTroopHealth(eventOutcomeModifier.Value);
                        break;
                }
            }
        }
        /// <summary>
        /// Modifies the gold amount by _goldAmount. Can be positive or negative.
        /// </summary>
        /// <param name="_goldAmount"> the amount to increase gold amount by</param>
        public void ModifyGoldSaveDataValue(int _goldAmount)
        {
            saveData.goldAmount += _goldAmount;

            //clamp gold to be at least 0
            saveData.goldAmount = math.max(saveData.goldAmount, 0);
            
            saveData.RunStats.goldEarned += _goldAmount > 0 ? _goldAmount : 0;
            if (!DisableSaving) SaveDataHandler.SaveCampaign(saveData);

            if (saveData.goldAmount > 20)
                SteamAchievements.Unlock(AchievementId.TwentyGold);
        }

        #region Gear
        public bool CanAquireGear()
        {
            return saveData.Gear.Count < maxGear;
        }
        public void AquireGear(GearID _gearName)
        {
            Debug.Log($"Aquiring gear {_gearName}");
            saveData.Gear.Add(_gearName);
            saveData.RunStats.gearAquired++;
            CampaignManager.Instance.GearManager.AquireGear(_gearName);
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.SellGear });
            SaveDataHandler.AquiredGear(_gearName);
            int gearRequiredForAchievement = 5;
# if DEMO
                gearRequiredForAchievement = 3;
# endif
            if (saveData.Gear.Count == gearRequiredForAchievement)
            {
                SteamAchievements.Unlock(AchievementId.FullGear);
            }

            OnGearChanged?.Invoke();
        }
        public void SellGear(GearID _gearName, int _sellValue)
        {
            Debug.Log($"Sold gear {_gearName}");
            saveData.Gear.Remove(_gearName);
            if (!saveData.SoldGear.Contains(_gearName)) {
                saveData.SoldGear.Add(_gearName);
            }
            CampaignManager.Instance.GearManager.UnAquireGear(_gearName);
            string localizedString = LocalizationManager.Instance.GetText($"{_gearName}Name");
            CampaignManager.Instance.GoldManager.ModifyGold(_sellValue, localizedString);
            CampaignSaveData tempSaveData = SaveDataHandler.Load();
            tempSaveData.Gear = saveData.Gear;
            tempSaveData.SoldGear = saveData.SoldGear;
            // tempSaveData.goldAmount = saveData.goldAmount;
            SaveDataHandler.SaveCampaign(tempSaveData);
            OnGearChanged?.Invoke();
        }
        // Called when the player leaves the shop, so gear sold before or during that visit becomes
        // drawable again from the next shop/loot roll onward.
        public void ClearSoldGear()
        {
            if (saveData.SoldGear.Count == 0) return;
            saveData.SoldGear.Clear();
            CampaignSaveData tempSaveData = SaveDataHandler.Load();
            tempSaveData.SoldGear = saveData.SoldGear;
            SaveDataHandler.SaveCampaign(tempSaveData);
        }
        // Draws random gear excluding both currently-owned and previously-sold-this-run gear,
        // so a sold item can't immediately reappear from the next pack/loot roll. If the sold-gear
        // exclusion would leave too few eligible items to satisfy _amount, it resets (clears
        // SoldGear) rather than block or infinite-loop the draw.
        public List<GearID> DrawRandomGear(int _amount, bool _isShop = false)
        {
            List<GearID> exclusionList = GetGearExclusionList();
            if (!GearData.HasEnoughEligibleGear(exclusionList, _amount, _isShop)) {
                saveData.SoldGear.Clear();
                exclusionList = GetGearExclusionList();
            }
            return GearData.GetRandomGear(_amount, exclusionList, GetSeededRandom(), saveData.bookNumber, _isShop);
        }
        private List<GearID> GetGearExclusionList()
        {
            List<GearID> exclusionList = new List<GearID>(saveData.Gear);
            foreach (GearID gearID in saveData.SoldGear) {
                if (!exclusionList.Contains(gearID)) {
                    exclusionList.Add(gearID);
                }
            }
            return exclusionList;
        }
        #endregion
        
        #region Consumables
        public bool HasRoomForConsumable()
        {
            // Debug.Log($"Checking consumable capacity: {saveData.consumables.Count}/{consumableCapacity}");
            return saveData.consumables.Count < consumableCapacity;
        }
        public void AquireConsumable(ConsumableEnum _consumable)
        {
            saveData.consumables.Add(_consumable);
            SaveDataHandler.AquiredPotionForCollection(_consumable);

            CampaignManager.Instance.ArmyJuiceManager.MarkConsumableAsNew(saveData.consumables.Count - 1);
            OnConsumablesChanged?.Invoke();
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1]{ TutorialData.ConsumableUsage });
        }
        public void SellConsumable(Consumable consumable, int sellValue)
        {
            string localizedString = LocalizationManager.Instance.GetText($"{consumable.ConsumableEnum}Name");
            CampaignManager.Instance.GoldManager.ModifyGold(sellValue, localizedString);
            RemoveConsumable(consumable.ConsumableEnum);
        }
        public void RemoveConsumable(ConsumableEnum _consumable)
        {
            saveData.consumables.Remove(_consumable);
            SaveCampaign();
            OnConsumablesChanged?.Invoke();
        }

        // Persisted "guarantee next roll" flag set by drinking a Fateshine Elixir. Read + cleared by the
        // roll sites (EventPanel, GamesPanel, and - via SaveDataHandler - BattleDiceRollPanel).
        public bool FateshineElixirArmed => saveData.fateshineElixirArmed;
        public void ArmFateshineElixir()     { saveData.fateshineElixirArmed = true;  SaveCampaign(); }
        public void ConsumeFateshineElixir() { saveData.fateshineElixirArmed = false; SaveCampaign(); }
        #endregion

        #region Healing
        public void HealTroopsInReserve(bool onlyHalf)
        {
            SquadToLoad[] playerSquadsSaveData = saveData.playerArmy;
            for(int i = 10; i < playerSquadsSaveData.Length; i++)
            {
                if(playerSquadsSaveData[i].SquadCurrentHealth == 0) continue;

                int healthRecovery = (int)(playerSquadsSaveData[i].SquadMaxHealth * TabletopTavernConstants.RESERVES_HEAL_AMOUNT);
                if(onlyHalf) healthRecovery /= 2;
                healthRecovery *= ReservesHealMultiplier;
                if(CampaignManager.Instance.GearManager.CheckForGear(GearID.ChugJug)) healthRecovery*=2;
        
                playerSquadsSaveData[i].SquadCurrentHealth = math.min(
                    playerSquadsSaveData[i].SquadCurrentHealth + healthRecovery, playerSquadsSaveData[i].SquadMaxHealth
                );
            }
            SavePlayerArmy(playerSquadsSaveData);
            OnUnitHealthChanged?.Invoke();
        }
        public void NonHealReserves()
        {
            SquadToLoad[] playerSquadsSaveData = saveData.playerArmy;
            SavePlayerArmy(playerSquadsSaveData);
            OnUnitHealthChanged?.Invoke();
        }
        public void CorrectHealthOfWithdrawnSquads()
        {
            int squadCount = 0;
            for(int i = 0; i < CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy.Length; i++) {
                if(CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i].UnitIndex != -1) {
                    squadCount++;
                }
            }
            SquadToLoad[] playerArmy =  saveData.playerArmy;
            SquadToLoad[] withdrawnSquads = GetWithdrawnSquads();
            for(int i = 0; i < withdrawnSquads.Length; i++)
            {
                for(int j = 0; j < playerArmy.Length; j++)
                {
                    if(playerArmy[j].UniqueID == withdrawnSquads[i].UniqueID)
                    {
                        playerArmy[j].SquadCurrentHealth = withdrawnSquads[i].SquadCurrentHealth;
                        // Debug.Log($"Correcting health of withdrawn squad {playerArmy[j].UnitName} to {playerArmy[j].SquadCurrentHealth}");
                    }
                }
            }
            saveData.withdrawnSquads.Clear();
            saveData.playerArmy = playerArmy;
        }
        #endregion

        public void GenerateTown(int _selectedNodeIndex, int level)
        {
            int seed = GetSeededRandom();
            int bookNumber = saveData.bookNumber;
            List<GearID> gearLooted = DrawRandomGear(1);

            TownSize townSize = TownSaveData.GenerateTownSize(level);
            Race townRace = GenerateTownRace(_selectedNodeIndex, bookNumber);
            int bountyAmount = TownSaveData.GenerateBountyAmount(townSize, seed);

            //Burn them all: 2x gold from sacking cities
            if (HeroBonusManager.Instance.ActiveHeroID == 4)
            {
                bountyAmount *= 2;
            }

            //Northern Looters: 2x gold from sacking cities
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.NorthernLooters))
            {
                bountyAmount *= 2;
            }

            List<UnitTier> unitsPool = TabletopTavernData.Instance.GetSquadsWithTiersFromRace(townRace);

            //DifficultyMod 16
            bool isImperator = CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Imperator;
            //DifficultyMod 10
            bool enemyPrestigeEligible = CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Duke;
            //DifficultyMod 14
            bool enemyPrestigeEnhanced = CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Emperor;
            SquadToLoad[] townGarrison = ArmyCreator.GenerateTownGarrison(townSize, seed, unitsPool, isImperator, bookNumber, enemyPrestigeEligible, enemyPrestigeEnhanced);
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.AuraFarming))
            {
                //remove the last squad from the array
                townGarrison = townGarrison.Take(townGarrison.Length - 1).ToArray();
            }

            //bear spray replaces large units with infantry
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.BearSpray))
            {
                townGarrison = ArmyCreator.ReplaceMonsterUnits(townGarrison, seed, unitsPool);
            }

            TownSaveData townSaveData = new()
            {
                townSize = townSize,
                townRace = townRace,
                townLootGearIDs = gearLooted,
                bountyAmount = bountyAmount,
                townGarrisonUnits = townGarrison,
                townName = townNames[UnityEngine.Random.Range(0, townNames.Length)],
            };

            Debug.Log($"Generated town with size {townSize} race {townRace} and bounty amount {bountyAmount}");
            SetTownData(townSaveData);
        }
        public static Race GenerateTownRace(int _seed, int bookNumber)
        {
            System.Random random = new(Seed: _seed + (bookNumber * 13));
            int randomInt = random.Next(0, 100);
            if (randomInt < 13) {
                return Race.IronLegion;
            } else if (randomInt < 25) {
                return Race.Gruntkin;
            } else if (randomInt < 37) {
                return Race.RavenHost;
            } else if (randomInt < 50) {
                return Race.TaelindorForest;
            } else if (randomInt < 62) {
                return Race.SanguineCourt;
            } else if (randomInt < 75) {
                return Race.SakuraDynasty;
            } else if (randomInt < 87) {
                return Race.DeepstoneHold;
            } else {
                return Race.DrakosaurBrood;
            }
        }
        public static Weather GenerateNodeWeather(int nodeIndex, int campaignSeed, int bookNumber, MapRegion mapRegion)
        {
            System.Random random = new(campaignSeed + nodeIndex + (bookNumber * 13));
            return mapRegion.GetRandomWeather(random);
        }
        public static Biome GenerateNodeBiome(int nodeIndex, int campaignSeed, int bookNumber, MapRegion mapRegion)
        {
            System.Random random = new(campaignSeed * 7 + nodeIndex + (bookNumber * 13));
            return mapRegion.GetRandomBiome(random);
        }
        public void StartGarrisonBattle()
        {
            Debug.Log($"Starting garrison battle");
            saveData.townData.townInteractionStatus = TownInteractionStatus.GarrisonBattleStarted;
            SaveCampaign();
        }
        public void SaveSquadsPostAutoresolve(
            SquadToLoad[] _playerSquads, SquadToLoad[] _enemySquads, bool _playerWon, List<SquadKillsStored> _squadIdKillCounter, List<SquadLossesStored> _squadIdLossCounter)
        {
            // Debug.Log($"playersquads length post battle: {_playerSquads.Length}");
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                string GUID = saveData.playerArmy[i].UniqueID;
                for(int j = 0; j < _playerSquads.Length; j++)
                {
                    if (_playerSquads[j].UniqueID == GUID)
                    {
                        saveData.playerArmy[i] = _playerSquads[j];
                        break;
                    }
                }
            }
            saveData.enemyArmy = _enemySquads;
            saveData.battleCompleted = true;
            saveData.playerWonBattle = _playerWon;
            saveData.SquadKillsStore = _squadIdKillCounter;
            saveData.HistoricalKillStore = SaveDataHandler.AddToHistoricalKills(saveData.HistoricalKillStore, _squadIdKillCounter);
            SaveDataHandler.RecordUnitNameKills(_playerSquads, _squadIdKillCounter);
            // Debug.Log($"new historical kill store count: {saveData.HistoricalKillStore.Count}");

            int totalKills = 0;
            foreach (var squadKill in _squadIdKillCounter) totalKills += squadKill.Kills;
            saveData.RunStats.enemiesSlain += totalKills;

            saveData.SquadLossesStore = _squadIdLossCounter;

            //achievement check - flawless victory (won losing zero units)
            if (_playerWon)
            {
                int totalUnitsLost = 0;
                if (_squadIdLossCounter != null)
                {
                    foreach (SquadLossesStored loss in _squadIdLossCounter) totalUnitsLost += loss.Losses;
                }
                if (totalUnitsLost == 0) SteamAchievements.Unlock(AchievementId.FlawlessVictory);
            }

            foreach (var playerSquad in _playerSquads)
            {
                // Hybrids shoot, so they disqualify the No Archers run just like a dedicated shooter.
                if (TabletopTavernConstants.FightsAtRange(TabletopTavernData.Instance.GetSquadStats(playerSquad.UnitName).unitType))
                {
                    saveData.archerUsedInBattle = true;
                    break;
                }
            }

            if (!DisableSaving) SaveDataHandler.SaveCampaignSnapshot(saveData);
        }
        public void PrestigeUnitsOnKills()
        {
            // saveData.playerArmy = SaveDataHandler.Load().playerArmy;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                //skip empty units
                if (saveData.playerArmy[i].UnitIndex == -1) continue;

                //get squad to check
                SquadToLoad squadToCheck = saveData.playerArmy[i];

                //check if it has the forge fury tempering attribute
                if (!TabletopTavernData.Instance.GetSquadStats(squadToCheck.UnitName).SquadAttributes.ForgefuryTempering) continue;

                //try get squad kills stored for this unit
                int killStoreIndex = saveData.HistoricalKillStore.FindIndex(x => x.SquadGUID == squadToCheck.UniqueID);
                if (killStoreIndex < 0) continue;

                int kills = saveData.HistoricalKillStore[killStoreIndex].Kills;

                // Thresholds are cumulative (50 for prestige 1, 100 for prestige 2), so a squad that
                // banks enough kills in a single battle must be able to prestige more than once here -
                // otherwise the second level silently waits for the next battle's results screen.
                // PrestigeSpecificUnit writes back into playerArmy in place, so re-read the slot each pass.
                while (saveData.playerArmy[i].UnitPrestige < 2 &&
                       kills >= TabletopTavernConstants.FORGEFURY_TEMPERING_KILLS_REQUIRED * (saveData.playerArmy[i].UnitPrestige + 1))
                {
                    int prestigeBefore = saveData.playerArmy[i].UnitPrestige;
                    PrestigeSpecificUnit(saveData.playerArmy[i]);

                    // Guard against spinning forever if the prestige didn't take (e.g. GUID lookup miss).
                    if (saveData.playerArmy[i].UnitPrestige == prestigeBefore)
                    {
                        Debug.LogError($"[Unit] ForgefuryTempering prestige did not apply to {squadToCheck.UnitName} ({squadToCheck.UniqueID}), aborting.");
                        break;
                    }
                }
            }
        }
        public int GetSquadHistoricalKillCount(string _uniqueID)
        {
            if(saveData == null) return 0;
            SquadKillsStored squadKillsStored = saveData.HistoricalKillStore.Find(x => x.SquadGUID == _uniqueID);
            if (squadKillsStored.Kills > 250)
            {
                SteamAchievements.Unlock(AchievementId.HighKill);
            }
            return squadKillsStored.Kills;
        }
        public int GetSeededRandom()
        {
            return saveData.seed * (saveData.activeMapLayer + 2) * (saveData.bookNumber + 1) + saveData.GetSelectedNodeIndex() + saveData.Rolls;
        }
        // Increments the per-run shop purchase counter (used by the MerchantsBane achievement).
        public void RegisterShopPurchase()
        {
            saveData.RunStats.shopPurchases++;
        }
        // Tracks gold wagered at games this run; unlocks Gamba once the run total reaches 100.
        public void RegisterGoldWagered(int amount)
        {
            if (saveData == null || amount <= 0) return;
            saveData.RunStats.goldWagered += amount;
            if (saveData.RunStats.goldWagered >= 100) SteamAchievements.Unlock(AchievementId.Gamba);
        }
        // Flags that a consumable was used this run (disqualifies BareEssentials).
        public void MarkConsumableUsed()
        {
            if (saveData == null) return;
            saveData.RunStats.consumableUsed = true;
        }
        // A battle offered the ransom-captives reward (denominator for Merciful).
        public void RegisterRansomOffered()
        {
            if (saveData == null) return;
            saveData.RunStats.ransomsOffered++;
        }
        // The ransom-captives reward was claimed (numerator for Merciful).
        public void RegisterRansomChosen()
        {
            if (saveData == null) return;
            saveData.RunStats.ransomsChosen++;
        }
        // Recomputes army-derived run stats: peak total models (QualityOverQuantity) and the
        // ever-held-a-duplicate flag (OneOfAKind). Subscribed to OnArmyStructureChanged + sampled on load.
        public void EvaluateArmyRunStats()
        {
            if (saveData == null || saveData.playerArmy == null) return;

            int totalModels = 0;
            HashSet<UnitName> seenNames = new();
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                SquadToLoad squad = saveData.playerArmy[i];
                if (squad.UnitIndex == -1) continue;

                totalModels += squad.maxUnitCount;

                if (!seenNames.Add(squad.UnitName))
                    saveData.RunStats.heldDuplicateUnit = true;
            }

            if (totalModels > saveData.RunStats.maxArmyModels)
                saveData.RunStats.maxArmyModels = totalModels;
        }
        public void CheckPostRunAchievements()
        {
#if DEMO
            Debug.Log($"Book 2 completed - Demo beaten on {saveData.difficultyLevel}");
#else
            Debug.Log($"Book 3 completed - Release beaten on {saveData.difficultyLevel}");
#endif


            SteamAchievements.Unlock(AchievementId.WinDemo);

            if (saveData.RunStats.shopPurchases == 0)
            {
                Debug.Log($"Unlocking Merchant's Bane Achievement");
                SteamAchievements.Unlock(AchievementId.MerchantsBane);
            }

            // Merciful: ransom claimed at every battle that offered it (and at least one did).
            if (saveData.RunStats.ransomsOffered > 0 && saveData.RunStats.ransomsChosen >= saveData.RunStats.ransomsOffered)
                SteamAchievements.Unlock(AchievementId.Merciful);

            // One of a Kind: never held a duplicate unit across the whole run.
            if (!saveData.RunStats.heldDuplicateUnit)
                SteamAchievements.Unlock(AchievementId.OneOfAKind);

            // Quality Over Quantity: army never exceeded 100 models.
            if (saveData.RunStats.maxArmyModels <= 100)
                SteamAchievements.Unlock(AchievementId.QualityOverQuantity);

            // Bare Essentials: no consumable used all run.
            if (!saveData.RunStats.consumableUsed)
                SteamAchievements.Unlock(AchievementId.BareEssentials);

            // Dead Shall Serve: Sanguine Court run where every unit gained came from Raise Dead.
            // The starting army does not route through RecruitSquad, so it never disqualifies.
            if (!saveData.RunStats.gainedUnitOutsideRaiseDead &&
                HeroData.GetRaceFromHero(saveData.heroID) == Race.SanguineCourt)
                SteamAchievements.Unlock(AchievementId.DeadShallServe);

            // "Uh, pause...": never used the pause button all run. Godking only - on lower
            // difficulties you can trivially auto-resolve every battle and never get the chance to pause.
            if (saveData.difficultyLevel == TT_Difficulty.Godking && !saveData.RunStats.pauseUsed)
                SteamAchievements.Unlock(AchievementId.UhPause);

            if (saveData.difficultyLevel == TT_Difficulty.Godking)
            {
                Debug.Log($"Unlocking Max Difficulty Achievement");
                SteamAchievements.Unlock(AchievementId.MaxDifficulty);
            }

            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();

            if(saveData.RunStats.gearAquired == 0 && playerSaveData.lastStartingGearId == GearID.None)
            {
                Debug.Log($"Unlocking No Gear Achievement");
                SteamAchievements.Unlock(AchievementId.NoGearRun);
            }

            if(!saveData.archerUsedInBattle)
            {
                Debug.Log($"Unlocking No Archers Achievement");
                SteamAchievements.Unlock(AchievementId.NoArchersRun);
            }

            SavePostRunDifficultyData();
            CheckGodkingCompletionAchievements();
        }

        // Saves the completed difficulty for this hero to playerSaveData. No Steam dependency.
        public void SavePostRunDifficultyData()
        {
            if (saveData.difficultyLevel != TT_Difficulty.Godking) return;

            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();

            int currentHeroID = saveData.heroID;
            int newDifficulty = (int)saveData.difficultyLevel;

            if (newDifficulty > playerSaveData.MaxDifficultyOverall)
                playerSaveData.MaxDifficultyOverall = newDifficulty;

            bool found = false;
            for (int i = 0; i < playerSaveData.HeroDifficultiesCompleted.Count; i++)
            {
                if (playerSaveData.HeroDifficultiesCompleted[i].HeroID == currentHeroID)
                {
                    if (!playerSaveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Contains(newDifficulty))
                        playerSaveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Add(newDifficulty);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                playerSaveData.HeroDifficultiesCompleted.Add(new HeroDifficultiesCompleted()
                {
                    HeroID = currentHeroID,
                    DifficultiesCompleted = new List<int>() { newDifficulty }
                });
            }

            SaveDataHandler.SavePlayerSaveData(playerSaveData);
            SaveDataHandler.RefreshTavernThemeUnlocks();
        }

        // Checks playerSaveData for godking completions and unlocks the achievement if earned.
        // Safe to call on startup since it only reads from playerSaveData.
        public static void CheckGodkingCompletionAchievements()
        {
            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();

            int godkingCompletions = 0;
            for (int i = 0; i < playerSaveData.HeroDifficultiesCompleted.Count; i++)
            {
                if (playerSaveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Contains((int)TT_Difficulty.Godking))
                    godkingCompletions++;
            }

#if DEMO
            if (godkingCompletions >= 4)
#else
            if (godkingCompletions >= 16)
#endif
            {
                Debug.Log($"Unlocking Max Difficulty All Heroes Achievement");
                SteamAchievements.Unlock(AchievementId.MaxDifficultyAllHeroes);
            }
        }
        public void CheckForFourFactions()
        {
            List<Race> racesInArmy = new();
            foreach (SquadToLoad squad in saveData.playerArmy)
            {
                if (squad.UnitIndex == -1) continue;

                Race raceOfSquad = TabletopTavernData.Instance.GetRaceFromUnitName(squad.UnitName);
                if (!racesInArmy.Contains(raceOfSquad))
                    racesInArmy.Add(raceOfSquad);
            }
            if (racesInArmy.Count >= 4)
            {
                Debug.Log($"Unlocking Four Factions Achievement");
                SteamAchievements.Unlock(AchievementId.FourUniqueFactionsRun);
            }
        }

        #region devtools
        [ContextMenu("Open Campaign Save Folder")]
        public void OpenCampaignSaveFolder()
        {
            OpenLogsExtension.OpenLogs();
        }
        # if UNITY_EDITOR
        [ContextMenu("Record Hero Completion For Testing")]
        public void RecordHeroCompletionForTestingContextMenu()
        {
            SaveDataHandler.RecordHeroCompletionForTesting(HeroData.EdricValeward.HeroID, TT_Difficulty.King);
        }
        #endif
        #endregion
    }
}