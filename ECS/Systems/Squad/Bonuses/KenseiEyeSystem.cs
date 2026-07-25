using Unity.Entities;

// SakuraDynasty passive — "Kensei's Eye"
// While engaged in continuous melee combat:
//   10s → +5 MeleeAttack, 20s → +5 more, 30s → +5 more (max +15 total)
// If the squad disengages (InCombat removed) the bonus and timer both reset.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct KenseiEyeSystem : ISystem
{
    private float _updateTimer;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = RaceBonusRuleData.KenseiEye;

        _updateTimer += SystemAPI.Time.DeltaTime;
        if (_updateTimer < config.UpdateInterval) return;
        _updateTimer -= config.UpdateInterval;

        var entityManager = state.EntityManager;

        // Active path — squad is in combat, accumulate time and apply stage bonuses
        foreach (var (entityBuffer, bonusBuffer, honor) in SystemAPI.Query<
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<KenseiEyeComponent>>()
            .WithAll<SakuraDynastyRaceTag, InCombat>())
        {
            honor.ValueRW.CombatTime += config.UpdateInterval;
            int newStage = (int)(honor.ValueRO.CombatTime / config.SecondsPerStage);
            if (newStage > config.MaxStages) newStage = config.MaxStages;

            int stagesToApply = newStage - honor.ValueRO.CurrentStage;

            if (stagesToApply <= 0) continue;

            int bonusToApply = stagesToApply * config.MeleeAttackPerStage;
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                attack.MeleeAttackValue += bonusToApply;
                entityManager.SetComponentData(unitEntity, attack);
            }

            honor.ValueRW.CurrentStage = newStage;
            SyncBonusBuffer(bonusBuffer, newStage * config.MeleeAttackPerStage);
        }

        // Reset path — squad left combat with an active bonus
        foreach (var (entityBuffer, bonusBuffer, honor) in SystemAPI.Query<
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<KenseiEyeComponent>>()
            .WithAll<SakuraDynastyRaceTag>()
            .WithNone<InCombat>())
        {
            if (honor.ValueRO.CurrentStage == 0) continue;

            int bonusToRemove = honor.ValueRO.CurrentStage * config.MeleeAttackPerStage;
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                attack.MeleeAttackValue -= bonusToRemove;
                entityManager.SetComponentData(unitEntity, attack);
            }

            honor.ValueRW.CombatTime = 0f;
            honor.ValueRW.CurrentStage = 0;
            SyncBonusBuffer(bonusBuffer, 0);
        }
    }

    private static void SyncBonusBuffer(DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer, int totalBonus)
    {
        for (int i = bonusBuffer.Length - 1; i >= 0; i--)
        {
            if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.KenseiEye)
                bonusBuffer.RemoveAt(i);
        }
        if (totalBonus > 0)
        {
            bonusBuffer.Add(new BattlefieldBonusBufferElement
            {
                Value = new BattlefieldBonus
                {
                    UnitStat = UnitStat.MeleeAttack,
                    BattlefieldBonusEnum = BattlefieldBonusEnum.KenseiEye,
                    Value = totalBonus,
                    Applied = true,
                    Range = 999999f
                }
            });
        }
    }
}
