using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;

partial struct ShieldedStanceSwitchSystem : ISystem {

    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SquadStatsData>();
        _random = new Unity.Mathematics.Random(1);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        ref var statsBlob = ref statsData.StatsBlob.Value; 

        foreach (var (squadEntity, entityBuffer, ShieldedStanceSquadComponent) in SystemAPI.Query<
            RefRO<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>, 
            RefRW<ShieldedStanceSquadComponent>>
        ()) {

            if(!ShieldedStanceSquadComponent.ValueRO.SwitchRequested) continue;

            ShieldedStanceSquadComponent.ValueRW.SwitchRequested = false;
            SquadStats squadStats = statsBlob.GetStats(squadEntity.ValueRO.UnitName);
            entityManager.SetComponentEnabled<DefensiveStanceTag>(squadEntity.ValueRO.SelfEntity, ShieldedStanceSquadComponent.ValueRO.Stance == ShieldedStance.Defensive);

            for (int i = 0; i < entityBuffer.Length; i++) 
            {
                Entity referencedEntity = entityBuffer[i].Entity;
                if(!entityManager.Exists(referencedEntity)) continue;


                if(entityManager.HasComponent<ShieldedStanceUnitComponent>(referencedEntity)) {
                    RefRW<ShieldedStanceUnitComponent> shieldedStanceUnit = SystemAPI.GetComponentRW<ShieldedStanceUnitComponent>(referencedEntity);
                    // The stat change is a delta, so a unit already in the requested stance must not get it again.
                    if(shieldedStanceUnit.ValueRO.Stance == ShieldedStanceSquadComponent.ValueRO.Stance) continue;
                    shieldedStanceUnit.ValueRW.Stance = ShieldedStanceSquadComponent.ValueRO.Stance;

                    if(entityManager.HasComponent<MeleeDefense>(referencedEntity)) 
                    {
                        ShieldedStance shieldedStance = ShieldedStanceSquadComponent.ValueRO.Stance;
                        int meleeDefenseValue = squadStats.MeleeDefense;
                        MeleeDefense meleeDefense = entityManager.GetComponentData<MeleeDefense>(referencedEntity);
                        if(shieldedStance == ShieldedStance.Balanced) {
                            meleeDefense.Value -= meleeDefenseValue / 2;
                        } else if (shieldedStance == ShieldedStance.Defensive) {
                            meleeDefense.Value += meleeDefenseValue / 2;
                        }
                        entityManager.SetComponentData(referencedEntity, meleeDefense);
                    }

                    if(entityManager.HasComponent<MeleeAttack>(referencedEntity)) 
                    {
                        ShieldedStance shieldedStance = ShieldedStanceSquadComponent.ValueRO.Stance;
                        int meleeAttackValue = squadStats.MeleeAttack;
                        MeleeAttack meleeAttack = entityManager.GetComponentData<MeleeAttack>(referencedEntity);
                        if(shieldedStance == ShieldedStance.Balanced) {
                            meleeAttack.MeleeAttackValue += meleeAttackValue / 2;
                        } else if (shieldedStance == ShieldedStance.Defensive) {
                            meleeAttack.MeleeAttackValue -= meleeAttackValue / 2;
                        }
                        entityManager.SetComponentData(referencedEntity, meleeAttack);
                    }

                    
                }
            }
        }
    }
}