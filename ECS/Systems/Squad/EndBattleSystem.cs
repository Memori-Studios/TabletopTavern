using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using ProjectDawn.Navigation;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(SquadRemoveUnitSystem))]
partial struct EndBattleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        bool playerSquadsExist = false;
        bool enemySquadsExist = false;

        //destroy squads with no entities
        foreach (RefRO<SquadEntity> squad in SystemAPI.Query<RefRO<SquadEntity>>().WithNone<BrokenSquadTag, GarrisonGateSquadTag>())
        {
            if(entityManager.HasComponent<PlayerSquad>(squad.ValueRO.SelfEntity)) {
                playerSquadsExist = true;
            } else if(entityManager.HasComponent<EnemySquad>(squad.ValueRO.SelfEntity)) {
                enemySquadsExist = true;
            }
        }

        if(!playerSquadsExist)
        {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<BattlePhase>().Build();
            Entity entity = query.GetSingletonEntity();
            ecb.RemoveComponent<BattlePhase>(entity);

            Entity battleEndEntity = entityManager.CreateEntity();
            ecb.AddComponent(battleEndEntity, new BattleOver { 
                PlayerWon = false 
            });
        } 
        else if(!enemySquadsExist)
        {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<BattlePhase>().Build();
            Entity entity = query.GetSingletonEntity();
            ecb.RemoveComponent<BattlePhase>(entity);

            Entity battleEndEntity = entityManager.CreateEntity();
            ecb.AddComponent(battleEndEntity, new BattleOver { 
                PlayerWon = true 
            });
        }
    }
}