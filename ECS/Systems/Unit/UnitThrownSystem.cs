using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Unity.Transforms;
using ProjectDawn.Navigation;
using UnityEngine;
using UnityEngine.Experimental.AI;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(ProcessUnitDeathSystem))]
partial struct UnitThrownSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        float deltaTime = SystemAPI.Time.DeltaTime;

        // A thrown unit has NavMeshPath disabled, so UnitCollisionSystem skips it and nothing stops it at a wall or gate.
        bool hasNavMesh = SystemAPI.TryGetSingleton(out NavMeshQuerySystem.Singleton navmesh);
        ComponentLookup<NavMeshPath> pathLookup = SystemAPI.GetComponentLookup<NavMeshPath>();
        BufferLookup<NavMeshNode> nodesLookup = SystemAPI.GetBufferLookup<NavMeshNode>();
        ComponentLookup<AgentShape> shapeLookup = SystemAPI.GetComponentLookup<AgentShape>(true);
        NativeList<UnitCollisionSystem.GateData> gates = new NativeList<UnitCollisionSystem.GateData>(4, Allocator.Temp);
        foreach (var (gate, gateTransform, gateShape, gateEntity) in SystemAPI.Query<RefRO<GateCollisionShape>, RefRO<LocalTransform>, RefRO<AgentShape>>().WithEntityAccess())
        {
            gates.Add(new UnitCollisionSystem.GateData
            {
                Entity = gateEntity,
                Centre = gateTransform.ValueRO.Position.xz,
                Y = gateTransform.ValueRO.Position.y,
                Height = gateShape.ValueRO.Height,
                Radius = gateShape.ValueRO.Radius,
                Axis = gate.ValueRO.Axis,
                HalfWidth = gate.ValueRO.HalfWidth,
            });
        }

        foreach (var (ThrowUnit, unit, localTransform, setDestination, agentBody, Entity) in SystemAPI.Query<
            RefRW<ThrowUnit>,
            RefRO<Unit>,
            RefRW<LocalTransform>,
            RefRW<SetDestination>,
            RefRW<AgentBody>
            >().WithEntityAccess()
            .WithNone<KillUnitTag>())
        {
            // If the unit has been killed during the throw, strip ThrowUnit immediately so
            // ProcessUnitDeathSystem (which filters .WithNone<ThrowUnit>) can run next frame
            // and properly remove the entity from the squad's EntityReferenceBufferElement.
            if (state.EntityManager.HasComponent<UnitRemovedFromSquad>(Entity))
            {
                ecb.RemoveComponent<ThrowUnit>(Entity);
                continue;
            }

            // Debug.Log($"UnitThrownSystem: Updating thrown unit entity {Entity}");
            ThrowUnit.ValueRW.TotalTime -= deltaTime;

            if(ThrowUnit.ValueRO.Damage>0)
            {
                DynamicBuffer<DamageBufferElement> damageBuffer = SystemAPI.GetBuffer<DamageBufferElement>(Entity);
                damageBuffer.Add(new DamageBufferElement
                {
                    AttackStrength = ThrowUnit.ValueRO.Damage,
                    DamageSource = DamageSource.Melee,
                    DamageType = DamageType.Physical,
                    TeamOfSource = ThrowUnit.ValueRO.HittingEntityTeam,
                    DamageSourceSquadId = ThrowUnit.ValueRO.HittingEntitySquad,
                    SourceIsArtillery = true,
                });
                //set damage to 0 so it only applies on the first frame of the throw
                ThrowUnit.ValueRW.Damage = 0;
            }

            //print entity name for debugging
            // Debug.Log($"UnitThrownSystem: Updating thrown unit entity {Entity}");

            // Inside the update or effect loop:
            if (ThrowUnit.ValueRW.RemainingTime > 0)
            {
                float totalLifetime = 1f; // Total time for the effect
                ThrowUnit.ValueRW.RemainingTime -= deltaTime;

                // Calculate the normalized elapsed time (0 -> 1 over the total lifetime)
                float normalizedElapsedTime = math.saturate(1f - (ThrowUnit.ValueRW.RemainingTime / totalLifetime));

                // Use initialPosition for direction calculation to avoid feedback loop.
                // Horizontal only: the arc handles y. An artillery shell explodes at the exact x/z of the
                // unit it was aimed at, so that unit's offset is zero (or purely vertical). math.normalize
                // and quaternion.LookRotation both return NaN there, which left the unit unrendered and
                // invisible to every physics query for the rest of the battle. Push it along its own
                // facing instead, and fall back to +Z if even that is degenerate.
                float3 offset = ThrowUnit.ValueRO.InitialLocation - ThrowUnit.ValueRO.HittingEntityLocation;
                offset.y = 0f;
                float3 direction = math.normalizesafe(offset, float3.zero);
                if (math.lengthsq(direction) < 0.5f)
                {
                    float3 facing = math.forward(localTransform.ValueRO.Rotation);
                    facing.y = 0f;
                    direction = math.normalizesafe(facing, new float3(0f, 0f, 1f));
                }
                float3 totalDisplacement = direction * ThrowUnit.ValueRO.Force;
                float3 displacementPerFrame = totalDisplacement * deltaTime / totalLifetime;

                // Calculate Y-axis displacement to create an arc
                float yDisplacement = -4f * (normalizedElapsedTime - 0.5f) * (normalizedElapsedTime - 0.5f) + 1f;
                yDisplacement *= 0.25f; // Scale the arc height
                if (normalizedElapsedTime > 0.5f)
                {
                    yDisplacement = -yDisplacement;
                }
                if (normalizedElapsedTime > 0.25f && normalizedElapsedTime < 0.75f)
                {
                    yDisplacement *= 0.3f;
                }
                // float t = normalizedElapsedTime;
                // float heightFactor = 4f * t * (1f - t);           // 0 → 1 → 0 parabola
                // float arcHeight = 0.15f;
                // float yDisplacement = heightFactor * arcHeight;

                if (ThrowUnit.ValueRO.Force < 3f) yDisplacement = 0;

                float3 arcDisplacement = new float3(displacementPerFrame.x, yDisplacement, displacementPerFrame.z);

                // Update the entity's position with displacement and Y arc
                localTransform.ValueRW.Position += arcDisplacement;

                // Walls are carved out of the navmesh, so walking the step across it stops at the wall face.
                if (hasNavMesh && pathLookup.HasComponent(Entity))
                {
                    RefRW<NavMeshPath> path = pathLookup.GetRefRW(Entity);
                    if (!path.ValueRO.Location.polygon.IsNull())
                    {
                        NavMeshLocation newLocation = navmesh.MoveLocation(path.ValueRO.Location, localTransform.ValueRO.Position, path.ValueRO.AreaMask);
                        localTransform.ValueRW.Position.xz = ((float3)newLocation.position).xz;
                        if (nodesLookup.HasBuffer(Entity))
                        {
                            DynamicBuffer<NavMeshNode> nodes = nodesLookup[Entity];
                            navmesh.ProgressPath(ref nodes, path.ValueRO.Location.polygon, newLocation.polygon);
                        }
                        path.ValueRW.Location = newLocation;
                    }
                }

                // The gateway has no navmesh obstacle; the gate unit's capsule is the only thing that closes it.
                float unitRadius = shapeLookup.TryGetComponent(Entity, out AgentShape unitShape) ? unitShape.Radius : 0.75f;
                for (int g = 0; g < gates.Length; g++)
                {
                    UnitCollisionSystem.GateData gate = gates[g];
                    float2 position = localTransform.ValueRO.Position.xz;
                    float along = math.clamp(math.dot(position - gate.Centre, gate.Axis), -gate.HalfWidth, gate.HalfWidth);
                    float2 closest = gate.Centre + gate.Axis * along;
                    float2 towards = position - closest;
                    float distance = math.length(towards);
                    float contactRadius = unitRadius + gate.Radius;
                    if (distance >= contactRadius) continue;
                    float2 outward = distance < 1e-4f ? -direction.xz : towards / distance;
                    localTransform.ValueRW.Position.xz = closest + outward * contactRadius;
                }

                //clamp it above the starting position
                if (localTransform.ValueRW.Position.y < ThrowUnit.ValueRO.InitialLocation.y)
                {
                    localTransform.ValueRW.Position.y = ThrowUnit.ValueRO.InitialLocation.y;
                }

                // Rotate the unit to face the direction of the spell
                localTransform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, new float3(0, 1, 0));
                // Debug.Log($"normalizedElapsedTime: {normalizedElapsedTime}, Y position: {localTransform.ValueRW.Position.y}");
            }
            else
            {
                localTransform.ValueRW.Position.y = ThrowUnit.ValueRO.InitialLocation.y;
            }

            // If the force duration ends, clean up components
            if (ThrowUnit.ValueRW.TotalTime <= 0)
            {
                ecb.RemoveComponent<ThrowUnit>(Entity);
                ecb.SetComponentEnabled<NavMeshPath>(Entity, true);
                ecb.SetComponentEnabled<AgentSonarAvoid>(Entity, true);
                agentBody.ValueRW.IsStopped = false;
                agentBody.ValueRW.SetDestination(setDestination.ValueRO.squadPosition);
            }
        }
    }
}
