using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal class ScreenSpaceDirectionalShadowsPass : ScriptableRenderPass
    {
        // Profiling tag
        private static string m_SSDSClassifyTilesProfilerTag            = "SSDS ClassifyTiles";
        private static string m_SSDS_RTRT_ClassifyTilesProfilerTag      = "SSDS RTRT ClassifyTiles";
        private static string m_RayTracingShadowsProfilerTag            = "RayTracingShadows";
        private static string m_SSDS_PCSS_ProfilerTag                   = "SSDS PCSS";
        private static string m_SSDS_AccumulateProfilerTag              = "SSDS Accumulate";
        private static string m_SSDS_EAWProfilerTag                     = "SSDS EdgeAvoidATrousWavelet";
        private static ProfilingSampler m_SSDSClassifyTilesProfilingSampler = new ProfilingSampler(m_SSDSClassifyTilesProfilerTag);
        private static ProfilingSampler m_SSDS_PCSS_ProfilingSampler = new ProfilingSampler(m_SSDS_PCSS_ProfilerTag);
        private static ProfilingSampler m_RayTracingShadowsProfilingSampler = new ProfilingSampler(m_RayTracingShadowsProfilerTag);
        private static ProfilingSampler m_SSDS_RTRT_ClassifyTiles_ProfilingSampler = new ProfilingSampler(m_SSDS_RTRT_ClassifyTilesProfilerTag);
        private static ProfilingSampler m_SSDS_AccumulateProfilingSampler = new ProfilingSampler(m_SSDS_AccumulateProfilerTag);
        private static ProfilingSampler m_SSDS_EAWProfilingSampler = new ProfilingSampler(m_SSDS_EAWProfilerTag);

        // Public Variables

        // Private Variables
        private ComputeShader m_ScreenSpaceDirectionalShadowsCS;
        private ComputeShader m_ShadowDenoiserCS;
        private int m_ClassifyTilesKernel;
        private int m_RayTracingClassifyTilesKernel;
        private int m_SSShadowsKernel;
        private int m_BilateralHKernel;
        private int m_BilateralVKernel;
        private int m_AccumulateKernel;
        private int m_AccumulateFinalKernel;
        private int m_EdgeAvoidATrousWaveletKernel;

        // Constants
        private const int c_screenSpaceShadowsTileSize = 16;

        // Statics


        public ScreenSpaceDirectionalShadowsPass(RenderPassEvent evt, ComputeShader ssDirectionalShadowsCS, ComputeShader shadowDenoiserCS)
        {
            base.renderPassEvent = evt;
            m_ScreenSpaceDirectionalShadowsCS = ssDirectionalShadowsCS;
            m_ShadowDenoiserCS = shadowDenoiserCS;

            m_ClassifyTilesKernel = m_ScreenSpaceDirectionalShadowsCS.FindKernel("ShadowClassifyTiles");
            m_RayTracingClassifyTilesKernel = m_ScreenSpaceDirectionalShadowsCS.FindKernel("RayTracingShadowClassifyTiles");
            m_SSShadowsKernel = m_ScreenSpaceDirectionalShadowsCS.FindKernel("ScreenSpaceShadowmap");

            m_BilateralHKernel = m_ShadowDenoiserCS.FindKernel("BilateralFilterH");
            m_BilateralVKernel = m_ShadowDenoiserCS.FindKernel("BilateralFilterV");
            m_AccumulateKernel = m_ShadowDenoiserCS.FindKernel("ShadowAccumulate");
            m_AccumulateFinalKernel = m_ShadowDenoiserCS.FindKernel("ShadowAccumulateFinal");
            m_EdgeAvoidATrousWaveletKernel = m_ShadowDenoiserCS.FindKernel("EdgeAvoidATrousWavelet");
        }

        static RTHandle HistoryTracedShadowTextureAllocator(RenderTextureDescriptor desc, string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            frameIndex &= 1;

            // Must use scaleFactor
            return rtHandleSystem.Alloc(Vector2.one, TextureXR.slices, colorFormat: desc.graphicsFormat,
                 filterMode: FilterMode.Point, enableRandomWrite: true, useDynamicScale: true,
                name: string.Format("{0}_TracedShadowTexture{1}", viewName, frameIndex));
        }

        internal void ReAllocatedTracedShadowTextureIfNeeded(HistoryFrameRTSystem historyRTSystem, UniversalCameraData cameraData, RenderTextureDescriptor desc, out RTHandle currFrameRT, out RTHandle prevFrameRT)
        {
            var curTexture = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedShadow);

            if (curTexture == null)
            {
                historyRTSystem.ReleaseHistoryFrameRT(HistoryFrameType.RaytracedShadow);

                historyRTSystem.AllocHistoryFrameRT((int)HistoryFrameType.RaytracedShadow, cameraData.camera.name
                                                            , HistoryTracedShadowTextureAllocator, desc, 2);
            }

            currFrameRT = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedShadow);
            prevFrameRT = historyRTSystem.GetPreviousFrameRT(HistoryFrameType.RaytracedShadow);
        }

        static RTHandle HistoryShadowMomentsTextureAllocator(RenderTextureDescriptor desc, string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            frameIndex &= 1;

            // Must use scaleFactor
            return rtHandleSystem.Alloc(Vector2.one, TextureXR.slices, colorFormat: desc.graphicsFormat,
                 filterMode: FilterMode.Point, enableRandomWrite: true, useDynamicScale: true,
                name: string.Format("{0}_TracedShadowMomentsTexture{1}", viewName, frameIndex));
        }

        internal void ReAllocatedShadowMomentsTextureIfNeeded(HistoryFrameRTSystem historyRTSystem, UniversalCameraData cameraData, RenderTextureDescriptor desc, out RTHandle currFrameRT, out RTHandle prevFrameRT)
        {
            var curTexture = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedShadowMoments);

            if (curTexture == null)
            {
                historyRTSystem.ReleaseHistoryFrameRT(HistoryFrameType.RaytracedShadowMoments);

                historyRTSystem.AllocHistoryFrameRT((int)HistoryFrameType.RaytracedShadowMoments, cameraData.camera.name
                                                            , HistoryShadowMomentsTextureAllocator, desc, 2);
            }

            currFrameRT = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedShadowMoments);
            prevFrameRT = historyRTSystem.GetPreviousFrameRT(HistoryFrameType.RaytracedShadowMoments);
        }

        private class PassData
        {
            // Compute shader
            internal ComputeShader cs;
            internal int classifyTilesKernel;
            internal int rayTracingClassifyKernel;
            internal int shadowmapKernel;
            internal ComputeShader denoiserCS;
            internal int bilateralHKernel;
            internal int bilateralVKernel;
            internal int accumulateKernel;
            internal int accumulateFinalKernel;
            internal int eawKernel;

            internal int numTilesX;
            internal int numTilesY;

            // Compute Buffers
            internal BufferHandle dispatchIndirectBuffer;
            internal BufferHandle tileListBuffer;
            internal BufferHandle dispatchRaysIndirectBuffer;
            internal BufferHandle raysCoordBuffer;

            // Texture
            internal TextureHandle dirShadowmapTex;
            internal TextureHandle screenSpaceShadowmapTex;
            internal Vector2Int screenSpaceShadowmapSize;
            internal TextureHandle tracedShadowTex;
            internal TextureHandle prevTracedShadowTex;
            internal TextureHandle shadowMomentsTex;
            internal TextureHandle prevShadowMomentsTex;
            internal TextureHandle normalGBuffer;
            internal TextureHandle stencilHandle;
            internal TextureHandle motionVectorTexture;
            internal TextureHandle prevCameraDepthTexture;
            internal TextureHandle meanVarianceTexture;

            internal int camHistoryFrameCount;
            internal TextureHandle blueNoiseArray;
            internal Camera camera;

            // Ray Tracing
            internal bool requireRayTracing;
            internal RayTracingShader rtrtShader;
            internal RayTracingAccelerationStructure rtas;
            internal uint dispatchRaySizeX;
            internal uint dispatchRaySizeY;
            internal ShaderVariablesRaytracing rayTracingCB;
            internal float rayTracingDirShadowPenumbra;
            internal float rayTracingDirShadowCharNormalOffset;
            internal float rayTracingDirShadowCharHalfDirScale;
            internal bool enableDenoiser;
            internal bool enableEAWBlur;

            internal Matrix4x4 clipToPrevClipMatrix;
        }

        /// <summary>
        /// Initialize the shared pass data.
        /// </summary>
        /// <param name="passData"></param>
        private void InitPassData(RenderGraph renderGraph, PassData passData, UniversalCameraData cameraData, UniversalResourceData resourceData, int historyFramCount)
        {
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.R16;
            desc.depthBufferBits = 0;
            desc.enableRandomWrite = true;

            passData.cs = m_ScreenSpaceDirectionalShadowsCS;
            passData.classifyTilesKernel = m_ClassifyTilesKernel;
            passData.rayTracingClassifyKernel = m_RayTracingClassifyTilesKernel;
            passData.shadowmapKernel = m_SSShadowsKernel;

            passData.denoiserCS = m_ShadowDenoiserCS;
            passData.bilateralHKernel = m_BilateralHKernel;
            passData.bilateralVKernel = m_BilateralVKernel;
            passData.accumulateKernel = m_AccumulateKernel;
            passData.accumulateFinalKernel = m_AccumulateFinalKernel;
            passData.eawKernel = m_EdgeAvoidATrousWaveletKernel;

            passData.camHistoryFrameCount = historyFramCount;
            passData.blueNoiseArray = resourceData.blueNoise128RG;
            passData.camera = cameraData.camera;

            var width = cameraData.cameraTargetDescriptor.width;
            var height = cameraData.cameraTargetDescriptor.height;
            passData.numTilesX = RenderingUtils.DivRoundUp(width, c_screenSpaceShadowsTileSize);
            passData.numTilesY = RenderingUtils.DivRoundUp(height, c_screenSpaceShadowsTileSize);

            var bufferSystem = GraphicsBufferSystem.instance;
            var dispatchIndirectBuffer = bufferSystem.GetGraphicsBuffer<uint>(GraphicsBufferSystemBufferID.ScreenSpaceShadowIndirect, 3, "dispatchIndirectBuffer", GraphicsBuffer.Target.IndirectArguments);
            passData.dispatchIndirectBuffer = renderGraph.ImportBuffer(dispatchIndirectBuffer);
            passData.tileListBuffer = renderGraph.CreateBuffer(new BufferDesc(passData.numTilesX * passData.numTilesY, sizeof(uint)));

            var dispatchRaysIndirectBuffer = bufferSystem.GetGraphicsBuffer<uint>(GraphicsBufferSystemBufferID.RTShadowRaysIndirect, 3, "dispatchRaysIndirectBuffer", GraphicsBuffer.Target.IndirectArguments);
            passData.dispatchRaysIndirectBuffer = renderGraph.ImportBuffer(dispatchRaysIndirectBuffer);
            passData.raysCoordBuffer = renderGraph.CreateBuffer(new BufferDesc(desc.width * desc.height, sizeof(uint)));


            passData.dirShadowmapTex = resourceData.directionalShadowsTexture;


            passData.screenSpaceShadowmapTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ScreenSpaceShadowmapTexture", true, Color.white);
            passData.screenSpaceShadowmapSize = new Vector2Int(desc.width, desc.height);


            passData.normalGBuffer = resourceData.gBuffer[2]; // Normal GBuffer
            passData.stencilHandle = resourceData.activeDepthTexture;
            passData.motionVectorTexture = resourceData.motionVectorColor;




            MotionVectorsPersistentData motionData = null;
            if (cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                motionData = additionalCameraData.motionVectorsPersistentData;
            if (motionData != null)
            {
                passData.clipToPrevClipMatrix = motionData.previousViewProjection * Matrix4x4.Inverse(motionData.viewProjection);
            }

        }

        private void InitRayTracingPassData(RenderGraph renderGraph, PassData passData, UniversalCameraData cameraData, UniversalResourceData resourceData)
        {
            var stack = VolumeManager.instance.stack;
            var volumeSettings = stack.GetComponent<Shadows>();
            if (volumeSettings == null)
            {
                passData.requireRayTracing = false;
                return;
            }

            passData.requireRayTracing &= volumeSettings.rayTracing.value;

            if (passData.requireRayTracing)
            {
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.R16;
                desc.depthBufferBits = 0;
                desc.enableRandomWrite = true;

                var runtimeShaders = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeShaders>();
                passData.rtrtShader = runtimeShaders.rayTracingShadows;
                passData.rtas = cameraData.rayTracingSystem.RequestAccelerationStructure();

                var width = cameraData.cameraTargetDescriptor.width;
                var height = cameraData.cameraTargetDescriptor.height;
                passData.dispatchRaySizeX = (uint)width;
                passData.dispatchRaySizeY = (uint)height;

                // RayTracing constant buffer
                {
                    var rayTracingSettings = stack.GetComponent<RayTracingSettings>();

                    passData.rayTracingCB = cameraData.rayTracingSystem.GetShaderVariablesRaytracingCB(new Vector2Int(width, height), rayTracingSettings);
                    passData.rayTracingCB._RaytracingRayMaxLength = Mathf.Min(volumeSettings.dirShadowsRayLength.value, rayTracingSettings.directionalShadowRayLength.value);
                    passData.rayTracingCB._RayTracingClampingFlag = 1;
                    passData.rayTracingCB._RaytracingIntensityClamp = 1.0f;
                    passData.rayTracingCB._RaytracingPreExposition = 0;
                    passData.rayTracingCB._RayTracingDiffuseLightingOnly = 0;
                    passData.rayTracingCB._RayTracingAPVRayMiss = 0;
                    passData.rayTracingCB._RayTracingRayMissFallbackHierarchy = 0;
                    passData.rayTracingCB._RayTracingRayMissUseAmbientProbeAsSky = 0;
                    passData.rayTracingCB._RayTracingLastBounceFallbackHierarchy = 0;
                    passData.rayTracingCB._RayTracingAmbientProbeDimmer = 1.0f;
                }

                // Other settings
                passData.rayTracingDirShadowPenumbra = volumeSettings.dirShadowPenumbra.value;
                passData.rayTracingDirShadowCharHalfDirScale = volumeSettings.characterHalfDirScale.value;
                passData.rayTracingDirShadowCharNormalOffset = volumeSettings.characterNormalOffset.value;
                passData.enableDenoiser = volumeSettings.denoiser.value;
                passData.enableEAWBlur = volumeSettings.edgeAvoidingWaveletBlur.value;


                // Import history texture.
                var historyRTSystem = HistoryFrameRTSystem.GetOrCreate(cameraData.camera);
                RTHandle prevCamDepthTexture = historyRTSystem?.GetPreviousFrameRT(HistoryFrameType.Depth);
                if (prevCamDepthTexture == null)
                {
                    passData.prevCameraDepthTexture = resourceData.cameraDepthTexture;
                }
                else
                {
                    passData.prevCameraDepthTexture = renderGraph.ImportTexture(prevCamDepthTexture);
                }

                RTHandle tracedShadowTexture, prevTracedShadowTexture;
                ReAllocatedTracedShadowTextureIfNeeded(historyRTSystem, cameraData, desc, out tracedShadowTexture, out prevTracedShadowTexture);
                passData.tracedShadowTex = renderGraph.ImportTexture(tracedShadowTexture);
                passData.prevTracedShadowTex = renderGraph.ImportTexture(prevTracedShadowTexture);


                var shadowMomentsDesc = desc;
                shadowMomentsDesc.colorFormat = RenderTextureFormat.RGB111110Float;
                RTHandle shadowMomentsTexture, prevshadowMomentsTexture;
                ReAllocatedShadowMomentsTextureIfNeeded(historyRTSystem, cameraData, shadowMomentsDesc, out shadowMomentsTexture, out prevshadowMomentsTexture);
                passData.shadowMomentsTex = renderGraph.ImportTexture(shadowMomentsTexture);
                passData.prevShadowMomentsTex = renderGraph.ImportTexture(prevshadowMomentsTexture);

                var meanVarianceDesc = desc;
                meanVarianceDesc.colorFormat = RenderTextureFormat.RG32;
                passData.meanVarianceTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, meanVarianceDesc, "_MeanVarianceTexture", true, Color.white);
            }
        }

        private static void ExecuteRayTracingShadowsPass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            bool needDenoiser = data.enableDenoiser && data.rayTracingDirShadowPenumbra != 0;
            bool needEAWFilter = data.enableEAWBlur;

            using (new ProfilingScope(cmd, m_SSDS_RTRT_ClassifyTiles_ProfilingSampler))
            {
                cmd.SetComputeBufferParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants.g_DispatchIndirectBuffer, data.dispatchIndirectBuffer);
                cmd.SetComputeBufferParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants.g_TileList, data.tileListBuffer);

                cmd.SetComputeBufferParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._ShadowRayIndirectBuffer, data.dispatchRaysIndirectBuffer);
                cmd.SetComputeBufferParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._ShadowRayCoordBuffer, data.raysCoordBuffer);

                cmd.SetComputeTextureParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._DirShadowmapTexture, data.dirShadowmapTex);
                cmd.SetComputeTextureParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._SSDirShadowmapTexture, data.screenSpaceShadowmapTex);
                cmd.SetComputeTextureParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._TracedShadowTexture, data.tracedShadowTex);
                cmd.SetComputeTextureParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._StencilTexture, data.stencilHandle, 0, RenderTextureSubElement.Stencil);
                cmd.SetComputeTextureParam(data.cs, data.rayTracingClassifyKernel, ShaderConstants._ShadowMomentstexture, data.shadowMomentsTex);

                cmd.DispatchCompute(data.cs, data.rayTracingClassifyKernel, data.numTilesX, data.numTilesY, 1);
            }

            using (new ProfilingScope(cmd, m_RayTracingShadowsProfilingSampler))
            {
                BlueNoiseSystem.BindSTBNParams(BlueNoiseTexFormat._128RG, cmd, data.rtrtShader, data.blueNoiseArray, data.camHistoryFrameCount);
                // Define the shader pass to use for the shadow pass
                cmd.SetRayTracingShaderPass(data.rtrtShader, "VisibilityDXR");

                // Set the acceleration structure for the pass
                cmd.SetRayTracingAccelerationStructure(data.rtrtShader, "_RaytracingAccelerationStructure", data.rtas);

                // Set ConstantBuffer
                ConstantBuffer.PushGlobal(cmd, data.rayTracingCB, RayTracingSystem._ShaderVariablesRaytracing);
                cmd.SetRayTracingFloatParam(data.rtrtShader, ShaderConstants._RayTracingDirShadowPenumbraCOS, Mathf.Cos(data.rayTracingDirShadowPenumbra * 45 *Mathf.Deg2Rad));
                cmd.SetRayTracingFloatParam(data.rtrtShader, ShaderConstants._RayTracingDirShadowCharacterNormalOffset, data.rayTracingDirShadowCharNormalOffset);
                cmd.SetRayTracingFloatParam(data.rtrtShader, ShaderConstants._RayTracingDirShadowCharacterHalfDirScale, data.rayTracingDirShadowCharHalfDirScale);
                cmd.SetRayTracingFloatParam(data.rtrtShader, ShaderConstants._CamHistoryFrameCount, data.camHistoryFrameCount);

                // Set Textures & Buffers
                cmd.SetRayTracingBufferParam(data.rtrtShader, ShaderConstants._ShadowRayCoordBuffer, data.raysCoordBuffer);
                cmd.SetRayTracingTextureParam(data.rtrtShader, ShaderConstants._RayTracingShadowsTextureRW, needDenoiser ? data.tracedShadowTex : data.screenSpaceShadowmapTex);
                cmd.SetGlobalTexture(ShaderConstants._StencilTexture, data.stencilHandle, RenderTextureSubElement.Stencil);

                cmd.DispatchRays(data.rtrtShader, "SingleRayGen", data.dispatchRaysIndirectBuffer, argsOffset: 0, data.camera);
            }


            if (!needDenoiser)
                return;


            // Accumulate
            using (new ProfilingScope(cmd, m_SSDS_AccumulateProfilingSampler))
            {
                var accumulateKernel = needEAWFilter ? data.accumulateKernel : data.accumulateFinalKernel;
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._TracedShadowTexture, data.tracedShadowTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevTracedShadowTexture, data.prevTracedShadowTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._ShadowMomentstexture, data.shadowMomentsTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevShadowMomentstexture, data.prevShadowMomentsTex);

                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._DirShadowmapTexture, data.dirShadowmapTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._StencilTexture, data.stencilHandle, 0, RenderTextureSubElement.Stencil);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._CameraMotionVectorsTexture, data.motionVectorTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevCameraDepthTexture, data.prevCameraDepthTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._SSDirShadowmapTexture, data.screenSpaceShadowmapTex);

                cmd.SetComputeMatrixParam(data.denoiserCS, ShaderConstants._ClipToPrevClipMatrix, data.clipToPrevClipMatrix);

                cmd.SetComputeBufferParam(data.denoiserCS, accumulateKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, accumulateKernel, data.dispatchIndirectBuffer, 0);
            }

            if (!needEAWFilter)
                return;

            // Edge-Avoiding A-Trous Wavelet (EAW) Filter
            using (new ProfilingScope(cmd, m_SSDS_EAWProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._SSDirShadowmapTexture, data.screenSpaceShadowmapTex);
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._TracedShadowTexture, data.tracedShadowTex);

                cmd.SetComputeBufferParam(data.denoiserCS, data.eawKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, data.eawKernel, data.dispatchIndirectBuffer, 0);
            }
        }

        private static void ExecuteComputeShadowsPass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            cmd.SetComputeFloatParam(data.cs, ShaderConstants._CamHistoryFrameCount, data.camHistoryFrameCount);

            // BuildIndirect
            using (new ProfilingScope(cmd, m_SSDSClassifyTilesProfilingSampler))
            {
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_DispatchIndirectBuffer, data.dispatchIndirectBuffer);
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_TileList, data.tileListBuffer);

                cmd.SetComputeTextureParam(data.cs, data.classifyTilesKernel, ShaderConstants._DirShadowmapTexture, data.dirShadowmapTex);
                cmd.SetComputeTextureParam(data.cs, data.classifyTilesKernel, ShaderConstants._SSDirShadowmapTexture, data.screenSpaceShadowmapTex);
                cmd.SetComputeTextureParam(data.cs, data.classifyTilesKernel, ShaderConstants._StencilTexture, data.stencilHandle, 0, RenderTextureSubElement.Stencil);

                cmd.DispatchCompute(data.cs, data.classifyTilesKernel, data.numTilesX, data.numTilesY, 1);
            }

            // PCSS ScreenSpaceShadowmap
            using (new ProfilingScope(cmd, m_SSDS_PCSS_ProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.cs, data.shadowmapKernel, ShaderConstants._DirShadowmapTexture, data.dirShadowmapTex);
                cmd.SetComputeTextureParam(data.cs, data.shadowmapKernel, ShaderConstants._PCSSTexture, data.screenSpaceShadowmapTex);

                // Indirect buffer & dispatch
                cmd.SetComputeBufferParam(data.cs, data.shadowmapKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.cs, data.shadowmapKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            }
        }

        private static void ExecutePass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            if (data.requireRayTracing)
            {
                // Ray Tracing Shadows
                ExecuteRayTracingShadowsPass(data, context);
            }
            else
            {
                // Compute Shadows
                ExecuteComputeShadowsPass(data, context);
            }

        }

        internal TextureHandle Render(RenderGraph renderGraph, ContextContainer frameData)
        {
            int historyFramCount = 0;
            var historyRTSystem = HistoryFrameRTSystem.GetOrCreate(frameData.Get<UniversalCameraData>().camera);
            if (historyRTSystem != null)
                historyFramCount = historyRTSystem.historyFrameCount;

            using (var builder = renderGraph.AddComputePass<PassData>("Render SS Shadow", out var passData, ProfilingSampler.Get(URPProfileId.RenderSSShadow)))
            {
                // Access resources
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalShadowData shadowData = frameData.Get<UniversalShadowData>();

                // Ray Tracing
                passData.requireRayTracing = cameraData.supportedRayTracing && cameraData.rayTracingSystem.GetRayTracingState();
                InitRayTracingPassData(renderGraph, passData, cameraData, resourceData);
                shadowData.rayTracingShadowsEnabled = passData.requireRayTracing;

                // Setup passData
                InitPassData(renderGraph, passData, cameraData, resourceData, historyFramCount);

                // Setup builder state
                builder.UseBuffer(passData.dispatchIndirectBuffer, AccessFlags.ReadWrite);
                builder.UseBuffer(passData.tileListBuffer, AccessFlags.ReadWrite);
                builder.UseTexture(passData.dirShadowmapTex, AccessFlags.Read);
                builder.UseTexture(passData.screenSpaceShadowmapTex, AccessFlags.ReadWrite);
                builder.UseTexture(passData.normalGBuffer, AccessFlags.Read);
                builder.UseTexture(passData.stencilHandle, AccessFlags.Read);

                builder.UseBuffer(passData.raysCoordBuffer, AccessFlags.ReadWrite);
                if (passData.requireRayTracing)
                {
                    builder.UseTexture(passData.tracedShadowTex, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.prevTracedShadowTex, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.shadowMomentsTex, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.prevShadowMomentsTex, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.blueNoiseArray, AccessFlags.Read);
                    builder.UseTexture(passData.motionVectorTexture, AccessFlags.Read);
                    builder.UseTexture(passData.prevCameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(passData.meanVarianceTexture, AccessFlags.ReadWrite);
                }

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(passData.requireRayTracing);
                //builder.EnableAsyncCompute(true);

                builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
                {
                    ExecutePass(data, context);
                });

                return passData.screenSpaceShadowmapTex;
            }
        }

        static class ShaderConstants
        {
            public static readonly int g_DispatchIndirectBuffer = Shader.PropertyToID("g_DispatchIndirectBuffer");
            public static readonly int g_TileList = Shader.PropertyToID("g_TileList");

            public static readonly int _ShadowRayIndirectBuffer = Shader.PropertyToID("_ShadowRayIndirectBuffer");
            public static readonly int _ShadowRayCoordBuffer = Shader.PropertyToID("_ShadowRayCoordBuffer");

            public static readonly int _DirShadowmapTexture = Shader.PropertyToID("_DirShadowmapTexture");
            public static readonly int _SSDirShadowmapTexture = Shader.PropertyToID("_SSDirShadowmapTexture");
            public static readonly int _ScreenSpaceShadowmapTexture = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
            public static readonly int _PCSSTexture = Shader.PropertyToID("_PCSSTexture");
            public static readonly int _BilateralTexture = Shader.PropertyToID("_BilateralTexture");
            public static readonly int _CamHistoryFrameCount = Shader.PropertyToID("_CamHistoryFrameCount");

            public static readonly int _RayTracingShadowsTextureRW = Shader.PropertyToID("_RayTracingShadowsTextureRW");
            public static readonly int _StencilTexture = Shader.PropertyToID("_StencilTexture");
            public static readonly int _RayTracingDirShadowPenumbra = Shader.PropertyToID("_RayTracingDirShadowPenumbra");
            public static readonly int _RayTracingDirShadowPenumbraCOS = Shader.PropertyToID("_RayTracingDirShadowPenumbraCOS");
            public static readonly int _RayTracingDirShadowCharacterNormalOffset = Shader.PropertyToID("_RayTracingDirShadowCharacterNormalOffset");
            public static readonly int _RayTracingDirShadowCharacterHalfDirScale = Shader.PropertyToID("_RayTracingDirShadowCharacterHalfDirScale");

            public static readonly int _TracedShadowTexture = Shader.PropertyToID("_TracedShadowTexture");
            public static readonly int _PrevTracedShadowTexture = Shader.PropertyToID("_PrevTracedShadowTexture");
            public static readonly int _CameraMotionVectorsTexture = Shader.PropertyToID("_CameraMotionVectorsTexture");
            public static readonly int _PrevCameraDepthTexture = Shader.PropertyToID("_PrevCameraDepthTexture");
            public static readonly int _ShadowMomentstexture = Shader.PropertyToID("_ShadowMomentstexture");
            public static readonly int _PrevShadowMomentstexture = Shader.PropertyToID("_PrevShadowMomentstexture");
            public static readonly int _MeanVarianceTexture = Shader.PropertyToID("_MeanVarianceTexture");

            public static readonly int _ClipToPrevClipMatrix = Shader.PropertyToID("_ClipToPrevClipMatrix");

        }
    }
}
