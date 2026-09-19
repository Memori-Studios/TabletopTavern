using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds one mesh of arrowhead outlines, one per unit with an active TriangleEntity, at the unit's
/// feet pointing where it faces. UnitOutlineFeature draws it under the models and over the grass.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(TriangleColorLerpSystem))]
public partial class UnitMarkerSystem : SystemBase
{
    // Arrowhead outline in unit space: tip forward, two back corners, a notch on the axis between them.
    private const float TipForward = 0.5f;
    private const float ArmBack = 0.35f;
    private const float ArmHalfWidth = 0.42f;
    private const float NotchForward = -0.22f;
    private const float LineThickness = 0.075f;
    private const float GroundOffset = 0.02f;

    private readonly float2[] _outline = new float2[4];
    private readonly float2[] _left = new float2[4];
    private readonly float2[] _right = new float2[4];

    private readonly List<Vector3> _vertices = new(4096);
    private readonly List<Color> _colors = new(4096);
    private readonly List<int> _indices = new(6144);
    private Mesh _mesh;

    protected override void OnCreate()
    {
        _mesh = new Mesh { name = "UnitMarkers", indexFormat = IndexFormat.UInt32 };
        _mesh.MarkDynamic();
        UnitMarkerState.Mesh = _mesh;
    }

    protected override void OnDestroy()
    {
        UnitMarkerState.Mesh = null;
        UnitMarkerState.Count = 0;
        if (_mesh != null) Object.Destroy(_mesh);
    }

    protected override void OnUpdate()
    {
        _vertices.Clear();
        _colors.Clear();
        _indices.Clear();
        int count = 0;

        foreach ((RefRO<TriangleEntity> marker, RefRO<LocalTransform> transform)
            in SystemAPI.Query<RefRO<TriangleEntity>, RefRO<LocalTransform>>())
        {
            float3 origin = transform.ValueRO.Position + new float3(0f, GroundOffset, 0f);
            AppendArrow(origin, transform.ValueRO.Rotation, marker.ValueRO.activeColor);
            count++;
        }

        _mesh.Clear(true);
        if (count > 0)
        {
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0, false);
            // Drawn straight from a command buffer, so the bounds only need to never cull it.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }
        UnitMarkerState.Count = count;
    }

    // Strokes the closed arrowhead: offset every edge by half the thickness on both sides and miter the
    // four corners, so the outer V and the inner V meet at the back corners with no gap or overlap.
    private void AppendArrow(float3 origin, quaternion rotation, Color color)
    {
        _outline[0] = new float2(0f, TipForward);
        _outline[1] = new float2(ArmHalfWidth, -ArmBack);
        _outline[2] = new float2(0f, NotchForward);
        _outline[3] = new float2(-ArmHalfWidth, -ArmBack);

        for (int i = 0; i < 4; i++)
        {
            float2 previous = _outline[(i + 3) % 4];
            float2 current = _outline[i];
            float2 next = _outline[(i + 1) % 4];
            float2 inDir = math.normalize(current - previous);
            float2 outDir = math.normalize(next - current);
            float2 inNormal = new(inDir.y, -inDir.x);
            float2 outNormal = new(outDir.y, -outDir.x);
            float half = LineThickness * 0.5f;
            _left[i] = Intersect(previous + inNormal * half, inDir, current + outNormal * half, outDir);
            _right[i] = Intersect(previous - inNormal * half, inDir, current - outNormal * half, outDir);
        }

        for (int i = 0; i < 4; i++)
        {
            int n = (i + 1) % 4;
            int first = _vertices.Count;
            _vertices.Add(ToWorld(origin, rotation, _left[i]));
            _vertices.Add(ToWorld(origin, rotation, _left[n]));
            _vertices.Add(ToWorld(origin, rotation, _right[n]));
            _vertices.Add(ToWorld(origin, rotation, _right[i]));
            for (int v = 0; v < 4; v++) _colors.Add(color);
            _indices.Add(first); _indices.Add(first + 1); _indices.Add(first + 2);
            _indices.Add(first); _indices.Add(first + 2); _indices.Add(first + 3);
        }
    }

    private static float2 Intersect(float2 a, float2 aDir, float2 b, float2 bDir)
    {
        float cross = aDir.x * bDir.y - aDir.y * bDir.x;
        float2 delta = b - a;
        float t = (delta.x * bDir.y - delta.y * bDir.x) / cross;
        return a + aDir * t;
    }

    private static Vector3 ToWorld(float3 origin, quaternion rotation, float2 local)
    {
        return origin + math.mul(rotation, new float3(local.x, 0f, local.y));
    }
}
