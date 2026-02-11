using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature that applies full-screen edge detection outlines.
/// Uses Roberts Cross operator on depth + normals for thick comic-book ink lines.
/// Compatible with URP 17+ (Unity 6).
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

        public OutlineRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempRT, desc, name: "_OutlineTempRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.outlineMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("OutlineEdgeDetect");

            // Set material properties
            _settings.outlineMaterial.SetColor(OutlineColorId, _settings.outlineColor);
            _settings.outlineMaterial.SetFloat(OutlineThicknessId, _settings.thickness);
            _settings.outlineMaterial.SetFloat(DepthThresholdId, _settings.depthThreshold);
            _settings.outlineMaterial.SetFloat(NormalThresholdId, _settings.normalThreshold);

            // Get camera color target
            RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Set the source texture for our shader
            cmd.SetGlobalTexture(BlitTextureId, cameraColor);
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            cmd.SetGlobalVector(BlitTexelSizeId, new Vector4(
                1f / desc.width, 1f / desc.height, desc.width, desc.height));

            // Blit: camera color → temp with outline shader
            Blitter.BlitCameraTexture(cmd, cameraColor, _tempRT, _settings.outlineMaterial, 0);

            // Blit: temp → camera color (copy back)
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
