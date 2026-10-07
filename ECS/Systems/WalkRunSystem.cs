using Unity.Entities;
using ProjectDawn.Navigation;
using TabletopTavern.GpuAnim;
using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using TJ;

partial struct WalkRunSystem : ISystem
{
    public Unity.Mathematics.Random random;
    private ComponentLookup<GpuAnimControl> gpuEcsAnimatorControlComponentLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        random = new Unity.Mathematics.Random(1);
        gpuEcsAnimatorControlComponentLookup = state.GetComponentLookup<GpuAnimControl>(false);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        gpuEcsAnimatorControlComponentLookup.Update(ref systemState);
        WalkRunJob walkRunJob = new () {
            gpuLookup = gpuEcsAnimatorControlComponentLookup,
            random = random
        };

        systemState.Dependency = walkRunJob.Schedule(systemState.Dependency);
    }
}

[BurstCompile]
[WithAbsent(typeof(ThrowUnit))]
[WithAbsent(typeof(GarrisonGateUnit))]
public partial struct WalkRunJob : IJobEntity {
    public Unity.Mathematics.Random random;
    public ComponentLookup<GpuAnimControl> gpuLookup;
    public void Execute (ref AgentBody agentBody, ref AnimationDataHolder animationDataHolder, Entity entity) {
        GpuAnimControl controlComp = gpuLookup[animationDataHolder.gpuEcsAnimatorEntity];

        if(agentBody.Speed > animationDataHolder.RunSpeedThreshold) {
            if (controlComp.Slot != animationDataHolder.runningAnimationId) {
                var localRandom = random; // Copy the base random
                localRandom.InitState((uint)entity.Index + random.NextUInt()); // Seed the random with the entity index
                controlComp.StartNormalizedTime = localRandom.NextFloat();
                controlComp.Slot = animationDataHolder.runningAnimationId;
                gpuLookup[animationDataHolder.gpuEcsAnimatorEntity] = controlComp;
            }
        } else if(agentBody.Speed > animationDataHolder.WalkSpeedThreshold) {
            if (controlComp.Slot != animationDataHolder.walkAnimationId ||
                controlComp.Slot != animationDataHolder.currentIdleAnimationId) {
                controlComp.Slot = animationDataHolder.walkAnimationId;
                gpuLookup[animationDataHolder.gpuEcsAnimatorEntity] = controlComp;
            }
        }else {
            if (controlComp.Slot == animationDataHolder.walkAnimationId ||
                controlComp.Slot == animationDataHolder.runningAnimationId ) {
                controlComp.StartNormalizedTime = 0;
                controlComp.Slot = animationDataHolder.currentIdleAnimationId;
                gpuLookup[animationDataHolder.gpuEcsAnimatorEntity] = controlComp;
            }
        }
    }
}

// foreach (var (AgentBody, AnimationDataHolder) in SystemAPI.Query<
//     RefRO<AgentBody>,
//     RefRW<AnimationDataHolder>
// >()
// .WithNone<ForceLifetime>()
// ){
//     RefRW<GpuEcsAnimatorControlComponent> controlComp = SystemAPI.GetComponentRW<GpuEcsAnimatorControlComponent>(AnimationDataHolder.ValueRO.gpuEcsAnimatorEntity);

//     if(AgentBody.ValueRO.Speed > 2) {
//         if (controlComp.ValueRO.animatorInfo.animationID != AnimationDataHolder.ValueRO.runningAnimationId) {
//             controlComp.ValueRW.startNormalizedTime = random.NextFloat();
//             controlComp.ValueRW.animatorInfo.animationID = AnimationDataHolder.ValueRO.runningAnimationId;
//         }
//     } else if(AgentBody.ValueRO.Speed > 1f) {
//         if (controlComp.ValueRO.animatorInfo.animationID != AnimationDataHolder.ValueRO.walkAnimationId ||
//             controlComp.ValueRO.animatorInfo.animationID != AnimationDataHolder.ValueRO.currentIdleAnimationId) {
//             controlComp.ValueRW.animatorInfo.animationID = AnimationDataHolder.ValueRO.walkAnimationId;
//         }
//     }else {
//         if (controlComp.ValueRO.animatorInfo.animationID == AnimationDataHolder.ValueRO.walkAnimationId ||
//             controlComp.ValueRO.animatorInfo.animationID == AnimationDataHolder.ValueRO.runningAnimationId ) {
//             controlComp.ValueRW.startNormalizedTime = 0;
//             controlComp.ValueRW.animatorInfo.animationID = AnimationDataHolder.ValueRO.currentIdleAnimationId;
//             // Debug.Log($"Setting to idle");
//         }
//     }
// }