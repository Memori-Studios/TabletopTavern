using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using ProjectDawn.Navigation;
using System.Collections.Generic;
using TabletopTavern.GpuAnim;

partial struct RangedMeleeConverterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // state.RequireForUpdate<BattlePhase>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer =
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (AnimationDataHolder, RangedMeleeConverter, Entity) in SystemAPI.Query<RefRW<AnimationDataHolder>, RefRW<RangedMeleeConverter>>().WithEntityAccess())
        {
            if (!entityManager.HasComponent<LocalTransform>(RangedMeleeConverter.ValueRO.BowEntity) || !entityManager.HasComponent<LocalTransform>(RangedMeleeConverter.ValueRO.SwordEntity))
            {
                // Debug.LogError($"RangedMeleeConverterSystem: Entity {Entity} is missing Bow or Sword entity.");
                continue;
            }
            
            if(RangedMeleeConverter.ValueRO.SwitchToMelee)
            {
                AnimationDataHolder.ValueRW.attackIdleAnimationId = TabletopTavernConstants.MELEE_ATTACK_IDLE_ID;
                AnimationDataHolder.ValueRW.attackanimationId = TabletopTavernConstants.MELEE_ATTACK_ID;
                AnimationDataHolder.ValueRW.currentIdleAnimationId = AnimationDataHolder.ValueRO.attackIdleAnimationId;

                GpuAnimControl controlComp = entityManager.GetComponentData<GpuAnimControl>(AnimationDataHolder.ValueRO.gpuEcsAnimatorEntity);
                controlComp.Slot = AnimationDataHolder.ValueRO.attackIdleAnimationId;
                entityManager.SetComponentData(AnimationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);

                LocalTransform bowTransform = entityManager.GetComponentData<LocalTransform>(RangedMeleeConverter.ValueRO.BowEntity);
                bowTransform.Scale = 0f;
                entityManager.SetComponentData(RangedMeleeConverter.ValueRO.BowEntity, bowTransform);

                LocalTransform swordTransform = entityManager.GetComponentData<LocalTransform>(RangedMeleeConverter.ValueRO.SwordEntity);
                swordTransform.Scale = 1f;
                entityManager.SetComponentData(RangedMeleeConverter.ValueRO.SwordEntity, swordTransform);

                //here
                if(entityManager.HasComponent<ShootAttack>(Entity) && !entityManager.HasComponent<GarrisonGateUnit>(Entity))
                    entityCommandBuffer.SetComponentEnabled<ShootAttack>(Entity, false);
                // Debug.Log($"RangedMeleeConverterSystem: is switching to melee");

                //here remove any current targets
                if (entityManager.HasComponent<NeedsToBeProcessed>(Entity))
                {
                    entityCommandBuffer.RemoveComponent<NeedsToBeProcessed>(Entity);
                }

                if (entityManager.HasComponent<UnitFindTarget>(Entity))
                {
                    entityCommandBuffer.SetComponent(Entity, new Target { targetEntity = Entity.Null });
                    entityCommandBuffer.SetComponentEnabled<UnitFindTarget>(Entity, true);
                }
            }
            else 
            {
                AnimationDataHolder.ValueRW.attackIdleAnimationId = TabletopTavernConstants.RANGED_ATTACK_IDLE_ID;
                AnimationDataHolder.ValueRW.attackanimationId = TabletopTavernConstants.RANGED_ATTACK_ID;
                AnimationDataHolder.ValueRW.currentIdleAnimationId = AnimationDataHolder.ValueRO.idleAnimationId;

                GpuAnimControl controlComp = entityManager.GetComponentData<GpuAnimControl>(AnimationDataHolder.ValueRO.gpuEcsAnimatorEntity);
                controlComp.Slot = AnimationDataHolder.ValueRO.idleAnimationId;
                entityManager.SetComponentData(AnimationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);

                LocalTransform bowTransform = entityManager.GetComponentData<LocalTransform>(RangedMeleeConverter.ValueRO.BowEntity);
                bowTransform.Scale = 1f;
                entityManager.SetComponentData(RangedMeleeConverter.ValueRO.BowEntity, bowTransform);

                LocalTransform swordTransform = entityManager.GetComponentData<LocalTransform>(RangedMeleeConverter.ValueRO.SwordEntity);
                swordTransform.Scale = 0f;
                entityManager.SetComponentData(RangedMeleeConverter.ValueRO.SwordEntity, swordTransform);

                if(entityManager.HasComponent<ShootAttack>(Entity))
                    entityCommandBuffer.SetComponentEnabled<ShootAttack>(Entity, true);
                // Debug.Log($"RangedMeleeConverterSystem: is switching to ranged");
            }

            entityCommandBuffer.SetComponentEnabled<RangedMeleeConverter>(Entity, false);
        }
    }
}