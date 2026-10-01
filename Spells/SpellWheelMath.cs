using UnityEngine;

namespace TJ.Spells
{
/// <summary>Where the cursor points on the spell wheel, measured from the point the key went down.</summary>
public enum WheelDirection { Centre, Left, Up, Right, Down }

public static class SpellWheelMath
{
    /// <summary>
    /// Four 90-degree slices split on the diagonals; inside the hub is Centre. Screen y points up.
    /// An exact diagonal goes to Up or Down, so every offset has one answer.
    /// </summary>
    public static WheelDirection Pick(Vector2 offset, float hubRadius)
    {
        if(offset.sqrMagnitude <= hubRadius * hubRadius) return WheelDirection.Centre;
        if(Mathf.Abs(offset.x) > Mathf.Abs(offset.y)) return offset.x < 0f ? WheelDirection.Left : WheelDirection.Right;
        return offset.y > 0f ? WheelDirection.Up : WheelDirection.Down;
    }

    /// <summary>The hotbar slot a direction arms: left 0, up 1, right 2. -1 for Centre and Down.</summary>
    public static int SlotOf(WheelDirection direction) => direction switch
    {
        WheelDirection.Left => 0,
        WheelDirection.Up => 1,
        WheelDirection.Right => 2,
        _ => -1
    };

    /// <summary>The direction that arms a hotbar slot, the inverse of SlotOf.</summary>
    public static WheelDirection DirectionOf(int slot) => slot switch
    {
        0 => WheelDirection.Left,
        1 => WheelDirection.Up,
        2 => WheelDirection.Right,
        _ => WheelDirection.Centre
    };

    /// <summary>
    /// Moves the wheel's centre inward until a wheel of <paramref name="radius"/> fits on screen, so every
    /// direction stays reachable when the key goes down against an edge.
    /// </summary>
    public static Vector2 ClampInside(Vector2 point, float radius, Vector2 screenSize)
    {
        float x = screenSize.x <= radius * 2f ? screenSize.x * 0.5f : Mathf.Clamp(point.x, radius, screenSize.x - radius);
        float y = screenSize.y <= radius * 2f ? screenSize.y * 0.5f : Mathf.Clamp(point.y, radius, screenSize.y - radius);
        return new Vector2(x, y);
    }
}
}
