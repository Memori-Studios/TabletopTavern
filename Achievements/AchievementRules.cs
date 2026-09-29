using System.Collections.Generic;
using Memori.Steamworks;
using TJ.Map;

namespace TJ.Achievements
{
    /// <summary>One deployed player squad as the battle achievements see it.</summary>
    public struct DeployedSquad
    {
        public string UniqueID;
        public UnitName UnitName;
        public UnitType Type;
        public UnitSize Size;
        public UnitRarity Rarity;
        public Race Race;
        public int Prestige;
        public bool Terrifying;
        public bool Shielded;
        public int Kills;
    }

    /// <summary>A fought campaign battle, reduced to what the achievement checks read.</summary>
    public class BattleOutcome
    {
        public bool PlayerWon;
        public bool FinalBattle;
        public Race HeroRace;
        // Race.Special when the enemy army holds no faction units.
        public Race EnemyRace = Race.Special;
        // Models on the field when the battle began; -1 when the battle report was missing.
        public int PlayerModelsAtStart = -1;
        public int EnemyModelsAtStart = -1;
        public int SpellKills;
        public List<DeployedSquad> Deployed = new();
        public List<string> StartingSquadIds = new();
    }

    /// <summary>One squad in the army, deployed or in reserve, for the checks that watch the whole army.</summary>
    public struct ArmySquad
    {
        public UnitType Type;
        public UnitRarity Rarity;
        public Race Race;
        public UnitAttribute PrestigeTrait;
    }

    /// <summary>The rules behind the Spell Update achievements. No scene objects, so tests can feed them directly.</summary>
    public static class AchievementRules
    {
        #region Thresholds
        public const int OUTNUMBERED_RATIO = 2;
        public const int BATTLE_MAGE_KILLS = 50;
        public const int ARTILLERY_KILLS = 100;
        public const int STORM_CALLER_KILLS = 100;
        public const int CIRCLE_OF_MAGI_MAGES = 3;
        public const int PANTHEON_MIN_SQUADS = 3;
        public const int GOLDEN_HOST_SQUADS = 10;
        public const int UNLIKELY_ALLIES_EACH = 2;
        public const int LEGENDS_ASSEMBLED_UNITS = 3;
        public const int MASTER_OF_ARMS_TRAITS = 5;
        public const int GRAND_ALLIANCE_FACTIONS = 8;
        public const int DRILL_SERGEANT_TRAININGS = 3;
        public const int ALL_IN_GOLD = 50;
        public const int OVERFLOWING_COFFERS_GOLD = 100;
        public const int MARCH_ON_ACT = 4;
        public const int BEYOND_THE_MAPS_EDGE_ACT = 5;
        public const int GOLD_PRESTIGE = 2;
        #endregion

        #region Unit groups
        private static readonly HashSet<UnitName> Goblins = new() { UnitName.GoblinRabble, UnitName.GoblinScrapShooters, UnitName.StonegulletEnforcers };
        private static readonly HashSet<UnitName> Kobolds = new() { UnitName.KoboldBrawlers, UnitName.ScalebowKobolds };
        private static readonly HashSet<UnitName> ShieldSisterUnits = new() { UnitName.Shieldmaidens, UnitName.Valkyries };
        private static readonly NodeType[] GrandTourNodes = { NodeType.Shop, NodeType.Town, NodeType.Event, NodeType.Games, NodeType.Campfire };
        private static readonly Weather[] HarshWeathers = { Weather.Rain, Weather.Fog, Weather.Snow };
        #endregion

        // Keyed by HeroDataSO.HeroID, the stable hero key.
        private static readonly Dictionary<int, AchievementId> HeroVictories = new()
        {
            { 1, AchievementId.LandsReclaimed },
            { 2, AchievementId.RangersRoad },
            { 3, AchievementId.LongLiveTheKing },
            { 4, AchievementId.ThirstSlaked },
            { 5, AchievementId.IronSkullIronWill },
            { 6, AchievementId.EyeOfTheStorm },
            { 7, AchievementId.Supernova },
            { 8, AchievementId.BloomOfNytherial },
            { 9, AchievementId.Unbound },
            { 10, AchievementId.CrimsonCrown },
            { 11, AchievementId.TheUnifier },
            { 12, AchievementId.Shogunate },
            { 13, AchievementId.GrudgeSettled },
            { 14, AchievementId.PowderAndGlory },
            { 15, AchievementId.KoboldKingdom },
            { 16, AchievementId.Primeval },
        };

        public static bool TryGetHeroVictory(int heroID, out AchievementId id) => HeroVictories.TryGetValue(heroID, out id);

        #region Battle
        public static List<AchievementId> ForBattle(BattleOutcome battle)
        {
            List<AchievementId> earned = new();
            List<DeployedSquad> deployed = battle.Deployed;

            // Kill feats count whether the battle is won or lost.
            if (battle.SpellKills >= STORM_CALLER_KILLS) earned.Add(AchievementId.StormCaller);
            int artilleryKills = 0;
            bool battleMage = false;
            foreach (DeployedSquad squad in deployed)
            {
                if (squad.Type == UnitType.Mage && squad.Kills >= BATTLE_MAGE_KILLS) battleMage = true;
                if (squad.Type == UnitType.Artillery) artilleryKills += squad.Kills;
            }
            if (battleMage) earned.Add(AchievementId.BattleMage);
            if (artilleryKills >= ARTILLERY_KILLS) earned.Add(AchievementId.ThunderAndPowder);

            if (!battle.PlayerWon || deployed.Count == 0) return earned;

            if (battle.PlayerModelsAtStart > 0 && battle.EnemyModelsAtStart >= battle.PlayerModelsAtStart * OUTNUMBERED_RATIO)
                earned.Add(AchievementId.Outnumbered);

            if (Count(deployed, s => s.Type == UnitType.Mage) >= CIRCLE_OF_MAGI_MAGES) earned.Add(AchievementId.CircleOfMagi);
            if (battle.EnemyRace != Race.Special && All(deployed, s => s.Race == battle.EnemyRace)) earned.Add(AchievementId.KnowThyEnemy);
            if (All(deployed, s => s.Size == UnitSize.Monstrous || s.Size == UnitSize.SingleUnit)) earned.Add(AchievementId.HereBeMonsters);
            if (All(deployed, s => s.Terrifying)) earned.Add(AchievementId.DreadLegion);
            if (All(deployed, s => s.Shielded)) earned.Add(AchievementId.WallOfShields);
            if (IsCombinedArms(deployed)) earned.Add(AchievementId.CombinedArms);

            if (All(deployed, s => s.Race == Race.Gruntkin || s.Race == Race.DeepstoneHold)
                && Count(deployed, s => s.Race == Race.Gruntkin) >= UNLIKELY_ALLIES_EACH
                && Count(deployed, s => s.Race == Race.DeepstoneHold) >= UNLIKELY_ALLIES_EACH)
                earned.Add(AchievementId.UnlikelyAllies);

            if (deployed.Count >= PANTHEON_MIN_SQUADS && All(deployed, s => s.Rarity == UnitRarity.Rare || s.Rarity == UnitRarity.Legendary))
                earned.Add(AchievementId.Pantheon);

            if (deployed.Count >= GOLDEN_HOST_SQUADS && All(deployed, s => s.Prestige >= GOLD_PRESTIGE)) earned.Add(AchievementId.GoldenHost);

            if (!battle.FinalBattle) return earned;

            if (All(deployed, s => s.Race != battle.HeroRace)) earned.Add(AchievementId.HiredSwords);
            if (All(deployed, s => Goblins.Contains(s.UnitName))) earned.Add(AchievementId.GoblinHorde);
            if (All(deployed, s => Kobolds.Contains(s.UnitName))) earned.Add(AchievementId.KoboldSwarm);
            if (All(deployed, s => ShieldSisterUnits.Contains(s.UnitName))) earned.Add(AchievementId.ShieldSisters);

            HashSet<string> starting = new(battle.StartingSquadIds ?? new List<string>());
            foreach (DeployedSquad squad in deployed)
            {
                if (squad.Prestige >= GOLD_PRESTIGE && starting.Contains(squad.UniqueID))
                {
                    earned.Add(AchievementId.FromLevyToLegend);
                    break;
                }
            }
            return earned;
        }

        private static bool IsCombinedArms(List<DeployedSquad> deployed) =>
            Count(deployed, s => s.Type == UnitType.Melee && s.Size == UnitSize.Infantry) > 0
            && Count(deployed, s => s.Type == UnitType.Ranged) > 0
            && Count(deployed, s => s.Size == UnitSize.Cavalry) > 0
            && Count(deployed, s => s.Type == UnitType.Artillery) > 0
            && Count(deployed, s => s.Size == UnitSize.Monstrous || s.Size == UnitSize.SingleUnit) > 0
            && Count(deployed, s => s.Type == UnitType.Mage) > 0;
        #endregion

        #region Army and run
        public static List<AchievementId> ForArmy(List<ArmySquad> army)
        {
            List<AchievementId> earned = new();
            if (Count(army, s => s.Rarity == UnitRarity.Legendary) >= LEGENDS_ASSEMBLED_UNITS) earned.Add(AchievementId.LegendsAssembled);

            HashSet<UnitAttribute> traits = new();
            HashSet<Race> races = new();
            foreach (ArmySquad squad in army)
            {
                if (squad.PrestigeTrait != UnitAttribute.None) traits.Add(squad.PrestigeTrait);
                if (squad.Race != Race.Special) races.Add(squad.Race);
            }
            if (traits.Count >= MASTER_OF_ARMS_TRAITS) earned.Add(AchievementId.MasterOfArms);
            if (races.Count >= GRAND_ALLIANCE_FACTIONS) earned.Add(AchievementId.GrandAlliance);
            return earned;
        }

        // Null means the run started before starting squads were recorded, so it cannot be proven.
        public static bool StartingArmyIntact(List<string> startingSquadIds, ICollection<string> armySquadIds)
        {
            if (startingSquadIds == null || startingSquadIds.Count == 0) return false;
            foreach (string id in startingSquadIds)
                if (!armySquadIds.Contains(id)) return false;
            return true;
        }

        public static bool GrandTourComplete(ICollection<NodeType> visited)
        {
            if (visited == null) return false;
            foreach (NodeType node in GrandTourNodes)
                if (!visited.Contains(node)) return false;
            return true;
        }

        public static bool WonInEveryWeather(ICollection<Weather> weathers)
        {
            if (weathers == null) return false;
            foreach (Weather weather in HarshWeathers)
                if (!weathers.Contains(weather)) return false;
            return true;
        }

        public static bool IsHarshWeather(Weather weather) => System.Array.IndexOf(HarshWeathers, weather) >= 0;

        // A merge deletes some copies; any run mark they carried moves to the copy that survives.
        public static void InheritMark(List<string> marked, string survivorId, IEnumerable<string> removedIds)
        {
            if (marked == null || string.IsNullOrEmpty(survivorId)) return;
            bool carried = false;
            foreach (string removed in removedIds)
                if (marked.Remove(removed)) carried = true;
            if (carried && !marked.Contains(survivorId)) marked.Add(survivorId);
        }
        #endregion

        #region Helpers
        private static bool All<T>(List<T> items, System.Predicate<T> test)
        {
            if (items.Count == 0) return false;
            foreach (T item in items)
                if (!test(item)) return false;
            return true;
        }

        private static int Count<T>(List<T> items, System.Predicate<T> test)
        {
            int count = 0;
            foreach (T item in items)
                if (test(item)) count++;
            return count;
        }
        #endregion
    }
}
