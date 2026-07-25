using Unity.Entities;
using Unity.Collections;

// RavenHost passive — "Deathcry"
// When a RavenHost squad falls (destroyed, broken, or withdrawn), all surviving RavenHost squads
// gain +20 Attack for 20 seconds. A second fall resets the timer but does not stack the bonus.
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(DestroySquadSystem))]
partial struct DeathcrySystem : ISystem
{
    public readonly void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        var config = RaceBonusRuleData.Deathcry;

        bool playerSideFell = false;
        bool enemySideFell = false;
        var toTag = new NativeList<Entity>(Allocator.Temp);

        foreach (var (entityBuffer, squadEntity, entity) in SystemAPI.Query<
            DynamicBuffer<EntityReferenceBufferElement>,
            RefRO<SquadEntity>>()
            .WithAll<RavenHostRaceTag>()
            .WithNone<DeathcryTriggeredTag>()
            .WithEntityAccess())
        {
            if (entityBuffer.Length > 0) continue;
            toTag.Add(entity);
            if (squadEntity.ValueRO.Team == Team.Player) playerSideFell = true;
            else enemySideFell = true;
        }

        foreach (var (squadEntity, entity) in SystemAPI.Query<RefRO<SquadEntity>>()
            .WithAll<RavenHostRaceTag, BrokenSquadTag>()
            .WithNone<DeathcryTriggeredTag>()
            .WithEntityAccess())
        {
            toTag.Add(entity);
            if (squadEntity.ValueRO.Team == Team.Player) playerSideFell = true;
            else enemySideFell = true;
        }

        foreach (var (squadEntity, entity) in SystemAPI.Query<RefRO<SquadEntity>>()
            .WithAll<RavenHostRaceTag, WithdrawSquadTag>()
            .WithNone<DeathcryTriggeredTag>()
            .WithEntityAccess())
        {
            toTag.Add(entity);
            if (squadEntity.ValueRO.Team == Team.Player) playerSideFell = true;
            else enemySideFell = true;
        }

        foreach (Entity e in toTag)
            entityManager.AddComponent<DeathcryTriggeredTag>(e);
        toTag.Dispose();

        bool anyJustFell = playerSideFell || enemySideFell;
        if (anyJustFell)
        {
            foreach (var (entityBuffer, bonusBuffer, deathcry, squadEntity) in SystemAPI.Query<
                DynamicBuffer<EntityReferenceBufferElement>,
                DynamicBuffer<BattlefieldBonusBufferElement>,
                RefRW<DeathcryComponent>,
                RefRO<SquadEntity>>()
                .WithAll<RavenHostRaceTag>()
                .WithNone<DeathcryTriggeredTag>())
            {
                if (entityBuffer.Length == 0) continue;

                bool friendlyFell = squadEntity.ValueRO.Team == Team.Player ? playerSideFell : enemySideFell;
                if (!friendlyFell) continue;

                if (deathcry.ValueRO.AppliedBonus == 0)
                {
                    for (int i = 0; i < entityBuffer.Length; i++)
                    {
                        Entity unitEntity = entityBuffer[i].Entity;
                        if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                        MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                        attack.MeleeAttackValue += config.MeleeAttackBonus;
                        entityManager.SetComponentData(unitEntity, attack);
                    }
                    deathcry.ValueRW.AppliedBonus = config.MeleeAttackBonus;
                    SyncBonusBuffer(bonusBuffer, config.MeleeAttackBonus);
                }

                deathcry.ValueRW.TimeRemaining = config.DurationSeconds;
            }
        }

        foreach (var (entityBuffer, bonusBuffer, deathcry) in SystemAPI.Query<
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<DeathcryComponent>>()
            .WithAll<RavenHostRaceTag>())
        {
            if (deathcry.ValueRO.AppliedBonus <= 0 || entityBuffer.Length == 0) continue;

            deathcry.ValueRW.TimeRemaining -= deltaTime;
            if (deathcry.ValueRO.TimeRemaining > 0f) continue;

            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                attack.MeleeAttackValue -= deathcry.ValueRO.AppliedBonus;
                entityManager.SetComponentData(unitEntity, attack);
            }

            deathcry.ValueRW.AppliedBonus = 0;
            deathcry.ValueRW.TimeRemaining = 0f;
            SyncBonusBuffer(bonusBuffer, 0);
        }
    }

    private static void SyncBonusBuffer(DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer, int totalBonus)
    {
        for (int i = bonusBuffer.Length - 1; i >= 0; i--)
        {
            if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Deathcry)
                bonusBuffer.RemoveAt(i);
        }
        if (totalBonus > 0)
        {
            bonusBuffer.Add(new BattlefieldBonusBufferElement
            {
                Value = new BattlefieldBonus
                {
                    UnitStat = UnitStat.MeleeAttack,
                    BattlefieldBonusEnum = BattlefieldBonusEnum.Deathcry,
                    Value = totalBonus,
                    Applied = true,
                    Range = 999999f
                }
            });
        }
    }
}
