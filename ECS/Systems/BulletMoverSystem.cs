using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using TMG.AnimationCurves;
using Unity.Collections;

partial struct BulletMoverSystem : ISystem {
    private ComponentLookup<LocalTransform> localTransformComponentLookup;
    private ComponentLookup<ShootVictim> shootVictimComponentLookup;
    // const float BULLET_SPEED_MULTIPLIER = 1.5f;

    private Unity.Mathematics.Random _random;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        _random = new Unity.Mathematics.Random(1);
        localTransformComponentLookup = state.GetComponentLookup<LocalTransform>(false);
        shootVictimComponentLookup = state.GetComponentLookup<ShootVictim>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        localTransformComponentLookup.Update(ref state);
        shootVictimComponentLookup.Update(ref state);
        AccelerationCurveReference accelerationCurveReference = SystemAPI.GetSingleton<AccelerationCurveReference>();

        BulletMoverJob bulletMoverJob = new BulletMoverJob {
            LocalTransformComponentLookup = localTransformComponentLookup,
            ShootVictimComponentLookup = shootVictimComponentLookup,
            DeltaTime = SystemAPI.Time.DeltaTime,
            accelerationCurveReference = accelerationCurveReference
        };
        state.Dependency = bulletMoverJob.Schedule(state.Dependency);
    }
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(SquadRemoveUnitSystem))]
partial struct BulletDestructionSystem : ISystem {
    const float DESTROY_DISTANCE_SQUARED = 0.2f;
    // Volleys land 30+ arrows at once; only some of them get a hit sound. Artillery always does.
    const float ARROW_HIT_SFX_CHANCE = 0.35f;
    const float PROJECTILE_HIT_SFX_MAX_DISTANCE = 40f;
    private Unity.Mathematics.Random _random;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        _random = new Unity.Mathematics.Random(1);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer =
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        

        foreach (var (localTransform, arrow, target, entity) in SystemAPI.Query<
            RefRW<LocalTransform>, 
            RefRW<Bullet>, 
            RefRO<Target>
            >().WithEntityAccess()) {
            
            float3 targetPosition = arrow.ValueRO.bulletTargetPosition;
            // Check if close enough to damage target, only check in the x and z axis
            Vector3 targetPositionVector3 = new (targetPosition.x, 0, targetPosition.z);
            Vector3 localTransformPositionVector3 = new (localTransform.ValueRO.Position.x, 0, localTransform.ValueRO.Position.z);

            if (math.distancesq(targetPositionVector3, localTransformPositionVector3) > DESTROY_DISTANCE_SQUARED) continue;

            bool targetExists = state.EntityManager.Exists(target.ValueRO.targetEntity);

            //check if it has a request explosion component, if it does, create an explosion at the target position with the data from the RequestExplosion component
            if (state.EntityManager.HasComponent<RequestExplosion>(entity))
            {
                RequestExplosion requestedExplosion = state.EntityManager.GetComponentData<RequestExplosion>(entity);
                Entity explosionEntity = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(explosionEntity, new Explosion
                {
                    ExplosionPosition = targetPositionVector3,
                    KnockbackSquadID = requestedExplosion.KnockbackSquadID,
                    KnockbackSquadTeam = requestedExplosion.KnockbackSquadTeam,
                    KnockbackRange = requestedExplosion.KnockbackRange,
                    KnockbackForce = requestedExplosion.KnockbackForce,
                    KnockbackInitialDamage = requestedExplosion.KnockbackInitialDamage
                });

                //create an explosion VFX at the target position
                DynamicBuffer<BloodBufferElement> bloodVFXHolder = SystemAPI.GetSingletonBuffer<BloodBufferElement>();
                bloodVFXHolder.Add(new BloodBufferElement
                {
                    Position = targetPositionVector3,
                    IsExplosion = true
                });
            }

            if(targetExists)
            { 
                DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(target.ValueRO.targetEntity);
                damageBuffer.Add(new DamageBufferElement
                {
                    DamageType = DamageType.Physical,
                    DamageSource = DamageSource.Ranged,
                    AttackStrength = arrow.ValueRO.damageAmount,
                    TeamOfSource = arrow.ValueRO.Team,
                    DamageSourceSquadId = arrow.ValueRO.squadId,
                    DamageAttributes = arrow.ValueRO.damageAttributes,
                    FlankAttack = arrow.ValueRO.shotIntoFlanks,
                    Flaming = arrow.ValueRO.flaming,
                    SourceIsArtillery = arrow.ValueRO.sourceIsArtillery
                });

                if ((arrow.ValueRO.sourceIsArtillery || _random.NextFloat() < ARROW_HIT_SFX_CHANCE)
                    && SystemAPI.HasBuffer<SFXBufferElement>(target.ValueRO.targetEntity))
                {
                    DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(target.ValueRO.targetEntity);
                    sfxBuffer.Add(new SFXBufferElement { UnitName = arrow.ValueRO.shooterUnitName, SFXEntityType = Memori.Audio.SFXEntityType.ProjectileHit, MaxDistance = PROJECTILE_HIT_SFX_MAX_DISTANCE });
                }

                entityCommandBuffer.DestroyEntity(entity);

            } 
            else  // if target is dead, and is not an explosion, leave the arrow near the target position
            { 
                if(state.EntityManager.HasComponent<RequestExplosion>(entity))
                {
                    entityCommandBuffer.RemoveComponent<RequestExplosion>(entity);
                    continue;
                }

                RefRW<LocalTransform> bulletTrajectoryTransform = SystemAPI.GetComponentRW<LocalTransform>(arrow.ValueRO.bulletTrajectoryTransform);
                bulletTrajectoryTransform.ValueRW.Position = new float3(targetPosition.x * _random.NextFloat(0,0.2f), -0.25f, targetPosition.z* _random.NextFloat(0,0.2f));
                //rotation should be angled with the bullet initial position on the xz plane and be sticking into the ground at 45 degrees
                quaternion releaseRotation = quaternion.LookRotationSafe(localTransform.ValueRO.Position - arrow.ValueRO.bulletInitialPosition, math.up());
                quaternion finalRotation = Quaternion.Euler(45,0,0);
                bulletTrajectoryTransform.ValueRW.Rotation = math.mul(releaseRotation, finalRotation);
            }

            entityCommandBuffer.RemoveComponent<Bullet>(entity);
            entityCommandBuffer.RemoveComponent<Target>(entity);
        }
    }
}

[BurstCompile]
public partial struct BulletMoverJob : IJobEntity {
    public ComponentLookup<LocalTransform> LocalTransformComponentLookup;
    [ReadOnly] public ComponentLookup<ShootVictim> ShootVictimComponentLookup;
    [ReadOnly] public AccelerationCurveReference accelerationCurveReference;
    // public const float HEIGHT_MULTIPLIER = 0.1f;
    public float DeltaTime; // Delta time passed from the system
    static float AngleAtPoint(float point, AccelerationCurveReference accelerationCurveReference) {
            // Function to calculate the derivative of the curve equation at a given point
            float DerivativeAtPoint(float point) {
                float epsilon = 0.001f; // Small value for calculating the derivative
                float x1 = point - epsilon;
                float x2 = point + epsilon;
                float y1 = accelerationCurveReference.GetValueAtTime(x1);
                float y2 = accelerationCurveReference.GetValueAtTime(x2);

                // Approximate the derivative using the slope formula (change in y divided by change in x)
                return (y2 - y1) / (x2 - x1);
            }

            float derivative = DerivativeAtPoint(point);
            return Mathf.Rad2Deg * Mathf.Atan(derivative);
        }
     
    public void Execute (ref Bullet arrow, ref Target target, Entity entity) {

        float3 targetPosition = arrow.bulletTargetPosition;

        // if(LocalTransformComponentLookup.EntityExists(entity) == false) {
        //     // Debug.Log($"target not dead lmao");
        //     // return;
        // }

        if(LocalTransformComponentLookup.HasComponent(target.targetEntity)){
            //finsh arc then destroy
            LocalTransform targetLocalTransform = LocalTransformComponentLookup[target.targetEntity];
            arrow.bulletTargetPosition = targetLocalTransform.Position;
            targetPosition = targetLocalTransform.TransformPoint(ShootVictimComponentLookup[target.targetEntity].hitLocalPosition);
        // } else {
            // return;
            // Debug.Log($"target dead lmao");
            // targetIsDead = true;
        }

        // moving the arrow
        LocalTransform localTransform = LocalTransformComponentLookup[entity];
        float distanceBeforeSq = math.distancesq(localTransform.Position, targetPosition);
        float3 moveDirection = targetPosition - localTransform.Position;
        if (math.lengthsq(moveDirection) < 0.0001f) return;
        moveDirection = math.normalize(moveDirection);
        localTransform.Position += arrow.speed * DeltaTime * moveDirection;
        float distanceAfterSq = math.distancesq(localTransform.Position, targetPosition);
        if (distanceAfterSq > distanceBeforeSq) {// Overshot
            localTransform.Position = targetPosition;
        }

        LocalTransformComponentLookup[entity] = localTransform;

        float percentTravelled = arrow.totalDistance > 0f
            ? math.saturate(math.distance(arrow.bulletInitialPosition, localTransform.Position) / arrow.totalDistance)
            : 1f;

        LocalTransform bulletTrajectoryTransform = LocalTransformComponentLookup[arrow.bulletTrajectoryTransform];

        float animationCurveAngle = accelerationCurveReference.GetValueAtTime(percentTravelled);
        float height = animationCurveAngle * arrow.totalDistance * arrow.arcHeight;
        float angle = AngleAtPoint(percentTravelled, accelerationCurveReference) *.5f;//* 0.25f; //prevents the angle from being too steep

        //rotation should be angled with the move direction on the xz plane and the animation curve angle on the y axis
        quaternion rotation = quaternion.LookRotationSafe(moveDirection, math.up());
        quaternion yRotation = Quaternion.Euler(-angle, 0, 0);
        bulletTrajectoryTransform.Position = new float3(0, height, 0);
        bulletTrajectoryTransform.Rotation = math.mul(rotation, yRotation);

        LocalTransformComponentLookup[arrow.bulletTrajectoryTransform] = bulletTrajectoryTransform;
    }
}
