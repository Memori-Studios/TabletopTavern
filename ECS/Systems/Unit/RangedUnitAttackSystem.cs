using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ProjectDawn.Navigation;
using Memori.Audio;
using Unity.Entities.UniversalDelegates;
// TO DO REFACTOR THIS TO REMOVE GETS
partial struct RangedUnitAttackSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        state.RequireForUpdate<BattlePhase>();
        _random = new Unity.Mathematics.Random(1);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var entityManager = state.EntityManager;

        foreach (var (localTransform, shootAttack, unitsTarget, rangedFireModeUnit, archerEntity) in SystemAPI
            .Query<
                RefRW<LocalTransform>, 
                RefRW<ShootAttack>, 
                RefRW<Target>,
                RefRO<RangedFireModeUnitComponent>>()
            .WithDisabled<MoveOverride>()
            .WithEntityAccess()
            .WithNone<ThrowUnit>() //prevent this system from running on units that are hit by spells
            )
        {
            // GarrisonGateUnit fires even while InCombat; all other ranged units do not
            if (entityManager.HasComponent<InCombat>(archerEntity) && !entityManager.HasComponent<GarrisonGateUnit>(archerEntity))
                continue;

            // Timing: tick first. The reset below is skipped while the unit has no target, so an
            // idle shooter's timer sits at or below zero and it fires the moment it acquires one.
            shootAttack.ValueRW.timer -= SystemAPI.Time.DeltaTime;

            // Play animation shootAnimationDelay seconds before the shot fires. The flag is latched
            // only once the animation actually plays - setting it unconditionally would consume it
            // while the unit sits idle without a target, and the first shot after acquiring one
            // would then spawn an arrow with no draw animation.
            if (!shootAttack.ValueRO.animationTriggered && shootAttack.ValueRO.timer <= shootAttack.ValueRO.shootAnimationDelay)
            {
                if (entityManager.HasComponent<LocalTransform>(unitsTarget.ValueRO.targetEntity) &&
                    entityManager.HasComponent<Unit>(unitsTarget.ValueRO.targetEntity))
                {
                    LocalTransform animTargetTF = SystemAPI.GetComponent<LocalTransform>(unitsTarget.ValueRO.targetEntity);
                    float3 animAimDir = animTargetTF.Position - localTransform.ValueRO.Position;
                    animAimDir.y = 0;
                    if (math.lengthsq(animAimDir) > 0.0001f)
                    {
                        animAimDir = math.normalize(animAimDir);
                        localTransform.ValueRW.Rotation = quaternion.LookRotation(animAimDir, math.up());
                        float3 animSpawnPos = localTransform.ValueRO.Position + math.mul(localTransform.ValueRO.Rotation, new float3(0, 2.5f, 1.75f));
                        shootAttack.ValueRW.animationTriggered = true;
                        shootAttack.ValueRW.onShoot.isTriggered = true;
                        shootAttack.ValueRW.onShoot.shootFromPosition = animSpawnPos;
                    }
                }
            }

            if (shootAttack.ValueRO.timer > 0f) continue;

            if (!entityManager.Exists(unitsTarget.ValueRO.targetEntity))
            {
                // With no target, hold the reload at zero rather than resetting it, so the unit
                // fires the instant one is acquired instead of waiting out a cycle it was already
                // partway through. Artillery is exempt and keeps cycling, which is what gives it a
                // wind-up before its opening shot. Gates are covered here too (they are not
                // ArtilleryUnit), preserving the immediate fire they already had.
                if (!entityManager.HasComponent<ArtilleryUnit>(archerEntity))
                    continue;
            }

            shootAttack.ValueRW.timer = shootAttack.ValueRO.timerMax;
            shootAttack.ValueRW.animationTriggered = false;

            //ranged unit fire mode
            if(rangedFireModeUnit.ValueRO.FireMode == RangedFireMode.FireAtWill)
            {
                float newTimerValue = shootAttack.ValueRO.timerMax / 2;
                //Randomize +/- 0.25 of the new timer value
                newTimerValue += newTimerValue * (_random.NextFloat() - 0.5f);
                shootAttack.ValueRW.timer = newTimerValue;
            }

            if(!entityManager.HasComponent<LocalTransform>(unitsTarget.ValueRO.targetEntity)) continue;

            if(!entityManager.HasComponent<Unit>(unitsTarget.ValueRO.targetEntity))
            {
                Debug.LogError($"Fuck");
                continue;
            }

            Unit targetUnitData = entityManager.GetComponentData<Unit>(unitsTarget.ValueRO.targetEntity);
            if (entityManager.Exists(targetUnitData.squadEntity) &&
                entityManager.HasComponent<BrokenSquadTag>(targetUnitData.squadEntity))
            {
                unitsTarget.ValueRW.targetEntity = Entity.Null;
                continue;
            }

            LocalTransform targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(unitsTarget.ValueRO.targetEntity);
            float3 aimDirection = targetLocalTransform.Position - localTransform.ValueRO.Position;
            aimDirection.y = 0;
            if (math.lengthsq(aimDirection) < 0.0001f) continue;
            aimDirection = math.normalize(aimDirection);
            quaternion targetRotation = quaternion.LookRotation(aimDirection, math.up());
            localTransform.ValueRW.Rotation = targetRotation;

            //spawning arrows
            Entity arrowEntity = state.EntityManager.Instantiate(shootAttack.ValueRO.ProjectileEntity);

            float3 arrowSpawnWorldPosition;
            if (entityManager.HasComponent<GarrisonGateUnit>(archerEntity) &&
                entityManager.HasComponent<UnitParentEntityTag>(archerEntity))
            {
                Entity squadEnt = entityManager.GetComponentData<UnitParentEntityTag>(archerEntity).parentSquadEntity;
                if (entityManager.HasComponent<GateFiringPoints>(squadEnt))
                {
                    GateFiringPoints pts = entityManager.GetComponentData<GateFiringPoints>(squadEnt);
                    arrowSpawnWorldPosition = pts.UsePointA ? pts.PointA : pts.PointB;
                    pts.UsePointA = !pts.UsePointA;
                    entityCommandBuffer.SetComponent(squadEnt, pts);
                }
                else
                {
                    arrowSpawnWorldPosition = localTransform.ValueRO.Position + math.mul(localTransform.ValueRO.Rotation, new float3(0, 2.5f, 1.75f));
                }
            }
            else
            {
                arrowSpawnWorldPosition = localTransform.ValueRO.Position + math.mul(localTransform.ValueRO.Rotation, new float3(0, 2.5f, 1.75f));
            }

            SystemAPI.SetComponent(arrowEntity, LocalTransform.FromPosition(arrowSpawnWorldPosition));

            RefRW<Bullet> bullet = SystemAPI.GetComponentRW<Bullet>(arrowEntity);
            bullet.ValueRW.damageAmount = shootAttack.ValueRO.damageAmount;
            bullet.ValueRW.bulletInitialPosition = arrowSpawnWorldPosition;
            bullet.ValueRW.bulletTargetPosition = targetLocalTransform.Position;
            bullet.ValueRW.totalDistance = math.distance(bullet.ValueRO.bulletInitialPosition, targetLocalTransform.Position);
            bullet.ValueRW.Team = entityManager.GetComponentData<EntityTeam>(archerEntity).Value;
            bullet.ValueRW.squadId = entityManager.GetComponentData<Unit>(archerEntity).squadId;
            bullet.ValueRW.shotIntoFlanks = entityManager.IsComponentEnabled<DealFlankingDamageTag>(archerEntity);
            if (bullet.ValueRO.shotIntoFlanks && entityManager.HasComponent<BackStabbersTag>(archerEntity)) {
                bullet.ValueRW.damageAmount = (int)(bullet.ValueRO.damageAmount * 2f);
            }
            bullet.ValueRW.flaming = entityManager.HasComponent<FlamingRangedAttackTag>(archerEntity);
            bullet.ValueRW.sourceIsArtillery = entityManager.HasComponent<ArtilleryUnit>(archerEntity);

            bool isArmorPiercing = entityManager.HasComponent<ArmorPiercingTag>(archerEntity);
            bool isAntiLarge = entityManager.HasComponent<AntiLargeTag>(archerEntity);
            bool isAntiInfantry = entityManager.HasComponent<AntiInfantryTag>(archerEntity);
            if(isArmorPiercing && isAntiLarge) {
                bullet.ValueRW.damageAttributes = DamageAttributes.ArmorPiercingAntiLarge;
            } else if(isArmorPiercing && isAntiInfantry) {
                bullet.ValueRW.damageAttributes = DamageAttributes.ArmorPiercingAntiInfantry;
            } else if(isArmorPiercing) {
                bullet.ValueRW.damageAttributes = DamageAttributes.ArmorPiercing;
            } else if(isAntiLarge) {
                bullet.ValueRW.damageAttributes = DamageAttributes.AntiLarge;
            } else if(isAntiInfantry) {
                bullet.ValueRW.damageAttributes = DamageAttributes.AntiInfantry;
            } else {
                bullet.ValueRW.damageAttributes = DamageAttributes.None;
            }
            RefRW<Target> arrowTarget = SystemAPI.GetComponentRW<Target>(arrowEntity);

            int accuracy = shootAttack.ValueRO.Accuracy;

            //fire mode affects accuracy - Steady Aim units shoot just as true in either mode
            if(rangedFireModeUnit.ValueRO.FireMode == RangedFireMode.FireAtWill &&
               !entityManager.HasComponent<SteadyAimTag>(archerEntity)) {
                accuracy -= TabletopTavernConstants.FIRE_AT_WILL_ACCURACY_PENALTY;
            }

            if(accuracy > _random.NextInt(0, 100)){
                arrowTarget.ValueRW.targetEntity = unitsTarget.ValueRO.targetEntity;
            } else {
                arrowTarget.ValueRW.targetEntity = Entity.Null;
            }

            // onShoot is triggered shootAnimationDelay seconds early (above); nothing to set here

            // add component to entity to indicate ammo spent (gates have infinite ammo)
            if (!entityManager.HasComponent<GarrisonGateUnit>(archerEntity))
                entityCommandBuffer.AddComponent<AmmuntionSpent>(archerEntity);

            //if artillery, play attack animation
            if(entityManager.HasComponent<ArtilleryUnit>(archerEntity)) 
            {
                ArtilleryUnit artillery = SystemAPI.GetComponent<ArtilleryUnit>(archerEntity);
                entityCommandBuffer.AddComponent(arrowEntity, new RequestExplosion
                {
                    KnockbackSquadID = artillery.SquadID,
                    KnockbackSquadTeam = Team.Neutral,
                    KnockbackRange = artillery.ExplosionRange,
                    KnockbackForce = artillery.ExplosionForce,
                    KnockbackInitialDamage = artillery.ExplosionDamage
                });

                // entityManager.SetComponentData(arrowEntity, new ApplyKnockbackOnContact { LifeTime = 10f });
                // entityCommandBuffer.SetComponentEnabled<ApplyKnockbackOnContact>(arrowEntity, true);
                
                //play artillery sfx
                UnitName unitNameArtillery = entityManager.GetComponentData<Unit>(archerEntity).unitName;
                DynamicBuffer<SFXBufferElement> sfxBufferArtillery = SystemAPI.GetBuffer<SFXBufferElement>(archerEntity);
                sfxBufferArtillery.Add(new SFXBufferElement { UnitName = unitNameArtillery, SFXEntityType = SFXEntityType.FireProjectile, MaxDistance = 120f });
                continue;
            }

            //sfx
            if (_random.NextFloat() > 0.5f) continue;

            UnitName unitName = entityManager.GetComponentData<Unit>(archerEntity).unitName;
            DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(archerEntity);
            sfxBuffer.Add(new SFXBufferElement { UnitName = unitName, SFXEntityType = SFXEntityType.FireProjectile, MaxDistance = 60f });
        }
    }
}