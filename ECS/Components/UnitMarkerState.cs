using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// One ground marker: where it sits, which way it points, and its colour with the bloom in alpha.
/// Layout is mirrored by the StructuredBuffer in TTUnitMarker.shader.
/// </summary>
public struct UnitMarkerInstance
{
    public float4 Position;
    public float4 Rotation;
    public float4 Color;

    public const int Stride = 48;
}

/// <summary>
/// The shared marker mesh and the instances UnitOutlineFeature draws each frame: the ECS units
/// (rebuilt by UnitMarkerSystem) plus the placement preview (filled by PositionDrawer).
/// </summary>
public static class UnitMarkerState
{
    // World-space line thickness; the shader widens it when that would fall under the minimum pixel width.
    public const float LineThickness = 0.075f;

    public static Mesh Mesh;
    public static NativeList<UnitMarkerInstance> Units;
    public static readonly List<UnitMarkerInstance> Preview = new();

    public static int UnitCount => Units.IsCreated ? Units.Length : 0;
    public static int Count => UnitCount + Preview.Count;
}
