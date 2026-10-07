using Unity.Burst;
using Unity.Entities;
using TabletopTavern.GpuAnim;
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

            if (!SystemAPI.HasComponent<GpuAnimControl>(idleAnimation.ValueRO.gpuEcsAnimatorEntity))
                continue;

            RefRW<GpuAnimControl> controlComp = SystemAPI.GetComponentRW<GpuAnimControl>(
                idleAnimation.ValueRO.gpuEcsAnimatorEntity);

            if (controlComp.ValueRO.Slot != idleAnimation.ValueRO.currentIdleAnimationId)
                continue;

            if (unit.ValueRO.unitType == UnitType.Ranged && unit.ValueRO.unitState == UnitState.InCombat)
                continue;

            controlComp.ValueRW.Slot = idleAnimation.ValueRO.idleAnimationIds[random.NextInt(0, 3)];
            controlComp.ValueRW.TransitionSeconds = 0.5f;

            if (random.NextFloat() < 0.05f && entityManager.HasBuffer<SFXBufferElement>(entity))
            {
                DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(entity);
                sfxBuffer.Add(new SFXBufferElement { UnitName = unit.ValueRO.unitName, SFXEntityType = Memori.Audio.SFXEntityType.Idle, MaxDistance = 30f });
            }
        }
    }
}
