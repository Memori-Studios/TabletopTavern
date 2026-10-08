using System;
using System.Collections.Generic;
using Memori.SaveData;

namespace TJ
{
    /// <summary>
    /// The March: the survival run after the last story act. No healing, no recruits, every node a battle against a
    /// different rogue host, and the score is battles survived. Pure rules only, so the map, the engagement screen,
    /// the tests and the difficulty sim all read the same numbers.
    /// </summary>
    public static class MarchRules
    {
        #region Cadence
        // Every Nth battle is led by a warlord, and beating him brings the next Ordeal.
        public const int WARLORD_EVERY = 5;

        public static bool InMarch(CampaignSaveData run) => run != null && InMarch(run.bookNumber);
        public static bool InMarch(int bookNumber) => TabletopTavernConstants.EndlessActs(bookNumber) > 0;

        /// <summary>The 1-based March battle fought on this map layer. One layer is one battle, so it follows from the wins so far.</summary>
        public static int BattleNumber(CampaignSaveData run, int layer) => run.marchBattlesWon + 1 + (layer - (run.activeMapLayer + 1));

        /// <summary>The battle the player is about to fight, or is fighting.</summary>
        public static int CurrentBattle(CampaignSaveData run) => run.marchBattlesWon + 1;

        public static bool IsWarlordBattle(int battleNumber) => battleNumber > 0 && battleNumber % WARLORD_EVERY == 0;

        public static int WarlordsBeaten(int battlesWon) => battlesWon / WARLORD_EVERY;
        #endregion

        #region Enemy armies
        public const int BASE_SQUADS = 7;
        public const int BASE_TIER_TWO = 4;
        public const int BATTLES_PER_EXTRA_SQUAD = 3;
        // The battle from which one tier 2 squad a battle becomes tier 3.
        public const int TIER_THREE_UPGRADES_FROM = 6;
        // The battle from which one tier 3 squad becomes tier 4, every BATTLES_PER_TIER_FOUR battles.
        public const int TIER_FOUR_FROM = 9;
        public const int BATTLES_PER_TIER_FOUR = 2;
        public const int MAX_TIER_FOUR = 6;
        public const int BATTLES_PER_PRESTIGE_STEP = 2;
        public const float PRESTIGE_CHANCE_PER_STEP = 0.05f;
        // Godking's "armies scale faster" and Quickened Hosts each run the schedule this many battles ahead.
        public const int SCHEDULE_HEAD_START = 2;
        // The battle from which every node carries a second Twist.
        public const int SECOND_TWIST_FROM = 11;

        /// <summary>The battle number the army tables read: the real one, pushed ahead by difficulty and Quickened Hosts.</summary>
        public static int ScheduleBattle(CampaignSaveData run, int battleNumber) =>
            ScheduleBattle(run.difficultyLevel, run.HasOrdeal(OrdealId.QuickenedHosts), battleNumber);

        public static int ScheduleBattle(TT_Difficulty difficulty, bool quickenedHosts, int battleNumber)
        {
            int battle = battleNumber;
            if (DifficultyRules.Applies(19, difficulty)) battle += SCHEDULE_HEAD_START;
            if (quickenedHosts) battle += SCHEDULE_HEAD_START;
            return battle;
        }

        /// <summary>
        /// The host for a March battle: seven squads, one more every three battles up to the deployment cap, better
        /// squads from battle 6. A warlord brings one more tier 4 squad. extraSquads is Outnumbered.
        /// </summary>
        public static TierCount[] ArmyTierCounts(int scheduleBattle, bool warlord, int extraSquads = 0)
        {
            int battle = Math.Max(1, scheduleBattle);
            int cap = TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP;
            int squads = Math.Min(cap, BASE_SQUADS + (battle - 1) / BATTLES_PER_EXTRA_SQUAD + Math.Max(0, extraSquads));

            int tierTwo = BASE_TIER_TWO - Math.Clamp(battle - TIER_THREE_UPGRADES_FROM + 1, 0, BASE_TIER_TWO);
            int tierFour = battle < TIER_FOUR_FROM ? 0 : Math.Min(MAX_TIER_FOUR, (battle - TIER_FOUR_FROM) / BATTLES_PER_TIER_FOUR + 1);
            if (warlord)
            {
                tierFour++;
                if (squads < cap) squads++;
            }
            tierFour = Math.Min(tierFour, squads - tierTwo);
            int tierThree = squads - tierTwo - tierFour;

            var counts = new List<TierCount>();
            if (tierTwo > 0) counts.Add(new TierCount { Tier = 2, Count = tierTwo });
            if (tierThree > 0) counts.Add(new TierCount { Tier = 3, Count = tierThree });
            if (tierFour > 0) counts.Add(new TierCount { Tier = 4, Count = tierFour });
            return counts.ToArray();
        }

        /// <summary>The act 3 prestige profile, raised one step every two battles and held at the endless caps.</summary>
        public static EnemyPrestigeRule PrestigeProfile(int scheduleBattle, bool enhanced)
        {
            EnemyPrestigeRule rule = ArmyGenerationRuleData.ResolveEnemyPrestigeProfile(TabletopTavernConstants.FINAL_STORY_ACT, enhanced);
            int steps = Math.Max(0, scheduleBattle) / BATTLES_PER_PRESTIGE_STEP;
            rule.ChancePerSquad = Math.Min(TabletopTavernConstants.ENDLESS_PRESTIGE_CHANCE_CAP, rule.ChancePerSquad + steps * PRESTIGE_CHANCE_PER_STEP);
            rule.MaxPrestigedSquads = Math.Min(TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP, rule.MaxPrestigedSquads + steps);
            rule.PrestigeTwoChance = Math.Min(TabletopTavernConstants.ENDLESS_PRESTIGE_TWO_CHANCE_CAP, rule.PrestigeTwoChance + steps * PRESTIGE_CHANCE_PER_STEP);
            return rule;
        }
        #endregion

        #region Rogue hosts
        // Each keeps its draw on its own stream, clear of the map, army and Ordeal seeds.
        private const int ROGUE_RACE_SEED_OFFSET = 482711;
        private const int TWIST_SEED_OFFSET = 915377;
        private const int FOUL_WEATHER_SEED_OFFSET = 271829;
        private const int SEED_PER_BOOK = 7919;
        private const int SEED_PER_LAYER = 104729;
        // Added per node to the army seed, which otherwise is the same for every node on a layer.
        public const int ARMY_SEED_PER_NODE = 15485863;

        private static readonly Race[] HostRaces = BuildHostRaces();
        private static Race[] BuildHostRaces()
        {
            var races = new List<Race>();
            foreach (Race race in Enum.GetValues(typeof(Race)))
                if (race != Race.Special) races.Add(race);
            return races.ToArray();
        }

        /// <summary>
        /// The faction holding this node. Every node on a layer gets a different one, and the same run always meets
        /// the same hosts. nodeIndex is the node's index on the map, which runs on within a layer.
        /// </summary>
        public static Race RogueRace(int seed, int bookNumber, int layer, int nodeIndex)
        {
            Race[] order = Shuffled(HostRaces, LayerSeed(seed, bookNumber, layer, ROGUE_RACE_SEED_OFFSET));
            return order[Math.Abs(nodeIndex) % order.Length];
        }

        private static int LayerSeed(int seed, int bookNumber, int layer, int offset) =>
            MathUtilities.MixSeed(seed + bookNumber * SEED_PER_BOOK + layer * SEED_PER_LAYER + offset);

        private static T[] Shuffled<T>(IReadOnlyList<T> source, int seed)
        {
            var random = new Random(seed);
            var order = new T[source.Count];
            for (int i = 0; i < order.Length; i++) order[i] = source[i];
            for (int i = order.Length - 1; i > 0; i--)
            {
                int pick = random.Next(i + 1);
                (order[i], order[pick]) = (order[pick], order[i]);
            }
            return order;
        }
        #endregion

        #region Twists
        // An Ordeal that lasts one battle. Every node shows its Twist before the player picks it.
        public static readonly OrdealId[] TwistPool =
        {
            OrdealId.VeteranHosts, OrdealId.EliteGuard, OrdealId.UnbrokenRanks, OrdealId.Ambush,
            OrdealId.ShortQuivers, OrdealId.BluntedCharge, OrdealId.ArcaneDrought, OrdealId.FoulWeather,
            OrdealId.BloodPact, OrdealId.Outnumbered, OrdealId.ForcedMarch, OrdealId.NoRetreat,
        };
        // A layer holds up to this many nodes, so node k takes Twists k and k + this from the layer's shuffle.
        private const int NODES_PER_LAYER = 3;

        public static int TwistCount(int battleNumber, ICollection<OrdealId> held)
        {
            int count = battleNumber >= SECOND_TWIST_FROM ? 2 : 1;
            if (held != null && held.Contains(OrdealId.TwinBanners)) count++;
            return count;
        }

        /// <summary>
        /// The node's Twists: never a card the run already holds, never the same as another node on the layer.
        /// Foul Weather is left out under Long Night, which already fixes the weather.
        /// </summary>
        public static List<OrdealId> Twists(int seed, int bookNumber, int layer, int nodeIndex, int battleNumber, ICollection<OrdealId> held)
        {
            var pool = new List<OrdealId>();
            foreach (OrdealId id in TwistPool)
            {
                if (held != null && held.Contains(id)) continue;
                if (id == OrdealId.FoulWeather && held != null && held.Contains(OrdealId.LongNight)) continue;
                pool.Add(id);
            }
            var twists = new List<OrdealId>();
            if (pool.Count == 0) return twists;

            OrdealId[] order = Shuffled(pool, LayerSeed(seed, bookNumber, layer, TWIST_SEED_OFFSET));
            int slot = Math.Abs(nodeIndex) % NODES_PER_LAYER;
            int count = Math.Min(TwistCount(battleNumber, held), order.Length);
            for (int i = 0; i < count; i++)
            {
                OrdealId twist = order[(slot + i * NODES_PER_LAYER) % order.Length];
                if (!twists.Contains(twist)) twists.Add(twist);
            }
            return twists;
        }

        public static List<OrdealId> Twists(CampaignSaveData run, int layer, int nodeIndex) =>
            Twists(run.seed, run.bookNumber, layer, nodeIndex, BattleNumber(run, layer), run.ordeals);

        private static readonly Weather[] HarshWeather = { Weather.Snow, Weather.Fog, Weather.Rain };

        /// <summary>Foul Weather's weather for a node.</summary>
        public static Weather FoulWeather(int seed, int bookNumber, int nodeIndex)
        {
            var random = new Random(MathUtilities.MixSeed(seed + bookNumber * SEED_PER_BOOK + nodeIndex + FOUL_WEATHER_SEED_OFFSET));
            return HarshWeather[random.Next(HarshWeather.Length)];
        }
        #endregion

        #region War Chest
        // Gold's one use on the March: pay to strike a Twist from the battle ahead.
        public const int STRIKE_TWIST_BASE_COST = 15;
        public const int STRIKE_TWIST_COST_RISE = 5;

        public static int StrikeTwistCost(int twistsStruck) => STRIKE_TWIST_BASE_COST + Math.Max(0, twistsStruck) * STRIKE_TWIST_COST_RISE;
        #endregion

        #region Score and Renown
        public const int RENOWN_PER_BATTLE = 5;
        public const int RENOWN_PER_WARLORD = 10;

        public static int Renown(int battlesWon) =>
            Math.Max(0, battlesWon) * RENOWN_PER_BATTLE + WarlordsBeaten(Math.Max(0, battlesWon)) * RENOWN_PER_WARLORD;
        #endregion
    }
}
