using System.Security.Cryptography;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal class ScreenSpaceAmbientOcclusionPass : ScriptableRenderPass
    {
        // Profiling tag
        private static string m_ClassifyTilesProfilerTag        = "SSAO ClassifyTiles";
        private static string m_PrefilterNormalProfilerTag      = "XeGTAO PrefilterNormal";
        private static string m_PrefilterDepthProfilerTag       = "XeGTAO PrefilterDepth";
        private static string m_MainPassProfilerTag             = "XeGTAO MainPass";
        private static string m_AccumulateTag                   = "SSAO Accumulate";
        private static string m_EdgeAvoidATrousWaveletTag       = "SSAO EdgeAvoidATrousWavelet";
        private static string m_RayTracingAmbientOcclusionProfilerTag = "RayTracing AmbientOcclusion";
        private static string m_BilateralFilterProfilerTag = "BilateralFilter";
        private static ProfilingSampler m_ClassifyTilesProfilingSampler = new ProfilingSampler(m_ClassifyTilesProfilerTag);
        private static ProfilingSampler m_PrefilterNormalProfilingSampler = new ProfilingSampler(m_PrefilterNormalProfilerTag);
        private static ProfilingSampler m_PrefilterDepthProfilingSampler = new ProfilingSampler(m_PrefilterDepthProfilerTag);
        private static ProfilingSampler m_MainPassProfilingSampler = new ProfilingSampler(m_MainPassProfilerTag);
        private static ProfilingSampler m_AccumulateProfilingSampler = new ProfilingSampler(m_AccumulateTag);
        private static ProfilingSampler m_EdgeAvoidATrousWaveletProfilingSampler = new ProfilingSampler(m_EdgeAvoidATrousWaveletTag);
        private static ProfilingSampler m_RayTracingAmbientOcclusionProfilingSampler = new ProfilingSampler(m_RayTracingAmbientOcclusionProfilerTag);
        private static ProfilingSampler m_BilateralFilterProfilingSampler = new ProfilingSampler(m_BilateralFilterProfilerTag);
        

        // Public Variables

        // Private Variables
        private ComputeShader m_XeGTAO_CS;
        private int m_ClassifyTilesKernel;
        private int m_PrefilterNormalKernel;
        private int m_PrefilterDepthKernel;
        private int m_MainPassKernel;

        private ComputeShader m_DenoiserCS;
        private int m_AccumulateKernel;
        private int m_EdgeAvoidATrousWaveletKernel;
        private int m_BilateralHKernel;
        private int m_BilateralVKernel;

        private RayTracingShader m_RTAOShader;

        private AmbientOcclusion m_volumeSettings;

        // Constants
        /// <summary>
        /// Compute shader is 8x8, each thread computes 2x2 blocks so processing 16x16 block.
        /// Dispatch needs to be called with (width + 16-1) / 16, (height + 16-1) / 16
        /// </summary>
        private const int c_XeGTAOTileSize = 16;
        private const int c_PrefilterDepthTileSize  = 16;
        private const int c_MainPassTileSize        = 8;

        // Statics


        public ScreenSpaceAmbientOcclusionPass(RenderPassEvent evt, ComputeShader gtaoCS, ComputeShader aoDenoiserCS, RayTracingShader rtaoShader)
        {
            base.renderPassEvent = evt;
            m_XeGTAO_CS = gtaoCS;
            m_DenoiserCS = aoDenoiserCS;
            m_RTAOShader = rtaoShader;

            m_ClassifyTilesKernel = m_XeGTAO_CS.FindKernel("XeGTAOClassifyTiles");
            m_PrefilterNormalKernel = m_XeGTAO_CS.FindKernel("XeGTAOPrefilterNormal");
            m_PrefilterDepthKernel = m_XeGTAO_CS.FindKernel("XeGTAOPrefilterDepth");
            m_MainPassKernel = m_XeGTAO_CS.FindKernel("XeGTAOMainPass");

            m_BilateralHKernel = m_DenoiserCS.FindKernel("BilateralFilterH");
            m_BilateralVKernel = m_DenoiserCS.FindKernel("BilateralFilterV");

            m_AccumulateKernel = m_DenoiserCS.FindKernel("AOAccumulate");
            m_EdgeAvoidATrousWaveletKernel = m_DenoiserCS.FindKernel("EdgeAvoidATrousWavelet");
        }

        /// <summary>
        /// Setup controls per frame shouldEnqueue this pass.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="renderingData"></param>
        /// <returns></returns>
        internal bool Setup()
        {
            var stack = VolumeManager.instance.stack;
            m_volumeSettings = stack.GetComponent<AmbientOcclusion>();

            return m_XeGTAO_CS != null && m_volumeSettings != null && m_volumeSettings.IsActive();
        }

        static RTHandle HistoryTracedAmbientOcclusionTextureAllocator(RenderTextureDescriptor desc, string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            frameIndex &= 1;

            // Must use scaleFactor
            return rtHandleSystem.Alloc(Vector2.one, TextureXR.slices, colorFormat: desc.graphicsFormat,
                 filterMode: FilterMode.Point, enableRandomWrite: true, useDynamicScale: true,
                name: string.Format("{0}_TracedAmbientOcclusionTexture{1}", viewName, frameIndex));
        }

        internal void ReAllocatedTracedAmbientOcclusionTextureIfNeeded(HistoryFrameRTSystem historyRTSystem, UniversalCameraData cameraData, RenderTextureDescriptor desc, out RTHandle currFrameRT, out RTHandle prevFrameRT)
        {
            var curTexture = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedAmbientOcclusion);

            if (curTexture == null)
            {
                historyRTSystem.ReleaseHistoryFrameRT(HistoryFrameType.RaytracedAmbientOcclusion);

                historyRTSystem.AllocHistoryFrameRT((int)HistoryFrameType.RaytracedAmbientOcclusion, cameraData.camera.name
                                                            , HistoryTracedAmbientOcclusionTextureAllocator, desc, 2);
            }

            currFrameRT = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedAmbientOcclusion);
            prevFrameRT = historyRTSystem.GetPreviousFrameRT(HistoryFrameType.RaytracedAmbientOcclusion);
        }

        static RTHandle HistoryAmbientOcclusionMomentsTextureAllocator(RenderTextureDescriptor desc, string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            frameIndex &= 1;

            // Must use scaleFactor
            return rtHandleSystem.Alloc(Vector2.one, TextureXR.slices, colorFormat: desc.graphicsFormat,
                 filterMode: FilterMode.Point, enableRandomWrite: true, useDynamicScale: true,
                name: string.Format("{0}_TracedAmbientOcclusionMomentsTexture{1}", viewName, frameIndex));
        }

        internal void ReAllocatedAmbientOcclusionMomentsTextureIfNeeded(HistoryFrameRTSystem historyRTSystem, UniversalCameraData cameraData, RenderTextureDescriptor desc, out RTHandle currFrameRT, out RTHandle prevFrameRT)
        {
            var curTexture = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedAmbientOcclusionMoments);

            if (curTexture == null)
            {
                historyRTSystem.ReleaseHistoryFrameRT(HistoryFrameType.RaytracedAmbientOcclusionMoments);

                historyRTSystem.AllocHistoryFrameRT((int)HistoryFrameType.RaytracedAmbientOcclusionMoments, cameraData.camera.name
                                                            , HistoryAmbientOcclusionMomentsTextureAllocator, desc, 2);
            }

            currFrameRT = historyRTSystem.GetCurrentFrameRT(HistoryFrameType.RaytracedAmbientOcclusionMoments);
            prevFrameRT = historyRTSystem.GetPreviousFrameRT(HistoryFrameType.RaytracedAmbientOcclusionMoments);
        }

        private class PassData
        {
            internal UniversalCameraData cameraData;

            // Compute shader
            internal ComputeShader cs;
            internal int classifyTilesKernel;
            internal int prefilterNormalKernel;
            internal int prefilterDepthKernel;
            internal int mainPassKernel;

            internal ComputeShader denoiserCS;
            internal int bilateralHKernel;
            internal int bilateralVKernel;
            internal int accumulateKernel;
            internal int eawKernel;

            internal int numTilesX;
            internal int numTilesY;
            internal int preFilterDepthNumTilesX;
            internal int preFilterDepthNumTilesY;
            internal int mainPassTilesX;
            internal int mainPassTilesY;

            // Compute Buffers
            internal BufferHandle dispatchIndirectBuffer;
            internal BufferHandle tileListBuffer;

            // Constants
            internal ShaderVariablesScreenSpaceAmbientOcclusion constantBuffer;

            internal int camHistoryFrameCount;
            internal TextureHandle blueNoiseArray;

            // Texture
            internal TextureHandle geometricNormalTex;
            internal TextureHandle ambientOccusionTexture;
            internal TextureHandle viewDepthMipTex;

            internal TextureHandle tracedAmbientOcclusionTexture;
            internal TextureHandle prevTracedAmbientOcclusionTexture;
            internal TextureHandle shadowMomentsTex;
            internal TextureHandle prevShadowMomentsTex;
            internal TextureHandle motionVectorTexture;
            internal TextureHandle prevCameraDepthTexture;
            internal TextureHandle meanVarianceTexture;

            // Ray Tracing
            internal bool requireRayTracing;
            internal RayTracingShader rtrtShader;
            internal RayTracingAccelerationStructure rtas;
            internal uint dispatchRaySizeX;
            internal uint dispatchRaySizeY;
            internal ShaderVariablesRaytracing rayTracingCB;

            internal Matrix4x4 clipToPrevClipMatrix;
            internal bool enableDenoiser;
            internal bool enableEAWBlur;
        }

        /// <summary>
        /// Initialize the shared pass data.
        /// </summary>
        /// <param name="passData"></param>
        private void InitPassData(RenderGraph renderGraph, PassData passData, UniversalCameraData cameraData, UniversalResourceData resourceData, int historyFrameCount)
        {
            passData.requireRayTracing &= m_volumeSettings.rayTracing.value;

            passData.cameraData = cameraData;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.graphicsFormat = GraphicsFormat.R32_SFloat;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.enableRandomWrite = true;

            var width = cameraData.cameraTargetDescriptor.width;
            var height = cameraData.cameraTargetDescriptor.height;

            passData.cs = m_XeGTAO_CS;
            passData.classifyTilesKernel    = m_ClassifyTilesKernel;
            passData.prefilterNormalKernel  = m_PrefilterNormalKernel;
            passData.prefilterDepthKernel   = m_PrefilterDepthKernel;
            passData.mainPassKernel         = m_MainPassKernel;

            passData.denoiserCS = m_DenoiserCS;
            passData.bilateralHKernel = m_BilateralHKernel;
            passData.bilateralVKernel = m_BilateralVKernel;
            passData.accumulateKernel = m_AccumulateKernel;
            passData.eawKernel = m_EdgeAvoidATrousWaveletKernel;

            passData.numTilesX = RenderingUtils.DivRoundUp(width, c_XeGTAOTileSize);
            passData.numTilesY = RenderingUtils.DivRoundUp(height, c_XeGTAOTileSize);
            passData.preFilterDepthNumTilesX = RenderingUtils.DivRoundUp(width, c_PrefilterDepthTileSize);
            passData.preFilterDepthNumTilesY = RenderingUtils.DivRoundUp(height, c_PrefilterDepthTileSize);
            passData.mainPassTilesX = RenderingUtils.DivRoundUp(width, c_MainPassTileSize);
            passData.mainPassTilesY = RenderingUtils.DivRoundUp(height, c_MainPassTileSize);

            // Sptial temporal blue noise
            passData.camHistoryFrameCount = historyFrameCount;
            passData.blueNoiseArray = passData.requireRayTracing ? resourceData.blueNoiseUnitVec3Cosine : resourceData.blueNoise128RG;


            // Graphics Buffer
            var bufferSystem = GraphicsBufferSystem.instance;
            var dispatchIndirectBuffer = bufferSystem.GetGraphicsBuffer<uint>(GraphicsBufferSystemBufferID.ScreenSpaceAmbientOcclusionIndirect, 3, "AOdispatchIndirectBuffer", GraphicsBuffer.Target.IndirectArguments);
            passData.dispatchIndirectBuffer = renderGraph.ImportBuffer(dispatchIndirectBuffer);
            passData.tileListBuffer = renderGraph.CreateBuffer(new BufferDesc(passData.numTilesX * passData.numTilesY, sizeof(uint)));

            // Texture
            var geometricNormalDesc = desc;
            geometricNormalDesc.graphicsFormat = GraphicsFormat.R16G16_SNorm;
            passData.geometricNormalTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, geometricNormalDesc, "_GeometricNormalTexture", true);

            var viewDepthMipDesc = desc;
            viewDepthMipDesc.graphicsFormat = GraphicsFormat.R32_SFloat;
            viewDepthMipDesc.useMipMap = true;
            passData.viewDepthMipTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, viewDepthMipDesc, "_ViewDepthMipTexture", true);

            var aoTexDesc = desc;
            aoTexDesc.graphicsFormat = GraphicsFormat.R8_UNorm;
            passData.ambientOccusionTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, aoTexDesc, "_AmbientOcclusionTexture", true, Color.white);


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

            RTHandle tracedAmbientOcclusionTexture, prevTracedAmbientOcclusionTexture;
            ReAllocatedTracedAmbientOcclusionTextureIfNeeded(historyRTSystem, cameraData, aoTexDesc, out tracedAmbientOcclusionTexture, out prevTracedAmbientOcclusionTexture);
            passData.tracedAmbientOcclusionTexture = renderGraph.ImportTexture(tracedAmbientOcclusionTexture);
            passData.prevTracedAmbientOcclusionTexture = renderGraph.ImportTexture(prevTracedAmbientOcclusionTexture);

            var shadowMomentsDesc = desc;
            shadowMomentsDesc.colorFormat = RenderTextureFormat.RGB111110Float;
            RTHandle shadowMomentsTexture, prevshadowMomentsTexture;
            ReAllocatedAmbientOcclusionMomentsTextureIfNeeded(historyRTSystem, cameraData, shadowMomentsDesc, out shadowMomentsTexture, out prevshadowMomentsTexture);
            passData.shadowMomentsTex = renderGraph.ImportTexture(shadowMomentsTexture);
            passData.prevShadowMomentsTex = renderGraph.ImportTexture(prevshadowMomentsTexture);

            passData.motionVectorTexture = resourceData.motionVectorColor;

            var meanVarianceDesc = desc;
            meanVarianceDesc.colorFormat = RenderTextureFormat.RG32;
            passData.meanVarianceTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, meanVarianceDesc, "_MeanVarianceTexture", true, Color.white);



            MotionVectorsPersistentData motionData = null;
            if (cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                motionData = additionalCameraData.motionVectorsPersistentData;
            if (motionData != null)
            {
                passData.clipToPrevClipMatrix = motionData.previousViewProjection * Matrix4x4.Inverse(motionData.viewProjection);
            }

            // Denoise
            passData.enableDenoiser = m_volumeSettings.denoiser.value;
            passData.enableEAWBlur = m_volumeSettings.edgeAvoidingWaveletBlur.value;

            // Constant Buffer
            float invHalfTanFOV = -cameraData.GetProjectionMatrix()[1, 1];
            float aspectRatio = 1.0f / cameraData.aspectRatio;

            passData.constantBuffer._Intensity = m_volumeSettings.intensity.value;
            passData.constantBuffer._DirectLightingStrength = m_volumeSettings.directLightingStrength.value;
            passData.constantBuffer._Radius = m_volumeSettings.radius.value;

            passData.constantBuffer._RadiusMultiplier = m_volumeSettings.radiusMultiplier.value;
            passData.constantBuffer._FalloffRange = m_volumeSettings.falloffRange.value;
            passData.constantBuffer._SampleDistributionPower = m_volumeSettings.sampleDistributionPower.value;
            passData.constantBuffer._ThinOccluderCompensation = m_volumeSettings.thinOccluderCompensation.value;
            passData.constantBuffer._FinalValuePower = m_volumeSettings.finalValuePower.value;
            passData.constantBuffer._DepthMIPSamplingOffset = m_volumeSettings.depthMIPSamplingOffset.value;
            passData.constantBuffer._NDCToViewParams = new Vector4(
                    2.0f / (invHalfTanFOV * aspectRatio),
                    2.0f / (invHalfTanFOV),
                    1.0f / (invHalfTanFOV * aspectRatio),
                    1.0f / invHalfTanFOV
                );


            
            if (passData.requireRayTracing)
            {
                passData.dispatchRaySizeX = (uint)width;
                passData.dispatchRaySizeY = (uint)height;

                passData.rtrtShader = m_RTAOShader;
                passData.rtas = cameraData.rayTracingSystem.RequestAccelerationStructure();

                // RayTracing constant buffer
                {
                    var rayTracingSettings = VolumeManager.instance.stack.GetComponent<RayTracingSettings>();

                    passData.rayTracingCB = cameraData.rayTracingSystem.GetShaderVariablesRaytracingCB(new Vector2Int(width, height), rayTracingSettings);
                    passData.rayTracingCB._RaytracingRayMaxLength = m_volumeSettings.rayLength.value;
                    passData.rayTracingCB._RaytracingNumSamples = Mathf.Clamp(m_volumeSettings.raySampleCount.value, 1, 32);
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
            }
        }

        private static void ExecuteRayTracingPass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            var traceTargetTexture = data.enableDenoiser ? data.tracedAmbientOcclusionTexture : data.ambientOccusionTexture;

            // Classify
            using (new ProfilingScope(cmd, m_ClassifyTilesProfilingSampler))
            {
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_DispatchIndirectBuffer, data.dispatchIndirectBuffer);
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.SetComputeTextureParam(data.cs, data.classifyTilesKernel, ShaderConstants._AmbientOccusionTexture, traceTargetTexture);

                cmd.DispatchCompute(data.cs, data.classifyTilesKernel, data.numTilesX, data.numTilesY, 1);
            }

            // RayTracing Ambient Occlusion
            using (new ProfilingScope(cmd, m_RayTracingAmbientOcclusionProfilingSampler))
            {
                BlueNoiseSystem.BindSTBNParams(BlueNoiseTexFormat._UnitVec3_Cosine, cmd, data.rtrtShader, data.blueNoiseArray, data.camHistoryFrameCount);
                // Define the shader pass to use for the shadow pass
                cmd.SetRayTracingShaderPass(data.rtrtShader, "VisibilityDXR");

                // Set the acceleration structure for the pass
                cmd.SetRayTracingAccelerationStructure(data.rtrtShader, "_RaytracingAccelerationStructure", data.rtas);

                // Set ConstantBuffer
                ConstantBuffer.PushGlobal(cmd, data.rayTracingCB, RayTracingSystem._ShaderVariablesRaytracing);

                // Set Textures & Buffers
                //cmd.SetRayTracingBufferParam(data.rtrtShader, ShaderConstants._ShadowRayCoordBuffer, data.raysCoordBuffer);
                cmd.SetRayTracingTextureParam(data.rtrtShader, ShaderConstants._AmbientOcclusionTextureRW, traceTargetTexture);

                cmd.DispatchRays(data.rtrtShader, "SingleRayGen", data.dispatchRaySizeX, data.dispatchRaySizeY, 1, data.cameraData.camera);
            }

            if (!data.enableDenoiser)
                return;

            // Accumulate
            using (new ProfilingScope(cmd, m_AccumulateProfilingSampler))
            {
                var accumulateKernel = data.accumulateKernel;
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AOTermTexture, data.tracedAmbientOcclusionTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevAOTerm, data.prevTracedAmbientOcclusionTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AOMomentsTexture, data.shadowMomentsTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevAOMomentsTexture, data.prevShadowMomentsTex);

                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._CameraMotionVectorsTexture, data.motionVectorTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevCameraDepthTexture, data.prevCameraDepthTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AODenoisedTexture, data.ambientOccusionTexture);

                cmd.SetComputeMatrixParam(data.denoiserCS, ShaderConstants._ClipToPrevClipMatrix, data.clipToPrevClipMatrix);

                cmd.SetComputeBufferParam(data.denoiserCS, accumulateKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, accumulateKernel, data.dispatchIndirectBuffer, argsOffset: 0);
                //cmd.DispatchCompute(data.denoiserCS, accumulateKernel, (int)data.dispatchRaySizeX, (int)data.dispatchRaySizeY, 1);
            }

            if (!data.enableEAWBlur)
                return;

            // Edge-Avoiding A-Trous Wavelet (EAW) Filter
            using (new ProfilingScope(cmd, m_EdgeAvoidATrousWaveletProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._AODenoisedTexture, data.ambientOccusionTexture);


                cmd.SetComputeBufferParam(data.denoiserCS, data.eawKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, data.eawKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            }

            //using (new ProfilingScope(cmd, m_BilateralFilterProfilingSampler))
            //{
            //    cmd.SetComputeTextureParam(data.denoiserCS, data.bilateralHKernel, ShaderConstants._BilateralTexture, data.ambientOccusionTexture);
            //    cmd.SetComputeBufferParam(data.denoiserCS, data.bilateralHKernel, ShaderConstants.g_TileList, data.tileListBuffer);
            //    cmd.DispatchCompute(data.denoiserCS, data.bilateralHKernel, data.dispatchIndirectBuffer, argsOffset: 0);


            //    cmd.SetComputeTextureParam(data.denoiserCS, data.bilateralVKernel, ShaderConstants._BilateralTexture, data.ambientOccusionTexture);
            //    cmd.SetComputeBufferParam(data.denoiserCS, data.bilateralVKernel, ShaderConstants.g_TileList, data.tileListBuffer);
            //    cmd.DispatchCompute(data.denoiserCS, data.bilateralVKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            //}
        }

        private static void ExecuteComputePass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            var gtaoTargetTexture = data.enableDenoiser ? data.tracedAmbientOcclusionTexture : data.ambientOccusionTexture;

            BlueNoiseSystem.BindSTBNParams(BlueNoiseTexFormat._128RG, cmd, data.cs, data.mainPassKernel, data.blueNoiseArray, data.camHistoryFrameCount);
            // Push ConstantBuffer to compute shader
            ConstantBuffer.Push(cmd, data.constantBuffer, data.cs, ShaderConstants.ShaderVariablesScreenSpaceAmbientOcclusion);

            using (new ProfilingScope(cmd, m_PrefilterDepthProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.cs, data.prefilterDepthKernel, ShaderConstants._ViewDepthMip0, data.viewDepthMipTex, 0);
                cmd.SetComputeTextureParam(data.cs, data.prefilterDepthKernel, ShaderConstants._ViewDepthMip1, data.viewDepthMipTex, 1);
                cmd.SetComputeTextureParam(data.cs, data.prefilterDepthKernel, ShaderConstants._ViewDepthMip2, data.viewDepthMipTex, 2);
                cmd.SetComputeTextureParam(data.cs, data.prefilterDepthKernel, ShaderConstants._ViewDepthMip3, data.viewDepthMipTex, 3);
                cmd.SetComputeTextureParam(data.cs, data.prefilterDepthKernel, ShaderConstants._ViewDepthMip4, data.viewDepthMipTex, 4);

                cmd.DispatchCompute(data.cs, data.prefilterDepthKernel, data.preFilterDepthNumTilesX, data.preFilterDepthNumTilesY, 1);
            }

            //using (new ProfilingScope(cmd, m_PrefilterNormalProfilingSampler))
            //{
            //    cmd.SetComputeTextureParam(data.cs, data.prefilterNormalKernel, ShaderConstants._GeometricNormalTex, data.geometricNormalTex);

            //    cmd.DispatchCompute(data.cs, data.prefilterNormalKernel, data.mainPassTilesX, data.mainPassTilesY, 1);
            //}

            using (new ProfilingScope(cmd, m_ClassifyTilesProfilingSampler))
            {
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_DispatchIndirectBuffer, data.dispatchIndirectBuffer);
                cmd.SetComputeBufferParam(data.cs, data.classifyTilesKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.SetComputeTextureParam(data.cs, data.classifyTilesKernel, ShaderConstants._AmbientOccusionTexture, gtaoTargetTexture);

                cmd.DispatchCompute(data.cs, data.classifyTilesKernel, data.numTilesX, data.numTilesY, 1);
            }

            using (new ProfilingScope(cmd, m_MainPassProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.cs, data.mainPassKernel, ShaderConstants._ViewDepthMipTexture, data.viewDepthMipTex);
                cmd.SetComputeTextureParam(data.cs, data.mainPassKernel, ShaderConstants._AmbientOccusionTexture, gtaoTargetTexture);


                // Indirect buffer & dispatch
                cmd.SetComputeBufferParam(data.cs, data.mainPassKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.cs, data.mainPassKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            }

            if (!data.enableDenoiser)
                return;

            // Accumulate
            using (new ProfilingScope(cmd, m_AccumulateProfilingSampler))
            {
                var accumulateKernel = data.accumulateKernel;
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AOTermTexture, data.tracedAmbientOcclusionTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevAOTerm, data.prevTracedAmbientOcclusionTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AOMomentsTexture, data.shadowMomentsTex);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevAOMomentsTexture, data.prevShadowMomentsTex);

                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._CameraMotionVectorsTexture, data.motionVectorTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._PrevCameraDepthTexture, data.prevCameraDepthTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, accumulateKernel, ShaderConstants._AODenoisedTexture, data.ambientOccusionTexture);

                cmd.SetComputeMatrixParam(data.denoiserCS, ShaderConstants._ClipToPrevClipMatrix, data.clipToPrevClipMatrix);

                cmd.SetComputeBufferParam(data.denoiserCS, accumulateKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, accumulateKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            }

            if (!data.enableEAWBlur)
                return;

            // Edge-Avoiding A-Trous Wavelet (EAW) Filter
            using (new ProfilingScope(cmd, m_EdgeAvoidATrousWaveletProfilingSampler))
            {
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._MeanVarianceTexture, data.meanVarianceTexture);
                cmd.SetComputeTextureParam(data.denoiserCS, data.eawKernel, ShaderConstants._AODenoisedTexture, data.ambientOccusionTexture);


                cmd.SetComputeBufferParam(data.denoiserCS, data.eawKernel, ShaderConstants.g_TileList, data.tileListBuffer);
                cmd.DispatchCompute(data.denoiserCS, data.eawKernel, data.dispatchIndirectBuffer, argsOffset: 0);
            }
        }

        private static void ExecutePass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            if (data.requireRayTracing)
            {
                // Ray Tracing ao
                ExecuteRayTracingPass(data, context);
            }
            else
            {
                // Compute ao
                ExecuteComputePass(data, context);
            }

        }

        internal TextureHandle Render(RenderGraph renderGraph, ContextContainer frameData)
        {
            int historyFrameCount = 0;
            var historyRTSystem = HistoryFrameRTSystem.GetOrCreate(frameData.Get<UniversalCameraData>().camera);
            if (historyRTSystem != null)
                historyFrameCount = historyRTSystem.historyFrameCount;

            using (var builder = renderGraph.AddComputePass<PassData>("Render SS AmbientOcclusion", out var passData, ProfilingSampler.Get(URPProfileId.RenderSSAO)))
            {
                // Access resources
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // Ray Tracing
                passData.requireRayTracing = cameraData.supportedRayTracing && cameraData.rayTracingSystem.GetRayTracingState();

                // Setup passData
                InitPassData(renderGraph, passData, cameraData, resourceData, historyFrameCount);

                // Setup builder state
                builder.UseBuffer(passData.dispatchIndirectBuffer, AccessFlags.ReadWrite);
                builder.UseBuffer(passData.tileListBuffer, AccessFlags.ReadWrite);
                builder.UseTexture(passData.geometricNormalTex, AccessFlags.ReadWrite);
                builder.UseTexture(passData.ambientOccusionTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.viewDepthMipTex, AccessFlags.ReadWrite);
                builder.UseTexture(passData.blueNoiseArray, AccessFlags.Read);

                builder.UseTexture(passData.tracedAmbientOcclusionTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.prevTracedAmbientOcclusionTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.shadowMomentsTex, AccessFlags.ReadWrite);
                builder.UseTexture(passData.prevShadowMomentsTex, AccessFlags.ReadWrite);
                builder.UseTexture(passData.motionVectorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.prevCameraDepthTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.meanVarianceTexture, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(passData.requireRayTracing);
                //builder.EnableAsyncCompute(true);

                builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
                {
                    ExecutePass(data, context);
                });

                return passData.ambientOccusionTexture;
            }
        }

        static class ShaderConstants
        {
            public static readonly int g_DispatchIndirectBuffer = Shader.PropertyToID("g_DispatchIndirectBuffer");
            public static readonly int g_TileList = Shader.PropertyToID("g_TileList");

            public static readonly int _ViewDepthMipTexture = Shader.PropertyToID("_ViewDepthMipTexture");
            public static readonly int _ViewDepthMip0 = Shader.PropertyToID("_ViewDepthMip0");
            public static readonly int _ViewDepthMip1 = Shader.PropertyToID("_ViewDepthMip1");
            public static readonly int _ViewDepthMip2 = Shader.PropertyToID("_ViewDepthMip2");
            public static readonly int _ViewDepthMip3 = Shader.PropertyToID("_ViewDepthMip3");
            public static readonly int _ViewDepthMip4 = Shader.PropertyToID("_ViewDepthMip4");
            public static readonly int _GeometricNormalTex = Shader.PropertyToID("_GeometricNormalTex");
            public static readonly int _AmbientOccusionTexture = Shader.PropertyToID("_AmbientOccusionTexture");

            public static readonly int ShaderVariablesScreenSpaceAmbientOcclusion = Shader.PropertyToID("ShaderVariablesScreenSpaceAmbientOcclusion");
            public static readonly int _NDCToViewParams = Shader.PropertyToID("_NDCToViewParams");

            public static readonly int _AOTermTexture = Shader.PropertyToID("_AOTermTexture");
            public static readonly int _PrevAOTerm = Shader.PropertyToID("_PrevAOTerm");
            public static readonly int _AOMomentsTexture = Shader.PropertyToID("_AOMomentsTexture");
            public static readonly int _PrevAOMomentsTexture = Shader.PropertyToID("_PrevAOMomentsTexture");
            public static readonly int _CameraMotionVectorsTexture = Shader.PropertyToID("_CameraMotionVectorsTexture");
            public static readonly int _PrevCameraDepthTexture = Shader.PropertyToID("_PrevCameraDepthTexture");
            public static readonly int _MeanVarianceTexture = Shader.PropertyToID("_MeanVarianceTexture");
            public static readonly int _AODenoisedTexture = Shader.PropertyToID("_AODenoisedTexture");
            public static readonly int _ClipToPrevClipMatrix = Shader.PropertyToID("_ClipToPrevClipMatrix");

            public static readonly int _AmbientOcclusionTextureRW = Shader.PropertyToID("_AmbientOcclusionTextureRW");

            public static readonly int _BilateralTexture = Shader.PropertyToID("_BilateralTexture");
        }
    }
}
