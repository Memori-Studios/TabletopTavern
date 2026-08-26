using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using TJ.Morale;

namespace TJ
{
    /// <summary>
    /// Manages all enemy ranged unit behaviour during battle.
    ///
    /// Responsibilities:
    ///   - Set all ranged squads to Volley fire at battle start to conserve ammunition.
    ///   - Switch individual archers to FireAtWill once the player closes within range.
    ///   - Trigger a mid-battle desperation phase (FireAtWill) at a configurable health threshold.
    ///   - Reprioritize artillery to focus player ranged units.
    ///   - Reprioritize archers to prefer unshielded player squads and clear stale orders.
    ///   - Switch all ranged to FireAtWill when 75 % army health is lost (army losses event).
    /// </summary>
    public class EnemyRangedBehavior : MonoBehaviour
    {
        [Header("Desperation Phase")]
        [Tooltip("Enemy health ratio below which archers switch from Volley to FireAtWill.")]
        [SerializeField] private float _desperationHealthThreshold = 0.60f;

        [Header("Volley → FireAtWill Switch")]
        [Tooltip("Fraction of attack range within which a player squad triggers the switch.")]
        [SerializeField] private float _volleySwitchRangeFraction = 0.6f;

        private EntityManager _entityManager;
        private EntityQuery   _enemySquadQuery;
        private EntityQuery   _playerSquadQuery;
        private EntityQuery   _balanceOfPowerQuery;

        private readonly List<Entity> _artillerySquads = new();
        private readonly List<Entity> _archerSquads    = new();

        private bool _desperationPhaseTriggered;
        private bool _armyLossesFireAtWillTriggered;

        #region Lifecycle

        public void SetUp()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _enemySquadQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<EnemySquad>(), ComponentType.ReadOnly<SquadEntity>() },
                None = new[] { ComponentType.ReadOnly<BrokenSquadTag>() }
            });
            _playerSquadQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<PlayerSquad>(), ComponentType.ReadOnly<SquadEntity>() },
                None = new[] { ComponentType.ReadOnly<BrokenSquadTag>() }
            });
            _balanceOfPowerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BalanceOfPower>());
        }

        public void TearDown()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _enemySquadQuery.Dispose();
                _playerSquadQuery.Dispose();
                _balanceOfPowerQuery.Dispose();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Scans all enemy squads and builds the artillery and archer tracking lists.
        /// Must be called once during DetermineStartingState, before ArmyHasArtillery().
        /// </summary>
        public void InitialiseSquadLists()
        {
            _artillerySquads.Clear();
            _archerSquads.Clear();

            NativeArray<Entity> enemyEntities = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in enemyEntities)
            {
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                UnitType    unitType    = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadEntity.UnitName);
                // Hybrids carry RangedSquad + RangedFireModeSquadComponent, so every archer brain
                // below (volley escalation, desperation phase, retargeting) applies to them.
                if (unitType == UnitType.Artillery) _artillerySquads.Add(entity);
                else if (TabletopTavernConstants.FightsAtRange(unitType)) _archerSquads.Add(entity);
            }
            enemyEntities.Dispose();
        }

        /// <summary>
        /// Sets all enemy ranged squads to Volley fire to conserve ammo during the approach.
        /// Call once at battle start.
        /// </summary>
        public void SetToVolleyFire()
        {
            NativeArray<Entity> enemyEntities = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in enemyEntities)
            {
                if (!_entityManager.HasComponent<RangedFireModeSquadComponent>(entity)) continue;
                RangedFireModeSquadComponent fireMode = _entityManager.GetComponentData<RangedFireModeSquadComponent>(entity);
                fireMode.FireMode        = RangedFireMode.Volley;
                fireMode.SwitchRequested = true;
                _entityManager.SetComponentData(entity, fireMode);
            }
            enemyEntities.Dispose();
        }

        /// <summary>Returns true if the enemy army contains at least one artillery squad.</summary>
        public bool ArmyHasArtillery() => _artillerySquads.Count > 0;

        /// <summary>
        /// Per-evaluation-tick update: desperation phase check and Volley → FireAtWill switching.
        /// Call every UpdateAggressive tick.
        /// </summary>
        public void Tick()
        {
            CheckDesperationPhase();
            CheckVolleyToFireAtWillSwitch();
        }

        /// <summary>
        /// Reprioritizes artillery and archer targets. Call every Aggressive and Passive tick.
        /// </summary>
        public void ReprioritizeTargets()
        {
            ReprioritizeArtillery();
            ReprioritizeArchers();
        }

        /// <summary>
        /// Switches all ranged squads to FireAtWill when the enemy army loses 75 % of its health.
        /// Wired up by EnemyGeneral via BalanceOfPowerDisplay.ArmyLossesTriggered.
        /// </summary>
        public void OnArmyLossesTriggered(Team team)
        {
            if (team != Team.Enemy || _armyLossesFireAtWillTriggered) return;
            _armyLossesFireAtWillTriggered = true;

            NativeArray<Entity> enemyEntities = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in enemyEntities)
            {
                if (!_entityManager.Exists(entity)) continue;

                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                if (!TabletopTavernConstants.FightsAtRange(TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadEntity.UnitName))) continue;
                if (!_entityManager.HasComponent<RangedFireModeSquadComponent>(squadEntity.SelfEntity)) continue;

                SetSquadToFireAtWill(squadEntity.SelfEntity);

                if (!_entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity)) continue;
                SquadOverridesComponent overrides = _entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
                overrides.FireMode = RangedFireMode.FireAtWill;
                _entityManager.SetComponentData(squadEntity.SelfEntity, overrides);
            }
            enemyEntities.Dispose();
        }

        #endregion

        #region Fire Mode Management

        /// <summary>
        /// When enemy health falls below the desperation threshold, switches all remaining
        /// archers to FireAtWill for a mid-battle escalation before the 75 % army loss event.
        /// </summary>
        private void CheckDesperationPhase()
        {
            if (_desperationPhaseTriggered) return;

            BalanceOfPower bop   = _balanceOfPowerQuery.GetSingleton<BalanceOfPower>();
            float          ratio = bop.EnemyMaxHealth > 0 ? bop.EnemyCurrentHealth / bop.EnemyMaxHealth : 1f;
            if (ratio > _desperationHealthThreshold) return;

            _desperationPhaseTriggered = true;
            Debug.Log($"[EnemyRangedBehavior] Desperation phase at {ratio * 100f:F0}% health — archers switching to FireAtWill");

            for (int i = _archerSquads.Count - 1; i >= 0; i--)
            {
                Entity entity = _archerSquads[i];
                if (!_entityManager.Exists(entity)) { _archerSquads.RemoveAt(i); continue; }
                SetSquadToFireAtWill(entity);
            }
        }

        /// <summary>
        /// Switches each archer individually from Volley to FireAtWill once a player squad
        /// enters within the configured fraction of that archer's attack range.
        /// </summary>
        private void CheckVolleyToFireAtWillSwitch()
        {
            NativeArray<Entity> playerEntities = _playerSquadQuery.ToEntityArray(Allocator.Temp);

            for (int i = _archerSquads.Count - 1; i >= 0; i--)
            {
                Entity archerEntity = _archerSquads[i];
                if (!_entityManager.Exists(archerEntity))                                     { _archerSquads.RemoveAt(i); continue; }
                if (!_entityManager.HasComponent<RangedFireModeSquadComponent>(archerEntity)) continue;
                if (!_entityManager.HasComponent<RangedSquad>(archerEntity))                  continue;

                RangedFireModeSquadComponent fireMode =
                    _entityManager.GetComponentData<RangedFireModeSquadComponent>(archerEntity);
                if (fireMode.FireMode == RangedFireMode.FireAtWill) continue;

                float3 archerCenter    = _entityManager.GetComponentData<SquadMovementComponent>(archerEntity).SquadCenter;
                float  attackRange     = _entityManager.GetComponentData<RangedSquad>(archerEntity).AttackRange;
                float  switchThreshold = attackRange * _volleySwitchRangeFraction;

                foreach (Entity playerEntity in playerEntities)
                {
                    float dist = math.distance(archerCenter,
                        _entityManager.GetComponentData<SquadMovementComponent>(playerEntity).SquadCenter);
                    if (dist > switchThreshold) continue;

                    // Debug.Log($"[EnemyRangedBehavior] Archer switching to FireAtWill — player {dist:F1} units away (threshold {switchThreshold:F1})");
                    SetSquadToFireAtWill(archerEntity);
                    break;
                }
            }

            playerEntities.Dispose();
        }

        #endregion

        #region Target Reprioritization

        /// <summary>
        /// If an artillery squad's current target is not a ranged unit, redirects it to the
        /// nearest player ranged squad within its attack range.
        /// </summary>
        private void ReprioritizeArtillery()
        {
            for (int i = _artillerySquads.Count - 1; i >= 0; i--)
            {
                Entity artilleryEntity = _artillerySquads[i];

                if (!_entityManager.Exists(artilleryEntity))                      { _artillerySquads.RemoveAt(i); continue; }
                if (_entityManager.HasComponent<BrokenSquadTag>(artilleryEntity)) { _artillerySquads.RemoveAt(i); continue; }
                if (!_entityManager.HasComponent<RangedSquad>(artilleryEntity))   { _artillerySquads.RemoveAt(i); continue; }

                Entity currentTarget   = _entityManager.GetComponentData<SquadEntity>(artilleryEntity).TargetSquadEntity;
                float3 artilleryCenter = _entityManager.GetComponentData<SquadMovementComponent>(artilleryEntity).SquadCenter;
                float  attackRange     = _entityManager.GetComponentData<RangedSquad>(artilleryEntity).AttackRange;

                // Distance to the current target only if it is a valid in-range ranged squad.
                float currentTargetDist = float.MaxValue;
                if (currentTarget != Entity.Null && _entityManager.Exists(currentTarget)
                    && _entityManager.HasComponent<RangedSquad>(currentTarget))
                {
                    float d = math.distance(artilleryCenter,
                        _entityManager.GetComponentData<SquadMovementComponent>(currentTarget).SquadCenter);
                    if (d <= attackRange) currentTargetDist = d;
                }

                // Find the CLOSEST player ranged squad within range.
                NativeArray<Entity> playerEntities = _playerSquadQuery.ToEntityArray(Allocator.Temp);
                int   bestTargetId = 0;
                float bestDist     = float.MaxValue;
                foreach (Entity playerEntity in playerEntities)
                {
                    if (!_entityManager.HasComponent<RangedSquad>(playerEntity)) continue;
                    float3 playerCenter = _entityManager.GetComponentData<SquadMovementComponent>(playerEntity).SquadCenter;
                    float  dist         = math.distance(artilleryCenter, playerCenter);
                    if (dist > attackRange) continue;

                    if (dist < bestDist)
                    {
                        bestDist     = dist;
                        bestTargetId = _entityManager.GetComponentData<SquadEntity>(playerEntity).SquadId;
                    }
                }
                playerEntities.Dispose();

                if (bestTargetId == 0) continue; // no ranged target in range -> keep current

                // No valid ranged target yet -> redirect onto the closest ranged squad. Already on a
                // ranged squad -> only switch if the alternative is meaningfully closer (hysteresis).
                bool hasValidRangedTarget = currentTargetDist < float.MaxValue;
                if (!hasValidRangedTarget ||
                    bestDist < currentTargetDist * TabletopTavernConstants.RANGED_REPRIORITIZE_CLOSER_FRACTION)
                {
                    IssueAttackOrder(artilleryEntity, bestTargetId);
                }
            }
        }

        /// <summary>
        /// For each archer without a valid in-range unshielded target, finds the nearest
        /// qualifying player squad and issues an attack order. Clears stale orders when the
        /// current target moves out of range or raises shields.
        /// </summary>
        private void ReprioritizeArchers()
        {
            for (int i = _archerSquads.Count - 1; i >= 0; i--)
            {
                Entity archerEntity = _archerSquads[i];

                if (!_entityManager.Exists(archerEntity))                                  { _archerSquads.RemoveAt(i); continue; }
                if (_entityManager.HasComponent<BrokenSquadTag>(archerEntity))             { _archerSquads.RemoveAt(i); continue; }
                if (!_entityManager.HasComponent<RangedSquad>(archerEntity))               { _archerSquads.RemoveAt(i); continue; }

                // Skip squads already engaged or being repositioned.
                if (_entityManager.HasComponent<InCombat>(archerEntity))                       continue;
                if (_entityManager.HasComponent<FormationEngagedInRangedCombat>(archerEntity)) continue;
                if (_entityManager.IsComponentEnabled<DisengageFromCombat>(archerEntity))      continue;
                if (_entityManager.HasComponent<SquadMoveOverrideTag>(archerEntity))           continue;

                float3 archerCenter = _entityManager.GetComponentData<SquadMovementComponent>(archerEntity).SquadCenter;
                float  attackRange  = _entityManager.GetComponentData<RangedSquad>(archerEntity).AttackRange;

                // Distance to the current target if it is still a valid (in-range, unshielded) priority.
                float currentTargetDist = CurrentPriorityTargetDistance(archerEntity, archerCenter, attackRange);

                // Find the CLOSEST qualifying (in-range, unshielded) player squad.
                NativeArray<Entity> playerEntities = _playerSquadQuery.ToEntityArray(Allocator.Temp);
                int   bestTargetId = 0;
                float bestDist     = float.MaxValue;
                foreach (Entity playerEntity in playerEntities)
                {
                    if (_entityManager.GetComponentData<ShieldedStanceSquadComponent>(playerEntity).Stance != ShieldedStance.None) continue;

                    float3 playerCenter = _entityManager.GetComponentData<SquadMovementComponent>(playerEntity).SquadCenter;
                    float  dist         = math.distance(archerCenter, playerCenter);
                    if (dist > attackRange) continue;

                    if (dist < bestDist)
                    {
                        bestDist     = dist;
                        bestTargetId = _entityManager.GetComponentData<SquadEntity>(playerEntity).SquadId;
                    }
                }
                playerEntities.Dispose();

                if (bestTargetId == 0) continue; // nothing qualifies

                // No valid current target -> take the closest. Valid current target -> only steal aim if
                // the alternative is meaningfully closer (hysteresis prevents frame-to-frame flip-flop).
                bool hasValidTarget = currentTargetDist < float.MaxValue;
                if (!hasValidTarget ||
                    bestDist < currentTargetDist * TabletopTavernConstants.RANGED_REPRIORITIZE_CLOSER_FRACTION)
                {
                    IssueAttackOrder(archerEntity, bestTargetId);
                }
            }
        }

        /// <summary>
        /// Returns the distance to the archer's current target if it is a valid priority
        /// (exists, in range, unshielded); otherwise float.MaxValue. Clears the archer's queued
        /// orders if the target has moved out of range.
        /// </summary>
        private float CurrentPriorityTargetDistance(Entity archerEntity, float3 archerCenter, float attackRange)
        {
            Entity targetEntity = _entityManager.GetComponentData<SquadEntity>(archerEntity).TargetSquadEntity;
            if (targetEntity == Entity.Null || !_entityManager.Exists(targetEntity)) return float.MaxValue;

            ShieldedStance targetStance =
                _entityManager.GetComponentData<ShieldedStanceSquadComponent>(targetEntity).Stance;
            if (targetStance != ShieldedStance.None) return float.MaxValue;

            float3 targetCenter = _entityManager.GetComponentData<SquadMovementComponent>(targetEntity).SquadCenter;
            float  dist         = math.distance(archerCenter, targetCenter);
            if (dist <= attackRange) return dist;

            // Target drifted out of range — clear the stale attack order.
            _entityManager.GetBuffer<QueuedOrder>(archerEntity).Clear();
            return float.MaxValue;
        }

        #endregion

        #region Utility

        private void IssueAttackOrder(Entity squadEntity, int targetSquadId)
        {
            DynamicBuffer<QueuedOrder> orders = _entityManager.GetBuffer<QueuedOrder>(squadEntity);
            orders.Clear();
            orders.Add(new QueuedOrder { Type = QueuedOrderType.Attack, TargetSquadId = targetSquadId });
            _entityManager.SetComponentEnabled<WaitingForCommand>(squadEntity, false);
        }

        private void SetSquadToFireAtWill(Entity squadEntity)
        {
            if (!_entityManager.HasComponent<RangedFireModeSquadComponent>(squadEntity)) return;
            RangedFireModeSquadComponent fireMode =
                _entityManager.GetComponentData<RangedFireModeSquadComponent>(squadEntity);
            fireMode.FireMode        = RangedFireMode.FireAtWill;
            fireMode.SwitchRequested = true;
            _entityManager.SetComponentData(squadEntity, fireMode);
        }

        #endregion
    }
}
