using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class UnderwaterRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material _material;

    private UnderwaterPass _pass;

    public override void Create()
    {
        _pass = new UnderwaterPass(_material)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null)
        {
            Debug.LogError("[UnderwaterRendererFeature] Material is not assigned.");
            return;
        }

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    private class UnderwaterPass : ScriptableRenderPass
    {
        private readonly Material _material;

        public UnderwaterPass(Material material)
        {
            _material = material;
        }

        private class PassData
        {
            internal TextureHandle src;
            internal Material      material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples     = MSAASamples.None;
            desc.name            = "_UnderwaterTemp";
            desc.clearBuffer     = false;

            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("UnderwaterPass_Blit", out PassData passData))
            {
                passData.src      = src;
                passData.material = _material;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("UnderwaterPass_Copy", out PassData passData))
            {
                passData.src      = temp;
                passData.material = null;

                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), 0, false);
                });
            }
        }

        public void Dispose() { }
    }
}