using System.Collections.Generic;
using Memori.SaveData;
using Memori.Steamworks;
using TabletopTavern.Analytics;
using TJ.Map;
using TJ.Spells;

namespace TJ.Achievements
{
    /// <summary>Reads a fought campaign battle off the saved campaign and the battle report, then unlocks what it earned.</summary>
    public static class BattleAchievements
    {
        // Custom battles and Spell Test Mode are sandboxes, so nothing earned in them counts.
        public static bool BattleCounts => !SpellTestMode.Active && !SaveDataHandler.IsCustomBattle();

        public static void Evaluate(CampaignSaveData save, bool playerWon, List<SquadKillsStored> kills, int spellKills,
            AnalyticsBattleReport report, SquadToLoad[] enemySquads, List<Spell> spellsCast)
        {
            if (!BattleCounts) return;

            // Runs at the tail of the battle-end save, so a failure here must never reach the post-battle flow.
            try
            {
                foreach (AchievementId id in AchievementRules.ForBattle(BuildOutcome(save, playerWon, kills, spellKills, report, enemySquads)))
                    SteamAchievements.Unlock(id);

                RecordLifetime(playerWon, save.battleFieldPreset.weather, spellsCast);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Achievements] Battle achievement check failed: {e}");
            }
        }

        private static BattleOutcome BuildOutcome(CampaignSaveData save, bool playerWon, List<SquadKillsStored> kills, int spellKills,
            AnalyticsBattleReport report, SquadToLoad[] enemySquads)
        {
            TabletopTavernData data = TabletopTavernData.Instance;
            BattleOutcome outcome = new()
            {
                PlayerWon = playerWon,
                FinalBattle = save.selectedNodeType == NodeType.Horde && save.bookNumber == TabletopTavernConstants.FINAL_STORY_ACT,
                HeroRace = HeroData.GetRaceFromHero(save.heroID),
                EnemyRace = FirstFactionRace(enemySquads, data),
                SpellKills = spellKills,
                StartingSquadIds = save.RunStats.startingSquadIds,
            };
            if (report != null)
            {
                outcome.PlayerModelsAtStart = ModelsAtStart(report.Player);
                outcome.EnemyModelsAtStart = ModelsAtStart(report.Enemy);
            }

            for (int i = 0; i < save.playerArmy.Length; i++)
            {
                SquadToLoad squad = save.playerArmy[i];
                if (!SaveDataHandler.FightsInBattle(squad) || string.IsNullOrEmpty(squad.UniqueID)) continue;

                SquadStats stats = data.GetSquadStats(squad.UnitName);
                outcome.Deployed.Add(new DeployedSquad
                {
                    UniqueID = squad.UniqueID,
                    UnitName = squad.UnitName,
                    Type = stats.unitType,
                    Size = stats.unitSize,
                    Rarity = stats.RarityTier,
                    Race = data.GetRaceFromUnitName(squad.UnitName),
                    Prestige = squad.UnitPrestige,
                    Terrifying = squad.PrestigeTrait == UnitAttribute.Terrifying
                        || HeroBonusManager.UnitHasAttribute(squad.UnitName, save.heroID, UnitAttribute.Terrifying),
                    Shielded = HeroBonusManager.UnitHasAttribute(squad.UnitName, save.heroID, UnitAttribute.StandardShields)
                        || HeroBonusManager.UnitHasAttribute(squad.UnitName, save.heroID, UnitAttribute.HeavyShields),
                    Kills = KillsFor(kills, squad.UniqueID),
                });
            }
            return outcome;
        }

        // The gate is a structure, not soldiers, so it never counts toward the enemy's numbers.
        private static int ModelsAtStart(List<AnalyticsSquadResult> squads)
        {
            int models = 0;
            foreach (AnalyticsSquadResult squad in squads)
                if (squad.Unit != nameof(UnitName.Gate)) models += squad.UnitsStart;
            return models;
        }

        // Enemy squads keep UnitIndex -1 from the SquadToLoad constructor, so only the unit count marks a real one.
        public static Race FirstFactionRace(SquadToLoad[] enemySquads, TabletopTavernData data)
        {
            if (enemySquads == null) return Race.Special;
            foreach (SquadToLoad squad in enemySquads)
            {
                if (squad.isEmptySquad || squad.maxUnitCount <= 0) continue;
                Race race = data.GetRaceFromUnitName(squad.UnitName);
                if (race != Race.Special) return race;
            }
            return Race.Special;
        }

        private static int KillsFor(List<SquadKillsStored> kills, string uniqueID)
        {
            if (kills == null) return 0;
            foreach (SquadKillsStored entry in kills)
                if (entry.SquadGUID == uniqueID) return entry.Kills;
            return 0;
        }

        private static void RecordLifetime(bool playerWon, Weather weather, List<Spell> spellsCast)
        {
            PlayerSaveData player = SaveDataHandler.LoadPlayerSaveData();
            player.spellsEverCast ??= new List<Spell>();
            player.weathersWonIn ??= new List<Weather>();
            bool changed = false;

            if (spellsCast != null)
            {
                foreach (Spell spell in spellsCast)
                {
                    if (player.spellsEverCast.Contains(spell)) continue;
                    player.spellsEverCast.Add(spell);
                    changed = true;
                }
            }
            if (playerWon && AchievementRules.IsHarshWeather(weather) && !player.weathersWonIn.Contains(weather))
            {
                player.weathersWonIn.Add(weather);
                changed = true;
            }
            if (changed) SaveDataHandler.SavePlayerSaveData(player);

            if (CastEveryRegisteredSpell(player.spellsEverCast)) SteamAchievements.Unlock(AchievementId.GrandGrimoire);
            if (AchievementRules.WonInEveryWeather(player.weathersWonIn)) SteamAchievements.Unlock(AchievementId.AllWeathers);
        }

        private static bool CastEveryRegisteredSpell(List<Spell> cast)
        {
            IReadOnlyList<SpellData> all = SpellRegistry.All;
            if (all.Count == 0) return false;
            foreach (SpellData spell in all)
                if (!cast.Contains(spell.Spell)) return false;
            return true;
        }
    }
}
