#if UNITY_EDITOR || TESTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Memori.SaveData;
using Memori.Scenes;
using UnityEngine;

namespace TJ
{
    // Tabletop Tavern > Boot Into Act III Victory (Hard): every Play starts on the map with the Act III
    // final just won on Hard, from a fresh save in Library/DevSaves, so the real save folder is never touched.
    // TESTING builds reach the same run from a main menu button (MainMenu.AddActThreeVictoryButton).
    public static class ActThreeVictoryTestSave
    {
        #region Settings
        // Fixed so every Play meets the same map, enemy faction and armies.
        private const int SEED = 424242;
        private const TT_Difficulty DIFFICULTY = TT_Difficulty.Hard;
        // Mirror MapGenerator.layers and nodesPerLayer in Map.unity: the final Horde node keeps index
        // (layers - 1) * nodesPerLayer. If they drift, LoadPostBattle falls back to the final layer's only node.
        private const int MAP_LAYERS = 12;
        private const int NODES_PER_LAYER = 3;
        private const int FINAL_NODE_INDEX = (MAP_LAYERS - 1) * NODES_PER_LAYER;
        private const int BATTLES_FOUGHT_IN_ACT = 7;
        private const int GOLD = 60;
        private const int GEAR_COUNT = 3;
        // Lowest share of max health a squad keeps after the final.
        private const float MIN_HEALTH_LEFT = 0.65f;
        #endregion

#if UNITY_EDITOR
        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "DevSaves", "ActThreeVictory"));

        #region Folder
        // Called from SaveDataHandler.ApplyDevSaveRoot before any scene loads: empties the dev folder and
        // copies in the real player save and keybinds, read-only, so unlocks and seen tutorials carry over.
        public static string PrepareFolder()
        {
            string folder = Folder;
            string realRoot = Path.GetFullPath(Application.persistentDataPath);
            if (string.Equals(folder.TrimEnd('\\', '/'), realRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("[ActThreeVictoryTestSave] Dev folder resolved to the real save folder.");

            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            CopyIfPresent(realRoot, folder, "playerSaveData.json");
            CopyIfPresent(realRoot, folder, "keybinds.json");
            return folder;
        }

        private static void CopyIfPresent(string fromFolder, string toFolder, string fileName)
        {
            string source = Path.Combine(fromFolder, fileName);
            if (File.Exists(source) && new FileInfo(source).Length > 0)
                File.Copy(source, Path.Combine(toFolder, fileName), true);
        }
        #endregion
#endif

        #region Build
#if UNITY_EDITOR
        // After the first scene's Awake, so TabletopTavernData and HeroData are loaded, and before any Start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Build()
        {
            if (!DevOverrides.BootIntoActThreeVictory) return;
            if (!string.Equals(Path.GetFullPath(SaveDataHandler.SaveRoot).TrimEnd('\\', '/'), Folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[ActThreeVictoryTestSave] Save root is {SaveDataHandler.SaveRoot}, not the dev folder. Nothing written.");
                return;
            }

            CampaignSaveData run = WriteRun();

            // Only a normal boot passes through the main menu; an open Map or TavernBattle scene boots straight in.
            if (SceneHandler.Instance.EditorOverride == SceneHandler.EditorOverrides.None)
                SceneHandler.Instance.OnGameStateChanged += ContinueToMap;

            Race enemyRace = TabletopTavernData.Instance.GetRaceFromUnitName(run.enemyArmy[0].UnitName);
            Debug.LogWarning($"[ActThreeVictoryTestSave] Act III final won on {DIFFICULTY}: hero {run.heroID}, beat {enemyRace}. Saves in {Folder}");
        }
#endif

        // Replaces the current run in SaveDataHandler.SaveRoot with the Act III final just won.
        public static CampaignSaveData WriteRun()
        {
            PlayerSaveData player = SaveDataHandler.LoadPlayerSaveData();
            player.customBattle = false;
            SaveDataHandler.SavePlayerSaveData(player);

            CampaignSaveData run = CreateRun(player.lastHeroID);
            SaveDataHandler.SaveCampaign(run);
            SaveDataHandler.SaveCampaignSnapshot(run);
            return run;
        }

        private static CampaignSaveData CreateRun(int lastHeroID)
        {
            TabletopTavernData data = TabletopTavernData.Instance;
            int heroID = HeroData.GetHeroByID(lastHeroID).HeroID;
            Race heroRace = HeroData.GetRaceFromHero(heroID);
            System.Random random = new(SEED);

            // An act 3 army of the hero's faction plus one elite (tier 4) squad, built the way the game builds one, battered by the final.
            SquadToLoad[] recruits = ArmyCreator.GenerateEnemyArmy(3, BATTLES_FOUGHT_IN_ACT, SEED,
                false, data.GetSquadsWithTiersFromRace(heroRace), false, true, false, eliteGuard: true);
            SquadToLoad[] playerArmy = new SquadToLoad[13];
            for (int i = 0; i < playerArmy.Length; i++) playerArmy[i].UnitIndex = -1;
            var kills = new List<SquadKillsStored>();
            var losses = new List<SquadLossesStored>();
            for (int i = 0; i < recruits.Length && i < 10; i++)
            {
                SquadToLoad squad = recruits[i];
                squad.UnitIndex = i;
                HeroBonusManager.ApplyHeroBaseUnitCount(ref squad, heroID);
                float healthLeft = MIN_HEALTH_LEFT + (float)random.NextDouble() * (1f - MIN_HEALTH_LEFT);
                squad.SquadCurrentHealth = Mathf.Max(squad.HitPointsPerUnit, (int)(squad.SquadMaxHealth * healthLeft));
                playerArmy[i] = squad;
                kills.Add(new SquadKillsStored { SquadGUID = squad.UniqueID, Kills = random.Next(5, 60) });
                losses.Add(new SquadLossesStored { SquadGUID = squad.UniqueID, Losses = squad.maxUnitCount - squad.SquadCurrentHealth / squad.HitPointsPerUnit });
            }

            // The Act III final the game would have generated for this seed, every squad destroyed.
            Race enemyRace = data.GenerateRaceForMap(3, SEED, heroRace);
            SquadToLoad[] enemyArmy = ArmyCreator.GenerateEnemyArmy(3,
                BATTLES_FOUGHT_IN_ACT + DifficultyRules.BattlesFoughtBonus(DIFFICULTY, 3), SEED + 1, true,
                data.GetSquadsWithTiersFromRace(enemyRace), DifficultyRules.HarderFinalBattle(DIFFICULTY),
                DifficultyRules.EnemyPrestigeEligible(DIFFICULTY), DifficultyRules.EnemyPrestigeEnhanced(DIFFICULTY),
                spellsExtraSquad: DifficultyRules.SpellsExtraSquad(DIFFICULTY));
            for (int i = 0; i < enemyArmy.Length; i++)
            {
                enemyArmy[i].UnitIndex = i;
                enemyArmy[i].SquadCurrentHealth = 0;
            }

            CampaignSaveData run = new(SEED, heroID, GOLD, playerArmy, DIFFICULTY, GearID.None, Guid.NewGuid());
            run.bookNumber = 3;
            // Unseeded, so each new test run gets a different set of gear.
            run.Gear = GearData.GetRandomGear(GEAR_COUNT, new List<GearID>(), Environment.TickCount, run.bookNumber);
            run.BattlesFought = BATTLES_FOUGHT_IN_ACT;
            // One node per layer up to the final, which OverrideSelectedNodeBeforeBattle had already recorded.
            run.nodePath = new List<int> { 0 };
            for (int layer = 1; layer < MAP_LAYERS - 1; layer++) run.nodePath.Add(layer * NODES_PER_LAYER + 1);
            run.nodePath.Add(FINAL_NODE_INDEX);
            run.activeMapLayer = MAP_LAYERS - 2;
            run.SetSelectedNodeIndex(FINAL_NODE_INDEX);
            run.selectedNodeType = Map.NodeType.Horde;
            run.enemyArmy = enemyArmy;
            run.battleCompleted = true;
            run.playerWonBattle = true;
            run.archerUsedInBattle = true;
            run.townData = new TownSaveData { townInteractionStatus = TownInteractionStatus.Sacked };
            run.SquadKillsStore = kills;
            run.HistoricalKillStore = new List<SquadKillsStored>(kills);
            run.SquadLossesStore = losses;
            // Every post-run achievement condition is false, because Steam unlocks are live in the Editor.
            // Win Demo has no condition and unlocks whenever the win is banked.
            run.RunStats = new RunStats
            {
                chaptersCompleted = 2 * MAP_LAYERS + MAP_LAYERS - 1,
                goldEarned = 320,
                unitsPrestiged = 4,
                unitsRecruited = 8,
                gearAquired = 3,
                enemiesSlain = 1400,
                shopPurchases = 5,
                maxArmyModels = 260,
                ransomsOffered = 4,
                ransomsChosen = 1,
                heldDuplicateUnit = true,
                consumableUsed = true,
                pauseUsed = true,
                gainedUnitOutsideRaiseDead = true,
                spellsCast = new List<SpellCastStored>(),
            };
            return run;
        }
        #endregion

        #region Boot
        // The first main menu of the session goes straight on to the map, as the Continue button would.
        private static void ContinueToMap(GameStateEnum state)
        {
            if (state != GameStateEnum.MainMenu) return;
            SceneHandler.Instance.OnGameStateChanged -= ContinueToMap;
            _ = SwitchWhenMenuIsReady();
        }

        private static async Task SwitchWhenMenuIsReady()
        {
            while (Application.isPlaying && !SceneHandler.Instance.SceneSetUpComplete) await Task.Yield();
            if (!Application.isPlaying) return;
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Map);
        }
        #endregion
    }
}
#endif
