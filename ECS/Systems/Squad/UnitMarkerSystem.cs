using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the chevron mesh once and, every frame, gathers one instance per unit with an active
/// TriangleEntity into UnitMarkerState.Units. UnitOutlineFeature draws them GPU-instanced.
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
    private const float GroundOffset = 0.02f;
    private const int CapSegments = 10;

    private Mesh _mesh;

    protected override void OnCreate()
    {
        _mesh = BuildChevron();
        UnitMarkerState.Mesh = _mesh;
        UnitMarkerState.Units = new NativeList<UnitMarkerInstance>(1024, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        UnitMarkerState.Mesh = null;
        if (UnitMarkerState.Units.IsCreated) UnitMarkerState.Units.Dispose();
        UnitMarkerState.Preview.Clear();
        if (_mesh != null) Object.Destroy(_mesh);
    }

    protected override void OnUpdate()
    {
        NativeList<UnitMarkerInstance> units = UnitMarkerState.Units;
        units.Clear();
        new GatherMarkersJob { Instances = units, GroundOffset = GroundOffset }.Run();
    }

    [BurstCompile]
    private partial struct GatherMarkersJob : IJobEntity
    {
        public NativeList<UnitMarkerInstance> Instances;
        public float GroundOffset;

        private void Execute(in TriangleEntity marker, in LocalTransform transform)
        {
            Color c = marker.activeColor;
            Instances.Add(new UnitMarkerInstance
            {
                Position = new float4(transform.Position + new float3(0f, GroundOffset, 0f), 0f),
                Rotation = transform.Rotation.value,
                Color = new float4(c.r, c.g, c.b, c.a),
            });
        }
    }

    #region Chevron mesh

    // Each vertex stores the outline point it belongs to (position) and the unit direction it is pushed
    // out along (normal). The shader applies the half thickness, so the line can never get thinner
    // than a set number of pixels on screen. Cap centres carry a zero direction.
    private static Mesh BuildChevron()
    {
        List<Vector3> vertices = new();
        List<Vector3> directions = new();
        List<int> indices = new();
        float2[] outline =
        {
            new(0f, TipForward),
            new(ArmHalfWidth, -ArmBack),
            new(0f, NotchForward),
            new(-ArmHalfWidth, -ArmBack),
        };

        for (int i = 0; i < 4; i++)
        {
            int n = (i + 1) % 4;
            float2 dir = math.normalize(outline[n] - outline[i]);
            float2 normal = new(dir.y, -dir.x);
            int first = vertices.Count;
            Add(vertices, directions, outline[i], normal);
            Add(vertices, directions, outline[n], normal);
            Add(vertices, directions, outline[n], -normal);
            Add(vertices, directions, outline[i], -normal);
            indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
            indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
        }

        for (int i = 0; i < 4; i++)
        {
            int centre = vertices.Count;
            Add(vertices, directions, outline[i], float2.zero);
            for (int k = 0; k < CapSegments; k++)
            {
                float angle = k * (2f * math.PI / CapSegments);
                Add(vertices, directions, outline[i], new float2(math.cos(angle), math.sin(angle)));
            }
            for (int k = 0; k < CapSegments; k++)
            {
                indices.Add(centre);
                indices.Add(centre + 1 + k);
                indices.Add(centre + 1 + (k + 1) % CapSegments);
            }
        }

        Mesh mesh = new() { name = "UnitMarkerChevron" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(directions);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        mesh.UploadMeshData(true);
        return mesh;
    }

    private static void Add(List<Vector3> vertices, List<Vector3> directions, float2 point, float2 direction)
    {
        vertices.Add(new Vector3(point.x, 0f, point.y));
        directions.Add(new Vector3(direction.x, 0f, direction.y));
    }

    #endregion
}
