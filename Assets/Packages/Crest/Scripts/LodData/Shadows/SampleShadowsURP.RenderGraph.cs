// Crest Ocean System

// Copyright 2024 Wave Harmonic Ltd

#if CREST_URP
#if UNITY_6000_0_OR_NEWER

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Crest
{
    public partial class SampleShadowsURP : ScriptableRenderPass
    {
        class PassData
        {
            public UniversalCameraData cameraData;
            public UniversalLightData lightData;
            public CullingResults cullResults;

            public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                cameraData = frameData.Get<UniversalCameraData>();
                lightData = frameData.Get<UniversalLightData>();
                cullResults = frameData.Get<UniversalRenderingData>().cullResults;
            }
        }

        readonly PassData passData = new();

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            using (var builder = graph.AddUnsafePass<PassData>(PassName, out var data))
            {
                data.Init(frame, builder);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    ExecutePass(context.GetRenderContext(), buffer, data);
                });
            }
        }

#if URP_COMPATIBILITY_MODE
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            passData.Init(renderingData.GetFrameData());
            var buffer = CommandBufferPool.Get(PassName);
            ExecutePass(context, buffer, passData);
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }
#endif
    }
}

#endif // UNITY_6000_0_OR_NEWER
#endif // CREST_URP
