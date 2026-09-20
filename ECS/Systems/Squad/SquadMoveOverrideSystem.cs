using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using System.Runtime.InteropServices;
using ProjectDawn.Navigation;

[StructLayout(LayoutKind.Auto)]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(DestroySquadSystem))]
partial struct SquadMoveOverrideSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }
    //TODO: This needs to be improved to complete if 90% of the units have reached the destination
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        // Ranged squad moving into range
        foreach (var (squad, squadMovementComponent, entityBuffer, squadMoveOverrideTag, orderQueue) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRW<SquadMovementComponent>,
            DynamicBuffer<EntityReferenceBufferElement>,
            RefRW<SquadMoveOverrideTag>,
            DynamicBuffer<QueuedOrder>>
        ()){
            if(squadMoveOverrideTag.ValueRO.DistanceGoal == 0)
            {
                //Create a distance goal value for the tag, it should be 95% of the distance to the goal position
                float3 startingPosition = squadMovementComponent.ValueRO.SquadCenter;
                float3 goalPosition = squadMovementComponent.ValueRO.GoalPosition;
                float distanceGoal = math.distance(startingPosition, goalPosition) * 0.2f;
                //set this to a minimum of 2 units
                if (distanceGoal < 2f) distanceGoal = 2f;
                squadMoveOverrideTag.ValueRW.DistanceGoal = distanceGoal;
                // Debug.Log($"SquadMoveOverrideSystem: Setting DistanceGoal for squad {squad.ValueRO.SquadId} to {distanceGoal}");
                continue;
            }
            //Get the distance to goal position from the squad movement component
            float distanceToGoal = math.distance(squadMovementComponent.ValueRO.GoalPosition, squadMovementComponent.ValueRO.SquadCenter);
            // Debug.Log($"SquadMoveOverrideSystem: Squad {squad.ValueRO.SquadId} distances to goal position: {distanceToGoal}, DistanceGoal: {squadMoveOverrideTag.ValueRO.DistanceGoal}");
            if (distanceToGoal <= squadMoveOverrideTag.ValueRO.DistanceGoal)
            {
                // Debug.Log($"SquadMoveOverrideSystem: Squad {squad.ValueRO.SquadId} has reached its goal position");
                entityCommandBuffer.AddComponent<CancelSquadMoveOverrideTag>(squad.ValueRO.SelfEntity);
            }

            //check if has the cancel tag
            if(entityManager.HasComponent<CancelSquadMoveOverrideTag>(squad.ValueRO.SelfEntity))
            {
                // Debug.Log($"SquadMoveOverrideSystem: Cancelling move override for squad {squad.ValueRO.SquadId}");
                entityCommandBuffer.RemoveComponent<CancelSquadMoveOverrideTag>(squad.ValueRO.SelfEntity);
                entityCommandBuffer.RemoveComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity);
                if (entityManager.HasComponent<FormationSpeedCap>(squad.ValueRO.SelfEntity))
                    entityCommandBuffer.RemoveComponent<FormationSpeedCap>(squad.ValueRO.SelfEntity);
                // Only clear JustFollowingOrders when completing a Move. If the queue was
                // replaced with an Attack, the attack is still in progress and this flag
                // should not be cleared until that order resolves.
                if (orderQueue.Length == 0 || orderQueue[0].Type == QueuedOrderType.Move)
                    entityCommandBuffer.SetComponentEnabled<JustFollowingOrders>(squad.ValueRO.SelfEntity, false);

                // Only pop the queued order if it's still a Move — if the queue was replaced
                // with an Attack order, completing it here would immediately consume that order
                if(orderQueue.Length > 0 && orderQueue[0].Type == QueuedOrderType.Move)
                    entityCommandBuffer.SetComponentEnabled<CompleteQueuedOrderTag>(squad.ValueRO.SelfEntity, true);
                //try get the first entity in the move queue buffer and obtain its rotation
                if(orderQueue.Length > 0)
                {
                    QueuedOrder firstOrder = orderQueue[0];
                    if(firstOrder.Type == QueuedOrderType.Move)
                    {
                        squadMovementComponent.ValueRW.SetRotation(firstOrder.Rotation);
                    }
                    else
                    {
                        squadMovementComponent.ValueRW.SetRotation(quaternion.identity);
                    }
                }

                if(orderQueue.Length == 0 || orderQueue[0].Type == QueuedOrderType.Move)
                    squad.ValueRW.SquadCommand = SquadCommand.None;
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    Entity entity = entityBuffer[i].Entity;
                    if(!entityManager.Exists(entity)) continue;
                    if (entityManager.HasComponent<MoveOverride>(entity))
                    {
                        entityCommandBuffer.SetComponentEnabled<MoveOverride>(entity, false);
                    }
                    // Debug.Log($"SquadMoveOverrideSystem: Squad {squad.ValueRO.SquadId} has reached its goal position");
                }
                // Debug.Log($"SquadMoveOverrideSystem: Squad {squad.ValueRO.SquadId} has cancelled its move override");

                continue;
            }
        }
    }
}