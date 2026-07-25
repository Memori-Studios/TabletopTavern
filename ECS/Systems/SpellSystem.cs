using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;
using Unity.Transforms;
using ProjectDawn.Navigation;
using GPUECSAnimationBaker.Engine.AnimatorSystem;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TJ.ApplyDamageSystem))]
partial struct SpellSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
        NativeList<DistanceHit> distanceHitList = new NativeList<DistanceHit>(Allocator.Temp);
        float deltaTime = SystemAPI.Time.DeltaTime;

        CollisionFilter collisionFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
            GroupIndex = 0,
        };

        foreach (var (spellEntity, entity) in SystemAPI.Query<RefRW<SpellEntity>>().WithEntityAccess())
        {
            DamageBufferElement damageBufferElement = spellEntity.ValueRO.DamageBufferElement;
            float3 spellPosition = spellEntity.ValueRO.SpellPosition;
            float spellRadius = spellEntity.ValueRO.SpellRadius;

            Entity targetSquadEntity = spellEntity.ValueRO.TargetSquadEntity;
            if (targetSquadEntity != Entity.Null
                && SystemAPI.Exists(targetSquadEntity)
                && SystemAPI.HasComponent<SquadMovementComponent>(targetSquadEntity))
            {
                spellPosition = SystemAPI.GetComponent<SquadMovementComponent>(targetSquadEntity).SquadCenter;
            }

            // Persistent spells (HoT/DoT) apply once per TickInterval instead of every frame. One-off
            // spells and TickInterval == 0 keep applying immediately/every frame (backward compatible).
            bool doApply = spellEntity.ValueRO.IsOneOff;
            if (!spellEntity.ValueRO.IsOneOff)
            {
                spellEntity.ValueRW.TickTimer -= deltaTime;
                if (spellEntity.ValueRO.TickTimer <= 0f)
                {
                    doApply = true;
                    spellEntity.ValueRW.TickTimer = spellEntity.ValueRO.TickInterval;
                }
            }

            if (doApply)
            {
            distanceHitList.Clear();
            if (collisionWorld.OverlapSphere(spellPosition, spellRadius, ref distanceHitList, collisionFilter))
            {
                foreach (DistanceHit distanceHit in distanceHitList)
                {
                    Entity hitEntity = distanceHit.Entity;
                    if (!SystemAPI.Exists(hitEntity) || !SystemAPI.HasComponent<Unit>(hitEntity)) continue;

                    if (SystemAPI.HasComponent<Health>(hitEntity) && SystemAPI.GetComponent<Health>(hitEntity).Value <= 0) continue;

                    DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(hitEntity);
                    damageBuffer.Add(damageBufferElement);

                    bool shouldKnockback = damageBufferElement.DamageType != DamageType.Healing
                        && spellEntity.ValueRO.SpellForce > 0f
                        && SystemAPI.GetComponent<Unit>(hitEntity).Team != damageBufferElement.TeamOfSource
                        && !SystemAPI.HasComponent<ResistKnockbackTag>(hitEntity)
                        && !SystemAPI.HasComponent<ThrowUnit>(hitEntity);

                    if (!shouldKnockback) continue;

                    ecb.AddComponent(hitEntity, new ThrowUnit
                    {
                        Force = spellEntity.ValueRO.SpellForce,
                        HittingEntityLocation = spellPosition,
                        InitialLocation = SystemAPI.GetComponent<LocalTransform>(hitEntity).Position,
                        HittingEntitySquad = damageBufferElement.DamageSourceSquadId,
                        HittingEntityTeam = damageBufferElement.TeamOfSource,
                        Damage = 0, // damage already applied above via DamageBufferElement, avoid double-hit
                        RemainingTime = ExplosionSystem.THROW_LIFETIME,
                        TotalTime = ExplosionSystem.THROW_TOTAL_TIME
                    });

                    ecb.SetComponentEnabled<NavMeshPath>(hitEntity, false);
                    ecb.SetComponentEnabled<AgentSonarAvoid>(hitEntity, false);

                    AnimationDataHolder animationDataHolder = SystemAPI.GetComponent<AnimationDataHolder>(hitEntity);
                    Entity childEntity = animationDataHolder.gpuEcsAnimatorEntity;
                    if (childEntity == Entity.Null) continue;

                    GpuEcsAnimatorControlComponent controlComp = SystemAPI.GetComponent<GpuEcsAnimatorControlComponent>(childEntity);
                    controlComp.transitionSpeed = 0f;
                    controlComp.animatorInfo.animationID = animationDataHolder.thrownAnimationId;
                    ecb.SetComponent(childEntity, controlComp);

                    RefRW<AgentBody> agentBody = SystemAPI.GetComponentRW<AgentBody>(hitEntity);
                    agentBody.ValueRW.IsStopped = true;
                    agentBody.ValueRW.SetDestination(SystemAPI.GetComponent<LocalTransform>(hitEntity).Position);
                }
            }
            } // end if (doApply)

            if (spellEntity.ValueRO.IsOneOff)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            spellEntity.ValueRW.RemainingDuration -= deltaTime;
            if (spellEntity.ValueRO.RemainingDuration <= 0f) ecb.DestroyEntity(entity);
        }

        distanceHitList.Dispose();
    }
}
