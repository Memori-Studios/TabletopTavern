using Memori.Utilities;
using Unity.Entities;
using UnityEngine;

/// <summary>Chevron colours per team; alpha carries the bloom strength the marker shader reads.</summary>
public static class TeamMarkerColors
{
    public static Color Hover(Team team) => WithBloom(Base(team), TabletopTavernConstants.TRIANGLE_HOVER_BLOOM);
    public static Color Selected(Team team) => WithBloom(Base(team), TabletopTavernConstants.TRIANGLE_SELECTED_BLOOM);

    private static Color Base(Team team) => team == Team.Player
        ? ColorVision.Good(TabletopTavernConstants.PLAYER_TRIANGLE_COLOR)
        : ColorVision.Bad(TabletopTavernConstants.ENEMY_TRIANGLE_COLOR);

    private static Color WithBloom(Color color, float bloom)
    {
        color.a = bloom;
        return color;
    }
}

/// <summary>Chevron colours are copied onto each soldier at spawn, so a mid-battle Colorblind Mode change rewrites them, hidden markers included.</summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(HoveredEventSystem))]
public partial class TeamMarkerColorSystem : SystemBase
{
    private bool _dirty;

    protected override void OnCreate()
    {
        ColorVision.Changed += MarkDirty;
    }

    protected override void OnDestroy()
    {
        ColorVision.Changed -= MarkDirty;
    }

    private void MarkDirty() => _dirty = true;

    protected override void OnUpdate()
    {
        if (!_dirty) return;
        _dirty = false;

        foreach ((RefRW<TriangleEntity> triangle, RefRO<Unit> unit) in SystemAPI.Query<RefRW<TriangleEntity>, RefRO<Unit>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
        {
            Color oldHover = triangle.ValueRO.hoverColor;
            Color oldSelected = triangle.ValueRO.selectedColor;
            Color newHover = TeamMarkerColors.Hover(unit.ValueRO.Team);
            Color newSelected = TeamMarkerColors.Selected(unit.ValueRO.Team);

            triangle.ValueRW.hoverColor = newHover;
            triangle.ValueRW.selectedColor = newSelected;
            triangle.ValueRW.colorTarget = Remap(triangle.ValueRO.colorTarget, oldHover, oldSelected, newHover, newSelected);
            triangle.ValueRW.activeColor = Remap(triangle.ValueRO.activeColor, oldHover, oldSelected, newHover, newSelected);
        }
    }

    // Hover and selected differ in alpha (bloom), so an exact match tells which one a marker is showing.
    private static Color Remap(Color current, Color oldHover, Color oldSelected, Color newHover, Color newSelected)
    {
        if (current == oldSelected) return newSelected;
        if (current == oldHover) return newHover;
        return current;
    }
}
