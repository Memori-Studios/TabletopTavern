using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using ProjectDawn.Navigation;
using TabletopTavern.GpuAnim;
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

    // Not [BurstCompile]: the flank charge morale shock reads the managed RaceBonusRuleData Sanguine Court toggle.
    // Only squads that made contact this frame are visited, so nearly every frame the loops are empty.
    public void OnUpdate(ref SystemState state)
    {
        bool sanguineImmune = RaceBonusRuleData.SanguineCourt.ImmuneToFlankMorale;
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
            // A charge that lands on a skirmishing archer squad strips its move tag a frame before this runs,
            // so the Retreat command would outlive the retreat and the squad would idle after the fight.
            else if (squad.ValueRO.SquadCommand == SquadCommand.Retreat)
            {
                squad.ValueRW.SquadCommand = SquadCommand.None;
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
            float chargeMultiplier = 1f;

            SquadStats squadStats = statsBlob.GetStats(squad.ValueRO.UnitName);
            float3 toTarget = targetSquadMovement.SquadCenter - SquadMovementComponent.ValueRO.SquadCenter;
            toTarget.y = 0f;
            float3 chargeDirection = math.normalizesafe(toTarget, math.forward(SquadMovementComponent.ValueRO.SquadRotation));
            OnFormationsCollide collide = new OnFormationsCollide
            {
                Position = (SquadMovementComponent.ValueRO.SquadCenter + targetSquadMovement.SquadCenter) / 2f,
                Kind = ChargeImpactKind.Contact,
                Size = squadStats.unitSize,
                Count = entityBuffer.Length,
                Right = math.cross(math.up(), chargeDirection),
                HalfWidth = SquadMovementComponent.ValueRO.SquadWidthAndDepth.x * TabletopTavernConstants.GetSpread(squadStats.unitSize) * 0.5f,
                SquadId = squad.ValueRO.SquadId,
            };

            if (isCharging)
            {
                // 1 = the charger is dead ahead of the target, -1 = dead behind it.
                float3 toCharger = SquadMovementComponent.ValueRO.SquadCenter - targetSquadMovement.SquadCenter;
                toCharger.y = 0f;
                float facingDot = math.dot(math.forward(targetSquadMovement.SquadRotation), math.normalizesafe(toCharger));
                float flankDot = squad.ValueRO.SquadId > 0
                    ? TabletopTavernConstants.CHARGE_FLANK_DOT_PLAYER
                    : TabletopTavernConstants.CHARGE_FLANK_DOT_ENEMY;
                bool flankCharge = facingDot < flankDot;
                bool blocked = !flankCharge && entityManager.HasComponent<AntiLargeTag>(squad.ValueRO.TargetSquadEntity);

                float rallyBonus = 0f;
                if (entityManager.HasComponent<EmpoweredChargeTag>(squad.ValueRO.SelfEntity))
                {
                    rallyBonus = entityManager.GetComponentData<EmpoweredChargeTag>(squad.ValueRO.SelfEntity).BonusImpact;
                    entityCommandBuffer.RemoveComponent<EmpoweredChargeTag>(squad.ValueRO.SelfEntity);
                    // The squad flag plays the cue.
                    if (!blocked) entityCommandBuffer.AddComponent<EmpoweredChargeLandedTag>(squad.ValueRO.SelfEntity);
                }

                if (blocked)
                {
                    allowKnockback = false;
                }
                else
                {
                    chargeMultiplier = flankCharge ? TabletopTavernConstants.CHARGE_FLANK_MULT : 1f;
                    entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new ChargeBonus());
                    entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new ApplyChargeBonusTag { Multiplier = chargeMultiplier, FlatBonus = rallyBonus });
                    if (flankCharge) ShockMorale(entityManager, squad.ValueRO.TargetSquadEntity, sanguineImmune);
                }

                entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new WearyTag { Remaining = TabletopTavernConstants.CHARGE_WEARY_TIME });
                collide.Kind = blocked ? ChargeImpactKind.Blocked : flankCharge ? ChargeImpactKind.FlankCharge : ChargeImpactKind.Charge;
                entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, collide);
            }
            else
            {
                // The squad that was charged leaves the event to its charger; two squads walking into each other both raise it.
                Entity other = FormationEngagedInCombat.ValueRO.EngagementEntity;
                bool otherCharged = entityManager.HasComponent<FormationEngagedInCombat>(other)
                    && entityManager.GetComponentData<FormationEngagedInCombat>(other).WasCharging;
                if (!otherCharged) entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, collide);
            }


            if (!isCharging) allowKnockback = false;

            bool opponentIsBracing = entityManager.IsComponentEnabled<BracedTag>(squad.ValueRO.TargetSquadEntity);

            if (opponentIsBracing) allowKnockback = false;

            // A large squad that landed a clean charge on an unbraced infantry line drives into it.
            float penetrationDepth = allowKnockback && largerHittingSmaller
                ? TabletopTavernConstants.ChargePenetrationDepth(squadStats.unitSize) : 0f;
            if (isCharging)
            {
                entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new SprintFollowThrough
                {
                    Remaining = penetrationDepth > 0f
                        ? TabletopTavernConstants.CHARGE_PENETRATION_TIME
                        : TabletopTavernConstants.CHARGE_FOLLOW_THROUGH_TIME,
                });
            }

            // How far forward the squad's leading model is, so the front rank can be told from the rest.
            float frontEdge = float.MinValue;
            if (bothSmall && allowKnockback)
            {
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    if (!entityManager.HasComponent<LocalTransform>(entityBuffer[i].Entity)) continue;
                    float3 position = entityManager.GetComponentData<LocalTransform>(entityBuffer[i].Entity).Position;
                    frontEdge = math.max(frontEdge, math.dot(position - SquadMovementComponent.ValueRO.SquadCenter, chargeDirection));
                }
            }

            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity entity = entityBuffer[i].Entity;
                bool unitKnockback = allowKnockback;
                if (bothSmall)
                {
                    // Infantry into infantry shoves only from the front rank, and never without a charge or into a brace.
                    if (unitKnockback)
                    {
                        float3 position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                        bool frontRank = math.dot(position - SquadMovementComponent.ValueRO.SquadCenter, chargeDirection)
                            >= frontEdge - TabletopTavernConstants.CHARGE_FRONT_RANK_DEPTH;
                        unitKnockback = frontRank && _random.NextFloat(0f, 1f) < TabletopTavernConstants.CHARGE_FRONT_RANK_SHOVE_CHANCE;
                    }
                    knockbackForce = _random.NextFloat(1f, 3f);
                }
                else
                {
                    knockbackForce = _random.NextFloat(6f, 8f);
                }

                if (unitKnockback)
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
                        KnockbackInitialDamage = (int)(chargeMultiplier * (entityManager.HasComponent<SquadChargeImpactDamage>(squad.ValueRO.SelfEntity)
                            ? entityManager.GetComponentData<SquadChargeImpactDamage>(squad.ValueRO.SelfEntity).Value
                            : squadStats.ChargeImactDamage)),
                    });
                    // Debug.Log($"SquadEngageInCombatSystem: squad {squad.ValueRO.SquadId} is engaging in combat with a smaller squad {squad.ValueRO.TargetSquadEntity.Index} and applying knockback to unit {entity}");
                }

                if (isCharging)
                {
                    // The first blows land as the ranks arrive, not whenever each model's old timer runs out.
                    MeleeAttack meleeAttack = entityManager.GetComponentData<MeleeAttack>(entity);
                    meleeAttack.timer = _random.NextFloat(0f, TabletopTavernConstants.CHARGE_FIRST_STRIKE_WINDOW);
                    entityManager.SetComponentData(entity, meleeAttack);
                }

                if (penetrationDepth > 0f && entityManager.HasComponent<ChargePenetration>(entity))
                {
                    entityManager.SetComponentData(entity, new ChargePenetration
                    {
                        Direction = chargeDirection,
                        DistanceLeft = penetrationDepth,
                        TimeLeft = TabletopTavernConstants.CHARGE_PENETRATION_TIME,
                    });
                    entityManager.SetComponentEnabled<ChargePenetration>(entity, true);
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
                    GpuAnimControl controlComp = entityManager.GetComponentData<GpuAnimControl>(animationDataHolder.ValueRO.gpuEcsAnimatorEntity);
                    controlComp.Slot = animationDataHolder.ValueRO.currentIdleAnimationId;
                    entityManager.SetComponentData(animationDataHolder.ValueRO.gpuEcsAnimatorEntity, controlComp);
                }
            }
        }
    }

    // A side or rear charge knocks a flat amount of morale off at once; MoraleSystem re-reads the state next frame.
    static void ShockMorale(EntityManager entityManager, Entity target, bool sanguineImmune)
    {
        if (!entityManager.HasComponent<TJ.Morale.MoraleComponent>(target)) return;
        if (entityManager.HasComponent<BrokenSquadTag>(target) || entityManager.HasComponent<GarrisonGateSquadTag>(target)) return;
        if (sanguineImmune && entityManager.HasComponent<SanguineCourtRaceTag>(target)) return;

        TJ.Morale.MoraleComponent morale = entityManager.GetComponentData<TJ.Morale.MoraleComponent>(target);
        morale.CurrentMorale = math.max(0f, morale.CurrentMorale - TabletopTavernConstants.CHARGE_FLANK_MORALE_SHOCK);
        entityManager.SetComponentData(target, morale);
    }
}

