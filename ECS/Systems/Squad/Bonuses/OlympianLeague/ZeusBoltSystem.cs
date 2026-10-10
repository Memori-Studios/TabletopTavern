#if FACTIONUPDATE
using Unity.Collections;
using Unity.Entities;

// Zeus's Bolt: every landed charge (marked by SquadEngageInCombatSystem) calls lightning onto the charged squad.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct ZeusBoltSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var bursts = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (strike, squad, entity) in SystemAPI.Query<RefRO<ZeusBoltStrike>, RefRO<SquadEntity>>().WithEntityAccess())
        {
            OlympianStrikes.Burst(bursts, strike.ValueRO.Position, TabletopTavernConstants.ZEUS_BOLT_RADIUS,
                TabletopTavernConstants.ZEUS_BOLT_DAMAGE, 0f, squad.ValueRO.Team, squad.ValueRO.SquadId);
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = strike.ValueRO.Position, Kind = OlympianVisual.Lightning });
            ecb.RemoveComponent<ZeusBoltStrike>(entity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endif
