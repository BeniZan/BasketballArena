using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class WebRTCCompositor : ScriptableRendererFeature {
    // A standard URP Blit material (created via Assets > Create > Shader > SRP Blit Shader)
    public Material blitMaterial;
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    private ScreenBlitPass m_BlitPass;
    public override void Create() {
        m_BlitPass = new ScreenBlitPass(blitMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        // Inject the pass into the execution queue
        renderer.EnqueuePass(m_BlitPass);
    }

    // The Custom Render Pass utilizing Unity 6 Render Graph
    class ScreenBlitPass : ScriptableRenderPass {
        private Material m_Material;

        public ScreenBlitPass(Material material) {
            m_Material = material;
            // Configure what the pass needs access to (e.g., Camera Color)
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        // PassData holds the texture handles required inside the execution function
        private class PassData {
            public TextureHandle source;
            public TextureHandle destination;
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            // 1. Extract URP frame resource data
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Ensure the active color texture is valid
            if (resourceData.activeColorTexture.IsValid() == false) return;
           


            // 2. Add a Raster Render Pass to the graph
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ScreenBlitPass", out var passData)) {
                // 3. Define the descriptor for our custom destination texture
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;

                // 4. Create the destination texture inside the Render Graph
                TextureHandle destinationTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_CustomScreenTexture", false);
                
                // 5. Populate PassData for the execution block
                passData.source = resourceData.activeColorTexture;
                passData.destination = destinationTex;
                passData.material = m_Material;

                // 6. Tell the builder how these textures are being utilized
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.destination, 0);

                // Prevent Unity from culling this pass if its output isn't immediately used by the camera
                builder.AllowPassCulling(false);

                // 7. Assign the execution function
                builder.SetRenderFunc<PassData>((data, rasterContext) => ExecutePass(data, rasterContext));
            }
        }

        // The safe execution context where actual rendering happens
        private static void ExecutePass(PassData data, RasterGraphContext context) {
            if (data.material == null) 
                return;

            // In Unity 6, use Blitter.BlitTexture instead of cmd.Blit
            // Scale bias (Vector4(1,1,0,0)) ensures it maps 1:1 onto the target aspect ratio
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
        }
    }
}
