#if FACTIONUPDATE
using Unity.Entities;

// Poseidon's Tremor: when the squad falls below POSEIDON_TREMOR_MODEL_SHARE of the models it fielded, the
// enemies around it are thrown back, once.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct PoseidonsTremorSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (tremor, squad, movement, units) in SystemAPI.Query<
            RefRW<PoseidonsTremorBlessing>, RefRO<SquadEntity>, RefRO<SquadMovementComponent>, DynamicBuffer<EntityReferenceBufferElement>>())
        {
            if (tremor.ValueRO.Spent == 1) continue;
            int alive = units.Length;
            // The fielded count, not the recruited size: a squad that starts the battle depleted must not fire at once.
            if (tremor.ValueRO.StartCount == 0) { tremor.ValueRW.StartCount = alive; continue; }
            if (alive >= tremor.ValueRO.StartCount * TabletopTavernConstants.POSEIDON_TREMOR_MODEL_SHARE) continue;

            tremor.ValueRW.Spent = 1;
            OlympianStrikes.Burst(ecb, movement.ValueRO.SquadCenter, TabletopTavernConstants.POSEIDON_TREMOR_RADIUS, 0,
                TabletopTavernConstants.POSEIDON_TREMOR_FORCE, squad.ValueRO.Team, squad.ValueRO.SquadId);
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Tremor });
        }
    }
}
#endif
