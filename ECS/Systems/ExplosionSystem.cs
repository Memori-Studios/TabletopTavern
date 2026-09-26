using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using ProjectDawn.Navigation;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using UnityEngine;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;
using Unity.Transforms;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(ProcessUnitDeathSystem))]
partial struct ExplosionSystem : ISystem
{
    public const float THROW_LIFETIME = 1f;
    public const float THROW_TOTAL_TIME = 2f;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CampaignSaveDataHolder>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
        NativeList<DistanceHit> distanceHitList = new NativeList<DistanceHit>(Allocator.Temp);
        // The broadphase build may still be in flight when this system reads the singleton, and an
        // OverlapSphere against a half-built tree returns nothing. An explosion is one-shot, so a miss
        // is a blast that hits nobody. Same fix as SpellSystem: sync once per frame, only when one fires.
        bool jobsCompleted = false;

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (Explosion, Entity) in SystemAPI.Query<
            RefRW<Explosion>
        >().WithEntityAccess())
        {
            if (Explosion.ValueRO.Delay > 0f)
            {
                Explosion.ValueRW.Delay -= deltaTime;
                continue;
            }

            if (!jobsCompleted)
            {
                entityManager.CompleteAllTrackedJobs();
                collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
                jobsCompleted = true;
            }
            distanceHitList.Clear();
            CollisionFilter collisionFilter = new()
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
                GroupIndex = 0,
            };

            // Debug.Log($"ExplosionSystem: Checking for units within range of explosion at position {Explosion.ValueRO.ExplosionPosition} with range {Explosion.ValueRO.KnockbackRange}");

            if (collisionWorld.OverlapSphere(Explosion.ValueRO.ExplosionPosition, Explosion.ValueRO.KnockbackRange, ref distanceHitList, collisionFilter))
            {
                foreach (DistanceHit distanceHit in distanceHitList)
                {
                    if (!SystemAPI.Exists(distanceHit.Entity) || !SystemAPI.HasComponent<Unit>(distanceHit.Entity))
                    {
                        Debug.Log($"ExplosionSystem: Entity {distanceHit.Entity} does not exist or does not have a Unit component, skipping");
                        continue;
                    }

                    if(SystemAPI.HasComponent<Health>(distanceHit.Entity))
                    {
                        Health health = SystemAPI.GetComponent<Health>(distanceHit.Entity);
                        if (health.Value <= 0)
                        {
                            // Debug.Log($"ExplosionSystem: Entity {distanceHit.Entity} is gonna die, skipping");
                            continue;
                        }
                    }

                    // A Shieldwall brace stops the throw but not the blast's damage; innate knockback immunity still skips both.
                    if (SystemAPI.HasComponent<ShieldwallResistGrantedTag>(distanceHit.Entity)
                        && SystemAPI.GetComponent<Unit>(distanceHit.Entity).Team != Explosion.ValueRO.KnockbackSquadTeam
                        && Explosion.ValueRO.KnockbackInitialDamage > 0)
                    {
                        SystemAPI.GetBuffer<DamageBufferElement>(distanceHit.Entity).Add(new DamageBufferElement
                        {
                            AttackStrength = Explosion.ValueRO.KnockbackInitialDamage,
                            DamageSource = DamageSource.Melee,
                            DamageType = DamageType.Physical,
                            TeamOfSource = Explosion.ValueRO.KnockbackSquadTeam,
                            DamageSourceSquadId = Explosion.ValueRO.KnockbackSquadID,
                            SourceIsArtillery = true,
                        });
                        continue;
                    }

                    //if it can resist the knockback or is on the same team as the explosion, skip it
                    if (SystemAPI.HasComponent<ResistKnockbackTag>(distanceHit.Entity) || SystemAPI.GetComponent<Unit>(distanceHit.Entity).Team == Explosion.ValueRO.KnockbackSquadTeam )
                    {
                        continue;
                    }
                    if (SystemAPI.HasComponent<ThrowUnit>(distanceHit.Entity))
                    {
                        // Debug.Log($"ExplosionSystem: Entity {distanceHit.Entity} is already being thrown, skipping");
                        continue;
                    }

                    entityCommandBuffer.AddComponent(distanceHit.Entity, new ThrowUnit
                    {
                        Force = Explosion.ValueRO.KnockbackForce,
                        HittingEntityLocation = Explosion.ValueRO.ExplosionPosition,
                        InitialLocation = SystemAPI.GetComponent<LocalTransform>(distanceHit.Entity).Position,
                        HittingEntitySquad = Explosion.ValueRO.KnockbackSquadID,
                        HittingEntityTeam = Explosion.ValueRO.KnockbackSquadTeam,
                        Damage = Explosion.ValueRO.KnockbackInitialDamage,
                        RemainingTime = THROW_LIFETIME, 
                        TotalTime = THROW_TOTAL_TIME
                    });

                    // DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(distanceHit.Entity);
                    // damageBuffer.Add(new DamageBufferElement
                    // {
                    //     AttackStrength = GameAssets.CHARGE_DAMAGE,
                    //     DamageSource = DamageSource.Melee,
                    //     DamageType = DamageType.Physical,
                    //     TeamOfSource = Explosion.ValueRO.KnockbackSquadTeam,
                    //     DamageSourceSquadId = Explosion.ValueRO.KnockbackSquadID,
                    // });
                    // Debug.Log($"Unit hit by explosion from");

                    entityCommandBuffer.SetComponentEnabled<NavMeshPath>(distanceHit.Entity, false);
                    entityCommandBuffer.SetComponentEnabled<AgentSonarAvoid>(distanceHit.Entity, false);

                    AnimationDataHolder animationDataHolder = entityManager.GetComponentData<AnimationDataHolder>(distanceHit.Entity);
                    Entity childEntity = animationDataHolder.gpuEcsAnimatorEntity;

                    if (childEntity == Entity.Null) continue;

                    GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(animationDataHolder.gpuEcsAnimatorEntity);
                    controlComp.transitionSpeed = 0f;
                    controlComp.animatorInfo.animationID = animationDataHolder.thrownAnimationId;

                    RefRW<AgentBody> agentBody = SystemAPI.GetComponentRW<AgentBody>(distanceHit.Entity);
                    agentBody.ValueRW.IsStopped = true;
                    agentBody.ValueRW.SetDestination(SystemAPI.GetComponent<LocalTransform>(distanceHit.Entity).Position);
                    // UnityEngine.Debug.Log($"Unit {distanceHit.Entity} hit by charge from squad {UnitChargeContactTag.ValueRO.KnockbackSquadID}");

                    entityCommandBuffer.SetComponent(childEntity, controlComp);
                }
            }

            //look into passing the archetype then setting the component as add component changes the archetype so this is a little inefficient
            Entity shakeEntity = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(shakeEntity, new OnExplosionShake { Position = Explosion.ValueRO.ExplosionPosition });
            entityCommandBuffer.RemoveComponent<Explosion>(Entity);
        }
        distanceHitList.Dispose();
    }
}