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

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _mageCastLookup = state.GetComponentLookup<MageCast>(false);
        _squadMovementLookup = state.GetComponentLookup<SquadMovementComponent>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<MageCastRequestBufferElement> requests))
            return;

        _mageCastLookup.Update(ref state);
        _squadMovementLookup.Update(ref state);

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
            if (mageCast.ValueRO.Timer > 0f) continue;

            // Casting stops in melee. A mage that is engaged swings its staff instead - MeleeAttack
            // is on the entity like every other unit, so that needs nothing from this system.
            if (entityManager.HasComponent<InCombat>(squad.ValueRO.SelfEntity)) continue;

            // Hold Spells. Reuses the existing Cease Fire stance rather than a mage-specific toggle.
            if (entityManager.HasComponent<CeaseFireTag>(squad.ValueRO.SelfEntity)
                && entityManager.IsComponentEnabled<CeaseFireTag>(squad.ValueRO.SelfEntity)) continue;

            Entity targetSquad = squad.ValueRO.TargetSquadEntity;
            if (!entityManager.Exists(targetSquad)) continue;
            if (entityManager.HasComponent<BrokenSquadTag>(targetSquad)) continue;
            if (!_squadMovementLookup.HasComponent(targetSquad)) continue;
            if (!_squadMovementLookup.HasComponent(squad.ValueRO.SelfEntity)) continue;

            float3 targetCenter = _squadMovementLookup[targetSquad].SquadCenter;
            float distance = math.distance(_squadMovementLookup[squad.ValueRO.SelfEntity].SquadCenter, targetCenter);
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
