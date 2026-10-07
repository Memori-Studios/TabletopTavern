using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using ProjectDawn.Navigation;

/// <summary>
/// Counts down a large model's drive into an infantry line. While it runs the model ignores sonar
/// avoidance, MeleeUnitCombatJob keeps it heading along the charge and UnitCollisionSystem weighs it
/// heavier. It ends on distance, on time, or when the squad leaves the fight.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct ChargePenetrationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<ChargePenetration>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (penetration, body, unit, entity) in SystemAPI.Query<
            RefRW<ChargePenetration>,
            RefRO<AgentBody>,
            RefRO<Unit>>()
            .WithEntityAccess())
        {
            penetration.ValueRW.TimeLeft -= deltaTime;
            penetration.ValueRW.DistanceLeft -= math.length(body.ValueRO.Velocity.xz) * deltaTime;

            bool fighting = unit.ValueRO.unitState == UnitState.InCombat || unit.ValueRO.unitState == UnitState.OnEngage;
            bool done = !fighting
                || penetration.ValueRO.TimeLeft <= 0f
                || penetration.ValueRO.DistanceLeft <= 0f
                || SystemAPI.HasComponent<ThrowUnit>(entity);

            // Entering combat turns sonar back on, so it is held off every frame of the drive.
            ecb.SetComponentEnabled<AgentSonarAvoid>(entity, done);
            if (done) ecb.SetComponentEnabled<ChargePenetration>(entity, false);
        }
    }
}
