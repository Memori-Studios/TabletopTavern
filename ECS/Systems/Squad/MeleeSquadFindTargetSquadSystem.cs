using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

partial struct SquadFindTargetSquadSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    private EntityQuery _nonBrokenSquadQuery;
    private ComponentLookup<SquadMovementComponent> _squadMovementLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
        _nonBrokenSquadQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SquadEntity, SquadMovementComponent>()
            .WithNone<BrokenSquadTag>()
            .Build(ref state);
        _squadMovementLookup = state.GetComponentLookup<SquadMovementComponent>(true);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        _squadMovementLookup.Update(ref state);

        double now = SystemAPI.Time.ElapsedTime;

        // Collect all non-broken squad data once per frame instead of querying inside the loop
        var allSquadData = _nonBrokenSquadQuery.ToComponentDataArray<SquadEntity>(Allocator.Temp);

        //Squad Finding Target
        foreach (var (squad, squadMovementComponent, SquadOverridesComponent, queuedOrders, blacklist) in SystemAPI.Query<RefRW<
            SquadEntity>,
            RefRO<SquadMovementComponent>,
            SquadOverridesComponent,
            DynamicBuffer<QueuedOrder>,
            DynamicBuffer<TargetBlacklistElement>
        >()
        .WithNone<
            ChargeSquad,
            InCombat,
            WithdrawSquadTag>()
        // MageSquad is excluded here. This list predates UnitType.Mage and only named RangedSquad,
        // the archer/artillery marker, so every mage fell through and was targeted as melee: nearest
        // enemy, issued as an Attack order, which added ChargeSquad and locked MageSquadFindTargetSystem
        // out for the rest of the battle (it carries .WithNone<ChargeSquad>()).
        //
        // Invisible for offensive casters, since nearest-enemy is a plausible answer for them, though it
        // did quietly override DensestEnemyCluster. A FriendlyNearestEnemy buff caster is what exposed
        // it: it kept being handed an enemy and charging across the field alone.
        //
        // SquadRanOutOfAmmoSystem swaps MageSquad for MeleeSquad once charges are spent, so a burnt-out
        // mage correctly rejoins melee targeting here.
        .WithNone<
            CavalryFlankingTag,
            SquadMoveOverrideTag,
            RangedSquad>()
        .WithNone<MageSquad>()
        .WithNone<
            StartChargeTag>()
        ){
            // Debug.Log($"Squad {squad.ValueRO.SquadId} is finding a target");

            // Prune expired kiter-blacklist entries, then a helper to skip still-blacklisted targets.
            for (int b = blacklist.Length - 1; b >= 0; b--)
            {
                if (now >= blacklist[b].ExpireTime) blacklist.RemoveAtSwapBack(b);
            }
            bool IsBlacklisted(Entity candidate)
            {
                for (int b = 0; b < blacklist.Length; b++)
                {
                    if (blacklist[b].Target == candidate) return true;
                }
                return false;
            }

            //check if an enemy is already attacking
            int AttackASquadAttackingMe()
            {
                for (int i = 0; i < allSquadData.Length; i++)
                {
                    var enemySquadData = allSquadData[i];
                    bool isEnemy = enemySquadData.SquadId < 0;
                    if (isEnemy != (squad.ValueRO.SquadId > 0)) continue;
                    
                    //check if squad has incombat component and if its target is this squad
                    if(!entityManager.HasComponent<InCombat>(enemySquadData.SelfEntity)) continue;

                    if(enemySquadData.TargetSquadEntity == squad.ValueRO.SelfEntity) 
                    {
                        return enemySquadData.SquadId;
                    }

                }
                return 0;
            }

            bool dazedFromOpponentRunningAway = entityManager.HasComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);

            if(dazedFromOpponentRunningAway) 
            {
                OpponentRanAwayTag opponentRanAwayTag = entityManager.GetComponentData<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                opponentRanAwayTag.DazedTime -= SystemAPI.Time.DeltaTime;
                entityManager.SetComponentData(squad.ValueRO.SelfEntity, opponentRanAwayTag);

                if(opponentRanAwayTag.DazedTime <= 0) {
                    entityCommandBuffer.RemoveComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                }
                // Debug.Log($"Squad {squad.ValueRO.SquadId} is dazed from opponent running away, skipping target finding");

                if (queuedOrders.Length > 0)
                {
                    queuedOrders.Clear();
                }

                continue;
            }

            int newTargetSquadId = 0;

            //if in guard mode and still in combat, thats fine it will target the closest
            if (SquadOverridesComponent.GuardMode)
            {
                newTargetSquadId = AttackASquadAttackingMe();

                if(newTargetSquadId == 0) continue;

                // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is targeting squad {newTargetSquadId} that is already attacking it");
            }
            else
            {
                newTargetSquadId = AttackASquadAttackingMe();

                if(newTargetSquadId == 0) 
                {
                    // If not in guard mode and has a squad attacking me, add a halt command to drop current target and re-evaluate
                    float closestDistance = float.MaxValue;
                    if (entityManager.IsComponentEnabled<WaitingForCommand>(squad.ValueRO.SelfEntity)) continue;

                    //check if monster
                    bool monster = entityManager.HasComponent<MonsterousSquadTag>(squad.ValueRO.SelfEntity);
                    bool priorityTargetFound = false;

                    if(monster) 
                    {
                        //if monster, prioritize non-anti-large squads
                        for (int i = 0; i < allSquadData.Length; i++)
                        {
                            var enemySquadData = allSquadData[i];
                            bool isEnemy = enemySquadData.SquadId < 0;
                            if (isEnemy != (squad.ValueRO.SquadId > 0)) continue;

                            if(IsBlacklisted(enemySquadData.SelfEntity)) continue;

                            if(entityManager.HasComponent<AntiLargeTag>(enemySquadData.SelfEntity)) continue;

                            float3 centerOfSquad = squadMovementComponent.ValueRO.SquadCenter;
                            float3 centerOfTarget = _squadMovementLookup[enemySquadData.SelfEntity].SquadCenter;

                            float distance = math.distance(centerOfSquad, centerOfTarget);
                            if(distance < closestDistance) {
                                closestDistance = distance;
                                newTargetSquadId = enemySquadData.SquadId;
                                priorityTargetFound = true;
                            }
                        }

                        if(priorityTargetFound)
                        {
                            // Debug.Log($"SquadTargettingSystem: large squad {squad.ValueRO.SquadId} is prioritizing non-anti-large squad {newTargetSquadId}");
                        }
                    }

                    if(!priorityTargetFound)
                    {
                        //if not large or no valid non-anti-large targets, target closest
                        for (int i = 0; i < allSquadData.Length; i++)
                        {
                            var enemySquadData = allSquadData[i];
                            bool isEnemy = enemySquadData.SquadId < 0;
                            if (isEnemy != (squad.ValueRO.SquadId > 0)) continue;

                            if(IsBlacklisted(enemySquadData.SelfEntity)) continue;

                            float3 centerOfSquad = squadMovementComponent.ValueRO.SquadCenter;
                            float3 centerOfTarget = _squadMovementLookup[enemySquadData.SelfEntity].SquadCenter;

                            float distance = math.distance(centerOfSquad, centerOfTarget);
                            if(distance < closestDistance) {
                                closestDistance = distance;
                                newTargetSquadId = enemySquadData.SquadId;
                            }
                        }
                    }

                    if(newTargetSquadId == 0) continue;

                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is targeting closest squad {newTargetSquadId}");
                }
                else
                {
                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is targeting squad {newTargetSquadId} that is already attacking it");
                }
            }

            //issue attack command here
            if (queuedOrders.Length > 0 && queuedOrders[0].Status == QueuedOrderStatus.InProgress) continue;

            QueuedOrder queuedOrder = new ()
            {
                Type = QueuedOrderType.Attack,
                TargetSquadId = newTargetSquadId,
            };

            queuedOrders.Clear();
            queuedOrders.Add(queuedOrder);
        }

        allSquadData.Dispose();
    }
}

