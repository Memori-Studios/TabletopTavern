using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using TJ.Morale;

namespace TJ
{
    /// <summary>
    /// Orchestrates the enemy AI during battle via a simple state machine.
    ///
    /// States:
    ///   Aggressive        – all squads committed; cavalry flanks once the player closes in.
    ///   Passive           – army holds until the player approaches or deals damage.
    ///   DelayedPassive    – Passive for up to 10 seconds, then forced Aggressive.
    ///   DelayedAggressive – idles for 1 second to let units finish teleporting, then goes Aggressive.
    ///   Garrison          – holds position until a gate is breached.
    ///
    /// Ranged unit behaviour is fully delegated to EnemyRangedBehavior.
    /// Cavalry flanking behaviour is fully delegated to EnemyCavalryBehavior.
    /// Both components must be attached to the same GameObject.
    /// </summary>
    [RequireComponent(typeof(EnemyRangedBehavior))]
    [RequireComponent(typeof(EnemyCavalryBehavior))]
    public class EnemyGeneral : MonoBehaviour
    {
        private enum EnemyGeneralState
        {
            Aggressive,
            Passive,
            DelayedPassive,
            DelayedAggressive,
            Garrison
        }

        [Header("State Machine")]
        [SerializeField] private EnemyGeneralState _currentState;
        [SerializeField] private float stateEvaluationInterval   = 1f;
        [SerializeField] private float aggressionTriggerDistance  = 20f;

        private EnemyRangedBehavior  _rangedBehavior;
        private EnemyCavalryBehavior _cavalryBehavior;

        private EntityManager _entityManager;
        private EntityQuery   _battleHasStartedQuery;
        private EntityQuery   _enemySquadQuery;
        private EntityQuery   _playerSquadQuery;
        private EntityQuery   _balanceOfPowerQuery;

        private float _stateEvaluationTimer;
        private int   _delayedPassiveTimer;
        private int   _delayedAggressiveTimer;
        private bool  _battleHasStarted;
        private bool  _battleHasEnded;
        private bool  _setup;
        private bool  _isRiverCrossing;
        private bool  _isGarrisonBattle;

        // Shielded squads monitored for combat deterioration in the Aggressive state.
        private readonly List<Entity> _shieldedEnemySquads = new();

        // Garrison gate tracking: built once at first UpdateGarrison tick, then used to release squads per gate.
        private readonly Dictionary<int, List<Entity>> _garrisonDefendersByGate = new();
        private readonly HashSet<int> _processedBreachedGates = new();
        private bool _garrisonCacheBuilt;

        [Header("Garrison Under Fire")]
        [Tooltip("Share of max health a holding defender must lose before it reacts to being hit from range.")]
        [SerializeField] private float _underFireHealthLoss = 0.05f;
        [Tooltip("Extra distance kept beyond an attacker's range when pulling back. An engaged ranged squad keeps firing 10 past its range.")]
        [SerializeField] private float _garrisonRangeMargin = 20f;
        [Tooltip("How far inside its own range a defender stops when stepping up to return fire.")]
        [SerializeField] private float _garrisonStepUpMargin = 10f;
        [Tooltip("Room kept between a moved defender's centre and the walls or the back edge.")]
        [SerializeField] private float _garrisonWallMargin = 8f;
        private readonly Dictionary<Entity, int> _garrisonHealthMarks = new();

        #region Lifecycle

        private void Awake()
        {
            // Resolve sub-component references immediately so external callers such as
            // BattleCleanUpManager can safely delegate to them before SetUp() is called.
            _rangedBehavior  = GetComponent<EnemyRangedBehavior>();
            _cavalryBehavior = GetComponent<EnemyCavalryBehavior>();
        }

        public void SetUp()
        {
            _entityManager         = World.DefaultGameObjectInjectionWorld.EntityManager;
            _battleHasStartedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattleHasStarted>());
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

            _isGarrisonBattle = BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;
            _setup = true;

            _rangedBehavior.SetUp();
            _cavalryBehavior.SetUp();

        }

        public void TearDown()
        {
            _rangedBehavior.TearDown();
            _cavalryBehavior.TearDown();

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _battleHasStartedQuery.Dispose();
                _enemySquadQuery.Dispose();
                _playerSquadQuery.Dispose();
                _balanceOfPowerQuery.Dispose();
            }
            _setup = false;
        }

        private void Update()
        {
            if (!_setup || _battleHasEnded) return;

            if (!_battleHasStarted)
            {
                if (_battleHasStartedQuery.CalculateEntityCount() == 0) return;
                _battleHasStarted = true;
                DetermineStartingState();
            }

            _stateEvaluationTimer -= Time.deltaTime;
            if (_stateEvaluationTimer > 0) return;
            _stateEvaluationTimer = stateEvaluationInterval;

            HandleStateMachine();
        }

        private void OnDestroy()
        {
            if (_setup) TearDown();
            if (!BattleManager.HasInstance || BattleManager.Instance.UIManager == null) return;
        }

        #endregion

        #region Public API

        /// <summary>Called by EntityWatcher when a squad with CavalryFlankingTag is registered.</summary>
        public void MarkSquadForFlanking(int squadId) =>
            _cavalryBehavior.MarkSquadForFlanking(squadId);

        public void SetRiverCrossing(bool isRiverCrossing)
        {
            _isRiverCrossing = isRiverCrossing;
            _cavalryBehavior.SetRiverCrossing(isRiverCrossing);
        }

        #endregion

        #region Battle Start

        public void DetermineStartingState()
        {
            _rangedBehavior.SetToVolleyFire();
            _rangedBehavior.InitialiseSquadLists();
            _cavalryBehavior.DetermineInfantryMarchSpeed();

            if (_isGarrisonBattle)
            {
                Debug.Log("[EnemyGeneral] Starting in Garrison state");
                SetGarrison();
                return;
            }

            if (_isRiverCrossing)
            {
                Debug.Log("[EnemyGeneral] Starting Aggressive (river crossing)");
                SetDelayedAggressive();
                return;
            }

            // Outriders beat artillery on purpose: Passive holds WaitingForCommand on every squad, so an
            // artillery army with outriders left the raiders parked at their spawn points behind the
            // player and the whole army inert, which players reported as "the enemy never moves".
            if (ArmyHasOutriders())
            {
                Debug.Log("[EnemyGeneral] Starting Aggressive — army contains outriders");
                SetDelayedAggressive();
                return;
            }

            if (_rangedBehavior.ArmyHasArtillery())
            {
                Debug.Log("[EnemyGeneral] Starting Passive — army contains artillery");
                SetPassive();
                return;
            }

            if (UnityEngine.Random.value < 0.25f)
            {
                Debug.Log("[EnemyGeneral] Starting DelayedPassive (random chance)");
                SetDelayedPassive();
            }
            else
            {
                Debug.Log("[EnemyGeneral] Starting Aggressive (default)");
                SetDelayedAggressive();
            }
        }

        private bool ArmyHasOutriders()
        {
            NativeArray<Entity> enemyEntities = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            bool hasOutrider = false;
            foreach (Entity entity in enemyEntities)
            {
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                if (TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName).SquadAttributes.Outrider)
                {
                    hasOutrider = true;
                    break;
                }
            }
            enemyEntities.Dispose();
            return hasOutrider;
        }

        #endregion

        #region State Machine

        private void HandleStateMachine()
        {
            EnemyGeneralState newState = EvaluateState();
            if (newState != _currentState)
            {
                ExitState();
                _currentState = newState;
                EnterState(_currentState);
            }
            UpdateState(_currentState);
        }

        private EnemyGeneralState EvaluateState()
        {
            BalanceOfPower bop   = _balanceOfPowerQuery.GetSingleton<BalanceOfPower>();
            float          ratio = bop.EnemyMaxHealth > 0 ? bop.EnemyCurrentHealth / bop.EnemyMaxHealth : 1f;

            // Force-commit to Aggressive if the army is near-dead while sitting passive.
            if (_currentState == EnemyGeneralState.Passive && ratio < 0.35f)
                return EnemyGeneralState.Aggressive;

            return _currentState;
        }

        private void EnterState(EnemyGeneralState state)
        {
            Debug.Log($"[EnemyGeneral] → {state}");
        }

        private void ExitState() { }

        private void UpdateState(EnemyGeneralState state)
        {
            switch (state)
            {
                case EnemyGeneralState.Aggressive:        UpdateAggressive();        break;
                case EnemyGeneralState.Passive:           UpdatePassive();           break;
                case EnemyGeneralState.DelayedPassive:    UpdateDelayedPassive();    break;
                case EnemyGeneralState.DelayedAggressive: UpdateDelayedAggressive(); break;
                case EnemyGeneralState.Garrison:          UpdateGarrison();          break;
            }
        }

        private void UpdateAggressive()
        {
            CheckShieldedSquadsForDefensiveSwitch();
            _rangedBehavior.Tick();
            _cavalryBehavior.Tick();
            _rangedBehavior.ReprioritizeTargets();
        }

        private void UpdatePassive()
        {
            NativeArray<Entity> enemyEntities  = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> playerEntities = _playerSquadQuery.ToEntityArray(Allocator.Temp);

            bool playerIsClose = false;
            foreach (Entity enemyEntity in enemyEntities)
            {
                SquadEntity enemySquadEntity = _entityManager.GetComponentData<SquadEntity>(enemyEntity);
                if (TabletopTavernData.Instance.GetSquadStats(enemySquadEntity.UnitName).SquadAttributes.Outrider) continue;

                float3 enemyCenter = _entityManager.GetComponentData<SquadMovementComponent>(enemyEntity).SquadCenter;
                foreach (Entity playerEntity in playerEntities)
                {
                    float3 playerCenter = _entityManager.GetComponentData<SquadMovementComponent>(playerEntity).SquadCenter;
                    if (math.distance(enemyCenter, playerCenter) <= aggressionTriggerDistance)
                    {
                        playerIsClose = true;
                        break;
                    }
                }
                if (playerIsClose) break;
            }

            enemyEntities.Dispose();
            playerEntities.Dispose();

            if (playerIsClose)
            {
                Debug.Log($"[EnemyGeneral] Player within {aggressionTriggerDistance} units — going Aggressive");
                SetAggressive();
            }
            else if (BattleManager.Instance.UIManager.BalanceOfPowerDisplay.EnemyHasTakenDamage)
            {
                Debug.Log("[EnemyGeneral] Enemy took damage — going Aggressive");
                SetAggressive();
            }

            _rangedBehavior.ReprioritizeTargets();
        }

        private void UpdateDelayedPassive()
        {
            UpdatePassive();
            _delayedPassiveTimer += 1;

            // Prevent the player from avoiding combat indefinitely by forcing Aggressive after 10 ticks.
            if (_delayedPassiveTimer >= 10)
            {
                Debug.Log("[EnemyGeneral] DelayedPassive expired — going Aggressive");
                SetAggressive();
            }
        }

        private void UpdateDelayedAggressive()
        {
            _delayedAggressiveTimer += 1;
            if (_delayedAggressiveTimer >= 1)
            {
                Debug.Log("[EnemyGeneral] DelayedAggressive expired — going Aggressive");
                SetAggressive();
            }
        }

        private void UpdateGarrison()
        {
            if (!_garrisonCacheBuilt)
                BuildGarrisonCache();

            if (_processedBreachedGates.Count > 0)
            {
                CheckShieldedSquadsForDefensiveSwitch();
                _rangedBehavior.Tick();
                _cavalryBehavior.Tick();
                _rangedBehavior.ReprioritizeTargets();
            }
            else
            {
                foreach (int gateIndex in _garrisonDefendersByGate.Keys)
                {
                    if (!BattleManager.Instance.IsGateBreached(gateIndex)) continue;
                    if (_processedBreachedGates.Contains(gateIndex)) continue;
                    ReleaseGateDefenders(gateIndex);
                }

                if (_processedBreachedGates.Count == 0)
                    MoveDefendersUnderFire();
            }
        }

        #region Garrison Under Fire

        // A holding defender never chases, so one hit from beyond its own reach would stand and die.
        private void MoveDefendersUnderFire()
        {
            PositionDrawer positionDrawer = BattleManager.Instance.PositionDrawer;
            if (!positionDrawer.HasGarrisonZone) return;
            GarrisonConcaveZone zone = positionDrawer.GarrisonZone;

            NativeArray<Entity> playerEntities = _playerSquadQuery.ToEntityArray(Allocator.Temp);
            foreach (List<Entity> defenders in _garrisonDefendersByGate.Values)
            {
                foreach (Entity defender in defenders)
                {
                    if (!_entityManager.Exists(defender)) continue;
                    if (_entityManager.HasComponent<BrokenSquadTag>(defender)) continue;

                    int health = _entityManager.GetComponentData<SquadStateComponent>(defender).CurrentHealthValue;
                    if (!_garrisonHealthMarks.TryGetValue(defender, out int mark))
                    {
                        _garrisonHealthMarks[defender] = health;
                        continue;
                    }

                    // A squad already walking finishes that move before it is judged again.
                    if (_entityManager.HasComponent<SquadMoveOverrideTag>(defender))
                    {
                        _garrisonHealthMarks[defender] = health;
                        continue;
                    }

                    int maxHealth = _entityManager.GetComponentData<SquadStateComponent>(defender).MaxHealthValue;
                    if (mark - health < maxHealth * _underFireHealthLoss) continue;
                    _garrisonHealthMarks[defender] = health;

                    OrderDefenderOutOfHarm(defender, playerEntities, zone);
                }
            }
            playerEntities.Dispose();
        }

        private void OrderDefenderOutOfHarm(Entity defender, NativeArray<Entity> playerEntities, GarrisonConcaveZone zone)
        {
            SquadMovementComponent movement = _entityManager.GetComponentData<SquadMovementComponent>(defender);
            float3 center = movement.SquadCenter;
            float ownRange = GetSquadAttackRange(defender);

            bool   underFire       = false;
            float  nearestDistance = float.MaxValue;
            float3 nearestThreat   = default;
            float  safeZ           = center.z;
            foreach (Entity playerEntity in playerEntities)
            {
                float threatRange = GetSquadAttackRange(playerEntity);
                if (threatRange <= 0f) continue;

                float3 threat   = _entityManager.GetComponentData<SquadMovementComponent>(playerEntity).SquadCenter;
                float  distance = math.distance(threat, center);
                if (distance > threatRange + _garrisonRangeMargin) continue;

                underFire = true;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestThreat   = threat;
                }

                float reach = threatRange + _garrisonRangeMargin;
                float dx    = center.x - threat.x;
                safeZ = math.max(safeZ, threat.z + math.sqrt(math.max(0f, reach * reach - dx * dx)));
            }
            if (!underFire) return;

            // Already able to answer: its own find-target system returns fire from where it stands.
            if (ownRange > 0f && nearestDistance <= ownRange) return;

            SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(defender);
            float3 goal;
            quaternion facing = movement.SquadRotation;

            float3 advance = nearestThreat + math.normalizesafe(center - nearestThreat) * math.max(0f, ownRange - _garrisonStepUpMargin);
            if (ownRange > 0f && zone.IsInsideEnemyZone(advance.x, advance.z - _garrisonWallMargin))
            {
                goal   = advance;
                facing = quaternion.LookRotationSafe(nearestThreat - advance, math.up());
                Debug.Log($"[EnemyGeneral] Garrison squad {squadEntity.SquadId} ({squadEntity.UnitName}) outranged - stepping up to return fire");
            }
            else
            {
                goal   = center;
                goal.z = math.min(safeZ, zone.battleMaxZ - _garrisonWallMargin);
                if (goal.z - center.z < 1f) return;
                Debug.Log($"[EnemyGeneral] Garrison squad {squadEntity.SquadId} ({squadEntity.UnitName}) under fire - pulling back out of range");
            }

            DynamicBuffer<QueuedOrder> orders = _entityManager.GetBuffer<QueuedOrder>(defender);
            orders.Clear();
            orders.Add(new QueuedOrder
            {
                Type          = QueuedOrderType.Move,
                Goal          = goal,
                Rotation      = facing,
                WidthAndDepth = movement.SquadWidthAndDepth
            });
        }

        private float GetSquadAttackRange(Entity squad)
        {
            if (_entityManager.HasComponent<RangedSquad>(squad) && _entityManager.IsComponentEnabled<RangedSquad>(squad))
                return _entityManager.GetComponentData<RangedSquad>(squad).AttackRange;
            if (_entityManager.HasComponent<MageSquad>(squad))
                return _entityManager.GetComponentData<MageSquad>(squad).AttackRange;
            return 0f;
        }

        #endregion

        private void BuildGarrisonCache()
        {
            NativeArray<Entity> enemies = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in enemies)
            {
                if (!_entityManager.HasComponent<GarrisonDefenderComponent>(entity)) continue;
                int gateIndex = _entityManager.GetComponentData<GarrisonDefenderComponent>(entity).GateIndex;
                if (!_garrisonDefendersByGate.TryGetValue(gateIndex, out List<Entity> list))
                {
                    list = new List<Entity>();
                    _garrisonDefendersByGate[gateIndex] = list;
                }
                list.Add(entity);
            }
            enemies.Dispose();
            _garrisonCacheBuilt = true;
            Debug.Log($"[EnemyGeneral] Garrison cache built: {_garrisonDefendersByGate.Count} gate(s)");
        }

        private void ReleaseGateDefenders(int gateIndex)
        {
            if (_processedBreachedGates.Count == 0)
                _cavalryBehavior.OnAggressiveStarted();

            _processedBreachedGates.Add(gateIndex);

            if (!_garrisonDefendersByGate.TryGetValue(gateIndex, out List<Entity> defenders)) return;

            int released = 0;
            foreach (Entity entity in defenders)
            {
                if (!_entityManager.Exists(entity)) continue;
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                if (_cavalryBehavior.TryRegisterFlankingSquad(entity, squadEntity)) continue;

                _entityManager.SetComponentEnabled<WaitingForCommand>(entity, false);

                SquadAttributes attrs = TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName).SquadAttributes;
                if (attrs.StandardShields || attrs.HeavyShields)
                    _shieldedEnemySquads.Add(entity);

                released++;
            }
            Debug.Log($"[EnemyGeneral] Gate {gateIndex} breached — released {released} defender(s), {_garrisonDefendersByGate.Count - _processedBreachedGates.Count} gate(s) still holding");

            // Reform the squads still holding at intact gates into a new defensive formation.
            List<Entity> holdingEntities = new();
            foreach (var kvp in _garrisonDefendersByGate)
            {
                if (_processedBreachedGates.Contains(kvp.Key)) continue;
                foreach (Entity e in kvp.Value)
                {
                    if (_entityManager.Exists(e))
                    {
                        holdingEntities.Add(e);
                        SquadEntity se = _entityManager.GetComponentData<SquadEntity>(e);
                        // Debug.Log($"[EnemyGeneral] Reform: queuing squad {se.SquadId} ({se.UnitName}) from gate {kvp.Key}");
                    }
                    else
                    {
                        Debug.Log($"[EnemyGeneral] Reform: entity {e} from gate {kvp.Key} no longer exists, skipping");
                    }
                }
            }
            Debug.Log($"[EnemyGeneral] Reform: {holdingEntities.Count} holding squad(s) queued for reformation");
            if (holdingEntities.Count > 0)
            {
                foreach (Entity e in holdingEntities)
                {
                    // _entityManager.SetComponentEnabled<WaitingForCommand>(e, false);
                    if (_entityManager.HasComponent<GarrisonGateSquadTag>(e))
                        _entityManager.RemoveComponent<GarrisonGateSquadTag>(e);
                    
                }
                BattleManager.Instance.ArmySpawnManager.IssueGarrisonReformOrders(holdingEntities);
            }
            // else
            //     Debug.Log("[EnemyGeneral] Reform: no holding squads found — skipping IssueGarrisonReformOrders");
        }

        #endregion

        #region State Entry

        private void SetAggressive()
        {
            _currentState = EnemyGeneralState.Aggressive;
            _shieldedEnemySquads.Clear();
            _cavalryBehavior.OnAggressiveStarted();

            NativeArray<Entity> enemyEntities = _enemySquadQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in enemyEntities)
            {
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);

                // Cavalry behavior claims flanking squads and handles their own setup.
                if (_cavalryBehavior.TryRegisterFlankingSquad(entity, squadEntity)) continue;

                _entityManager.SetComponentEnabled<WaitingForCommand>(entity, false);

                SquadAttributes attrs = TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName).SquadAttributes;
                if (attrs.StandardShields || attrs.HeavyShields )//|| attrs.TowerShields
                    _shieldedEnemySquads.Add(entity);
            }
            enemyEntities.Dispose();
        }

        private void SetPassive()           => _currentState = EnemyGeneralState.Passive;
        private void SetDelayedPassive()    => _currentState = EnemyGeneralState.DelayedPassive;
        private void SetGarrison()
        {
            _currentState = EnemyGeneralState.Garrison;
            _garrisonDefendersByGate.Clear();
            _processedBreachedGates.Clear();
            _garrisonHealthMarks.Clear();
            _garrisonCacheBuilt = false;
        }
        private void SetDelayedAggressive() { _delayedAggressiveTimer = 0; _currentState = EnemyGeneralState.DelayedAggressive; }

        #endregion

        #region Shielded Squad Monitoring

        /// <summary>
        /// When a shielded squad starts losing its melee fight, orders it to switch to
        /// Defensive stance to reduce incoming damage.
        /// </summary>
        private void CheckShieldedSquadsForDefensiveSwitch()
        {
            for (int i = _shieldedEnemySquads.Count - 1; i >= 0; i--)
            {
                Entity entity = _shieldedEnemySquads[i];
                if (!_entityManager.Exists(entity))                          { _shieldedEnemySquads.RemoveAt(i); continue; }
                if (!_entityManager.HasComponent<InCombat>(entity))          continue;
                if (!_entityManager.HasComponent<HealthLossPercent>(entity)) continue;

                HealthLossPercent healthLoss = _entityManager.GetComponentData<HealthLossPercent>(entity);
                if (healthLoss.CombatStatus != CombatStatus.Losing) continue;

                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                BattleManager.Instance.SquadManager.SetDefensiveStancForEntity(squadEntity);
                _shieldedEnemySquads.RemoveAt(i);
            }
        }

        #endregion
    }
}
