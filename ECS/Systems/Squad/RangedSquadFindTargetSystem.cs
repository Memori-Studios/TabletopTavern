using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

partial struct RangedSquadFindTargetSystem : ISystem
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
            .WithNone<DestroyEntityTag>()
            .Build(ref state);
        _squadMovementLookup = state.GetComponentLookup<SquadMovementComponent>(true);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        _squadMovementLookup.Update(ref state);

        // Collect all non-broken squad data once per frame instead of querying inside the loop
        var allSquadData = _nonBrokenSquadQuery.ToComponentDataArray<SquadEntity>(Allocator.Temp);

        double now = SystemAPI.Time.ElapsedTime;

        //Squad Finding Target
        foreach (var (squad, squadMovementComponent, SquadOverridesComponent, entityBuffer, queuedOrders, blacklist, rangedSquad) in SystemAPI.Query<RefRW<
            SquadEntity>,
            RefRO<SquadMovementComponent>,
            SquadOverridesComponent,
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<QueuedOrder>,
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
            CeaseFireRequestedTag>()
        .WithNone<
            ArtillerySquad,
            StartChargeTag,
            CeaseFireTag
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
            if( entityManager.Exists(squad.ValueRO.TargetSquadEntity) && 
                !entityManager.HasComponent<BrokenSquadTag>(squad.ValueRO.TargetSquadEntity
            )) {
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
                        if(target.targetEntity == Entity.Null && !entityManager.HasComponent<NeedsToBeProcessed>(referencedEntity)) {
                            entityCommandBuffer.AddComponent(referencedEntity, new NeedsToBeProcessed() {Delay = _random.NextFloat(0f, 0.5f) } );//_random.NextFloat(0f, 0.01f)
                        }
                    }
                }
                else if(distance > rangedSquad.ValueRO.AttackRange) {
                    // Debug.Log($"SquadTargettingSystem: squad {squad.ValueRO.SquadId} is out of range");
                    queuedOrders.Clear();
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

            for (int i = 0; i < allSquadData.Length; i++)
            {
                var enemySquadData = allSquadData[i];

                if(searchForEnemySquads) {
                    if(enemySquadData.SquadId > 0) continue;
                } else {
                    if(enemySquadData.SquadId < 0) continue;
                }

                if(IsBlacklisted(enemySquadData.SelfEntity)) continue;

                float3 centerOfTarget = _squadMovementLookup[enemySquadData.SelfEntity].SquadCenter;
                float distance = math.distance(squadMovementComponent.ValueRO.SquadCenter, centerOfTarget);
                if(distance < closestDistance) {
                    closestDistance = distance;
                    closestEnemySquadEntity = enemySquadData.SquadId ;
                }
            }

            if(closestEnemySquadEntity == 0) continue;

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

            if (queuedOrders.Length > 0 && queuedOrders[0].Status == QueuedOrderStatus.InProgress) continue;

            //issue attack command here
            // Debug.Log($"Squad {squad.ValueRO.SquadId} is engaging squad {closestEnemySquadEntity}");
            QueuedOrder queuedOrder = new ()
            {
                Type = QueuedOrderType.Attack,
                TargetSquadId = closestEnemySquadEntity,
            };

            queuedOrders.Clear();
            queuedOrders.Add(queuedOrder);
        }
        allSquadData.Dispose();
    }
}

