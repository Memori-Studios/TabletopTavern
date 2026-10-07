using System;
using System.Collections.Generic;
using System.IO;
using Memori.Localization;
using Memori.SaveData;
using Memori.Scenes;
using Memori.Utilities;
using TJ.Event;
using TJ.Map;
using TJ.Spells;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Path = System.IO.Path;

namespace TJ.DevTools
{
    /// <summary>
    /// Every tool and browser tab on the Dev Tools page. Add a tool by adding an entry to Tools().
    /// A tool calls the same methods the game calls and answers with what it did, or why it did not.
    /// </summary>
    public static class DevToolCatalog
    {
        const string StateFolder = "DevStates";
        const string CampaignFile = "campaignSaveData.json";
        const string SnapshotFile = "campaignSaveDataSnapshot.json";
        const string Caption = "#8C9AA2";

        static readonly Race[] Factions =
        {
            Race.IronLegion, Race.Gruntkin, Race.RavenHost, Race.TaelindorForest,
            Race.SanguineCourt, Race.SakuraDynasty, Race.DeepstoneHold, Race.DrakosaurBrood,
        };
        static readonly float[] HealthSteps = { 1f, 0.75f, 0.5f, 0.25f };
        static readonly string[] PrestigeNames = { "Prestige I", "Prestige II", "Prestige III" };

        static int recruitPrestige;
        static int recruitHealth;
        static int heroIndex;
        static int levelIndex;
        static bool lodForced;
        static float shadowDistanceBefore;

        #region Scope
        static CampaignManager Campaign => CampaignManager.InstanceIfExists;
        static CampaignSaveManager Saves => Campaign.CampaignSaveManager;
        static CampaignSaveData Run => Saves.SaveData;
        static MapSceneManager Road => Campaign.MapSceneUIManager.MapSceneManager;
        static BattleManager Field => BattleManager.InstanceIfExists;
        static bool NodeOpen => Campaign.MapSceneUIManager.LayerNodeSelected != -1;

        public static string Title(DevScope scope) => scope switch
        {
            DevScope.Map => "Campaign map",
            DevScope.Battle => "Battle",
            DevScope.Editor => "Editor switches (apply on the next Play)",
            _ => "Anywhere",
        };

        // CurrentGameState flips about a second before the new scene's managers exist, so both are checked.
        public static bool Available(DevScope scope, out string why)
        {
            why = null;
            SceneHandler scenes = SceneHandler.Instance;
            switch (scope)
            {
                case DevScope.Map:
                    if (scenes.CurrentGameState == GameStateEnum.Map && scenes.SceneSetUpComplete && Campaign != null && Saves != null && Run != null) return true;
                    why = "Only on the campaign map.";
                    return false;
                case DevScope.Battle:
                    if (scenes.CurrentGameState == GameStateEnum.Battle && scenes.SceneSetUpComplete && Field != null) return true;
                    why = "Only in a battle.";
                    return false;
                case DevScope.Editor:
                    if (Application.isEditor) return true;
                    why = "Only in the Editor.";
                    return false;
                default:
                    return true;
            }
        }

        public static string Status()
        {
            var parts = new List<string> { Pair("State", SceneHandler.Instance.CurrentGameState.ToString()) };
            if (Available(DevScope.Map, out _))
            {
                parts.Add(Pair("Act", $"{Run.bookNumber}, chapter {Run.activeMapLayer + 2}{(Run.InMarch ? " (March)" : "")}"));
                parts.Add(Pair("Gold", Run.goldAmount.ToString()));
                parts.Add(Pair("Army", $"{Saves.GetArmySize()}/{ArmyCapacity()}"));
                parts.Add(Pair("Level", DifficultyRules.Normalize(Run.difficultyLevel).ToString()));
                parts.Add(Pair("Seed", Run.seed.ToString()));
            }
            else if (Available(DevScope.Battle, out _))
            {
                parts.Add(Pair("Phase", Field.GamePhase.ToString()));
                if (Field.SpellManager != null) parts.Add(Pair("Mana", $"{Field.SpellManager.ManaRemaining}/{Field.SpellManager.ManaMax}"));
                parts.Add(Pair("Selected", Field.UnitSelectionManager.SelectedSquadIds.Count.ToString()));
            }
            parts.Add(Pair("Build", $"v{Application.version}{Defines()}"));
            return string.Join("     ", parts);
        }

        static string Pair(string caption, string value) => $"<size=80%><color={Caption}>{caption.ToUpperInvariant()}</color></size> {value}";

        static string Defines()
        {
            string defines = "";
#if SPELLS
            defines += " SPELLS";
#endif
#if TESTING
            defines += " TESTING";
#endif
#if DEMO
            defines += " DEMO";
#endif
#if RELEASE
            defines += " RELEASE";
#endif
            return defines;
        }
        #endregion

        #region Tools
        public static List<DevTool> Tools()
        {
            var tools = new List<DevTool>
            {
                new(DevScope.Map, "Treasury", "Gold",
                    new DevControl("+100", () => Gold(100)),
                    new DevControl("+500", () => Gold(500)),
                    new DevControl("+2,000", () => Gold(2000)),
                    new DevControl("-500", () => Gold(-500), DevButtonStyle.Danger)),

                new(DevScope.Map, "Army", "Health of every squad",
                    new DevControl("Heal all", () => SetArmyHealth(1f)),
                    new DevControl("Set to 50%", () => SetArmyHealth(0.5f), DevButtonStyle.Danger)),
                new(DevScope.Map, "Army", "Prestige a random squad",
                    new DevControl("Prestige", PrestigeRandom)),

                new(DevScope.Map, "Road", "Finish the open node",
                    new DevControl("Skip chapter", SkipChapter)),
                new(DevScope.Map, "Road", "Go to, on the next layer",
                    new DevControl("Town", () => GoTo("Town", NodeType.Town)),
                    new DevControl("Campfire", () => GoTo("Campfire", NodeType.Campfire)),
                    new DevControl("Games", () => GoTo("Games", NodeType.Games)),
                    new DevControl("Event", () => GoTo("Event", NodeType.Event))),
                new(DevScope.Map, "Road", "Go to (more)",
                    new DevControl("Shop", () => GoTo("Shop", NodeType.Shop)),
                    new DevControl("Treasure", () => GoTo("Treasure", NodeType.Treasure)),
                    new DevControl("Battle", () => GoTo("battle", NodeType.Skirmish, NodeType.Horde, NodeType.Warband))),
                new(DevScope.Map, "Road", "Map",
                    new DevControl("Reveal all nodes", RevealMap),
                    new DevControl("Reload map", () => ReloadMap("Map reloaded."))),
                new(DevScope.Map, "Road", "Jump ahead",
                    new DevControl("Last chapter", JumpToLastChapter, DevButtonStyle.Danger),
                    new DevControl("Finish act", FinishAct, DevButtonStyle.Danger)),
                new(DevScope.Map, "Road", "Enter the March (Steam: March On)",
                    new DevControl("March", EnterMarch, DevButtonStyle.Danger)),

                new(DevScope.Map, "Save states", "State A", new DevControl("Save", () => SaveState("A")), new DevControl("Load", () => LoadState("A"), DevButtonStyle.Danger)),
                new(DevScope.Map, "Save states", "State B", new DevControl("Save", () => SaveState("B")), new DevControl("Load", () => LoadState("B"), DevButtonStyle.Danger)),
                new(DevScope.Map, "Save states", "State C", new DevControl("Save", () => SaveState("C")), new DevControl("Load", () => LoadState("C"), DevButtonStyle.Danger)),
                new(DevScope.Map, "Save states", "This run",
                    new DevControl("Copy seed", () => Copy("Seed", Run.seed.ToString())),
                    new DevControl("Copy run id", () => Copy("Run id", Run.RunId))),

                new(DevScope.Battle, "Result", "End the battle. A win is saved.",
                    new DevControl("Win", () => EndBattle(true), DevButtonStyle.Danger),
                    new DevControl("Lose", () => EndBattle(false), DevButtonStyle.Danger)),
                new(DevScope.Battle, "Squads", "Selected squads",
                    new DevControl("Heal", () => HitSelected(true)),
                    new DevControl("Kill", () => HitSelected(false), DevButtonStyle.Danger)),
                new(DevScope.Battle, "Squads", "Every enemy squad",
                    new DevControl("Hold", () => EnemyGuard(true)),
                    new DevControl("Release", () => EnemyGuard(false)),
                    new DevControl("Kill", KillEnemies, DevButtonStyle.Danger)),
                new(DevScope.Battle, "Spells", "Mana",
                    new DevControl("Refill", RefillMana)),
                new(DevScope.Battle, "Spells", "Mages cast with no cooldown (custom battle)",
                    new DevControl(() => OnOff(SpellTestMode.MageNoCooldown), () => Flip(() => SpellTestMode.MageNoCooldown, v => SpellTestMode.MageNoCooldown = v, "TESTING"))),
                new(DevScope.Battle, "Spells", "Mages have unlimited charges (custom battle)",
                    new DevControl(() => OnOff(SpellTestMode.MageUnlimitedCharges), () => Flip(() => SpellTestMode.MageUnlimitedCharges, v => SpellTestMode.MageUnlimitedCharges = v, "TESTING"))),

                new(DevScope.Anywhere, "Progress", "Renown",
                    new DevControl("+100", () => Renown(100)),
                    new DevControl("+1,000", () => Renown(1000))),
                new(DevScope.Anywhere, "Progress", "Hero completion (Steam)",
                    new DevControl(() => HeroName(Heroes()[heroIndex % Heroes().Length]), () => Cycle(ref heroIndex, Heroes().Length)),
                    new DevControl(() => DifficultyRules.Ladder[levelIndex].ToString(), () => Cycle(ref levelIndex, DifficultyRules.Ladder.Length)),
                    new DevControl("Record", RecordHeroCompletion, DevButtonStyle.Danger)),
                new(DevScope.Anywhere, "Progress", "Collection (Steam on Find all)",
                    new DevControl("Find all", () => SetCollection(true), DevButtonStyle.Danger),
                    new DevControl("Clear", () => SetCollection(false), DevButtonStyle.Danger)),
                new(DevScope.Anywhere, "Progress", "Tavern themes",
                    new DevControl("Unlock all", UnlockThemes)),
                new(DevScope.Anywhere, "Progress", "Tutorial and tips",
                    new DevControl("Reset", ResetTutorial)),
                new(DevScope.Anywhere, "Tools", "LOD override and long shadows",
                    new DevControl(() => OnOff(lodForced), ToggleLod)),
                new(DevScope.Anywhere, "Tools", "Save and log folder",
                    new DevControl("Open", OpenSaveFolder)),
            };
#if UNITY_EDITOR
            tools.Add(new DevTool(DevScope.Editor, "Boot", "Boot into",
                new DevControl(() => DevOverrides.EditorOverride.ToString(), CycleBoot)));
            tools.Add(new DevTool(DevScope.Editor, "Boot", "Boot loads the custom battle save",
                new DevControl(() => OnOff(DevOverrides.LoadCustomBattleSaveData), () => { DevOverrides.LoadCustomBattleSaveData = !DevOverrides.LoadCustomBattleSaveData; return DevResult.Silent; })));
            tools.Add(new DevTool(DevScope.Editor, "Boot", "Boot loads the campaign battle",
                new DevControl(() => OnOff(DevOverrides.LoadCampaignBattle), () => { DevOverrides.LoadCampaignBattle = !DevOverrides.LoadCampaignBattle; return DevResult.Silent; })));
            tools.Add(new DevTool(DevScope.Editor, "Boot", "Boot into the Act III victory (Hard)",
                new DevControl(() => OnOff(DevOverrides.BootIntoActThreeVictory), () => { DevOverrides.BootIntoActThreeVictory = !DevOverrides.BootIntoActThreeVictory; return DevResult.Silent; })));
            tools.Add(new DevTool(DevScope.Editor, "Battle", "Enemies start in guard mode",
                new DevControl(() => OnOff(DevOverrides.LockEnemiesToGuardMode), () => { DevOverrides.LockEnemiesToGuardMode = !DevOverrides.LockEnemiesToGuardMode; return DevResult.Silent; })));
            tools.Add(new DevTool(DevScope.Editor, "Battle", "Spell Test Mode",
                new DevControl(() => OnOff(SpellTestMode.Enabled), () => Flip(() => SpellTestMode.Enabled, v => SpellTestMode.Enabled = v, "TESTING"))));
#endif
            return tools;
        }
        #endregion

        #region Map tools
        static void SaveRun()
        {
            Saves.SaveCampaign();
            Saves.SaveCampaignSnapshot();
        }

        static int ArmyCapacity() => Mathf.Max(Run.playerArmy.Length, 10 + Saves.MaxReserveSlots);

        static DevResult Gold(int amount)
        {
            // Taking never drops the treasury below zero.
            if (amount < 0) amount = -Mathf.Min(Run.goldAmount, -amount);
            if (amount == 0) return DevResult.Refused("No gold to take.");
            Campaign.GoldManager.ModifyGold(amount, "Dev Tools");
            SaveRun();
            return DevResult.Done($"Gold {amount:+#;-#}. Now {Run.goldAmount}.");
        }

        static DevResult SetArmyHealth(float fraction)
        {
            SquadToLoad[] army = Run.playerArmy;
            int squads = 0;
            for (int i = 0; i < army.Length; i++)
            {
                // A squad at zero is dead until the layer ends, as in the game's own heals.
                if (army[i].UnitIndex == -1 || army[i].isEmptySquad || army[i].SquadCurrentHealth <= 0) continue;
                army[i].SquadCurrentHealth = Mathf.Max(1, Mathf.RoundToInt(army[i].SquadMaxHealth * fraction));
                squads++;
            }
            if (squads == 0) return DevResult.Refused("No living squads.");
            Campaign.MapSceneUIManager.HUDPanel.ArmyHealthChanged();
            SaveRun();
            return DevResult.Done($"Set {squads} squads to {Mathf.RoundToInt(fraction * 100f)}% health.");
        }

        static DevResult PrestigeRandom()
        {
            int before = Run.RunStats.unitsPrestiged;
            string squad = Saves.PrestigeRandomUnit();
            if (Run.RunStats.unitsPrestiged == before) return DevResult.Refused("No squad can prestige.");
            SaveRun();
            Campaign.MapSceneUIManager.TryDrainPendingPrestigeChoices();
            return DevResult.Done($"Prestiged {squad}. A Prestige III squad waits for its trait pick behind this page.");
        }

        static DevResult SkipChapter()
        {
            // CompleteLayer reads the selected node, which only exists while a node panel is open.
            if (!NodeOpen) return DevResult.Refused("Open a node first. This finishes the node you are on.");
            Campaign.MapSceneUIManager.CompleteLayerAction();
            return DevResult.Done("Finished the node.");
        }

        static DevResult GoTo(string label, params NodeType[] types)
        {
            if (NodeOpen) return DevResult.Refused("Finish the open node first.");
            if (!Road.AllowMapInput) return DevResult.Refused("The map is busy. Try again in a moment.");
            int next = Road.GetActiveChapterIndex() + 1;
            if (next < 0 || next >= Road.MapLayers.Count) return DevResult.Refused("There is no next layer.");
            foreach (MapNodeData node in Road.MapLayers[next].LayerNodes)
            {
                if (Array.IndexOf(types, node.type) < 0 || node.mapNodeGameObject == null || !node.mapNodeGameObject.Selectable) continue;
                CloseSettings();
                Road.SelectNode(node.mapNodeGameObject);
                Road.FinishHopping();
                return DevResult.Done($"Arrived at the {label} on layer {next + 1}.");
            }
            return DevResult.Refused($"No {label} you can reach on the next layer.");
        }

        static DevResult RevealMap()
        {
            Road.RevealNodesInNextLayers(-1, Road.MapLayers.Count);
            return DevResult.Done("Revealed every node in this act.");
        }

        static DevResult ReloadMap(string message)
        {
            CloseSettings();
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Map, true);
            return DevResult.Done(message);
        }

        // What CompleteChapter clears, for tools that move the run without playing the node.
        static void ClearNodeState()
        {
            Run.nodeResume = default;
            Run.activeTwists?.Clear();
            Run.twistsStruckHere?.Clear();
            Run.wonBattleThisTurn = false;
            Run.battleCompleted = false;
            Run.SetSelectedNodeIndex(-1);
            Run.nodeGenerated = false;
            Run.Rolls = 0;
            Run.townData = new TownSaveData();
        }

        static DevResult JumpToLastChapter()
        {
            if (NodeOpen) return DevResult.Refused("Finish the open node first.");
            // On the March every layer is a numbered battle, so skipping layers would renumber the road.
            if (Run.InMarch) return DevResult.Refused("Not on the March: its battles are numbered by layer.");
            int target = Road.MapLayers.Count - 2;
            if (Run.activeMapLayer >= target) return DevResult.Refused("Already at the last chapter.");

            // The reload needs a walked node on every layer, or it opens layer 0 again.
            MapNodeData? previous = null;
            if (Run.activeMapLayer >= 0 && Run.nodePath.Count > 0)
                foreach (MapNodeData node in Road.MapLayers[Run.activeMapLayer].LayerNodes)
                    if (node.index == Run.nodePath[Run.nodePath.Count - 1]) previous = node;
            for (int layer = Run.activeMapLayer + 1; layer <= target; layer++)
            {
                List<MapNodeData> nodes = Road.MapLayers[layer].LayerNodes;
                MapNodeData step = nodes[0];
                if (previous.HasValue)
                    foreach (MapNodeData node in nodes)
                        if (previous.Value.connectedNodeIndexes.Contains(node.index)) { step = node; break; }
                Run.nodePath.Add(step.index);
                previous = step;
            }
            Run.activeMapLayer = target;
            ClearNodeState();
            SaveRun();
            return ReloadMap($"Jumped to chapter {target + 2}, the act's last.");
        }

        static DevResult FinishAct()
        {
            if (NodeOpen) return DevResult.Refused("Finish the open node first.");
            if (Run.InMarch) return DevResult.Refused("The March has no acts to finish.");
            if (Run.bookNumber >= TabletopTavernConstants.FINAL_STORY_ACT) return DevResult.Refused("Act III ends the run. Win its final battle, or use Enter the March.");
            ClearNodeState();
            Saves.CompleteBook();
            return ReloadMap($"Finished the act. Now in act {Run.bookNumber}.");
        }

        static DevResult EnterMarch()
        {
            if (NodeOpen) return DevResult.Refused("Finish the open node first.");
            if (Run.InMarch) return DevResult.Refused("Already on the March.");
            if (!DifficultyRules.EndlessUnlocked) return DevResult.Refused("The March needs a SPELLS build.");
            ClearNodeState();
            Run.bookNumber = TabletopTavernConstants.FINAL_STORY_ACT;
            Run.marchBattlesWon = 0;
            Run.marchOrdealPicks = 0;
            // The run ends as a win, as it would after a real act III, without the account unlocks a real win records.
            Run.victoryBanked = true;
            Saves.CompleteBook();
            return ReloadMap("Marched on. The army was healed and the March's laws apply.");
        }

        static string StatePath(string slot, string file) => Path.Combine(SaveDataHandler.SaveRoot, StateFolder, slot, file);

        static DevResult SaveState(string slot)
        {
            if (NodeOpen) return DevResult.Refused("Finish the open node first, so the state loads cleanly.");
            Saves.SaveCampaign();
            Directory.CreateDirectory(Path.Combine(SaveDataHandler.SaveRoot, StateFolder, slot));
            foreach (string file in new[] { CampaignFile, SnapshotFile })
            {
                string source = Path.Combine(SaveDataHandler.SaveRoot, file);
                if (File.Exists(source)) File.Copy(source, StatePath(slot, file), true);
            }
            return DevResult.Done($"Saved state {slot}: act {Run.bookNumber}, chapter {Run.activeMapLayer + 2}, {Run.goldAmount} gold.");
        }

        static DevResult LoadState(string slot)
        {
            if (!File.Exists(StatePath(slot, CampaignFile))) return DevResult.Refused($"State {slot} is empty.");
            foreach (string file in new[] { CampaignFile, SnapshotFile })
            {
                string source = StatePath(slot, file);
                if (File.Exists(source)) File.Copy(source, Path.Combine(SaveDataHandler.SaveRoot, file), true);
            }
            return ReloadMap($"Loaded state {slot}. Renown and other account progress are not rolled back.");
        }

        static DevResult Copy(string label, string value)
        {
            GUIUtility.systemCopyBuffer = value;
            return DevResult.Done($"{label} copied: {value}");
        }
        #endregion

        #region Battle tools
        static DevResult EndBattle(bool playerWon)
        {
            GamePhase phase = Field.GamePhase;
            // A win in deployment would be saved against an enemy army that never spawned.
            if (playerWon && phase != GamePhase.Battle) return DevResult.Refused("Start the battle first.");
            if (phase != GamePhase.Battle && phase != GamePhase.Deployment) return DevResult.Refused("The battle is not running.");
            // Closed first: closing later would put the paused time scale back over the end screen.
            CloseSettings();
            Field.ForceBattleResult(playerWon);
            return DevResult.Done(playerWon ? "Battle won." : "Battle lost.");
        }

        static DevResult HitSelected(bool heal)
        {
            if (Field.GamePhase != GamePhase.Battle) return DevResult.Refused("Start the battle first.");
            var ids = new List<int>(Field.UnitSelectionManager.SelectedSquadIds);
            if (ids.Count == 0) return DevResult.Refused("Select a squad, then open this page.");
            Field.SquadManager.DevHitSquads(ids, heal);
            return DevResult.Done(heal ? $"Healed {ids.Count} selected squads to full." : $"Killed {ids.Count} selected squads.");
        }

        static DevResult KillEnemies()
        {
            if (Field.GamePhase != GamePhase.Battle) return DevResult.Refused("Start the battle first.");
            List<int> ids = Field.SquadManager.DevEnemySquadIds();
            if (ids.Count == 0) return DevResult.Refused("No enemy squads on the field.");
            Field.SquadManager.DevHitSquads(ids, false);
            return DevResult.Done($"Killed {ids.Count} enemy squads. The battle ends on its own.");
        }

        static DevResult EnemyGuard(bool hold)
        {
            int squads = Field.SquadManager.DevSetEnemyGuardMode(hold);
            if (squads == 0) return DevResult.Refused("No enemy squads on the field yet.");
            return DevResult.Done(hold ? $"{squads} enemy squads hold. Their general's own orders can still move them." : $"{squads} enemy squads released.");
        }

        static DevResult RefillMana()
        {
            SpellManager spells = Field.SpellManager;
            if (spells == null || spells.ManaMax == 0) return DevResult.Refused("This battle has no mana pool.");
            spells.RefillMana();
            return DevResult.Done($"Mana refilled to {spells.ManaMax}.");
        }
        #endregion

        #region Anywhere tools
        static Hero[] Heroes() => HeroData.Heroes;

        static string HeroName(Hero hero) => Text(hero.HeroName, $"Hero {hero.HeroID}");

        static DevResult Renown(int amount)
        {
            SaveDataHandler.AddRenown(amount);
            return DevResult.Done($"Renown +{amount}. Total {SaveDataHandler.GetRenown()}.");
        }

        static DevResult RecordHeroCompletion()
        {
            Hero hero = Heroes()[heroIndex % Heroes().Length];
            TT_Difficulty level = DifficultyRules.Ladder[levelIndex];
            SaveDataHandler.RecordHeroCompletionForTesting(hero.HeroID, level);
            SaveDataHandler.RefreshTavernThemeUnlocks();
            return DevResult.Done($"Recorded {HeroName(hero)} on {level}.");
        }

        static List<UnitName> AllUnits()
        {
            var units = new List<UnitName>();
            foreach (Race race in Factions) units.AddRange(TabletopTavernData.Instance.GetUnitsOfRace(race));
            return units;
        }

        static DevResult SetCollection(bool found)
        {
            SaveDataHandler.DevSetCollection(found, AllUnits(), GearData.GetGearIDs(), ConsumableData.GetAllConsumableEnums());
            return DevResult.Done(found ? "Every Collection entry is found. Steam unlocks follow when the Collection opens." : "Collection cleared.");
        }

        static DevResult UnlockThemes()
        {
            foreach (Race race in Factions) SaveDataHandler.UnlockTavernTheme(race);
            return DevResult.Done("Every tavern theme is unlocked.");
        }

        static DevResult ResetTutorial()
        {
            SettingsManager.Instance.ResetTutorial();
            return DevResult.Done("Tutorial and tips reset.");
        }

        static DevResult ToggleLod()
        {
            lodForced = !lodForced;
            foreach (LODGroup lod in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                lod.ForceLOD(lodForced ? 1 : -1);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                if (lodForced)
                {
                    shadowDistanceBefore = pipeline.shadowDistance;
                    pipeline.shadowDistance = 1000f;
                    // The pipeline asset is shared; in the Editor a value left on it would outlive Play.
                    Application.quitting -= RestoreShadowDistance;
                    Application.quitting += RestoreShadowDistance;
                }
                else RestoreShadowDistance();
            }
            return DevResult.Done(lodForced ? "LOD override on for the loaded scenes. Shadows reach 1000." : "LOD override off.");
        }

        static void RestoreShadowDistance()
        {
            Application.quitting -= RestoreShadowDistance;
            if (shadowDistanceBefore > 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                pipeline.shadowDistance = shadowDistanceBefore;
            lodForced = false;
        }

        static DevResult OpenSaveFolder()
        {
            OpenLogsExtension.OpenLogs();
            return DevResult.Done("Opened the save and log folder.");
        }

#if UNITY_EDITOR
        static DevResult CycleBoot()
        {
            int count = Enum.GetValues(typeof(SceneHandler.EditorOverrides)).Length;
            DevOverrides.EditorOverride = (SceneHandler.EditorOverrides)(((int)DevOverrides.EditorOverride + 1) % count);
            return DevResult.Silent;
        }
#endif
        #endregion

        #region Browser
        public static List<DevBrowserTab> Tabs() => new()
        {
            new DevBrowserTab
            {
                Name = "Units", Scope = DevScope.Map, UnitTiles = true, SearchHint = "Search units: name, faction, rarity or type. Enter gives the first.",
                Entries = UnitEntries, Pick = GiveUnit,
                Options = new[]
                {
                    new DevControl(() => "Arrives at " + PrestigeNames[recruitPrestige], () => Cycle(ref recruitPrestige, PrestigeNames.Length)),
                    new DevControl(() => $"Health {Mathf.RoundToInt(HealthSteps[recruitHealth] * 100f)}%", () => Cycle(ref recruitHealth, HealthSteps.Length)),
                },
            },
            new DevBrowserTab
            {
                Name = "Gear", Scope = DevScope.Map, SearchHint = "Search gear. Click to give; click a held item to take it away.",
                Entries = GearEntries, Pick = ToggleGear,
            },
            new DevBrowserTab
            {
                Name = "Consumables", Scope = DevScope.Map, SearchHint = "Search consumables. Click to give.",
                Entries = ConsumableEntries, Pick = GiveConsumable,
                Options = new[] { new DevControl("Clear held", ClearConsumables, DevButtonStyle.Danger) },
            },
            new DevBrowserTab
            {
                Name = "Ordeals", Scope = DevScope.Map, SearchHint = "Search Ordeals. Click to grant; click a held card to drop it.",
                Entries = OrdealEntries, Pick = ToggleOrdeal,
                Options = new[] { new DevControl("Reload map", () => ReloadMap("Map reloaded.")) },
            },
            new DevBrowserTab
            {
                Name = "Events", Scope = DevScope.Map, SearchHint = "Search events by key. Click to open it at the next Event node.",
                Entries = EventEntries, Pick = ForceEvent,
            },
        };

        static List<DevEntry> UnitEntries()
        {
            var entries = new List<DevEntry>();
            TabletopTavernData data = TabletopTavernData.Instance;
            foreach (Race race in Factions)
            {
                string faction = Spaced(race.ToString());
                // GetUnitsOfRace already leaves out mages in a build without SPELLS, and no faction holds the garrison gate.
                foreach (UnitName unit in data.GetUnitsOfRace(race))
                {
                    SquadStats stats = data.GetSquadStats(unit);
                    string name = Text(unit.ToString(), Spaced(unit.ToString()));
                    entries.Add(new DevEntry
                    {
                        Id = unit.ToString(),
                        Name = name,
                        Group = faction,
                        GroupColour = ColorData.GetRaceDisplayColor(race),
                        Search = $"{name} {Spaced(unit.ToString())} {unit} {faction} {stats.RarityTier} {stats.unitType}".ToLowerInvariant(),
                        Icon = data.GetUnitIcon(unit),
                        TypeIcon = data.GetSquadTypeIcon(unit),
                        Rarity = (Color)ColorData.GetRarityTierColor(stats.RarityTier),
                    });
                }
            }
            return entries;
        }

        static DevResult GiveUnit(DevEntry entry)
        {
            if (Run.InMarch) return DevResult.Refused("The March takes no new squads.");
            if (!Saves.CheckForRoomToRecruit()) return DevResult.Refused($"Army is full ({Saves.GetArmySize()}/{ArmyCapacity()}). Nothing given.");
            var unit = (UnitName)Enum.Parse(typeof(UnitName), entry.Id);
            Saves.RecruitSquad(TabletopTavernData.Instance.GetSquadStats(unit), HealthSteps[recruitHealth], false, false, recruitPrestige);
            SaveRun();
            string note = "";
            if (recruitPrestige == PrestigeNames.Length - 1)
            {
                Campaign.MapSceneUIManager.TryDrainPendingPrestigeChoices();
                note = " Its trait pick waits behind this page.";
            }
            string prestige = recruitPrestige > 0 ? $" at {PrestigeNames[recruitPrestige]}" : "";
            return DevResult.Done($"Gave {entry.Name}{prestige}. Army {Saves.GetArmySize()}/{ArmyCapacity()}.{note}");
        }

        static List<DevEntry> GearEntries()
        {
            var entries = new List<DevEntry>();
            foreach (GearID id in GearData.GetGearIDs())
            {
                Gear gear = GearData.GetGear(id);
                string name = Text(id + "Name", gear.GearName);
                GearID held = id;
                entries.Add(new DevEntry
                {
                    Id = id.ToString(),
                    Name = name,
                    Group = gear.GearRarity.ToString(),
                    GroupColour = (Color)ColorData.GetGearRarityColor(gear.GearRarity),
                    Search = $"{name} {id} {gear.GearRarity}".ToLowerInvariant(),
                    Icon = SpriteData.GetSprite(gear.GearName),
                    Rarity = (Color)ColorData.GetGearRarityColor(gear.GearRarity),
                    Held = () => Available(DevScope.Map, out _) && Run.Gear.Contains(held),
                });
            }
            return entries;
        }

        static DevResult ToggleGear(DevEntry entry)
        {
            var id = (GearID)Enum.Parse(typeof(GearID), entry.Id);
            if (Run.Gear.Contains(id))
                return Saves.DevRemoveGear(id) ? DevResult.Done($"Took {entry.Name} away.") : DevResult.Refused($"Could not take {entry.Name}.");
            if (Run.InMarch) return DevResult.Refused("The March takes no new gear.");
            if (!Saves.CanAquireGear()) return DevResult.Refused($"Gear is full ({Run.Gear.Count}/{Saves.MaxGear}). Click a held item to take it away.");
            Saves.AquireGear(id);
            SaveRun();
            return DevResult.Done($"Gave {entry.Name}. Gear {Run.Gear.Count}/{Saves.MaxGear}.");
        }

        static List<DevEntry> ConsumableEntries()
        {
            var entries = new List<DevEntry>();
            foreach (ConsumableEnum id in ConsumableData.GetAllConsumableEnums())
            {
                Color rarity = (Color)ColorData.GetRarityTierColor((UnitRarity)(int)ConsumableData.GetConsumable(id).ConsumableRarity);
                string name = Text(id + "Name", Spaced(id.ToString()));
                entries.Add(new DevEntry
                {
                    Id = id.ToString(),
                    Name = name,
                    Search = $"{name} {id}".ToLowerInvariant(),
                    Icon = SpriteData.GetSprite(id.ToString()),
                    Rarity = rarity,
                });
            }
            return entries;
        }

        static DevResult GiveConsumable(DevEntry entry)
        {
            if (!Saves.HasRoomForConsumable()) return DevResult.Refused($"Consumables are full ({Run.consumables.Count}/{Saves.ConsumableCapacity}). Use Clear held.");
            Saves.AquireConsumable((ConsumableEnum)Enum.Parse(typeof(ConsumableEnum), entry.Id));
            SaveRun();
            return DevResult.Done($"Gave {entry.Name}. Consumables {Run.consumables.Count}/{Saves.ConsumableCapacity}.");
        }

        static DevResult ClearConsumables()
        {
            int count = Run.consumables.Count;
            if (count == 0) return DevResult.Refused("No consumables held.");
            foreach (ConsumableEnum consumable in new List<ConsumableEnum>(Run.consumables)) Saves.RemoveConsumable(consumable);
            SaveRun();
            return DevResult.Done($"Cleared {count} consumables.");
        }

        static List<DevEntry> OrdealEntries()
        {
            var entries = new List<DevEntry>();
            foreach (OrdealDefinition ordeal in OrdealRegistry.All)
            {
                // The March's laws are never held as cards, and unoffered cards exist only as Twists.
                if (!ordeal.Offered || OrdealRegistry.IsMarchLaw(ordeal.Id)) continue;
                string name = Text(ordeal.NameKey, Spaced(ordeal.Id.ToString()));
                OrdealId held = ordeal.Id;
                entries.Add(new DevEntry
                {
                    Id = ordeal.Id.ToString(),
                    Name = name,
                    Search = $"{name} {ordeal.Id}".ToLowerInvariant(),
                    Icon = string.IsNullOrEmpty(ordeal.IconName) ? null : SpriteData.GetSprite(ordeal.IconName),
                    Rarity = new Color(0.89f, 0.41f, 0.37f),
                    Held = () => Available(DevScope.Map, out _) && Run.ordeals != null && Run.ordeals.Contains(held),
                });
            }
            return entries;
        }

        static DevResult ToggleOrdeal(DevEntry entry)
        {
            var id = (OrdealId)Enum.Parse(typeof(OrdealId), entry.Id);
            OrdealDefinition ordeal = OrdealRegistry.Get(id);
            // Cards that change the road, and any card on the March, only show after the nodes are redrawn.
            string redraw = (ordeal != null && ordeal.RedrawsMap) || Run.InMarch ? " Use Reload map to redraw the road." : "";
            if (Run.ordeals != null && Run.ordeals.Contains(id))
                return Saves.DevRemoveOrdeal(id) ? DevResult.Done($"Dropped {entry.Name}. What it already took stays taken.{redraw}") : DevResult.Refused($"Could not drop {entry.Name}.");
            Saves.TakeOrdeal(id);
            return DevResult.Done($"Granted {entry.Name}.{redraw}");
        }

        static List<DevEntry> EventEntries()
        {
            var entries = new List<DevEntry>();
            foreach (EventDefinitionSO definition in EventData.GetAllEvents())
            {
                if (definition == null || string.IsNullOrEmpty(definition.TableKey)) continue;
                entries.Add(new DevEntry
                {
                    Id = definition.TableKey,
                    Name = Spaced(definition.TableKey),
                    Search = $"{Spaced(definition.TableKey)} {definition.TableKey}".ToLowerInvariant(),
                    Rarity = new Color(0.69f, 0.54f, 0.24f),
                });
            }
            return entries;
        }

        static DevResult ForceEvent(DevEntry entry)
        {
            EventPanel.DevForcedEventKey = entry.Id;
            DevResult arrival = GoTo("Event", NodeType.Event);
            return arrival.Ok
                ? DevResult.Done($"Opened {entry.Name}. Choices whose requirements fail stay hidden.")
                : DevResult.Done($"Armed {entry.Name}. The next Event node you enter shows it.");
        }
        #endregion

        #region Helpers
        static void CloseSettings()
        {
            if (SettingsManager.Instance.SettingsPanelOpen) SettingsManager.Instance.CloseSettingsPanel();
        }

        static DevResult Cycle(ref int index, int count)
        {
            index = (index + 1) % count;
            return DevResult.Silent;
        }

        // Some switches only read true in a build with the named define; say so instead of looking broken.
        static DevResult Flip(Func<bool> get, Action<bool> set, string define)
        {
            bool wanted = !get();
            set(wanted);
            return get() == wanted ? DevResult.Silent : DevResult.Refused($"This switch needs a {define} build.");
        }

        static string OnOff(bool on) => on ? "On" : "Off";

        static string Text(string key, string fallback)
        {
            LocalizationManager localization = LocalizationManager.InstanceIfExists;
            if (localization == null || localization.stringTable == null) return fallback;
            string text = localization.GetText(key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }

        // "BlackEagleHalberdiers" reads as "Black Eagle Halberdiers".
        static string Spaced(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) builder.Append(' ');
                builder.Append(name[i]);
            }
            return builder.ToString();
        }
        #endregion
    }
}
