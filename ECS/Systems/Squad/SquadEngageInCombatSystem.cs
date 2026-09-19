using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using ProjectDawn.Navigation;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using TJ;

[UpdateInGroup(typeof(LateSimulationSystemGroup), OrderFirst = true)]
partial struct SquadEngageInCombatSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<SquadStatsData>();
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        ref var statsBlob = ref statsData.StatsBlob.Value; 
        
        // When squads first engage in melee combat
        foreach (var (squad, SquadMovementComponent, entityBuffer, FormationEngagedInCombat) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRW<SquadMovementComponent>, 
            DynamicBuffer<EntityReferenceBufferElement>, 
            RefRO<FormationEngagedInCombat>>())
        {

            entityCommandBuffer.RemoveComponent<FormationEngagedInCombat>(squad.ValueRO.SelfEntity);

            if (entityManager.HasComponent<EmblazerTag>(squad.ValueRO.SelfEntity))
                entityCommandBuffer.AddComponent<EmblazingApplicatorTag>(FormationEngagedInCombat.ValueRO.EngagementEntity);

            if (entityManager.IsComponentEnabled<WaitingForCommand>(squad.ValueRO.SelfEntity))
                entityManager.SetComponentEnabled<WaitingForCommand>(squad.ValueRO.SelfEntity, false);

            if (entityManager.HasComponent<RangedSquadSkirmishTag>(squad.ValueRO.SelfEntity))
                entityCommandBuffer.RemoveComponent<RangedSquadSkirmishTag>(squad.ValueRO.SelfEntity);

            //might need to return early if already in combat?
            if (entityManager.HasComponent<InCombat>(squad.ValueRO.SelfEntity))
            {
                // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is already in combat, skipping engage");
                continue;
            }

            //if hitting a ranged squad
            if(entityManager.HasComponent<RangedSquad>(FormationEngagedInCombat.ValueRO.EngagementEntity))
            {
                // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is engaging a ranged squad");

                //create new queued order to switch to attack the squad targeting it in melee
                DynamicBuffer<QueuedOrder> queuedOrders = entityManager.GetBuffer<QueuedOrder>(FormationEngagedInCombat.ValueRO.EngagementEntity);

                //check the first order

                QueuedOrder currentOrder = queuedOrders.Length > 0 ? queuedOrders[0] : default;
                if (currentOrder.Type == QueuedOrderType.Attack || currentOrder.TargetSquadId != squad.ValueRO.SquadId)
                {
                    QueuedOrder newAttackOrder = new ()
                    {
                        Type = QueuedOrderType.Attack,
                        TargetSquadId = squad.ValueRO.SquadId,
                        Status = QueuedOrderStatus.InProgress
                    };
                    queuedOrders.Clear();
                    queuedOrders.Add(newAttackOrder);
                }
            }

            squad.ValueRW.TargetSquadEntity = FormationEngagedInCombat.ValueRO.EngagementEntity;

            SquadMovementComponent targetSquadMovement = entityManager.GetComponentData<SquadMovementComponent>(FormationEngagedInCombat.ValueRO.EngagementEntity);
            SquadMovementComponent.ValueRW.GoalPosition = targetSquadMovement.SquadCenter;

            if (entityManager.HasComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity))
            {
                entityCommandBuffer.RemoveComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity);
                squad.ValueRW.SquadCommand = SquadCommand.None;
                // Debug.Log($"[SquadEngageInCombatSystem] Squad {squad.ValueRO.SquadId} had SquadMoveOverrideTag while engaging — cleared it.");
            }
           
            entityCommandBuffer.AddComponent<InCombat>(squad.ValueRO.SelfEntity);
            // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is engaging in combat");

            // EntityWatcher (MonoBehaviour) runs after ECS systems. If a stale IssueSquadCommand
            // exists when InCombat is added, OrderSquadToAttack will see InCombat and immediately
            // trigger DisengageFromCombat, cancelling the engagement. Remove it here so the
            // LateSim ECB removal lands after any Sim-group dispatch, winning the race.
            if (entityManager.HasComponent<IssueSquadCommand>(squad.ValueRO.SelfEntity))
                entityCommandBuffer.RemoveComponent<IssueSquadCommand>(squad.ValueRO.SelfEntity);

            //get sizes of attacker and defender for charge damage
            bool attackerIsLarge = SystemAPI.HasComponent<LargeTag>(squad.ValueRO.SelfEntity);
            bool defenderIsLarge = SystemAPI.HasComponent<LargeTag>(squad.ValueRO.TargetSquadEntity);

            bool largerHittingSmaller = attackerIsLarge && !defenderIsLarge;
            bool smallerHittingLarger = !attackerIsLarge && defenderIsLarge;
            bool bothLarge = attackerIsLarge && defenderIsLarge;
            bool bothSmall = !attackerIsLarge && !defenderIsLarge;

            bool allowKnockback = !smallerHittingLarger && !bothLarge;
            float knockbackRange = largerHittingSmaller ? 4f : 2f;
            float knockbackForce = 0f;

            bool isCharging = FormationEngagedInCombat.ValueRO.WasCharging;

            //remove charge charge
            if (isCharging)
            {
                // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId}  Charging: {isCharging}");
                if (entityManager.HasComponent<SquadStateComponent>(squad.ValueRO.SelfEntity))
                {
                    SquadStateComponent squadState = entityManager.GetComponentData<SquadStateComponent>(squad.ValueRO.SelfEntity);
                    squadState.ChargesRemaining = math.max(0, squadState.ChargesRemaining - 1);
                    entityManager.SetComponentData(squad.ValueRO.SelfEntity, squadState);
                    if (squadState.ChargesRemaining == 0)
                    {
                        entityCommandBuffer.AddComponent<ExhaustedTag>(squad.ValueRO.SelfEntity);
                    }
                }
            }


            if (!isCharging) allowKnockback = false;

            bool opponentIsBracing = entityManager.IsComponentEnabled<BracedTag>(squad.ValueRO.TargetSquadEntity);

            if (opponentIsBracing) allowKnockback = false;

            SquadStats squadStats = statsBlob.GetStats(squad.ValueRO.UnitName);

            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity entity = entityBuffer[i].Entity;
                if (bothSmall)
                {
                    allowKnockback = _random.NextFloat(0f, 1f) < 0.15f;
                    knockbackForce = _random.NextFloat(1f, 3f);
                }
                else
                {
                    knockbackForce = _random.NextFloat(6f, 8f);
                }

                if (allowKnockback)
                {
                    ApplyKnockbackOnContact ApplyKnockbackOnContact = entityManager.GetComponentData<ApplyKnockbackOnContact>(entity);
                    ApplyKnockbackOnContact.LifeTime = 2;
                    entityManager.SetComponentData(entity, ApplyKnockbackOnContact);

                    entityCommandBuffer.SetComponentEnabled<ApplyKnockbackOnContact>(entity, true);
                    entityCommandBuffer.AddComponent(entity, new RequestExplosion
                    {
                        KnockbackSquadID = squad.ValueRO.SquadId,
                        KnockbackSquadTeam = squad.ValueRO.Team,
                        KnockbackRange = knockbackRange,
                        KnockbackForce = knockbackForce,
                        KnockbackInitialDamage = entityManager.HasComponent<SquadChargeImpactDamage>(squad.ValueRO.SelfEntity)
                            ? entityManager.GetComponentData<SquadChargeImpactDamage>(squad.ValueRO.SelfEntity).Value
                            : squadStats.ChargeImactDamage,
                    });
                    // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is engaging in combat with a smaller squad {squad.ValueRO.TargetSquadEntity.Index} and applying knockback to unit {entity}");
                }

                Unit unit = entityManager.GetComponentData<Unit>(entity);
                unit.unitState = UnitState.OnEngage;
                // Debug.Log($"SquadEngageInCombatSystem: setting unit {entity} state to OnEngage");
                entityManager.SetComponentData(entity, unit);
            }
        }

        // When ranged squads first engage in combat
        foreach (var (squad, SquadMovementComponent, entityBuffer, animationDataHolder) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRW<SquadMovementComponent>,
            DynamicBuffer<EntityReferenceBufferElement>,
            RefRW<AnimationDataHolder>>()
        .WithAll<FormationEngagedInRangedCombat>())
        {
            entityCommandBuffer.RemoveComponent<FormationEngagedInRangedCombat>(squad.ValueRO.SelfEntity);
            SquadMovementComponent.ValueRW.GoalPosition = SquadMovementComponent.ValueRO.SquadCenter;

            squad.ValueRW.TargetSquadEntity = Entity.Null;

            if(!entityManager.HasComponent<InCombat>(squad.ValueRO.SelfEntity))
            {
                // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is engaging in ranged combat");

                //switch animations for attack and attack idle to ranged attack and ranged attack idle
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    animationDataHolder.ValueRW.currentIdleAnimationId = animationDataHolder.ValueRO.attackIdleAnimationId;
                    GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(animationDataHolder.ValueRO.gpuEcsAnimatorEntity);
                    controlComp.animatorInfo.animationID = animationDataHolder.ValueRO.currentIdleAnimationId;
                    entityManager.SetComponentData(animationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);
                }
            }
        }
    }
}

