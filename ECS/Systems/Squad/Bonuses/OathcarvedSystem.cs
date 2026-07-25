using Unity.Entities;
using Unity.Collections;

// DeepstoneHold passive — "Oathcarved"
// Every unit death within a DeepstoneHold squad permanently grants surviving units
// +2 WeaponStrength. Stacks indefinitely; never removed.
// Runs before ProcessUnitDeathSystem — UnitRemovedFromSquad is removed by that system,
// so we must read it first. The dying unit is still in the buffer; we skip it explicitly.
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(ProcessUnitDeathSystem))]
partial struct OathcarvedSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        int weaponStrengthPerDeath = RaceBonusRuleData.Oathcarved.WeaponStrengthPerDeath;

        // Build SquadId → Entity lookup for DeepstoneHold squads
        var dwarfSquads = new NativeHashMap<int, Entity>(16, Allocator.Temp);
        foreach (var (squad, entity) in SystemAPI.Query<RefRO<SquadEntity>>()
            .WithAll<DeepstoneHoldRaceTag>()
            .WithEntityAccess())
        {
            dwarfSquads[squad.ValueRO.SquadId] = entity;
        }

        if (dwarfSquads.Count == 0)
        {
            dwarfSquads.Dispose();
            return;
        }

        // For each unit death, check if it belongs to a DeepstoneHold squad
        foreach (var removedUnit in SystemAPI.Query<RefRO<UnitRemovedFromSquad>>())
        {
            int deadSquadId = removedUnit.ValueRO.SquadId;
            if (!dwarfSquads.TryGetValue(deadSquadId, out Entity squadEntity)) continue;

            // Buff every surviving unit in this squad (+1 WeaponStrength, +1 MeleeDefense)
            Entity dyingUnit = removedUnit.ValueRO.Entity;
            var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity unitEntity = entityBuffer[i].Entity;
                if (unitEntity == dyingUnit) continue;
                if (!entityManager.Exists(unitEntity)) continue;

                if (entityManager.HasComponent<MeleeAttack>(unitEntity))
                {
                    MeleeAttack attack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                    attack.WeaponStrength += weaponStrengthPerDeath;
                    entityManager.SetComponentData(unitEntity, attack);
                }
            }

            // Track death count and sync UI buffer
            OathcarvedComponent oathcarved = entityManager.GetComponentData<OathcarvedComponent>(squadEntity);
            oathcarved.DeathCount++;
            entityManager.SetComponentData(squadEntity, oathcarved);

            var bonusBuffer = entityManager.GetBuffer<BattlefieldBonusBufferElement>(squadEntity);
            SyncBonusBuffer(bonusBuffer, oathcarved.DeathCount * weaponStrengthPerDeath);
        }

        dwarfSquads.Dispose();
    }

    private static void SyncBonusBuffer(DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer, int deathCount)
    {
        for (int i = bonusBuffer.Length - 1; i >= 0; i--)
        {
            if (bonusBuffer[i].Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Oathcarved)
                bonusBuffer.RemoveAt(i);
        }
        if (deathCount > 0)
        {
            bonusBuffer.Add(new BattlefieldBonusBufferElement
            {
                Value = new BattlefieldBonus
                {
                    UnitStat = UnitStat.WeaponStrength,
                    BattlefieldBonusEnum = BattlefieldBonusEnum.Oathcarved,
                    Value = deathCount,
                    Applied = true,
                    Range = 999999f
                }
            });
        }
    }
}
