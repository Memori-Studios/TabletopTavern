using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
partial struct SquadDisengageInCombatSystem : ISystem
{
    private EntityQuery _nonBrokenSquadQuery;
    private ComponentLookup<IsFlanking> _isFlankingLookup;
    private ComponentLookup<Unit> _unitLookup;
    private ComponentLookup<FormationEngagedInRangedCombat> _formationLookup;
    private ComponentLookup<BrokenSquadTag> _brokenSquadLookup;
    private ComponentLookup<InCombat> _inCombatLookup;
    private ComponentLookup<FormationEngagedInCombat> _engagingLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _nonBrokenSquadQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SquadEntity>()
            .WithNone<BrokenSquadTag>()
            .WithNone<DestroyEntityTag>()
            .Build(ref state);
        _isFlankingLookup = state.GetComponentLookup<IsFlanking>(false);
        _unitLookup = state.GetComponentLookup<Unit>(true);
        _formationLookup = state.GetComponentLookup<FormationEngagedInRangedCombat>(true);
        _brokenSquadLookup = state.GetComponentLookup<BrokenSquadTag>(true);
        _inCombatLookup = state.GetComponentLookup<InCombat>(true);
        _engagingLookup = state.GetComponentLookup<FormationEngagedInCombat>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _isFlankingLookup.Update(ref state);
        _unitLookup.Update(ref state);
        _formationLookup.Update(ref state);
        _brokenSquadLookup.Update(ref state);
        _inCombatLookup.Update(ref state);
        _engagingLookup.Update(ref state);

        var allSquadData = _nonBrokenSquadQuery.ToComponentDataArray<SquadEntity>(Allocator.TempJob);
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new SquadDisengageJob
        {
            AllSquadData = allSquadData,
            IsFlankingLookup = _isFlankingLookup,
            UnitLookup = _unitLookup,
            FormationLookup = _formationLookup,
            BrokenSquadLookup = _brokenSquadLookup,
            InCombatLookup = _inCombatLookup,
            EngagingLookup = _engagingLookup,
            Ecb = ecb
        }.ScheduleParallel(state.Dependency);

        allSquadData.Dispose(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(DisengageFromCombat))]
[WithAll(typeof(SquadMovementComponent))]
partial struct SquadDisengageJob : IJobEntity
{
    [ReadOnly] public NativeArray<SquadEntity> AllSquadData;
    [NativeDisableParallelForRestriction] public ComponentLookup<IsFlanking> IsFlankingLookup;
    [ReadOnly] public ComponentLookup<Unit> UnitLookup;
    [ReadOnly] public ComponentLookup<FormationEngagedInRangedCombat> FormationLookup;
    [ReadOnly] public ComponentLookup<BrokenSquadTag> BrokenSquadLookup;
    [ReadOnly] public ComponentLookup<InCombat> InCombatLookup;
    [ReadOnly] public ComponentLookup<FormationEngagedInCombat> EngagingLookup;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity,
        ref SquadEntity squad, ref DisengageFromCombat disengage,
        DynamicBuffer<QueuedOrder> queuedOrders,
        DynamicBuffer<EntityReferenceBufferElement> entityBuffer)
    {
        Ecb.SetComponentEnabled<DisengageFromCombat>(sortKey, squad.SelfEntity, false);
        Ecb.RemoveComponent<InCombat>(sortKey, squad.SelfEntity);

        if (IsFlankingLookup.IsComponentEnabled(squad.SelfEntity))
        {
            IsFlanking isFlanking = IsFlankingLookup[squad.SelfEntity];
            isFlanking.TargetFlankedSquadEntity = Entity.Null;
            IsFlankingLookup[squad.SelfEntity] = isFlanking;
            Ecb.SetComponentEnabled<IsFlanking>(sortKey, squad.SelfEntity, false);
        }

        if (FormationLookup.HasComponent(squad.SelfEntity))
            Ecb.RemoveComponent<FormationEngagedInRangedCombat>(sortKey, squad.SelfEntity);

        // SquadEngageInCombatSystem engages this squad later this frame; a queued OnDisengage would overwrite its OnEngage and leave MoveOverride on, making every model untargetable.
        bool engagingThisFrame = EngagingLookup.HasComponent(squad.SelfEntity) && !InCombatLookup.HasComponent(squad.SelfEntity);
        for (int i = 0; i < entityBuffer.Length && !engagingThisFrame; i++)
        {
            Entity unitEntity = entityBuffer[i].Entity;
            if (!UnitLookup.HasComponent(unitEntity)) continue;
            Unit unit = UnitLookup[unitEntity];
            unit.unitState = UnitState.OnDisengage;
            Ecb.SetComponent(sortKey, unitEntity, unit);
        }

        if (disengage.newTargetSquad != Entity.Null)
        {
            bool newTargetValid = false;
            for (int i = 0; i < AllSquadData.Length; i++)
            {
                if (AllSquadData[i].SelfEntity == disengage.newTargetSquad) { newTargetValid = true; break; }
            }

            if (newTargetValid)
            {
                squad.TargetSquadEntity = disengage.newTargetSquad;
                disengage.newTargetSquad = Entity.Null;
                return;
            }
            disengage.newTargetSquad = Entity.Null;
        }

        if (BrokenSquadLookup.HasComponent(squad.SelfEntity))
        {
            squad.TargetSquadEntity = Entity.Null;
            disengage.newTargetSquad = Entity.Null;
            return;
        }

        if (squad.SquadCommand == SquadCommand.Move)
            return;

        for (int i = 0; i < AllSquadData.Length; i++)
        {
            SquadEntity enemySquad = AllSquadData[i];
            if (enemySquad.Team == squad.Team) continue;
            if (enemySquad.SelfEntity == squad.TargetSquadEntity) continue;

            if (enemySquad.TargetSquadEntity == squad.SelfEntity &&
                InCombatLookup.HasComponent(enemySquad.SelfEntity))
            {
                queuedOrders.Clear();
                queuedOrders.Add(new QueuedOrder
                {
                    Type = QueuedOrderType.Attack,
                    TargetSquadId = enemySquad.SquadId,
                });
                disengage.newTargetSquad = Entity.Null;
                return;
            }
        }

        disengage.newTargetSquad = Entity.Null;
    }
}
