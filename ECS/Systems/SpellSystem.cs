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
        double elapsedTime = SystemAPI.Time.ElapsedTime;
        // GetSingleton<PhysicsWorldSingleton>() does not guarantee the broadphase build jobs have
        // finished by the time this variable-rate system runs, and an OverlapSphere against a
        // half-built tree returns nothing. Measured 2026-09-13: about one persistent-spell tick in
        // eight found 0 units at a point the next tick found 42 (NumBodies unchanged), and 52/52
        // ticks landed once jobs were completed first. The sync point is paid at most once per
        // frame, and only on frames where a spell actually applies.
        bool jobsCompleted = false;

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
            if (!jobsCompleted)
            {
                state.EntityManager.CompleteAllTrackedJobs();
                collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
                jobsCompleted = true;
            }
            distanceHitList.Clear();
            if (collisionWorld.OverlapSphere(spellPosition, spellRadius, ref distanceHitList, collisionFilter))
            {
                // Execute strike: keep only the living unit nearest the strike point. Resolved up front
                // so the loop below can stay one code path for both shapes.
                Entity singleTarget = Entity.Null;
                if (spellEntity.ValueRO.HitsSingleUnit)
                {
                    float bestDistance = float.MaxValue;
                    foreach (DistanceHit candidate in distanceHitList)
                    {
                        Entity e = candidate.Entity;
                        if (!SystemAPI.Exists(e) || !SystemAPI.HasComponent<Unit>(e)) continue;
                        if (SystemAPI.HasComponent<Health>(e) && SystemAPI.GetComponent<Health>(e).Value <= 0) continue;
                        if (SystemAPI.GetComponent<Unit>(e).Team == damageBufferElement.TeamOfSource) continue;
                        if (candidate.Distance >= bestDistance) continue;
                        bestDistance = candidate.Distance;
                        singleTarget = e;
                    }
                }

                foreach (DistanceHit distanceHit in distanceHitList)
                {
                    Entity hitEntity = distanceHit.Entity;
                    if (spellEntity.ValueRO.HitsSingleUnit && hitEntity != singleTarget) continue;
                    if (!SystemAPI.Exists(hitEntity) || !SystemAPI.HasComponent<Unit>(hitEntity)) continue;

                    if (SystemAPI.HasComponent<Health>(hitEntity) && SystemAPI.GetComponent<Health>(hitEntity).Value <= 0) continue;

                    DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(hitEntity);
                    damageBuffer.Add(damageBufferElement);

                    // Zone spells have no squad-level tag, so each tick stamps the parent squad's status entry
                    // to outlive the next tick by half a second; a squad that leaves the zone drops it then.
                    int statusSpellId = spellEntity.ValueRO.StatusSpellId;
                    if (statusSpellId > 0 && !spellEntity.ValueRO.IsOneOff && SystemAPI.HasComponent<UnitParentEntityTag>(hitEntity))
                    {
                        Entity parentSquad = SystemAPI.GetComponent<UnitParentEntityTag>(hitEntity).parentSquadEntity;
                        if (SystemAPI.HasBuffer<SpellStatusBufferElement>(parentSquad))
                            SpellStatus.Set(SystemAPI.GetBuffer<SpellStatusBufferElement>(parentSquad), statusSpellId,
                                elapsedTime + spellEntity.ValueRO.TickInterval + 0.5, elapsedTime);
                    }

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
