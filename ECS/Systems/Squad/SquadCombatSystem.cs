using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectDawn.Navigation;

partial struct SquadCombatSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (squad, SquadMovementComponent, entityBuffer, InCombat) in SystemAPI.Query<
            RefRW<SquadEntity>, 
            RefRO<SquadMovementComponent>, 
            DynamicBuffer<EntityReferenceBufferElement>, 
            InCombat>
        ()) {
            
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity referencedEntity = entityBuffer[i].Entity;
                if(!entityManager.IsComponentEnabled<UnitFindTarget>(referencedEntity)) {
                    if (entityManager.HasComponent<UnitFindTarget>(referencedEntity))
                    {
                        UnitFindTarget findTarget = entityManager.GetComponentData<UnitFindTarget>(referencedEntity);
                        findTarget.TargetedSquadEntity = squad.ValueRO.TargetSquadEntity;
                        entityManager.SetComponentData(referencedEntity, findTarget);
                        entityManager.SetComponentEnabled<UnitFindTarget>(referencedEntity, true);
                    }
                }
            }

            if(squad.ValueRO.TargetSquadEntity == Entity.Null) continue;
            
            bool targetIsRunningAway = entityManager.HasComponent<SquadMoveOverrideTag>(squad.ValueRO.TargetSquadEntity);

            if(targetIsRunningAway)
            {
                entityCommandBuffer.SetComponentEnabled<DisengageFromCombat>(squad.ValueRO.SelfEntity, true);

                //for enemy squads, add the dazed tag
                if(squad.ValueRO.SquadId < 0)
                {
                    entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new OpponentRanAwayTag() { DazedTime = TabletopTavernConstants.DAZED_ON_DISENGAGE_TIME });
                }


                // for (int i = 0; i < entityBuffer.Length; i++)
                // {
                //     Entity entity = entityBuffer[i].Entity;
                //     // if (entityManager.HasComponent<UnitFindTarget>(entity))
                //     // {
                //     //     UnitFindTarget findTarget = entityManager.GetComponentData<UnitFindTarget>(entity);
                //     //     findTarget.TargetedSquadEntity = Entity.Null;
                //     //     entityManager.SetComponentData(entity, findTarget);
                //     //     entityCommandBuffer.SetComponentEnabled<UnitFindTarget>(entity, false);
                //     //     // entityCommandBuffer.SetComponentEnabled<RotateToDirection>(entity, true);
                //     // }

                //     //set the idle animation to the idle animation
                //     if(!entityManager.HasComponent<AnimationDataHolder>(entity)) continue;

                //     // AnimationDataHolder animationDataHolder = entityManager.GetComponentData<AnimationDataHolder>(entity);
                //     // animationDataHolder.currentIdleAnimationId = animationDataHolder.idleAnimationId;
                //     // entityManager.SetComponentData(entity, animationDataHolder);

                //     // if(!entityManager.HasComponent<GpuEcsAnimatorControlComponent>(animationDataHolder.gpuEcsAnimatorEntity)) continue;
                //     // GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(
                //     //             animationDataHolder.gpuEcsAnimatorEntity);
                //     // controlComp.animatorInfo.animationID = animationDataHolder.currentIdleAnimationId;
                //     // entityManager.SetComponentData(animationDataHolder.gpuEcsAnimatorEntity, controlComp);
                    
                // }
            }
        }
    }
}

