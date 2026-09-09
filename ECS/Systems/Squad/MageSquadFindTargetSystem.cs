using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

// Sibling of ArtillerySquadFindTargetSystem. Mage squads get their own find-target system for the
// same reason artillery does: RangedSquadFindTargetSystem excludes ArtillerySquad explicitly, and a
// caster wants a different selection rule again.
//
// Like artillery, this issues a QueuedOrder rather than writing TargetSquadEntity directly, so the
// mage rides the existing order pipeline and the player's right-click attack order overrides it for
// free. CeaseFireTag is excluded, which is what makes the existing Cease Fire button work as
// "hold spells" with no new component.
partial struct MageSquadFindTargetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        foreach (var (squad, squadMovementComponent, squadOverrides, queuedOrders, mageSquad) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRO<SquadMovementComponent>,
            SquadOverridesComponent,
            DynamicBuffer<QueuedOrder>,
            RefRO<MageSquad>>()
        .WithNone<
            ChargeSquad,
            InCombat,
            WithdrawSquadTag>()
        .WithNone<
            CavalryFlankingTag,
            SquadMoveOverrideTag,
            BrokenSquadTag>()
        .WithNone<
            StartChargeTag,
            CeaseFireTag,
            CeaseFireRequestedTag
        >())
        {
            if (!squadOverrides.AutoTarget && !entityManager.Exists(squad.ValueRO.TargetSquadEntity))
                continue;

            // An existing target is kept until it dies, breaks, or walks out of casting range. Unlike
            // archers there is nothing to re-acquire per shot - the cast system reads
            // TargetSquadEntity when its timer fires.
            if (entityManager.Exists(squad.ValueRO.TargetSquadEntity))
            {
                if (entityManager.HasComponent<BrokenSquadTag>(squad.ValueRO.TargetSquadEntity))
                {
                    squad.ValueRW.TargetSquadEntity = Entity.Null;
                    continue;
                }

                // Out-of-range drop. Reaching casting range ends the charge (SquadHaltCommandSystem
                // strips ChargeSquad on the halt), so a mage whose target then walks away used to sit
                // there holding a target it could never cast at, for the rest of the battle.
                //
                // Dropping the target here lets the search below re-acquire on this same tick, which
                // also fixes re-approach for free: if that squad is still the best candidate it is
                // simply chosen again, and the fresh Attack order re-charges the mage toward it.
                //
                // Two things make this safe rather than thrashy. This query excludes ChargeSquad, so
                // it can only fire while the mage is stationary, never mid-approach. And the stale
                // order has to be cleared as well - it is left InProgress by the completed charge, and
                // the guard further down would otherwise refuse to issue the replacement, leaving the
                // mage with no target at all.
                //
                // AutoTarget gates it: a player who right-clicked a specific squad gets to keep that
                // order, and clearing the buffer would throw away orders they queued by hand.
                bool targetOutOfRange = false;
                if (squadOverrides.AutoTarget)
                {
                    float3 targetCenter = entityManager
                        .GetComponentData<SquadMovementComponent>(squad.ValueRO.TargetSquadEntity).SquadCenter;
                    float targetDistance = math.distance(squadMovementComponent.ValueRO.SquadCenter, targetCenter);
                    targetOutOfRange = targetDistance >
                        mageSquad.ValueRO.AttackRange * TabletopTavernConstants.MAGE_TARGET_DROP_RANGE_MULTIPLIER;
                }

                if (!targetOutOfRange) continue;

                squad.ValueRW.TargetSquadEntity = Entity.Null;
                queuedOrders.Clear();
            }
            else if (squadOverrides.AutoTarget && queuedOrders.Length > 0)
            {
                // The target is gone - killed, or broken and dropped from the non-broken set. The
                // Attack order that produced it is still InProgress, and the guard at the bottom
                // would refuse to issue a replacement, so the mage would hold full charges for the
                // rest of the battle. Clear it for the same reason the out-of-range path does.
                queuedOrders.Clear();
            }

            float3 selfCenter = squadMovementComponent.ValueRO.SquadCenter;
            bool selfIsPlayer = squad.ValueRO.SquadId > 0;
            MageTargetPriority priority = mageSquad.ValueRO.TargetPriority;

            // A buff caster scans its OWN side; everything else scans the opposition. Both cases
            // reduce to one flag because SquadId sign is the team, so a friendly search is just
            // the opposite sign to the one an offensive mage would use.
            bool wantFriendly = priority == MageTargetPriority.FriendlyNearestEnemy;
            bool searchForEnemySquads = wantFriendly ? !selfIsPlayer : selfIsPlayer;

            // FriendlyNearestEnemy means "the ally doing the fighting", so it needs a point that
            // stands in for the enemy line to measure allies against. The hostile squad nearest
            // the mage is a cheap and honest proxy: the mage sits behind its own line, so
            // whatever is closest to it is the part of the enemy its army is actually engaging.
            float3 enemyReference = selfCenter;
            bool hasEnemyReference = false;
            if (wantFriendly)
            {
                float nearestHostileDistance = float.MaxValue;
                foreach (var (hostileSquad, hostileMovement) in SystemAPI
                    .Query<RefRO<SquadEntity>, RefRO<SquadMovementComponent>>()
                    .WithNone<BrokenSquadTag>())
                {
                    bool hostileIsEnemy = hostileSquad.ValueRO.SquadId < 0;
                    if (hostileIsEnemy != selfIsPlayer) continue;

                    float hostileDistance = math.distance(selfCenter, hostileMovement.ValueRO.SquadCenter);
                    if (hostileDistance >= nearestHostileDistance) continue;
                    nearestHostileDistance = hostileDistance;
                    enemyReference = hostileMovement.ValueRO.SquadCenter;
                    hasEnemyReference = true;
                }
            }

            // Two candidates tracked at once. The priority rule only applies among squads actually in
            // range; if nothing is in range we still take the nearest, because that is what makes the
            // squad advance with the army rather than standing idle forever (same as artillery).
            int bestInRangeSquadId = 0;
            float bestInRangeScore = float.MinValue;
            int nearestSquadId = 0;
            float nearestDistance = float.MaxValue;

            foreach (var (enemySquad, enemyMovement) in SystemAPI
                .Query<RefRO<SquadEntity>, RefRO<SquadMovementComponent>>()
                .WithNone<BrokenSquadTag>())
            {
                bool isEnemy = enemySquad.ValueRO.SquadId < 0;
                if (isEnemy != searchForEnemySquads) continue;

                // Load-bearing only for a friendly search, where the mage is otherwise a valid
                // candidate at distance zero and would always pick itself.
                if (enemySquad.ValueRO.SelfEntity == squad.ValueRO.SelfEntity) continue;

                // Nor another caster. A mage is a one-model support unit standing behind the line,
                // so it is never "the ally doing the fighting" this priority is meant to find -
                // two mages would otherwise happily buff each other. MageSquad is the right test
                // rather than UnitType: a spent mage has traded it for MeleeSquad and by then is a
                // legitimate front-line body to buff.
                if (wantFriendly && entityManager.HasComponent<MageSquad>(enemySquad.ValueRO.SelfEntity)) continue;

                float distance = math.distance(selfCenter, enemyMovement.ValueRO.SquadCenter);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestSquadId = enemySquad.ValueRO.SquadId;
                }

                if (distance > mageSquad.ValueRO.AttackRange) continue;

                float score = ScoreCandidate(
                    ref entityManager,
                    priority,
                    enemySquad.ValueRO.SelfEntity,
                    distance,
                    enemyMovement.ValueRO.SquadCenter,
                    enemyReference,
                    hasEnemyReference);

                if (score > bestInRangeScore)
                {
                    bestInRangeScore = score;
                    bestInRangeSquadId = enemySquad.ValueRO.SquadId;
                }
            }

            int chosenSquadId = bestInRangeSquadId != 0 ? bestInRangeSquadId : nearestSquadId;
            if (chosenSquadId == 0) continue;

            if (bestInRangeSquadId == 0)
            {
                // Nothing in range. Hold position if the player asked for that, otherwise advance.
                if (squadOverrides.GuardMode) continue;
                if (entityManager.IsComponentEnabled<WaitingForCommand>(squad.ValueRO.SelfEntity)) continue;
            }
            else if (squad.ValueRO.SquadCommand == SquadCommand.Move)
            {
                // Already in range and under a move order - do not overwrite the player's order.
                continue;
            }

            if (queuedOrders.Length > 0 && queuedOrders[0].Status == QueuedOrderStatus.InProgress) continue;

            queuedOrders.Clear();
            queuedOrders.Add(new QueuedOrder
            {
                Type = QueuedOrderType.Attack,
                TargetSquadId = chosenSquadId,
            });
        }
    }

    // Higher is better. Distance is folded in as a small negative term so that ties break toward the
    // closer squad and every rule stays a single comparable float.
    private static float ScoreCandidate(ref EntityManager entityManager, MageTargetPriority priority,
        Entity candidate, float distance, float3 candidateCenter, float3 enemyReference,
        bool hasEnemyReference)
    {
        switch (priority)
        {
            case MageTargetPriority.DensestEnemyCluster:
                // Model count, so a Smite lands on the block worth spending a charge on rather than
                // on whichever 4-model skirmisher happened to wander closest.
                int modelCount = 0;
                if (entityManager.HasBuffer<EntityReferenceBufferElement>(candidate))
                    modelCount = entityManager.GetBuffer<EntityReferenceBufferElement>(candidate).Length;
                return modelCount - distance * 0.01f;

            case MageTargetPriority.FriendlyNearestEnemy:
                // Buff whichever ally is closest to the fighting. With no live opponent there is
                // no front line to measure against, so fall back to the nearest ally rather than
                // picking arbitrarily - which is also the sane answer once a battle is won.
                if (!hasEnemyReference) return -distance;
                return -math.distance(candidateCenter, enemyReference);

            // ChargingEnemy is still declared but unimplemented. Primal Quake was the design it
            // was written for and deliberately ships on NearestEnemy instead - a charge-hunter
            // idles whenever nothing is charging, which a unit with three casts cannot afford.
            // Kept as a tuning upgrade; no shipped SpellData selects it.
            case MageTargetPriority.NearestEnemy:
            default:
                return -distance;
        }
    }
}
