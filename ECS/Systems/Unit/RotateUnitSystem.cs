using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct RotateUnitSystem : ISystem
{
    private ComponentLookup<LocalTransform> _localTransformLookup;
    private ComponentLookup<InCombat> _inCombatLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        _inCombatLookup = state.GetComponentLookup<InCombat>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _localTransformLookup.Update(ref state);
        _inCombatLookup.Update(ref state);

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        // Pass 1 (InCombat only): update targetRotation from live target position.
        // Uses in LocalTransform (read-only handle) + ReadOnly ComponentLookup<LocalTransform> — no alias.
        state.Dependency = new UpdateCombatTargetRotationJob
        {
            LocalTransformLookup = _localTransformLookup,
        }.ScheduleParallel(state.Dependency);

        // Pass 2 (all RotateUnit entities): apply slerp and self-disable when done.
        // Writes LocalTransform.Rotation directly — no ComponentLookup<LocalTransform> needed.
        state.Dependency = new ApplyRotationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = ecb,
            InCombatLookup = _inCombatLookup,
        }.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Runs only on InCombat units. Recomputes RotateUnit.targetRotation from the live
/// target position every frame so units continuously face their current enemy.
/// </summary>
[BurstCompile]
[WithAll(typeof(InCombat), typeof(SetDestination))]
[WithNone(typeof(ThrowUnit))]
partial struct UpdateCombatTargetRotationJob : IJobEntity
{
    // ReadOnly ComponentLookup<LocalTransform> + in LocalTransform param = both read-only, no alias.
    [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;

    [BurstCompile]
    public void Execute(ref RotateUnit rotateUnit, in LocalTransform localTransform, in Target target)
    {
        if (target.targetEntity == Entity.Null || !LocalTransformLookup.HasComponent(target.targetEntity))
            return;

        float3 toTarget = LocalTransformLookup[target.targetEntity].Position - localTransform.Position;
        if (math.lengthsq(toTarget) > 0.0001f)
            rotateUnit.targetRotation = quaternion.LookRotation(math.normalizesafe(toTarget), math.up());
    }
}

/// <summary>
/// Slerps all active RotateUnit entities toward their targetRotation.
/// Out-of-combat units self-disable when the rotation is complete.
/// In-combat units never self-disable — UpdateCombatTargetRotationJob keeps them updated.
/// </summary>
[BurstCompile]
[WithNone(typeof(ThrowUnit))]
[WithAll(typeof(SetDestination))]
partial struct ApplyRotationJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter ECB;
    // InCombat is a structural (non-enableable) component — HasComponent is the correct check.
    [ReadOnly] public ComponentLookup<InCombat> InCombatLookup;

    [BurstCompile]
    public void Execute(
        Entity entity,
        ref RotateUnit rotateUnit,
        ref LocalTransform localTransform,
        [ChunkIndexInQuery] int chunkIndex)
    {
        // A unit taking its formation facing after a Move turns at the same fixed rate as its turn before the march.
        if (rotateUnit.steadyTurn && !InCombatLookup.HasComponent(entity))
        {
            float angle = 2f * math.acos(math.min(1f, math.abs(math.dot(localTransform.Rotation, rotateUnit.targetRotation))));
            float maxStep = math.radians(TabletopTavernConstants.MARCH_TURN_RATE) * DeltaTime;
            if (angle <= maxStep)
            {
                localTransform.Rotation = rotateUnit.targetRotation;
                ECB.SetComponentEnabled<RotateUnit>(chunkIndex, entity, false);
            }
            else
            {
                localTransform.Rotation = math.slerp(localTransform.Rotation, rotateUnit.targetRotation, maxStep / angle);
            }
            return;
        }

        localTransform.Rotation = math.slerp(
            localTransform.Rotation,
            rotateUnit.targetRotation,
            DeltaTime * 5f);

        // Only self-disable outside of combat. InCombat units are kept running by
        // UpdateCombatTargetRotationJob so their facing tracks the enemy continuously.
        if (!InCombatLookup.HasComponent(entity) &&
            math.abs(math.dot(localTransform.Rotation, rotateUnit.targetRotation)) > 0.99f)
        {
            ECB.SetComponentEnabled<RotateUnit>(chunkIndex, entity, false);
        }
    }
}
