using Unity.Burst;
using Unity.Entities;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectDawn.Navigation;

partial struct UnitIdleSystem : ISystem
{
    public Random random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        random = new Random(1);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;

        foreach (var (idleAnimation, unit, entity) in SystemAPI
            .Query<RefRO<AnimationDataHolder>, RefRO<Unit>>()
            .WithNone<GarrisonGateUnit>()
            .WithNone<InMeleeRange, UnitRemovedFromSquad>()
            .WithEntityAccess())
        {
            if (unit.ValueRO.unitState == UnitState.Dead || 
                unit.ValueRO.unitState == UnitState.Moving || 
                unit.ValueRO.unitState == UnitState.Charge ||
                unit.ValueRO.unitState == UnitState.Broken )
                continue;

            float animFrequency = entityManager.HasComponent<InCombat>(entity)
                ? TabletopTavernConstants.COMBAT_ANIMATION_FREQUENCY
                : TabletopTavernConstants.IDLE_ANIMATION_FREQUENCY;

            if (random.NextFloat() >= animFrequency)
                continue;

            if (!SystemAPI.HasComponent<GpuEcsAnimatorControlComponent>(idleAnimation.ValueRO.gpuEcsAnimatorEntity))
                continue;

            RefRW<GpuEcsAnimatorControlComponent> controlComp = SystemAPI.GetComponentRW<GpuEcsAnimatorControlComponent>(
                idleAnimation.ValueRO.gpuEcsAnimatorEntity);

            if (controlComp.ValueRO.animatorInfo.animationID != idleAnimation.ValueRO.currentIdleAnimationId)
                continue;

            if (unit.ValueRO.unitType == UnitType.Ranged && unit.ValueRO.unitState == UnitState.InCombat)
                continue;

            controlComp.ValueRW.animatorInfo.animationID = idleAnimation.ValueRO.idleAnimationIds[random.NextInt(0, 3)];
            controlComp.ValueRW.transitionSpeed = 0.5f;

            if (random.NextFloat() < 0.05f && entityManager.HasBuffer<SFXBufferElement>(entity))
            {
                DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(entity);
                sfxBuffer.Add(new SFXBufferElement { UnitName = unit.ValueRO.unitName, SFXEntityType = Memori.Audio.SFXEntityType.Idle, MaxDistance = 30f });
            }
        }
    }
}

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
                    GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(eventEntity);
                    controlComp.animatorInfo.animationID = 0;
                    controlComp.transitionSpeed = 0.5f;
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
                GpuEcsAnimatorControlComponent parentControlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(
                    animDataHolder.gpuEcsAnimatorEntity);
                parentControlComp.animatorInfo.animationID = animDataHolder.currentIdleAnimationId;
                parentControlComp.transitionSpeed = 0.5f;
                entityManager.SetComponentData(animDataHolder.gpuEcsAnimatorEntity, parentControlComp);
            }
        }).Run();
    }
}
