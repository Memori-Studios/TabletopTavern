using MemoriStudios.TJBake;

namespace TabletopTavern.GpuAnim
{
    /// <summary>Maps a loaded TJBake visual onto the game's builder input. The package knows no ECS types.</summary>
    public static class GpuAnimVisualImport
    {
        public static GpuAnimVisualData From(TJBakeVisual v)
        {
            var data = new GpuAnimVisualData
            {
                Name = v.Name, BoneCount = v.BoneCount, FrameCount = v.FrameCount, Fps = v.Fps,
                BoneTexture = v.BoneTexture, AnchorCount = v.AnchorCount, AnchorMatrices = v.AnchorMatrices,
                SkinnedMaterials = v.SkinnedMaterials, RigidMaterials = v.RigidMaterials, LodSwitch = v.LodSwitch,
            };
            foreach (TJBakeSlot s in v.Slots)
                data.Slots.Add(new GpuAnimSlot { Start = s.Start, Count = s.Count, Fps = s.Fps, Loop = s.Loop, ReturnToIdle = s.ReturnToIdle, ReturnAt = s.ReturnAt });
            foreach (TJBakeVariant variant in v.Variants)
            {
                var vd = new GpuAnimVariantData();
                foreach (TJBakeLod lod in variant.Lods)
                {
                    var ld = new GpuAnimLodData();
                    foreach (TJBakeMeshRef m in lod.Meshes) ld.Meshes.Add(new GpuAnimMeshData { Mesh = m.Mesh, MaterialIndex = m.MaterialIndex, Bounds = m.Bounds });
                    vd.Lods.Add(ld);
                }
                foreach (TJBakeAttachment a in variant.Attachments)
                    vd.Attachments.Add(new GpuAnimAttachmentData { Mesh = a.Mesh, MaterialIndex = a.MaterialIndex, AnchorIndex = a.AnchorIndex, Role = (GpuAnimPropRole)(byte)a.Role, LodMask = a.LodMask, Bounds = a.Bounds });
                data.Variants.Add(vd);
            }
            if (v.Rider != null) data.Rider = From(v.Rider);
            return data;
        }
    }
}
