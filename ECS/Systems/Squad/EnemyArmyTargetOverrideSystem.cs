using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

partial struct EnemyArmyTargetOverrideSystem : ISystem
{
    private double lastExecutionTime;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        lastExecutionTime = 0;
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //only fire twice per second
        double currentTime = SystemAPI.Time.ElapsedTime;
        if (currentTime - lastExecutionTime < 0.5f) return;

        lastExecutionTime = currentTime;

        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        
        foreach (var (squad, squadMovementComponent, blacklist, enemySquad) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRO<SquadMovementComponent>,
            DynamicBuffer<TargetBlacklistElement>,
            EnemySquad
        >().WithNone<
            InCombat,
            WithdrawSquadTag>()
        .WithNone<
            CavalryFlankingTag>()
        // Mages are excluded: they have their own find-target system, exactly as artillery does,
        // and this override would undo it. It writes TargetSquadEntity directly and adds
        // StartChargeTag, so an enemy caster loses the squad MageSquadFindTargetSystem chose and
        // charges the nearest player squad instead - which for a FriendlyNearestEnemy buff caster
        // means abandoning the ally it was about to heal and walking the length of the field alone.
        // It also silently replaced DensestEnemyCluster with "closest" for every enemy Smite/Foxfire
        // caster. SquadRanOutOfAmmoSystem strips MageSquad once the charges are spent, so a burnt-out
        // mage is a plain melee body again and correctly falls back under this system.
        .WithNone<MageSquad>()
        ){

            // Prune expired kiter-blacklist entries so an abandoned target becomes eligible again.
            for (int b = blacklist.Length - 1; b >= 0; b--)
            {
                if (currentTime >= blacklist[b].ExpireTime) blacklist.RemoveAtSwapBack(b);
            }

            bool dazedFromOpponentRunningAway = entityManager.HasComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
            if(dazedFromOpponentRunningAway) {
                OpponentRanAwayTag opponentRanAwayTag = entityManager.GetComponentData<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                opponentRanAwayTag.DazedTime -= Time.deltaTime;
                entityManager.SetComponentData(squad.ValueRO.SelfEntity, opponentRanAwayTag);

                if(opponentRanAwayTag.DazedTime <= 0) {
                    entityCommandBuffer.RemoveComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                }
                continue;
            }

            Entity closestEnemySquadEntity = squad.ValueRO.TargetSquadEntity;
            if(closestEnemySquadEntity == Entity.Null) continue; //if not yet targeting an enemy squad, ignore this system

            float currentDistanceToTarget = math.distance(
                squadMovementComponent.ValueRO.SquadCenter, 
                entityManager.GetComponentData<SquadMovementComponent>(closestEnemySquadEntity).SquadCenter);

            // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is looking for targeting override, current distance to target {currentDistanceToTarget}");

            foreach (RefRO<SquadEntity> playerSquad in SystemAPI.Query<RefRO<SquadEntity>>().WithNone<BrokenSquadTag>())
            {
                //only target player squads
                if(playerSquad.ValueRO.SquadId < 0) continue;

                //ignore current target
                if(playerSquad.ValueRO.SelfEntity == squad.ValueRO.TargetSquadEntity) continue;

                //skip a target this squad just abandoned as an uncatchable kiter
                bool blacklisted = false;
                for (int b = 0; b < blacklist.Length; b++)
                {
                    if (blacklist[b].Target == playerSquad.ValueRO.SelfEntity) { blacklisted = true; break; }
                }
                if (blacklisted) continue;

                float3 centerOfSquad = squadMovementComponent.ValueRO.SquadCenter;
                float3 centerOfTargetSquad = entityManager.GetComponentData<SquadMovementComponent>(playerSquad.ValueRO.SelfEntity).SquadCenter;
                float distance = math.distance(centerOfSquad, centerOfTargetSquad);

                // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is checking targeting override, distance to squad {playerSquad.ValueRO.SquadId} is {distance}");

                if(distance < TabletopTavernConstants.OVERIDE_TARGET_SQUADENTITY_DISTANCE && distance < currentDistanceToTarget) {
                    currentDistanceToTarget = distance;
                    closestEnemySquadEntity = playerSquad.ValueRO.SelfEntity;
                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is overriding targeting {closestEnemySquadEntity.Index}");
                }
            }
            
            //if no closest enemy squad found, ignore this system
            if(closestEnemySquadEntity == squad.ValueRO.TargetSquadEntity) continue;

            squad.ValueRW.TargetSquadEntity = closestEnemySquadEntity;
            // float3 centerOfTarget = entityManager.GetComponentData<SquadMovementComponent>(closestEnemySquadEntity).SquadCenter;
            // float3 squadcenter = squadMovementComponent.ValueRO.SquadCenter;

            // float3 directionToTarget = math.normalize(centerOfTarget - squadcenter);
            // quaternion newRotation = quaternion.LookRotationSafe(directionToTarget, math.up());
            // entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new DirectionToRotateTo { Value = newRotation });
            entityCommandBuffer.AddComponent<StartChargeTag>(squad.ValueRO.SelfEntity);

            // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is targeting {closestEnemySquadEntity.Index}");
        }
    }
}

