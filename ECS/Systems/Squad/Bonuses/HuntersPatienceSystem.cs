using Unity.Entities;
using Unity.Mathematics;

// TaelindorForest passive — "Hunter's Patience"
// While stationary: ranged squads gain +3 Accuracy per second (max +20),
// melee squads gain +2 WeaponStrength per second (max +12).
// Moving or charging resets the entire accumulated bonus immediately.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct HuntersPatienceSystem : ISystem
{
    private float _updateTimer;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = RaceBonusRuleData.HuntersPatience;

        _updateTimer += SystemAPI.Time.DeltaTime;
        if (_updateTimer < config.UpdateInterval) return;
        _updateTimer -= config.UpdateInterval;

        var entityManager = state.EntityManager;

        foreach (var (squad, orderBuffer, entityBuffer, bonusBuffer, patience, entity) in SystemAPI.Query<
            RefRO<SquadEntity>,
            DynamicBuffer<QueuedOrder>,
            DynamicBuffer<EntityReferenceBufferElement>,
            DynamicBuffer<BattlefieldBonusBufferElement>,
            RefRW<HuntersPatienceComponent>>()
            .WithAll<TaelindorForestRaceTag>()
            .WithEntityAccess())
        {
            bool isMoving = orderBuffer.Length > 0 && orderBuffer[0].Type == QueuedOrderType.Move;
            bool isCharging = entityManager.HasComponent<ChargeSquad>(entity);
            bool isStationary = !isMoving && !isCharging;

            int cap = patience.ValueRO.IsRanged ? config.RangedBonusCap : config.MeleeBonusCap;

            if (isStationary && patience.ValueRO.CurrentBonus < cap)
            {
                int bonusAmount = patience.ValueRO.IsRanged ? config.RangedBonusPerTick : config.MeleeBonusPerTick;
                int newBonus = math.min(patience.ValueRO.CurrentBonus + bonusAmount, cap);
                int toApply = newBonus - patience.ValueRO.CurrentBonus;

                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    Entity unitEntity = entityBuffer[i].Entity;
                    if (patience.ValueRO.IsRanged)
                    {
                        if (!entityManager.HasComponent<ShootAttack>(unitEntity)) continue;
                        ShootAttack shoot = entityManager.GetComponentData<ShootAttack>(unitEntity);
                        shoot.Accuracy += toApply;
                        entityManager.SetComponentData(unitEntity, shoot);
                    }
                    else
                    {
                        if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                        MeleeAttack melee = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                        melee.WeaponStrength += toApply;
                        entityManager.SetComponentData(unitEntity, melee);
                    }
                }

                patience.ValueRW.CurrentBonus = newBonus;
                SyncBonusBuffer(bonusBuffer, patience.ValueRO.IsRanged, newBonus);
            }
            else if (!isStationary && patience.ValueRO.CurrentBonus > 0)
            {
                int toRemove = patience.ValueRO.CurrentBonus;
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    Entity unitEntity = entityBuffer[i].Entity;
                    if (patience.ValueRO.IsRanged)
                    {
                        if (!entityManager.HasComponent<ShootAttack>(unitEntity)) continue;
                        ShootAttack shoot = entityManager.GetComponentData<ShootAttack>(unitEntity);
                        shoot.Accuracy -= toRemove;
                        entityManager.SetComponentData(unitEntity, shoot);
                    }
                    else
                    {
                        if (!entityManager.HasComponent<MeleeAttack>(unitEntity)) continue;
                        MeleeAttack melee = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                        melee.WeaponStrength -= toRemove;
                        entityManager.SetComponentData(unitEntity, melee);
                    }
                }

                patience.ValueRW.CurrentBonus = 0;
                SyncBonusBuffer(bonusBuffer, patience.ValueRO.IsRanged, 0);
            }
        }
    }

    private static void SyncBonusBuffer(DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer, bool isRanged, int totalBonus)
    {
        for (int i = bonusBuffer.Length - 1; i >= 0; i--)
        {
            if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.HuntersPatience)
                bonusBuffer.RemoveAt(i);
        }
        if (totalBonus > 0)
        {
            bonusBuffer.Add(new BattlefieldBonusBufferElement
            {
                Value = new BattlefieldBonus
                {
                    UnitStat = isRanged ? UnitStat.Accuracy : UnitStat.WeaponStrength,
                    BattlefieldBonusEnum = BattlefieldBonusEnum.HuntersPatience,
                    Value = totalBonus,
                    Applied = true,
                    Range = 999999f
                }
            });
        }
    }
}
