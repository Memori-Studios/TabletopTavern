using Unity.Burst;
using Unity.Entities;
using ProjectDawn.Navigation;

// Bjorn's Shieldwall: brace a friendly squad - its units become knockback-immune and move at half speed
// for a duration. Cast via SpellData.BracesTarget (ActiveSpell adds ShieldwallTag to the squad).
// Knockback immunity reuses the existing ResistKnockbackTag that SpellSystem / ExplosionSystem /
// LargeUnitPushSystem already honor; ShieldwallResistGrantedTag marks only the units WE made immune, so
// a unit that was immune from spawn never has that immunity stripped on expiry. Speed is scaled by a
// multiply/divide pair (like the Rain modifier) so it restores exactly regardless of the base value.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct ShieldwallSystem : ISystem
{
    const float SHIELDWALL_SPEED_MODIFIER = 0.5f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (shieldwall, unitBuffer, entity) in SystemAPI.Query<
            RefRW<ShieldwallTag>,
            DynamicBuffer<EntityReferenceBufferElement>>().WithEntityAccess())
        {
            if (!shieldwall.ValueRO.Applied)
            {
                // Brace: make each not-already-immune unit knockback-proof, and slow the whole squad.
                for (int i = 0; i < unitBuffer.Length; i++)
                {
                    Entity unit = unitBuffer[i].Entity;
                    if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<AgentLocomotion>(unit)) continue;

                    if (!SystemAPI.HasComponent<ResistKnockbackTag>(unit))
                    {
                        ecb.AddComponent<ResistKnockbackTag>(unit);
                        ecb.AddComponent<ShieldwallResistGrantedTag>(unit);
                    }

                    RefRW<AgentLocomotion> loc = SystemAPI.GetComponentRW<AgentLocomotion>(unit);
                    loc.ValueRW.Speed *= SHIELDWALL_SPEED_MODIFIER;
                    loc.ValueRW.Acceleration *= SHIELDWALL_SPEED_MODIFIER;
                }
                shieldwall.ValueRW.Applied = true;
                continue;
            }

            shieldwall.ValueRW.RemainingDuration -= deltaTime;
            if (shieldwall.ValueRO.RemainingDuration > 0f) continue;

            // Expired: undo the brace.
            for (int i = 0; i < unitBuffer.Length; i++)
            {
                Entity unit = unitBuffer[i].Entity;
                if (!SystemAPI.Exists(unit)) continue;

                if (SystemAPI.HasComponent<ShieldwallResistGrantedTag>(unit))
                {
                    ecb.RemoveComponent<ResistKnockbackTag>(unit);
                    ecb.RemoveComponent<ShieldwallResistGrantedTag>(unit);
                }

                if (SystemAPI.HasComponent<AgentLocomotion>(unit))
                {
                    RefRW<AgentLocomotion> loc = SystemAPI.GetComponentRW<AgentLocomotion>(unit);
                    loc.ValueRW.Speed /= SHIELDWALL_SPEED_MODIFIER;
                    loc.ValueRW.Acceleration /= SHIELDWALL_SPEED_MODIFIER;
                }
            }
            ecb.RemoveComponent<ShieldwallTag>(entity);
        }
    }
}
