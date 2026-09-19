using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

// Ticks each mage squad's cast cooldown and, when it fires, appends a request to the
// MageCastRequestBufferElement singleton for EntityWatcher to turn into a real ActiveSpell.
//
// The effect itself cannot be built here: a SpellData is a managed ScriptableObject and ActiveSpell
// is a MonoBehaviour prefab, neither reachable from an ISystem. Same split, and the same singleton
// buffer idiom, as BattlefieldBonusAppliedDetectionSystem.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct MageCastSystem : ISystem
{
    private ComponentLookup<MageCast> _mageCastLookup;
    private ComponentLookup<SquadMovementComponent> _squadMovementLookup;
    private ComponentLookup<MageManualCastOrder> _manualCastLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _mageCastLookup = state.GetComponentLookup<MageCast>(false);
        _squadMovementLookup = state.GetComponentLookup<SquadMovementComponent>(true);
        _manualCastLookup = state.GetComponentLookup<MageManualCastOrder>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<MageCastRequestBufferElement> requests))
            return;

        _mageCastLookup.Update(ref state);
        _squadMovementLookup.Update(ref state);
        _manualCastLookup.Update(ref state);

        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (squad, entityBuffer, mageSquad) in SystemAPI.Query<
            RefRO<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>,
            RefRO<MageSquad>>()
        .WithNone<BrokenSquadTag, WithdrawSquadTag>())
        {
            if (entityBuffer.Length == 0) continue;
            Entity unitEntity = entityBuffer[0].Entity;
            if (!_mageCastLookup.HasComponent(unitEntity)) continue;

            RefRW<MageCast> mageCast = _mageCastLookup.GetRefRW(unitEntity);

            // Tick unconditionally, and only reset on an actual cast. A blocked mage therefore holds
            // its timer at or below zero and casts the instant it is free, rather than being caught
            // mid-cycle - the same correction RangedUnitAttackSystem needed for archers, where a
            // resetting idle timer meant waiting out half a reload after acquiring a target.
            mageCast.ValueRW.Timer -= deltaTime;

            Entity self = squad.ValueRO.SelfEntity;
            bool manualCastPending = _manualCastLookup.HasComponent(self) && _manualCastLookup.IsComponentEnabled(self);
            if (manualCastPending)
            {
                // A manual target that died or broke voids the order whatever the timer says, so the
                // auto loop is not held off for a whole cooldown by a dead order. The charge is kept.
                Entity manualTarget = _manualCastLookup[self].TargetSquadEntity;
                if (manualTarget != Entity.Null
                    && (!entityManager.Exists(manualTarget)
                        || entityManager.HasComponent<BrokenSquadTag>(manualTarget)
                        || !_squadMovementLookup.HasComponent(manualTarget)))
                {
                    _manualCastLookup.SetComponentEnabled(self, false);
                    manualCastPending = false;
                }
            }

            if (mageCast.ValueRO.Timer > 0f) continue;

            // Casting stops in melee. A mage that is engaged swings its staff instead - MeleeAttack
            // is on the entity like every other unit, so that needs nothing from this system.
            if (entityManager.HasComponent<InCombat>(self)) continue;

            if (!_squadMovementLookup.HasComponent(self)) continue;
            float3 selfCenter = _squadMovementLookup[self].SquadCenter;

            #region Manual cast
            // A player-directed cast takes precedence over the auto loop and ignores Cease Fire:
            // holding spells is exactly the state a player casts out of by hand.
            if (manualCastPending)
            {
                MageManualCastOrder manual = _manualCastLookup[self];
                Entity manualTarget = manual.TargetSquadEntity;
                float3 castPoint = manualTarget != Entity.Null
                    ? _squadMovementLookup[manualTarget].SquadCenter
                    : manual.Position;

                // Out of range: the approach order issued with this cast is still walking the squad in.
                if (math.distance(selfCenter, castPoint) > mageSquad.ValueRO.AttackRange) continue;

                requests.Add(new MageCastRequestBufferElement
                {
                    SquadId = squad.ValueRO.SquadId,
                    UnitName = squad.ValueRO.UnitName,
                    TeamOfSource = squad.ValueRO.SquadId > 0 ? Team.Player : Team.Enemy,
                    Position = castPoint,
                    TargetSquadEntity = manualTarget,
                });
                mageCast.ValueRW.Timer = mageCast.ValueRO.Cooldown;
                entityCommandBuffer.AddComponent<AmmuntionSpent>(unitEntity);
                _manualCastLookup.SetComponentEnabled(self, false);
                continue;
            }
            #endregion

            // Hold Spells. Reuses the existing Cease Fire stance rather than a mage-specific toggle.
            if (entityManager.HasComponent<CeaseFireTag>(squad.ValueRO.SelfEntity)
                && entityManager.IsComponentEnabled<CeaseFireTag>(squad.ValueRO.SelfEntity)) continue;

            Entity targetSquad = squad.ValueRO.TargetSquadEntity;
            if (!entityManager.Exists(targetSquad)) continue;
            if (entityManager.HasComponent<BrokenSquadTag>(targetSquad)) continue;
            if (!_squadMovementLookup.HasComponent(targetSquad)) continue;

            float3 targetCenter = _squadMovementLookup[targetSquad].SquadCenter;
            float distance = math.distance(selfCenter, targetCenter);
            if (distance > mageSquad.ValueRO.AttackRange) continue;

            requests.Add(new MageCastRequestBufferElement
            {
                SquadId = squad.ValueRO.SquadId,
                UnitName = squad.ValueRO.UnitName,
                // SquadId sign is the team shortcut used throughout the codebase: positive is the
                // player, negative is the enemy. This is what reaches DamageBufferElement.TeamOfSource
                // and stops an enemy mage from damaging its own army.
                TeamOfSource = squad.ValueRO.SquadId > 0 ? Team.Player : Team.Enemy,
                Position = targetCenter,
                TargetSquadEntity = targetSquad,
            });

            mageCast.ValueRW.Timer = mageCast.ValueRO.Cooldown;

            // Spend the charge. RangedUnitAttackSystem tags the shooter this way and
            // RangedSquadRemoveAmmunitionSystem decrements the squad's SquadAmmunition off it - a
            // mage has no ShootAttack and so never reaches that system, which is why the tag is
            // raised here instead. Safe against double-spending: the cooldown was just reset above,
            // so the tag is always consumed long before the next cast.
            entityCommandBuffer.AddComponent<AmmuntionSpent>(unitEntity);
        }
    }
}
