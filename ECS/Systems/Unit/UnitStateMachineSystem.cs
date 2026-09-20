using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ProjectDawn.Navigation;
using Memori.Utilities;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using Unity.Mathematics;

partial struct UnitStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        //flip from attacking to idle and vice versa
        foreach ((
            RefRW<Unit> unit,
            RefRW<Target> target,
            AgentBody agentBody,
            RefRW<AnimationDataHolder> animationDataHolder,
            RefRW<AgentSonarAvoid> agentSonarAvoid,
            Entity entity
            )
            in SystemAPI.Query<
                RefRW<Unit>,
                RefRW<Target>,
                AgentBody,
                RefRW<AnimationDataHolder>,
                RefRW<AgentSonarAvoid>
                >().WithPresent<AgentSonarAvoid, AgentSeparation>().WithEntityAccess())
        {
            switch (unit.ValueRO.unitState)
            {
                case UnitState.Spawn:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity has spawned");
                        unit.ValueRW.unitState = UnitState.Idle;

                        agentSonarAvoid.ValueRW.BlockedStop = false;
                        agentSonarAvoid.ValueRW.MaxAngle = math.radians(360);
                        entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, true);
                        break;
                    }
                case UnitState.Idle:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is now Idle");
                        if (agentBody.IsStopped == false)
                        {
                            unit.ValueRW.unitState = UnitState.Moving;
                            // Debug.Log($"UnitStateMachineSystem: Entity {entity} is now Moving");
                            agentSonarAvoid.ValueRW.BlockedStop = false;
                            // Large units use a narrower sonar cone so they push straight through
                            // infantry rather than arcing around them.
                            agentSonarAvoid.ValueRW.MaxAngle = entityManager.HasComponent<LargeTag>(entity)
                                ? math.radians(120) : math.radians(360);
                            entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, true);
                        }
                        break;
                    }
                case UnitState.Moving:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is now Moving");
                        if (agentBody.IsStopped == true)
                        {
                            unit.ValueRW.unitState = UnitState.Idle;
                            entityCommandBuffer.SetComponentEnabled<RotateUnit>(entity, true);
                        }
                        break;
                    }
                case UnitState.OnCharge:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is charging");
                        unit.ValueRW.unitState = UnitState.Charge;

                        entityCommandBuffer.SetComponent(entity, new Target { targetEntity = Entity.Null });

                        agentSonarAvoid.ValueRW.BlockedStop = false;
                        agentSonarAvoid.ValueRW.MaxAngle = math.radians(120);
                        entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, true);
                        entityCommandBuffer.SetComponentEnabled<AgentSeparation>(entity, false);
                        break;
                    }
                case UnitState.Charge:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is charging");
                        break;
                    }
                case UnitState.OnEngage:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is engaging in combat");
                        unit.ValueRW.unitState = UnitState.InCombat;

                        agentSonarAvoid.ValueRW.BlockedStop = true;
                        agentSonarAvoid.ValueRW.MaxAngle = math.radians(120);
                        entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, true);
                        entityCommandBuffer.SetComponentEnabled<AgentSeparation>(entity, false);

                        // Stop the nav system from auto-rotating units based on velocity direction.
                        // RotateUnitSystem takes sole control of rotation while InCombat.
                        AgentLocomotion agentLocomotion = entityManager.GetComponentData<AgentLocomotion>(entity);
                        agentLocomotion.AngularSpeed = 0f;
                        entityManager.SetComponentData(entity, agentLocomotion);

                        // Enable continuous combat facing from the first InCombat frame.
                        entityCommandBuffer.SetComponentEnabled<RotateUnit>(entity, true);

                        entityCommandBuffer.AddComponent<InCombat>(entity);

                        GpuEcsAnimatorControlComponent controlComp =
                            entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(animationDataHolder.ValueRO.gpuEcsAnimatorEntity);

                        if (entityManager.HasComponent<RangedMeleeConverter>(entity))
                        {
                            RangedMeleeConverter rangedMeleeConverter = entityManager.GetComponentData<RangedMeleeConverter>(entity);
                            rangedMeleeConverter.SwitchToMelee = true;
                            entityCommandBuffer.SetComponent(entity, rangedMeleeConverter);
                            entityCommandBuffer.SetComponentEnabled<RangedMeleeConverter>(entity, true);
                        }
                        else
                        {
                            animationDataHolder.ValueRW.currentIdleAnimationId = animationDataHolder.ValueRO.attackIdleAnimationId;
                            controlComp.animatorInfo.animationID = animationDataHolder.ValueRO.currentIdleAnimationId;
                            entityCommandBuffer.SetComponent(animationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);
                        }

                        if (entityManager.IsComponentEnabled<MoveOverride>(entity))
                        {
                            entityCommandBuffer.SetComponentEnabled<MoveOverride>(entity, false);
                        }
                        break;
                    }
                case UnitState.OnEngageRanged:
                    {
                        // Debug.Log($"UnitStateMachineSystem: Entity is engaging in ranged combat");
                        unit.ValueRW.unitState = UnitState.InCombat;

                        entityCommandBuffer.SetComponentEnabled<RotateUnit>(entity, true);
                        entityCommandBuffer.SetComponentEnabled<AgentSeparation>(entity, false);
                        // entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, false);
                        break;
                    }
                case UnitState.InCombat:
                {
                    // Debug.Log($"UnitStateMachineSystem: Entity is in combat");
                    break;
                }
                case UnitState.OnDisengage:
                    {
                        // entityManager.SetComponentEnabled<RotateUnit>(entity, true);

                        if (entityManager.HasComponent<InCombat>(entity))
                        {
                            entityCommandBuffer.RemoveComponent<InCombat>(entity);
                        }
                        if (entityManager.HasComponent<DealFlankingDamageTag>(entity))
                        {
                            entityManager.SetComponentEnabled<DealFlankingDamageTag>(entity, false);
                        }

                        //for removing attacks
                        if (entityManager.HasComponent<NeedsToBeProcessed>(entity))
                        {
                            entityCommandBuffer.RemoveComponent<NeedsToBeProcessed>(entity);
                        }

                        if (entityManager.HasComponent<UnitFindTarget>(entity))
                        {
                            entityCommandBuffer.SetComponent(entity, new Target { targetEntity = Entity.Null });
                            entityCommandBuffer.SetComponentEnabled<UnitFindTarget>(entity, false);
                        }
                        entityCommandBuffer.SetComponentEnabled<AgentSeparation>(entity, true);
                        agentSonarAvoid.ValueRW.BlockedStop = false;
                        agentSonarAvoid.ValueRW.MaxAngle = math.radians(360);

                        entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, false);

                        // Restore nav auto-rotation now that RotateUnitSystem hands back control.
                        AgentLocomotion agentLocomotionDisengage = entityManager.GetComponentData<AgentLocomotion>(entity);
                        agentLocomotionDisengage.AngularSpeed = math.radians(120);
                        entityManager.SetComponentData(entity, agentLocomotionDisengage);

                        if (entityManager.HasComponent<RangedMeleeConverter>(entity))
                        {
                            RangedMeleeConverter rangedMeleeConverter = entityManager.GetComponentData<RangedMeleeConverter>(entity);
                            rangedMeleeConverter.SwitchToMelee = false;
                            entityCommandBuffer.SetComponent(entity, rangedMeleeConverter);
                            entityCommandBuffer.SetComponentEnabled<RangedMeleeConverter>(entity, true);
                        }
                        else
                        {
                            //set the idle animation to the idle animation
                            animationDataHolder.ValueRW.currentIdleAnimationId = animationDataHolder.ValueRO.idleAnimationId;
                        }

                        if (!entityManager.HasComponent<UnitRemovedFromSquad>(entity))
                        {
                            GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(
                                        animationDataHolder.ValueRO.gpuEcsAnimatorEntity);
                            controlComp.animatorInfo.animationID = animationDataHolder.ValueRO.currentIdleAnimationId;
                            entityCommandBuffer.SetComponent(animationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);
                        }


                        // Debug.Log($"UnitEngageInCombatSystem: Entity is disengaging from combat");
                        if(entityManager.IsComponentEnabled<RetreatingUnit>(entity))
                        {
                            unit.ValueRW.unitState = UnitState.Broken;
                            if(entityManager.HasComponent<AgentShape>(entity))
                            {
                                AgentShape AgentShape = entityManager.GetComponentData<AgentShape>(entity);
                                AgentShape.Radius = 0.01f;
                                entityCommandBuffer.SetComponent(entity, AgentShape);
                                entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(entity, true);
                            }
                        }
                        else
                        {
                            unit.ValueRW.unitState = UnitState.Idle;
                        }

                        break;
                    }
                case UnitState.Broken:
                    {
                        break;
                    }
                case UnitState.Dead:
                    break;
            }
            
        }
    }
}
