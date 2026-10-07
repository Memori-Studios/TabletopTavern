using System.Collections.Generic;
using TJ.Engagement;
using Unity.Mathematics;

namespace TJ
{
    /// <summary>One unit's worth and the two pillars behind it.</summary>
    public struct UnitValueResult
    {
        public UnitName Unit;
        // False for units with no value at all (the gate); their other fields are zero.
        public bool Valued;
        // The value was set by hand in the weights because the model cannot price the unit (mages); it has no pillars.
        public bool Fixed;
        // 0 to 100, straight-line army points.
        public float Value;
        // Average exchange against the field before scaling. 1 is an even trade.
        public float Power;
        // Multiples of the field average.
        public float Offence;
        public float Toughness;
    }

    /// <summary>
    /// Values every unit by meeting it with every other unit on paper. A meeting uses the live combat
    /// rules as expected values and ends when one side breaks or dies. A unit's power is its average
    /// exchange against the field: how much of each opponent's capacity it removed against how much
    /// of its own it lost.
    /// </summary>
    public static class UnitValueModel
    {
        #region Constants
        // An exchange more lopsided than this counts as this.
        private const float ExchangeCap = 25f;
        // A meeting nobody can finish is scored on the damage shares after this long.
        private const float StalemateSeconds = 120f;
        // Keeps a side that dealt nothing from scoring as infinitely beaten.
        private const float ShareFloor = 0.02f;
        // A squad whose Leadership starts at the break point still counts as this share of its health.
        private const float BrokenShare = 0.02f;
        // Speed that earns no bonus, and the span over which the bonus fills.
        private const float SpeedFloor = 30f;
        private const float SpeedSpan = 50f;
        // Ranks behind the front that a monster's blast still reaches.
        private const int SplashRanks = 2;
        // Share of incoming damage a Blood Drinker can never heal back.
        private const float BloodDrinkerFloor = 0.3f;
        // Fire counts toward breaking its target at this share while the target is not yet in melee.
        private const float FireMoraleShare = 0.5f;
        // Free damage is spread over this long when shown as a rate in the Offence pillar.
        private const float PillarSeconds = 60f;
        // HealthLossTrackingSystem's window, and the health swing inside it that reads as winning.
        private const float RecentLossSeconds = 5f;
        private const float WinningSwing = 250f;
        // UnitSetUpSystem literals with no shared constant.
        private const float EtherealPhysicalMultiplier = 0.5f;
        private const float ThickScalesMultiplier = 0.75f;
        private const float ArmorPiercingMultiplier = 0.5f;
        private const float FireAtWillCadence = 0.5f;
        // Forest Dweller's Melee Attack and Missile Strength gain inside trees.
        private const float ForestDwellerBonus = 5f;
        #endregion

        private struct Profile
        {
            public SquadStats Stats;
            // After terrain shares and the stat-changing traits.
            public float MeleeAttack, MissileStrength, Range, Speed, Ammo;
            public float Health, Mitigation, Block, ClosingSpeed;
            public int Front;
            public bool Holds, Shoots, Monster, Valued;
        }

        /// <summary>A value for every stat block given, in the order given. Each one is an opponent for all the others.</summary>
        public static List<UnitValueResult> ValueAll(IReadOnlyList<SquadStats> units, UnitValueWeights weights)
        {
            var profiles = new Profile[units.Count];
            for (int i = 0; i < units.Count; i++) profiles[i] = Build(units[i], weights);

            var results = new List<UnitValueResult>(units.Count);
            float top = 0f, logOffence = 0f, logToughness = 0f;
            int valued = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                var result = new UnitValueResult { Unit = units[i].unitName, Valued = profiles[i].Valued };
                if (profiles[i].Valued)
                {
                    float total = 0f, weightSum = 0f, offenceSum = 0f, toughnessSum = 0f;
                    for (int j = 0; j < profiles.Length; j++)
                    {
                        if (j == i || !profiles[j].Valued) continue;
                        float weight = weights.OpponentWeight(profiles[j].Stats.RarityTier);
                        if (weight <= 0f) continue;
                        float exchange = Meet(ref profiles[i], ref profiles[j], weights, out float offence, out float toughness);
                        total += weight * exchange;
                        weightSum += weight;
                        offenceSum += weight * math.log(math.max(offence, 1e-9f));
                        toughnessSum += weight * math.log(math.clamp(toughness, 1e-9f, 1e6f));
                    }
                    if (weightSum > 0f)
                    {
                        result.Power = math.exp(total / weightSum) * Adjustment(ref profiles[i], weights);
                        result.Offence = math.exp(offenceSum / weightSum);
                        result.Toughness = math.exp(toughnessSum / weightSum);
                        top = math.max(top, result.Power);
                        logOffence += math.log(result.Offence);
                        logToughness += math.log(result.Toughness);
                        valued++;
                    }
                    else result.Valued = false;
                }
                results.Add(result);
            }

            if (valued == 0 || top <= 0f) return results;
            // A frozen reference keeps every other value still when one unit changes.
            float reference = weights.referencePower > 0f ? weights.referencePower : top;
            float meanOffence = math.exp(logOffence / valued), meanToughness = math.exp(logToughness / valued);
            for (int i = 0; i < results.Count; i++)
            {
                UnitValueResult result = results[i];
                if (result.Valued)
                {
                    result.Value = 100f * math.pow(result.Power / reference, weights.crowding);
                    result.Offence /= meanOffence;
                    result.Toughness /= meanToughness;
                }
                else if (weights.TryGetFixedValue(result.Unit, out float value))
                {
                    result.Valued = result.Fixed = true;
                    result.Value = value;
                    // The power that value stands for, so the two stay consistent for anything that reads both.
                    result.Power = reference * math.pow(math.max(value, 0f) / 100f, 1f / weights.crowding);
                }
                else continue;
                results[i] = result;
            }
            return results;
        }

        /// <summary>One unit's exchange against each of the others, in the order given: above 1 it wins the trade. 0 for itself and for units the model does not price.</summary>
        public static float[] Exchanges(IReadOnlyList<SquadStats> units, UnitValueWeights weights, int unit)
        {
            var exchanges = new float[units.Count];
            Profile subject = Build(units[unit], weights);
            if (!subject.Valued) return exchanges;
            for (int i = 0; i < units.Count; i++)
            {
                if (i == unit) continue;
                Profile opponent = Build(units[i], weights);
                if (opponent.Valued) exchanges[i] = math.exp(Meet(ref subject, ref opponent, weights, out _, out _));
            }
            return exchanges;
        }

        #region Profile
        private static Profile Build(SquadStats stats, UnitValueWeights weights)
        {
            SquadAttributes traits = stats.SquadAttributes;
            var profile = new Profile
            {
                Stats = stats,
                MeleeAttack = stats.MeleeAttack,
                MissileStrength = stats.MissileStrength,
                Range = stats.BaseRange,
                Speed = stats.Speed,
                Ammo = stats.Ammunition,
                Health = stats.baseUnitCount * (float)stats.HitPointsPerUnit,
                Mitigation = stats.Armor / (stats.Armor + 100f),
                Block = traits.HeavyShields ? AutoResolveSimulation.Model.HeavyShieldBlock
                      : traits.StandardShields ? AutoResolveSimulation.Model.StandardShieldBlock : 0f,
                Monster = stats.unitSize == UnitSize.Monstrous || stats.unitSize == UnitSize.SingleUnit,
            };
            if (traits.ForestDweller)
            {
                profile.MeleeAttack += ForestDwellerBonus * weights.forestShare;
                if (profile.MissileStrength > 0f) profile.MissileStrength += ForestDwellerBonus * weights.forestShare;
            }
            if (traits.Overdraw) profile.Range *= TabletopTavernConstants.OVERDRAW_RANGE_MULTIPLIER;
            if (traits.SwiftStride) profile.Speed *= TabletopTavernConstants.SWIFT_STRIDE_SPEED_MULTIPLIER;
            if (stats.unitType == UnitType.Artillery && traits.PowderReserves) profile.Ammo *= TabletopTavernConstants.POWDER_RESERVES_AMMO_MULTIPLIER;
            if (stats.unitType == UnitType.Ranged && traits.DeepQuivers) profile.Ammo += TabletopTavernConstants.DEEP_QUIVERS_AMMO_BONUS;
            profile.ClosingSpeed = math.max(0.5f, profile.Speed / 10f);
            profile.Front = stats.unitSize == UnitSize.SingleUnit ? 1
                : math.min(stats.baseUnitCount, (int)math.ceil(DataTypes.GetFormationWidthFromUnitCount(stats.baseUnitCount) * AutoResolveSimulation.Model.FrontFraction));

            // The same split AutoResolveSimulation makes: archers and artillery hold at range, hybrids shoot on the way in.
            bool holdsAtRange = (TabletopTavernConstants.FightsAtRange(stats.unitType) || stats.unitType == UnitType.Artillery)
                && !TabletopTavernConstants.FightsInMelee(stats.unitType);
            bool armed = profile.Ammo > 0f && profile.MissileStrength > 0f;
            profile.Holds = holdsAtRange && armed;
            profile.Shoots = (holdsAtRange || TabletopTavernConstants.FightsAtRange(stats.unitType)) && armed;
            // Mages need a spell model and the gate is not a unit a player fields.
            profile.Valued = !TabletopTavernConstants.Casts(stats.unitType) && stats.unitType != UnitType.Structure
                && stats.baseUnitCount > 0 && stats.HitPointsPerUnit > 0;
            return profile;
        }

        private static float Adjustment(ref Profile unit, UnitValueWeights weights)
        {
            float bonus = 1f + weights.speedBonus * math.saturate((unit.Speed - SpeedFloor) / SpeedSpan);
            if (unit.Stats.SquadAttributes.Outrider) bonus += weights.outriderBonus;
            if (unit.Stats.SquadAttributes.Terrifying) bonus += weights.terrorCrowdBonus;
            return bonus * weights.Nudge(unit.Stats.unitName);
        }
        #endregion

        #region Damage
        // ApplyDamageSystem's physical path, with its integer truncation.
        private static float Physical(float damage, ref Profile attacker, ref Profile target, bool melee)
        {
            SquadAttributes traits = attacker.Stats.SquadAttributes;
            if (target.Stats.SquadAttributes.Ethereal) damage = math.floor(damage * EtherealPhysicalMultiplier);
            float mitigation = target.Mitigation * (traits.ArmorPiercing ? ArmorPiercingMultiplier : 1f);
            // A sundered squad's armour stops working against the squad that sundered it.
            if (melee && (traits.Emblazing || traits.ArmorSundering)) mitigation = 0f;
            damage -= math.floor(damage * mitigation);
            if (target.Stats.unitSize == UnitSize.Infantry && traits.AntiInfantry) damage *= 2f;
            if (target.Stats.unitSize != UnitSize.Infantry && traits.AntiLarge) damage *= 2f;
            if (melee && target.Stats.unitType == UnitType.Artillery) damage *= 2f;
            return damage;
        }

        private static float MeleeModifier(float damage) => math.floor(damage * TabletopTavernConstants.MELEE_TOTAL_DAMAGE_MODIFIER);

        // Health per second with every front model swinging, and the models of the target it keeps airborne.
        private static float MeleeRate(ref Profile unit, ref Profile target, bool charging, UnitValueWeights weights, out float thrown)
        {
            SquadStats stats = unit.Stats;
            SquadAttributes traits = stats.SquadAttributes;
            float swinging = unit.Front;
            if (target.Stats.unitSize == UnitSize.SingleUnit) swinging = math.min(swinging, AutoResolveSimulation.Model.SingleUnitContactCapacity);

            float attack = unit.MeleeAttack + (charging ? stats.ChargeBonus : 0);
            float hitChance = math.clamp(
                TabletopTavernConstants.MELEE_BASE_HIT_CHANCE + (attack - target.Stats.MeleeDefense) * TabletopTavernConstants.MELEE_HIT_CHANCE_PER_POINT,
                TabletopTavernConstants.MELEE_HIT_CHANCE_MIN, TabletopTavernConstants.MELEE_HIT_CHANCE_MAX) / 100f;

            float strength = stats.WeaponStrength + (charging ? stats.ChargeBonus : 0);
            if (traits.Rage) strength += stats.WeaponStrength * weights.rageTime;
            if (traits.BloodFrenzy) strength += stats.WeaponStrength * weights.frenzyTime;
            if (traits.MonsterSlayer) strength += stats.WeaponStrength * (target.Monster ? 1f : weights.monsterShare);
            if (traits.BackStabbers) strength += stats.WeaponStrength * weights.flankShare;

            float targetModelHealth = target.Stats.HitPointsPerUnit;
            float hits = swinging * hitChance / math.max(0.25f, stats.attackCooldown);
            float rate = hits * math.min(targetModelHealth, MeleeModifier(Physical(strength, ref unit, ref target, true)));

            thrown = 0f;
            if (unit.Monster && target.Stats.unitSize == UnitSize.Infantry && stats.ExplosionDamage > 0)
            {
                float throwSeconds = AutoResolveSimulation.Model.ThrowSeconds;
                float perHit = math.min(AutoResolveSimulation.Model.MonsterSplashModels, target.Stats.baseUnitCount);
                // A model already in the air takes no splash, so the blast can only reach each exposed model once per throw.
                float exposed = math.min(target.Stats.baseUnitCount, SplashRanks * target.Front);
                float throws = math.min(hits * perHit, exposed / throwSeconds);
                rate += throws * math.min(targetModelHealth, MeleeModifier(Physical(stats.ExplosionDamage, ref unit, ref target, true)));
                thrown = math.min(target.Stats.baseUnitCount, throws * throwSeconds);
            }
            return rate;
        }

        // The melee rate across a whole fight: steady blows with the charge bonus mixed in for its share of the time.
        private static float MeleeAverage(ref Profile unit, ref Profile target, UnitValueWeights weights, out float thrown)
        {
            float steady = MeleeRate(ref unit, ref target, false, weights, out thrown);
            // Anti-large stops a charge from the front.
            float share = target.Stats.SquadAttributes.AntiLarge ? 0f
                : unit.Stats.unitSize == UnitSize.Cavalry ? weights.chargeShareMounted : weights.chargeShareFoot;
            if (unit.Stats.ChargeBonus <= 0 || share <= 0f) return steady;
            return (1f - share) * steady + share * MeleeRate(ref unit, ref target, true, weights, out _);
        }

        // Health per second while shooting, and the most the ammo pool can ever do to this target.
        private static float RangedRate(ref Profile unit, ref Profile target, bool fireAtWill, out float ammoCap)
        {
            ammoCap = 0f;
            if (!unit.Shoots) return 0f;
            SquadStats stats = unit.Stats;
            SquadAttributes traits = stats.SquadAttributes, targetTraits = target.Stats.SquadAttributes;
            bool artillery = stats.unitType == UnitType.Artillery;

            float cadence = artillery ? math.max(0.5f, stats.rateOfFire) : TabletopTavernConstants.RANGED_ATTACK_COOLDOWN;
            if (traits.ShotDiscipline) cadence *= TabletopTavernConstants.SHOT_DISCIPLINE_RELOAD_MULTIPLIER;
            float accuracy = stats.attackAccuracy / 100f;
            if (fireAtWill && !artillery)
            {
                cadence *= FireAtWillCadence;
                if (!traits.SteadyAim) accuracy -= TabletopTavernConstants.FIRE_AT_WILL_ACCURACY_PENALTY / 100f;
            }
            accuracy = math.saturate(accuracy);

            float targetModelHealth = target.Stats.HitPointsPerUnit;
            float perShot = Physical(unit.MissileStrength, ref unit, ref target, false) * TabletopTavernConstants.RANGED_TOTAL_DAMAGE_MODIFIER;
            if (targetTraits.ThickScales) perShot *= ThickScalesMultiplier;
            if (targetTraits.ProjectileWard) perShot *= TabletopTavernConstants.PROJECTILE_WARD_DAMAGE_MULTIPLIER;
            perShot = math.min(targetModelHealth, perShot) * (1f - target.Block);
            if (artillery)
            {
                // The shell throws the models round the impact, each taking the blast through the melee path; shields do not stop it.
                float blast = stats.ExplosionDamage * (traits.Demolisher ? TabletopTavernConstants.DEMOLISHER_EXPLOSION_MULTIPLIER : 1f);
                perShot += math.min(AutoResolveSimulation.Model.ArtillerySplashModels, target.Stats.baseUnitCount)
                    * math.min(targetModelHealth, MeleeModifier(Physical(blast, ref unit, ref target, true)));
            }
            ammoCap = unit.Ammo * accuracy * perShot;
            return stats.baseUnitCount / cadence * accuracy * perShot;
        }
        #endregion

        #region Morale
        // Health a squad can lose before it breaks or dies under a steady incoming rate: MoraleUpdateJob solved for the break time.
        private static float Capacity(ref Profile unit, float incoming, bool terrified, bool onFire, bool winning)
        {
            if (incoming <= 0f) return unit.Health;
            float lossPerSecond = incoming / unit.Health;
            float steady = (terrified ? TabletopTavernConstants.MORALE_THREAT_PENALTY : 0f)
                + (onFire ? TabletopTavernConstants.MORALE_FIRE_DAMAGE_PENALTY : 0f)
                - (winning ? TabletopTavernConstants.MORALE_WINNING_BONUS : 0f);
            float budget = (unit.Stats.Leadership - TabletopTavernConstants.MORALE_BREAK_THRESHOLD) / TabletopTavernConstants.MORALE_LOSS_MODIFIER;
            if (budget <= 0f) return BrokenShare * unit.Health;

            // Morale drains by (recent loss + depletion so far + steady terms) each second: a quadratic in time.
            float a = 0.5f * TabletopTavernConstants.MORALE_TOTAL_HEALTH_DEPLETION_PENALTY * lossPerSecond;
            float b = TabletopTavernConstants.MORALE_RECENT_HEALTH_LOSS_PENALTY * RecentLossSeconds * 100f * lossPerSecond + steady;
            float seconds = b < 0f
                // Morale holds at its cap until depletion outweighs the winning bonus, then drains from there.
                ? -b / (TabletopTavernConstants.MORALE_TOTAL_HEALTH_DEPLETION_PENALTY * lossPerSecond) + math.sqrt(budget / a)
                : (-b + math.sqrt(b * b + 4f * a * budget)) / (2f * a);
            return unit.Health * math.min(1f, lossPerSecond * seconds);
        }
        #endregion

        #region Meeting
        // What one side does to the other: damage before the other can hit back, the rate once both are fighting, and its ammo limit.
        private static void Side(ref Profile unit, ref Profile target, UnitValueWeights weights,
            out float free, out float rate, out float cap, out float thrown, out float fireRate)
        {
            free = 0f; cap = float.PositiveInfinity; thrown = 0f; fireRate = 0f;
            float gap = AutoResolveSimulation.Model.DeploymentGap, contact = AutoResolveSimulation.Model.ContactDistance;
            if (unit.Holds && !target.Holds)
            {
                // The target has to walk in: volleys first, Fire at Will inside 60% of range, plus the time a friendly line buys.
                float range = math.min(unit.Range, gap);
                float walking = math.max(0f, range - contact) / target.ClosingSpeed;
                float atWill = math.min(walking, math.max(0f, AutoResolveSimulation.Model.FireAtWillRangeShare * unit.Range - contact) / target.ClosingSpeed);
                float volleys = walking - atWill + weights.shieldedSeconds;
                float volleyRate = RangedRate(ref unit, ref target, false, out float ammoCap);
                float atWillRate = RangedRate(ref unit, ref target, true, out _);
                free = math.min(ammoCap, volleyRate * volleys + atWillRate * atWill);
                rate = MeleeAverage(ref unit, ref target, weights, out thrown);
                fireRate = volleyRate;
            }
            else if (unit.Holds && target.Holds)
            {
                // Two shooters trade fire; the longer range gets the volleys the other needs to walk into its own range.
                float volleyRate = RangedRate(ref unit, ref target, false, out float ammoCap);
                free = math.min(ammoCap, volleyRate * math.max(0f, unit.Range - target.Range) / target.ClosingSpeed);
                rate = volleyRate;
                cap = ammoCap - free;
                fireRate = volleyRate;
            }
            else
            {
                rate = MeleeAverage(ref unit, ref target, weights, out thrown);
                if (unit.Shoots)
                {
                    // A hybrid throws while the gap closes.
                    float closing = unit.ClosingSpeed + (target.Holds ? 0f : target.ClosingSpeed);
                    float seconds = math.max(0f, math.min(unit.Range, gap - contact)) / math.max(0.5f, closing);
                    float volleyRate = RangedRate(ref unit, ref target, false, out float ammoCap);
                    free = math.min(ammoCap, volleyRate * seconds);
                    fireRate = volleyRate;
                }
            }
        }

        // The log exchange of one meeting: positive when the unit removes more of the opponent's capacity than it loses of its own.
        private static float Meet(ref Profile unit, ref Profile opponent, UnitValueWeights weights, out float offence, out float toughness)
        {
            Side(ref unit, ref opponent, weights, out float freeUnit, out float rateUnit, out float capUnit, out float thrownByUnit, out float fireUnit);
            Side(ref opponent, ref unit, weights, out float freeOpponent, out float rateOpponent, out float capOpponent, out float thrownByOpponent, out float fireOpponent);

            // Thrown models come out of the whole squad; the front only thins once the rear cannot refill it.
            bool exchangeOfFire = unit.Holds && opponent.Holds;
            if (thrownByOpponent > 0f && !exchangeOfFire && unit.Front > 0)
                rateUnit *= math.min(unit.Front, math.max(0f, unit.Stats.baseUnitCount - thrownByOpponent)) / unit.Front;
            if (thrownByUnit > 0f && !exchangeOfFire && opponent.Front > 0)
                rateOpponent *= math.min(opponent.Front, math.max(0f, opponent.Stats.baseUnitCount - thrownByUnit)) / opponent.Front;

            float intoUnit = rateOpponent, intoOpponent = rateUnit;
            if (unit.Stats.SquadAttributes.BloodDrinker && !unit.Holds)
                intoUnit = math.max(BloodDrinkerFloor * rateOpponent, rateOpponent - TabletopTavernConstants.BLOOD_DRINKER_LIFESTEAL * rateUnit);
            if (opponent.Stats.SquadAttributes.BloodDrinker && !opponent.Holds)
                intoOpponent = math.max(BloodDrinkerFloor * rateUnit, rateUnit - TabletopTavernConstants.BLOOD_DRINKER_LIFESTEAL * rateOpponent);

            float winningMargin = WinningSwing / RecentLossSeconds;
            float capacityUnit = Capacity(ref unit, math.max(intoUnit, fireOpponent * FireMoraleShare),
                Terrifies(ref opponent, ref unit), opponent.Stats.SquadAttributes.FlamingAmmo && opponent.Shoots, rateUnit - rateOpponent > winningMargin);
            float capacityOpponent = Capacity(ref opponent, math.max(intoOpponent, fireUnit * FireMoraleShare),
                Terrifies(ref unit, ref opponent), unit.Stats.SquadAttributes.FlamingAmmo && unit.Shoots, rateOpponent - rateUnit > winningMargin);

            float leftUnit = capacityUnit - freeOpponent, leftOpponent = capacityOpponent - freeUnit;
            float removedByUnit, removedByOpponent;
            if (leftUnit <= 0f && leftOpponent <= 0f) removedByUnit = removedByOpponent = 1f;
            else if (leftOpponent <= 0f) { removedByUnit = 1f; removedByOpponent = math.min(1f, freeOpponent / capacityUnit); }
            else if (leftUnit <= 0f) { removedByOpponent = 1f; removedByUnit = math.min(1f, freeUnit / capacityOpponent); }
            else
            {
                float seconds = math.min(SecondsToBeat(leftOpponent, intoOpponent, capUnit), SecondsToBeat(leftUnit, intoUnit, capOpponent));
                if (float.IsPositiveInfinity(seconds)) seconds = StalemateSeconds;
                removedByUnit = math.min(1f, (freeUnit + math.min(capUnit, intoOpponent * seconds)) / capacityOpponent);
                removedByOpponent = math.min(1f, (freeOpponent + math.min(capOpponent, intoUnit * seconds)) / capacityUnit);
            }

            offence = (freeUnit / PillarSeconds + rateUnit) / capacityOpponent;
            float taken = freeOpponent / PillarSeconds + rateOpponent;
            toughness = taken > 0f ? capacityUnit / taken : float.PositiveInfinity;

            float limit = math.log(ExchangeCap);
            return math.clamp(math.log((removedByUnit + ShareFloor) / (removedByOpponent + ShareFloor)), -limit, limit);
        }

        private static float SecondsToBeat(float left, float rate, float cap) =>
            rate <= 0f || left > cap ? float.PositiveInfinity : left / rate;

        private static bool Terrifies(ref Profile source, ref Profile target) =>
            source.Stats.SquadAttributes.Terrifying && !target.Stats.SquadAttributes.Stalwart && !target.Stats.SquadAttributes.Terrifying;
        #endregion
    }
}
