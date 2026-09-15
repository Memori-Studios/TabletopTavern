using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct HealthBarSystem : ISystem {
    private ComponentLookup<LocalTransform> localTransformComponentLookup;
    private ComponentLookup<Health> healthComponentLookup;
    private ComponentLookup<MaxHitPoints> maxHitPointsComponentLookup;
    private ComponentLookup<PostTransformMatrix> postTransformMatrixComponentLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
        localTransformComponentLookup = state.GetComponentLookup<LocalTransform>();
        healthComponentLookup = state.GetComponentLookup<Health>(true);
        maxHitPointsComponentLookup = state.GetComponentLookup<MaxHitPoints>(true);
        postTransformMatrixComponentLookup = state.GetComponentLookup<PostTransformMatrix>(false);
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state) {
        Vector3 cameraForward = Vector3.zero;
        if (Camera.main != null) {
            cameraForward = Camera.main.transform.forward;
        }

        localTransformComponentLookup.Update(ref state);
        healthComponentLookup.Update(ref state);
        maxHitPointsComponentLookup.Update(ref state);
        postTransformMatrixComponentLookup.Update(ref state);
        
        HealthBarJob healthBarJob = new HealthBarJob {
            cameraForward = cameraForward,
            localTransformComponentLookup = localTransformComponentLookup,
            healthComponentLookup = healthComponentLookup,
            maxHitPointsComponentLookup = maxHitPointsComponentLookup,
            postTransformMatrixComponentLookup = postTransformMatrixComponentLookup,
        };
        state.Dependency = healthBarJob.ScheduleParallel(state.Dependency);
    }
}


[BurstCompile]
public partial struct HealthBarJob : IJobEntity {
    //[NativeDisableParallelForRestriction] dont use this unless I am positive that I wont be overwriting the same data
    [ReadOnly] public ComponentLookup<Health> healthComponentLookup;
    [ReadOnly] public ComponentLookup<MaxHitPoints> maxHitPointsComponentLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> localTransformComponentLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<PostTransformMatrix> postTransformMatrixComponentLookup;
    public float3 cameraForward;

    public void Execute(in HealthBar healthBar, Entity entity) {
        RefRW<LocalTransform> localTransform = localTransformComponentLookup.GetRefRW(entity);
        LocalTransform parentLocalTransform = localTransformComponentLookup[healthBar.healthEntity];
        // cameraForward is zero while no enabled main camera exists (scene door, Collection overlay), and
        // quaternion.LookRotation returns NaN for a zero or vertical forward. Keep the last rotation then.
        if (localTransform.ValueRO.Scale == 1f && math.lengthsq(cameraForward) > 0.0001f) {
            // Health bar is visible
            localTransform.ValueRW.Rotation = parentLocalTransform.InverseTransformRotation(quaternion.LookRotationSafe(cameraForward, math.up()));
        }
        if(!healthComponentLookup.HasComponent(entity)) return; // Check if the entity has a Health component

        Health health = healthComponentLookup[healthBar.healthEntity];
        MaxHitPoints maxHitPoints = maxHitPointsComponentLookup[healthBar.healthEntity];

        if (!health.onHealthChanged) {
            return;
        }

        if (maxHitPoints.Value <= 0) return;
        float healthNormalized = math.clamp((float)health.Value / maxHitPoints.Value, 0f, 1f);

        if (healthNormalized == 1f) {
            localTransform.ValueRW.Scale = 0f;
        } else {
            localTransform.ValueRW.Scale = 1f;
        }

        RefRW<PostTransformMatrix> barVisualPostTransformMatrix =
            postTransformMatrixComponentLookup.GetRefRW(healthBar.barVisualEntity);

        barVisualPostTransformMatrix.ValueRW.Value = float4x4.Scale(healthNormalized, 1, 1);
    }
}