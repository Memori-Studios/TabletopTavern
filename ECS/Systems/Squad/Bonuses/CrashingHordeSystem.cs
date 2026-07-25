using Unity.Entities;
using Unity.Collections;
using UnityEngine;

// Gruntkin passive — "Crashing Horde"
// Each living Gruntkin squad above 50% health gives +5 WeaponStrength to every other Gruntkin squad.
// Checked every 1 second; bonus adjusts dynamically as squads die or recover.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct CrashingHordeSystem : ISystem
{
    private float _updateTimer;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = RaceBonusRuleData.CrashingHorde;

        _updateTimer -= SystemAPI.Time.DeltaTime;
        if (_updateTimer > 0f) return;
        _updateTimer = config.UpdateInterval;

        var entityManager = state.EntityManager;

        int healthyCount = 0;
        foreach (var squadState in SystemAPI.Query<RefRO<SquadStateComponent>>().WithAll<GruntkinRaceTag>())
        {
            if (squadState.ValueRO.CurrentHealthValue > squadState.ValueRO.MaxHealthValue * config.HealthThreshold)
                healthyCount++;
        }

        foreach (var (squadState, entityBuffer, bonusBuffer, horde) in SystemAPI.Query<
            RefRO<SquadStateComponent>,
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<CrashingHordeComponent>>()
            .WithAll<GruntkinRaceTag>())
        {
            bool isSelfHealthy = squadState.ValueRO.CurrentHealthValue > squadState.ValueRO.MaxHealthValue * config.HealthThreshold;
            int othersHealthy = isSelfHealthy ? healthyCount - 1 : healthyCount;
            int desiredStacks = Mathf.Min(othersHealthy, config.MaxStacks);
            int delta = desiredStacks - horde.ValueRO.AppliedStacks;

            if (delta == 0) continue;

            int weaponStrengthDelta = delta * config.WeaponStrengthPerStack;
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                attack.WeaponStrength += weaponStrengthDelta;
                entityManager.SetComponentData(unitEntity, attack);
            }

            for (int i = bonusBuffer.Length - 1; i >= 0; i--)
            {
                if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.CrashingHorde)
                    bonusBuffer.RemoveAt(i);
            }
            if (desiredStacks > 0)
            {
                bonusBuffer.Add(new BattlefieldBonusBufferElement
                {
                    Value = new BattlefieldBonus
                    {
                        UnitStat = UnitStat.WeaponStrength,
                        BattlefieldBonusEnum = BattlefieldBonusEnum.CrashingHorde,
                        Value = desiredStacks * config.WeaponStrengthPerStack,
                        Applied = true,
                        Range = 999999f
                    }
                });
            }

            horde.ValueRW.AppliedStacks = desiredStacks;
        }
    }
}
