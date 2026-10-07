using TabletopTavern.GpuAnim;
using Unity.Burst;
using Unity.Entities;

/// <summary>Keeps each visual's return target at its unit's current stance idle, which the game changes on engage, brace and disengage.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(GpuAnimPlaybackSystem))]
[BurstCompile]
public partial struct GpuAnimIdleSlotSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new IdleSlotJob { Controls = SystemAPI.GetComponentLookup<GpuAnimControl>(false) }.Schedule();
    }

    [BurstCompile]
    public partial struct IdleSlotJob : IJobEntity
    {
        public ComponentLookup<GpuAnimControl> Controls;

        private void Execute(in AnimationDataHolder holder)
        {
            if (!Controls.HasComponent(holder.gpuEcsAnimatorEntity)) return;
            RefRW<GpuAnimControl> control = Controls.GetRefRW(holder.gpuEcsAnimatorEntity);
            control.ValueRW.IdleSlot = holder.currentIdleAnimationId;
        }
    }
}
