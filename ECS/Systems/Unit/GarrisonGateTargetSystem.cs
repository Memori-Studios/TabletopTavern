using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct GarrisonGateTargetSystem : ISystem
{
    private ComponentLookup<Unit> _unitLookup;
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        _unitLookup = state.GetComponentLookup<Unit>(true);
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _unitLookup.Update(ref state);
        _transformLookup.Update(ref state);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        foreach (var (shootAttack, target, transform) in SystemAPI
            .Query<RefRW<ShootAttack>, RefRW<Target>, RefRO<LocalTransform>>()
            .WithAll<GarrisonGateUnit>())
        {
            float2 gateForwardXZ = new float2(0f, -1f); // hardcoded test: gate faces (0,0,-1)

            // Validate existing target — clear if dead, retreating, or behind the gate
            if (target.ValueRO.targetEntity != Entity.Null)
            {
                bool clear = false;
                if (!_transformLookup.HasComponent(target.ValueRO.targetEntity))
                {
                    clear = true;
                }
                else
                {
                    float3 toExisting = _transformLookup[target.ValueRO.targetEntity].Position - transform.ValueRO.Position;
                    float2 toExistingXZ = new float2(toExisting.x, toExisting.z);
                    // Same 10 buffer RangedSquadFindTargetSystem gives ranged squads before they drop a target.
                    float keepRange = shootAttack.ValueRO.Range + 10f;
                    if (math.lengthsq(toExistingXZ) > keepRange * keepRange)
                        clear = true;
                    else if (math.lengthsq(toExistingXZ) > 0f)
                    {
                        float dot = math.dot(gateForwardXZ, math.normalize(toExistingXZ));
                        if (dot < 0.0872f) clear = true; // 85° half-angle = 170° arc
                    }
                }

                if (clear)
                    target.ValueRW.targetEntity = Entity.Null;
                else
                    continue; // target still valid and in cone, keep shooting
            }

            // Only scan for a new target when the shoot timer has expired
            if (shootAttack.ValueRO.timer > 0f)
                continue;

            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
                GroupIndex = 0
            };

            Entity closest = Entity.Null;
            float closestDist = float.MaxValue;

            if (physicsWorld.CollisionWorld.OverlapSphere(
                    transform.ValueRO.Position, shootAttack.ValueRO.Range,
                    ref hits, filter))
            {
                foreach (var hit in hits)
                {
                    if (!_unitLookup.HasComponent(hit.Entity)) continue;
                    if (_unitLookup[hit.Entity].Team != Team.Player) continue;

                    float3 toTarget = _transformLookup[hit.Entity].Position - transform.ValueRO.Position;
                    float2 toTargetXZ = new float2(toTarget.x, toTarget.z);
                    if (math.lengthsq(toTargetXZ) > 0f)
                    {
                        float dot = math.dot(gateForwardXZ, math.normalize(toTargetXZ));
                        if (dot < 0.0872f) continue; // 85° half-angle = 170° arc
                    }

                    if (hit.Distance < closestDist)
                    {
                        closestDist = hit.Distance;
                        closest = hit.Entity;
                    }
                }
            }

            hits.Dispose();
            target.ValueRW.targetEntity = closest;
        }
    }
}
