using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TJ.Engagement
{
    /// <summary>
    /// The time-stepped battle model behind auto-resolve. One call to Tick advances every squad by
    /// one second: squads close on their targets across a flat field, archers shoot what is in
    /// range, engaged squads trade blows along their front rank, wounds land on individual models,
    /// and morale drains with the same formula MoraleUpdateJob uses. A squad leaves the fight when
    /// its morale breaks or its last model dies, and the battle ends when a side has no squad left
    /// standing, which is exactly what EndBattleSystem checks.
    ///
    /// Every number here is either read from the live systems (hit roll, damage modifiers, reload,
    /// morale terms) or a fitted constant in <see cref="Model"/> with the harness measurement it
    /// came from. Randomness goes through UnityEngine.Random so a seeded run replays.
    /// </summary>
    internal static class AutoResolveSimulation
    {
        #region Tuning

        /// <summary>Fitted constants. Each one names the live measurement it was fitted to.</summary>
        internal static class Model
        {
            // Seconds per tick.
            public const float Dt = 1f;
            // Distance between the two front rows at deployment. Harness roster: front rows at z = -59 and +60.
            public const float DeploymentGap = 119f;
            // Lateral spacing between deployment slots and depth between rows (ArmySpawnManager).
            public const float SlotSpacing = 30f;
            public const float RowSpacing = 20f;
            // Two squads are in melee contact inside this distance.
            public const float ContactDistance = 4f;
            // Models swinging while engaged, as a share of formation width. Live 1v1 block fights show
            // 13-15 of a 12-wide Levy or Templar front in reach, and their damage rate confirms it.
            public const float FrontFraction = 1.15f;
            // How far the exposed set extends past the front rank when a squad is flanked.
            public const float FlankedExposure = 1.5f;
            // Whether a second squad piling onto an engaged target counts as a flank. Live flank flags
            // were raised in about 1% of engaged samples across 40 harness battles, so it is off.
            public const bool PileOnIsFlank = false;
            // Share of melee hits that land on the most wounded exposed model rather than a random
            // one. Live attackers all pick their closest defender, so blows pile onto the same few
            // models: a Levy death costs about 115 HP of damage, not 140.
            public const float MeleeFocus = 0.3f;
            // Extra share of a squad's front that joins in per additional enemy squad pressing it.
            // Live: 14 Levy in reach against one squad, 17 against two.
            public const float ExtraContactFront = 0.2f;
            // Models that can crowd round one single-model unit at once. Three Levy squads on a giant
            // had 12-15 models in reach between them.
            public const int SingleUnitContactCapacity = 14;
            // Models thrown by one artillery shell. Geometry says up to a dozen inside a 5 u blast,
            // but live Kaiser Cannons averaged about 100 HP per shell fired (46 kills from 54 shells),
            // which is the direct hit plus a few thrown neighbours.
            public const int ArtillerySplashModels = 6;
            // Models thrown by one monster hit (ExplosionRange 2-3 u).
            public const int MonsterSplashModels = 3;
            // Ranged squads switch to Fire at Will when the target is inside this share of their range
            // (EnemyRangedBehavior escalates at 60%).
            public const float FireAtWillRangeShare = 0.6f;
            // Morale breaks at this value (SquadManager sets MoraleThreshold to the same constant).
            public const float MoraleBreak = TabletopTavernConstants.MORALE_BREAK_THRESHOLD;
            // The army-wide morale penalty latches when a side falls to this share of its health.
            public const float ArmyLossesShare = 0.25f;
            // Seconds of movement before contact needed for the charge bonus (TIME_REQUIRED_FOR_CHARGE_BONUS).
            public const float ChargeBuildup = 2f;
            // Seconds the charge bonus lasts after contact (TIME_TO_REMOVE_CHARGE_BONUS).
            public const float ChargeWindow = 6f;
            // Shield block chances (UnitSetUpSystem.GetShieldBlockChance).
            public const float StandardShieldBlock = 0.35f;
            public const float HeavyShieldBlock = 0.70f;
            // Seconds after a friendly squad breaks that nearby squads carry the retreating-allies penalty.
            public const float RetreatingAlliesSeconds = 10f;
            // Seconds a monster's throw keeps a model out of the fight (THROW_TOTAL_TIME).
            public const float ThrowSeconds = 2.5f;
            // Lateral spacing of models in a formation, used to measure to a squad's edge rather than
            // its centre when picking a target: live models go for the nearest enemy model.
            public const float ModelSpacing = 1.5f;
            // Extra distance a candidate target counts as, per friendly squad already heading for it.
            // Live squads bump into each other and spread across the enemy line instead of stacking.
            public const float SharedTargetPenalty = 6f;
            // Enemy cavalry march at the slowest infantry's pace until a player squad is this close,
            // then release into the charge (EnemyCavalryBehavior._cavalryReleaseDistance).
            public const float CavalryReleaseDistance = 55f;
        }

        /// <summary>
        /// Whether the player's melee squads march on the enemy or hold their line and let the enemy
        /// come, which is what the enemy AI does within two seconds of the battle starting either way.
        /// Mutable so the harness can replay both behaviours against live data.
        /// </summary>
        internal static bool PlayerAdvances = true;
        /// <summary>
        /// Player archers and artillery keep their deployment spot and shoot what comes into range;
        /// enemy shooters walk up to their range the way EnemyRangedBehavior does.
        /// </summary>
        internal static bool PlayerRangedHolds = true;

        #endregion

        #region Setup

        /// <summary>Lays both armies out and builds per-model health. Idempotent.</summary>
        internal static void Initialize(AutoResolveSquad[] player, AutoResolveSquad[] enemy)
        {
            if (player == null || enemy == null) return;
            bool playerReady = AllInitialized(player), enemyReady = AllInitialized(enemy);
            if (playerReady && enemyReady) return;
            if (!playerReady) { Deploy(player, -1f); }
            if (!enemyReady) { Deploy(enemy, +1f); }
        }

        private static bool AllInitialized(AutoResolveSquad[] squads)
        {
            for (int i = 0; i < squads.Length; i++)
                if (!squads[i].SimReady) return false;
            return true;
        }

        // Mirrors ArmySpawnManager.AssignSpawnPositionsNormalBattle: melee front row centre-out,
        // monstrous then ranged in the middle row, cavalry then ranged at the back. Rows are
        // RowSpacing apart, slots SlotSpacing apart. The player faces +z, the enemy -z.
        private static void Deploy(AutoResolveSquad[] squads, float facing)
        {
            var melee = new List<int>(); var ranged = new List<int>(); var cavalry = new List<int>(); var large = new List<int>();
            for (int i = 0; i < squads.Length; i++)
            {
                SquadStats s = squads[i].squadStats;
                if (s.unitSize == UnitSize.Cavalry) cavalry.Add(i);
                else if (s.unitSize == UnitSize.Monstrous) large.Add(i);
                else if (s.unitType == UnitType.Melee || s.unitType == UnitType.Hybrid) melee.Add(i);
                else ranged.Add(i);
            }
            int[] centerOut = { 2, 1, 3, 0, 4 };
            int[][] rows = { new int[5], new int[5], new int[5] };
            for (int r = 0; r < 3; r++) for (int c = 0; c < 5; c++) rows[r][c] = -1;

            int m = 0, l = 0, rg = 0, cv = 0;
            for (int i = 0; i < 5 && m < melee.Count; i++) rows[0][centerOut[i]] = melee[m++];
            for (int i = 0; i < 5 && l < large.Count; i++) rows[1][centerOut[i]] = large[l++];
            for (int i = 0; i < 5 && rg < ranged.Count; i++) { int slot = centerOut[i]; if (rows[1][slot] == -1) rows[1][slot] = ranged[rg++]; }
            for (int slot = 0; slot < 5 && m < melee.Count; slot++) if (rows[1][slot] == -1) rows[1][slot] = melee[m++];
            for (int i = 0; i < 5 && cv < cavalry.Count; i++) rows[2][centerOut[i]] = cavalry[cv++];
            int[] outerFirst = { 0, 4, 1, 3, 2 };
            for (int i = 0; i < 5 && rg < ranged.Count; i++) { int slot = outerFirst[i]; if (rows[2][slot] == -1) rows[2][slot] = ranged[rg++]; }
            var rest = new List<int>();
            for (; m < melee.Count; m++) rest.Add(melee[m]);
            for (; l < large.Count; l++) rest.Add(large[l]);
            for (; rg < ranged.Count; rg++) rest.Add(ranged[rg]);
            for (; cv < cavalry.Count; cv++) rest.Add(cavalry[cv]);
            int k = 0;
            for (int r = 0; r < 3 && k < rest.Count; r++) for (int c = 0; c < 5 && k < rest.Count; c++) if (rows[r][c] == -1) rows[r][c] = rest[k++];

            for (int r = 0; r < 3; r++)
            {
                int filled = 0; for (int c = 0; c < 5; c++) if (rows[r][c] != -1) filled++;
                if (filled == 0) continue;
                float startX = -(filled - 1) * 0.5f * Model.SlotSpacing;
                int placed = 0;
                for (int c = 0; c < 5; c++)
                {
                    int idx = rows[r][c];
                    if (idx == -1) continue;
                    squads[idx].X = startX + placed * Model.SlotSpacing;
                    squads[idx].Z = facing * (Model.DeploymentGap * 0.5f + r * Model.RowSpacing);
                    placed++;
                }
            }
            for (int i = 0; i < squads.Length; i++) InitSquad(ref squads[i]);
        }

        private static void InitSquad(ref AutoResolveSquad q)
        {
            int hp = math.max(1, q.healthPerKill);
            int alive = math.clamp((int)math.ceil(math.max(0, q.finalHealth) / (float)hp), 0, math.max(q.maxUnits, 1));
            q.UnitHealth = new int[math.max(q.maxUnits, 1)];
            int pool = math.max(0, q.finalHealth);
            for (int i = 0; i < alive; i++) { int v = math.min(hp, pool); q.UnitHealth[i] = v; pool -= v; }
            q.UnitsAlive = alive;
            q.finalHealth = SumHealth(ref q);
            q.Morale = q.squadStats.Leadership;
            q.Broken = q.UnitsAlive <= 0;
            q.Ammo = q.squadStats.Ammunition;
            q.RecentLoss = new float[5];
            q.RecentDealt = new float[5];
            q.FormationWidth = DataTypes.GetFormationWidthFromUnitCount(math.max(1, q.maxUnits));
            q.TargetIndex = -1;
            q.ContactIndex = -1;
            q.ChargeWindow = 0; q.MoveSeconds = 0; q.HitCarry = 0; q.ShotCarry = 0; q.ThrownModels = 0;
            q.RetreatingAlliesTimer = 0; q.FireAtWill = false; q.ArmyLosses = false; q.DefensiveStance = false;
            q.SimReady = true;
        }

        private static int SumHealth(ref AutoResolveSquad q)
        {
            int total = 0;
            for (int i = 0; i < q.UnitsAlive; i++) total += q.UnitHealth[i];
            return total;
        }

        #endregion

        #region Tick

        /// <summary>Health of every standing squad on a side, summed once per tick for the activity check.</summary>
        private static long TotalHealth(AutoResolveSquad[] squads)
        {
            long total = 0;
            for (int i = 0; i < squads.Length; i++) total += math.max(0, squads[i].finalHealth);
            return total;
        }

        /// <summary>
        /// Advances the battle by Model.Dt seconds. Returns false when nothing could happen this
        /// second (no damage and no squad able to move), so a caller can end a stalemate by health
        /// rather than spin to the round cap.
        /// </summary>
        internal static bool Tick(AutoResolveSquad[] player, AutoResolveSquad[] enemy, float enemyDamageBonus, List<int3> damageLog)
        {
            Initialize(player, enemy);
            ApplyPooledDamage(player, enemy, damageLog);
            long healthBefore = TotalHealth(player) + TotalHealth(enemy);

            AssignTargets(player, enemy);
            AssignTargets(enemy, player);
            Move(player, enemy, PlayerAdvances, PlayerRangedHolds, false);
            Move(enemy, player, true, false, true);
            ResolveContacts(player, enemy);
            CrashingHorde(player);

            // Hits land as they are rolled, so whoever strikes first in a second has a small edge.
            // A coin flip per second keeps a mirror match a coin flip instead of a fixed advantage.
            bool playerFirst = UnityEngine.Random.value < 0.5f;
            if (playerFirst)
            {
                Fire(player, enemy, 1f);
                Fire(enemy, player, enemyDamageBonus);
                Melee(player, enemy, 1f);
                Melee(enemy, player, enemyDamageBonus);
                UpdateMorale(player, enemy, false);
                UpdateMorale(enemy, player, true);
            }
            else
            {
                Fire(enemy, player, enemyDamageBonus);
                Fire(player, enemy, 1f);
                Melee(enemy, player, enemyDamageBonus);
                Melee(player, enemy, 1f);
                UpdateMorale(enemy, player, true);
                UpdateMorale(player, enemy, false);
            }
            EndOfTick(player);
            EndOfTick(enemy);
            return TotalHealth(player) + TotalHealth(enemy) != healthBefore || AnyoneClosing(player, enemy) || AnyoneClosing(enemy, player);
        }

        private static bool AnyoneClosing(AutoResolveSquad[] own, AutoResolveSquad[] foes)
        {
            for (int i = 0; i < own.Length; i++)
                if (Standing(ref own[i]) && own[i].ContactIndex == -1 && own[i].MoveSeconds > 0f && IndexOf(foes, own[i].TargetIndex) != -1) return true;
            return false;
        }

        private static bool Standing(ref AutoResolveSquad q) => q.SimReady && !q.Broken && q.UnitsAlive > 0;

        private static float Distance(ref AutoResolveSquad a, ref AutoResolveSquad b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return math.sqrt(dx * dx + dz * dz);
        }

        private static int IndexOf(AutoResolveSquad[] squads, int squadIndex)
        {
            for (int i = 0; i < squads.Length; i++) if (squads[i].SquadIndex == squadIndex) return i;
            return -1;
        }

        // Archers and artillery: shooters that hold at range. Hybrids also shoot (see Fire) but close to melee.
        private static bool IsShooter(ref AutoResolveSquad q) =>
            (TabletopTavernConstants.FightsAtRange(q.squadStats.unitType) || q.squadStats.unitType == UnitType.Artillery)
            && !TabletopTavernConstants.FightsInMelee(q.squadStats.unitType);

        private static bool CanShoot(ref AutoResolveSquad q) =>
            IsShooter(ref q) || (TabletopTavernConstants.FightsAtRange(q.squadStats.unitType) && q.squadStats.MissileStrength > 0);

        private static bool OutOfAmmo(ref AutoResolveSquad q) => q.Ammo <= 0f;

        // A squad that closes to melee: melee, hybrid, monsters, cavalry, mages (once their charges are
        // spent, in the live game), and any shooter that ran dry, artillery crews included so a field
        // of spent batteries still resolves.
        private static bool SeeksMelee(ref AutoResolveSquad q)
        {
            if (TabletopTavernConstants.FightsInMelee(q.squadStats.unitType)) return true;
            if (TabletopTavernConstants.Casts(q.squadStats.unitType)) return true;
            return OutOfAmmo(ref q);
        }

        private static bool IsLarge(ref AutoResolveSquad q) => q.squadStats.unitSize != UnitSize.Infantry;

        #endregion

        #region Targeting and movement

        // Closest standing enemy, as SquadFindTargetSquadSystem picks it: a squad already attacking
        // me first, monsters prefer targets without AntiLarge, artillery hunts shooters.
        internal static void AssignTargets(AutoResolveSquad[] own, AutoResolveSquad[] foes)
        {
            for (int i = 0; i < own.Length; i++)
            {
                if (!Standing(ref own[i])) { own[i].TargetIndex = -1; own[i].ContactIndex = -1; continue; }

                int current = IndexOf(foes, own[i].TargetIndex);
                if (current != -1 && Standing(ref foes[current])) continue;
                own[i].TargetIndex = -1;
                own[i].ContactIndex = -1;
                own[i].MoveSeconds = 0;

                int pick = -1; float best = float.MaxValue;
                for (int j = 0; j < foes.Length; j++)
                {
                    if (!Standing(ref foes[j])) continue;
                    if (foes[j].TargetIndex == own[i].SquadIndex && foes[j].ContactIndex == own[i].SquadIndex) { pick = j; break; }
                }
                if (pick == -1)
                {
                    bool monster = own[i].squadStats.unitSize == UnitSize.Monstrous || own[i].squadStats.unitSize == UnitSize.SingleUnit;
                    bool artillery = own[i].squadStats.unitType == UnitType.Artillery;
                    for (int pass = 0; pass < 2 && pick == -1; pass++)
                    {
                        for (int j = 0; j < foes.Length; j++)
                        {
                            if (!Standing(ref foes[j])) continue;
                            if (pass == 0 && monster && foes[j].squadStats.SquadAttributes.AntiLarge) continue;
                            if (pass == 0 && artillery && !IsShooter(ref foes[j])) continue;
                            float d = Distance(ref own[i], ref foes[j]) - EdgeOffset(ref foes[j]);
                            for (int k = 0; k < own.Length; k++)
                                if (k != i && Standing(ref own[k]) && own[k].TargetIndex == foes[j].SquadIndex) d += Model.SharedTargetPenalty;
                            if (d < best) { best = d; pick = j; }
                        }
                    }
                }
                own[i].TargetIndex = pick == -1 ? -1 : foes[pick].SquadIndex;
            }
        }

        private static float EdgeOffset(ref AutoResolveSquad q) =>
            q.squadStats.unitSize == UnitSize.SingleUnit ? 0f : math.min(q.UnitsAlive, q.FormationWidth) * Model.ModelSpacing * 0.5f;

        private static void Move(AutoResolveSquad[] own, AutoResolveSquad[] foes, bool meleeAdvances, bool rangedHolds, bool cavalryMarches)
        {
            // Enemy cavalry hold to the infantry's pace until a player squad is close (EnemyCavalryBehavior).
            float marchSpeed = float.MaxValue;
            if (cavalryMarches)
                for (int i = 0; i < own.Length; i++)
                    if (Standing(ref own[i]) && own[i].squadStats.unitSize == UnitSize.Infantry && TabletopTavernConstants.FightsInMelee(own[i].squadStats.unitType))
                        marchSpeed = math.min(marchSpeed, own[i].squadStats.Speed / 10f);
            for (int i = 0; i < own.Length; i++)
            {
                ref AutoResolveSquad q = ref own[i];
                if (!Standing(ref q) || q.ContactIndex != -1) continue;
                int t = IndexOf(foes, q.TargetIndex);
                if (t == -1) continue;
                ref AutoResolveSquad target = ref foes[t];
                float d = Distance(ref q, ref target);

                bool shooter = IsShooter(ref q) && !OutOfAmmo(ref q);
                if (shooter && (rangedHolds || d <= q.squadStats.BaseRange)) { q.MoveSeconds = 0; continue; }
                if (!shooter && !meleeAdvances) continue;

                float speed = q.squadStats.Speed / 10f;
                if (cavalryMarches && q.squadStats.unitSize == UnitSize.Cavalry && marchSpeed < speed && NearestFoe(ref q, foes) > Model.CavalryReleaseDistance)
                    speed = marchSpeed;
                float step = math.min(d, speed * Model.Dt);
                if (d > 0.001f)
                {
                    q.X += (target.X - q.X) / d * step;
                    q.Z += (target.Z - q.Z) / d * step;
                }
                q.MoveSeconds += Model.Dt;
            }
        }

        private static float NearestFoe(ref AutoResolveSquad q, AutoResolveSquad[] foes)
        {
            float best = float.MaxValue;
            for (int j = 0; j < foes.Length; j++)
                if (Standing(ref foes[j])) best = math.min(best, Distance(ref q, ref foes[j]));
            return best;
        }

        // A melee seeker that reaches its target locks both into contact. The defender keeps its own
        // target if it already has one in contact; otherwise it turns to face the attacker, which is
        // what the reciprocal attack order does in the live game.
        private static void ResolveContacts(AutoResolveSquad[] player, AutoResolveSquad[] enemy)
        {
            Contact(player, enemy);
            Contact(enemy, player);
        }

        private static void Contact(AutoResolveSquad[] own, AutoResolveSquad[] foes)
        {
            for (int i = 0; i < own.Length; i++)
            {
                ref AutoResolveSquad q = ref own[i];
                if (!Standing(ref q)) continue;
                int t = IndexOf(foes, q.TargetIndex);
                if (t == -1) { q.ContactIndex = -1; continue; }
                ref AutoResolveSquad target = ref foes[t];
                if (q.ContactIndex == target.SquadIndex) continue;
                if (!SeeksMelee(ref q)) continue;
                if (Distance(ref q, ref target) > Model.ContactDistance) continue;

                q.ContactIndex = target.SquadIndex;
                q.ChargeWindow = q.MoveSeconds >= Model.ChargeBuildup ? Model.ChargeWindow : 0f;
                // A charge into a braced anti-large line is stripped on contact (SquadChargeBonusSystem).
                if (target.squadStats.SquadAttributes.AntiLarge) q.ChargeWindow = 0f;
                q.MoveSeconds = 0;

                int targetsOwnTarget = IndexOf(own, target.TargetIndex);
                bool targetBusy = targetsOwnTarget != -1 && target.ContactIndex == own[targetsOwnTarget].SquadIndex;
                if (!targetBusy)
                {
                    // A defender that was itself charging this squad meets it head on and keeps its charge.
                    bool counterCharge = target.TargetIndex == q.SquadIndex && target.MoveSeconds >= Model.ChargeBuildup
                        && !q.squadStats.SquadAttributes.AntiLarge;
                    target.TargetIndex = q.SquadIndex;
                    target.ContactIndex = q.SquadIndex;
                    target.ChargeWindow = counterCharge ? Model.ChargeWindow : 0f;
                    target.MoveSeconds = 0;
                }
            }
        }

        #endregion

        #region Ranged

        private static void Fire(AutoResolveSquad[] own, AutoResolveSquad[] foes, float damageBonus)
        {
            for (int i = 0; i < own.Length; i++)
            {
                ref AutoResolveSquad q = ref own[i];
                if (!Standing(ref q) || !CanShoot(ref q) || OutOfAmmo(ref q)) continue;
                // Shooters caught in melee stop firing (RangedUnitAttackSystem skips InCombat units).
                if (q.ContactIndex != -1 || EngagedBy(ref q, foes)) continue;
                int t = IndexOf(foes, q.TargetIndex);
                if (t == -1) continue;
                ref AutoResolveSquad target = ref foes[t];
                float range = q.squadStats.BaseRange;
                float d = Distance(ref q, ref target);
                if (d > range) continue;

                bool artillery = q.squadStats.unitType == UnitType.Artillery;
                float cadence = artillery ? math.max(0.5f, q.squadStats.rateOfFire) : TabletopTavernConstants.RANGED_ATTACK_COOLDOWN;
                if (q.squadStats.SquadAttributes.ShotDiscipline) cadence *= TabletopTavernConstants.SHOT_DISCIPLINE_RELOAD_MULTIPLIER;
                float accuracy = q.squadStats.attackAccuracy;
                if (!artillery)
                {
                    // Volley until the target is close, then Fire at Will: twice the rate, less accurate.
                    q.FireAtWill = q.FireAtWill || d <= range * Model.FireAtWillRangeShare;
                    if (q.FireAtWill)
                    {
                        cadence *= 0.5f;
                        if (!q.squadStats.SquadAttributes.SteadyAim) accuracy -= TabletopTavernConstants.FIRE_AT_WILL_ACCURACY_PENALTY / 100f;
                    }
                }
                accuracy = math.clamp(accuracy, 0f, 1f);

                float shots = q.UnitsAlive * Model.Dt / cadence;
                shots = math.min(shots, q.Ammo);
                q.Ammo -= shots;
                float expectedHits = shots * accuracy + q.ShotCarry;
                int hits = (int)math.floor(expectedHits);
                q.ShotCarry = expectedHits - hits;

                for (int h = 0; h < hits; h++)
                {
                    if (!Standing(ref target)) break;
                    int victim = UnityEngine.Random.Range(0, target.UnitsAlive);
                    int damage = RangedHitDamage(ref q, ref target, damageBonus);
                    if (damage > 0) Hit(ref target, victim, damage, ref q, foes);
                    if (artillery)
                    {
                        // The shell throws every model near the impact (ExplosionSystem), each taking the
                        // explosion damage through the melee path (UnitThrownSystem), and out of the fight
                        // while airborne.
                        int splash = math.min(Model.ArtillerySplashModels, target.UnitsAlive);
                        int splashDamage = MeleeHitDamage(q.squadStats.ExplosionDamage, ref q.squadStats, ref target, false, damageBonus);
                        for (int s = 0; s < splash && Standing(ref target); s++)
                            Hit(ref target, UnityEngine.Random.Range(0, target.UnitsAlive), splashDamage, ref q, foes);
                        target.ThrownModels = math.min(target.UnitsAlive, target.ThrownModels + splash);
                    }
                }
            }
        }

        private static int RangedHitDamage(ref AutoResolveSquad shooter, ref AutoResolveSquad target, float damageBonus)
        {
            int damage = shooter.squadStats.MissileStrength;
            damage = ApplyPhysicalModifiers(damage, ref shooter.squadStats, ref target, false);
            damage = (int)(damage * TabletopTavernConstants.RANGED_TOTAL_DAMAGE_MODIFIER * damageBonus);
            if (target.squadStats.SquadAttributes.ThickScales) damage = (int)(damage * 0.75f);
            float block = target.shieldBlockChance;
            if (block > 0f && UnityEngine.Random.value < block) return 0;
            if (shooter.squadStats.SquadAttributes.FlamingAmmo) target.OnFireTimer = 5f;
            return ApplyTakenMultiplier(damage, ref target);
        }

        #endregion

        #region Melee

        private static bool EngagedBy(ref AutoResolveSquad q, AutoResolveSquad[] foes)
        {
            for (int j = 0; j < foes.Length; j++)
                if (Standing(ref foes[j]) && foes[j].ContactIndex == q.SquadIndex) return true;
            return false;
        }

        // Live models attack whichever enemy model is closest, so a squad in contact with several
        // enemies splits its swings between them by how much of each is in reach.
        private static void Melee(AutoResolveSquad[] own, AutoResolveSquad[] foes, float damageBonus)
        {
            for (int i = 0; i < own.Length; i++)
            {
                ref AutoResolveSquad q = ref own[i];
                if (!Standing(ref q)) continue;

                // Everyone this squad is touching: its own contact plus anyone in contact with it.
                var contacts = new List<int>(4);
                int primary = IndexOf(foes, q.ContactIndex);
                if (primary != -1 && Standing(ref foes[primary])) contacts.Add(primary);
                for (int j = 0; j < foes.Length; j++)
                    if (j != primary && Standing(ref foes[j]) && foes[j].ContactIndex == q.SquadIndex) contacts.Add(j);
                if (contacts.Count == 0) { q.EngagedSeconds = 0; continue; }
                q.EngagedSeconds += Model.Dt;

                int attackers = Attackers(ref q, contacts.Count);
                float cooldown = math.max(0.25f, q.squadStats.attackCooldown);
                float weight = 0;
                for (int c = 0; c < contacts.Count; c++) weight += Attackers(ref foes[contacts[c]]);
                if (weight <= 0) weight = contacts.Count;

                for (int c = 0; c < contacts.Count; c++)
                {
                    ref AutoResolveSquad target = ref foes[contacts[c]];
                    float share = contacts.Count == 1 ? 1f : math.max(1f, Attackers(ref target)) / weight;
                    bool flanking = Model.PileOnIsFlank && target.ContactIndex != q.SquadIndex && target.ContactIndex != -1;
                    float swinging = attackers * share;
                    if (target.squadStats.unitSize == UnitSize.SingleUnit)
                        swinging = math.min(swinging, Model.SingleUnitContactCapacity / (float)math.max(1, SquadsInContactWith(ref target, own)));

                    // ShieldedStanceSwitchSystem: a defensive squad trades half its attack for half again its defence.
                    int meleeAttack = q.squadStats.MeleeAttack - (q.DefensiveStance ? q.squadStats.MeleeAttack / 2 : 0) + (q.ChargeWindow > 0 ? q.ChargeBonus : 0);
                    int meleeDefense = target.squadStats.MeleeDefense + (target.DefensiveStance ? target.squadStats.MeleeDefense / 2 : 0);
                    if (flanking) meleeDefense = (int)(meleeDefense * 0.5f);
                    int hitChance = TabletopTavernConstants.MELEE_BASE_HIT_CHANCE + (meleeAttack - meleeDefense) * TabletopTavernConstants.MELEE_HIT_CHANCE_PER_POINT;
                    hitChance = math.clamp(hitChance, TabletopTavernConstants.MELEE_HIT_CHANCE_MIN, TabletopTavernConstants.MELEE_HIT_CHANCE_MAX);

                    float expectedHits = swinging * (Model.Dt / cooldown) * (hitChance / 100f) + q.HitCarry;
                    int hits = (int)math.floor(expectedHits);
                    q.HitCarry = expectedHits - hits;

                    int weaponStrength = WeaponStrength(ref q, foes) + (q.ChargeWindow > 0 ? q.ChargeBonus : 0);
                    if (flanking && q.squadStats.SquadAttributes.BackStabbers) weaponStrength *= 2;
                    int exposed = Exposed(ref target, flanking);
                    bool monster = q.squadStats.unitSize == UnitSize.Monstrous || q.squadStats.unitSize == UnitSize.SingleUnit;

                    for (int h = 0; h < hits; h++)
                    {
                        if (!Standing(ref target)) break;
                        int victim = PickMeleeVictim(ref target, exposed);
                        int damage = MeleeHitDamage(weaponStrength, ref q.squadStats, ref target, flanking, damageBonus);
                        Hit(ref target, victim, damage, ref q, foes);
                        if (flanking) target.FlankedTimer = 2f;
                        if (monster && !IsLarge(ref target) && q.squadStats.ExplosionDamage > 0)
                        {
                            int splash = math.min(Model.MonsterSplashModels, target.UnitsAlive);
                            int splashDamage = MeleeHitDamage(q.squadStats.ExplosionDamage, ref q.squadStats, ref target, false, damageBonus);
                            for (int s = 0; s < splash && Standing(ref target); s++)
                                Hit(ref target, UnityEngine.Random.Range(0, math.min(exposed, target.UnitsAlive)), splashDamage, ref q, foes);
                            target.ThrownModels = math.min(target.UnitsAlive, target.ThrownModels + splash);
                        }
                    }
                }
            }
        }

        private static int PickMeleeVictim(ref AutoResolveSquad target, int exposed)
        {
            int pool = math.min(exposed, target.UnitsAlive);
            if (UnityEngine.Random.value >= Model.MeleeFocus) return UnityEngine.Random.Range(0, pool);
            int pick = 0;
            for (int i = 1; i < pool; i++) if (target.UnitHealth[i] < target.UnitHealth[pick]) pick = i;
            return pick;
        }

        private static int SquadsInContactWith(ref AutoResolveSquad target, AutoResolveSquad[] attackers)
        {
            int n = 0;
            for (int j = 0; j < attackers.Length; j++)
                if (Standing(ref attackers[j]) && (attackers[j].ContactIndex == target.SquadIndex || target.ContactIndex == attackers[j].SquadIndex)) n++;
            return n;
        }

        // Models able to swing this second: the front rank less anyone airborne.
        private static int Attackers(ref AutoResolveSquad q, int contacts = 1)
        {
            if (q.squadStats.unitSize == UnitSize.SingleUnit) return math.min(1, q.UnitsAlive);
            int front = (int)math.ceil(q.FormationWidth * Model.FrontFraction * (1f + Model.ExtraContactFront * math.max(0, contacts - 1)));
            return math.max(0, math.min(q.UnitsAlive - q.ThrownModels, front));
        }

        private static int Exposed(ref AutoResolveSquad q, bool flanked)
        {
            int front = q.squadStats.unitSize == UnitSize.SingleUnit ? 1 : (int)math.ceil(q.FormationWidth * Model.FrontFraction);
            if (flanked) front = (int)math.ceil(front * Model.FlankedExposure);
            return math.max(1, math.min(q.UnitsAlive, front));
        }

        /// <summary>
        /// CrashingHordeSystem: every other Gruntkin squad above half health adds WeaponStrength, capped.
        /// Player side only (TJ, 2026-09-21): the enemy horde fights without it so auto-resolve never
        /// reads harder than the old model against Gruntkin.
        /// </summary>
        private static void CrashingHorde(AutoResolveSquad[] player)
        {
            RaceBonusRuleData.CrashingHordeConfig config = RaceBonusRuleData.CrashingHorde;
            int healthy = 0;
            for (int i = 0; i < player.Length; i++)
                if (player[i].Race == Race.Gruntkin && HordeHealthy(ref player[i], config.HealthThreshold)) healthy++;
            for (int i = 0; i < player.Length; i++)
            {
                ref AutoResolveSquad q = ref player[i];
                if (q.Race != Race.Gruntkin) { q.HordeStacks = 0; continue; }
                int others = HordeHealthy(ref q, config.HealthThreshold) ? healthy - 1 : healthy;
                q.HordeStacks = math.min(others, config.MaxStacks);
            }
        }

        private static bool HordeHealthy(ref AutoResolveSquad q, float threshold) =>
            !q.Broken && q.UnitsAlive > 0 && q.finalHealth > q.maxUnits * q.healthPerKill * threshold;

        private static int WeaponStrength(ref AutoResolveSquad q, AutoResolveSquad[] foes)
        {
            int ws = q.squadStats.WeaponStrength + q.HordeStacks * RaceBonusRuleData.CrashingHorde.WeaponStrengthPerStack;
            float healthShare = q.maxUnits * q.healthPerKill > 0 ? q.finalHealth / (float)(q.maxUnits * q.healthPerKill) : 1f;
            // RageSystem: weapon strength doubles once the squad is under half health.
            if (q.squadStats.SquadAttributes.Rage && healthShare < 0.5f) ws += q.squadStats.WeaponStrength;
            // BloodFrenzySystem: doubled while the squad is above half health.
            if (q.squadStats.SquadAttributes.BloodFrenzy && healthShare > 0.5f) ws += q.squadStats.WeaponStrength;
            // SlayerSystem: doubled for the whole battle when the enemy fields a monstrous squad.
            if (q.squadStats.SquadAttributes.MonsterSlayer)
                for (int j = 0; j < foes.Length; j++)
                    if (foes[j].squadStats.unitSize == UnitSize.Monstrous || foes[j].squadStats.unitSize == UnitSize.SingleUnit) { ws += q.squadStats.WeaponStrength; break; }
            if (q.squadStats.SquadAttributes.ThrowingAxes) ws = (int)(ws * 1.15f);
            return ws;
        }

        private static int MeleeHitDamage(int weaponStrength, ref SquadStats attacker, ref AutoResolveSquad target, bool flanking, float damageBonus)
        {
            int damage = ApplyPhysicalModifiers(weaponStrength, ref attacker, ref target, true);
            damage = (int)(damage * TabletopTavernConstants.MELEE_TOTAL_DAMAGE_MODIFIER * damageBonus);
            return ApplyTakenMultiplier(damage, ref target);
        }

        // ApplyDamageSystem's physical path: armour (halved by armour piercing, and again by a sunder),
        // anti-infantry and anti-large doubling, melee into artillery doubling.
        private static int ApplyPhysicalModifiers(int damage, ref SquadStats attacker, ref AutoResolveSquad target, bool melee)
        {
            if (target.armorMitigation > 0f)
            {
                float mitigation = target.armorMitigation;
                if (attacker.SquadAttributes.ArmorPiercing) mitigation *= 0.5f;
                if (attacker.SquadAttributes.ArmorSundering || attacker.SquadAttributes.Emblazing) mitigation *= 0.5f;
                damage -= (int)(damage * mitigation);
            }
            if (target.squadStats.unitSize == UnitSize.Infantry && attacker.SquadAttributes.AntiInfantry) damage *= 2;
            if (target.squadStats.unitSize != UnitSize.Infantry && attacker.SquadAttributes.AntiLarge) damage *= 2;
            if (melee && target.squadStats.unitType == UnitType.Artillery) damage *= 2;
            return damage;
        }

        /// <summary>One hit through the physical pipeline and the mark or brace multiplier.</summary>
        internal static int ModifyHit(int damage, ref SquadStats attacker, ref AutoResolveSquad target, bool melee)
        {
            damage = ApplyPhysicalModifiers(damage, ref attacker, ref target, melee);
            return ApplyTakenMultiplier(damage, ref target);
        }

        /// <summary>Restores health to the most wounded living models first, never above per-model max.</summary>
        internal static void Heal(ref AutoResolveSquad q, int amount)
        {
            if (amount <= 0 || q.healthPerKill <= 0) return;
            if (!q.SimReady)
            {
                int maxHealth = q.maxUnits * q.healthPerKill;
                q.finalHealth = math.min(maxHealth, q.finalHealth + amount);
                q.UnitsAlive = math.min(q.maxUnits, (int)math.ceil(q.finalHealth / (float)q.healthPerKill));
                return;
            }
            while (amount > 0)
            {
                int pick = -1, lowest = int.MaxValue;
                for (int i = 0; i < q.UnitsAlive; i++)
                    if (q.UnitHealth[i] < q.healthPerKill && q.UnitHealth[i] < lowest) { lowest = q.UnitHealth[i]; pick = i; }
                if (pick == -1) break;
                int give = math.min(amount, q.healthPerKill - q.UnitHealth[pick]);
                q.UnitHealth[pick] += give;
                q.finalHealth += give;
                amount -= give;
            }
        }

        private static int ApplyTakenMultiplier(int damage, ref AutoResolveSquad target)
        {
            if (target.damageTakenMultiplier != 1f && target.damageTakenMultiplier > 0f)
                damage = math.max(1, (int)(damage * target.damageTakenMultiplier));
            return damage;
        }

        // One model takes one hit. A model at zero dies, the last living model steps into its slot,
        // and the kill is credited to the striking squad.
        private static void Hit(ref AutoResolveSquad target, int victim, int damage, ref AutoResolveSquad striker, AutoResolveSquad[] targetSide)
        {
            if (damage <= 0 || target.UnitsAlive <= 0) return;
            victim = math.clamp(victim, 0, target.UnitsAlive - 1);
            int before = target.UnitHealth[victim];
            int dealt = math.min(before, damage);
            target.UnitHealth[victim] = before - dealt;
            target.finalHealth -= dealt;
            target.TickLoss += dealt;
            striker.TickDealt += dealt;
            if (target.UnitHealth[victim] <= 0)
            {
                int last = target.UnitsAlive - 1;
                target.UnitHealth[victim] = target.UnitHealth[last];
                target.UnitHealth[last] = 0;
                target.UnitsAlive--;
                if (target.ThrownModels > target.UnitsAlive) target.ThrownModels = target.UnitsAlive;
                striker.UnitsSlain++;
            }
        }

        #endregion

        #region Morale and bookkeeping

        // MoraleUpdateJob, one second at a time. Recent loss is the health lost in the last five
        // seconds as a share of max health, in percent points, exactly as HealthLossTrackingSystem
        // reports it.
        private static void UpdateMorale(AutoResolveSquad[] own, AutoResolveSquad[] foes, bool aiSide)
        {
            // BalanceOfPowerSystem: max health counts every squad, current health only the ones still
            // standing, so each squad that breaks pushes the side toward the army-losses collapse.
            float ownMax = 0, ownCurrent = 0;
            for (int i = 0; i < own.Length; i++)
            {
                ownMax += own[i].maxUnits * (float)own[i].healthPerKill;
                if (Standing(ref own[i])) ownCurrent += math.max(0, own[i].finalHealth);
            }
            bool armyLosses = ownMax > 0 && ownCurrent <= ownMax * Model.ArmyLossesShare;

            for (int i = 0; i < own.Length; i++)
            {
                ref AutoResolveSquad q = ref own[i];
                if (!Standing(ref q)) continue;
                if (armyLosses) q.ArmyLosses = true;

                q.RecentLoss[q.RingIndex] = q.TickLoss;
                q.RecentDealt[q.RingIndex] = q.TickDealt;
                float loss5 = 0, dealt5 = 0;
                for (int r = 0; r < 5; r++) { loss5 += q.RecentLoss[r]; dealt5 += q.RecentDealt[r]; }
                float maxHealth = math.max(1f, q.maxUnits * (float)q.healthPerKill);
                float recentLossPercent = loss5 / maxHealth * 100f;
                float healthShare = math.max(0f, q.finalHealth) / maxHealth;

                bool terrified = false;
                if (!q.squadStats.SquadAttributes.Stalwart && !q.squadStats.SquadAttributes.Terrifying)
                    for (int j = 0; j < foes.Length; j++)
                        if (Standing(ref foes[j]) && foes[j].squadStats.SquadAttributes.Terrifying && Distance(ref q, ref foes[j]) <= TabletopTavernConstants.TERROR_RADIUS) { terrified = true; break; }

                float discrepancy = dealt5 - loss5;
                bool winning = discrepancy > 250f, losing = discrepancy < -250f;

                // EnemyGeneral.CheckShieldedSquadsForDefensiveSwitch: an enemy shielded squad that is
                // losing a melee goes defensive for the rest of the battle.
                if (aiSide && losing && q.ContactIndex != -1 && !q.DefensiveStance
                    && (q.squadStats.SquadAttributes.StandardShields || q.squadStats.SquadAttributes.HeavyShields))
                    q.DefensiveStance = true;

                float modifier = 0f;
                if (recentLossPercent > 0f) modifier -= TabletopTavernConstants.MORALE_RECENT_HEALTH_LOSS_PENALTY * recentLossPercent;
                else modifier += TabletopTavernConstants.MORALE_NO_RECENT_HEALTH_LOSS_REGENERATION;
                modifier -= TabletopTavernConstants.MORALE_TOTAL_HEALTH_DEPLETION_PENALTY * (1f - healthShare);
                if (q.FlankedTimer > 0f) modifier -= TabletopTavernConstants.MORALE_FLANK_PENALTY;
                if (q.RetreatingAlliesTimer > 0f) modifier -= TabletopTavernConstants.MORALE_RETREATING_ALLIES_PENALTY;
                if (terrified) modifier -= TabletopTavernConstants.MORALE_THREAT_PENALTY;
                if (q.ArmyLosses) modifier -= TabletopTavernConstants.MORALE_ARMY_LOSSES_PENALTY;
                if (winning) modifier += TabletopTavernConstants.MORALE_WINNING_BONUS;
                if (losing) modifier += TabletopTavernConstants.MORALE_LOSING_PENALTY;
                if (q.OnFireTimer > 0f) modifier -= TabletopTavernConstants.MORALE_FIRE_DAMAGE_PENALTY;
                modifier *= TabletopTavernConstants.MORALE_LOSS_MODIFIER;

                q.Morale = math.clamp(q.Morale + modifier * Model.Dt, 0f, q.squadStats.Leadership);
                if (q.Morale <= Model.MoraleBreak)
                {
                    q.Broken = true;
                    q.ContactIndex = -1;
                    for (int k = 0; k < own.Length; k++) if (k != i) own[k].RetreatingAlliesTimer = Model.RetreatingAlliesSeconds;
                }
            }
        }

        private static void EndOfTick(AutoResolveSquad[] squads)
        {
            for (int i = 0; i < squads.Length; i++)
            {
                ref AutoResolveSquad q = ref squads[i];
                if (!q.SimReady) continue;
                q.RingIndex = (q.RingIndex + 1) % 5;
                q.TickLoss = 0; q.TickDealt = 0;
                q.ChargeWindow = math.max(0f, q.ChargeWindow - Model.Dt);
                q.FlankedTimer = math.max(0f, q.FlankedTimer - Model.Dt);
                q.RetreatingAlliesTimer = math.max(0f, q.RetreatingAlliesTimer - Model.Dt);
                q.OnFireTimer = math.max(0f, q.OnFireTimer - Model.Dt);
                // Thrown models land again after the throw time; half of them per second is close enough.
                q.ThrownModels = math.max(0, q.ThrownModels - (int)math.ceil(q.ThrownModels * (Model.Dt / Model.ThrowSeconds)));
                if (q.UnitsAlive <= 0) { q.Broken = true; q.finalHealth = math.min(q.finalHealth, 0); }
                // A squad that has left the field keeps its wounds from bleeding into finalHealth reads.
                if (q.Broken) { q.ContactIndex = -1; }
            }
        }

        /// <summary>
        /// Applies damage queued by the mage alpha strike (SquadIndex of striker, SquadIndex of target,
        /// damage). Spell damage is area damage, so it is spread across the target's models front first.
        /// </summary>
        private static void ApplyPooledDamage(AutoResolveSquad[] player, AutoResolveSquad[] enemy, List<int3> damageLog)
        {
            if (damageLog == null || damageLog.Count == 0) return;
            foreach (int3 entry in damageLog)
            {
                int strikerSide = IndexOf(player, entry.x) != -1 ? 0 : 1;
                AutoResolveSquad[] strikers = strikerSide == 0 ? player : enemy;
                AutoResolveSquad[] targets = strikerSide == 0 ? enemy : player;
                int s = IndexOf(strikers, entry.x), t = IndexOf(targets, entry.y);
                if (t == -1) continue;
                ref AutoResolveSquad target = ref targets[t];
                int remaining = entry.z;
                int guard = 0;
                while (remaining > 0 && target.UnitsAlive > 0 && guard++ < 100000)
                {
                    int victim = UnityEngine.Random.Range(0, target.UnitsAlive);
                    int chunk = math.min(remaining, math.max(1, target.UnitHealth[victim]));
                    if (s != -1) Hit(ref target, victim, chunk, ref strikers[s], targets);
                    else
                    {
                        AutoResolveSquad dummy = default;
                        dummy.UnitHealth = null;
                        Hit(ref target, victim, chunk, ref dummy, targets);
                    }
                    remaining -= chunk;
                }
            }
            damageLog.Clear();
        }

        /// <summary>Survivors leave the field healed, as the live post-battle save does.</summary>
        internal static void Settle(AutoResolveSquad[] squads)
        {
            if (squads == null) return;
            for (int i = 0; i < squads.Length; i++)
            {
                if (!squads[i].SimReady) continue;
                squads[i].finalHealth = squads[i].UnitsAlive * squads[i].healthPerKill;
            }
        }

        #endregion
    }
}
