using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

partial struct MeleeSquadChargeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }
    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        // static bool IsSquadNavMeshPositionWithinDistance(float3 positionA, float3 positionB, float distance) {
        //     return math.distance(positionA, positionB) <= distance;
        // }

        // Melee squad movement
        foreach (var (squad, SquadTargettingComponent, squadMovement, SquadOverridesComponent, entityBuffer, MeleeSquad, ChargeSquad) 
            in SystemAPI.Query<
                RefRW<SquadEntity>, 
                RefRW<SquadTargettingComponent>,
                RefRW<SquadMovementComponent>, 
                SquadOverridesComponent,
                DynamicBuffer<EntityReferenceBufferElement>, 
                MeleeSquad, 
                ChargeSquad
            >()
            .WithNone<
                InCombat, 
                BrokenSquadTag, 
                CavalryFlankingTag
            >()
            )
        {
            // Debug.Log($"MeleeSquadChargeSystem: ");
            entityCommandBuffer.SetComponentEnabled<SquadTargettingComponent>(squad.ValueRO.SelfEntity, false);

            if(!entityManager.Exists(squad.ValueRO.TargetSquadEntity)) {
                // Debug.Log($"MeleeSquadChargeSystem: squad {squad.ValueRO.SquadId} does not have a target squad entity");
                entityCommandBuffer.RemoveComponent<ChargeSquad>(squad.ValueRO.SelfEntity);
                continue;
            }
            if(entityManager.HasComponent<BrokenSquadTag>(squad.ValueRO.TargetSquadEntity)) {
                // Debug.Log($"MeleeSquadChargeSystem: squad {squad.ValueRO.SquadId} is charging a broken squad {squad.ValueRO.TargetSquadEntity}");
                entityCommandBuffer.RemoveComponent<ChargeSquad>(squad.ValueRO.SelfEntity);
                continue;
            }
            
            if(squad.ValueRO.TargetSquadEntity == Entity.Null) continue;


            SquadMovementComponent targetSquadMovement = entityManager.GetComponentData<SquadMovementComponent>(squad.ValueRO.TargetSquadEntity);
            // bool closeEnoughForCombat = IsSquadNavMeshPositionWithinDistance(squadMovement.ValueRO.SquadCenter, targetSquad.SquadCenter, 8f);

            // In any system that needs to know if two squads are close:
            float distance = math.distance(squadMovement.ValueRO.SquadCenter, targetSquadMovement.SquadCenter);
            float combinedRadius = squadMovement.ValueRO.BoundsRadius + targetSquadMovement.BoundsRadius; // + attack range

            bool AABBIntersect(in SquadMovementComponent a, in SquadMovementComponent b, float margin = 0f)
            {
                float3 aMin = a.BoundsMin - margin;
                float3 aMax = a.BoundsMax + margin;
                float3 bMin = b.BoundsMin - margin;
                float3 bMax = b.BoundsMax + margin;

                //make sure that aabb is valid
                if(aMin.x > aMax.x || aMin.y > aMax.y || aMin.z > aMax.z ||
                   bMin.x > bMax.x || bMin.y > bMax.y || bMin.z > bMax.z)
                {
                    Debug.LogError($"MeleeSquadChargeSystem: Invalid AABB for squads {squad.ValueRO.SquadId} or {squad.ValueRO.TargetSquadEntity}");
                    return false;
                }


                return aMin.x <= bMax.x && aMax.x >= bMin.x &&
                    aMin.y <= bMax.y && aMax.y >= bMin.y &&
                    aMin.z <= bMax.z && aMax.z >= bMin.z;
            }
            

            bool closeEnoughForCombat = AABBIntersect(squadMovement.ValueRO, targetSquadMovement, 3f);
            // Debug.Log($"MeleeSquadChargeSystem: squad {squad.ValueRO.SquadId} distance to target squad {squad.ValueRO.TargetSquadEntity} is {distance}, combined radius is {combinedRadius}, closeEnoughForCombat: {closeEnoughForCombat}");


            // float distance = math.distance(targetSquad.SquadCenter, SquadMovementComponent.ValueRO.SquadCenter);
            quaternion directionToTarget = quaternion.LookRotationSafe(targetSquadMovement.SquadCenter - squadMovement.ValueRO.SquadCenter, math.up());
            
            squadMovement.ValueRW.SetRotation(directionToTarget);

            if(closeEnoughForCombat)
            {
                // Debug.Log($"MeleeSquadChargeSystem: squad {squad.ValueRO.SquadId} distance to target squad {squad.ValueRO.TargetSquadEntity} is {distance}, combined radius is {combinedRadius}, closeEnoughForCombat: {closeEnoughForCombat}");
                // Debug.Log($"MeleeSquadChargeSystem: squad {squad.ValueRO.SquadId} is close enough for combat with squad {squad.ValueRO.TargetSquadEntity}");
                
                //self entity
                if(!entityManager.HasComponent<HaltCommandTag>(squad.ValueRO.SelfEntity))
                    entityCommandBuffer.AddComponent<HaltCommandTag>(squad.ValueRO.SelfEntity);

                if (entityManager.HasComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity))
                    entityCommandBuffer.RemoveComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity);
              
                entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new FormationEngagedInCombat{
                    EngagementEntity = squad.ValueRO.TargetSquadEntity,
                    WasCharging = ChargeSquad.ChargeTime >= TabletopTavernConstants.CHARGE_SPRINT_TIME
                });

                //target entity
                if (!entityManager.IsComponentEnabled<JustFollowingOrders>(squad.ValueRO.TargetSquadEntity))
                {
                    if (!entityManager.HasComponent<HaltCommandTag>(squad.ValueRO.TargetSquadEntity))
                        entityCommandBuffer.AddComponent<HaltCommandTag>(squad.ValueRO.TargetSquadEntity);

                    if (entityManager.HasComponent<SquadMoveOverrideTag>(squad.ValueRO.TargetSquadEntity))
                        entityCommandBuffer.RemoveComponent<SquadMoveOverrideTag>(squad.ValueRO.TargetSquadEntity);

                    entityCommandBuffer.AddComponent(squad.ValueRO.TargetSquadEntity, new FormationEngagedInCombat{
                        EngagementEntity = squad.ValueRO.SelfEntity,
                        // A counter-charge only counts when the target was sprinting at this squad.
                        WasCharging = entityManager.HasComponent<ChargeSquad>(squad.ValueRO.TargetSquadEntity)
                            && entityManager.GetComponentData<ChargeSquad>(squad.ValueRO.TargetSquadEntity).ChargeTime >= TabletopTavernConstants.CHARGE_SPRINT_TIME
                            && entityManager.GetComponentData<SquadEntity>(squad.ValueRO.TargetSquadEntity).TargetSquadEntity == squad.ValueRO.SelfEntity
                    });
                }

            }
            else
            {
                squadMovement.ValueRW.GoalPosition = targetSquadMovement.SquadCenter;
                entityCommandBuffer.AddComponent<RecalculatePositionsForUnitsCharging>(squad.ValueRO.SelfEntity);
            }
        }        
    }
}

