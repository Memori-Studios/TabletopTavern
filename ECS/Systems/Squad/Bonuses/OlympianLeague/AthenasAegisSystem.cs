#if FACTIONUPDATE
using Unity.Collections;
using Unity.Entities;

// Athena's Aegis: after ATHENAS_AEGIS_HOLD_SECONDS of unbroken melee the squad is shielded for ATHENAS_AEGIS_SECONDS, once.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct AthenasAegisSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (aegis, movement, entity) in SystemAPI.Query<RefRW<AthenasAegisBlessing>, RefRO<SquadMovementComponent>>().WithEntityAccess())
        {
            if (aegis.ValueRO.Spent == 0)
            {
                aegis.ValueRW.CombatTime = SystemAPI.HasComponent<InCombat>(entity) ? aegis.ValueRO.CombatTime + dt : 0f;
                if (aegis.ValueRO.CombatTime < TabletopTavernConstants.ATHENAS_AEGIS_HOLD_SECONDS) continue;
                aegis.ValueRW.Spent = 1;
                aegis.ValueRW.Remaining = TabletopTavernConstants.ATHENAS_AEGIS_SECONDS;
                ecb.AddComponent<AegisActiveTag>(entity);
                if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Aegis });
            }
            else if (aegis.ValueRO.Remaining > 0f)
            {
                aegis.ValueRW.Remaining -= dt;
                if (aegis.ValueRO.Remaining <= 0f) ecb.RemoveComponent<AegisActiveTag>(entity);
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

// Cancels every non-healing hit on a model whose squad holds the Aegis. A pre-ApplyDamage modifier like BloodPactSystem.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TJ.ApplyDamageSystem))]
[UpdateAfter(typeof(SpellSystem))]
[UpdateAfter(typeof(BloodPactSystem))]
partial struct AegisDamageSystem : ISystem
{
    private EntityQuery _aegisQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        _aegisQuery = SystemAPI.QueryBuilder().WithAll<AegisActiveTag>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_aegisQuery.IsEmpty) return;
        var aegisLookup = SystemAPI.GetComponentLookup<AegisActiveTag>(true);

        foreach (var (damageBuffer, parent) in SystemAPI.Query<DynamicBuffer<DamageBufferElement>, RefRO<UnitParentEntityTag>>())
        {
            if (!aegisLookup.HasComponent(parent.ValueRO.parentSquadEntity)) continue;
            for (int i = 0; i < damageBuffer.Length; i++)
            {
                ref DamageBufferElement element = ref damageBuffer.ElementAt(i);
                if (element.DamageType != DamageType.Healing) element.AttackStrength = 0;
            }
        }
    }
}
#endif
