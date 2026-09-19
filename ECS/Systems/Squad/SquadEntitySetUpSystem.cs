using Unity.Burst;
using Unity.Entities;

partial struct SquadEntitySetUpSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationRate>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<NeedsToBeProcessed>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        //Set up
        foreach (var (squadEntity, needsToBeProcessed, entityBuffer, entity) in SystemAPI.Query<
            RefRW<SquadEntity>, NeedsToBeProcessed, DynamicBuffer<EntityReferenceBufferElement>>().WithEntityAccess())
        {
            // EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
            // UnityEngine.Debug.Log($"SquadEntitySystem: Processing squad {squadEntity.ValueRO.SquadId}");
            entityCommandBuffer.RemoveComponent<NeedsToBeProcessed>(entity);

            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                RefRW<Unit> unit = SystemAPI.GetComponentRW<Unit>(unitEntity);
                unit.ValueRW.squadId = squadEntity.ValueRO.SquadId;
                unit.ValueRW.squadEntity = entity;
            }

            // Entity flagEntity = state.EntityManager.Instantiate(entitiesReferences.flagEntity);
            // RefRW<FlagComponent> flagComponent = SystemAPI.GetComponentRW<FlagComponent>(flagEntity);     
            // flagComponent.ValueRW.squadId = squadEntity.ValueRO.SquadId;
            // squadEntity.ValueRW.flagEntity = flagEntity;

            entityCommandBuffer.AddComponent(entity, new SquadEntityGameObjectsProcessingNeeded());
            entityCommandBuffer.AddComponent(entity, new SquadCameraDistanceComponent());
            entityCommandBuffer.AddBuffer<BattlefieldBonusBufferElement>(entity);
            entityCommandBuffer.AddComponent(entity, new BattlefieldBonusSeenMask());
            entityCommandBuffer.AddBuffer<SpellStatusBufferElement>(entity);
            // Blacklist of abandoned kiting targets; seeded here so GetBuffer is always valid and the
            // pursuit-watchdog give-up path never triggers a structural add. Harmless on player squads.
            entityCommandBuffer.AddBuffer<TargetBlacklistElement>(entity);
        }
    }
}

