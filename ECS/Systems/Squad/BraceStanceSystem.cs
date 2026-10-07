using Unity.Collections;
using Unity.Entities;
using TabletopTavern.GpuAnim;

/// <summary>
/// A standing melee squad that a sprinting enemy is about to hit holds its melee stance, so the line
/// visibly readies before the impact. Visual only: BracedTag still decides what a brace does.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct BraceStanceSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        NativeHashSet<Entity> threatened = new NativeHashSet<Entity>(8, Allocator.Temp);
        foreach (RefRO<SquadEntity> squad in SystemAPI.Query<RefRO<SquadEntity>>().WithAll<SprintingTag>())
        {
            if (squad.ValueRO.TargetSquadEntity != Entity.Null) threatened.Add(squad.ValueRO.TargetSquadEntity);
        }

        foreach (var (units, entity) in SystemAPI.Query<DynamicBuffer<EntityReferenceBufferElement>>()
            .WithAll<MeleeSquad>()
            .WithNone<BraceStanceTag, InCombat, ChargeSquad>()
            .WithNone<SquadMoveOverrideTag, BrokenSquadTag, RangedSquad>()
            .WithEntityAccess())
        {
            if (!threatened.Contains(entity)) continue;
            SetStance(ref state, units, true);
            ecb.AddComponent<BraceStanceTag>(entity);
        }

        foreach (var (units, entity) in SystemAPI.Query<DynamicBuffer<EntityReferenceBufferElement>>()
            .WithAll<BraceStanceTag>()
            .WithEntityAccess())
        {
            bool inCombat = SystemAPI.HasComponent<InCombat>(entity);
            if (!inCombat && threatened.Contains(entity)) continue;

            // Combat sets and clears the stance itself, so only a threat that passed is undone here.
            if (!inCombat) SetStance(ref state, units, false);
            ecb.RemoveComponent<BraceStanceTag>(entity);
        }

        threatened.Dispose();
    }

    void SetStance(ref SystemState state, DynamicBuffer<EntityReferenceBufferElement> units, bool ready)
    {
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Entity;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<AnimationDataHolder>(unit)) continue;

            RefRW<AnimationDataHolder> holder = SystemAPI.GetComponentRW<AnimationDataHolder>(unit);
            int from = holder.ValueRO.currentIdleAnimationId;
            int to = ready ? holder.ValueRO.attackIdleAnimationId : holder.ValueRO.idleAnimationId;
            if (from == to) continue;
            holder.ValueRW.currentIdleAnimationId = to;

            Entity model = holder.ValueRO.gpuEcsAnimatorEntity;
            if (!SystemAPI.HasComponent<GpuAnimControl>(model)) continue;
            RefRW<GpuAnimControl> control = SystemAPI.GetComponentRW<GpuAnimControl>(model);
            // A model that is walking or mid fidget picks the new idle up when it next returns to idle.
            if (control.ValueRO.Slot == from) control.ValueRW.Slot = to;
        }
    }
}
