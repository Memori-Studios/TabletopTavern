using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Turns the hover and select events on a unit into rendering-layer bits on every mesh under its
/// animator entity, so UnitOutlineFeature can draw the outline for exactly those meshes.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(HoveredEventSystem))]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
partial struct UnitOutlineSystem : ISystem
{
    private EntityQuery _unitQuery;

    public void OnCreate(ref SystemState state)
    {
        _unitQuery = SystemAPI.QueryBuilder().WithAll<Hovered, AnimationDataHolder, Unit>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
#if !SPELLS
        return;
#endif
        // Units vanish without events on scene unload, so the counts restart with the next battle.
        if (_unitQuery.IsEmptyIgnoreFilter)
        {
            UnitOutlineState.Reset();
            return;
        }

        NativeList<Entity> roots = new(Allocator.Temp);
        NativeList<uint> setBits = new(Allocator.Temp);
        NativeList<uint> clearBits = new(Allocator.Temp);

        foreach ((RefRO<Hovered> hovered, RefRO<AnimationDataHolder> anim, RefRO<Unit> unit)
            in SystemAPI.Query<RefRO<Hovered>, RefRO<AnimationDataHolder>, RefRO<Unit>>())
        {
            Hovered h = hovered.ValueRO;
            if (!h.onHover && !h.onUnhover && !h.onSelected && !h.onDeselected) continue;

            uint hoverBit = unit.ValueRO.Team == Team.Player
                ? TabletopTavernConstants.OUTLINE_LAYER_HOVER_PLAYER
                : TabletopTavernConstants.OUTLINE_LAYER_HOVER_ENEMY;

            uint set = 0, clear = 0;
            if (h.onHover) set |= hoverBit;
            if (h.onUnhover) clear |= hoverBit;
            if (h.onSelected) set |= TabletopTavernConstants.OUTLINE_LAYER_SELECTED;
            if (h.onDeselected) clear |= TabletopTavernConstants.OUTLINE_LAYER_SELECTED;

            roots.Add(anim.ValueRO.gpuEcsAnimatorEntity);
            setBits.Add(set);
            clearBits.Add(clear);
        }

        using NativeArray<Entity> corpses = SystemAPI.QueryBuilder().WithAll<UnitOutlineClearTag>().Build().ToEntityArray(Allocator.Temp);
        for (int i = 0; i < corpses.Length; i++)
        {
            roots.Add(corpses[i]);
            setBits.Add(0);
            clearBits.Add(TabletopTavernConstants.OUTLINE_LAYER_ALL);
        }

        // Shared-component writes are structural, so they run after the query loops have finished.
        NativeList<Entity> stack = new(Allocator.Temp);
        for (int i = 0; i < roots.Length; i++)
        {
            if (!state.EntityManager.Exists(roots[i])) continue;
            if (state.EntityManager.HasComponent<UnitOutlineClearTag>(roots[i]))
                state.EntityManager.RemoveComponent<UnitOutlineClearTag>(roots[i]);
            ApplyToSubtree(ref state, roots[i], setBits[i], clearBits[i], stack);
        }
    }

    private static void ApplyToSubtree(ref SystemState state, Entity root, uint set, uint clear, NativeList<Entity> stack)
    {
        bool counted = false;
        stack.Clear();
        stack.Add(root);

        while (stack.Length > 0)
        {
            Entity entity = stack[stack.Length - 1];
            stack.RemoveAt(stack.Length - 1);

            if (state.EntityManager.HasComponent<RenderFilterSettings>(entity))
            {
                RenderFilterSettings filter = state.EntityManager.GetSharedComponent<RenderFilterSettings>(entity);
                uint mask = (filter.RenderingLayerMask & ~clear) | set;
                if (!counted)
                {
                    UnitOutlineState.Track(filter.RenderingLayerMask, mask);
                    counted = true;
                }
                if (mask != filter.RenderingLayerMask)
                {
                    filter.RenderingLayerMask = mask;
                    state.EntityManager.SetSharedComponent(entity, filter);
                }
            }

            if (state.EntityManager.HasBuffer<Child>(entity))
            {
                DynamicBuffer<Child> children = state.EntityManager.GetBuffer<Child>(entity, true);
                for (int c = 0; c < children.Length; c++) stack.Add(children[c].Value);
            }
        }
    }
}
