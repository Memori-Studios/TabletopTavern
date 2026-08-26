using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

// Mirror of RangedSquadRangeSystem for mage squads: copies the unit's MageCast.Range up onto
// MageSquad.AttackRange so squad-level targeting has a range to compare against, and so anything
// that modifies the unit's range (prestige, and any future mage range gear) propagates without
// the two ever being set independently.
//
// For archers, reading entityBuffer[0] is an approximation - one unit stands in for the squad.
// A mage squad is a single model, so here it is exact.
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(DestroySquadSystem))]
partial struct MageSquadRangeSystem : ISystem
{
    private ComponentLookup<MageCast> _mageCastLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _mageCastLookup = state.GetComponentLookup<MageCast>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _mageCastLookup.Update(ref state);
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new MageSquadRangeJob
        {
            MageCastLookup = _mageCastLookup,
            Ecb = ecb
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
partial struct MageSquadRangeJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<MageCast> MageCastLookup;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity,
        DynamicBuffer<EntityReferenceBufferElement> entityBuffer, ref MageSquad mageSquad)
    {
        if (entityBuffer.Length == 0) return;
        Entity unitEntity = entityBuffer[0].Entity;
        if (!MageCastLookup.HasComponent(unitEntity)) return;

        float range = MageCastLookup[unitEntity].Range;
        if (range == mageSquad.AttackRange) return;

        mageSquad.AttackRange = range;
        // ArcherRangeUpdated is named for archers but its EntityWatcher drain is squad-generic - it
        // just calls UpdateArcherRangeDrawer(squadEntity) - so the mage range ring redraws for free.
        Ecb.AddComponent(sortKey, entity, new ArcherRangeUpdated());
    }
}
