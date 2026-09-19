using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Outlines hovered and selected units. UnitOutlineSystem marks their meshes with rendering-layer
/// bits; this feature draws those meshes' depth into a private texture per state and composites a
/// screen-space edge of that depth onto the camera colour, hidden wherever the scene is nearer.
/// </summary>
public class UnitOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader outlineShader;
    [Range(1f, 6f)]
    [SerializeField] private float widthPixels = 2f;
    [Tooltip("Depth ratio between neighbouring soldiers that draws a line between them. 0 = silhouette only.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float interiorEdgeRatio = 0.02f;
    [ColorUsage(false, true)]
    [SerializeField] private Color hoverPlayerColor = new(1f, 0.75f, 0f, 1f);
    [ColorUsage(false, true)]
    [SerializeField] private Color hoverEnemyColor = new(1f, 0f, 0f, 1f);
    [ColorUsage(false, true)]
    [SerializeField] private Color selectedColor = new(2f, 1.5f, 0f, 1f);

    private Material _material;
    private UnitOutlinePass _pass;

    public override void Create()
    {
        if (outlineShader == null) return;
        _material = CoreUtils.CreateEngineMaterial(outlineShader);
        _pass = new UnitOutlinePass(_material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        if (UnitOutlineState.ActiveMask == 0) return;

        _pass.Configure(widthPixels, interiorEdgeRatio, hoverPlayerColor, hoverEnemyColor, selectedColor);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (_pass != null) _pass.Dispose();
        CoreUtils.Destroy(_material);
    }

    private class UnitOutlinePass : ScriptableRenderPass
    {
        private class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        private class CompositePassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public TextureHandle outlineDepth;
        }

        private static readonly ShaderTagId DepthOnlyTag = new("DepthOnly");
        private static readonly int OutlineDepthId = Shader.PropertyToID("_UnitOutlineDepth");
        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int ColorId = Shader.PropertyToID("_UnitOutlineColor");
        private static readonly int ParamsId = Shader.PropertyToID("_UnitOutlineParams");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private readonly Material _material;
        // Selected is last so a hovered selected squad shows the selected colour.
        private readonly uint[] _bits =
        {
            TabletopTavernConstants.OUTLINE_LAYER_HOVER_PLAYER,
            TabletopTavernConstants.OUTLINE_LAYER_HOVER_ENEMY,
            TabletopTavernConstants.OUTLINE_LAYER_SELECTED,
        };
        private readonly Color[] _colors = new Color[3];
        private readonly MaterialPropertyBlock[] _properties = { new(), new(), new() };
        private float _widthPixels;
        private float _interiorEdgeRatio;
        private RTHandle _compatOutlineDepth;

        public UnitOutlinePass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Unit Outline");
        }

        public void Configure(float widthPixels, float interiorEdgeRatio, Color hoverPlayer, Color hoverEnemy, Color selected)
        {
            _widthPixels = widthPixels;
            _interiorEdgeRatio = interiorEdgeRatio;
            _colors[0] = hoverPlayer;
            _colors[1] = hoverEnemy;
            _colors[2] = selected;
        }

        public void Dispose()
        {
            _compatOutlineDepth?.Release();
            _compatOutlineDepth = null;
        }

        private static RenderTextureDescriptor OutlineDepthDescriptor(in RenderTextureDescriptor cameraDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            return descriptor;
        }

        private MaterialPropertyBlock FillProperties(int state, in RenderTextureDescriptor descriptor)
        {
            // Width is authored for 1080p so the line looks the same at every resolution.
            float width = _widthPixels * descriptor.height / 1080f;
            MaterialPropertyBlock properties = _properties[state];
            properties.SetColor(ColorId, _colors[state]);
            properties.SetVector(ParamsId, new Vector4(1f / descriptor.width, 1f / descriptor.height, width, _interiorEdgeRatio));
            properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            return properties;
        }

        #region Render Graph path

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            if (resourceData.isActiveTargetBackBuffer) return;
            if (!resourceData.cameraDepthTexture.IsValid()) return;

            uint active = UnitOutlineState.ActiveMask;
            if (active == 0) return;

            RenderTextureDescriptor descriptor = OutlineDepthDescriptor(cameraData.cameraTargetDescriptor);

            for (int i = 0; i < _bits.Length; i++)
            {
                if ((active & _bits[i]) == 0) continue;

                TextureHandle outlineDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_UnitOutlineDepth", true);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Outline Mask", out MaskPassData data, profilingSampler))
                {
                    DrawingSettings drawing = CreateDrawingSettings(DepthOnlyTag, renderingData, cameraData, lightData, SortingCriteria.None);
                    drawing.perObjectData = PerObjectData.None;
                    FilteringSettings filtering = new(RenderQueueRange.opaque, -1, _bits[i]);
                    RendererListParams listParams = new(renderingData.cullResults, drawing, filtering);
                    data.rendererList = renderGraph.CreateRendererList(listParams);

                    builder.UseRendererList(data.rendererList);
                    builder.SetRenderAttachmentDepth(outlineDepth, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaskPassData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(d.rendererList);
                    });
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Outline Composite", out CompositePassData data, profilingSampler))
                {
                    data.material = _material;
                    data.properties = FillProperties(i, descriptor);
                    data.outlineDepth = outlineDepth;

                    builder.UseTexture(outlineDepth, AccessFlags.Read);
                    builder.UseGlobalTexture(CameraDepthTextureId);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((CompositePassData d, RasterGraphContext context) =>
                    {
                        RTHandle outlineDepthHandle = d.outlineDepth;
                        d.properties.SetTexture(OutlineDepthId, outlineDepthHandle);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.properties);
                    });
                }
            }
        }

        #endregion

        #region Compatibility mode path

#pragma warning disable CS0672, CS0618
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = OutlineDepthDescriptor(renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateHandleIfNeeded(ref _compatOutlineDepth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_UnitOutlineDepth");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            uint active = UnitOutlineState.ActiveMask;
            if (active == 0) return;

            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            CommandBuffer cmd = CommandBufferPool.Get("Unit Outline");

            for (int i = 0; i < _bits.Length; i++)
            {
                if ((active & _bits[i]) == 0) continue;

                CoreUtils.SetRenderTarget(cmd, _compatOutlineDepth, ClearFlag.Depth);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawing = CreateDrawingSettings(DepthOnlyTag, ref renderingData, SortingCriteria.None);
                drawing.perObjectData = PerObjectData.None;
                FilteringSettings filtering = new(RenderQueueRange.opaque, -1, _bits[i]);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);

                MaterialPropertyBlock properties = FillProperties(i, descriptor);
                properties.SetTexture(OutlineDepthId, _compatOutlineDepth);
                // Rebinding both camera targets leaves URP's tracked attachments true for the next pass.
                RTHandle cameraDepth = renderer.cameraDepthTargetHandle;
                bool depthIsBackBuffer = cameraDepth == null || cameraDepth.nameID == new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
                if (depthIsBackBuffer)
                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, ClearFlag.None);
                else
                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, cameraDepth, ClearFlag.None);
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1, properties);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0672, CS0618

        #endregion
    }
}
