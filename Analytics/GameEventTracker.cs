using System;
using System.Collections.Generic;
using System.IO;
using Memori.Analytics;
using Memori.SaveData;
using TJ;
using TJ.Spells;
using UnityEngine;

namespace TabletopTavern.Analytics
{
    public enum RunResult { Win, Loss, Abandon }

    // Game-side analytics layer. Owns two things:
    //
    //   1. Bootstrap - picks the backend once, before any scene loads. Editor -> console logger
    //      (or the real server when "Send From Editor" is ticked); RELEASE build -> the studio's
    //      analytics server; DEMO and any other build -> no backend, so nothing is captured.
    //
    //   2. The game's event vocabulary: runStarted, nodeCompleted, battleEnded, runEnded,
    //      endlessEnded and bugReportSubmitted. Every in-run event carries runId, heroId, difficulty
    //      and act so the views can join and slice them.
    public static class GameEventTracker
    {
        #region Server
        private const string Endpoint = "https://analytics.memoristudios.com/v1/events";
        // Must match WRITE_KEY in Tools/AnalyticsServer/.env. It ships in the build, so it only filters noise.
        private const string WriteKey = "8cb3ae857774f16277fc2ff8a3a5423b";
        #endregion

        private const string ConsentPref = "TabletopTavern.Analytics.Consent";

        private static HttpAnalyticsBackend s_http;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            s_http = null;
            // A saved opt-out has to be in place before any backend exists, or it would send once more.
            AnalyticsService.SetConsent(PlayerPrefs.GetInt(ConsentPref, 1) == 1);
#if UNITY_EDITOR
            // Editor: log to console, or send tagged "editor" (kept off the dashboard) when the menu toggle is on.
            if (UnityEditor.EditorPrefs.GetBool(SendFromEditorPref, false))
                InstallHttpBackend("editor", Path.Combine(Application.dataPath, "..", "Temp", "AnalyticsQueue"));
            else
                AnalyticsService.SetBackend(new DebugLogAnalyticsBackend());
#elif RELEASE
            // Real telemetry, RELEASE build only.
            InstallHttpBackend("release", Path.Combine(Application.persistentDataPath, "Analytics"));
#else
            // DEMO (or any non-RELEASE build): analytics off - leaving the backend unset makes
            // every AnalyticsService call a no-op, so nothing is captured.
#endif
        }

        private static void InstallHttpBackend(string buildKind, string queueFolder)
        {
            s_http = new HttpAnalyticsBackend(Endpoint, WriteKey, buildKind, queueFolder);
            AnalyticsService.SetBackend(s_http);
        }

        // Call whenever the player makes or changes their privacy choice. It is saved for later launches.
        public static void SetConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentPref, granted ? 1 : 0);
            PlayerPrefs.Save();
            AnalyticsService.SetConsent(granted);
        }

        // For bug reports: the id this install's events carry, or "None" when this build sends nothing.
        public static string InstallId => s_http == null ? "None" : s_http.InstallId;

        #region Run events
        // The Metabase views and the Difficulty Sim SQL read these props by name; AnalyticsEventTests pins them.

        public static void RunStarted(CampaignSaveData run, AnalyticsRunSetup setup)
        {
            TryRun("runStarted", () =>
            {
                setup ??= new AnalyticsRunSetup();
                PlayerSaveData player = SaveDataHandler.LoadPlayerSaveData();
                Dictionary<string, object> p = RunProps(run, 0);
                p["source"] = setup.Source;
                p["seed"] = run.seed;
                p["army"] = ArmyUnitNames(run.playerArmy);
                p["startingGear"] = run.Gear != null && run.Gear.Count > 0 ? run.Gear[0].ToString() : GearID.None.ToString();
                p["startingGold"] = run.goldAmount;
                p["spells"] = Names(run.selectedSpells);
                p["spellSlots"] = SpellLoadout.GetUnlockedSlotCount();
                p["manaBonus"] = SpellLoadout.GetManaBonus();
                p["renownNodes"] = new List<int>(player.metaprogressionNodesUnlocked ?? new List<int>());
                p["renownBanked"] = player.renown;
                p["priorRunsStarted"] = player.campaignsStarted;
                p["runsWon"] = player.gameCompletions;
                p["maxDifficultyUnlocked"] = (int)DifficultyRules.HighestUnlocked(player.MaxDifficultyOverall);
                // Only the setup screen knows these; a quick restart replays the last setup without it.
                p["armyLocked"] = setup.FromMenu ? (object)setup.ArmyLocked : null;
                p["armyCustomized"] = setup.FromMenu ? (object)setup.ArmyCustomized : null;
                p["armyOptions"] = setup.FromMenu ? setup.ArmyOptions : null;
                p["treasury"] = setup.FromMenu
                    ? new Dictionary<string, int>
                    {
                        { "base", setup.TreasuryBase },
                        { "renownBonus", setup.TreasuryRenownBonus },
                        { "armySpend", setup.ArmySpend },
                        { "gearSpend", setup.GearSpend },
                    }
                    : null;
                AddQualityFlags(p);
                AnalyticsService.Record("runStarted", p);
            });
        }

        // Fired once, when the outcome is decided. A banked act 3 win reports here even if the run marches on.
        public static void RunEnded(CampaignSaveData run, RunResult result, string endReason, int renown = -1)
        {
            TryRun("runEnded", () => AnalyticsService.Record("runEnded", RunEndProps(run, result, endReason, renown)));
        }

        /// <summary>
        /// For every path that closes a run: runEnded while the outcome is still open, endlessEnded when
        /// the run banked its act 3 win and marched on, nothing when a banked run closes at act 3.
        /// </summary>
        public static void RunClosed(CampaignSaveData run, RunResult result, string endReason, int renown = -1)
        {
            if (run == null) return;
            if (!run.victoryBanked)
                RunEnded(run, result, endReason, renown);
            else if (run.bookNumber > TabletopTavernConstants.FINAL_STORY_ACT)
                TryRun("endlessEnded", () => AnalyticsService.Record("endlessEnded", RunEndProps(run, result, endReason, renown)));
        }

        public static void NodeCompleted(CampaignSaveData run, AnalyticsNodeReport node)
        {
            if (node == null) return;
            TryRun("nodeCompleted", () =>
            {
                Dictionary<string, object> p = RunProps(run, node.Layer);
                NodeVisit visit = run.nodeVisit;
                // A run continued from a save made after the pick has no record of what was on offer.
                bool known = visit.recorded && visit.nodeIndex == node.NodeIndex;
                p["nodeIndex"] = node.NodeIndex;
                p["nodeType"] = node.NodeType;
                p["hidden"] = known ? (object)visit.hidden : null;
                p["goldBefore"] = known ? (object)visit.goldOnEntry : null;
                p["offered"] = known ? Offers(visit.offered) : null;
                p["goldAfter"] = node.GoldAfter;
                p["interest"] = node.Interest;
                p["squadsLost"] = node.SquadsLost;
                p["armySize"] = node.ArmySize;
                p["deployedSquads"] = node.DeployedSquads;
                p["armyValue"] = node.ArmyValue;
                p["unitsAlive"] = node.UnitsAlive;
                p["unitsMax"] = node.UnitsMax;
                p["healthPct"] = node.HealthMax > 0 ? Math.Round(100.0 * node.HealthNow / node.HealthMax, 1) : 0.0;
                AnalyticsService.Record("nodeCompleted", p);
            });
        }

        public static void BattleEnded(CampaignSaveData run, AnalyticsBattleReport report)
        {
            if (report == null) return;
            TryRun("battleEnded", () =>
            {
                Dictionary<string, object> p = RunProps(run, run.activeMapLayer + 1);
                p["battlesFought"] = run.BattlesFought;
                p["nodeType"] = BattleNodeType(run.selectedNodeType);
                p["garrison"] = report.Garrison;
                p["mode"] = report.Mode;
                p["result"] = report.Result;
                p["enemyRace"] = report.EnemyRace;
                p["weather"] = run.battleFieldPreset.weather.ToString();
                p["biome"] = report.Garrison ? "Garrison" : run.battleFieldPreset.biome.ToString();
                p["layout"] = report.Layout;
                p["manaMax"] = report.ManaMax >= 0 ? (object)report.ManaMax : null;
                p["manaLeft"] = report.ManaLeft >= 0 ? (object)report.ManaLeft : null;
                p["spellCasts"] = report.SpellCasts;
                p["spellKills"] = report.SpellKills;
                p["armyLossTriggered"] = report.ArmyLossTriggered;
                p["pauseUsed"] = report.PauseUsed;
                p["p"] = Squads(report.Player, true);
                p["e"] = Squads(report.Enemy, false);
                AnalyticsService.Record("battleEnded", p);
            });
        }
        #endregion

        #region Other events
        /// <summary>
        /// A player sent a report from the Report a Bug screen and Discord answered; delivered is false when
        /// Discord refused it. While a campaign save exists its run ids come along, so the report joins to
        /// that run's events. The install id already joins it to everything else this player sent.
        /// </summary>
        public static void BugReportSubmitted(bool delivered, string threadUrl, bool crashAttached, string gameState)
        {
            TryRun("bugReportSubmitted", () =>
            {
                CampaignSaveData run = SaveDataHandler.CampaignSaveExists() ? SaveDataHandler.Load() : null;
                Dictionary<string, object> p = run != null
                    ? RunProps(run, run.activeMapLayer + 1)
                    : new Dictionary<string, object>
                    {
                        { "runId", null },
                        { "heroId", null },
                        { "difficulty", null },
                        { "act", null },
                        { "layer", null },
                    };
                p["delivered"] = delivered;
                p["threadUrl"] = delivered ? threadUrl : null;
                p["crashAttached"] = crashAttached;
                p["gameState"] = gameState;
                p["uptimeSec"] = (int)Time.realtimeSinceStartup;
                AddQualityFlags(p);
                AnalyticsService.Record("bugReportSubmitted", p);
            });
        }
        #endregion

        #region Props
        private static Dictionary<string, object> RunProps(CampaignSaveData run, int layer)
        {
            return new Dictionary<string, object>
            {
                { "runId", run.RunId },
                { "heroId", run.heroID },
                { "difficulty", (int)run.difficultyLevel },
                { "act", run.bookNumber },
                { "layer", layer },
            };
        }

        private static Dictionary<string, object> RunEndProps(CampaignSaveData run, RunResult result, string endReason, int renown)
        {
            RunStats stats = run.RunStats;
            var p = new Dictionary<string, object>
            {
                { "heroId", run.heroID },
                { "difficulty", (int)run.difficultyLevel },
                { "runResult", result.ToString() },
                { "turnNumber", stats.chaptersCompleted },
                { "spellsUsed", CountSpellsUsed(stats.spellsCast) },
                { "runId", run.RunId },
                { "act", run.bookNumber },
                // activeMapLayer is the last finished layer of the act, -1 before the first.
                { "layersCompleted", run.activeMapLayer + 1 },
                { "endReason", endReason },
                { "renown", renown >= 0 ? (object)renown : null },
                { "loadout", Names(run.selectedSpells) },
                { "goldAtEnd", run.goldAmount },
                { "playTimeSec", (int)Math.Round(run.playTimeSeconds) },
                { "goldEarned", stats.goldEarned },
                { "enemiesSlain", stats.enemiesSlain },
                { "unitsPrestiged", stats.unitsPrestiged },
                { "unitsRecruited", stats.unitsRecruited },
                { "gearAcquired", stats.gearAquired },
                { "shopPurchases", stats.shopPurchases },
                { "goldWagered", stats.goldWagered },
                { "maxArmyModels", stats.maxArmyModels },
                { "ransomsOffered", stats.ransomsOffered },
                { "ransomsChosen", stats.ransomsChosen },
                { "consumableUsed", stats.consumableUsed },
                { "pauseUsed", stats.pauseUsed },
                { "gear", Names(run.Gear) },
                { "consumablesHeld", Names(run.consumables) },
                { "army", Army(run.playerArmy) },
            };
            AddQualityFlags(p);
            return p;
        }

        // Modded and dev-tool runs skew the numbers balance reads, so every run start and end says whether it was one.
        private static void AddQualityFlags(Dictionary<string, object> p)
        {
            var mods = ModLoadOrder.LoadedFolderNamesThisSession;
            p["modCount"] = mods == null ? 0 : mods.Count;
            p["devTool"] = SaveDataHandler.IsDevToolUser();
#if SPELLS
            p["spellsBuild"] = true;
#else
            p["spellsBuild"] = false;
#endif
        }

        // Only these node types start a battle; anything else is a save from before the type was recorded.
        private static string BattleNodeType(TJ.Map.NodeType type)
        {
            return type == TJ.Map.NodeType.Skirmish || type == TJ.Map.NodeType.Horde || type == TJ.Map.NodeType.Town
                ? type.ToString()
                : "Unknown";
        }

        // {"Smite": 3, "RallyTheBanners": 2}: every spell cast this run, hotbar and mage casts alike.
        private static Dictionary<string, int> CountSpellsUsed(List<SpellCastStored> spellsCast)
        {
            var counts = new Dictionary<string, int>();
            if (spellsCast == null) return counts;
            foreach (SpellCastStored entry in spellsCast)
            {
                string spell = entry.Spell.ToString();
                counts[spell] = counts.TryGetValue(spell, out int soFar) ? soFar + entry.Casts : entry.Casts;
            }
            return counts;
        }

        private static List<string> Names<T>(IEnumerable<T> items)
        {
            var names = new List<string>();
            if (items == null) return names;
            foreach (T item in items) names.Add(item.ToString());
            return names;
        }

        private static bool IsRealSquad(SquadToLoad squad) => squad.UnitIndex != -1 && !squad.isEmptySquad;

        private static List<string> ArmyUnitNames(SquadToLoad[] army)
        {
            var names = new List<string>();
            if (army == null) return names;
            foreach (SquadToLoad squad in army)
                if (IsRealSquad(squad)) names.Add(squad.UnitName.ToString());
            return names;
        }

        // Whole units, the way the post-battle loss count reads them.
        private static int UnitsAlive(SquadToLoad squad) => squad.HitPointsPerUnit > 0 ? squad.SquadCurrentHealth / squad.HitPointsPerUnit : 0;

        private static List<Dictionary<string, object>> Army(SquadToLoad[] army)
        {
            var squads = new List<Dictionary<string, object>>();
            if (army == null) return squads;
            foreach (SquadToLoad squad in army)
            {
                if (!IsRealSquad(squad)) continue;
                squads.Add(new Dictionary<string, object>
                {
                    { "u", squad.UnitName.ToString() },
                    { "s", squad.UnitIndex },
                    { "pr", squad.UnitPrestige },
                    { "tr", squad.PrestigeTrait.ToString() },
                    { "n", UnitsAlive(squad) },
                    { "max", squad.maxUnitCount },
                });
            }
            return squads;
        }

        private static List<Dictionary<string, object>> Squads(List<AnalyticsSquadResult> results, bool withSlot)
        {
            var squads = new List<Dictionary<string, object>>();
            if (results == null) return squads;
            foreach (AnalyticsSquadResult r in results)
            {
                var squad = new Dictionary<string, object>
                {
                    { "u", r.Unit },
                    { "pr", r.Prestige },
                    { "tr", r.Trait },
                    { "n0", r.UnitsStart },
                    { "n1", r.UnitsEnd },
                    { "k", r.Kills },
                    { "st", r.Status },
                };
                if (withSlot) squad["s"] = r.Slot;
                squads.Add(squad);
            }
            return squads;
        }

        private static List<Dictionary<string, object>> Offers(List<OfferedNode> offered)
        {
            var offers = new List<Dictionary<string, object>>();
            if (offered == null) return offers;
            foreach (OfferedNode node in offered)
            {
                offers.Add(new Dictionary<string, object>
                {
                    { "i", node.index },
                    { "type", node.type.ToString() },
                    { "hidden", node.hidden },
                });
            }
            return offers;
        }

        #endregion

        #region Guards
        // Analytics code runs inside game paths that must finish (a save, a layer, an abandon): a failure costs the event, never the path.
        public static void TryRun(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] {what} was not recorded: {e.Message}");
            }
        }

        public static T TryBuild<T>(string what, Func<T> build) where T : class
        {
            try
            {
                return build();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] {what} was not built: {e.Message}");
                return null;
            }
        }
        #endregion

#if UNITY_EDITOR
        #region Editor toggle
        private const string SendFromEditorPref = "TabletopTavern.Analytics.SendFromEditor";
        private const string SendFromEditorMenu = "Tabletop Tavern/Analytics/Send From Editor";

        [UnityEditor.MenuItem(SendFromEditorMenu)]
        private static void ToggleSendFromEditor()
        {
            bool on = !UnityEditor.EditorPrefs.GetBool(SendFromEditorPref, false);
            UnityEditor.EditorPrefs.SetBool(SendFromEditorPref, on);
            Debug.Log($"[Analytics] Send From Editor is {(on ? "on" : "off")}; takes effect on the next Play.");
        }

        [UnityEditor.MenuItem(SendFromEditorMenu, true)]
        private static bool ToggleSendFromEditorValidate()
        {
            UnityEditor.Menu.SetChecked(SendFromEditorMenu, UnityEditor.EditorPrefs.GetBool(SendFromEditorPref, false));
            return true;
        }
        #endregion
#endif
    }
}
