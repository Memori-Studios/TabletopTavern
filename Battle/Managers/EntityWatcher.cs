using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Audio;
using Memori.Scenes;
using Memori.Notifications;
using TJ.Map;
using TJ.Morale;
using System;

namespace TJ
{
    public class EntityWatcher : MonoBehaviour
    {
        private EntityManager _entityManager;
        private EntityQuery _balanceQuery;
        private EntityQuery _updatedSquadUnitCountQuery;
        private EntityQuery _queryNeedsToBeProcessed;
        private EntityQuery _artilleryQuery;
        private EntityQuery _artilleryRemoveQuery;
        private EntityQuery _newArrowsQuery;
        private EntityQuery _queryIssueSquadCommand;
        private EntityQuery _queryBreakSquad;
        private EntityQuery _queryWithdraw;
        private EntityQuery _queryCharge;
        private EntityQuery _queryEndBattle;
        private EntityQuery _queryKills;
        private EntityQuery _sfxQuery;
        private EntityQuery _bloodQuery;
        private EntityQuery _dustCloudQuery;
        private EntityQuery _battlefieldBonusAppliedQuery;
        private EntityQuery _mageCastRequestQuery;
        private EntityQuery _archerRangeUpdatedQuery;
        private EntityQuery _queryOnFormationsCollide;
        private EntityQuery _queryOnExplosionShake;
        private EntityQuery _queryGetTerrifiedSquads;
        private EntityQuery _queryGetChargingSquads;
        private EntityQuery _queryGetDestroyedSquads;
        private EntityQuery _querySquadCommandChanged;
        private EntityQuery _querySetUpGarrisonGateSquad;
        private EntityQuery _queryBattlePhase;
        private Entity _balanceEntity; // cache for fast access
        private readonly Dictionary<int, SquadSFXManager> _squadSFXManagers = new();
        private readonly Dictionary<int, int> _barkCounts = new(); // per-frame accumulator, avoids allocation

        // Squads whose rejected-command log has already been written. A player who keeps clicking
        // a broken squad would otherwise flood the log with one line per click.
        private readonly HashSet<int> _brokenCommandLogged = new();

        public delegate void OnArtilleryRemoved(int _squadId);
        public event OnArtilleryRemoved OnArtilleryRemovedEvent;

        bool setup;
        public void SetUp()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _entityManager.CreateEntity(typeof(BattleHasNotEnded));
            _entityManager.CreateEntity(typeof(BalanceOfPower));

            _balanceQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BalanceOfPower>()
            );

            _updatedSquadUnitCountQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UpdatedSquadUnitCount>()
            );

            _queryNeedsToBeProcessed = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntityGameObjectsProcessingNeeded>());
            _artilleryQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ArtilleryCrewSetUpEntity>());
            _artilleryRemoveQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RemoveArtilleryTag>());
            _newArrowsQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<NewArrowTag>());
            _queryIssueSquadCommand = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<IssueSquadCommand>());
            _queryBreakSquad = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BreakSquadTag>());
            _queryWithdraw = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<WithdrawCompleteTag>());
            _queryCharge = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RecalculatePositionsForUnitsCharging>());
            _queryEndBattle = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattleOver>());
            _queryKills = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadKillTag>());
            _sfxQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SFXBufferElement>());
            _bloodQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BloodBufferElement>());
            _dustCloudQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<DustCloudBufferElement>());
            _battlefieldBonusAppliedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattlefieldBonusAppliedBufferElement>());
            _mageCastRequestQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<MageCastRequestBufferElement>());
            _archerRangeUpdatedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ArcherRangeUpdated>());
            _queryOnFormationsCollide = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<OnFormationsCollide>());
            _queryOnExplosionShake = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<OnExplosionShake>());
            _queryGetTerrifiedSquads = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<IsTerrified>());
            _queryGetChargingSquads = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ChargeSquad>());
            _queryGetDestroyedSquads = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadDestroyed>());
            _querySquadCommandChanged = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadCommandChangedTag>(), ComponentType.ReadOnly<SquadEntity>());
            _querySetUpGarrisonGateSquad = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SetUpGarrisonGateSquad>());
            _queryBattlePhase = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattlePhase>());

            // if (_balanceQuery.IsEmptyIgnoreFilter)
            // {
            //     Debug.LogError("No BalanceOfPower singleton found at startup!");
            // }
            BattleManager.Instance.OnGateDestroyed += OnGateDestroyed;
            setup = true;
            Debug.Log($"EntityWatcher setup complete.");
        }
        public void TearDown()
        {
            _balanceQuery.Dispose();
            _updatedSquadUnitCountQuery.Dispose();
            _queryNeedsToBeProcessed.Dispose();
            _artilleryQuery.Dispose();
            _artilleryRemoveQuery.Dispose();
            _newArrowsQuery.Dispose();
            _queryIssueSquadCommand.Dispose();
            _queryBreakSquad.Dispose();
            _queryWithdraw.Dispose();
            _queryCharge.Dispose();
            _queryEndBattle.Dispose();
            _queryKills.Dispose();
            _sfxQuery.Dispose();
            _bloodQuery.Dispose();
            _dustCloudQuery.Dispose();
            _battlefieldBonusAppliedQuery.Dispose();
            _archerRangeUpdatedQuery.Dispose();
            _queryOnFormationsCollide.Dispose();
            _queryOnExplosionShake.Dispose();
            _queryGetTerrifiedSquads.Dispose();
            _queryGetChargingSquads.Dispose();
            _queryGetDestroyedSquads.Dispose();
            _querySquadCommandChanged.Dispose();
            _querySetUpGarrisonGateSquad.Dispose();
            _queryBattlePhase.Dispose();
            BattleManager.Instance.OnGateDestroyed -= OnGateDestroyed;
            _squadSFXManagers.Clear();
            _barkCounts.Clear();
            _brokenCommandLogged.Clear();
            if (SFXManager.Instance != null) SFXManager.Instance.StopAll();
            setup = false;
            Debug.Log($"EntityWatcher torn down.");
        }
        private void Update()
        {
            if (!setup) return;

            if(World.DefaultGameObjectInjectionWorld == null) {
                Debug.LogError($"EntityManager is null in EntityWatcher Update, skipping.");
                return;
            }

            _entityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            #region Updating UI for Squad Unit Count changes
            if(!_updatedSquadUnitCountQuery.IsEmptyIgnoreFilter)
            {
                _entityManager.CompleteAllTrackedJobs();
                NativeArray<Entity> squadEntities = _updatedSquadUnitCountQuery.ToEntityArray(Allocator.Temp);
                foreach (Entity entity in squadEntities)
                {
                    UpdatedSquadUnitCount updatedSquadUnitCount = _entityManager.GetComponentData<UpdatedSquadUnitCount>(entity);
                    // Debug.Log($"Entity {updatedSquadUnitCount.SquadId} has been updated with unit count {updatedSquadUnitCount.UnitCount}");
                    BattleManager.Instance.SquadManager.OnSquadUpdatedEvent(updatedSquadUnitCount.SquadId, updatedSquadUnitCount.UnitCount);
                    // ecb.DestroyEntity(entity);
                    // ecb.AddComponent<DestroyEntityTag>(entity, true);
                    // ecb.SetComponentEnabled<DestroyEntityTag>(entity, true);
                    ecb.DestroyEntity(entity);


                    if (updatedSquadUnitCount.SquadId > 0 && updatedSquadUnitCount.UnitCount.x == 0)
                    {
                        BattleManager.Instance.UIManager.RemoveSquad(updatedSquadUnitCount.SquadId);
                    }
                }
                squadEntities.Dispose();
            }
            #endregion

            #region Setting up Squad GameObjects
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> needsEntities = _queryNeedsToBeProcessed.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in needsEntities)
            {
                if(!_entityManager.HasComponent<SquadEntity>(entity)) {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot set up squad.");
                    continue;
                }
                if (!_entityManager.HasComponent<MoraleComponent>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a MoraleComponent, cannot set up squad.");
                    continue;
                }

                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                DynamicBuffer<EntityReferenceBufferElement> entityBuffer = _entityManager.GetBuffer<EntityReferenceBufferElement>(entity);
                ecb.RemoveComponent<SquadEntityGameObjectsProcessingNeeded>(entity);

                int defenderGateIndex = BattleManager.Instance.ArmySpawnManager.GetGateIndexForDefenderSquad(squadEntity.SquadId);
                // Debug.Log($"[GarrisonDefender] Squad {squadEntity.SquadId} ({squadEntity.UnitName}): gateIndex lookup = {defenderGateIndex}");
                if (defenderGateIndex >= 0)
                {
                    ecb.AddComponent(entity, new GarrisonDefenderComponent { GateIndex = defenderGateIndex });
                    ecb.AddComponent<DefendersResolveComponent>(entity);
                    var moraleComp = _entityManager.GetComponentData<MoraleComponent>(entity);
                    moraleComp.CurrentMorale += TabletopTavernConstants.FORTIFIED_MORALE_BONUS;
                    moraleComp.MaxMorale += TabletopTavernConstants.FORTIFIED_MORALE_BONUS;
                    _entityManager.SetComponentData(entity, moraleComp);
                    foreach (var unitRef in entityBuffer)
                    {
                        if (!_entityManager.Exists(unitRef.Entity)) continue;
                        if (!_entityManager.HasComponent<MissileResistance>(unitRef.Entity))
                            ecb.AddComponent(unitRef.Entity, new MissileResistance { DamageMultiplier = 0.5f });
                    }
                }

                UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadEntity.UnitName);
                MoraleComponent moraleComponent = _entityManager.GetComponentData<MoraleComponent>(squadEntity.SelfEntity);

                SquadSFXManager sfxManager = BattleManager.Instance.SquadManager.SetUpSquadFlag(squadEntity, squadEntity.SquadId > 0 ? Team.Player : Team.Enemy, squadEntity.SquadId, moraleComponent);
                if (sfxManager != null) _squadSFXManagers[squadEntity.SquadId] = sfxManager;
                SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName);

                // GetSquadStats returns the raw authored blob, so a prestige-granted trait has to be
                // merged in here the same way UnitSetUpSystem does it for the per-unit path -
                // otherwise anything below that reads SquadAttributes silently ignores the grant.
                UnitAttribute grantedTrait = BattleManager.Instance.SquadManager.GetSquadPrestigeTrait(squadEntity.SquadId);
                if (grantedTrait != UnitAttribute.None)
                    TabletopTavernConstants.SetAttribute(ref squadStats.SquadAttributes, grantedTrait);

                //Create Range Drawer for the squad
                if (TabletopTavernConstants.FightsAtRange(unitType))
                {
                    int ammunition = squadStats.Ammunition;
                    // Prestige ammo (and Deep Quivers) only applies to dedicated shooters. Hybrids
                    // carry a token handful of throwing weapons, so a flat +100/level or +500 would
                    // dwarf the whole pool - see IsTraitEligibleForUnitType.
                    if(unitType == UnitType.Ranged)
                    {
                        ammunition += TabletopTavernConstants.PRESTIGE_AMMO_BONUS_RANGED * BattleManager.Instance.SquadManager.GetSquadPrestige(squadEntity.SquadId);
                        // Deep Quivers sits inside this same guard so the throwing-weapon units are
                        // excluded by the one rule, even if a mod sets the attribute on them.
                        if (squadStats.SquadAttributes.DeepQuivers)
                            ammunition += TabletopTavernConstants.DEEP_QUIVERS_AMMO_BONUS;
                    }

                    // Hero-granted ammunition bonuses (e.g. Bertha/14 Supply Lines) now come from
                    // HeroBonusManager's rule data instead of a hardcoded hero check.
                    if (HeroBonusManager.Instance.ActiveHeroID != -1)
                    {
                        foreach (var bonus in HeroBonusManager.GetHeroStatBonus(UnitStat.Ammunition, squadEntity.UnitName, HeroBonusManager.Instance.ActiveHeroID, ammunition))
                            ammunition += (int)bonus.Value;
                    }
                    ecb.AddComponent(entity, new RangedSquad()
                    {
                        // AttackRange = squadStats.BaseRange,
                    });
                    ecb.AddComponent(entity, new SquadAmmunition() { Value = ammunition });

                    // SwitchRequested stays false: UnitSetUpSystem already gave each unit this same
                    // mode, and RangedFireModeSwitchJob would overwrite their freshly-seeded
                    // ShootAttack timers on top of doing nothing else useful.
                    RangedFireMode squadFireMode = squadEntity.SquadId > 0
                        ? TabletopTavernConstants.GetPlayerDefaultFireMode()
                        : RangedFireMode.Volley;
                    ecb.AddComponent(entity, new RangedFireModeSquadComponent() { FireMode = squadFireMode, SwitchRequested = false });
                    BattleManager.Instance.SquadManager.CreateArcherRangeDrawer(squadEntity);

                    // Enemy archers receive skirmish tag. Hybrids are excluded: they are meant to
                    // close and fight, and RangedSquadSkirmishSystem would retreat them at
                    // ARCHER_FLEE_DISTANCE before their melee stats ever mattered.
                    if (unitType == UnitType.Ranged && squadEntity.SquadId < 0  && squadEntity.UnitName != UnitName.Gate && !BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                    {
                        ecb.AddComponent(entity, new RangedSquadSkirmishTag() { });
                    }
                }
                else if (unitType == UnitType.Artillery)
                {
                    int ammunition = squadStats.Ammunition + TabletopTavernConstants.PRESTIGE_AMMO_BONUS_ARTILLERY * BattleManager.Instance.SquadManager.GetSquadPrestige(squadEntity.SquadId);
                    if (squadStats.SquadAttributes.PowderReserves)
                        ammunition = (int)(ammunition * TabletopTavernConstants.POWDER_RESERVES_AMMO_MULTIPLIER);
                    // Hero-granted ammunition bonuses (e.g. Bertha/14 Supply Lines) now come from
                    // HeroBonusManager's rule data instead of a hardcoded hero check.
                    if (HeroBonusManager.Instance.ActiveHeroID != -1)
                    {
                        foreach (var bonus in HeroBonusManager.GetHeroStatBonus(UnitStat.Ammunition, squadEntity.UnitName, HeroBonusManager.Instance.ActiveHeroID, ammunition))
                            ammunition += (int)bonus.Value;
                    }
                    ecb.AddComponent(entity, new RangedSquad() { });
                    ecb.AddComponent(entity, new SquadAmmunition() { Value = ammunition });
                    BattleManager.Instance.SquadManager.CreateArcherRangeDrawer(squadEntity);
                    ecb.AddComponent<ArtillerySquad>(entity);
                }
                else if (TabletopTavernConstants.Casts(unitType))
                {
                    // Mage. Deliberately NOT given RangedSquad: that component is what pulls a squad
                    // into archer targeting, skirmishing and fire modes, none of which apply here.
                    // This branch has to sit above the else, which is exactly where Hybrid silently
                    // ended up before it was fixed.
                    TJ.Spells.SpellData mageSpell = TabletopTavernData.Instance.SquadAssetsDictionary[squadEntity.UnitName].mageSpell;
                    if (mageSpell == null)
                    {
                        Debug.LogError($"EntityWatcher: {squadEntity.UnitName} is UnitType.Mage but its SquadData has no mageSpell assigned - it will never cast.");
                    }

                    ecb.AddComponent(entity, new MageSquad()
                    {
                        // Seeded here and then kept in sync with the unit's MageCast.Range by
                        // MageSquadRangeSystem, so prestige Range reaches squad-level targeting.
                        AttackRange = squadStats.BaseRange,
                        TargetPriority = mageSpell != null ? mageSpell.MageTargetPriority : MageTargetPriority.NearestEnemy,
                    });

                    // Charges, spent one per cast through the same SquadAmmunition pipeline archers
                    // use. The prestige constant is 1 per level rather than the ranged 100 or the
                    // artillery 6, because a mage carries a handful of charges and a single extra
                    // cast is already a large swing.
                    int charges = squadStats.Ammunition + TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * BattleManager.Instance.SquadManager.GetSquadPrestige(squadEntity.SquadId);
                    ecb.AddComponent(entity, new SquadAmmunition() { Value = charges });

                    BattleManager.Instance.SquadManager.CreateArcherRangeDrawer(squadEntity);

                    // The drawer is created immediately but MageSquad is added through the ECB, so
                    // the Recalculate inside SetUp finds no MageSquad and leaves the ring at radius
                    // zero - invisible for the whole battle.
                    //
                    // An archer survives the same race by accident: RangedSquad is seeded with
                    // AttackRange 0, so RangedSquadRangeSystem always has a correction to make and
                    // raises ArcherRangeUpdated as a side effect, which redraws the ring. MageSquad
                    // above is seeded with the right range already, so MageSquadRangeSystem hits its
                    // "range == mageSquad.AttackRange" early-out and never raises anything.
                    //
                    // Requesting the redraw explicitly is what closes that gap. It rides the same
                    // ECB, so the component and the request land together and the drain re-runs
                    // Recalculate once MageSquad actually exists.
                    ecb.AddComponent(entity, new ArcherRangeUpdated());
                }
                else
                {
                    ecb.AddComponent<MeleeSquad>(entity);
                }

                if (squadEntity.SquadId > 0)
                {
                    ecb.AddComponent<PlayerSquad>(entity);
                    BattleManager.Instance.UIManager.AddSquad(squadEntity, entityBuffer.Length);
                }
                else
                {
                    ecb.AddComponent<EnemySquad>(entity);
                }
                // squadEntity.ValueRW.CachedSquadCenter = squadEntity.CenterPosition;
                ecb.AddComponent<FormationNeedsToBeProcessed>(entity);
                ecb.AddComponent<FormationShapeChanged>(entity);

                if(squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields)// || squadStats.SquadAttributes.TowerShields)
                {
                    ecb.AddComponent(entity, new ShieldedStanceSquadComponent { Stance = ShieldedStance.Balanced });
                }
                else
                {
                    ecb.AddComponent(entity, new ShieldedStanceSquadComponent { Stance = ShieldedStance.None });
                }

                BattleManager.Instance.UnitDebugSetUp.SetUpSquadDebug(entity, _entityManager);
                BattleManager.Instance.SquadMovementManager.ReceiveSquadEntity(squadEntity.SelfEntity, squadStats.Speed / 10f, squadEntity.SquadId);

                if (squadEntity.SquadId > 0)
                {
                    BattleManager.Instance.UIManager.CreateAttackArrow(squadEntity);
                }

                if (_entityManager.HasComponent<CavalryFlankingTag>(entity))
                {
                    BattleManager.Instance.EnemyGeneral.MarkSquadForFlanking(squadEntity.SquadId);
                }
                // Debug.Log($"Set up squad {squadEntity.SquadId} enemies left to spawn: {BattleManager.Instance.BattleSaveManager.EnemiesToSpawn}");

                // if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle &&
                //     BattleManager.Instance.BattleSaveManager.EnemiesToSpawn == squadEntity.SquadId)
                // {
                //     BattleManager.Instance.StartBattle();
                // }
            }
            needsEntities.Dispose();
            #endregion

            #region Setting up Artillery GameObjects
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> artilleryEntities = _artilleryQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in artilleryEntities)
            {
                // Get the parent of the spawned entity
                if (!_entityManager.HasComponent<Parent>(entity)){
                    continue;
                }
                var parentComponent = _entityManager.GetComponentData<Parent>(entity);
                Entity parentEntity = parentComponent.Value;

                if (!_entityManager.HasComponent<Parent>(parentEntity)){
                    continue;
                }
                var grandparentComponent = _entityManager.GetComponentData<Parent>(parentEntity);
                Entity grandparentEntity = grandparentComponent.Value;

                if (!_entityManager.HasComponent<ArtilleryUnit>(grandparentEntity))
                {
                    continue;
                }
                // ArtilleryUnit artilleryComp = entityManager.GetComponentData<ArtilleryUnit>(grandparentEntity);

                if (!_entityManager.HasComponent<Unit>(grandparentEntity))
                {
                    continue;
                }
                Unit unitComp = _entityManager.GetComponentData<Unit>(grandparentEntity);
                ecb.RemoveComponent<ArtilleryCrewSetUpEntity>(entity);
                SetupArtilleryCrewAsync(grandparentEntity, parentEntity, unitComp.squadId, unitComp.unitName);
                // Debug.Log($"Artillery crew prefab instantiated for entity {entity}");
            }
            artilleryEntities.Dispose();
            #endregion

            #region Deleting Artillery GameObjects
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> artilleryRemoveEntities = _artilleryRemoveQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in artilleryRemoveEntities)
            {
                int squadID = _entityManager.GetComponentData<RemoveArtilleryTag>(entity).SquadID;
                // Debug.Log($"Removing artillery crew for squad {squadID}");
                ecb.RemoveComponent<RemoveArtilleryTag>(entity);
                OnArtilleryRemovedEvent?.Invoke(squadID);
            }
            artilleryRemoveEntities.Dispose();
            #endregion

            #region Setting up Trail Renderers
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> newArrowsEntities = _newArrowsQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in newArrowsEntities)
            {
                NewArrowTag newArrowTag = _entityManager.GetComponentData<NewArrowTag>(entity);
                ecb.RemoveComponent<NewArrowTag>(entity);
                BattleManager.Instance.TrailRendererSetup.SetupTrail(entity, _entityManager, newArrowTag.SmokeExplosionOnStart, newArrowTag.FlamingAmmo);
            }
            newArrowsEntities.Dispose();
            #endregion

            #region Squad Commands
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> squadEntitiesOverrideSquadCommandTag = _queryIssueSquadCommand.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in squadEntitiesOverrideSquadCommandTag)
            {
                IssueSquadCommand overrideSquadCommandTag = _entityManager.GetComponentData<IssueSquadCommand>(entity);
                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot issue command.");
                    ecb.RemoveComponent<IssueSquadCommand>(entity);
                    continue;
                }
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                squadEntity.SquadCommand = overrideSquadCommandTag.SquadCommand;
                _entityManager.SetComponentData(entity, squadEntity);

                if (_entityManager.HasComponent<BrokenSquadTag>(squadEntity.SelfEntity))
                {
                    ecb.RemoveComponent<IssueSquadCommand>(entity);
                    BattleManager.Instance.PositionDrawer.TurnOff();

                    // Logged once per squad: this branch swallows the command before the
                    // issueSquadCommand log below, so without it a player's orders to a broken
                    // squad leave no trace at all and the report reads as unresponsive input.
                    if (_brokenCommandLogged.Add(squadEntity.SquadId))
                        Debug.Log($"[Morale] Command {overrideSquadCommandTag.SquadCommand} rejected - squad {squadEntity.SquadId} ({squadEntity.UnitName}) is broken and cannot be commanded");

                    continue;
                }

                Debug.Log($"issueSquadCommand {overrideSquadCommandTag.SquadCommand} for squad {squadEntity.SquadId}");

                switch (overrideSquadCommandTag.SquadCommand)
                {
                    case SquadCommand.Move:
                        ecb.SetComponentEnabled<JustFollowingOrders>(entity, true);
                        if (!_entityManager.HasComponent<SquadDestination>(squadEntity.SelfEntity))
                        {
                            Debug.Log($"jesus christ forgot the squad destination");
                            continue;
                        }
                        SquadDestination squadDestination = _entityManager.GetComponentData<SquadDestination>(squadEntity.SelfEntity);
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToDestination(squadEntity, squadDestination, ecb);
                        break;
                    case SquadCommand.Attack:
                        ecb.SetComponentEnabled<JustFollowingOrders>(entity, true);
                        bool enemySquad = _entityManager.HasComponent<EnemySquad>(entity);
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToAttack(squadEntity, overrideSquadCommandTag.NewTargetSquad, enemySquad, ecb);
                        break;
                    case SquadCommand.Halt:
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToHalt(squadEntity, ecb);
                        break;
                    case SquadCommand.HaltAndFreeze:
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToHaltAndFreeze(squadEntity, ecb);
                        break;
                    case SquadCommand.Withdraw:
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToWithdraw(squadEntity, false, ecb);
                        break;
                    case SquadCommand.Retreat:
                        BattleManager.Instance.UnitPositioningManager.OrderSquadToWithdraw(squadEntity, true, ecb);
                        break;
                    default:
                        break;
                }
                ecb.RemoveComponent<IssueSquadCommand>(entity);
            }
            squadEntitiesOverrideSquadCommandTag.Dispose();
            #endregion

            #region On Breaking Squads
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryBreakTag = _queryBreakSquad.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in queryBreakTag)
            {
                ecb.RemoveComponent<BreakSquadTag>(entity);
                if (_entityManager.HasComponent<CausesTerrorTag>(entity))
                {
                    ecb.RemoveComponent<CausesTerrorTag>(entity);
                }

                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot break squad.");
                    continue;
                }
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);

                BattleManager.Instance.BreakSquad(squadEntity);
                ecb.AddComponent<BrokenSquadTag>(entity);
            }
            queryBreakTag.Dispose();
            #endregion

            #region Withdrawing Squads
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryWithdrawTag = _queryWithdraw.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in queryWithdrawTag)
            {
                ecb.RemoveComponent<WithdrawCompleteTag>(entity);
                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot process withdrawal.");
                    continue;
                }
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                BattleManager.Instance.SquadManager.WithdrawSquad(squadEntity.SquadId);
            }
            queryWithdrawTag.Dispose();
            #endregion

            #region Charging Update Goal Positions
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryChargeTag = _queryCharge.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in queryChargeTag)
            {
                ecb.RemoveComponent<RecalculatePositionsForUnitsCharging>(entity);
                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot recalculate charge positions.");
                    continue;
                }
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                BattleManager.Instance.UnitPositioningManager.UpdateSquadPositionsOnCharge(squadEntity.SelfEntity);
                // Debug.Log($"Entity {squadEntity.SquadId} has been updated with rotation {squadEntity.SquadRotation}");
            }
            queryChargeTag.Dispose();
            #endregion

            #region EndBattle
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryEndBattleEntities = _queryEndBattle.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in queryEndBattleEntities)
            {
                BattleOver battleOver = _entityManager.GetComponentData<BattleOver>(entity);
                EndBattle(battleOver.PlayerWon);
                ecb.DestroyEntity(entity);
                Debug.Log($"Destroyed BattleHasNotEnded entity");
            }
            queryEndBattleEntities.Dispose();
            #endregion

            #region Record Kill
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryKillUnitTags = _queryKills.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in queryKillUnitTags)
            {
                SquadKillTag killUnitTag = _entityManager.GetComponentData<SquadKillTag>(entity);
                BattleManager.Instance.ArmySpawnManager.RecordSquadKill(killUnitTag.SquadId);
                ecb.DestroyEntity(entity);
                // ecb.SetComponentEnabled<DestroyEntityTag>(entity, true);
            }
            queryKillUnitTags.Dispose();
            #endregion

            #region SFX
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> sfxEntities = _sfxQuery.ToEntityArray(Allocator.Temp);
            _barkCounts.Clear();

            foreach (Entity unitEntity in sfxEntities)
            {
                DynamicBuffer<SFXBufferElement> sfxBuffer = _entityManager.GetBuffer<SFXBufferElement>(unitEntity);

                if (sfxBuffer.Length == 0) continue;

                if (!_entityManager.HasComponent<Unit>(unitEntity)) { sfxBuffer.Clear(); continue; }
                Unit unit = _entityManager.GetComponentData<Unit>(unitEntity);

                if (!_squadSFXManagers.TryGetValue(unit.squadId, out SquadSFXManager squadSFXManager) || squadSFXManager == null)
                { sfxBuffer.Clear(); continue; }

                if (!_entityManager.HasComponent<LocalTransform>(unitEntity)) { sfxBuffer.Clear(); continue; }
                Vector3 unitPos = _entityManager.GetComponentData<LocalTransform>(unitEntity).Position;

                for (int i = 0; i < sfxBuffer.Length; i++)
                {
                    SFXBufferElement evt = sfxBuffer[i];
                    if (evt.SFXEntityType == SFXEntityType.Idle)
                    {
                        // Accumulate bark events per squad and dispatch as a burst after the loop
                        _barkCounts.TryGetValue(unit.squadId, out int n);
                        _barkCounts[unit.squadId] = n + 1;
                    }
                    else
                    {
                        AudioClip clip = TabletopTavernData.Instance.GetBattlefieldAudio(evt.UnitName, evt.SFXEntityType);
                        squadSFXManager.PlaySFX(clip, unitPos);
                    }
                }
                sfxBuffer.Clear();
            }

            // Dispatch accumulated barks — one PlayBarks call per squad, not one per unit
            foreach (KeyValuePair<int, int> kvp in _barkCounts)
            {
                if (_squadSFXManagers.TryGetValue(kvp.Key, out SquadSFXManager manager) && manager != null)
                    manager.PlayBarks(kvp.Value, manager.transform.position);
            }

            sfxEntities.Dispose();
            #endregion

            #region Blood VFX
            _entityManager.CompleteAllTrackedJobs();
            if (!_bloodQuery.IsEmptyIgnoreFilter)
            {
                DynamicBuffer<BloodBufferElement> BloodBuffer = _bloodQuery.GetSingletonBuffer<BloodBufferElement>();
                foreach (var element in BloodBuffer)
                {
                    BattleManager.Instance.MeshTextureUpdater.ApplySplatAtPoint(new Vector3(element.Position.x, 0, element.Position.z));
                    if(element.IsExplosion)
                    {
                        BattleManager.Instance.MeshTextureUpdater.ExplosionAtPoint(new Vector3(element.Position.x, 0, element.Position.z));
                    }
                }
                BloodBuffer.Clear();
            }
            #endregion

            #region Dust Cloud VFX
            _entityManager.CompleteAllTrackedJobs();
            if (!_dustCloudQuery.IsEmptyIgnoreFilter)
            {
                DynamicBuffer<DustCloudBufferElement> dustCloudBuffer = _dustCloudQuery.GetSingletonBuffer<DustCloudBufferElement>();
                foreach (var element in dustCloudBuffer)
                {
                    BattleManager.Instance.MeshTextureUpdater.SpawnDustCloudAt(new Vector3(element.Position.x, element.Position.y, element.Position.z));
                }
                dustCloudBuffer.Clear();
            }
            #endregion

            #region Battlefield Bonus Applied
            _entityManager.CompleteAllTrackedJobs();
            if (!_battlefieldBonusAppliedQuery.IsEmptyIgnoreFilter)
            {
                DynamicBuffer<BattlefieldBonusAppliedBufferElement> appliedBuffer = _battlefieldBonusAppliedQuery.GetSingletonBuffer<BattlefieldBonusAppliedBufferElement>();
                foreach (var element in appliedBuffer)
                {
                    BattleManager.Instance.SquadManager.RaiseBattlefieldBonusApplied(element.SquadId, element.BonusEnum, element.UnitStat, element.Value);
                }
                appliedBuffer.Clear();
            }
            #endregion

            #region Mage cast requests
            // MageCastSystem can decide a mage should cast but cannot build the effect: a SpellData is
            // a managed ScriptableObject and ActiveSpell is a MonoBehaviour prefab. It appends here and
            // this drains once per frame, same idiom as the battlefield-bonus stream above.
            //
            // The buffer singleton is only created when spells are enabled (see BattleManager.StartBattle),
            // so in a build without the SPELLS define this query stays empty and MageCastSystem early-outs
            // rather than filling a buffer nothing ever drains.
            _entityManager.CompleteAllTrackedJobs();
            if (!_mageCastRequestQuery.IsEmptyIgnoreFilter)
            {
                DynamicBuffer<MageCastRequestBufferElement> mageCastBuffer = _mageCastRequestQuery.GetSingletonBuffer<MageCastRequestBufferElement>();
                foreach (var request in mageCastBuffer)
                {
                    TJ.Spells.SpellData spellData = TabletopTavernData.Instance.SquadAssetsDictionary[request.UnitName].mageSpell;
                    BattleManager.Instance.SpellManager.CastUnitSpell(
                        spellData,
                        new Vector3(request.Position.x, request.Position.y, request.Position.z),
                        request.TeamOfSource,
                        request.SquadId,
                        request.TargetSquadEntity);
                }
                mageCastBuffer.Clear();
            }
            #endregion

            #region ArcherRangeUpdated
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> ArcherRangeUpdatedEntities = _archerRangeUpdatedQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in ArcherRangeUpdatedEntities)
            {
                ecb.RemoveComponent<ArcherRangeUpdated>(entity);
                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot update archer range.");
                    continue;
                }
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);
                BattleManager.Instance.SquadManager.UpdateArcherRangeDrawer(squadEntity);
                // Debug.Log($"Entity {entity} has been updated");
            }
            ArcherRangeUpdatedEntities.Dispose();
            #endregion

            #region OnFormationsCollide
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryOnFormationsCollideEntities = _queryOnFormationsCollide.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in queryOnFormationsCollideEntities)
            {
                OnFormationsCollide onFormationsCollide = _entityManager.GetComponentData<OnFormationsCollide>(entity);
                BattleManager.Instance.CameraShaker.ChargeShake(onFormationsCollide.Position);
                ecb.RemoveComponent<OnFormationsCollide>(entity);
            }
            queryOnFormationsCollideEntities.Dispose();
            #endregion

            #region OnExplosionShake
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> explosionShakeEntities = _queryOnExplosionShake.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in explosionShakeEntities)
            {
                OnExplosionShake shake = _entityManager.GetComponentData<OnExplosionShake>(entity);
                BattleManager.Instance.CameraShaker.ExplosionShake(shake.Position);
                ecb.DestroyEntity(entity);
            }
            explosionShakeEntities.Dispose();
            #endregion


            #region GetTerrifiedSquads
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryGetTerrifiedSquadsEntities = _queryGetTerrifiedSquads.ToEntityArray(Allocator.Temp);
            List<int> terrifiedSquadIds = new();
            foreach (Entity entity in queryGetTerrifiedSquadsEntities)
            {
                if (!_entityManager.HasComponent<SquadEntity>(entity))
                {
                    Debug.LogError($"Entity {entity} does not have a SquadEntity component, cannot process terrified squad.");
                    continue;
                }
                terrifiedSquadIds.Add(_entityManager.GetComponentData<SquadEntity>(entity).SquadId);
            }
            BattleManager.Instance.SquadManager.TerrifiedSquads(terrifiedSquadIds);
            queryGetTerrifiedSquadsEntities.Dispose();
            #endregion

            #region GetChargingSquads
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryGetChargingSquadsEntities = _queryGetChargingSquads.ToEntityArray(Allocator.Temp);
            List<int> chargingSquadIds = new();
            foreach (Entity entity in queryGetChargingSquadsEntities)
            {
                if (!_entityManager.HasComponent<SquadEntity>(entity)) continue;
                chargingSquadIds.Add(_entityManager.GetComponentData<SquadEntity>(entity).SquadId);
            }
            BattleManager.Instance.SquadManager.ChargingSquads(chargingSquadIds);
            queryGetChargingSquadsEntities.Dispose();
            #endregion

            #region SquadCommandChanged
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> commandChangedEntities = _querySquadCommandChanged.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in commandChangedEntities)
            {
                SquadCommandChangedTag changed = _entityManager.GetComponentData<SquadCommandChangedTag>(entity);
                SquadEntity squadEntity = _entityManager.GetComponentData<SquadEntity>(entity);

                // Debug.Log($"[SquadSFX] Squad {squadEntity.SquadId}: {changed.OldCommand} → {changed.NewCommand}");

                bool isActive = changed.NewCommand == SquadCommand.Move
                             || changed.NewCommand == SquadCommand.Attack;

                bool wasActive = changed.OldCommand == SquadCommand.Move
                              || changed.OldCommand == SquadCommand.Attack;

                if (_squadSFXManagers.TryGetValue(squadEntity.SquadId, out SquadSFXManager sfxManager) && sfxManager != null)
                {
                    if (isActive && !wasActive)
                    {
                        Vector3 squadCenter = _entityManager.HasComponent<SquadMovementComponent>(entity)
                            ? (Vector3)_entityManager.GetComponentData<SquadMovementComponent>(entity).SquadCenter
                            : sfxManager.transform.position;
                        sfxManager.StartChargeSound(squadCenter);
                    }
                    else if (!isActive && wasActive)
                    {
                        sfxManager.StopChargeSound();
                    }
                }

                ecb.RemoveComponent<SquadCommandChangedTag>(entity);
            }
            commandChangedEntities.Dispose();
            #endregion

            #region GetDestroyedSquads
            _entityManager.CompleteAllTrackedJobs();
            NativeArray<Entity> queryGetDestroyedSquadsEntities = _queryGetDestroyedSquads.ToEntityArray(Allocator.Temp);
            List<int> destroyedSquadIds = new();
            foreach (Entity entity in queryGetDestroyedSquadsEntities)
            {
                int squadId = _entityManager.GetComponentData<SquadDestroyed>(entity).SquadId;
                // Debug.Log($"Squad {squadId} has been marked for destruction");
                if(!destroyedSquadIds.Contains(squadId)) {
                    destroyedSquadIds.Add(squadId);
                }
                _squadSFXManagers.Remove(squadId);

                ecb.DestroyEntity(entity);
                // ecb.DestroyEntity(entity);
                // ecb.AddComponent<DestroyEntityTag>(entity);
                // ecb.SetComponentEnabled<DestroyEntityTag>(entity, true);
            }
            if (destroyedSquadIds.Count > 0)
            {
                BattleManager.Instance.SquadManager.DestroyedSquads(destroyedSquadIds);
            }
            queryGetDestroyedSquadsEntities.Dispose();
            #endregion

            #region SetUpGarrisonGateSquad
            if (!_querySetUpGarrisonGateSquad.IsEmptyIgnoreFilter)
            {
                _entityManager.CompleteAllTrackedJobs();
                NativeArray<Entity> gateSquadEntities = _querySetUpGarrisonGateSquad.ToEntityArray(Allocator.Temp);
                foreach (Entity entity in gateSquadEntities)
                {
                    if (!_entityManager.IsComponentEnabled<SetUpGarrisonGateSquad>(entity)) continue;

                    ecb.SetComponentEnabled<SetUpGarrisonGateSquad>(entity, false);

                    GarrisonGateSquadTag gateTag = _entityManager.GetComponentData<GarrisonGateSquadTag>(entity);
                    GameObject gateGO = BattleManager.Instance.ArmySpawnManager.GetGateGameObject(gateTag.GateIndex);

                    // SquadCenterSystem is excluded from gate squads, so set SquadCenter
                    // and AABB bounds once here from the gate's world position — it never moves.
                    // Without bounds, MeleeSquadChargeSystem's AABBIntersect always fails (bounds
                    // stay at world-origin default) and melee units can never engage the gate.
                    SquadMovementComponent squadMovement = _entityManager.GetComponentData<SquadMovementComponent>(entity);
                    float3 gatePos = gateGO.transform.position;
                    squadMovement.SquadCenter  = new float3(gatePos.x, 0f, gatePos.z);
                    squadMovement.GoalPosition = squadMovement.SquadCenter;
                    float3 halfExtent = new float3(3f); // TEST: inflated gate bounds
                    squadMovement.BoundsMin    = squadMovement.SquadCenter - halfExtent;
                    squadMovement.BoundsMax    = squadMovement.SquadCenter + halfExtent;
                    squadMovement.BoundsRadius = 3f;
                    _entityManager.SetComponentData(entity, squadMovement);

                    GarrisonGateSquadHelper helper = gateGO.AddComponent<GarrisonGateSquadHelper>();
                    helper.Initialize(entity, gateGO);

                    GarrisonGateRangeDrawer rangeDrawer = gateGO.GetComponentInChildren<GarrisonGateRangeDrawer>(true);
                    if (rangeDrawer != null)
                    {
                        int squadId = _entityManager.GetComponentData<SquadEntity>(entity).SquadId;
                        BattleManager.Instance.SquadManager.RegisterGateRangeDrawer(squadId, rangeDrawer);
                        rangeDrawer.TurnOff();
                    }
                }
                gateSquadEntities.Dispose();
            }
            #endregion

            #region GetBalanceOfPower
            if (!_balanceQuery.IsEmptyIgnoreFilter)
            {
                var entity = _balanceQuery.GetSingletonEntity();
                var bop = _entityManager.GetComponentData<BalanceOfPower>(entity);
                BattleManager.Instance.UIManager.UpdateBalanceOfPower(bop);
            }
            #endregion

            ecb.Playback(_entityManager);
        }
        private void OnGateDestroyed(int gateIndex)
        {
            if (!setup) return;
            Debug.Log($"[GarrisonGate] Gate {gateIndex} destroyed — releasing defender squads");

            _entityManager.CompleteAllTrackedJobs();

            EntityQuery defenderQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GarrisonDefenderComponent>());
            using var defenders = defenderQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            defenderQuery.Dispose();

            foreach (Entity squadEntity in defenders)
            {
                if (!_entityManager.Exists(squadEntity)) continue;
                // if (_entityManager.GetComponentData<GarrisonDefenderComponent>(squadEntity).GateIndex == gateIndex)
                    

                _entityManager.RemoveComponent<GarrisonDefenderComponent>(squadEntity);
                // Debug.Log($"[GarrisonGate] Removed GarrisonDefenderComponent from squad entity {squadEntity}");
                if (_entityManager.HasBuffer<EntityReferenceBufferElement>(squadEntity))
                {
                    var unitBuffer = _entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
                    var unitEntities = new System.Collections.Generic.List<Entity>(unitBuffer.Length);
                    for (int i = 0; i < unitBuffer.Length; i++)
                        unitEntities.Add(unitBuffer[i].Entity);

                    foreach (var entity in unitEntities)
                    {
                        if (!_entityManager.Exists(entity)) continue;
                        if (_entityManager.HasComponent<MissileResistance>(entity))
                            _entityManager.RemoveComponent<MissileResistance>(entity);
                    }
                }
            }
        }
        public void EndBattle(bool playerWon)
        {
            if(!setup) {
                Debug.LogError($"Cannot end battle, EntityWatcher is not set up.");
                return;
            }
            if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                BattleManager.Instance.ArmySpawnManager.MapSquadsToKillsAndWithdrwanSquads(playerWon);
            }

            BattleManager.Instance.EndBattle(playerWon);
            foreach (SquadSFXManager sfxManager in _squadSFXManagers.Values)
            {
                if (sfxManager != null)
                {
                    sfxManager.StopChargeSound();
                    sfxManager.StopCombatSound();
                }
            }

            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            var simulationSystemGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.Enabled = false;
            var initializationSystemGroup = defaultWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            initializationSystemGroup.Enabled = false;
            setup = false;
        }

        private async void SetupArtilleryCrewAsync(Entity grandparentEntity, Entity parentEntity, int squadId, UnitName unitName)
        {
            GameObject prefab = await TabletopTavernData.Instance.LoadArtilleryCrewPrefabAsync(unitName);
            ArtilleryCrewPrefabGO artilleryCrewGO = Instantiate(prefab).GetComponent<ArtilleryCrewPrefabGO>();
            artilleryCrewGO.SetArtilleryEntity(grandparentEntity, parentEntity, squadId, this);
        }
    }
}
