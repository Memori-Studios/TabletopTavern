using GPUECSAnimationBaker.Engine.AnimatorSystem;
using TabletopTavern.GpuAnim;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

// The only game code that knows the third-party animator. Game systems write GpuAnimControl on every visual; for the
// bakes still made with the old package, this file forwards it, and turns the old clip events back into GpuAnimControl.

/// <summary>Copies the game's requests into the old package's control right before its animator reads them.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ReturnToIdleEventHandlerSystem))]
[UpdateBefore(typeof(GpuEcsAnimatorSystem))]
[BurstCompile]
public partial struct GpuAnimLegacyBridgeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new BridgeJob { States = SystemAPI.GetComponentLookup<GpuEcsAnimatorControlStateComponent>(false) }.Schedule();
    }

    [BurstCompile]
    public partial struct BridgeJob : IJobEntity
    {
        public ComponentLookup<GpuEcsAnimatorControlStateComponent> States;

        private void Execute(Entity entity, ref GpuAnimControl control, ref GpuAnimRestart restart, ref GpuAnimLegacyState legacy,
            ref GpuEcsAnimatorControlComponent package)
        {
            // Something outside the bridge changed the package's slot; the game reads GpuAnimControl, so take it over.
            if (package.animatorInfo.animationID != legacy.LastSlot) control.Slot = package.animatorInfo.animationID;

            if (control.Slot != legacy.LastSlot)
            {
                package.animatorInfo.animationID = control.Slot;
                legacy.LastSlot = control.Slot;
            }
            if (control.TransitionSeconds != legacy.LastTransition)
            {
                package.transitionSpeed = control.TransitionSeconds;
                legacy.LastTransition = control.TransitionSeconds;
            }
            if (control.StartNormalizedTime != legacy.LastStart)
            {
                package.startNormalizedTime = control.StartNormalizedTime;
                legacy.LastStart = control.StartNormalizedTime;
            }
            if (restart.Value)
            {
                restart.Value = false;
                if (States.HasComponent(entity))
                {
                    GpuEcsAnimatorControlStateComponent s = States[entity];
                    s.state = GpuEcsAnimatorControlStates.Start;
                    States[entity] = s;
                }
            }
        }
    }
}

/// <summary>The old bakes return to idle through clip events: id 20 means the event's own animator, any other id the unit's visual.</summary>
[BurstCompile]
public partial class ReturnToIdleEventHandlerSystem : SystemBase
{
    const int CavalryReturnToIdleEventId = 20;

    [BurstCompile]
    protected override void OnUpdate()
    {
        EntityManager entityManager = World.EntityManager;
        Entities.ForEach((in DynamicBuffer<GpuEcsAnimatorEventBufferElement> gpuEcsAnimatorEventBuffer, in Entity eventEntity) =>
        {
            foreach (GpuEcsAnimatorEventBufferElement animatorEvent in gpuEcsAnimatorEventBuffer)
            {
                if (animatorEvent.eventId == CavalryReturnToIdleEventId)
                {
                    if (!entityManager.HasComponent<GpuAnimControl>(eventEntity)) continue;
                    GpuAnimControl controlComp = entityManager.GetComponentData<GpuAnimControl>(eventEntity);
                    controlComp.Slot = 0;
                    controlComp.TransitionSeconds = 0.5f;
                    entityManager.SetComponentData(eventEntity, controlComp);
                    continue;
                }

                if (!entityManager.HasComponent<Parent>(eventEntity))
                    continue;

                Parent parent = SystemAPI.GetComponent<Parent>(eventEntity);

                // Parent entity was destroyed (unit died and was cleaned up by KillUnitSystem)
                if (!entityManager.HasComponent<AnimationDataHolder>(parent.Value))
                    continue;

                AnimationDataHolder animDataHolder = entityManager.GetComponentData<AnimationDataHolder>(parent.Value);
                if (!entityManager.HasComponent<GpuAnimControl>(animDataHolder.gpuEcsAnimatorEntity)) continue;
                GpuAnimControl parentControlComp = entityManager.GetComponentData<GpuAnimControl>(animDataHolder.gpuEcsAnimatorEntity);
                parentControlComp.Slot = animDataHolder.currentIdleAnimationId;
                parentControlComp.TransitionSeconds = 0.5f;
                entityManager.SetComponentData(animDataHolder.gpuEcsAnimatorEntity, parentControlComp);
            }
        }).Run();
    }
}
