using Unity.Entities;
using TJ.Morale;
using Unity.Burst;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct DamageDealtListenerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer<SquadDamageBufferElement>(out var damageBuffer))
            return;

        // Use a temp map to sum damage per SquadId
        var damagePerSquad = new NativeHashMap<int, int>(16, Allocator.Temp);
        bool hasTotals = SystemAPI.TryGetSingletonBuffer<SquadDamageTotalElement>(out var totals);

        foreach (var element in damageBuffer)
        {
            if (!damagePerSquad.TryGetValue(element.SquadId, out var current))
                current = 0;

            damagePerSquad[element.SquadId] = current + element.DamageAmount;

            if (hasTotals && element.Credited > 0)
                AddToTotal(totals, element.SquadId, element.Credited);
        }

        // Now apply totals to SquadDamageComponent entities
        foreach (var (squadDamage, entity) in 
            SystemAPI.Query<RefRW<SquadDamageComponent>>()
                     .WithAll<SquadDamageComponent>()
                     .WithEntityAccess())
        {
            if (damagePerSquad.TryGetValue(squadDamage.ValueRO.SquadId, out var totalDamage))
            {
                squadDamage.ValueRW.DamageDealt += totalDamage;
                // Optional: cap, reset, or trigger morale here
            }
        }

        // Clear buffer for next frame
        damageBuffer.Clear();

        damagePerSquad.Dispose();
    }

    private static void AddToTotal(DynamicBuffer<SquadDamageTotalElement> totals, int squadId, int amount)
    {
        for (int i = 0; i < totals.Length; i++)
        {
            if (totals[i].SquadId != squadId) continue;
            totals.ElementAt(i).Total += amount;
            return;
        }
        totals.Add(new SquadDamageTotalElement { SquadId = squadId, Total = amount });
    }
}