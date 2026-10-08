using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using TJ;
using TJ.Spells;
using TJ.Achievements;
using System;
using Memori.Steamworks;
using Memori.Metaprogression;
using TabletopTavern.Analytics;

namespace Memori.SaveData
{
    // A save file that records the format version it was written in. SaveToJSON stamps it on every write.
    public interface IVersionedSave
    {
        int SaveVersion { get; set; }
    }

    [Serializable] public class CampaignSaveData : IVersionedSave
    {
        public int saveVersion;
        int IVersionedSave.SaveVersion { get => saveVersion; set => saveVersion = value; }
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
        // Nodes the player marked as their planned route this act, one per layer. Null in older saves: none marked.
        public List<int> plannedNodes = new();
        public bool battleCompleted;
        public bool playerWonBattle;
        // A battle was won at the current node; CompleteChapter clears it. False in older saves.
        public bool wonBattleThisTurn;
        public List<SquadKillsStored> SquadKillsStore;
        public List<SquadKillsStored> HistoricalKillStore;
        public List<SquadLossesStored> SquadLossesStore;
        // Last battle only. Null in older saves: read it as no damage recorded.
        public List<SquadDamageStored> SquadDamageStore;
        // Spoils taken after the last won battle, so a reloaded result cannot pay twice. Null in older saves: none taken.
        public List<string> spoilsTaken;
        public List<UnitNameOverrides> unitNameOverrides;
        public RunStats RunStats;
        public BattleFieldPreset battleFieldPreset;
        public int turnsSincePotato;
        public int Rolls;
        public List<SquadToLoad> withdrawnSquads;
        // Legacy shuffled event order: EventPanel turns what an old save still holds into seenEvents once, then empties it.
        public List<int> eventOrdering;
        // TableKeys of events drawn since the pool last emptied.
        public List<string> seenEvents = new();
        // One "TableKey/choice/outcome" per resolved event; story-chain events require entries from it.
        public List<string> eventHistory = new();
        public EventBattleEffects eventBattleEffects = new();
        public int BattlesFought;
        public TT_Difficulty difficultyLevel;
        public bool snapShot;
        public bool blank;
        // Armed by drinking a Fateshine Elixir; guarantees the best result on the next dice roll of any
        // type (event, gamble, or battle initiative). Persisted here so it survives main-menu exit and
        // battle entry - see ConsumableManager / EventPanel / GamesPanel / BattleDiceRollPanel.
        public bool fateshineElixirArmed;
        // Mana Draughts drunk since the last fought battle. GetSpellManaPool adds SPELL_MANA_POOL_DRAUGHT
        // per draught; SaveSquadsPostBattle clears it. Auto-resolve has no mana pool and leaves it alone.
        public int manaDraughtsArmed;
        // The Prestige III traits dealt to this squad, kept so a reload shows the same three and only a Fateshine rerolls them.
        public string prestigeOfferSquadId;
        public List<UnitAttribute> prestigeOffer = new();
        public int signatureUnitPacksPurchased;
        public int townsSacked;
        public bool archerUsedInBattle;
        // The four spells chosen at run setup, in slot order. Slot 0 is always the hero's signature
        // spell. Only the enum values are stored; SpellRegistry resolves them back to assets, and
        // SpellLoadout.Sanitize re-validates on read so a save that predates a spell change or a mod
        // removing a spell cannot produce an illegal loadout. See SpellLoadout.
        public Spell[] selectedSpells = Array.Empty<Spell>();
        // Set once when the run is created. Analytics joins every event of a run on it.
        public string runId;
        // Recorded with the selected node so the battle scene can tell a Horde from a Skirmish or a garrison.
        public TJ.Map.NodeType selectedNodeType;
        public NodeVisit nodeVisit;
        // A roll the player has seen at the current node; Continue reopens the node on it instead of letting the pick be redone.
        public NodeResume nodeResume;
        /// <summary>Seconds of real play on this run (Map and campaign battles). See RunClock.</summary>
        public double playTimeSeconds;
        // Set when the act 3 win is recorded while the run marches on into endless acts. From then on the
        // run ends as a victory whatever happens, and the win-only bookkeeping never runs a second time.
        public bool victoryBanked;
        // Whether banking the victory was this hero's first completion, so the end screen can still show
        // the hero unlock after the completion is already on the player save.
        public bool victoryWasFirstHeroCompletion;
        // Hero leading the saved enemy army with his bonus rules (see EnemyWarlord), or 0 for none.
        public int enemyWarlordHeroID;
        // Ordeals taken on the March, in the order taken. One is due per warlord beaten. See OrdealRegistry.
        public List<OrdealId> ordeals = new();
        // Gear broken by Rusted Arms. It stays in Gear, keeping its slot, but no longer works.
        public List<GearID> brokenGear = new();
        // Long Night's weather for every battle; ClearSkies means the card is not held.
        public Weather ordealWeather;
        // Sealed Page's slot; 0 means none, since slot 0 is the signature spell and is never sealed.
        public int sealedSpellSlot;

        // Battles won since March On. The March's score, and what its enemy armies scale on.
        public int marchBattlesWon;
        // Ordeal picks made on the March; one is due for every warlord beaten.
        public int marchOrdealPicks;
        // The Twists on the node being fought: Ordeals that last this one battle. CompleteChapter clears them.
        public List<OrdealId> activeTwists = new();
        // Twists bought off this run; each one raises the next price.
        public int twistsStruck;
        // Twists bought off the node being fought, so a reload does not bring them back. CompleteChapter clears them.
        public List<OrdealId> twistsStruckHere = new();

        // Run History detail. Kept here, not on the player save, so a quit and Continue rolls it back with the snapshot.
        // 0 in saves from before it was kept.
        public int startingGold;
        // Units each player squad lost over the run.
        public List<SquadLossesStored> squadLossTotals = new();
        // Squads that died this run, as they stood when they fell.
        public List<RunSquad> fallenSquads = new();
        // The army as each act ended, one entry per act.
        public List<RunAct> actArmies = new();
        // One entry per resolved battle.
        public List<RunBattle> battleLog = new();

        /// <summary>Past the last story act: no healing, no recruits, only battles.</summary>
        public bool InMarch => MarchRules.InMarch(bookNumber);
        public bool HasOrdeal(OrdealId id)
        {
            if (ordeals != null && ordeals.Contains(id)) return true;
            if (activeTwists != null && activeTwists.Contains(id)) return true;
            return InMarch && OrdealRegistry.IsMarchLaw(id);
        }
        /// <summary>Everything in force right now: the March's laws, the cards taken, then this battle's Twists.</summary>
        public IEnumerable<OrdealId> ActiveOrdeals
        {
            get
            {
                if (InMarch)
                    foreach (OrdealId law in OrdealRegistry.MarchLaws) yield return law;
                if (ordeals != null)
                    foreach (OrdealId id in ordeals) yield return id;
                if (activeTwists != null)
                    foreach (OrdealId id in activeTwists)
                        if (ordeals == null || !ordeals.Contains(id)) yield return id;
            }
        }
        public bool IsGearBroken(GearID gear) => brokenGear != null && brokenGear.Contains(gear);
        // Owned but switched off: broken by Rusted Arms, or cancelled by a held Ordeal.
        public bool IsGearInactive(GearID gear) => IsGearBroken(gear) || OrdealRegistry.CounteringOrdeal(ActiveOrdeals, gear) != OrdealId.None;
        public bool IsConsumableBlocked(ConsumableEnum consumable) => OrdealRegistry.CounteringOrdeal(ActiveOrdeals, consumable) != OrdealId.None;
        public bool IsFactionPassiveBlocked(Race race) => OrdealRegistry.CounteringOrdeal(ActiveOrdeals, race) != OrdealId.None;
        public bool HasWorkingGear(GearID gear) => Gear != null && Gear.Contains(gear) && !IsGearInactive(gear);
        public bool OrdealPickDue => InMarch && MarchRules.WarlordsBeaten(marchBattlesWon) > marchOrdealPicks;
        public ulong OrdealBits
        {
            get
            {
                ulong bits = 0;
                foreach (OrdealId id in ActiveOrdeals) bits |= OrdealMask.Bit(id);
                return bits;
            }
        }

        /// <summary>runId, or a stable stand-in built from fields that never change mid-run for a run saved before runId existed.</summary>
        public string RunId => string.IsNullOrEmpty(runId) ? $"legacy-{seed}-{heroID}-{(int)difficultyLevel}" : runId;

        // _selectedSpells is optional: the blank/recovery saves constructed elsewhere in this file
        // pass nothing and get the hero's default loadout, which Sanitize produces from null.
        public CampaignSaveData(int _seed, int _hero, int _startingGold, SquadToLoad[] _playerArmy, TT_Difficulty _difficulty, GearID _startingGear, Guid _runUUID, Spell[] _selectedSpells = null)
        {
            runId = _runUUID == Guid.Empty ? string.Empty : _runUUID.ToString();
            seed = _seed;
            heroID = _hero;
            selectedSpells = SpellLoadout.Sanitize(_selectedSpells, _hero);
            goldAmount = _startingGold;
            startingGold = _startingGold;
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
            SquadDamageStore = new List<SquadDamageStored>();
            unitNameOverrides = new List<UnitNameOverrides>();
            RunStats = new RunStats();
            withdrawnSquads = new List<SquadToLoad>();
            difficultyLevel = _difficulty;
            selectedNodeIndex = -1;
            bookNumber = 1;
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
    [Serializable] public class CustomBattleSaveData : IVersionedSave
    {
        public int saveVersion;
        int IVersionedSave.SaveVersion { get => saveVersion; set => saveVersion = value; }
        public SquadToLoad[] playerCustomBattleArmy; 
        public List<SquadBattlePosition> playerCustomBattleSquadBattlePositions = new();
        public List<SavedSquadGroup> playerCustomBattleSquadGroups = new();
        // The four hotbar spells as equipped when the army was saved. Empty on saves that predate the
        // field, in which case SpellManager keeps its inspector defaults.
        public Spell[] playerCustomBattleSpells = Array.Empty<Spell>();
        public SquadToLoad[] enemyCustomBattleArmy;
        public List<SquadBattlePosition> enemyCustomBattleSquadBattlePositions = new();
        // TownSize ordinal of the garrison walls, -1 for none; the initializer keeps saves without the field at none.
        public int customBattleGarrison = -1;
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
        public bool gainedUnitOutsideRaiseDead; // true once a unit was gained by any means but Raise Dead (DeadShallServe)
        public List<SpellCastStored> spellsCast; // player casts this run per spell, hotbar and mage alike; reported on runEnded
        public List<string> startingSquadIds;    // squads the run began with; a merge passes the mark on (BandOfBrothers, FromLevyToLegend)
        public List<string> conscriptedSquadIds; // squads gained through Conscript Survivors; a merge passes the mark on (OneOfUs)
        public bool heldMage;                    // true once the army ever held a mage (SteelOverSorcery)
        public int campfireTrainings;            // squads trained at campfires this run (DrillSergeant)
        public List<TJ.Map.NodeType> nodeTypesVisited; // kinds of node picked this run (GrandTour)
        public int battlesWon;                   // battles won this run, garrisons included; BattlesFought resets every act
        public int unitsLost;                    // units the player's squads lost in battle this run (Run History)
        public int goldSpent;                    // gold taken from the purse this run, after the clamp at 0 (Run History)
        public int campfireRests;                // times the army rested at a campfire (Run History)
        public int consumablesUsed;              // consumables used this run, battle-scene uses included (Run History)
        public int healingItemsUsed;             // Minor and Major Health potions used (Run History)
        public int villagesSacked;               // towns sacked by size; CampaignSaveData.townsSacked is the total (Run History)
        public int castlesSacked;
        public int citiesSacked;
    }
    [System.Serializable] public struct SpellCastStored
    {
        public Spell Spell;
        public int Casts;
    }
    /// <summary>The node the player picked and what else was on offer, held until the node resolves.</summary>
    [System.Serializable] public struct NodeVisit
    {
        public bool recorded;
        public int nodeIndex;
        public bool hidden;
        public int goldOnEntry;
        public List<OfferedNode> offered;
    }
    /// <summary>A dice result locked in at a Games or Event node, written to the snapshot so a quit to the menu cannot undo it.</summary>
    [System.Serializable] public struct NodeResume
    {
        public bool active;
        public int nodeIndex;
        public TJ.Map.NodeType nodeType;
        // Games node: which table, its stake, the dice and where the game stands (GamesPanel's constants).
        public int tableGame;
        public int stake;
        public int playerFace;
        public int houseFace;
        public int wins;
        public int phase;
        public bool callHigher;
        public int goldChange;
        // Event node: the drawn event, the chosen choice and its d20.
        public string eventKey;
        public int choiceIndex;
        public int roll;
    }
    [System.Serializable] public struct OfferedNode
    {
        public int index;
        public TJ.Map.NodeType type;
        public bool hidden;
    }
    public struct RenownAward
    {
        public int chaptersCompleted;
        public int chapterRenown;
        public int actsCompleted;
        public int actRenown;
        public TT_Difficulty difficulty;
        public float difficultyMultiplier;
        public int marchBattles;
        public int marchRenown;
        public int ordealCount;
        public float ordealMultiplier;
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
    [System.Serializable] public class PlayerSaveData : IVersionedSave
    {
        public int saveVersion;
        int IVersionedSave.SaveVersion { get => saveVersion; set => saveVersion = value; }
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
        // The player ticked "Don't show this again" on the March guide. Reset Tutorial clears it.
        public bool hideMarchGuide;
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
        /// <summary>Every finished campaign, newest last. See <see cref="RunRecord"/>.</summary>
        public List<RunRecord> runHistory = new();
        // Totals over every recorded run, past the Run History cap. Seeded once from runHistory on load.
        public LifetimeStats lifetime = new();
        // Spells cast in fought campaign battles, ever (GrandGrimoire).
        public List<Spell> spellsEverCast = new();
        // Harsh weathers the player has won a fought battle in, ever (AllWeathers).
        public List<Weather> weathersWonIn = new();
        // Achievements earned while Steam could not take them; AchievementSync sends them once it can.
        public List<string> pendingAchievements = new();
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
    [System.Serializable] public struct SquadDamageStored
    {
        public string SquadGUID;
        public int Damage;
        // The worth of the enemy troops that damage destroyed, in unit value points. 0 in saves from before it existed.
        public float Value;
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

        // Set from the battle scene (UIManager) the first time the player's army drops below 25% of its
        // starting health. Consumed at battle end for "Against All Odds" - a win from that state.
        // Same battle-scene bridge as PauseUsedThisBattle; auto-resolved battles never set it.
        public static bool ArmyLossesSufferedThisBattle;

        // Player spell casts this battle, keyed by spell. Same battle-scene bridge as
        // PauseUsedThisBattle: SpellManager fills it, SaveSquadsPostBattle folds it into RunStats.
        public static readonly Dictionary<Spell, int> SpellsCastThisBattle = new();

        // In-memory authoritative copy of playerSaveData.json. SaveDataHandler is the sole gateway
        // to that file, so every read returns this cached instance and every write refreshes it.
        // Populated lazily on first LoadPlayerSaveData(); invalidated by DeletePlayerSaveData().
        static PlayerSaveData _playerCache;

        // Root directory every save file is read from and written to. Null means the real one.
        // Nothing in the game should ever set this: it exists so a test run can point the whole save
        // layer at a temp directory and be structurally unable to read or overwrite a player's save.
        static string _saveRootOverride;

        /// <summary>Where save files live. Defaults to Application.persistentDataPath.</summary>
        public static string SaveRoot => _saveRootOverride ?? Application.persistentDataPath;

        /// <summary>
        /// Redirects every save read and write to <paramref name="root"/>, or back to the real save
        /// directory when passed null.
        ///
        /// Dropping _playerCache is load-bearing rather than tidiness: it is an in-memory copy of the
        /// PREVIOUS root's file, and LoadPlayerSaveData returns it before ever touching disk, so
        /// without this a redirected read would be served the old root's data.
        /// </summary>
        public static void SetSaveRoot(string root)
        {
            _saveRootOverride = root;
            _playerCache = null;
            // Keybinds sit beside the saves but are written by Memori.Input, which cannot see this class.
            Memori.Input.JSONFileHandler.SetRoot(root);
        }

#if UNITY_EDITOR
        // Runs before any scene Awake, so a redirected Editor boot never reads the real save folder.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ApplyDevSaveRoot()
        {
            string root = Memori.Scenes.DevOverrides.BootIntoActThreeVictory
                ? ActThreeVictoryTestSave.PrepareFolder()
                : Memori.Scenes.DevOverrides.SaveRoot;
            SetSaveRoot(string.IsNullOrEmpty(root) ? null : root);
            if (!string.IsNullOrEmpty(root)) UnityEngine.Debug.LogWarning($"[SaveDataHandler] Dev save root: {root}");
        }
#endif

        public static bool CheckForGear(GearID _gearID)
        {
            return Load().Gear.Contains(_gearID);
        }

        public static GearIDsSerialized GetGearCollected()
        {
            CampaignSaveData run = Load();
            List<GearID> gearIDs = run.Gear.FindAll(gear => !run.IsGearInactive(gear));
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
        // Format version stamped into every save; 0 means written before versioning. Bump it when a save's meaning
        // changes and migrate older versions on read. An older build drops the field, so migrations must be safe to rerun.
        public const int CURRENT_SAVE_VERSION = 1;

        private static void SaveToJSON<T> (T toSave, string filename)
        {
            if (toSave is IVersionedSave versioned) versioned.SaveVersion = CURRENT_SAVE_VERSION;
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
            return useLocal ? Application.dataPath + "/Data/" + filename : Path.Combine(SaveRoot, filename);
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
            toSave.playTimeSeconds += RunClock.TakeUnflushed();
            SaveToJSON(toSave, "campaignSaveData.json");
        }
        public static void SaveCampaignSnapshot(CampaignSaveData toSave)
        {
            toSave.snapShot = true;
            toSave.playTimeSeconds += RunClock.TakeUnflushed();
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
        public const int SLAUGHTERS_CHAMPION_KILLS = 250;
        // Checked at battle end, so it no longer waits for the squad's card to be drawn on the map.
        public static void CheckSlaughtersChampion(List<SquadKillsStored> _historicalKills, ICollection<string> _playerSquadGuids)
        {
            if (_historicalKills == null) return;
            foreach (SquadKillsStored entry in _historicalKills)
            {
                if (entry.Kills < SLAUGHTERS_CHAMPION_KILLS || !_playerSquadGuids.Contains(entry.SquadGUID)) continue;
                SteamAchievements.Unlock(AchievementId.HighKill);
                return;
            }
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
        // Slots 0-9 take the field, and so do goblins in reserve.
        public static bool FightsInBattle(SquadToLoad squad) =>
            squad.UnitIndex != -1 && (squad.UnitIndex < 10 || TabletopTavernConstants.IsTrueGoblin(squad.UnitName));
        // For building a battle only: a fallen Restless Dead squad stays in the army but never takes the field.
        public static bool TakesTheField(SquadToLoad squad) =>
            FightsInBattle(squad) && squad.SquadCurrentHealth > 0;
        /// <summary>
        /// Saves the squads after a manual battle has been completed.
        /// </summary>
        /// <param name="_playerSquads"></param>
        /// <param name="_enemySquads"></param>
        /// <param name="_playerWon"></param>
        /// <param name="_squadIdKillCounter"></param>
        /// <param name="_squadIdLossCounter"></param>
        public static void SaveSquadsPostBattle(SquadToLoad[] _playerSquads, SquadToLoad[] _enemySquads, bool _playerWon, List<SquadKillsStored> _squadIdKillCounter, List<SquadLossesStored> _squadIdLossCounter, List<SquadDamageStored> _squadDamage, int _spellKills = 0, AnalyticsBattleReport _report = null)
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

            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (!TakesTheField(saveData.playerArmy[i])) continue;

                saveData.playerArmy[i] = GetPlayerSquad(saveData.playerArmy[i].UniqueID);
            }
            saveData.enemyArmy = _enemySquads;
            saveData.battleCompleted = true;
            saveData.playerWonBattle = _playerWon;
            if (_playerWon)
            {
                saveData.wonBattleThisTurn = true;
                saveData.RunStats.battlesWon++;
            }
            saveData.spoilsTaken = null;
            saveData.manaDraughtsArmed = 0;
            saveData.eventBattleEffects = new EventBattleEffects();
            saveData.SquadKillsStore = _squadIdKillCounter;

            saveData.HistoricalKillStore = AddToHistoricalKills(saveData.HistoricalKillStore, _squadIdKillCounter);
            saveData.SquadLossesStore = _squadIdLossCounter;
            saveData.SquadDamageStore = _squadDamage;
            // Read before the line below marks every fought battle's town as sacked.
            bool garrison = IsGarrisonBattle(saveData);
            if(saveData.townData == null) {
                saveData.townData = new TownSaveData();
                UnityEngine.Debug.Log($"Created new TownSaveData in SaveSquadsPostBattle");
            }
            saveData.townData.townInteractionStatus = TownInteractionStatus.Sacked;
            // saveData.withdrawnSquads = _withdrawnSquads;

            // The kill and loss lists carry enemy squads too; run stats and achievements count the player's only.
            HashSet<string> playerSquadGuids = new();
            foreach (SquadToLoad squad in _playerSquads) playerSquadGuids.Add(squad.UniqueID);

            // Hotbar spell kills belong to no squad (ArmySpawnManager.SPELL_KILL_SQUAD_ID) and would
            // otherwise vanish here; they count toward the run total even though no card shows them.
            int totalKills = _spellKills;
            foreach (var squadKill in _squadIdKillCounter)
            {
                if (playerSquadGuids.Contains(squadKill.SquadGUID)) totalKills += squadKill.Kills;
            }
            UnityEngine.Debug.Log($"Total enemies slain in battle: {totalKills}");
            saveData.RunStats.enemiesSlain += totalKills;

            // Read before the battle-scene flags and the spell tally are cleared below.
            if (_report != null)
            {
                _report.PauseUsed = PauseUsedThisBattle;
                _report.ArmyLossTriggered = ArmyLossesSufferedThisBattle;
                _report.SpellKills = _spellKills;
                foreach (KeyValuePair<Spell, int> cast in SpellsCastThisBattle) _report.SpellCasts[cast.Key.ToString()] = cast.Value;
            }

            //achievement tracking - archer used in battle
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (!FightsInBattle(saveData.playerArmy[i])) continue;

                // Only the Ranged class breaks the No Archers run; Hybrids count as melee.
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

            saveData.RunStats.spellsCast ??= new List<SpellCastStored>();
            foreach (KeyValuePair<Spell, int> cast in SpellsCastThisBattle)
            {
                int index = saveData.RunStats.spellsCast.FindIndex(entry => entry.Spell == cast.Key);
                if (index < 0) saveData.RunStats.spellsCast.Add(new SpellCastStored { Spell = cast.Key, Casts = cast.Value });
                else saveData.RunStats.spellsCast[index] = new SpellCastStored { Spell = cast.Key, Casts = saveData.RunStats.spellsCast[index].Casts + cast.Value };
            }
            List<Spell> spellsCastThisBattle = new(SpellsCastThisBattle.Keys);
            SpellsCastThisBattle.Clear();

            RecordBattleForRunHistory(saveData, _playerSquads, _enemySquads, _playerWon, true, garrison, totalKills, _squadIdLossCounter);

            // The battle result goes to disk before any stats or achievement work so a failure below
            // cannot leave the map treating this battle as unfought.
            SaveCampaign(saveData);

            //update the last snapshot to overwrite the snapshot of the pre battle state since the battle is now completed
            SaveCampaignSnapshot(saveData);

            GameEventTracker.BattleEnded(saveData, _report);

            RecordUnitNameKills(_playerSquads, _squadIdKillCounter);

            //achievement check - cav only
            bool cavOnly = true;
            for (int i = 0; i < saveData.playerArmy.Length; i++)
            {
                if (!FightsInBattle(saveData.playerArmy[i])) continue;

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
                    foreach (SquadLossesStored loss in _squadIdLossCounter)
                        if (playerSquadGuids.Contains(loss.SquadGUID)) totalUnitsLost += loss.Losses;
                }
                if (totalUnitsLost == 0) SteamAchievements.Unlock(AchievementId.FlawlessVictory);

                // "Against All Odds" - won after the army fell below 25% health.
                if (ArmyLossesSufferedThisBattle) SteamAchievements.Unlock(AchievementId.AgainstAllOdds);
            }
            ArmyLossesSufferedThisBattle = false;

            CheckSlaughtersChampion(saveData.HistoricalKillStore, playerSquadGuids);
            BattleAchievements.Evaluate(saveData, _playerWon, _squadIdKillCounter, _spellKills, _report, _enemySquads, spellsCastThisBattle);
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
            MigrateRunHistoryArmies(loadedSaveData);
            SeedLifetimeStats(loadedSaveData);
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
            TT_Difficulty highestUnlocked = DifficultyRules.HighestUnlocked(saveData.MaxDifficultyOverall);

            for (int i = 0; i < saveData.HeroLastDifficulties.Count; i++)
            {
                if (saveData.HeroLastDifficulties[i].HeroID == heroID)
                {
                    TT_Difficulty lastWin = saveData.HeroLastDifficulties[i].LastDifficulty;
                    if (DifficultyRules.Rank(lastWin) < DifficultyRules.Rank(saveData.MaxDifficultyOverall))
                        return DifficultyRules.Normalize(lastWin);
                    break;
                }
            }

            return highestUnlocked;
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
        /// <summary>
        /// The run's four spells as assets, ready for SpellManager. Re-sanitized on every read so a
        /// save written before a spell's enum value changed, or before a mod removed a spell, still
        /// yields a legal four-slot loadout instead of null entries. See SpellLoadout.
        /// </summary>
        public static SpellData[] GetCampaignSpells()
        {
            CampaignSaveData campaignSaveData = Load();
            Spell[] sanitized = SpellLoadout.Sanitize(campaignSaveData.selectedSpells, campaignSaveData.heroID);
            // After Sanitize, which would refill an empty unlocked slot; selectedSpells itself is never changed.
            int sealedSlot = campaignSaveData.sealedSpellSlot;
            if (sealedSlot > 0 && sealedSlot < sanitized.Length) sanitized[sealedSlot] = Spell.None;
            return SpellRegistry.Resolve(sanitized);
        }
        /// <summary>A campaign hotbar slot is locked when Renown has not opened it, or Sealed Page sealed it.</summary>
        public static bool IsCampaignSlotLocked(int slotIndex)
        {
            if (SpellLoadout.IsSlotLocked(slotIndex)) return true;
            int sealedSlot = Load().sealedSpellSlot;
            return sealedSlot > 0 && slotIndex == sealedSlot;
        }
        /// <summary>
        /// The spell mana budget for one battle. Static because the battle scene has no
        /// CampaignSaveManager - the same reason GetCampaignSpells lives here.
        ///
        /// Base pool, plus SPELL_MANA_POOL_PER_ACT for every act after the first (capped at
        /// SPELL_MANA_POOL_CAP for endless acts), plus the Renown bonus, plus SPELL_MANA_POOL_DRAUGHT
        /// per Mana Draught armed on the save. The act and the
        /// draughts are read off the campaign save on disk because this runs in the battle scene.
        /// A custom battle has no act and ignores Renown and draughts: it always gets the fixed
        /// sandbox maximum. The pool is granted whole at the start of every battle and does not
        /// regenerate or carry over, so this is the only place its size is decided. This is a pure
        /// read; the draughts are spent by SaveSquadsPostBattle, not here.
        /// </summary>
        public static int GetSpellManaPool()
        {
            if (SpellTestMode.Active) return SpellTestMode.ManaPool;
            if (IsCustomBattle()) return TabletopTavernConstants.SPELL_MANA_POOL_CUSTOM_BATTLE;

            CampaignSaveData save = Load();
            int act = Math.Max(1, save.bookNumber);
            int actPool = Math.Min(TabletopTavernConstants.SPELL_MANA_POOL_CAP,
                TabletopTavernConstants.SPELL_MANA_POOL_BASE + (act - 1) * TabletopTavernConstants.SPELL_MANA_POOL_PER_ACT);
            int pool = actPool
                 + SpellLoadout.GetManaBonus()
                 + save.manaDraughtsArmed * TabletopTavernConstants.SPELL_MANA_POOL_DRAUGHT
                 + (save.eventBattleEffects == null ? 0 : save.eventBattleEffects.mana);
            // Arcane Drought shrinks the whole pool, Renown and Mana Draughts included.
            if (save.HasOrdeal(OrdealId.ArcaneDrought)) pool = (int)Math.Ceiling(pool * OrdealRegistry.ARCANE_DROUGHT_MANA);
            return pool;
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
        public static void CreateCampaign(Hero hero, ArmySaveData armySaveData, TT_Difficulty _difficultyLevelSelected, GearID _startingGear, Guid _runUUID, int startingGold, Spell[] _selectedSpells = null, AnalyticsRunSetup _setup = null)
        {
            SquadToLoad[] squadsToLoad = new SquadToLoad[armySaveData.SquadsInArmy.Length];
            for(int i = 0; i < armySaveData.SquadsInArmy.Length; i++) {
                squadsToLoad[i] = new SquadToLoad(armySaveData.SquadsInArmy[i], 0, i);
            }
            CreateCampaign(hero, squadsToLoad, _difficultyLevelSelected, _startingGear, _runUUID, startingGold, _selectedSpells, _setup);
        }
        public static void CreateCampaign(Hero hero, SquadToLoad[] squadsToLoad, TT_Difficulty _difficultyLevelSelected, GearID _startingGear, Guid _runUUID, int startingGold, Spell[] _selectedSpells = null, AnalyticsRunSetup _setup = null)
        {
            // UnityEngine.Debug.Log($"Creating campaign with hero: {hero.HeroID} and difficulty: {_difficultyLevelSelected}");
            // Quick restart replays a saved value that may come from the other ladder; a new run starts on today's.
            _difficultyLevelSelected = DifficultyRules.Normalize(_difficultyLevelSelected);
            SquadToLoad[] playerArmy = new SquadToLoad[13];
            for(int i = 0; i < playerArmy.Length; i++) {
                playerArmy[i].UnitIndex = -1;
            }
            //DifficultyMod 18, via DifficultyRules so the difficulty sim starts its armies the same way
            float startingHealth = DifficultyRules.StartingHealth(_difficultyLevelSelected);

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
                HeroBonusManager.ApplyHeroBaseUnitCount(ref playerArmy[i], hero.HeroID);
                recruitedUnitNames.Add(playerArmy[i].UnitName);
            }
            AquiredTroops(recruitedUnitNames);

            int seed = UnityEngine.Random.Range(0, 1000000);
            CampaignSaveData campaignSaveData = new (seed, hero.HeroID, startingGold, playerArmy, _difficultyLevelSelected, _startingGear, _runUUID, _selectedSpells);
            campaignSaveData.RunStats.startingSquadIds = new List<string>(squadsToLoad.Length);
            for (int i = 0; i < squadsToLoad.Length; i++) campaignSaveData.RunStats.startingSquadIds.Add(playerArmy[i].UniqueID);

            RunClock.Reset();
            SaveCampaign(campaignSaveData);
            SaveCampaignSnapshot(campaignSaveData);
            SaveLastCampaignStats(hero.HeroID, _difficultyLevelSelected, _startingGear, squadsToLoad, startingGold);
            GameEventTracker.RunStarted(campaignSaveData, _setup);
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
            EvaluateGearCollection();
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
            EvaluateRaceCollection(TabletopTavernData.Instance.GetRaceFromUnitName(_unitName));
        }
        // Batch variant: records several recruited troops with a single save, so callers adding a
        // whole army (e.g. CreateCampaign) don't pay one whole-file write per unit.
        public static void AquiredTroops(IEnumerable<UnitName> _unitNames)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            bool changed = false;
            HashSet<Race> racesAdded = new();
            foreach (UnitName unitName in _unitNames)
            {
                if (saveData.troopsRecruited.Contains(unitName)) continue;
                saveData.troopsRecruited.Add(unitName);
                racesAdded.Add(TabletopTavernData.Instance.GetRaceFromUnitName(unitName));
                changed = true;
            }
            if (!changed) return;

            SavePlayerSaveData(saveData);
            foreach (Race race in racesAdded) EvaluateRaceCollection(race);
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
            EvaluateConsumableCollection();
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

        #region Run history
        /// <summary>Newest run first. The stored list is oldest-first so appends are cheap.</summary>
        public const int MAX_RUN_HISTORY = 100;

        public static List<RunRecord> GetRunHistory()
        {
            List<RunRecord> history = LoadPlayerSaveData().runHistory;
            var newestFirst = new List<RunRecord>(history);
            newestFirst.Reverse();
            return newestFirst;
        }

        /// <summary>
        /// Records a run the player walked away from. Wins and losses are recorded inside
        /// <see cref="RecordGameOver"/> so they share its save write; this is the entry for the
        /// three abandon paths (main-menu Abandon Run, the settings Abandon Run, Quick Restart),
        /// which each already hold the save they are about to delete.
        /// </summary>
        public static void RecordAbandonedRun(CampaignSaveData abandonedRun)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            abandonedRun.playTimeSeconds += RunClock.TakeUnflushed();

            // A run that already banked its act 3 win ends as a victory at the act reached and still pays
            // renown for the acts it finished.
            RunOutcome outcome = RunOutcome.Abandon;
            int renownEarned = 0;
            if (abandonedRun.victoryBanked)
            {
                RenownAward award = ComputeRenownReward(abandonedRun.RunStats, Mathf.Max(abandonedRun.bookNumber - 1, 0), abandonedRun.difficultyLevel, abandonedRun.ordeals?.Count ?? 0, abandonedRun.marchBattlesWon);
                saveData.renown += award.total;
                renownEarned = award.total;
                outcome = RunOutcome.Win;
            }

            // A run abandoned before its first battle is a restart, not a run, and would push real runs out of the cap.
            if (outcome == RunOutcome.Abandon && RunBattlesFought(abandonedRun) == 0) return;

            AppendRunRecord(saveData, abandonedRun, outcome, renownEarned);
            SavePlayerSaveData(saveData);
        }

        /// <summary>
        /// Snapshots the run onto the player save. Does not write - the caller owns the save
        /// round-trip so a win records in the same write as its renown.
        /// </summary>
        private static void AppendRunRecord(PlayerSaveData saveData, CampaignSaveData run, RunOutcome outcome, int renownEarned)
        {
            if (run == null || run.blank) return;

            var record = new RunRecord
            {
                runUUID = run.RunId,
                heroID = run.heroID,
                difficulty = run.difficultyLevel,
                outcome = outcome,
                endedAtUtcTicks = DateTime.UtcNow.Ticks,
                actReached = run.bookNumber,
                chaptersCompleted = run.RunStats.chaptersCompleted,
                battlesFought = RunBattlesFought(run),
                marchBattles = run.marchBattlesWon,
                goldAtEnd = run.goldAmount,
                goldEarned = run.RunStats.goldEarned,
                enemiesSlain = run.RunStats.enemiesSlain,
                renownEarned = renownEarned,
                playTimeSeconds = run.playTimeSeconds,
                squads = RunSquad.FromArmy(run.playerArmy, run.HistoricalKillStore, run.squadLossTotals),
                gear = run.Gear != null ? new List<GearID>(run.Gear) : new List<GearID>(),
                spells = run.selectedSpells != null ? new List<Spell>(run.selectedSpells) : new List<Spell>(),

                detailVersion = RunRecord.CURRENT_DETAIL_VERSION,
                startingGold = run.startingGold,
                goldSpent = run.RunStats.goldSpent,
                unitsLost = run.RunStats.unitsLost,
                unitsPrestiged = run.RunStats.unitsPrestiged,
                unitsRecruited = run.RunStats.unitsRecruited,
                gearFound = run.RunStats.gearAquired,
                shopPurchases = run.RunStats.shopPurchases,
                goldWagered = run.RunStats.goldWagered,
                campfireRests = run.RunStats.campfireRests,
                campfireTrainings = run.RunStats.campfireTrainings,
                consumablesUsed = run.RunStats.consumablesUsed,
                healingItemsUsed = run.RunStats.healingItemsUsed,
                townsSacked = run.townsSacked,
                villagesSacked = run.RunStats.villagesSacked,
                castlesSacked = run.RunStats.castlesSacked,
                citiesSacked = run.RunStats.citiesSacked,
                spellsCast = run.RunStats.spellsCast != null ? new List<SpellCastStored>(run.RunStats.spellsCast) : new List<SpellCastStored>(),
                fallen = run.fallenSquads != null ? new List<RunSquad>(run.fallenSquads) : new List<RunSquad>(),
                acts = run.actArmies != null ? new List<RunAct>(run.actArmies) : new List<RunAct>(),
                battles = run.battleLog != null ? new List<RunBattle>(run.battleLog) : new List<RunBattle>(),
            };

            if (saveData.runHistory == null) saveData.runHistory = new List<RunRecord>();
            saveData.runHistory.Add(record);
            if (saveData.runHistory.Count > MAX_RUN_HISTORY)
                saveData.runHistory.RemoveRange(0, saveData.runHistory.Count - MAX_RUN_HISTORY);
            for (int i = 0; i < saveData.runHistory.Count - MAX_BATTLE_LOGS; i++)
                saveData.runHistory[i].battles = new List<RunBattle>();

            (saveData.lifetime ??= new LifetimeStats()).Add(record);
        }

        /// <summary>How many of the newest runs keep their battle-by-battle log; older runs keep only their totals.</summary>
        public const int MAX_BATTLE_LOGS = 20;

        // battlesWon misses losses and BattlesFought resets every act; the battle log has every battle, but only on newer saves.
        private static int RunBattlesFought(CampaignSaveData run) =>
            Math.Max(Math.Max(run.RunStats.battlesWon, run.BattlesFought), run.battleLog?.Count ?? 0);

        private static bool IsGarrisonBattle(CampaignSaveData run) =>
            run.townData != null && run.townData.townInteractionStatus == TownInteractionStatus.GarrisonBattleStarted;

        /// <summary>
        /// Adds one resolved battle to the run's history: units lost, each squad's loss total and a battle log entry.
        /// Both result paths call it once per battle, before the save is written, so a reload cannot count it twice.
        /// </summary>
        public static void RecordBattleForRunHistory(CampaignSaveData run, SquadToLoad[] playerSquads, SquadToLoad[] enemySquads,
            bool playerWon, bool fought, bool garrison, int kills, List<SquadLossesStored> losses)
        {
            // The fought path's loss list carries enemy squads too.
            HashSet<string> playerSquadGuids = new();
            foreach (SquadToLoad squad in playerSquads) playerSquadGuids.Add(squad.UniqueID);

            int lost = 0;
            run.squadLossTotals ??= new List<SquadLossesStored>();
            if (losses != null)
            {
                foreach (SquadLossesStored loss in losses)
                {
                    if (loss.Losses <= 0 || !playerSquadGuids.Contains(loss.SquadGUID)) continue;
                    lost += loss.Losses;
                    int index = run.squadLossTotals.FindIndex(entry => entry.SquadGUID == loss.SquadGUID);
                    if (index < 0) run.squadLossTotals.Add(loss);
                    else run.squadLossTotals[index] = new SquadLossesStored { SquadGUID = loss.SquadGUID, Losses = run.squadLossTotals[index].Losses + loss.Losses };
                }
            }
            run.RunStats.unitsLost += lost;

            int fallen = 0;
            foreach (SquadToLoad squad in playerSquads)
                if (RunSquad.IsFilled(squad) && squad.SquadCurrentHealth <= 0) fallen++;

            (run.battleLog ??= new List<RunBattle>()).Add(new RunBattle
            {
                act = run.bookNumber,
                node = run.selectedNodeType,
                town = garrison && run.townData != null ? (int)run.townData.townSize : -1,
                race = BattleAchievements.FirstFactionRace(enemySquads, TabletopTavernData.Instance),
                warlord = run.enemyWarlordHeroID,
                weather = run.battleFieldPreset.weather,
                fought = fought,
                won = playerWon,
                kills = kills,
                lost = lost,
                fallen = fallen,
            });
        }

        /// <summary>Keeps a squad that died, with its run kills and losses, before it is cleared from the army.</summary>
        public static void RecordFallenSquad(CampaignSaveData run, SquadToLoad squad)
        {
            if (!RunSquad.IsFilled(squad)) return;
            (run.fallenSquads ??= new List<RunSquad>()).Add(RunSquad.From(squad, -1, run.HistoricalKillStore, run.squadLossTotals));
        }

        /// <summary>Keeps the army as an act ends. Safe to call twice for the same act: the newer one replaces the older.</summary>
        public static void RecordActArmy(CampaignSaveData run)
        {
            run.actArmies ??= new List<RunAct>();
            run.actArmies.RemoveAll(entry => entry.act == run.bookNumber);
            run.actArmies.Add(new RunAct
            {
                act = run.bookNumber,
                gold = run.goldAmount,
                squads = RunSquad.FromArmy(run.playerArmy, run.HistoricalKillStore, run.squadLossTotals),
            });
        }

        // In memory only, like the army migration: the next player save writes it. Safe to run twice.
        private static void SeedLifetimeStats(PlayerSaveData saveData)
        {
            saveData.lifetime ??= new LifetimeStats();
            if (saveData.lifetime.seeded) return;
            if (saveData.runHistory != null)
                foreach (RunRecord record in saveData.runHistory) saveData.lifetime.Add(record);
            saveData.lifetime.seeded = true;
        }

        // In memory only: the next player save writes the compact form, and a test pointed at a fixture folder never writes to it.
        private static void MigrateRunHistoryArmies(PlayerSaveData saveData)
        {
            if (saveData.runHistory == null) return;
            foreach (RunRecord record in saveData.runHistory)
            {
                if (record.army == null || record.army.Length == 0) continue;
                record.squads = RunSquad.FromArmy(record.army);
                record.army = Array.Empty<SquadToLoad>();
            }
        }
        #endregion

        #region Collection achievements
        // Evaluated both on acquisition and when CollectionPanel opens, so the pop lands at the moment
        // the set is completed rather than waiting for the player to visit the collection screen.
        // Race.Special is deliberately absent: it holds structures, not a collectable roster.
        private static readonly Dictionary<Race, AchievementId> RaceCollectionAchievements = new()
        {
            { Race.IronLegion,      AchievementId.IronLegionCollection },
            { Race.Gruntkin,        AchievementId.GruntkinCollection },
            { Race.RavenHost,       AchievementId.RavenHostCollection },
            { Race.TaelindorForest, AchievementId.TaelindorForestCollection },
            { Race.SanguineCourt,   AchievementId.SanguineCourtCollection },
            { Race.SakuraDynasty,   AchievementId.SakuraDynastyCollection },
            { Race.DeepstoneHold,   AchievementId.DeepstoneHoldCollection },
            { Race.DrakosaurBrood,  AchievementId.DrakosaurBroodCollection },
        };

        public static void EvaluateRaceCollection(Race _race)
        {
            if (!RaceCollectionAchievements.TryGetValue(_race, out AchievementId achievementId)) return;

            UnitName[] roster = TabletopTavernData.Instance.GetUnitsOfRace(_race);
            // A mod can empty a roster via unit_overrides.json; without this, 0 >= 0 would auto-unlock.
            if (roster.Length == 0) return;

            List<UnitName> recruited = GetTroopsIDsCollected();
            int collectedCount = 0;
            for (int i = 0; i < roster.Length; i++)
            {
                if (recruited.Contains(roster[i])) collectedCount++;
            }

            if (collectedCount < roster.Length) return;
            SteamAchievements.Unlock(achievementId);
            if (AllRaceCollectionsComplete()) SteamAchievements.Unlock(AchievementId.GrandCollector);
        }

        // An emptied roster cannot be completed, so a mod that empties one also blocks Grand Collector.
        private static bool AllRaceCollectionsComplete()
        {
            List<UnitName> recruited = GetTroopsIDsCollected();
            foreach (Race race in RaceCollectionAchievements.Keys)
            {
                UnitName[] roster = TabletopTavernData.Instance.GetUnitsOfRace(race);
                if (roster.Length == 0) return false;
                foreach (UnitName unit in roster)
                    if (!recruited.Contains(unit)) return false;
            }
            return true;
        }

        public static void EvaluateGearCollection()
        {
            GearID[] allGear = GearData.GetGearIDs();
            if (allGear.Length == 0) return;

            // Counts only ids that are actually in the roster. The raw list length is not safe:
            // it has historically held stale ids (see the legacy 0 placeholder stripped in AquiredGear).
            List<int> collected = GetGearIDsCollected();
            int collectedCount = 0;
            for (int i = 0; i < allGear.Length; i++)
            {
                if (collected.Contains((int)allGear[i])) collectedCount++;
            }

            if (collectedCount >= allGear.Length) SteamAchievements.Unlock(AchievementId.CollectionGear);
        }

        public static void EvaluateConsumableCollection()
        {
            ConsumableEnum[] allConsumables = ConsumableData.GetAllConsumableEnums();
            if (allConsumables.Length == 0) return;

            List<int> collected = GetPotionsIDsCollected();
            int collectedCount = 0;
            for (int i = 0; i < allConsumables.Length; i++)
            {
                if (collected.Contains((int)allConsumables[i])) collectedCount++;
            }

            if (collectedCount >= allConsumables.Length) SteamAchievements.Unlock(AchievementId.CollectionConsumables);
        }
        #endregion
        // Placeholder tuning values - adjust to taste.
        private const int RENOWN_PER_CHAPTER = 1;
        private const int RENOWN_PER_ACT_COMPLETED = 50;

        // The March pays by the battle (MarchRules.Renown), not by the act: only story acts pay the act rate.
        private static RenownAward ComputeRenownReward(RunStats runStats, int bookNumber, TT_Difficulty difficulty, int ordealCount, int marchBattles = 0)
        {
            int chapterRenown = runStats.chaptersCompleted * RENOWN_PER_CHAPTER;
            int storyActs = Mathf.Min(bookNumber, TabletopTavernConstants.FINAL_STORY_ACT);
            int actRenown = storyActs * RENOWN_PER_ACT_COMPLETED;
            int marchRenown = MarchRules.Renown(marchBattles);
            float difficultyMultiplier = DifficultyRules.RenownMultiplier(difficulty);
            float ordealMultiplier = OrdealRegistry.RenownMultiplier(ordealCount);
            int total = Mathf.RoundToInt((chapterRenown + actRenown + marchRenown) * difficultyMultiplier * ordealMultiplier);

            return new RenownAward
            {
                chaptersCompleted = runStats.chaptersCompleted,
                chapterRenown = chapterRenown,
                actsCompleted = storyActs,
                actRenown = actRenown,
                marchBattles = marchBattles,
                marchRenown = marchRenown,
                difficulty = difficulty,
                difficultyMultiplier = difficultyMultiplier,
                ordealCount = ordealCount,
                ordealMultiplier = ordealMultiplier,
                total = total
            };
        }

        /// <summary>
        /// Locks the win in the moment the last story act falls: completions, difficulty unlock, hero
        /// records and roster achievement. A player who marches on into endless acts
        /// keeps all of it whatever happens next. Runs once per run; RecordGameOver skips the same block
        /// once the campaign save says it ran.
        /// </summary>
        public static void RecordVictoryUnlocks()
        {
            CampaignSaveData campaignSaveData = CampaignManager.Instance.CampaignSaveManager.SaveData;
            if (campaignSaveData.victoryBanked) return;

            PlayerSaveData saveData = LoadPlayerSaveData();
            campaignSaveData.playTimeSeconds += RunClock.TakeUnflushed();
            campaignSaveData.victoryWasFirstHeroCompletion = GetHeroDifficultiesCompleted(campaignSaveData.heroID).Count == 0;

            ApplyVictoryUnlocks(saveData, campaignSaveData);
            campaignSaveData.victoryBanked = true;

            SavePlayerSaveData(saveData);
        }

        public static RenownAward RecordGameOver(bool _playerWon)
        {
            UnityEngine.Debug.Log($"Recording game over, player won: {_playerWon}");

            CampaignSaveData campaignSaveData = CampaignManager.Instance.CampaignSaveManager.SaveData;
            PlayerSaveData saveData = LoadPlayerSaveData();
            campaignSaveData.playTimeSeconds += RunClock.TakeUnflushed();

            // bookNumber is the act currently in progress. On a win it was actually finished, but on a
            // loss it wasn't - don't award renown for the act the player died in.
            int actsCompleted = _playerWon ? campaignSaveData.bookNumber : Mathf.Max(campaignSaveData.bookNumber - 1, 0);
            RenownAward renownAward = ComputeRenownReward(campaignSaveData.RunStats, actsCompleted, campaignSaveData.difficultyLevel, campaignSaveData.ordeals?.Count ?? 0, campaignSaveData.marchBattlesWon);
            saveData.renown += renownAward.total;

            if (saveData.renown >= 100)
                SteamAchievements.Unlock(AchievementId.ASnackForLater);

            // A banked victory already ran the win block; the run is a win however it ended.
            bool countsAsWin = _playerWon || campaignSaveData.victoryBanked;
            if (_playerWon && !campaignSaveData.victoryBanked)
                ApplyVictoryUnlocks(saveData, campaignSaveData);

            // --- Legacy deposited-gold sweep, disabled - kept in case this system is restored ---
            // saveData.goldToDeposit += campaignSaveData.goldAmount;
            // SavePlayerSaveData(saveData);
            // DepositGold();

            AppendRunRecord(saveData, campaignSaveData, countsAsWin ? RunOutcome.Win : RunOutcome.Loss, renownAward.total);

            SavePlayerSaveData(saveData);
            return renownAward;
        }

        private static void ApplyVictoryUnlocks(PlayerSaveData saveData, CampaignSaveData campaignSaveData)
        {
            saveData.gameCompletions++;

            int currentHeroID = campaignSaveData.heroID;
            int newDifficulty = (int)campaignSaveData.difficultyLevel;

            //update max difficulty unlocked if needed
            saveData.MaxDifficultyOverall = DifficultyRules.Harder(saveData.MaxDifficultyOverall, newDifficulty);

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
            if (AchievementRules.TryGetHeroVictory(currentHeroID, out AchievementId heroVictory)) SteamAchievements.Unlock(heroVictory);
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
        /// <summary>
        /// The hero the campaign save is being played with, or the default hero when there is
        /// no campaign. Callers load the prefab themselves so they own its Addressable handle.
        /// </summary>
        public static int GetPlayerHeroID()
        {
            return CampaignSaveExists() ? Load().heroID : HeroData.DefaultHeroID;
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
            // UnityEngine.Debug.Log($"IsMetaprogressionNodeUnlocked {_node.name}: {conditionUnlocked}");
            return conditionUnlocked;
        }
        #region Dev Tools
        public static void AddRenown(int _amount)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            // Renown is a lifetime total that spending never lowers, so it cannot go below zero.
            saveData.renown = Math.Max(0, saveData.renown + _amount);
            SavePlayerSaveData(saveData);
        }
        /// <summary>Marks every Collection entry found and seen, or clears them all, in one write.</summary>
        public static void DevSetCollection(bool _found, IEnumerable<UnitName> _units, IEnumerable<GearID> _gear, IEnumerable<ConsumableEnum> _consumables)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            saveData.troopsRecruited.Clear();
            saveData.troopsAcknowledged.Clear();
            saveData.gearIdsCollected.Clear();
            saveData.gearIdsAcknowledged.Clear();
            saveData.consumablesAquired.Clear();
            saveData.consumablesAcknowledged.Clear();
            if (_found)
            {
                saveData.troopsRecruited.AddRange(_units);
                saveData.troopsAcknowledged.AddRange(_units);
                foreach (GearID gear in _gear)
                {
                    saveData.gearIdsCollected.Add((int)gear);
                    saveData.gearIdsAcknowledged.Add((int)gear);
                }
                foreach (ConsumableEnum consumable in _consumables)
                {
                    saveData.consumablesAquired.Add((int)consumable);
                    saveData.consumablesAcknowledged.Add((int)consumable);
                }
            }
            SavePlayerSaveData(saveData);
        }
        #endregion
        // Writes the completion only: no Steam call, no last-difficulty or completion count. Steam still awards the hero's victory at the next stats sync.
        public static void RecordHeroCompletionForTesting(int _heroID, TT_Difficulty _difficulty)
        {
            PlayerSaveData saveData = LoadPlayerSaveData();
            int newDifficulty = (int)DifficultyRules.Normalize(_difficulty);

            //update max difficulty unlocked if needed
            saveData.MaxDifficultyOverall = DifficultyRules.Harder(saveData.MaxDifficultyOverall, newDifficulty);

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
    }
}
