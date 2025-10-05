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
        public IRenderer renderer;

        [HideInInspector]
        public Camera Camera;
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private CommandBufferPass _commandBufferPass;

        public override void Create()
        {
            _commandBufferPass = new CommandBufferPass(renderer)
            {
                renderPassEvent = RenderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Camera != renderingData.cameraData.camera) return;

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
            private IRenderer renderer;

            public CommandBufferPass(IRenderer renderer)
            {
                this.renderer = renderer;
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
                if (renderer ==  null)
                {
                    return;
                }

                renderer.RenderDrawLists(context.cmd, ImGui.GetDrawData());
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
