using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

// DrakosaurBrood passive — "Pack Instinct"
// For each other DrakosaurBrood squad attacking the same target, this squad gains +8 WeaponStrength.
// Max 2 stacks (+16). Checked every 0.5 seconds; adjusts as squads redirect targets.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct ApexHuntersSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();

    }

    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        var config = RaceBonusRuleData.ApexHunters;

        bool shouldUpdate = false;
        foreach (var pack in SystemAPI.Query<RefRW<ApexHuntersComponent>>()
            .WithAll<DrakosaurBroodRaceTag>())
        {
            pack.ValueRW.UpdateTimer -= deltaTime;
            if (pack.ValueRO.UpdateTimer <= 0f)
            {
                pack.ValueRW.UpdateTimer = config.UpdateInterval;
                shouldUpdate = true;
            }
            break;
        }

        if (!shouldUpdate) return;

        // Reset all other squads' timers to stay in sync
        foreach (var pack in SystemAPI.Query<RefRW<ApexHuntersComponent>>()
            .WithAll<DrakosaurBroodRaceTag>())
        {
            pack.ValueRW.UpdateTimer = config.UpdateInterval;
        }

        // Build a map: TargetSquadEntity index → how many Drakosaur squads are targeting it
        var targetCounts = new NativeHashMap<int, int>(16, Allocator.Temp);
        foreach (var squad in SystemAPI.Query<RefRO<SquadEntity>>()
            .WithAll<DrakosaurBroodRaceTag>())
        {
            Entity targetEntity = squad.ValueRO.TargetSquadEntity;
            if (targetEntity == Entity.Null) continue;
            int targetIndex = targetEntity.Index;
            targetCounts.TryGetValue(targetIndex, out int count);
            targetCounts[targetIndex] = count + 1;
        }

        // For each Drakosaur squad, compute desired stacks and apply delta
        foreach (var (squad, entityBuffer, bonusBuffer, pack) in SystemAPI.Query<
            RefRO<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<ApexHuntersComponent>>()
            .WithAll<DrakosaurBroodRaceTag>())
        {
            Entity targetEntity = squad.ValueRO.TargetSquadEntity;
            int totalOnTarget = 0;
            if (targetEntity != Entity.Null && targetCounts.TryGetValue(targetEntity.Index, out int cnt))
                totalOnTarget = cnt;

            int otherCount = math.max(0, totalOnTarget - 1);
            int desiredStacks = math.min(otherCount, config.MaxStacks);
            int delta = desiredStacks - pack.ValueRO.AppliedStacks;

            if (delta == 0) continue;

            int weaponDelta = delta * config.WeaponStrengthPerStack;
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                attack.WeaponStrength += weaponDelta;
                entityManager.SetComponentData(unitEntity, attack);
            }

            pack.ValueRW.AppliedStacks = desiredStacks;
            SyncBonusBuffer(bonusBuffer, desiredStacks * config.WeaponStrengthPerStack);
        }

        targetCounts.Dispose();
    }

    private static void SyncBonusBuffer(DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer, int totalBonus)
    {
        for (int i = bonusBuffer.Length - 1; i >= 0; i--)
        {
            if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.ApexHunters)
                bonusBuffer.RemoveAt(i);
        }
        if (totalBonus > 0)
        {
            bonusBuffer.Add(new BattlefieldBonusBufferElement
            {
                Value = new BattlefieldBonus
                {
                    UnitStat = UnitStat.WeaponStrength,
                    BattlefieldBonusEnum = BattlefieldBonusEnum.ApexHunters,
                    Value = totalBonus,
                    Applied = true,
                    Range = 999999f
                }
            });
        }
    }
}
