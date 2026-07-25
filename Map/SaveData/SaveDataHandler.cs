using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using TJ;
using System;
using Memori.Steamworks;
using Memori.Metaprogression;

namespace Memori.SaveData
{
    [Serializable] public class CampaignSaveData
    {
        public int seed;
        public int activeMapLayer;
        public int bookNumber;
        public int goldAmount;
        public List<int> nodePath;
        public List<GearID> Gear;
        // Gear sold this run, excluded from future gear-pack/loot draws so a sold item can't
        // immediately reappear. Cleared on leaving the shop, or automatically once it would
        // exhaust the draw pool.
        public List<GearID> SoldGear = new();
        public List<ConsumableEnum> consumables = new ();
        public int heroID;
        public SquadToLoad[] playerArmy = new SquadToLoad[13];
        public List<SquadBattlePosition> playerSquadBattlePositions = new();
        public List<SavedSquadGroup> playerSquadGroups = new();
        public SquadToLoad[] enemyArmy = Array.Empty<SquadToLoad>();
        [SerializeField] private int selectedNodeIndex = -1;
        public TownSaveData townData;
        public UnitName[] recruitableUnits;
        public GearID[] recruitableGear;
        public bool nodeGenerated;
        public bool nodesRevealed;
        public bool battleCompleted;
        public bool playerWonBattle;
        public List<SquadKillsStored> SquadKillsStore;
        public List<SquadKillsStored> HistoricalKillStore;
        public List<SquadLossesStored> SquadLossesStore;
        public List<UnitNameOverrides> unitNameOverrides;
        public RunStats RunStats;
        public BattleFieldPreset battleFieldPreset;
        public int turnsSincePotato;
        public int Rolls;
        public List<SquadToLoad> withdrawnSquads;
        public List<int> eventOrdering;
        public int BattlesFought;
        public TT_Difficulty difficultyLevel;
        public bool snapShot;
        public bool blank;
        public int signatureUnitPacksPurchased;
        public int townsSacked;
        public bool archerUsedInBattle;
        public Guid runUUID;

        public CampaignSaveData(int _seed, int _hero, int _startingGold, SquadToLoad[] _playerArmy, TT_Difficulty _difficulty, GearID _startingGear, Guid _runUUID)
        {
            runUUID = _runUUID;
            seed = _seed;
            heroID = _hero;
            goldAmount = _startingGold;
            playerArmy = _playerArmy;
            activeMapLayer = -1;
            nodeGenerated = false;
            battleCompleted = false;
            nodePath = new List<int>();
            if(_startingGear == GearID.None) {
                Gear = new List<GearID>();
            } else {
                Gear = new List<GearID>() {
                    _startingGear
                };
            }
            SquadKillsStore = new List<SquadKillsStored>();
            HistoricalKillStore = new List<SquadKillsStored>();
            SquadLossesStore = new List<SquadLossesStored>();
            unitNameOverrides = new List<UnitNameOverrides>();
            RunStats = new RunStats();
            withdrawnSquads = new List<SquadToLoad>();
            difficultyLevel = _difficulty;
            selectedNodeIndex = -1;
            bookNumber = 1;
            eventOrdering = EventData.GetEventOrdering(new System.Random(seed));//order in which events will be generated
            playerSquadBattlePositions = new List<SquadBattlePosition>();
        }
        public int GetSelectedNodeIndex()
        {
            // UnityEngine.Debug.Log($"getting selected node index: {selectedNodeIndex}");
            return selectedNodeIndex;
        }
        public void SetSelectedNodeIndex(int _index)
        {
            // UnityEngine.Debug.Log($"setting selected node index: {_index}");
            selectedNodeIndex = _index;
        }
    }
    [Serializable] public class CustomBattleSaveData
    {
        public SquadToLoad[] playerCustomBattleArmy; 
        public List<SquadBattlePosition> playerCustomBattleSquadBattlePositions = new();
        public List<SavedSquadGroup> playerCustomBattleSquadGroups = new();
        public SquadToLoad[] enemyCustomBattleArmy;
        public List<SquadBattlePosition> enemyCustomBattleSquadBattlePositions = new();
    }
    [System.Serializable] public struct RunStats
    {
        public int chaptersCompleted;
        public int goldEarned;
        // public int goldDeposited; // legacy deposited-gold system, disabled in favor of Renown
        public int unitsPrestiged;
        public int unitsRecruited;
        public int gearAquired;
        public int enemiesSlain;
        public int shopPurchases;
        public int goldWagered;       // total gold bet at games this run (Gamba)
        public int maxArmyModels;     // peak total models across the army this run (QualityOverQuantity)
        public int ransomsOffered;    // battles this run that offered the ransom-captives reward (Merciful)
        public int ransomsChosen;     // battles this run where ransom captives was actually claimed (Merciful)
        public bool heldDuplicateUnit;// true once the army ever held two units of the same name (OneOfAKind)
        public bool consumableUsed;   // true once a consumable was used this run (BareEssentials)
        public bool pauseUsed;        // true once the pause button was used this run (UhPause)
    }
    public struct RenownAward
    {
        public int chaptersCompleted;
        public int chapterRenown;
        public int actsCompleted;
        public int actRenown;
        public TT_Difficulty difficulty;
        public float difficultyMultiplier;
        public int total;
    }
    [System.Serializable] public struct UnitNameOverrides
    {
        public string unitGUID;
        public string unitNameOverride;
        public UnitNameOverrides(string _unitGUID, string _unitNameOverride)
        {
            unitGUID = _unitGUID;
            unitNameOverride = _unitNameOverride;
        }
    }
    [System.Serializable] public class PlayerSaveData
    {
        public int campaignsStarted;
        public int campaignsCompleted;
        public List<int> tutorialStepCompleted = new ();
        public bool customBattle;

        //last campaign stats
        public int lastHeroID;
        public GearID lastStartingGearId;
        public TT_Difficulty lastDifficultyLevelSelected;
        public SquadToLoad[] lastArmySaveData;
        public int lastStartingGold;
        #region Collection
        public List<int> gearIdsCollected = new();
        public List<int> gearIdsAcknowledged= new ();
        public List<UnitName> troopsRecruited = new ();
        public List<UnitName> troopsAcknowledged = new ();
        public List<int> consumablesAquired = new ();
        public List<int> consumablesAcknowledged = new ();
        public List<int> metaprogressionNodesUnlocked = new ();
        public List<string> BattlefieldInfoSectionsViewed = new ();
        // Legacy deposited-gold system, disabled in favor of Renown. Fields kept (not removed)
        // so JsonUtility can still deserialize existing saves for MigrateLegacyDepositedGoldToRenown.
        public int goldToDeposit;
        public int depositedGold;
        public int renown;
        #endregion

        public int gameCompletions;
        public List<UnlockCondition> unlockConditionsCompleted = new (){
            UnlockCondition.None
        };
        public List<HeroDifficultiesCompleted> HeroDifficultiesCompleted = new ();
        public int MaxDifficultyOverall;
        public List<HeroLastDifficulty> HeroLastDifficulties = new ();
        public List<Race> unlockedTavernThemes = new ();
        public bool hasTavernThemeSelected = false;
        public Race activeTavernThemeRace;
        public bool isDevToolUser;
        public List<UnitNameKillsStored> UnitNameHistoricalKillStore = new();
    }
    [System.Serializable] public struct SquadKillsStored
    {
        public string SquadGUID;
        public int Kills;
    }
    [System.Serializable] public struct SquadLossesStored
    {
        public string SquadGUID;
        public int Losses;
    }
    [System.Serializable] public struct UnitNameKillsStored
    {
        public UnitName UnitName;
        public int Kills;
    }
    [System.Serializable] public struct HeroDifficultiesCompleted
    {
        public int HeroID;
        public List<int> DifficultiesCompleted;
    }
    [System.Serializable] public struct HeroLastDifficulty
    {
        public int HeroID;
        public TT_Difficulty LastDifficulty;
    }
    public static class SaveDataHandler
    {
        static readonly bool useLocal = false;

        // Set from the battle scene (GameSpeedManager) when the player pauses; consumed at battle end
        // in SaveSquadsPostBattle so it rides the same file-based bridge as archerUsedInBattle into
        // RunStats. The in-memory CampaignSaveManager is not available in the battle scene. ("Uh, pause...")
        public static bool PauseUsedThisBattle;

        // In-memory authoritative copy of playerSaveData.json. SaveDataHandler is the sole gateway
        // to that file, so every read returns this cached instance and every write refreshes it.
        // Populated lazily on first LoadPlayerSaveData(); invalidated by DeletePlayerSaveData().
        static PlayerSaveData _playerCache;

        public static bool CheckForGear(GearID _gearID)
        {
            return Load().Gear.Contains(_gearID);
        }

        public static GearIDsSerialized GetGearCollected()
        {
            List<GearID> gearIDs = Load().Gear;
            GearIDsSerialized gearIDsSerialized = new GearIDsSerialized();
            for(int i = 0; i < gearIDs.Count; i++) {
                switch(i) {
                    case 0:
                        gearIDsSerialized.gearID1 = gearIDs[i];
                        break;
                    case 1:
                        gearIDsSerialized.gearID2 = gearIDs[i];
                        break;
                    case 2:
                        gearIDsSerialized.gearID3 = gearIDs[i];
                        break;
                    case 3:
                        gearIDsSerialized.gearID4 = gearIDs[i];
                        break;
                    case 4:
                        gearIDsSerialized.gearID5 = gearIDs[i];
                        break;
                }
            }
            return gearIDsSerialized;
        }
        private static void SaveToJSON<T> (T toSave, string filename)
        {
            string content = JsonUtility.ToJson(toSave, true);  // 'true' for pretty-printing (optional, human-readable)
            string targetPath = GetPath(filename);
            string tempPath = targetPath + ".tmp";  // Temporary file in same directory

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.WriteAllText(tempPath, content);

            // Atomically replace/move
            if (File.Exists(targetPath))
            {
                try
                {
                    File.Replace(tempPath, targetPath, null);
                }
                catch (IOException)
                {
                    // File.Replace failed (file temporarily locked), fall back to direct overwrite
                    File.Copy(tempPath, targetPath, overwrite: true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, targetPath);
            }

            // Optional: Verify (for extra safety)
            if (!File.Exists(targetPath))
            {
                UnityEngine.Debug.LogError($"Atomic save failed for {filename}: Target file missing after replace.");
            }
        }
        public static T ReadListFromJSON<T> (string filename)
        {
            string path = GetPath(filename);
            string content = ReadFile(path);

            if (string.IsNullOrWhiteSpace(content))
            {
                // UnityEngine.Debug.LogWarning($"[ReadListFromJSON] Empty or invalid JSON at path: {path}");
                return default;
            }

            try
            {
                return JsonUtility.FromJson<T>(content);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ReadListFromJSON] Failed to parse JSON from file: {path}\n{e}");
                return default;
            }
        }
        private static string GetPath (string filename)
        {
            return useLocal ? Application.dataPath +"/Data/" +filename : Application.persistentDataPath + "/" + filename;
        }
        private static string ReadFile (string path)
        {
            if (File.Exists (path))
            {
                using StreamReader reader = new StreamReader(path);
                string content = reader.ReadToEnd();
                return content;
            }
            return "";
        }
        public static void SaveCampaign(CampaignSaveData toSave)
        {
            toSave.snapShot = false;
            SaveToJSON(toSave, "campaignSaveData.json");
        }
        public static void SaveCampaignSnapshot(CampaignSaveData toSave)
        {
            toSave.snapShot = true;
            SaveToJSON(toSave, "campaignSaveDataSnapshot.json");
        }
        public static void SaveCustomBattleSaveData(CustomBattleSaveData toSave)
        {
            SaveToJSON(toSave, "customBattleSaveData.json");
        }
        public static List<SquadKillsStored> AddToHistoricalKills(List<SquadKillsStored> _historicalKills, List<SquadKillsStored> _currentKills)
        {
            _historicalKills ??= new List<SquadKillsStored>();
            for(int i = 0; i < _currentKills.Count; i++) {
                bool found = false;
                for(int j = 0; j < _historicalKills.Count; j++) {
                    if(_currentKills[i].SquadGUID == _historicalKills[j].SquadGUID) {
                        _historicalKills[j] = new SquadKillsStored() {
                            SquadGUID = _currentKills[i].SquadGUID,
                            Kills = _historicalKills[j].Kills + _currentKills[i].Kills
                        };
                        found = true;
                        break;
                    }
                }
                if(!found) {
                    _historicalKills.Add(_currentKills[i]);
                }
            }
            return _historicalKills;
        }
        private static List<UnitNameKillsStored> AddToUnitNameHistoricalKills(List<UnitNameKillsStored> _historicalKills, List<UnitNameKillsStored> _currentKills)
        {
            _historicalKills ??= new List<UnitNameKillsStored>();
            for(int i = 0; i < _currentKills.Count; i++) {
                bool found = false;
                for(int j = 0; j < _historicalKills.Count; j++) {
                    if(_currentKills[i].UnitName == _historicalKills[j].UnitName) {
                        _historicalKills[j] = new UnitNameKillsStored() {
                            UnitName = _currentKills[i].UnitName,
                            Kills = _historicalKills[j].Kills + _currentKills[i].Kills
                        };
                        found = true;
                        break;
                    }
                }
                if(!found) {
                    _historicalKills.Add(_currentKills[i]);
                }
            }
            return _historicalKills;
        }
        /// <summary>
        /// Folds a battle's GUID-keyed kill counts into the player save's lifetime per-UnitName kill tracker.
        /// Only kills scored by squads found in _playerSquads are counted (enemy squad kills are ignored).
        /// </summary>
        public static void RecordUnitNameKills(SquadToLoad[] _playerSquads, List<SquadKillsStored> _squadIdKillCounter)
        {
            List<UnitNameKillsStored> currentKillsByUnitName = new();
            foreach (SquadKillsStored squadKill in _squadIdKillCounter)
            {
                if (squadKill.Kills <= 0) continue;

                bool foundSquad = false;
                UnitName unitName = default;
                for (int i = 0; i < _playerSquads.Length; i++)
                {
                    if (_playerSquads[i].UniqueID == squadKill.SquadGUID)
                    {
                        unitName = _playerSquads[i].UnitName;
                        foundSquad = true;
                        break;
                    }
                }
                if (!foundSquad) continue;

                int existingIndex = currentKillsByUnitName.FindIndex(x => x.UnitName == unitName);
                if (existingIndex >= 0)
                {
                    UnitNameKillsStored entry = currentKillsByUnitName[existingIndex];
                    entry.Kills += squadKill.Kills;
                    currentKillsByUnitName[existingIndex] = entry;
                }
                else
                {
                    currentKillsByUnitName.Add(new UnitNameKillsStored { UnitName = unitName, Kills = squadKill.Kills });
                }
            }
            if (currentKillsByUnitName.Count == 0) return;

            PlayerSaveData playerSaveData = LoadPlayerSaveData();
            playerSaveData.UnitNameHistoricalKillStore = AddToUnitNameHistoricalKills(playerSaveData.UnitNameHistoricalKillStore, currentKillsByUnitName);
            SavePlayerSaveData(playerSaveData);
        }
        /// <summary>
        /// Saves the squads after a manual battle has been completed.
        /// </summary>
        /// <param name="_playerSquads"></param>
        /// <param name="_enemySquads"></param>
        /// <param name="_playerWon"></param>
        /// <param name="_squadIdKillCounter"></param>
        /// <param name="_squadIdLossCounter"></param>
        public static void SaveSquadsPostBattle(SquadToLoad[] _playerSquads, SquadToLoad[] _enemySquads, bool _playerWon, List<SquadKillsStored> _squadIdKillCounter, List<SquadLossesStored> _squadIdLossCounter)
        {
            UnityEngine.Debug.Log($"SaveDataHandler SaveSquadsPostBattle: player won: {_playerWon}");
            CampaignSaveData saveData = Load();
            SquadToLoad GetPlayerSquad(string _uniqueID)
            {
                for (int i = 0; i < _playerSquads.Length; i++)
                {
                    if (_playerSquads[i].UniqueID == _uniqueID) return _playerSquads[i];
                }
                UnityEngine.Debug.LogError($"Could not find player squad with uniqueID {_uniqueID}");
                return new SquadToLoad();
            }

            for (int i = 0; i < 10; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;

                saveData.playerArmy[i] = GetPlayerSquad(saveData.playerArmy[i].UniqueID);
            }
            saveData.enemyArmy = _enemySquads;
            saveData.battleCompleted = true;
            saveData.playerWonBattle = _playerWon;
            saveData.SquadKillsStore = _squadIdKillCounter;

            saveData.HistoricalKillStore = AddToHistoricalKills(saveData.HistoricalKillStore, _squadIdKillCounter);
            RecordUnitNameKills(_playerSquads, _squadIdKillCounter);
            saveData.SquadLossesStore = _squadIdLossCounter;
            if(saveData.townData == null) {
                saveData.townData = new TownSaveData();
                UnityEngine.Debug.Log($"Created new TownSaveData in SaveSquadsPostBattle");
            }
            saveData.townData.townInteractionStatus = TownInteractionStatus.Sacked;
            // saveData.withdrawnSquads = _withdrawnSquads;

            int totalKills = 0;
            foreach (var squadKill in _squadIdKillCounter)
            {
                totalKills += squadKill.Kills;
            }
            UnityEngine.Debug.Log($"Total enemies slain in battle: {totalKills}");
            saveData.RunStats.enemiesSlain += totalKills;

            //achievement check - cav only
            bool cavOnly = true;
            for (int i = 0; i < 10; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;

                //get unit size for each unit
                UnitSize unitSize = TabletopTavernData.Instance.GetSquadStats(saveData.playerArmy[i].UnitName).unitSize;
                if (unitSize != UnitSize.Cavalry)
                {
                    cavOnly = false;
                    break;
                }
            }

            if (cavOnly && _playerWon)
            {
                SteamAchievements.Unlock(AchievementId.OnlyCavBattle);
            }

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

            //achievement tracking - archer used in battle
            for (int i = 0; i < 10; i++)
            {
                if (saveData.playerArmy[i].UnitIndex == -1) continue;

                if (TabletopTavernData.Instance.GetSquadStats(saveData.playerArmy[i].UnitName).unitType == UnitType.Ranged)
                {
                    saveData.archerUsedInBattle = true;
                    break;
                }
            }

            // "Uh, pause..." - carry the battle-scene pause flag into the run save, then clear it.
            if (PauseUsedThisBattle)
            {
                saveData.RunStats.pauseUsed = true;
                PauseUsedThisBattle = false;
            }

            SaveCampaign(saveData);

            //update the last snapshot to overwrite the snapshot of the pre battle state since the battle is now completed
            SaveCampaignSnapshot(saveData);
        }
        public static void SavePlayerSaveData(PlayerSaveData toSave)
        {
            _playerCache = toSave;
            SaveToJSON(toSave, "playerSaveData.json");
        }
        public static bool CampaignSaveExists()
        {
            if (!File.Exists(GetPath("campaignSaveData.json"))) return false;
            CampaignSaveData save = ReadListFromJSON<CampaignSaveData>("campaignSaveData.json");
            return save != null && !save.blank;
        }
        public static void DeleteCampaignSave()
        {
            var blankSave = new CampaignSaveData(0, 0, 0, null, TT_Difficulty.Peasant, GearID.None, Guid.Empty) { blank = true };
            SaveToJSON(blankSave, "campaignSaveData.json");
            blankSave.snapShot = true;
            SaveToJSON(blankSave, "campaignSaveDataSnapshot.json");
        }
        public static bool PlayerSaveDataExists()
        {
            return File.Exists(GetPath("playerSaveData.json"));
        }
        public static void DeletePlayerSaveData()
        {
            string path = GetPath("playerSaveData.json");
            if (File.Exists(path))
                File.Delete(path);
            _playerCache = null;
        }
        public static CampaignSaveData Load()
        {
            CampaignSaveData loadedSaveData = ReadListFromJSON<CampaignSaveData>("campaignSaveData.json");
            if (loadedSaveData == null || loadedSaveData.blank)
            {
                // UnityEngine.Debug.LogError($"No campaign save data found, creating new default save data.");
                loadedSaveData = new CampaignSaveData(UnityEngine.Random.Range(0, 100000), HeroData.EdricValeward.HeroID, 0, new SquadToLoad[13], TT_Difficulty.Peasant, GearID.None, Guid.NewGuid());
            }

            return loadedSaveData;
        }
        public static CustomBattleSaveData LoadCustomBattleSaveData()
        {
            CustomBattleSaveData loadedSaveData = ReadListFromJSON<CustomBattleSaveData>("customBattleSaveData.json");
            if (loadedSaveData == null)
            {
                // UnityEngine.Debug.LogError($"No custom battle save data found, creating new default save data.");
                loadedSaveData ??= new CustomBattleSaveData();
            }

            return loadedSaveData;
        }
        public static CampaignSaveData LoadSnapshot()
        {
            // UnityEngine.Debug.Log($"Loading campaign snapshot save data.");
            CampaignSaveData loadedSaveData = ReadListFromJSON<CampaignSaveData>("campaignSaveDataSnapshot.json");
            if (loadedSaveData == null || loadedSaveData.blank) {
                UnityEngine.Debug.Log($"Campaign snapshot save data is blank or does not exist, creating new default save data.");
                loadedSaveData = new CampaignSaveData( UnityEngine.Random.Range(0, 100000), HeroData.EdricValeward.HeroID, 0, new SquadToLoad[13], TT_Difficulty.Peasant, GearID.None, Guid.NewGuid());
            }

            return loadedSaveData;
        }
        public static CampaignSaveData LoadSnapshotNullAllowed()
        {
            CampaignSaveData loadedSaveData = ReadListFromJSON<CampaignSaveData>("campaignSaveDataSnapshot.json");
            if (loadedSaveData != null && loadedSaveData.blank) return null;
            return loadedSaveData;
        }
        public static PlayerSaveData LoadPlayerSaveData()
        {
            if (_playerCache != null) return _playerCache;

            PlayerSaveData loadedSaveData = ReadListFromJSON<PlayerSaveData>("playerSaveData.json");
            loadedSaveData ??= new PlayerSaveData();
            _playerCache = loadedSaveData;
            MigrateLegacyDepositedGoldToRenown(loadedSaveData);

            return _playerCache;
        }

        // One-time migration: folds any pre-existing goldToDeposit/depositedGold balance into
        // Renown. Idempotent - becomes a no-op once both legacy fields are zeroed.
        private static void MigrateLegacyDepositedGoldToRenown(PlayerSaveData saveData)
        {
            if (saveData.depositedGold <= 0 && saveData.goldToDeposit <= 0) return;

            saveData.renown += saveData.depositedGold + saveData.goldToDeposit;
            saveData.depositedGold = 0;
            saveData.goldToDeposit = 0;
            SavePlayerSaveData(saveData);
        }
        public static List<int> GetHeroDifficultiesCompleted(int _heroID)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            for(int i = 0; i < saveData.HeroDifficultiesCompleted.Count; i++) {
                if(saveData.HeroDifficultiesCompleted[i].HeroID == _heroID) {
                    return saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted;
                }
            }
            return new List<int>();
        }
        public static TT_Difficulty GetHeroLastDifficulty(int heroID)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            int maxAvailable = Math.Min(saveData.MaxDifficultyOverall + 1, (int)TT_Difficulty.Godking);
            if (maxAvailable < 1) maxAvailable = 1;

            for (int i = 0; i < saveData.HeroLastDifficulties.Count; i++)
            {
                if (saveData.HeroLastDifficulties[i].HeroID == heroID)
                {
                    int lastWin = (int)saveData.HeroLastDifficulties[i].LastDifficulty;
                    if (lastWin < saveData.MaxDifficultyOverall)
                        return (TT_Difficulty)lastWin;
                    break;
                }
            }

            return (TT_Difficulty)maxAvailable;
        }
        public static void SaveHeroLastDifficulty(int heroID, TT_Difficulty difficulty)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            for (int i = 0; i < saveData.HeroLastDifficulties.Count; i++)
            {
                if (saveData.HeroLastDifficulties[i].HeroID == heroID)
                {
                    saveData.HeroLastDifficulties[i] = new HeroLastDifficulty { HeroID = heroID, LastDifficulty = difficulty };
                    SavePlayerSaveData(saveData);
                    return;
                }
            }
            saveData.HeroLastDifficulties.Add(new HeroLastDifficulty { HeroID = heroID, LastDifficulty = difficulty });
            SavePlayerSaveData(saveData);
        }
        public static bool IsTavernThemeUnlocked(Race _race)
        {
            if (_race == Race.Special) return true;
            PlayerSaveData saveData = LoadPlayerSaveData();
            return saveData.unlockedTavernThemes.Contains(_race);
        }
        public static void UnlockTavernTheme(Race _race)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if (!saveData.unlockedTavernThemes.Contains(_race))
            {
                saveData.unlockedTavernThemes.Add(_race);
                SavePlayerSaveData(saveData);
            }
        }
        public static void SetActiveTavernTheme(Race _race)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            saveData.hasTavernThemeSelected = true;
            saveData.activeTavernThemeRace = _race;
            SavePlayerSaveData(saveData);
        }
        public static void ClearActiveTavernTheme()
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            saveData.hasTavernThemeSelected = false;
            SavePlayerSaveData(saveData);
        }
        public static bool TryGetActiveTavernTheme(out Race _race)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            _race = saveData.activeTavernThemeRace;
            return saveData.hasTavernThemeSelected;
        }
        // Returns true if any hero of the given race has completed Godking difficulty
        public static bool HasCompletedGodkingWithRace(Race _race)
        {
            List<Hero> heroes = HeroData.GetHeroesByRace(_race);
            foreach (Hero hero in heroes)
            {
                List<int> difficulties = GetHeroDifficultiesCompleted(hero.HeroID);
                if (difficulties.Contains((int)TT_Difficulty.Godking))
                    return true;
            }
            return false;
        }
        // Grants the tavern theme for every race whose heroes have a recorded Godking completion.
        // Must be called from anywhere the unlock state is read or earned, not just once at boot:
        // Tavern.unity is a persistent scene, so TavernThemeManager.Start() only runs on the first
        // load and would never see a completion earned later in the same session.
        // Walks the completion list directly (one save read) and only writes when something changed.
        public static void RefreshTavernThemeUnlocks()
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool changed = false;

            for (int i = 0; i < saveData.HeroDifficultiesCompleted.Count; i++)
            {
                if (!saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Contains((int)TT_Difficulty.Godking))
                    continue;

                int heroID = saveData.HeroDifficultiesCompleted[i].HeroID;
                // GetHeroByID falls back to the default hero for unknown IDs, which would wrongly
                // unlock that hero's race if a mod removed the hero this entry refers to.
                if (HeroData.GetHeroByID(heroID).HeroID != heroID) continue;

                Race race = HeroData.GetRaceFromHero(heroID);
                if (race == Race.Special || saveData.unlockedTavernThemes.Contains(race)) continue;

                saveData.unlockedTavernThemes.Add(race);
                changed = true;
            }

            if (changed)
                SavePlayerSaveData(saveData);
        }
        public static bool IsCustomBattle()
        {
            return LoadPlayerSaveData().customBattle;
        }
        public static bool IsDevToolUser()
        {
            return LoadPlayerSaveData().isDevToolUser;
        }
        public static void SaveLastCampaignStats(int _heroID, TT_Difficulty _difficultyLevelSelected, GearID _gearID, SquadToLoad[] _armySaveData, int _startingGold)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();

            saveData.lastHeroID = _heroID;
            saveData.lastDifficultyLevelSelected = _difficultyLevelSelected;
            saveData.lastStartingGearId = _gearID;
            saveData.lastArmySaveData = _armySaveData;
            saveData.lastStartingGold = _startingGold;
            SavePlayerSaveData(saveData);
        }
        public static int GetActiveHeroID()
        {
            return Load().heroID;
        }
        public static Race GetEnemyRace()
        {
            CampaignSaveData save = Load();
            return save.enemyArmy != null && save.enemyArmy.Length > 0 ? TabletopTavernData.Instance.GetRaceFromUnitName(save.enemyArmy[0].UnitName) : Race.Special;
        }

        public static void OpenSaveFolder()
        {
            string path = Application.persistentDataPath;
            if (Directory.Exists(path)) {
                // string formattedPath = $"\"{path}\"";
                string formattedPath = path.Replace("/", "\\");
                formattedPath = $"\"{formattedPath}\"";
                // UnityEngine.Debug.Log($"Opening folder: {formattedPath}");
                Process.Start("explorer.exe", formattedPath);
            } else {
                UnityEngine.Debug.LogWarning($"Directory not found: {path}");
            }
        }
        public static void CreateCampaign(Hero hero, ArmySaveData armySaveData, TT_Difficulty _difficultyLevelSelected, GearID _startingGear, Guid _runUUID, int startingGold)
        {
            SquadToLoad[] squadsToLoad = new SquadToLoad[armySaveData.SquadsInArmy.Length];
            for(int i = 0; i < armySaveData.SquadsInArmy.Length; i++) {
                squadsToLoad[i] = new SquadToLoad(armySaveData.SquadsInArmy[i], 0, i);
            }
            CreateCampaign(hero, squadsToLoad, _difficultyLevelSelected, _startingGear, _runUUID, startingGold);
        }
        public static void CreateCampaign(Hero hero, SquadToLoad[] squadsToLoad, TT_Difficulty _difficultyLevelSelected, GearID _startingGear, Guid _runUUID, int startingGold)
        {
            // UnityEngine.Debug.Log($"Creating campaign with hero: {hero.HeroID} and difficulty: {_difficultyLevelSelected}");
            SquadToLoad[] playerArmy = new SquadToLoad[13];
            for(int i = 0; i < playerArmy.Length; i++) {
                playerArmy[i].UnitIndex = -1;
            }
            float startingHealth = 1f;

            //DifficultyMod 18
            if(_difficultyLevelSelected >= TT_Difficulty.Overlord) startingHealth = 0.75f;

            List<UnitName> recruitedUnitNames = new(squadsToLoad.Length);
            for(int i = 0; i < squadsToLoad.Length; i++) {
                playerArmy[i] = new SquadToLoad(
                    squadsToLoad[i].UnitName,
                    _prestige: 0,
                    _unitIndex: i,
                    _modifiedHealthValueByAmount : startingHealth
                );

                //int get base unit count
                int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(playerArmy[i].UnitName);
                int maxUnitCount = TabletopTavernData.Instance.GetHitPointsPerUnit(playerArmy[i].UnitName);
                playerArmy[i].SquadCurrentHealth = (int)((baseUnitCount * maxUnitCount) * startingHealth);
                playerArmy[i].maxUnitCount = baseUnitCount;
                playerArmy[i].HitPointsPerUnit = maxUnitCount;
                recruitedUnitNames.Add(playerArmy[i].UnitName);
            }
            AquiredTroops(recruitedUnitNames);

            int seed = UnityEngine.Random.Range(0, 1000000);
            CampaignSaveData campaignSaveData = new (seed, hero.HeroID, startingGold, playerArmy, _difficultyLevelSelected, _startingGear, _runUUID);
            
            SaveCampaign(campaignSaveData);
            SaveCampaignSnapshot(campaignSaveData);
            SaveLastCampaignStats(hero.HeroID, _difficultyLevelSelected, _startingGear, squadsToLoad, startingGold);
        }
        public static List<int> GetGearIDsCollected()
        {
            return LoadPlayerSaveData().gearIdsCollected;
        }
        public static void AquiredGear(GearID _gearID)
        {
            if (_gearID == GearID.None) return;
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool changed = saveData.gearIdsCollected.Remove(0); // strip legacy placeholder 0 if present
            if (!saveData.gearIdsCollected.Contains((int)_gearID))
            {
                saveData.gearIdsCollected.Add((int)_gearID);
                changed = true;
            }
            if (changed) SavePlayerSaveData(saveData);
        }
        public static List<int> GetGearIDsAcknowledged()
        {
            return LoadPlayerSaveData().gearIdsAcknowledged;
        }
        public static void AcknowledgedGear(GearID _gearID)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool changed = saveData.gearIdsAcknowledged.Remove(0); // strip legacy placeholder 0 if present
            if (!saveData.gearIdsAcknowledged.Contains((int)_gearID))
            {
                saveData.gearIdsAcknowledged.Add((int)_gearID);
                changed = true;
            }
            if (changed) SavePlayerSaveData(saveData);
        }
        public static List<UnitName> GetTroopsIDsCollected()
        {
            List<UnitName> troopsRecruitied = LoadPlayerSaveData().troopsRecruited;

            return troopsRecruitied;
        }
        public static void AquiredTroop(UnitName _unitName)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if(saveData.troopsRecruited.Contains(_unitName)) return;
            saveData.troopsRecruited.Add(_unitName);
            SavePlayerSaveData(saveData);
        }
        // Batch variant: records several recruited troops with a single save, so callers adding a
        // whole army (e.g. CreateCampaign) don't pay one whole-file write per unit.
        public static void AquiredTroops(IEnumerable<UnitName> _unitNames)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool changed = false;
            foreach (UnitName unitName in _unitNames)
            {
                if (saveData.troopsRecruited.Contains(unitName)) continue;
                saveData.troopsRecruited.Add(unitName);
                changed = true;
            }
            if (changed) SavePlayerSaveData(saveData);
        }
        public static List<UnitName> GetTroopsIDsAcknowledged()
        {
            return LoadPlayerSaveData().troopsAcknowledged;
        }
        public static void AcknowledgedTroop(UnitName _unitName)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if(saveData.troopsAcknowledged.Contains(_unitName)) return;
            saveData.troopsAcknowledged.Add(_unitName);
            SavePlayerSaveData(saveData);
        }
        public static List<int> GetPotionsIDsCollected()
        {
            return LoadPlayerSaveData().consumablesAquired;
        }
        public static void AquiredPotionForCollection(ConsumableEnum _consumable)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if(saveData.consumablesAquired.Contains((int)_consumable)) return;
            saveData.consumablesAquired.Add((int)_consumable);
            SavePlayerSaveData(saveData);
        }
        public static List<int> GetPotionsIDsAcknowledged()
        {
            return LoadPlayerSaveData().consumablesAcknowledged;
        }
        public static void AcknowledgedPotion(ConsumableEnum _consumable)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if(saveData.consumablesAcknowledged.Contains((int)_consumable)) return;
            saveData.consumablesAcknowledged.Add((int)_consumable);
            SavePlayerSaveData(saveData);
        }
        // Placeholder tuning values - adjust to taste.
        private const int RENOWN_PER_CHAPTER = 1;
        private const int RENOWN_PER_ACT_COMPLETED = 50;
        private const float RENOWN_DIFFICULTY_MULTIPLIER_PER_LEVEL = 0.25f; // e.g. difficulty 3 (Knight) => x1.5

        private static RenownAward ComputeRenownReward(RunStats runStats, int bookNumber, TT_Difficulty difficulty)
        {
            int chapterRenown = runStats.chaptersCompleted * RENOWN_PER_CHAPTER;
            int actRenown = bookNumber * RENOWN_PER_ACT_COMPLETED;
            // TT_Difficulty starts at 1 (Peasant), so offset by 1 to give the lowest difficulty x1.
            float difficultyMultiplier = 1f + ((int)difficulty - 1) * RENOWN_DIFFICULTY_MULTIPLIER_PER_LEVEL;
            int total = Mathf.RoundToInt((chapterRenown + actRenown) * difficultyMultiplier);

            return new RenownAward
            {
                chaptersCompleted = runStats.chaptersCompleted,
                chapterRenown = chapterRenown,
                actsCompleted = bookNumber,
                actRenown = actRenown,
                difficulty = difficulty,
                difficultyMultiplier = difficultyMultiplier,
                total = total
            };
        }

        public static RenownAward RecordGameOver(bool _playerWon)
        {
            UnityEngine.Debug.Log($"Recording game over, player won: {_playerWon}");

            CampaignSaveData campaignSaveData = CampaignManager.Instance.CampaignSaveManager.SaveData;
            PlayerSaveData saveData = LoadPlayerSaveData();

            // bookNumber is the act currently in progress. On a win it was actually finished, but on a
            // loss it wasn't - don't award renown for the act the player died in.
            int actsCompleted = _playerWon ? campaignSaveData.bookNumber : Mathf.Max(campaignSaveData.bookNumber - 1, 0);
            RenownAward renownAward = ComputeRenownReward(campaignSaveData.RunStats, actsCompleted, campaignSaveData.difficultyLevel);
            saveData.renown += renownAward.total;

            if (saveData.renown >= 100)
                SteamAchievements.Unlock(AchievementId.ASnackForLater);

            if (_playerWon)
            {
                saveData.gameCompletions++;

                int currentHeroID = campaignSaveData.heroID;
                int newDifficulty = (int)campaignSaveData.difficultyLevel;

                //update max difficulty unlocked if needed
                if(newDifficulty > saveData.MaxDifficultyOverall) {
                    saveData.MaxDifficultyOverall = newDifficulty;
                }

                bool found = false;
                for (int i = 0; i < saveData.HeroDifficultiesCompleted.Count; i++)
                {
                    //hero has an entry
                    if (saveData.HeroDifficultiesCompleted[i].HeroID == currentHeroID)
                    {
                        // Only update if the new difficulty was not already recorded
                        if (!saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Contains(newDifficulty))
                        {
                            saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Add(newDifficulty);
                        }
                        found = true;
                        break;
                    }
                }

                // Only add if no existing entry was found
                if (!found)
                {
                    saveData.HeroDifficultiesCompleted.Add(new HeroDifficultiesCompleted()
                    {
                        HeroID = currentHeroID,
                        DifficultiesCompleted = new List<int>() { newDifficulty }
                    });
                }

                bool heroLastDiffFound = false;
                for (int i = 0; i < saveData.HeroLastDifficulties.Count; i++)
                {
                    if (saveData.HeroLastDifficulties[i].HeroID == currentHeroID)
                    {
                        saveData.HeroLastDifficulties[i] = new HeroLastDifficulty { HeroID = currentHeroID, LastDifficulty = (TT_Difficulty)newDifficulty };
                        heroLastDiffFound = true;
                        break;
                    }
                }
                if (!heroLastDiffFound)
                    saveData.HeroLastDifficulties.Add(new HeroLastDifficulty { HeroID = currentHeroID, LastDifficulty = (TT_Difficulty)newDifficulty });

                //achievement check - roster complete (beat the game with every hero, any difficulty).
                //Hero counts mirror the hardcoded totals used by the max-difficulty-all-heroes check; bump if the roster grows.
#if DEMO
                int totalHeroes = 4;
#else
                int totalHeroes = 16;
#endif
                int heroesBeaten = 0;
                for (int i = 0; i < saveData.HeroDifficultiesCompleted.Count; i++)
                {
                    if (saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Count > 0) heroesBeaten++;
                }
                if (heroesBeaten >= totalHeroes) SteamAchievements.Unlock(AchievementId.RosterComplete);
            }

            // --- Legacy deposited-gold sweep, disabled - kept in case this system is restored ---
            // saveData.goldToDeposit += campaignSaveData.goldAmount;
            // SavePlayerSaveData(saveData);
            // DepositGold();

            SavePlayerSaveData(saveData);
            return renownAward;
        }
        public static bool IsUnlockConditionUnlocked(UnlockCondition _unlockCondition, int heroID)
        {
            if (_unlockCondition == UnlockCondition.NotAvailableInDemo)
            {
                return false;
            }

            if (_unlockCondition == UnlockCondition.HeroCompletion && heroID > 0)
            {
                return GetHeroDifficultiesCompleted(heroID - 1).Count > 0;
            }

            return LoadPlayerSaveData().unlockConditionsCompleted.Contains(_unlockCondition);
        }
        public static async Task<GameObject> GetPlayerHeroPrefabAsync()
        {
            int heroID = CampaignSaveExists() ? Load().heroID : HeroData.EdricValeward.HeroID;
            return await TabletopTavernData.Instance.LoadHeroPrefabAsync(heroID);
        }
        // Legacy deposited-gold system, disabled in favor of Renown. Kept commented out in case
        // this system is restored.
        // public static void DepositGold()
        // {
        //     PlayerSaveData playerSaveData = LoadPlayerSaveData();
        //     // UnityEngine.Debug.Log($"Depositing gold: {playerSaveData.goldToDeposit}");
        //     playerSaveData.depositedGold += playerSaveData.goldToDeposit;
        //     //cap at 500
        // #if DEMO
        //             if(playerSaveData.depositedGold > TabletopTavernConstants.MAX_DEMO_DEPOSITED_GOLD) {
        //                 playerSaveData.depositedGold = TabletopTavernConstants.MAX_DEMO_DEPOSITED_GOLD;
        //             }
        // #endif
        //     playerSaveData.goldToDeposit = 0;
        //
        //     if(playerSaveData.depositedGold >= 100) {
        //         SteamStatic.UnlockAchievement(SteamData.ACHIEVEMENT_A_SNACK_FOR_LATER);
        //     }
        //     SavePlayerSaveData(playerSaveData);
        // }
        // public static int GetDepositedGold()
        // {
        //     return LoadPlayerSaveData().depositedGold;
        // }
        public static int GetRenown()
        {
            return LoadPlayerSaveData().renown;
        }
        public static int GetUnitNameHistoricalKillCount(UnitName _unitName)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            return saveData.UnitNameHistoricalKillStore.Find(x => x.UnitName == _unitName).Kills;
        }
        public static void UnlockMetaprogressionNode(MetaprogressionModel _node)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            if(saveData.metaprogressionNodesUnlocked.Contains(_node.NodeId)) return;
            saveData.metaprogressionNodesUnlocked.Add(_node.NodeId);
            SavePlayerSaveData(saveData);
        }
        public static List<int> GetUnlockedMetaprogressionNodes()
        {
            return LoadPlayerSaveData().metaprogressionNodesUnlocked;
        }
        public static void ResetMetaprogression()
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            saveData.metaprogressionNodesUnlocked = new List<int>();
            SavePlayerSaveData(saveData);
        }
        public static bool IsMetaprogressionNodeUnlocked(MetaprogressionModel _node)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool conditionUnlocked = saveData.metaprogressionNodesUnlocked.Contains(_node.NodeId);
            // UnityEngine.Debug.Log($"IsMetaprogressionNodeUnlocked {_node.NodeName}: {conditionUnlocked}");
            return conditionUnlocked;
        }
        #if UNITY_EDITOR
        //record hero completions for testing
        public static void RecordHeroCompletionForTesting(int _heroID, TT_Difficulty _difficulty)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            int newDifficulty = (int)_difficulty;

            //update max difficulty unlocked if needed
            if(newDifficulty > saveData.MaxDifficultyOverall) {
                saveData.MaxDifficultyOverall = newDifficulty;
            }

            bool found = false;
            for (int i = 0; i < saveData.HeroDifficultiesCompleted.Count; i++)
            {
                //hero has an entry
                if (saveData.HeroDifficultiesCompleted[i].HeroID == _heroID)
                {
                    // Only update if the new difficulty was not already recorded
                    if (!saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Contains(newDifficulty))
                    {
                        saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted.Add(newDifficulty);
                    }
                    found = true;
                    break;
                }
            }

            // Only add if no existing entry was found
            if (!found)
            {
                saveData.HeroDifficultiesCompleted.Add(new HeroDifficultiesCompleted()
                {
                    HeroID = _heroID,
                    DifficultiesCompleted = new List<int>() { newDifficulty }
                });
            }
            SavePlayerSaveData(saveData);
        }
        #endif
    }
}
