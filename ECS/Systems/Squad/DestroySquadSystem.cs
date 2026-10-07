using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using ProjectDawn.Navigation;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(SquadRemoveUnitSystem))]
partial struct DestroySquadSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        //destroy squads with no entities
        // First pass: collect dying squad entities and queue destruction
        var dyingSquadEntities = new NativeList<Entity>(Allocator.Temp);
        foreach (var (squadWithZeroEntities, entityBuffer) in SystemAPI.Query<
            RefRW<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>>()
        .WithNone<DestroyEntityTag>())
        {
            if(entityBuffer.Length != 0) continue;

            dyingSquadEntities.Add(squadWithZeroEntities.ValueRO.SelfEntity);

            Entity SquadKillTagEntity = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(SquadKillTagEntity, new SquadDestroyed { SquadId = squadWithZeroEntities.ValueRO.SquadId });
            entityCommandBuffer.SetComponentEnabled<DestroyEntityTag>(squadWithZeroEntities.ValueRO.SelfEntity, true);
        }

        // Second pass: clear target references on any squad that was targeting a dying squad
        if (dyingSquadEntities.Length > 0)
        {
            foreach (var attackingSquad in SystemAPI.Query<RefRW<SquadEntity>>().WithNone<BrokenSquadTag>())
            {
                for (int i = 0; i < dyingSquadEntities.Length; i++)
                {
                    if (attackingSquad.ValueRW.TargetSquadEntity == dyingSquadEntities[i] ||
                        attackingSquad.ValueRW.SelfEntity == dyingSquadEntities[i])
                    {
                        attackingSquad.ValueRW.TargetSquadEntity = Entity.Null;
                        entityCommandBuffer.AddComponent(attackingSquad.ValueRO.SelfEntity, new TargetSquadDestroyed { });
                        break;
                    }
                }
            }
        }

        dyingSquadEntities.Dispose();

        //update the squads that were fighting the destroyed squad
        foreach (var (squadThatDestroyedItsTarget, TargetSquadDestroyed, entityBuffer) in SystemAPI.Query<
            RefRW<SquadEntity>,
            TargetSquadDestroyed,
            DynamicBuffer<EntityReferenceBufferElement>
        >().WithNone<BrokenSquadTag>()) {

            // Debug.Log($"DestroySquadSystem: Squad {squadThatDestroyedItsTarget.ValueRO.SquadId} destroyed its target");

            entityCommandBuffer.RemoveComponent<TargetSquadDestroyed>(squadThatDestroyedItsTarget.ValueRO.SelfEntity);

            if(squadThatDestroyedItsTarget.ValueRW.TargetSquadEntity == Entity.Null)
            {
                entityCommandBuffer.SetComponentEnabled<DisengageFromCombat>(squadThatDestroyedItsTarget.ValueRO.SelfEntity, true);
            }
        }

        //destroy squads through UI
        foreach (var (squadToDestroy, entityBuffer, DeleteSquadTag) in SystemAPI.Query<
            RefRW<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>,
            DeleteSquadTag
        >()) {
            entityCommandBuffer.RemoveComponent<DeleteSquadTag>(squadToDestroy.ValueRO.SelfEntity);
            int length = entityBuffer.Length;
            for (int i = 0; i < length; i++) {
                Entity unitEntity = entityBuffer[i].Entity;

                entityCommandBuffer.AddComponent(unitEntity,
                        new UnitRemovedFromSquad { Entity = unitEntity, SquadId = squadToDestroy.ValueRO.SquadId, DeleteCorpse = true });
            }

            if(entityManager.HasComponent<ArtillerySquad>(squadToDestroy.ValueRO.SelfEntity)) {
                // Debug.Log($"DestroySquadSystem: Removing artillery crew for squad {squadToDestroy.ValueRO.SquadId}");
                entityCommandBuffer.AddComponent(squadToDestroy.ValueRO.SelfEntity, new RemoveArtilleryTag { SquadID = squadToDestroy.ValueRO.SquadId });
            }
        }
    }
}
