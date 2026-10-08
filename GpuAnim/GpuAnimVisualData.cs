using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TabletopTavern.GpuAnim
{
    /// <summary>Everything the builder needs for one visual, already turned into engine objects. The file reader fills it; tests fill it by hand.</summary>
    public sealed class GpuAnimVisualData
    {
        public string Name;
        public int BoneCount;
        public int FrameCount;
        public float Fps;
        public List<GpuAnimSlot> Slots = new();
        // RGBAHalf, width BoneCount * 3, height FrameCount, point sampled.
        public Texture2D BoneTexture;
        public int AnchorCount;
        // Frame-major: index = frame * AnchorCount + anchor. Empty when there are no anchors.
        public float3x4[] AnchorMatrices = System.Array.Empty<float3x4>();
        // Skinned (TJBake/Unit with _BoneTex) and rigid (URP Lit) material per unit.json material entry.
        public Material[] SkinnedMaterials = System.Array.Empty<Material>();
        public Material[] RigidMaterials = System.Array.Empty<Material>();
        public List<GpuAnimVariantData> Variants = new();
        // Screen-height fractions below which the next LOD shows; one fewer than the LOD count.
        public float[] LodSwitch = System.Array.Empty<float>();
        public GpuAnimVisualData Rider;
    }

    public sealed class GpuAnimVariantData
    {
        public List<GpuAnimLodData> Lods = new();
        public List<GpuAnimAttachmentData> Attachments = new();
    }

    public sealed class GpuAnimLodData
    {
        public List<GpuAnimMeshData> Meshes = new();
    }

    public sealed class GpuAnimMeshData
    {
        public Mesh Mesh;
        public int MaterialIndex;
        // Animation-inclusive, in model space.
        public Bounds Bounds;
    }

    /// <summary>Attachment tags the game's own bakes write and the battle reads.</summary>
    public static class UnitVisualTags
    {
        // ShieldRandomizerSystem swaps this prop's mesh for a random one from the shield set.
        public const string RandomShield = "randomShield";
    }

    public sealed class GpuAnimAttachmentData
    {
        public Mesh Mesh;
        public int MaterialIndex;
        public int AnchorIndex;
        public GpuAnimPropRole Role;
        // Bit i set = shown at LOD i.
        public int LodMask = 0b11;
        public Bounds Bounds;
        public string Tag = "";
    }
}
