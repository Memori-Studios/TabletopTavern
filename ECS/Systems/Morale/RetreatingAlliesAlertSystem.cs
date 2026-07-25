using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

namespace TJ.Morale
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MoraleSystem))]
    public partial struct RetreatingAlliesAlertSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattlePhase>();
        }

        // Not [BurstCompile]: reads the managed RaceBonusRuleData.SanguineCourt immunity toggle so
        // mods can turn Sanguine Court's retreating-allies morale immunity on/off. This system is
        // event-driven (only iterates when a squad is breaking), so the cost is negligible.
        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;
            EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            bool sanguineImmune = RaceBonusRuleData.SanguineCourt.ImmuneToRetreatingAlliesMorale;
    
            foreach (var (squad, squadMovementComponent, alertNearbyUnitsOfBreakingTag) in
                SystemAPI.Query<RefRO<SquadEntity>, RefRO<SquadMovementComponent>, RefRO<AlertNearbyUnitsOfBreakingTag>>())
            {
                // Debug.Log($"Squad {squad.ValueRO.SquadId} is breaking and will alert nearby units!");

                //query all squad entities and check distance to breaking squad, if within certain radius, add RetreatingNearbyAllies component
                float3 centerOfSquad = squadMovementComponent.ValueRO.SquadCenter;
                bool searchForEnemySquads = squad.ValueRO.SquadId > 0;

                foreach (var (enemySquad, enemyMovement) in 
                    SystemAPI.Query<RefRO<SquadEntity>, RefRO<SquadMovementComponent>>()
                    .WithNone<BrokenSquadTag>()
                    .WithPresent<RetreatingNearbyAllies>())
                {
                    // Debug.Log($"Checking squad {enemySquad.ValueRO.SquadId} for retreating allies alert");

                    if(enemySquad.ValueRO.SquadId == squad.ValueRO.SquadId) continue; // Skip self

                    if(searchForEnemySquads) {
                        if(enemySquad.ValueRO.SquadId < 0) continue;
                    } else {
                        if(enemySquad.ValueRO.SquadId > 0) continue;
                    }
                    
                    float3 centerOfTarget = enemyMovement.ValueRO.SquadCenter;
                    float distance = math.distance(centerOfSquad, centerOfTarget);
                    // Debug.Log($"Checking squad {enemySquad.ValueRO.SquadId} at distance {distance}");

                    if(distance < TabletopTavernConstants.NEARBY_ALLIES_RETREATING_DISTANCE)
                    {
                        if (sanguineImmune && entityManager.HasComponent<SanguineCourtRaceTag>(enemySquad.ValueRO.SelfEntity)) continue;
                        // Debug.Log($"Alerting squad {enemySquad.ValueRO.SquadId} of nearby retreating allies!");
                        entityManager.SetComponentData(enemySquad.ValueRO.SelfEntity, new RetreatingNearbyAllies { AlertTimer = TabletopTavernConstants.NEARBY_ALLIES_RETREATING_PENALTY_TIME });
                        entityManager.SetComponentEnabled<RetreatingNearbyAllies>(enemySquad.ValueRO.SelfEntity, true);
                    }
                }

                entityCommandBuffer.RemoveComponent<AlertNearbyUnitsOfBreakingTag>(squad.ValueRO.SelfEntity);
            }
          
        }
    }

}