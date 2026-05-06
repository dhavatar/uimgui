using UImGui.Assets;
using UImGui.Renderer;
using UImGui.Texture;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using System.Linq;
#if HAS_URP
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Rendering.Universal;
#endif
#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace UImGui
{
	internal static class RenderUtility
	{
		public static IRenderer Create(RenderType type, ShaderResourcesAsset shaders, TextureManager textures)
		{
			Assert.IsNotNull(shaders, "Shaders not assigned.");

#if UNITY_WEBGL
			// SV_VertexID is not supported on WebGL/GLES 2.0 — force Mesh renderer.
			type = RenderType.Mesh;
#endif

			switch (type)
			{
#if UNITY_2020_1_OR_NEWER
				case RenderType.Mesh:
					return new RendererMesh(shaders, textures);
#endif
				case RenderType.Procedural:
					return new RendererProcedural(shaders, textures);
				default:
					return null;
			}
		}

		public static bool IsUsingURP()
		{
			var currentRP = GraphicsSettings.currentRenderPipeline;
#if HAS_URP
			return currentRP is UniversalRenderPipelineAsset;
#else
			return false;
#endif
		}

#if HAS_URP
		public static RenderImGui FindRenderFeatureInCurrentPipeline()
		{
			var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null)
                {
                    return null;
                }
            }

            if (urpAsset.rendererDataList == null)
            {
                return null;
            }

            // Find and store the first instance of the RenderImGui in the render list
            RenderImGui result = null;
            foreach (var renderData in urpAsset.rendererDataList)
            {
                result = renderData.rendererFeatures.Where(x => x is RenderImGui)
                    .FirstOrDefault() as RenderImGui;
                if (result != null)
                    break;
            }

			return result;
		}
#endif

		public static bool IsUsingHDRP()
		{
			var currentRP = GraphicsSettings.currentRenderPipeline;

#if HAS_HDRP
			return currentRP is HDRenderPipelineAsset;
#else
			return false;
#endif
		}

		public static CommandBuffer GetCommandBuffer(string name)
		{
#if HAS_URP || HAS_HDRP
			return CommandBufferPool.Get(name);
#else
			return new CommandBuffer { name = name };
#endif
		}

		public static void ReleaseCommandBuffer(CommandBuffer commandBuffer)
		{
#if HAS_URP || HAS_HDRP
			CommandBufferPool.Release(commandBuffer);
#else
			commandBuffer.Release();
#endif
		}
	}
}
