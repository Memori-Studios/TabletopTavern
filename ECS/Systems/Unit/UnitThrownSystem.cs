using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using ProjectDawn.Navigation;
using UnityEngine;

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
