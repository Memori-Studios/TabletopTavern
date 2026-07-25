using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using TJ.Morale;

// Iron Legion passive — "Iron Resolve"
// When a squad's morale first hits Wavering, hold it there for 10 seconds.
// After the timer expires the IronResolveComponent is removed so morale falls normally.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MoraleSystem))]
[UpdateBefore(typeof(BrokenSquadTaggingSystem))]
partial struct IronResolveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        var toRemove = new NativeList<Entity>(Allocator.Temp);

        foreach (var (morale, resolve, entity) in SystemAPI.Query<
            RefRW<MoraleComponent>,
            RefRW<IronResolveComponent>>()
            .WithAll<IronLegionRaceTag>()
            .WithEntityAccess())
        {
            if (resolve.ValueRO.IsClamped)
            {
                resolve.ValueRW.ClampTimer -= deltaTime;

                // Pin morale to 1.99x threshold every frame so MoraleSystem can't push it into Broken
                float clampedMorale = morale.ValueRO.MaxMorale * 0.45f - 0.01f;
                morale.ValueRW.CurrentMorale = clampedMorale;
                morale.ValueRW.MoraleState = 1;

                if (resolve.ValueRO.ClampTimer <= 0f)
                {
                    toRemove.Add(entity);
                }
            }
            else if (morale.ValueRO.MoraleState == 1)
            {
                // Squad just hit Wavering — pin morale and start 10s timer
                float clampedMorale = morale.ValueRO.MaxMorale * 0.45f - 0.01f;
                morale.ValueRW.CurrentMorale = clampedMorale;
                morale.ValueRW.MoraleState = 1;
                resolve.ValueRW.IsClamped = true;
                resolve.ValueRW.ClampTimer = RaceBonusRuleData.IronResolve.ClampDurationSeconds;
            }
        }

        foreach (Entity e in toRemove)
            entityManager.RemoveComponent<IronResolveComponent>(e);

        toRemove.Dispose();
    }
}
