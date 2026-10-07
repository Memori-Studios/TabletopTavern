using Unity.Burst;
using Unity.Entities;
using TabletopTavern.GpuAnim;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct ShootAnimationEventSystem : ISystem {
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {

        foreach ((RefRO<ShootAttack> shootAttack, Entity entity) in SystemAPI.Query<RefRO<ShootAttack>>().WithEntityAccess()) {

            if (shootAttack.ValueRO.onShoot.isTriggered) {
                RefRO<AnimationDataHolder> gpuEcsAnimatorAspect = SystemAPI.GetComponentRO<AnimationDataHolder>(entity);
                RefRW<GpuAnimControl> controlComp = SystemAPI.GetComponentRW<GpuAnimControl>(gpuEcsAnimatorAspect.ValueRO.gpuEcsAnimatorEntity);
                controlComp.ValueRW.Slot = gpuEcsAnimatorAspect.ValueRO.attackanimationId;
                SystemAPI.GetComponentRW<GpuAnimRestart>(gpuEcsAnimatorAspect.ValueRO.gpuEcsAnimatorEntity).ValueRW.Value = true;
            }

        }
    }


}