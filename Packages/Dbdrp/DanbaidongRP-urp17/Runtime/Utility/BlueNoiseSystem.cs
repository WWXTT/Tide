using System;
using UnityEngine.Assertions;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    public enum BlueNoiseTexFormat
    {
        _128R,
        _128RG,
        _UnitVec3_Cosine   // cosine-weighted 3D unit vectors
    }

    /// <summary>
    /// A bank of nvidia pre-generated spatiotemporal blue noise textures.
    /// ref: https://github.com/NVIDIAGameWorks/SpatiotemporalBlueNoiseSDK/tree/main
    /// </summary>
    public sealed class BlueNoiseSystem : IDisposable
    {
        public static BlueNoiseSystem m_Instance = null;
        public static int blueNoiseArraySize = 64;

        readonly Texture2D[] m_Textures128R;
        readonly Texture2D[] m_Textures128RG;
        readonly Texture2D[] m_TexturesUnitVec3Cosine;

        Texture2DArray m_TextureArray128R;
        Texture2DArray m_TextureArray128RG;
        Texture2DArray m_TextureArrayUnitVec3Cosine;

        RTHandle m_TextureHandle128R;
        RTHandle m_TextureHandle128RG;
        RTHandle m_TextureHandleUnitVec3Cosine;

        /// <summary>
        /// Spatiotemporal blue noise valuse[0,1] with R single-channel 128x128 textures.
        /// </summary>
        public Texture2D[] textures128R { get { return m_Textures128R; } }

        /// <summary>
        /// Spatiotemporal blue noise valuse[0,1] with RG multi-channel 128x128 textures.
        /// </summary>
        public Texture2D[] textures128RG { get { return m_Textures128RG; } }

        /// <summary>
        /// Spatiotemporal blue noise unitvec3 cosine-weighted (RGB) 128x128 textures.
        /// </summary>
        public Texture2D[] texturesUnitVec3Cosine { get { return m_TexturesUnitVec3Cosine; } }

        public Texture2DArray textureArray128R { get { return m_TextureArray128R; } }
        public Texture2DArray textureArray128RG { get { return m_TextureArray128RG; } }
        public Texture2DArray textureArrayUnitVec3Cosine { get { return m_TextureArrayUnitVec3Cosine; } }

        public RTHandle textureHandle128R { get { return m_TextureHandle128R; } }
        public RTHandle textureHandle128RG { get { return m_TextureHandle128RG; } }
        public RTHandle textureHandleUnitVec3Cosine { get { return m_TextureHandleUnitVec3Cosine; } }


        public static readonly int s_STBNVec1Texture = Shader.PropertyToID("_STBNVec1Texture");
        public static readonly int s_STBNVec2Texture = Shader.PropertyToID("_STBNVec2Texture");
        public static readonly int s_STBNUnitVec3CosineTexture = Shader.PropertyToID("_STBNUnitVec3CosineTexture");
        public static readonly int s_STBNIndex = Shader.PropertyToID("_STBNIndex");

        private BlueNoiseSystem(UniversalRenderPipelineRuntimeTextures runtimeTextures)
        {
            InitTextures(128, TextureFormat.R16, runtimeTextures.blueNoise128RTex, out m_Textures128R, out m_TextureArray128R, out m_TextureHandle128R, "_STBNVec1Texture");
            InitTextures(128, TextureFormat.RG32, runtimeTextures.blueNoise128RGTex, out m_Textures128RG, out m_TextureArray128RG, out m_TextureHandle128RG, "_STBNVec2Texture");
            InitTextures(128, TextureFormat.RGBAFloat, runtimeTextures.blueNoiseUnitVec3CosineTex, out m_TexturesUnitVec3Cosine, out m_TextureArrayUnitVec3Cosine, out m_TextureHandleUnitVec3Cosine, "_STBNUnitVec3CosineTexture");
        }

        /// <summary>
        /// Initialize BlueNoiseSystem.
        /// </summary>
        /// <param name="resources"></param>
        internal static void Initialize(UniversalRenderPipelineRuntimeTextures runtimeTextures)
        {
            if (m_Instance == null)
                m_Instance = new BlueNoiseSystem(runtimeTextures);
        }

        /// <summary>
        /// Try get blueNoise instance, could be null if not initialized before.
        /// </summary>
        /// <returns>null if none initialized</returns>
        public static BlueNoiseSystem TryGetInstance()
        {
            return m_Instance;
        }


        public static void ClearAll()
        {
            if (m_Instance != null)
                m_Instance.Dispose();

            m_Instance = null;
        }

        /// <summary>
        /// Cleanups up internal textures.
        /// </summary>
        public void Dispose()
        {
            CoreUtils.Destroy(m_TextureArray128R);
            CoreUtils.Destroy(m_TextureArray128RG);
            CoreUtils.Destroy(m_TextureArrayUnitVec3Cosine);

            RTHandles.Release(m_TextureHandle128R);
            RTHandles.Release(m_TextureHandle128RG);
            RTHandles.Release(m_TextureHandleUnitVec3Cosine);

            m_TextureArray128R = null;
            m_TextureArray128RG = null;
            m_TextureArrayUnitVec3Cosine = null;
        }

        static void InitTextures(int size, TextureFormat format, Texture2D[] sourceTextures, out Texture2D[] destination, out Texture2DArray destinationArray, out RTHandle destinationHandle, string name = "BlueNoise")
        {
            Assert.IsNotNull(sourceTextures);

            int len = sourceTextures.Length;

            Assert.IsTrue(len > 0);

            destination = new Texture2D[len];
            destinationArray = new Texture2DArray(size, size, len, format, false, true);
            destinationArray.hideFlags = HideFlags.HideAndDontSave;
            destinationArray.name = name;

            for (int i = 0; i < len; i++)
            {
                var noiseTex = sourceTextures[i];

                // Fail safe; should never happen unless the resources asset is broken
                if (noiseTex == null)
                {
                    destination[i] = Texture2D.whiteTexture;
                    continue;
                }

                destination[i] = noiseTex;
                Graphics.CopyTexture(noiseTex, 0, 0, destinationArray, i, 0);
            }

            destinationHandle = RTHandles.Alloc(destinationArray);
        }

        /// <summary>
        /// Bind spatiotemporal blue noise texture with given index (loop in blueNoiseArraySize).
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="textureIndex"></param>
        public static void BindSTBNParams(BlueNoiseTexFormat format, ComputeCommandBuffer cmd, ComputeShader computeShader, int kernel, TextureHandle texture, int frameCount)
        {
            int texID;
            switch (format)
            {
                case BlueNoiseTexFormat._128R:
                    texID = s_STBNVec1Texture;
                    break;
                case BlueNoiseTexFormat._128RG:
                    texID = s_STBNVec2Texture;
                    break;
                case BlueNoiseTexFormat._UnitVec3_Cosine:
                    texID = s_STBNUnitVec3CosineTexture;
                    break;
                default:
                    texID = s_STBNVec1Texture;
                    break;
            }

            cmd.SetComputeTextureParam(computeShader, kernel, texID, texture);
            cmd.SetComputeIntParam(computeShader, s_STBNIndex, frameCount % blueNoiseArraySize);
        }

        public static void BindSTBNParams(BlueNoiseTexFormat format, ComputeCommandBuffer cmd, RayTracingShader rayTracingShader, TextureHandle texture, int frameCount)
        {
            int texID;
            switch (format)
            {
                case BlueNoiseTexFormat._128R:
                    texID = s_STBNVec1Texture;
                    break;
                case BlueNoiseTexFormat._128RG:
                    texID = s_STBNVec2Texture;
                    break;
                case BlueNoiseTexFormat._UnitVec3_Cosine:
                    texID = s_STBNUnitVec3CosineTexture;
                    break;
                default:
                    texID = s_STBNVec1Texture;
                    break;
            }

            cmd.SetRayTracingTextureParam(rayTracingShader, texID, texture);
            cmd.SetRayTracingIntParam(rayTracingShader, s_STBNIndex, frameCount % blueNoiseArraySize);
        }
    }
}
