#if FACTIONUPDATE
using Unity.Entities;
using Unity.Mathematics;
using TJ.Morale;

// Olympian League passive "Answered Prayers": each time a squad turns Wavering, while strikes are left,
// lightning hits every enemy model around it.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MoraleSystem))]
[UpdateBefore(typeof(BrokenSquadTaggingSystem))]
partial struct AnsweredPrayersSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = RaceBonusRuleData.AnsweredPrayers;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (morale, prayers, squad, movement) in SystemAPI.Query<
            RefRO<MoraleComponent>,
            RefRW<AnsweredPrayersComponent>,
            RefRO<SquadEntity>,
            RefRO<SquadMovementComponent>>()
            .WithAll<OlympianLeagueRaceTag>())
        {
            bool wavering = morale.ValueRO.MoraleState == 1;
            if (wavering && prayers.ValueRO.WasWavering == 0 && prayers.ValueRO.StrikesLeft > 0)
            {
                prayers.ValueRW.StrikesLeft--;
                float3 center = movement.ValueRO.SquadCenter;
                OlympianStrikes.Burst(ecb, center, config.StrikeRadius, config.StrikeDamage, 0f, squad.ValueRO.Team, squad.ValueRO.SquadId);
                if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = center, Kind = OlympianVisual.Lightning });
            }
            prayers.ValueRW.WasWavering = (byte)(wavering ? 1 : 0);
        }
    }
}
#endif
