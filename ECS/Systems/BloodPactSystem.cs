using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

// Blood Pact: the player's squads deal and take more damage. A pre-ApplyDamage modifier like HuntersMarkSystem,
// ordered after the other two so each hit is scaled once, on top of the spell and mark scaling.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TJ.ApplyDamageSystem))]
[UpdateAfter(typeof(SpellSystem))]
[UpdateAfter(typeof(HuntersMarkSystem))]
partial struct BloodPactSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<CampaignSaveDataHolder>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CampaignSaveDataHolder holder = SystemAPI.GetSingleton<CampaignSaveDataHolder>();
        if (holder.IsCustomBattle || !OrdealMask.Has(holder.OrdealMask, OrdealId.BloodPact)) return;

        foreach (var (damageBuffer, entityTeam) in SystemAPI.Query<DynamicBuffer<DamageBufferElement>, EntityTeam>())
        {
            for (int i = 0; i < damageBuffer.Length; i++)
            {
                ref DamageBufferElement element = ref damageBuffer.ElementAt(i);
                if (element.DamageType == DamageType.Healing) continue;

                // Dealt: the player's squads only; hero spells carry squad id 0 and are left out.
                bool dealt = entityTeam.Value == Team.Enemy && element.TeamOfSource == Team.Player && element.DamageSourceSquadId > 0;
                // Taken: every enemy source, enemy mages included.
                bool taken = entityTeam.Value == Team.Player && element.TeamOfSource == Team.Enemy;
                if (!dealt && !taken) continue;

                // Rounded, so a small hit still gains its 10%.
                element.AttackStrength = (int)math.round(element.AttackStrength * OrdealMask.BLOOD_PACT_DAMAGE);
            }
        }
    }
}
