using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
using ImGuiNET;
using System;
using System.Runtime.InteropServices;
#if HAS_URP
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine;
#endif

namespace UImGui.Renderer
{
#if HAS_URP
	internal class RenderImGui : ScriptableRendererFeature
	{
        // Wrapper around the renderer so UImGui can update and change it and the render pass won't lose access
        // due to the pointer changing with a new object.
        public class Settings
        {
            public IRenderer renderer;
        }

        public Settings settings;

        [HideInInspector]
        public Camera Camera;
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private CommandBufferPass _commandBufferPass;

        public override void Create()
        {
            _commandBufferPass = new CommandBufferPass(settings)
            {
                renderPassEvent = RenderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Camera != renderingData.cameraData.camera || Camera == null) return;

            renderer.EnqueuePass(_commandBufferPass);
        }

#if URP_COMPATIBILITY_MODE
        private class CommandBufferPass : ScriptableRenderPass
		{
			public CommandBuffer commandBuffer;

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				context.ExecuteCommandBuffer(commandBuffer);
			}
		}
#else
        private class CommandBufferPass : ScriptableRenderPass
        {
            private Settings settings;

            public CommandBufferPass(Settings settings)
            {
                this.settings = settings;
            }

            private class PassData
            {
                // Nothing needed?
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                using var builder = renderGraph.AddRasterRenderPass("IMGui Command Buffer", out PassData passData);

                // Use the active color texture from the camera to write the blit information from IMGui
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderFunc<PassData>(ExecutePass);
            }

            private void ExecutePass(PassData data, RasterGraphContext context)
            {
                if (settings == null || settings.renderer ==  null)
                {
                    return;
                }

                settings.renderer.RenderDrawLists(context.cmd, ImGui.GetDrawData());
            }
        }
#endif
	}
#else
    public class RenderImGui : UnityEngine.ScriptableObject
	{
		public CommandBuffer CommandBuffer;
	}
#endif
}
