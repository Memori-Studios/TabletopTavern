using System;
using System.Collections.Generic;
using Memori.SaveData;
using TJ.Spells;

namespace TJ
{
    public enum OrdealGroup { EnemyArmies, Warband, RoadAndCoin, Magic, DoubleEdged }

    public sealed class OrdealDefinition
    {
        public OrdealId Id;
        public OrdealGroup Group;
        // An IconLibrary key. Placeholders from the existing set until the Ordeal icons are drawn.
        public string IconName;
        // Changes what map nodes show, so taking it redraws the act's nodes.
        public bool RedrawsMap;
        // Items whose effect would cancel this Ordeal; they stay owned but stop working while it is active.
        public GearID[] CounteredGear = Array.Empty<GearID>();
        public ConsumableEnum[] CounteredConsumables = Array.Empty<ConsumableEnum>();
        // The faction passive this Ordeal switches off, if any.
        public Race? CounteredFactionPassive;
        // False when the card would do nothing for this run, so the draw never offers it.
        public Func<CampaignSaveData, bool> IsEligible = _ => true;
        // False for the March's laws, a Twist-only id, and cards that act on nodes the March no longer has.
        public bool Offered = true;

        public string NameKey => $"Ordeal{Id}";
        public string DescriptionKey => $"Ordeal{Id}Desc";
        // A Twist lasts one battle, so it has its own shorter text; the card text speaks of every battle.
        public string TwistDescriptionKey => $"Twist{Id}Desc";
        public string GroupKey => $"OrdealGroup{Group}";
    }

    /// <summary>Every Ordeal card, and the seeded pick-three draw at the start of each endless act.</summary>
    public static class OrdealRegistry
    {
        public const int OFFER_COUNT = 3;
        public const float RENOWN_BONUS_PER_ORDEAL = 0.10f;
        // Keeps the draw on its own stream, clear of the map, event and army seeds.
        private const int ORDEAL_SEED_OFFSET = 611953;
        private const int ORDEAL_SEED_PER_ACT = 7919;

        #region Definitions
        private static readonly Dictionary<OrdealId, OrdealDefinition> Definitions = Build();

        private static Dictionary<OrdealId, OrdealDefinition> Build()
        {
            var list = new List<OrdealDefinition>
            {
                new() { Id = OrdealId.VeteranHosts, Group = OrdealGroup.EnemyArmies, IconName = "Prestige" },
                new() { Id = OrdealId.QuickenedHosts, Group = OrdealGroup.EnemyArmies, IconName = "Speed" },
                new() { Id = OrdealId.EliteGuard, Group = OrdealGroup.EnemyArmies, IconName = "Horde" },
                new() { Id = OrdealId.UnbrokenRanks, Group = OrdealGroup.EnemyArmies, IconName = "Leadership" },
                new()
                {
                    Id = OrdealId.Ambush, Group = OrdealGroup.EnemyArmies, IconName = "Skirmish",
                    CounteredConsumables = new[] { ConsumableEnum.FateshineElixir, ConsumableEnum.Rewind },
                },

                // The March has no towns, shops or recruits, and a squad lost there cannot be replaced.
                new() { Id = OrdealId.PressGanged, Group = OrdealGroup.Warband, IconName = "Recruit", CounteredFactionPassive = Race.IronLegion, Offered = false },
                new() { Id = OrdealId.GreenRecruits, Group = OrdealGroup.Warband, IconName = "UnitHealth", Offered = false },
                new() { Id = OrdealId.ShortQuivers, Group = OrdealGroup.Warband, IconName = "Ammunition", IsEligible = HasShooter },
                new() { Id = OrdealId.BluntedCharge, Group = OrdealGroup.Warband, IconName = "ChargeBonus" },
                new() { Id = OrdealId.Deserters, Group = OrdealGroup.Warband, IconName = "NegativeReputation", IsEligible = run => ArmySize(run) > 2, Offered = false },
                new() { Id = OrdealId.RustedArms, Group = OrdealGroup.Warband, IconName = "GearDrop", IsEligible = HasWorkingGear },
                new() { Id = OrdealId.ForcedMarch, Group = OrdealGroup.Warband, IconName = "Speed" },
                new() { Id = OrdealId.NoRetreat, Group = OrdealGroup.Warband, IconName = "Skirmish" },
                new() { Id = OrdealId.Dread, Group = OrdealGroup.Warband, IconName = "Leadership" },

                new() { Id = OrdealId.TheTithe, Group = OrdealGroup.RoadAndCoin, IconName = "Gold" },
                new()
                {
                    Id = OrdealId.IronCoffers, Group = OrdealGroup.RoadAndCoin, IconName = "Shop", Offered = false,
                    CounteredGear = new[] { GearID.PrivateeringPapers, GearID.CookieAndFowlCard },
                },
                // March wins pay no ransom, so the card has nothing left to take.
                new() { Id = OrdealId.NoQuarter, Group = OrdealGroup.RoadAndCoin, IconName = "engagement", Offered = false },
                new() { Id = OrdealId.Embargo, Group = OrdealGroup.RoadAndCoin, IconName = "Treasure", Offered = false },
                new() { Id = OrdealId.ScorchedEarth, Group = OrdealGroup.RoadAndCoin, IconName = "Town", RedrawsMap = true, Offered = false },
                new() { Id = OrdealId.FogOnTheRoad, Group = OrdealGroup.RoadAndCoin, IconName = "Unknown", RedrawsMap = true },
                new()
                {
                    Id = OrdealId.LongNight, Group = OrdealGroup.RoadAndCoin, IconName = "Event", RedrawsMap = true,
                    CounteredGear = new[] { GearID.BraceletoftheSunGoddess }, CounteredFactionPassive = Race.TaelindorForest,
                },
                new() { Id = OrdealId.BloodPrice, Group = OrdealGroup.RoadAndCoin, IconName = "PrestigeUnit", Offered = false },
                new() { Id = OrdealId.BlindMarch, Group = OrdealGroup.RoadAndCoin, IconName = "Unknown" },
                new() { Id = OrdealId.TwinBanners, Group = OrdealGroup.RoadAndCoin, IconName = "Event", RedrawsMap = true },

                new() { Id = OrdealId.ArcaneDrought, Group = OrdealGroup.Magic, IconName = "Mana" },
                new() { Id = OrdealId.SealedPage, Group = OrdealGroup.Magic, IconName = "SpellStatCharges", IsEligible = _ => SpellLoadout.GetUnlockedSlotCount() >= 2 },

                new() { Id = OrdealId.MercenaryContract, Group = OrdealGroup.DoubleEdged, IconName = "Warband" },
                new() { Id = OrdealId.BloodPact, Group = OrdealGroup.DoubleEdged, IconName = "AttackDamage" },
                new() { Id = OrdealId.DeathWish, Group = OrdealGroup.DoubleEdged, IconName = "AttackDamage" },
                new() { Id = OrdealId.LastStand, Group = OrdealGroup.DoubleEdged, IconName = "Leadership" },
                new() { Id = OrdealId.BurnTheWagons, Group = OrdealGroup.DoubleEdged, IconName = "Warband", IsEligible = HasReserve },

                new() { Id = OrdealId.Outnumbered, Group = OrdealGroup.EnemyArmies, IconName = "Horde" },

                new()
                {
                    Id = OrdealId.NoRespite, Group = OrdealGroup.Warband, IconName = "UnitHealth", Offered = false,
                    CounteredGear = new[] { GearID.PumpkinPie, GearID.ChugJug },
                    CounteredConsumables = new[] { ConsumableEnum.MinorHealth, ConsumableEnum.MajorHealth },
                },
                new()
                {
                    Id = OrdealId.NoReinforcements, Group = OrdealGroup.Warband, IconName = "Recruit", Offered = false,
                    CounteredGear = new[] { GearID.RiverTrout, GearID.JailersKey },
                    CounteredConsumables = new[] { ConsumableEnum.Duplicate, ConsumableEnum.NewUnit, ConsumableEnum.LambSauce },
                },
                new() { Id = OrdealId.FoulWeather, Group = OrdealGroup.RoadAndCoin, IconName = "Event", Offered = false },
            };

            var byId = new Dictionary<OrdealId, OrdealDefinition>();
            foreach (OrdealDefinition definition in list) byId.Add(definition.Id, definition);
            return byId;
        }

        public static IEnumerable<OrdealDefinition> All => Definitions.Values;

        public static OrdealDefinition Get(OrdealId id) => Definitions.TryGetValue(id, out OrdealDefinition definition) ? definition : null;

        // Held by every run on the March without being taken, so they never count as a card.
        public static readonly OrdealId[] MarchLaws = { OrdealId.NoRespite, OrdealId.NoReinforcements };
        public static bool IsMarchLaw(OrdealId id) => id == OrdealId.NoRespite || id == OrdealId.NoReinforcements;
        #endregion

        #region Eligibility
        private static int ArmySize(CampaignSaveData run)
        {
            int size = 0;
            if (run.playerArmy == null) return 0;
            foreach (SquadToLoad squad in run.playerArmy)
                if (squad.UnitIndex != -1) size++;
            return size;
        }

        private static bool HasShooter(CampaignSaveData run)
        {
            if (run.playerArmy == null) return false;
            foreach (SquadToLoad squad in run.playerArmy)
            {
                if (squad.UnitIndex == -1) continue;
                if (TabletopTavernConstants.Shoots(TabletopTavernData.Instance.GetSquadStats(squad.UnitName).unitType)) return true;
            }
            return false;
        }

        private static bool HasWorkingGear(CampaignSaveData run)
        {
            if (run.Gear == null) return false;
            foreach (GearID gear in run.Gear)
                if (run.brokenGear == null || !run.brokenGear.Contains(gear)) return true;
            return false;
        }

        // Burn the Wagons gives up the reserve, so it needs one to give up.
        private static bool HasReserve(CampaignSaveData run) => run.playerArmy != null && run.playerArmy.Length > DEPLOYED_SLOTS;
        private const int DEPLOYED_SLOTS = 10;
        #endregion

        #region Draw
        /// <summary>The cards offered for this run's next pick. Same run and pick, same cards.</summary>
        public static List<OrdealId> DrawOffer(CampaignSaveData run)
        {
            // The first pick draws on the stream the first endless act used to.
            int stream = TabletopTavernConstants.FINAL_STORY_ACT + 1 + run.marchOrdealPicks;
            return DrawOffer(run.seed, stream, run.ordeals, id => Get(id).IsEligible(run));
        }

        /// <summary>
        /// Up to OFFER_COUNT offered cards the run does not hold and that pass isEligible, seeded by run seed and
        /// pick. A partial Fisher-Yates over the pool in enum order, so the pool order never depends on dictionary
        /// order. A Double-Edged card takes the last place when the draw holds none, so there is always one to weigh.
        /// </summary>
        public static List<OrdealId> DrawOffer(int seed, int stream, ICollection<OrdealId> owned, Func<OrdealId, bool> isEligible)
        {
            var pool = new List<OrdealId>();
            foreach (OrdealId id in Enum.GetValues(typeof(OrdealId)))
            {
                if (id == OrdealId.None || !Definitions.TryGetValue(id, out OrdealDefinition definition) || !definition.Offered) continue;
                if (owned != null && owned.Contains(id)) continue;
                if (!isEligible(id)) continue;
                pool.Add(id);
            }

            var random = new Random(MathUtilities.MixSeed(seed + stream * ORDEAL_SEED_PER_ACT + ORDEAL_SEED_OFFSET));
            int count = Math.Min(OFFER_COUNT, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
            }

            if (count > 0 && !pool.GetRange(0, count).Exists(IsDoubleEdged))
            {
                var doubleEdged = new List<int>();
                for (int i = count; i < pool.Count; i++)
                    if (IsDoubleEdged(pool[i])) doubleEdged.Add(i);
                if (doubleEdged.Count > 0)
                    pool[count - 1] = pool[doubleEdged[random.Next(doubleEdged.Count)]];
            }
            return pool.GetRange(0, count);
        }

        private static bool IsDoubleEdged(OrdealId id) => Definitions[id].Group == OrdealGroup.DoubleEdged;
        #endregion

        public static float RenownMultiplier(int ordealCount) => 1f + RENOWN_BONUS_PER_ORDEAL * Math.Max(0, ordealCount);

        #region Card rules
        public const int TITHE_GOLD_PER_TURN = 3;
        public const int MERCENARY_CONTRACT_GOLD = 25;
        public const int MERCENARY_CONTRACT_GOLD_PER_TURN = 10;
        public const int IRON_COFFERS_PRICE_RISE = 5;
        public const int PRESS_GANGED_RECRUIT_RISE = 2;
        public const float GREEN_RECRUITS_HEALTH = 0.75f;
        public const float FOG_HIDDEN_NODE_CHANCE = 0.60f;
        public const float ARCANE_DROUGHT_MANA = 0.75f;
        public const int UNBROKEN_RANKS_LEADERSHIP = 10;
        public const float SHORT_QUIVERS_AMMUNITION = 0.75f;
        public const int OUTNUMBERED_SQUADS = 2;
        // Last Stand's price: every host brings one more squad.
        public const int LAST_STAND_ENEMY_SQUADS = 1;

        // Veteran Hosts runs the enhanced prestige table; on a level that already does, it doubles the chance instead.
        public static bool EnemyPrestigeEnhanced(CampaignSaveData run) =>
            DifficultyRules.EnemyPrestigeEnhanced(run.difficultyLevel) || run.HasOrdeal(OrdealId.VeteranHosts);
        public static bool DoubleEnemyPrestigeChance(CampaignSaveData run) =>
            DifficultyRules.EnemyPrestigeEnhanced(run.difficultyLevel) && run.HasOrdeal(OrdealId.VeteranHosts);

        // Quickened Hosts: one more battles-fought step on top of the difficulty's.
        public static int BattlesFoughtStep(CampaignSaveData run) => run.HasOrdeal(OrdealId.QuickenedHosts) ? 1 : 0;

        // Gold lost at the end of every turn: The Tithe and Mercenary Contract stack.
        public static int GoldLostPerTurn(CampaignSaveData run) => GoldLostPerTurn(run.ordeals);

        public static int GoldLostPerTurn(IEnumerable<OrdealId> held)
        {
            if (held == null) return 0;
            int loss = 0;
            foreach (OrdealId id in held)
            {
                if (id == OrdealId.TheTithe) loss += TITHE_GOLD_PER_TURN;
                else if (id == OrdealId.MercenaryContract) loss += MERCENARY_CONTRACT_GOLD_PER_TURN;
            }
            return loss;
        }
        #endregion

        #region Countered items
        /// <summary>The held Ordeal that switches this gear off, or None.</summary>
        public static OrdealId CounteringOrdeal(IEnumerable<OrdealId> held, GearID gear)
        {
            if (held == null) return OrdealId.None;
            foreach (OrdealId id in held)
                if (Get(id) is OrdealDefinition definition && Array.IndexOf(definition.CounteredGear, gear) >= 0) return id;
            return OrdealId.None;
        }

        /// <summary>The held Ordeal that switches this consumable off, or None.</summary>
        public static OrdealId CounteringOrdeal(IEnumerable<OrdealId> held, ConsumableEnum consumable)
        {
            if (held == null) return OrdealId.None;
            foreach (OrdealId id in held)
                if (Get(id) is OrdealDefinition definition && Array.IndexOf(definition.CounteredConsumables, consumable) >= 0) return id;
            return OrdealId.None;
        }

        /// <summary>The held Ordeal that switches this faction's passive off, or None.</summary>
        public static OrdealId CounteringOrdeal(IEnumerable<OrdealId> held, Race factionPassive)
        {
            if (held == null) return OrdealId.None;
            foreach (OrdealId id in held)
                if (Get(id) is OrdealDefinition definition && definition.CounteredFactionPassive == factionPassive) return id;
            return OrdealId.None;
        }

        /// <summary>The tooltip footer for an owned thing an Ordeal switched off, in the warning colour.</summary>
        public static string InactiveNote(OrdealId countering)
        {
            string ordealName = Memori.Localization.LocalizationManager.Instance.GetText(Get(countering).NameKey);
            string note = string.Format(Memori.Localization.LocalizationManager.Instance.GetText("OrdealItemInactive"), ordealName);
            return $"<color={ColorData.Error}>{note}</color>";
        }

        /// <summary>The tooltip footer for gear Rusted Arms broke.</summary>
        public static string BrokenNote()
        {
            return $"<color={ColorData.Error}>{Memori.Localization.LocalizationManager.Instance.GetText("OrdealGearBroken")}</color>";
        }
        #endregion
    }
}
