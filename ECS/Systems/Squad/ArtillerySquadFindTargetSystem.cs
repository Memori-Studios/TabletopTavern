using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

partial struct ArtillerySquadFindTargetSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        double now = SystemAPI.Time.ElapsedTime;

        //Squad Finding Target
        foreach (var (squad, squadMovementComponent, SquadOverridesComponent, queuedOrders, entityBuffer, blacklist, rangedSquad) in SystemAPI.Query<RefRW<
            SquadEntity>,
            RefRO<SquadMovementComponent>,
            SquadOverridesComponent,
            DynamicBuffer<QueuedOrder>,
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<TargetBlacklistElement>,
            RefRO<RangedSquad>>()
        .WithNone<
            ChargeSquad, 
            InCombat, 
            WithdrawSquadTag>()
        .WithNone<
            CavalryFlankingTag,
            SquadMoveOverrideTag,
            MeleeSquad>() 
        .WithNone<
            StartChargeTag,
            CeaseFireTag,
            CeaseFireRequestedTag
        >()
        .WithPresent<
            ArtillerySquad
        >()){
            
            // Dazed after abandoning a target (EnemyPursuitWatchdogSystem gives up on a kiter, or the
            // opponent broke off). Mirrors the melee finder: tick the daze down, and drop the queued
            // orders. The drop is load-bearing, not tidy-up - the abandoned Attack order is still
            // InProgress (QueuedOrderSystem only completes an Attack when its target dies) and the
            // guard at the bottom of this loop refuses a replacement while one is InProgress. Without
            // this, an enemy archer that gave up chasing cavalry never targeted anything again.
            // Only enemy squads are ever dazed, so this cannot discard a player's hand-queued orders.
            if (entityManager.HasComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity))
            {
                OpponentRanAwayTag opponentRanAwayTag = entityManager.GetComponentData<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                opponentRanAwayTag.DazedTime -= SystemAPI.Time.DeltaTime;
                entityManager.SetComponentData(squad.ValueRO.SelfEntity, opponentRanAwayTag);

                if (opponentRanAwayTag.DazedTime <= 0)
                {
                    entityCommandBuffer.RemoveComponent<OpponentRanAwayTag>(squad.ValueRO.SelfEntity);
                }

                if (queuedOrders.Length > 0)
                {
                    queuedOrders.Clear();
                }

                continue;
            }

            if(!SquadOverridesComponent.AutoTarget && !entityManager.Exists(squad.ValueRO.TargetSquadEntity))
            {
                continue;
            }

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

            #region if ranged squad target is set, make sure units have targets
            if(entityManager.Exists(squad.ValueRO.TargetSquadEntity)) {
                if (entityManager.HasComponent<BrokenSquadTag>(squad.ValueRO.TargetSquadEntity))
                {
                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} target is broken");
                    squad.ValueRW.TargetSquadEntity = Entity.Null;
                    continue;
                }
                
                //check if within range
                SquadMovementComponent targetSquad = entityManager.GetComponentData<SquadMovementComponent>(squad.ValueRO.TargetSquadEntity);
                float distance = math.distance(targetSquad.SquadCenter, squadMovementComponent.ValueRO.SquadCenter);
                // Debug.Log($"distance: {distance}");
                // Debug.Log($"distance: {distance}, attack range: {rangedSquad.AttackRange}");
                //little buffer of 10
                if(distance - 10 < rangedSquad.ValueRO.AttackRange)
                {
                    for (int i = 0; i < entityBuffer.Length; i++) {
                        Entity referencedEntity = entityBuffer[i].Entity;
                        Target target = entityManager.GetComponentData<Target>(referencedEntity);
                        // Debug.Log($"referencedEntity: {referencedEntity}, target: {target.targetEntity}");
                        if(target.targetEntity == Entity.Null && !entityManager.HasComponent<NeedsToBeProcessed>(referencedEntity)) {
                            entityCommandBuffer.AddComponent(referencedEntity, new NeedsToBeProcessed() {Delay = _random.NextFloat(0f, 0.5f) } );//_random.NextFloat(0f, 0.01f) 
                        }
                    }
                }
                else if(distance > rangedSquad.ValueRO.AttackRange) {
                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is out of range");
                    entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new HaltCommandTag { DropTarget = true });
                }
                
                continue;
            }
            #endregion

            //if in guard mode, thats fine it will target the closest

            #region get closest enemy squad
            float closestDistance = float.MaxValue;
            int closestEnemySquadEntity = 0;
            bool searchForEnemySquads = squad.ValueRO.SquadId > 0;

            foreach (RefRO<SquadEntity> enemySquad in SystemAPI.Query<RefRO<SquadEntity>>().WithNone<BrokenSquadTag>())
            {
                bool isEnemy = enemySquad.ValueRO.SquadId < 0;
                if (isEnemy != searchForEnemySquads) continue;

                if(IsBlacklisted(enemySquad.ValueRO.SelfEntity)) continue;

                if(!entityManager.HasComponent<SquadMovementComponent>(enemySquad.ValueRO.SelfEntity)) continue;

                float3 centerOfSquad = entityManager.GetComponentData<SquadMovementComponent>(squad.ValueRO.SelfEntity).SquadCenter;
                float3 centerOfTarget = entityManager.GetComponentData<SquadMovementComponent>(enemySquad.ValueRO.SelfEntity).SquadCenter;

                float distance = math.distance(centerOfSquad, centerOfTarget);

                if(distance < closestDistance) 
                {
                    closestDistance = distance;
                    closestEnemySquadEntity = enemySquad.ValueRO.SquadId;
                }
            }

            if(closestEnemySquadEntity == 0) continue;

            // Debug.Log($"closestEnemySquadEntity: {closestEnemySquadEntity}");
            #endregion

            if(closestDistance > rangedSquad.ValueRO.AttackRange)
            {
                if (SquadOverridesComponent.GuardMode) continue;
                if(entityManager.IsComponentEnabled<WaitingForCommand>(squad.ValueRO.SelfEntity)) continue;
            }
            else //if within range
            {
                if(squad.ValueRO.SquadCommand == SquadCommand.Move) continue;
            }

            //issue attack command here
            // Debug.Log($"Squad {squad.ValueRO.SquadId} is engaging squad {closestEnemySquadEntity}");
            if (queuedOrders.Length > 0 && queuedOrders[0].Status == QueuedOrderStatus.InProgress) continue;

            QueuedOrder queuedOrder = new ()
            {
                Type = QueuedOrderType.Attack,
                TargetSquadId = closestEnemySquadEntity,
            };

            queuedOrders.Clear();
            queuedOrders.Add(queuedOrder);
            Debug.Log($"Squad {squad.ValueRO.SquadId} is engaging squad {closestEnemySquadEntity}");
        }
    }
}

