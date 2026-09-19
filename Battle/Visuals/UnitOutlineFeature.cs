using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws the unit outlines and ground markers. UnitOutlineSystem marks hovered and selected meshes
/// with rendering-layer bits; UnitMarkerSystem builds the chevron mesh. Both are hidden where a
/// model is nearer (a depth-only draw of the model layer) or the terrain is nearer than a grass
/// height, and both draw before transparents so the banner cloth covers them.
/// </summary>
public class UnitOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader outlineShader;
    [SerializeField] private Shader markerShader;
    [Tooltip("GameObject layers whose depth counts as 'a model'. Units, flagposts and props sit on Default; the grass is the ground mesh on Tile.")]
    [SerializeField] private LayerMask modelLayers = 1;
    [Tooltip("How far in front of an outline or marker the terrain may be before it hides them. Lets grass through, not hills.")]
    [SerializeField] private float grassHeight = 1.5f;
    [Range(1f, 6f)]
    [SerializeField] private float widthPixels = 2f;
    [Tooltip("Depth ratio between neighbouring soldiers that draws a line between them. 0 = silhouette only.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float interiorEdgeRatio = 0.02f;
    [ColorUsage(false, true)]
    [SerializeField] private Color hoverPlayerColor = new(1f, 0.89f, 0f, 1f);
    [ColorUsage(false, true)]
    [SerializeField] private Color hoverEnemyColor = new(1f, 0f, 0f, 1f);
    [ColorUsage(false, true)]
    [SerializeField] private Color selectedColor = new(1.15f, 1.02f, 0f, 1f);

    private Material _outlineMaterial;
    private Material _markerMaterial;
    private UnitOutlinePass _pass;

    public override void Create()
    {
        if (outlineShader == null || markerShader == null) return;
        _outlineMaterial = CoreUtils.CreateEngineMaterial(outlineShader);
        _markerMaterial = CoreUtils.CreateEngineMaterial(markerShader);
        _pass = new UnitOutlinePass(_outlineMaterial, _markerMaterial) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        // The outline ships with the Spell Update; the ground marker ships in every build.
        uint activeOutlines = UnitOutlineState.ActiveMask;
#if !SPELLS
        activeOutlines = 0;
#endif
        if (activeOutlines == 0 && UnitMarkerState.Count == 0) return;

        _pass.Configure(activeOutlines, modelLayers, grassHeight, widthPixels, interiorEdgeRatio, hoverPlayerColor, hoverEnemyColor, selectedColor);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (_pass != null) _pass.Dispose();
        CoreUtils.Destroy(_outlineMaterial);
        CoreUtils.Destroy(_markerMaterial);
    }

    private class UnitOutlinePass : ScriptableRenderPass
    {
        private class DepthPassData
        {
            public RendererListHandle rendererList;
        }

        private class CompositePassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public TextureHandle outlineDepth;
        }

        private class MarkerPassData
        {
            public Material material;
            public Mesh mesh;
        }

        private static readonly ShaderTagId DepthOnlyTag = new("DepthOnly");
        private static readonly int OutlineDepthId = Shader.PropertyToID("_UnitOutlineDepth");
        private static readonly int ModelDepthId = Shader.PropertyToID("_ModelDepth");
        private static readonly int GrassHeightId = Shader.PropertyToID("_UnitOcclusionGrassHeight");
        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int ColorId = Shader.PropertyToID("_UnitOutlineColor");
        private static readonly int ParamsId = Shader.PropertyToID("_UnitOutlineParams");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private readonly Material _outlineMaterial;
        private readonly Material _markerMaterial;
        // Selected is last so a hovered selected squad shows the selected colour.
        private readonly uint[] _bits =
        {
            TabletopTavernConstants.OUTLINE_LAYER_HOVER_PLAYER,
            TabletopTavernConstants.OUTLINE_LAYER_HOVER_ENEMY,
            TabletopTavernConstants.OUTLINE_LAYER_SELECTED,
        };
        private readonly Color[] _colors = new Color[3];
        private readonly MaterialPropertyBlock[] _properties = { new(), new(), new() };
        private uint _activeOutlines;
        private int _modelLayers;
        private float _grassHeight;
        private float _widthPixels;
        private float _interiorEdgeRatio;
        private RTHandle _compatOutlineDepth;
        private RTHandle _compatModelDepth;

        public UnitOutlinePass(Material outlineMaterial, Material markerMaterial)
        {
            _outlineMaterial = outlineMaterial;
            _markerMaterial = markerMaterial;
            profilingSampler = new ProfilingSampler("Unit Outline");
        }

        public void Configure(uint activeOutlines, LayerMask modelLayers, float grassHeight, float widthPixels, float interiorEdgeRatio, Color hoverPlayer, Color hoverEnemy, Color selected)
        {
            _activeOutlines = activeOutlines;
            _modelLayers = modelLayers;
            _grassHeight = grassHeight;
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
            _compatModelDepth?.Release();
            _compatModelDepth = null;
        }

        private static RenderTextureDescriptor DepthDescriptor(in RenderTextureDescriptor cameraDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            return descriptor;
        }

        private MaterialPropertyBlock FillOutlineProperties(int state, in RenderTextureDescriptor descriptor)
        {
            // Width is authored for 1080p so the line looks the same at every resolution.
            float width = _widthPixels * descriptor.height / 1080f;
            MaterialPropertyBlock properties = _properties[state];
            properties.SetColor(ColorId, _colors[state]);
            properties.SetVector(ParamsId, new Vector4(1f / descriptor.width, 1f / descriptor.height, width, _interiorEdgeRatio));
            properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            return properties;
        }

        private static bool DrawMarkers => UnitMarkerState.Count > 0 && UnitMarkerState.Mesh != null;

        #region Render Graph path

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            if (resourceData.isActiveTargetBackBuffer) return;
            if (!resourceData.cameraDepthTexture.IsValid()) return;

            uint active = _activeOutlines;
            bool drawMarkers = DrawMarkers;
            if (active == 0 && !drawMarkers) return;

            RenderTextureDescriptor descriptor = DepthDescriptor(cameraData.cameraTargetDescriptor);
            Shader.SetGlobalFloat(GrassHeightId, _grassHeight);

            TextureHandle modelDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_ModelDepth", true);
            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Model Depth", out DepthPassData data, profilingSampler))
            {
                data.rendererList = renderGraph.CreateRendererList(DepthOnlyList(renderingData, cameraData, lightData, _modelLayers, uint.MaxValue));
                builder.UseRendererList(data.rendererList);
                builder.SetRenderAttachmentDepth(modelDepth, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(modelDepth, ModelDepthId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DepthPassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.rendererList));
            }

            for (int i = 0; i < _bits.Length; i++)
            {
                if ((active & _bits[i]) == 0) continue;

                TextureHandle outlineDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_UnitOutlineDepth", true);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Outline Mask", out DepthPassData data, profilingSampler))
                {
                    data.rendererList = renderGraph.CreateRendererList(DepthOnlyList(renderingData, cameraData, lightData, -1, _bits[i]));
                    builder.UseRendererList(data.rendererList);
                    builder.SetRenderAttachmentDepth(outlineDepth, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((DepthPassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.rendererList));
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Outline Composite", out CompositePassData data, profilingSampler))
                {
                    data.material = _outlineMaterial;
                    data.properties = FillOutlineProperties(i, descriptor);
                    data.outlineDepth = outlineDepth;
                    builder.UseTexture(outlineDepth, AccessFlags.Read);
                    builder.UseGlobalTexture(ModelDepthId);
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

            if (drawMarkers)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Unit Markers", out MarkerPassData data, profilingSampler))
                {
                    data.material = _markerMaterial;
                    data.mesh = UnitMarkerState.Mesh;
                    builder.UseGlobalTexture(ModelDepthId);
                    builder.UseGlobalTexture(CameraDepthTextureId);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((MarkerPassData d, RasterGraphContext context) => context.cmd.DrawMesh(d.mesh, Matrix4x4.identity, d.material, 0, 0));
                }
            }
        }

        private RendererListParams DepthOnlyList(UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData, int layerMask, uint renderingLayerMask)
        {
            DrawingSettings drawing = CreateDrawingSettings(DepthOnlyTag, renderingData, cameraData, lightData, SortingCriteria.None);
            drawing.perObjectData = PerObjectData.None;
            FilteringSettings filtering = new(RenderQueueRange.opaque, layerMask, renderingLayerMask);
            return new RendererListParams(renderingData.cullResults, drawing, filtering);
        }

        #endregion

        #region Compatibility mode path

#pragma warning disable CS0672, CS0618
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = DepthDescriptor(renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateHandleIfNeeded(ref _compatOutlineDepth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_UnitOutlineDepth");
            RenderingUtils.ReAllocateHandleIfNeeded(ref _compatModelDepth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_ModelDepth");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            uint active = _activeOutlines;
            bool drawMarkers = DrawMarkers;
            if (active == 0 && !drawMarkers) return;

            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            CommandBuffer cmd = CommandBufferPool.Get("Unit Outline");

            cmd.SetGlobalFloat(GrassHeightId, _grassHeight);
            CoreUtils.SetRenderTarget(cmd, _compatModelDepth, ClearFlag.Depth);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            DrawDepthOnly(context, ref renderingData, _modelLayers, uint.MaxValue);
            cmd.SetGlobalTexture(ModelDepthId, _compatModelDepth);

            for (int i = 0; i < _bits.Length; i++)
            {
                if ((active & _bits[i]) == 0) continue;

                CoreUtils.SetRenderTarget(cmd, _compatOutlineDepth, ClearFlag.Depth);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                DrawDepthOnly(context, ref renderingData, -1, _bits[i]);

                MaterialPropertyBlock properties = FillOutlineProperties(i, descriptor);
                properties.SetTexture(OutlineDepthId, _compatOutlineDepth);
                BindCameraTargets(cmd, renderer);
                cmd.DrawProcedural(Matrix4x4.identity, _outlineMaterial, 0, MeshTopology.Triangles, 3, 1, properties);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            if (drawMarkers)
            {
                BindCameraTargets(cmd, renderer);
                cmd.DrawMesh(UnitMarkerState.Mesh, Matrix4x4.identity, _markerMaterial, 0, 0);
            }

            // Leaving the camera targets bound keeps URP's tracked attachments true for the next pass.
            BindCameraTargets(cmd, renderer);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void DrawDepthOnly(ScriptableRenderContext context, ref RenderingData renderingData, int layerMask, uint renderingLayerMask)
        {
            DrawingSettings drawing = CreateDrawingSettings(DepthOnlyTag, ref renderingData, SortingCriteria.None);
            drawing.perObjectData = PerObjectData.None;
            FilteringSettings filtering = new(RenderQueueRange.opaque, layerMask, renderingLayerMask);
            context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
        }

        private static void BindCameraTargets(CommandBuffer cmd, ScriptableRenderer renderer)
        {
            RTHandle cameraDepth = renderer.cameraDepthTargetHandle;
            bool depthIsBackBuffer = cameraDepth == null || cameraDepth.nameID == new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            if (depthIsBackBuffer)
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, ClearFlag.None);
            else
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, cameraDepth, ClearFlag.None);
        }
#pragma warning restore CS0672, CS0618

        #endregion
    }
}
