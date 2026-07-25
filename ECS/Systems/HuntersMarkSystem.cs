using Unity.Burst;
using Unity.Entities;

// Hunter's Mark: mark one enemy squad, and every one of your units deals extra damage to it for a
// duration. Modelled on ArtilleryVsArtilleryDamageSystem - a pre-ApplyDamage modifier that scales
// AttackStrength on the DamageBufferElement before ApplyDamageSystem consumes and clears the buffer,
// so the boost is applied exactly once per hit and armour/type math still runs on top of it.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TJ.ApplyDamageSystem))]
partial struct HuntersMarkSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Match ApplyDamageSystem's guard: only run mid-battle, so a stale buffer can't be re-amplified
        // frame after frame while damage application is paused.
        state.RequireForUpdate<BattleHasStarted>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Tick down each marked squad; drop the mark when it expires.
        foreach (var (mark, entity) in SystemAPI.Query<RefRW<HuntersMarkTag>>().WithEntityAccess())
        {
            mark.ValueRW.RemainingDuration -= deltaTime;
            if (mark.ValueRO.RemainingDuration <= 0f)
                ecb.RemoveComponent<HuntersMarkTag>(entity);
        }

        // Amplify hostile damage queued against units whose parent squad is marked.
        foreach (var (damageBuffer, entityTeam, parentSquad) in SystemAPI.Query<
            DynamicBuffer<DamageBufferElement>,
            EntityTeam,
            UnitParentEntityTag>())
        {
            Entity squadEntity = parentSquad.parentSquadEntity;
            if (!SystemAPI.HasComponent<HuntersMarkTag>(squadEntity)) continue;

            float multiplier = SystemAPI.GetComponent<HuntersMarkTag>(squadEntity).DamageMultiplier;

            for (int i = 0; i < damageBuffer.Length; i++)
            {
                // ElementAt returns a ref into the buffer; the indexer setter is disallowed on a
                // foreach-iteration variable (CS1654), so this is the only way to write in place.
                ref DamageBufferElement element = ref damageBuffer.ElementAt(i);
                // Only the mark owner's hits count - skip any same-team entry (ApplyDamageSystem would
                // drop it as friendly fire anyway, but don't spend the multiplier on it).
                if (element.TeamOfSource == entityTeam.Value) continue;
                element.AttackStrength = (int)(element.AttackStrength * multiplier);
            }
        }
    }
}
