using System.Collections.Generic;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TJ.Spells
{
    // The pieces of a Barricades line and which of them would land on a unit, shared by the placement preview and the cast so both agree.
    public static class BarricadeLine
    {
        // Pieces sit end to end along the line's local X, centred on the line's centre.
        public static void Layout(Vector3 centre, Quaternion rotation, int count, float pieceLength, List<Vector3> positions)
        {
            positions.Clear();
            Vector3 along = rotation * Vector3.right;
            for (int i = 0; i < count; i++)
                positions.Add(centre + along * ((i - (count - 1) * 0.5f) * pieceLength));
        }

        // A piece is occupied when any unit's body overlaps its footprint; carving there would pop the unit to either side.
        public static void MarkOccupied(List<Vector3> positions, Quaternion rotation, float pieceLength, float pieceDepth, List<bool> occupied)
        {
            occupied.Clear();
            for (int i = 0; i < positions.Count; i++) occupied.Add(false);
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || positions.Count == 0) return;

            EntityManager entityManager = world.EntityManager;
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Unit>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<AgentShape>());
            using NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<AgentShape> shapes = query.ToComponentDataArray<AgentShape>(Allocator.Temp);

            Quaternion toLocal = Quaternion.Inverse(rotation);
            float halfLength = pieceLength * 0.5f;
            float halfDepth = pieceDepth * 0.5f;
            for (int u = 0; u < transforms.Length; u++)
            {
                float3 position = transforms[u].Position;
                float radius = shapes[u].Radius;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (occupied[i]) continue;
                    Vector3 local = toLocal * (new Vector3(position.x, 0f, position.z) - new Vector3(positions[i].x, 0f, positions[i].z));
                    if (Mathf.Abs(local.x) < halfLength + radius && Mathf.Abs(local.z) < halfDepth + radius) occupied[i] = true;
                }
            }
        }
    }
}
