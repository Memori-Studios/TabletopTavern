#if FACTIONUPDATE
using ProjectDawn.Navigation;
using TabletopTavern.GpuAnim;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Gorgon's Gaze: the squad that charged the Gorgon stands as stone for GORGON_GAZE_SECONDS. A zero-force throw
// holds each model in place, unable to move or attack, and an animation speed of 0 holds its pose.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct PetrifySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (petrified, control, model) in SystemAPI.Query<RefRW<Petrified>, RefRW<GpuAnimControl>>().WithEntityAccess())
        {
            petrified.ValueRW.Remaining -= dt;
            // A dying model thaws at once so its death clip plays; the unit is marked removed a frame before that clip is set.
            Entity unit = petrified.ValueRO.Unit;
            bool dying = !SystemAPI.Exists(unit) || SystemAPI.HasComponent<UnitRemovedFromSquad>(unit) || SystemAPI.HasComponent<KillUnitTag>(unit);
            if (petrified.ValueRO.Remaining > 0f && !dying) continue;
            control.ValueRW.Speed = 1f;
            ecb.RemoveComponent<Petrified>(model);
        }

        float seconds = TabletopTavernConstants.GORGON_GAZE_SECONDS;
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);
        foreach (var (squad, movement, units, squadEntity) in SystemAPI.Query<
            RefRO<SquadEntity>, RefRO<SquadMovementComponent>, DynamicBuffer<EntityReferenceBufferElement>>()
            .WithAll<PetrifyRequest>().WithEntityAccess())
        {
            ecb.RemoveComponent<PetrifyRequest>(squadEntity);
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Gaze });
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Entity;
                // A model already mid-throw keeps its throw; overwriting it would leave it hanging in the air.
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<AgentBody>(unit) || SystemAPI.HasComponent<ThrowUnit>(unit)
                    || SystemAPI.HasComponent<KillUnitTag>(unit) || SystemAPI.HasComponent<UnitRemovedFromSquad>(unit)) continue;

                float3 position = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                ecb.AddComponent(unit, new ThrowUnit
                {
                    HittingEntityLocation = position,
                    InitialLocation = position,
                    HittingEntitySquad = squad.ValueRO.SquadId,
                    HittingEntityTeam = squad.ValueRO.Team,
                    TotalTime = seconds,
                });
                ecb.SetComponentEnabled<NavMeshPath>(unit, false);
                ecb.SetComponentEnabled<AgentSonarAvoid>(unit, false);
                RefRW<AgentBody> body = SystemAPI.GetComponentRW<AgentBody>(unit);
                body.ValueRW.IsStopped = true;
                body.ValueRW.SetDestination(position);

                if (SystemAPI.HasComponent<AnimationDataHolder>(unit))
                    Freeze(ref state, ecb, SystemAPI.GetComponent<AnimationDataHolder>(unit).gpuEcsAnimatorEntity, unit, seconds);
                if (SystemAPI.HasComponent<Cavalry>(unit))
                    Freeze(ref state, ecb, SystemAPI.GetComponent<Cavalry>(unit).riderEntity, unit, seconds);
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void Freeze(ref SystemState state, EntityCommandBuffer ecb, Entity model, Entity unit, float seconds)
    {
        if (model == Entity.Null || !SystemAPI.HasComponent<GpuAnimControl>(model)) return;
        SystemAPI.GetComponentRW<GpuAnimControl>(model).ValueRW.Speed = 0f;
        ecb.AddComponent(model, new Petrified { Remaining = seconds, Unit = unit });
    }
}
#endif
