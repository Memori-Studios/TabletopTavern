using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TabletopTavern.GpuAnim
{
    /// <summary>Owns everything one built visual registered or allocated, so a battle can drop it in one call.</summary>
    public sealed class GpuAnimVisualHandle
    {
        public Entity Prefab;
        public Entity RiderPrefab;
        public BlobAssetReference<GpuAnimAnchorBlob> Anchors;
        public BlobAssetReference<GpuAnimAnchorBlob> RiderAnchors;
        public readonly List<BatchMeshID> Meshes = new();
        public readonly List<BatchMaterialID> Materials = new();
        public readonly List<Object> OwnedObjects = new();

        public void Dispose(EntityManager em)
        {
            if (em.Exists(Prefab)) em.DestroyEntity(Prefab);
            if (em.Exists(RiderPrefab)) em.DestroyEntity(RiderPrefab);
            Prefab = Entity.Null;
            RiderPrefab = Entity.Null;
            var egs = em.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (egs != null)
            {
                foreach (BatchMeshID id in Meshes) egs.UnregisterMesh(id);
                foreach (BatchMaterialID id in Materials) egs.UnregisterMaterial(id);
            }
            Meshes.Clear();
            Materials.Clear();
            if (Anchors.IsCreated) Anchors.Dispose();
            if (RiderAnchors.IsCreated) RiderAnchors.Dispose();
            foreach (Object o in OwnedObjects) if (o != null) Object.Destroy(o);
            OwnedObjects.Clear();
        }
    }

    /// <summary>Builds the entity tree the unit visual contract describes, as a prefab entity the spawn code instantiates.</summary>
    public static class GpuAnimVisualBuilder
    {
        public delegate void RoleMarker(EntityManager em, Entity entity, GpuAnimPropRole role);

        // Matches the baked renderers: layer Default, rendering layer 1, object motion vectors.
        private static RenderMeshDescription BodyDescription => new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true,
            motionVectorGenerationMode: MotionVectorGenerationMode.Object, layer: 0, renderingLayerMask: 1);

        public static GpuAnimVisualHandle Build(EntityManager em, GpuAnimVisualData data, int variantIndex, RoleMarker markRole)
        {
            var handle = new GpuAnimVisualHandle();
            handle.Prefab = BuildOne(em, data, variantIndex, markRole, handle, out handle.Anchors);
            if (data.Rider != null)
                handle.RiderPrefab = BuildOne(em, data.Rider, 0, markRole, handle, out handle.RiderAnchors);
            return handle;
        }

        private static Entity BuildOne(EntityManager em, GpuAnimVisualData data, int variantIndex, RoleMarker markRole, GpuAnimVisualHandle handle,
            out BlobAssetReference<GpuAnimAnchorBlob> anchorBlob)
        {
            var egs = em.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (egs == null) throw new System.InvalidOperationException("No EntitiesGraphicsSystem in this world.");
            if (data.Variants.Count == 0) throw new System.ArgumentException("A visual needs at least one variant.");
            GpuAnimVariantData variant = data.Variants[math.clamp(variantIndex, 0, data.Variants.Count - 1)];
            if (variant.Lods.Count == 0 || variant.Lods[0].Meshes.Count == 0) throw new System.ArgumentException("A variant needs a LOD0 with a mesh.");

            anchorBlob = BuildAnchorBlob(data);

            Entity root = em.CreateEntity();
            em.AddComponent<Prefab>(root);
            em.AddComponentData(root, LocalTransform.Identity);
            em.AddComponentData(root, new LocalToWorld { Value = float4x4.identity });
            em.AddComponentData(root, GpuAnimControl.Default);
            em.AddComponentData(root, new GpuAnimRestart());
            em.AddComponentData(root, new GpuAnimSlotCount { Value = data.Slots.Count });
            em.AddComponentData(root, GpuAnimState.Default);
            em.AddComponentData(root, new GpuAnimInfo { BoneCount = data.BoneCount, FrameCount = data.FrameCount });
            em.AddComponentData(root, new GpuAnimAnchors { Blob = anchorBlob });
            DynamicBuffer<GpuAnimSlot> slots = em.AddBuffer<GpuAnimSlot>(root);
            foreach (GpuAnimSlot slot in data.Slots) slots.Add(slot);

            var linked = new List<Entity> { root };
            int lodCount = variant.Lods.Count;
            bool useLod = lodCount > 1;
            if (useLod) em.AddComponentData(root, LodGroup(variant, data.LodSwitch));

            var materialIds = new BatchMaterialID[data.SkinnedMaterials.Length];
            for (int i = 0; i < materialIds.Length; i++) materialIds[i] = Register(egs, data.SkinnedMaterials[i], handle);
            var rigidIds = new BatchMaterialID[data.RigidMaterials.Length];
            for (int i = 0; i < rigidIds.Length; i++) rigidIds[i] = Register(egs, data.RigidMaterials[i], handle);

            for (int lod = 0; lod < lodCount; lod++)
            {
                foreach (GpuAnimMeshData meshData in variant.Lods[lod].Meshes)
                {
                    Entity mesh = em.CreateEntity();
                    em.AddComponent<Prefab>(mesh);
                    em.AddComponentData(mesh, LocalTransform.Identity);
                    em.AddComponentData(mesh, new LocalToWorld { Value = float4x4.identity });
                    em.AddComponentData(mesh, new Parent { Value = root });
                    em.AddComponentData(mesh, new GpuAnimMeshLink { Animator = root });
                    em.AddComponentData(mesh, new GpuAnimCurProperty());
                    em.AddComponentData(mesh, new GpuAnimPrevProperty());
                    AddRender(em, egs, handle, mesh, meshData.Mesh, materialIds[Index(meshData.MaterialIndex, materialIds.Length)], meshData.Bounds);
                    if (useLod) em.AddComponentData(mesh, new MeshLODComponent { Group = root, ParentGroup = Entity.Null, LODMask = 1 << lod });
                    linked.Add(mesh);
                }
            }

            foreach (GpuAnimAttachmentData att in variant.Attachments)
            {
                Entity follower = em.CreateEntity();
                em.AddComponent<Prefab>(follower);
                em.AddComponentData(follower, LocalTransform.Identity);
                em.AddComponentData(follower, new LocalToWorld { Value = float4x4.identity });
                em.AddComponentData(follower, new Parent { Value = root });
                em.AddComponentData(follower, new GpuAnimAttachment { Animator = root, AnchorIndex = att.AnchorIndex });
                em.AddComponentData(follower, new GpuAnimRole { Value = att.Role });
                linked.Add(follower);

                // Bow and sword keep their mesh one level down so the set-up systems find them three hops from the unit root.
                Entity meshHolder = follower;
                if (att.Role == GpuAnimPropRole.Bow || att.Role == GpuAnimPropRole.Sword)
                {
                    meshHolder = em.CreateEntity();
                    em.AddComponent<Prefab>(meshHolder);
                    em.AddComponentData(meshHolder, LocalTransform.Identity);
                    em.AddComponentData(meshHolder, new LocalToWorld { Value = float4x4.identity });
                    em.AddComponentData(meshHolder, new Parent { Value = follower });
                    em.AddComponentData(meshHolder, new GpuAnimRole { Value = att.Role });
                    linked.Add(meshHolder);
                }
                AddRender(em, egs, handle, meshHolder, att.Mesh, rigidIds[Index(att.MaterialIndex, rigidIds.Length)], att.Bounds);
                if (useLod) em.AddComponentData(meshHolder, new MeshLODComponent { Group = root, ParentGroup = Entity.Null, LODMask = att.LodMask });
                markRole?.Invoke(em, meshHolder, att.Role);
            }

            DynamicBuffer<LinkedEntityGroup> group = em.AddBuffer<LinkedEntityGroup>(root);
            foreach (Entity e in linked) group.Add(new LinkedEntityGroup { Value = e });
            return root;
        }

        private static int Index(int wanted, int count) => count == 0 ? 0 : math.clamp(wanted, 0, count - 1);

        private static BatchMaterialID Register(EntitiesGraphicsSystem egs, Material material, GpuAnimVisualHandle handle)
        {
            BatchMaterialID id = egs.RegisterMaterial(material);
            handle.Materials.Add(id);
            return id;
        }

        private static void AddRender(EntityManager em, EntitiesGraphicsSystem egs, GpuAnimVisualHandle handle, Entity entity, Mesh mesh, BatchMaterialID material, Bounds bounds)
        {
            BatchMeshID meshId = egs.RegisterMesh(mesh);
            handle.Meshes.Add(meshId);
            RenderMeshUtility.AddComponents(entity, em, BodyDescription, new MaterialMeshInfo(material, meshId));
            // AddComponents writes the bind-pose box; the animation-inclusive one goes on top so culling never clips a pose.
            em.SetComponentData(entity, new RenderBounds { Value = new AABB { Center = bounds.center, Extents = bounds.extents } });
        }

        // Entities Graphics stores the far edge of each LOD as a distance: size / screen height, as its LODGroup baker does.
        private static MeshLODGroupComponent LodGroup(GpuAnimVariantData variant, float[] lodSwitch)
        {
            Bounds union = variant.Lods[0].Meshes[0].Bounds;
            foreach (GpuAnimMeshData m in variant.Lods[0].Meshes) union.Encapsulate(m.Bounds);
            float size = math.cmax(union.size);
            var d0 = new float4(float.PositiveInfinity);
            for (int i = 0; i < variant.Lods.Count - 1 && i < 4; i++)
            {
                float h = i < lodSwitch.Length ? lodSwitch[i] : 0.01f;
                d0[i] = size / math.max(h, 0.0001f);
            }
            return new MeshLODGroupComponent
            {
                ParentGroup = Entity.Null,
                ParentMask = 0,
                LODDistances0 = d0,
                LODDistances1 = new float4(float.PositiveInfinity),
                LocalReferencePoint = union.center,
            };
        }

        private static BlobAssetReference<GpuAnimAnchorBlob> BuildAnchorBlob(GpuAnimVisualData data)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref GpuAnimAnchorBlob blob = ref builder.ConstructRoot<GpuAnimAnchorBlob>();
            int count = data.AnchorCount;
            int frames = count > 0 ? data.AnchorMatrices.Length / count : 0;
            blob.AnchorCount = count;
            blob.FrameCount = frames;
            BlobBuilderArray<float3x4> array = builder.Allocate(ref blob.Matrices, frames * count);
            for (int i = 0; i < frames * count; i++) array[i] = data.AnchorMatrices[i];
            return builder.CreateBlobAssetReference<GpuAnimAnchorBlob>(Allocator.Persistent);
        }
    }
}
