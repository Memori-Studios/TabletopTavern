using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using TJ;

// Lifesteal never revives a model mid-battle: it tops up the most wounded living models and banks the rest.
[UpdateAfter(typeof(ApplyDamageSystem))]
partial struct BloodDrinkerSystem : ISystem
{
    private struct WoundedModel : IComparable<WoundedModel>
    {
        public Entity Entity;
        public int SquadId;
        public int Health;
        public int MaxHealth;
        public int CompareTo(WoundedModel other) => Health.CompareTo(other.Health);
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<BloodDrinkerHealElement>();
        state.RequireForUpdate<BloodBankElement>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BloodDrinkerHealElement> drained = SystemAPI.GetSingletonBuffer<BloodDrinkerHealElement>();
        if (drained.Length == 0) return;

        var owed = new NativeHashMap<int, float>(8, Allocator.Temp);
        foreach (BloodDrinkerHealElement element in drained)
        {
            owed.TryGetValue(element.SquadId, out float current);
            owed[element.SquadId] = current + element.Amount * TabletopTavernConstants.BLOOD_DRINKER_LIFESTEAL;
        }
        drained.Clear();

        var wounded = new NativeList<WoundedModel>(64, Allocator.Temp);
        foreach (var (health, maxHealth, unit, entity) in SystemAPI.Query<RefRO<Health>, RefRO<MaxHitPoints>, RefRO<Unit>>().WithEntityAccess())
        {
            if (!owed.ContainsKey(unit.ValueRO.squadId)) continue;
            int current = health.ValueRO.Value;
            if (current <= 0 || current >= maxHealth.ValueRO.Value) continue;
            wounded.Add(new WoundedModel { Entity = entity, SquadId = unit.ValueRO.squadId, Health = current, MaxHealth = maxHealth.ValueRO.Value });
        }
        wounded.Sort();

        foreach (WoundedModel model in wounded)
        {
            float left = owed[model.SquadId];
            int give = math.min((int)left, model.MaxHealth - model.Health);
            if (give <= 0) continue;
            RefRW<Health> health = SystemAPI.GetComponentRW<Health>(model.Entity);
            health.ValueRW.Value += give;
            health.ValueRW.onHealthChanged = true;
            owed[model.SquadId] = left - give;
        }

        DynamicBuffer<BloodBankElement> bank = SystemAPI.GetSingletonBuffer<BloodBankElement>();
        foreach (KVPair<int, float> pair in owed)
        {
            int spare = (int)pair.Value;
            if (spare <= 0) continue;
            AddToBank(bank, pair.Key, spare);
        }

        wounded.Dispose();
        owed.Dispose();
    }

    private static void AddToBank(DynamicBuffer<BloodBankElement> bank, int squadId, int amount)
    {
        for (int i = 0; i < bank.Length; i++)
        {
            if (bank[i].SquadId != squadId) continue;
            bank.ElementAt(i).Banked += amount;
            return;
        }
        bank.Add(new BloodBankElement { SquadId = squadId, Banked = amount });
    }
}
