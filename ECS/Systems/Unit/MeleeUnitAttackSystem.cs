using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using TJ;
using Unity.Mathematics;
using Unity.Transforms;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using Memori.Audio;
using ProjectDawn.Navigation;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
partial struct MeleeUnitAttackSystem : ISystem
{
    private ComponentLookup<LocalTransform> localTransformComponentLookup;
    private ComponentLookup<MoveOverride> moveOverrideComponentLookup;
    private ComponentLookup<InMeleeRange> inMeleeRangeLookup;
    private Unity.Mathematics.Random _random;
    private EntityQuery _infantryMeleeQuery;
    private EntityQuery _largeMeleeQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FindTargets>();
        localTransformComponentLookup = state.GetComponentLookup<LocalTransform>(true);
        moveOverrideComponentLookup = state.GetComponentLookup<MoveOverride>(true);
        inMeleeRangeLookup = state.GetComponentLookup<InMeleeRange>(false);
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
        _infantryMeleeQuery = SystemAPI.QueryBuilder()
            .WithAll<MeleeAttack, Target, SetDestination>()
            .WithPresent<InCombat>()
            .WithAbsent<ThrowUnit, LargeTag, GarrisonGateUnit>()
            .WithDisabled<MoveOverride>()
            .Build();
        _largeMeleeQuery = SystemAPI.QueryBuilder()
            .WithAll<MeleeAttack, Target, SetDestination, LargeTag>()
            .WithPresent<InCombat>()
            .WithAbsent<ThrowUnit, GarrisonGateUnit>()
            .WithDisabled<MoveOverride>()
            .Build();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((
            RefRO<Unit> unit,
            RefRW<LocalTransform> localTransform,
            RefRW<MeleeAttack> meleeAttack,
            RefRW<Target> target,
            Entity entity)
            in SystemAPI.Query<
                RefRO<Unit>,
                RefRW<LocalTransform>,
                RefRW<MeleeAttack>,
                RefRW<Target>>()
                .WithPresent<InCombat>()
                .WithDisabled<MoveOverride>()
                .WithEntityAccess()
                .WithNone<ThrowUnit>() //prevent this system from running on units that are hit by spells
        ) {

            if(!meleeAttack.ValueRO.onAttack) continue;

            meleeAttack.ValueRW.onAttack = false;

            if (!entityManager.HasComponent<LocalTransform>(target.ValueRO.targetEntity))
            {
                // Debug.Log($"MeleeUnitAttackSystem: {entity} target is dead");
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }
            if (entityManager.IsComponentEnabled<RetreatingUnit>(target.ValueRO.targetEntity))
            {
                // Debug.Log($"MeleeUnitAttackSystem: {entity} target is retreating");
                target.ValueRW.targetEntity = Entity.Null;
                continue;
            }
            
            if (entityManager.IsComponentEnabled<ApplyKnockbackOnContact>(entity))
            {
                // Debug.Log($"MeleeUnitAttackSystem: {entity} is already applying knockback");
                if (!entityManager.HasComponent<RequestExplosion>(entity)) continue;
                RequestExplosion requestedExplosion = entityManager.GetComponentData<RequestExplosion>(entity);
                Entity explosionEntity = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(explosionEntity, new Explosion
                {
                    ExplosionPosition = localTransform.ValueRO.Position,
                    KnockbackSquadID = requestedExplosion.KnockbackSquadID,
                    KnockbackSquadTeam = requestedExplosion.KnockbackSquadTeam,
                    KnockbackRange = requestedExplosion.KnockbackRange,
                    KnockbackForce = requestedExplosion.KnockbackForce,
                    KnockbackInitialDamage = requestedExplosion.KnockbackInitialDamage
                });
                entityCommandBuffer.SetComponentEnabled<Explosion>(explosionEntity, true);
            }

            //reset timer
            meleeAttack.ValueRW.timer = meleeAttack.ValueRO.timerMax + _random.NextFloat(-0.2f, 0.2f);
            
            //flanking check
            bool flankAttack = entityManager.IsComponentEnabled<DealFlankingDamageTag>(entity);

            // bool flamingAttack = entityManager.IsComponentEnabled<FlamingRangedAttackTag>(entity);

            //play attack animation
            RefRO<AnimationDataHolder> gpuEcsAnimatorAspect = SystemAPI.GetComponentRO<AnimationDataHolder>(entity);
            RefRW<GpuEcsAnimatorControlComponent> controlComp = SystemAPI.GetComponentRW<GpuEcsAnimatorControlComponent>(gpuEcsAnimatorAspect.ValueRO.gpuEcsAnimatorEntity);
            int animationId = _random.NextInt(0, 100) > 10 ? 
                gpuEcsAnimatorAspect.ValueRO.attackanimationId : gpuEcsAnimatorAspect.ValueRO.altattackanimationId;

            controlComp.ValueRW.animatorInfo.animationID = animationId;
            RefRW<GpuEcsAnimatorControlStateComponent> controlStateComp = SystemAPI.GetComponentRW<GpuEcsAnimatorControlStateComponent>(gpuEcsAnimatorAspect.ValueRO.gpuEcsAnimatorEntity);
            controlStateComp.ValueRW.state = GpuEcsAnimatorControlStates.Start;

            //if cavalry, play rider attack animation
            if(SystemAPI.HasComponent<Cavalry>(entity)) {
                Cavalry cavalry = SystemAPI.GetComponent<Cavalry>(entity);
                RefRW<GpuEcsAnimatorControlComponent> controlComp2 = SystemAPI.GetComponentRW<GpuEcsAnimatorControlComponent>(cavalry.riderEntity);
                controlComp2.ValueRW.animatorInfo.animationID = 1;
            }

            //sfx
            if (_random.NextFloat() > 0.5f)
            {
                DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(entity);
                sfxBuffer.Add(new SFXBufferElement { UnitName = unit.ValueRO.unitName, SFXEntityType = Memori.Audio.SFXEntityType.MeleeAttack, MaxDistance = 60f });
                // if(SystemAPI.HasComponent<PlayAudioClipOnDamageData>(entity)) {
                //     entityCommandBuffer.SetComponentEnabled<PlayAudioClipOnDamageData>(entity, true);
                // }
            }

            //attempt to apply damage
            int meleeDefense = SystemAPI.GetComponent<MeleeDefense>(target.ValueRO.targetEntity).Value;
            if (flankAttack) {
                meleeDefense = (int)(meleeDefense * 0.5f); //50% reduction to melee defense on flanking attacks
            }
            
            int meleeAttackValue = meleeAttack.ValueRO.MeleeAttackValue;

            int hitChance = TabletopTavernConstants.MELEE_BASE_HIT_CHANCE
                + (meleeAttackValue - meleeDefense) * TabletopTavernConstants.MELEE_HIT_CHANCE_PER_POINT;
            hitChance = math.clamp(hitChance, TabletopTavernConstants.MELEE_HIT_CHANCE_MIN, TabletopTavernConstants.MELEE_HIT_CHANCE_MAX);
            if(_random.NextInt(0, 100) > hitChance) {
                // Debug.Log($"MeleeUnitAttackSystem: {entity} missed the attack");
                continue;
            }

            //apply damage
            DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(target.ValueRO.targetEntity);
            DamageAttributes DamageAttributes = DamageAttributes.None;

            if(SystemAPI.HasComponent<ArmorPiercingTag>(entity)) {
                if(SystemAPI.HasComponent<AntiInfantryTag>(entity)) {
                    DamageAttributes = DamageAttributes.ArmorPiercingAntiInfantry;
                } else if(SystemAPI.HasComponent<AntiLargeTag>(entity)) {
                    DamageAttributes = DamageAttributes.ArmorPiercingAntiLarge;
                } else {
                    DamageAttributes = DamageAttributes.ArmorPiercing;
                }
            } else if(SystemAPI.HasComponent<AntiInfantryTag>(entity)) {
                DamageAttributes = DamageAttributes.AntiInfantry;
            } else if(SystemAPI.HasComponent<AntiLargeTag>(entity)) {
                DamageAttributes = DamageAttributes.AntiLarge;
            }

            if(SystemAPI.HasComponent<MonsterTag>(entity) && !SystemAPI.HasComponent<LargeTag>(target.ValueRO.targetEntity)) 
            {
                // Debug.Log($"thrown: {entity} is a monster and {target.ValueRO.targetEntity} is not a large unit, applying damage anyway");

                MonsterTag monsterTag = SystemAPI.GetComponent<MonsterTag>(entity);
                Entity monsterExplosionEntity = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(monsterExplosionEntity, new Explosion {
                    ExplosionPosition = localTransform.ValueRO.Position + math.forward(localTransform.ValueRO.Rotation) * 2f,
                    KnockbackSquadID = unit.ValueRO.squadId,
                    KnockbackSquadTeam = unit.ValueRO.Team,
                    KnockbackRange = monsterTag.KnockbackRange,
                    KnockbackForce = 5f,
                    KnockbackInitialDamage = monsterTag.KnockbackInitialDamage,
                    Delay = 1.3f,
                });
                entityCommandBuffer.SetComponentEnabled<Explosion>(monsterExplosionEntity, true);
                
                // entityCommandBuffer.AddComponent(target.ValueRO.targetEntity, 
                //     new ForceLifetime { RemainingTime = 1f , TotalTime = 2.5f}
                // );

                // NavMeshPath is deliberately NOT disabled here. ExplosionSystem disables it on the units
                // the delayed blast actually throws and UnitThrownSystem re-enables it when the throw ends;
                // disabling it on every hit left any target the blast missed (moved away, already thrown,
                // resists knockback) walking in straight lines through walls for the rest of the battle.
                if (!entityManager.HasComponent<AnimationDataHolder>(target.ValueRO.targetEntity)) continue;
                AnimationDataHolder animationDataHolder = entityManager.GetComponentData<AnimationDataHolder>(target.ValueRO.targetEntity);
                Entity childEntity = animationDataHolder.gpuEcsAnimatorEntity;

                GpuEcsAnimatorControlComponent controlComp3 = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(animationDataHolder.gpuEcsAnimatorEntity);
                controlComp3.transitionSpeed = 0f;
                controlComp3.animatorInfo.animationID = animationDataHolder.thrownAnimationId;

                RefRW<AgentBody> agentBody = SystemAPI.GetComponentRW<AgentBody>(target.ValueRO.targetEntity);
                agentBody.ValueRW.IsStopped = true;
                agentBody.ValueRW.SetDestination(SystemAPI.GetComponent<LocalTransform>(target.ValueRO.targetEntity).Position);

                entityCommandBuffer.SetComponent(childEntity, controlComp3);

                // UnityEngine.Debug.Log($"MeleeUnitAttackSystem: {unit.ValueRO.unitName} attacked {target.ValueRO.targetEntity} with {meleeAttackValue} damage and attributes {DamageAttributes}");     
            }

            int meleeAttackStrength = meleeAttack.ValueRO.WeaponStrength;
            if(flankAttack && SystemAPI.HasComponent<BackStabbersTag>(entity)) {
                meleeAttackStrength = (int)(meleeAttackStrength * 2f); 
            }


            damageBuffer.Add(new DamageBufferElement
            {
                DamageType = DamageType.Physical,
                DamageSource = DamageSource.Melee,
                AttackStrength = meleeAttackStrength,
                TeamOfSource = unit.ValueRO.Team,
                DamageSourceSquadId = unit.ValueRO.squadId,
                DamageAttributes = DamageAttributes,
                FlankAttack = flankAttack
            });
            // UnityEngine.Debug.Log($"MeleeUnitAttackSystem: {unit.ValueRO.unitName} attacked {target.ValueRO.targetEntity} with {meleeAttackValue} damage and attributes {DamageAttributes}");     
        }

        localTransformComponentLookup.Update(ref state);
        moveOverrideComponentLookup.Update(ref state);
        inMeleeRangeLookup.Update(ref state);

        MeleeUnitCombatJob infantryJob = new MeleeUnitCombatJob {
            LocalTransformComponentLookup = localTransformComponentLookup,
            MoveOverrideComponentLookup = moveOverrideComponentLookup,
            InMeleeRangeLookup = inMeleeRangeLookup,
            DeltaTime = SystemAPI.Time.DeltaTime,
            random = _random,
            AttackDistance = TabletopTavernConstants.MELEE_ATTACK_DISTANCE,
            IsLargeJob = false
        };
        state.Dependency = infantryJob.Schedule(_infantryMeleeQuery, state.Dependency);

        MeleeUnitCombatJob largeJob = new MeleeUnitCombatJob {
            LocalTransformComponentLookup = localTransformComponentLookup,
            MoveOverrideComponentLookup = moveOverrideComponentLookup,
            InMeleeRangeLookup = inMeleeRangeLookup,
            DeltaTime = SystemAPI.Time.DeltaTime,
            random = _random,
            AttackDistance = TabletopTavernConstants.MELEE_ATTACK_DISTANCE * 1.5f,
            IsLargeJob = true
        };
        state.Dependency = largeJob.Schedule(_largeMeleeQuery, state.Dependency);
        }
    }

// [BurstCompile]
[WithPresent(typeof(InCombat))]
[WithAbsent(typeof(ThrowUnit))]
[WithDisabled(typeof(MoveOverride))]
public partial struct MeleeUnitCombatJob : IJobEntity {
    [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformComponentLookup;
    [ReadOnly] public ComponentLookup<MoveOverride> MoveOverrideComponentLookup;
    public ComponentLookup<InMeleeRange> InMeleeRangeLookup;
    [ReadOnly] public float DeltaTime;
    public Unity.Mathematics.Random random;
    [ReadOnly] public float AttackDistance;
    [ReadOnly] public bool IsLargeJob;

    public void Execute (ref MeleeAttack meleeAttack, ref Target target, ref SetDestination setDestination, Entity entity)
    {
        if (target.targetEntity == Entity.Null) return;
        if(entity == Entity.Null) return;

        LocalTransform localTransform = LocalTransformComponentLookup[entity];

        if(MoveOverrideComponentLookup.IsComponentEnabled(target.targetEntity))
        {
            target.targetEntity = Entity.Null;
            meleeAttack.timer = meleeAttack.timerMax;
            if (InMeleeRangeLookup.HasComponent(entity))
                InMeleeRangeLookup.SetComponentEnabled(entity, false);
            return;
        }

        meleeAttack.timer -= DeltaTime;

        LocalTransform targetLocalTransform = LocalTransformComponentLookup[target.targetEntity];
        float distanceToTarget = math.distancesq(localTransform.Position, targetLocalTransform.Position);
        bool isCloseEnoughToAttack = distanceToTarget < AttackDistance;

        if (InMeleeRangeLookup.HasComponent(entity))
            InMeleeRangeLookup.SetComponentEnabled(entity, isCloseEnoughToAttack);

        if (!isCloseEnoughToAttack)
        {
            setDestination.destinationPosition = targetLocalTransform.Position;
            return;
        }

        // In attack range — hold position so the nav agent stops and IsStopped can fire.
        setDestination.destinationPosition = localTransform.Position;

        if (meleeAttack.timer > 0) return;

        //reset timer
        meleeAttack.timer = meleeAttack.timerMax + random.NextFloat(-0.2f, 0.2f);
        meleeAttack.onAttack = true;
    }
}