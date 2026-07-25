using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Collections;

// Boblin's Snare Trap. Watches each placed trap for an enemy entering its trigger radius; when one does,
// it springs a one-off burst SpellEntity at the trap's spot (so SpellSystem does the damage + knockback,
// no duplicated logic) and removes the trap. Unsprung traps expire after RemainingArmedTime.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct SnareTrapSystem : ISystem
{
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
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        float deltaTime = SystemAPI.Time.DeltaTime;

        CollisionFilter collisionFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
            GroupIndex = 0,
        };

        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

        foreach (var (trap, entity) in SystemAPI.Query<RefRW<SnareTrapEntity>>().WithEntityAccess())
        {
            bool triggered = false;
            hits.Clear();
            if (collisionWorld.OverlapSphere(trap.ValueRO.Position, trap.ValueRO.TriggerRadius, ref hits, collisionFilter))
            {
                foreach (DistanceHit hit in hits)
                {
                    Entity u = hit.Entity;
                    if (!SystemAPI.Exists(u) || !SystemAPI.HasComponent<Unit>(u)) continue;
                    if (SystemAPI.HasComponent<Health>(u) && SystemAPI.GetComponent<Health>(u).Value <= 0) continue;
                    if (SystemAPI.GetComponent<Unit>(u).Team != trap.ValueRO.OwnerTeam) { triggered = true; break; }
                }
            }

            if (triggered)
            {
                // Spring: a one-off burst SpellEntity carries the damage + knockback via SpellSystem.
                Entity burst = ecb.CreateEntity();
                ecb.AddComponent(burst, new SpellEntity
                {
                    Entity = burst,
                    DamageBufferElement = new DamageBufferElement
                    {
                        DamageType = DamageType.Magical,
                        AttackStrength = trap.ValueRO.Damage,
                        TeamOfSource = trap.ValueRO.OwnerTeam,
                        DamageSourceSquadId = 0
                    },
                    SpellPosition = trap.ValueRO.Position,
                    SpellRadius = trap.ValueRO.BlastRadius,
                    IsOneOff = true,
                    SpellForce = trap.ValueRO.SpellForce,
                    RemainingDuration = 0f,
                    TargetSquadEntity = Entity.Null,
                    TickInterval = 0f,
                    TickTimer = 0f
                });
                ecb.DestroyEntity(entity);
                continue;
            }

            trap.ValueRW.RemainingArmedTime -= deltaTime;
            if (trap.ValueRO.RemainingArmedTime <= 0f) ecb.DestroyEntity(entity);
        }

        hits.Dispose();
    }
}
