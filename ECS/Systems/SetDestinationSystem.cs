using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Unity.Collections;
using ProjectDawn.Navigation;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = false)]
partial struct SetDestinationDebugSystem : ISystem
{
    private ComponentLookup<LocalTransform> localTransformComponentLookup;
    private ComponentLookup<SetDestination> setDestinationComponentLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        localTransformComponentLookup = state.GetComponentLookup<LocalTransform>(true);
        setDestinationComponentLookup = state.GetComponentLookup<SetDestination>(false);
        state.RequireForUpdate<BattleHasNotEnded>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        localTransformComponentLookup.Update(ref state);
        setDestinationComponentLookup.Update(ref state);

        SetDestinationToDebugJob setDestinationToDebugJob = new () {
            localTransformComponentLookup = localTransformComponentLookup,
            setDestinationComponentLookup = setDestinationComponentLookup
        };

        state.Dependency = setDestinationToDebugJob.Schedule(state.Dependency);
    }
}
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
partial struct SetDestinationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        SetDestinationJob setDestinationJob = new () {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };

        state.Dependency = setDestinationJob.Schedule(state.Dependency);
    }
}
[BurstCompile]
[WithAbsent(typeof(ThrowUnit))]
[WithAbsent(typeof(InCombat))]
public partial struct SetDestinationToDebugJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> localTransformComponentLookup;
    public ComponentLookup<SetDestination> setDestinationComponentLookup;
    public void Execute(in DynamicBuffer<EntityReferenceBufferElement> entityBuffer)
    {
        for (int i = 0; i < entityBuffer.Length; i++)
        {
            Entity entity = entityBuffer[i].Entity;
            if (!setDestinationComponentLookup.HasComponent(entity))
                continue;

            Entity debugEntity = entityBuffer[i].DebugEntity;
            if (!localTransformComponentLookup.HasComponent(debugEntity))
                continue;
            

            SetDestination setDestination = setDestinationComponentLookup[entity];
            setDestination.destinationPosition = localTransformComponentLookup[debugEntity].Position;
            setDestinationComponentLookup[entity] = setDestination;
        }
    }
}


[BurstCompile]
[WithAbsent(typeof(ThrowUnit))]
// [WithAbsent(typeof(InCombat))]
public partial struct SetDestinationJob : IJobEntity {
    [ReadOnly] public float DeltaTime;
    public void Execute (ref SetDestination setDestination, ref LocalTransform localTransform, ref AgentBody agentBody, Entity entity)
    {
        if(!agentBody.IsStopped) {
            setDestination.destinationPosition = new float3(
                setDestination.destinationPosition.x,
                localTransform.Position.y,
                setDestination.destinationPosition.z
            );
        }

        // Compare in XZ: the nav agent remaps the destination's Y to the navmesh, so a 3D compare
        // re-issued the destination every frame and every idle unit re-sought its slot unguarded.
        if(setDestination.destinationPosition.xz.Equals(agentBody.Destination.xz)) return;

        if(setDestination.delayRemaining > 0) {
            setDestination.delayRemaining -= DeltaTime;
            return;
        }

        agentBody.SetDestination(setDestination.destinationPosition);
    }
}