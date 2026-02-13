using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature that applies full-screen edge detection outlines.
/// Uses Roberts Cross operator on depth + normals for thick comic-book ink lines.
/// Supports both RenderGraph (Unity 6 default) and legacy compatibility mode.
/// </summary>
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public Material outlineMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [Range(0, 5)] public float thickness = 2.0f;
        public Color outlineColor = Color.black;
        [Range(0, 10)] public float depthThreshold = 1.5f;
        [Range(0, 2)] public float normalThreshold = 0.4f;
    }

    public OutlineSettings settings = new OutlineSettings();
    OutlineRenderPass _outlinePass;

    public override void Create()
    {
        _outlinePass = new OutlineRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.outlineMaterial == null) return;
        renderer.EnqueuePass(_outlinePass);
    }

    class OutlineRenderPass : ScriptableRenderPass
    {
        OutlineSettings _settings;
        RTHandle _tempRT;
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitTexelSizeId = Shader.PropertyToID("_BlitTexture_TexelSize");

        class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public OutlineRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        void SetMaterialProperties()
        {
            _settings.outlineMaterial.SetColor(OutlineColorId, _settings.outlineColor);
            _settings.outlineMaterial.SetFloat(OutlineThicknessId, _settings.thickness);
            _settings.outlineMaterial.SetFloat(DepthThresholdId, _settings.depthThreshold);
            _settings.outlineMaterial.SetFloat(NormalThresholdId, _settings.normalThreshold);
        }

        // === RENDER GRAPH PATH (Unity 6 / URP 17 default) ===
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_settings.outlineMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            SetMaterialProperties();

            var source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_OutlineTemp";
            var temp = renderGraph.CreateTexture(desc);

            // Pass 1: Blit source -> temp with outline material
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineEdgeDetect", out var passData))
            {
                passData.source = source;
                passData.material = _settings.outlineMaterial;

                builder.UseTexture(source);
                builder.SetRenderAttachment(temp, 0);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: Blit temp -> source (copy back)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineCopyBack", out var passData))
            {
                passData.source = temp;
                passData.material = null;

                builder.UseTexture(temp);
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        // === LEGACY PATH (compatibility mode) ===
        [System.Obsolete("Legacy path for compatibility mode only")]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempRT, desc, name: "_OutlineTempRT");
        }

        [System.Obsolete("Legacy path for compatibility mode only")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.outlineMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("OutlineEdgeDetect");

            SetMaterialProperties();

            RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

            cmd.SetGlobalTexture(BlitTextureId, cameraColor);
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            cmd.SetGlobalVector(BlitTexelSizeId, new Vector4(
                1f / desc.width, 1f / desc.height, desc.width, desc.height));

            Blitter.BlitCameraTexture(cmd, cameraColor, _tempRT, _settings.outlineMaterial, 0);
            Blitter.BlitCameraTexture(cmd, _tempRT, cameraColor);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle managed by ReAllocateHandleIfNeeded, no manual cleanup needed
        }
    }
}
