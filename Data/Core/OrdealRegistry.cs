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

        public string NameKey => $"Ordeal{Id}";
        public string DescriptionKey => $"Ordeal{Id}Desc";
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

                new() { Id = OrdealId.PressGanged, Group = OrdealGroup.Warband, IconName = "Recruit", CounteredFactionPassive = Race.IronLegion },
                new() { Id = OrdealId.GreenRecruits, Group = OrdealGroup.Warband, IconName = "UnitHealth" },
                new() { Id = OrdealId.ShortQuivers, Group = OrdealGroup.Warband, IconName = "Ammunition", IsEligible = HasShooter },
                new() { Id = OrdealId.BluntedCharge, Group = OrdealGroup.Warband, IconName = "ChargeBonus" },
                new() { Id = OrdealId.Deserters, Group = OrdealGroup.Warband, IconName = "NegativeReputation", IsEligible = run => ArmySize(run) > 2 },
                new() { Id = OrdealId.RustedArms, Group = OrdealGroup.Warband, IconName = "GearDrop", IsEligible = HasWorkingGear },

                new() { Id = OrdealId.TheTithe, Group = OrdealGroup.RoadAndCoin, IconName = "Gold" },
                new()
                {
                    Id = OrdealId.IronCoffers, Group = OrdealGroup.RoadAndCoin, IconName = "Shop",
                    CounteredGear = new[] { GearID.PrivateeringPapers, GearID.CookieAndFowlCard },
                },
                new() { Id = OrdealId.NoQuarter, Group = OrdealGroup.RoadAndCoin, IconName = "engagement" },
                new() { Id = OrdealId.Embargo, Group = OrdealGroup.RoadAndCoin, IconName = "Treasure" },
                new() { Id = OrdealId.ScorchedEarth, Group = OrdealGroup.RoadAndCoin, IconName = "Town", RedrawsMap = true },
                new() { Id = OrdealId.FogOnTheRoad, Group = OrdealGroup.RoadAndCoin, IconName = "Unknown", RedrawsMap = true },
                new()
                {
                    Id = OrdealId.LongNight, Group = OrdealGroup.RoadAndCoin, IconName = "Event", RedrawsMap = true,
                    CounteredGear = new[] { GearID.BraceletoftheSunGoddess }, CounteredFactionPassive = Race.TaelindorForest,
                },
                new() { Id = OrdealId.BloodPrice, Group = OrdealGroup.RoadAndCoin, IconName = "PrestigeUnit" },

                new() { Id = OrdealId.ArcaneDrought, Group = OrdealGroup.Magic, IconName = "Mana" },
                new() { Id = OrdealId.SealedPage, Group = OrdealGroup.Magic, IconName = "SpellStatCharges", IsEligible = _ => SpellLoadout.GetUnlockedSlotCount() >= 2 },

                new() { Id = OrdealId.MercenaryContract, Group = OrdealGroup.DoubleEdged, IconName = "Warband" },
                new() { Id = OrdealId.BloodPact, Group = OrdealGroup.DoubleEdged, IconName = "AttackDamage" },
            };

            var byId = new Dictionary<OrdealId, OrdealDefinition>();
            foreach (OrdealDefinition definition in list) byId.Add(definition.Id, definition);
            return byId;
        }

        public static IEnumerable<OrdealDefinition> All => Definitions.Values;

        public static OrdealDefinition Get(OrdealId id) => Definitions.TryGetValue(id, out OrdealDefinition definition) ? definition : null;
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
        #endregion

        #region Draw
        /// <summary>The cards offered at the start of this run's current act. Same run and act, same cards.</summary>
        public static List<OrdealId> DrawOffer(CampaignSaveData run)
        {
            return DrawOffer(run.seed, run.bookNumber, run.ordeals, id => Get(id).IsEligible(run));
        }

        /// <summary>
        /// Up to OFFER_COUNT cards the run does not hold and that pass isEligible, seeded by run seed and act.
        /// A partial Fisher-Yates over the pool in enum order, so the pool order never depends on dictionary order.
        /// </summary>
        public static List<OrdealId> DrawOffer(int seed, int bookNumber, ICollection<OrdealId> owned, Func<OrdealId, bool> isEligible)
        {
            var pool = new List<OrdealId>();
            foreach (OrdealId id in Enum.GetValues(typeof(OrdealId)))
            {
                if (id == OrdealId.None || !Definitions.ContainsKey(id)) continue;
                if (owned != null && owned.Contains(id)) continue;
                if (!isEligible(id)) continue;
                pool.Add(id);
            }

            var random = new Random(MathUtilities.MixSeed(seed + bookNumber * ORDEAL_SEED_PER_ACT + ORDEAL_SEED_OFFSET));
            int count = Math.Min(OFFER_COUNT, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
            }
            return pool.GetRange(0, count);
        }
        #endregion

        public static float RenownMultiplier(int ordealCount) => 1f + RENOWN_BONUS_PER_ORDEAL * Math.Max(0, ordealCount);

        #region Card rules
        public const int TITHE_GOLD_PER_TURN = 3;
        public const int MERCENARY_CONTRACT_GOLD = 25;
        public const int MERCENARY_CONTRACT_GOLD_PER_TURN = 1;
        public const int IRON_COFFERS_PRICE_RISE = 5;
        public const int PRESS_GANGED_RECRUIT_RISE = 2;
        public const float GREEN_RECRUITS_HEALTH = 0.75f;
        public const float FOG_HIDDEN_NODE_CHANCE = 0.60f;
        public const float ARCANE_DROUGHT_MANA = 0.75f;
        public const int UNBROKEN_RANKS_LEADERSHIP = 10;
        public const float SHORT_QUIVERS_AMMUNITION = 0.75f;

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
