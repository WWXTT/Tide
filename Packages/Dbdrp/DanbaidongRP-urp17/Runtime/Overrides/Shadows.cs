using System;

namespace UnityEngine.Rendering.Universal
{
    public enum ShadowScatterMode
    {
        None = 0,
        RampTexture = 1,
        SubSurface = 2,
    }

    [Serializable]
    public sealed class ShadowScatterModeParameter : VolumeParameter<ShadowScatterMode>
    {
        public ShadowScatterModeParameter(ShadowScatterMode value, bool overrideState = false) : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("Lighting/Shadows")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed partial class Shadows : VolumeComponent, IPostProcessComponent
    {
        private static Texture2D s_DefaultShadowRampTex;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (s_DefaultShadowRampTex == null)
            {
                var runtimeTextures = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeTextures>();
                s_DefaultShadowRampTex = runtimeTextures.defaultDirShadowRampTex;
            }
        }

        [Tooltip("Use RayTracing for opaques.")]
        public BoolParameter rayTracing = new BoolParameter(false);

        [Tooltip("Controls the ray length for ray traced directional shadows.")]
        public MinFloatParameter dirShadowsRayLength = new MinFloatParameter(1000.0f, 0.01f);

        [Tooltip("Penumbra controls shadows soften width.")]
        public ClampedFloatParameter dirShadowPenumbra = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        [Tooltip("Controls character self shadows layer.")]
        public LayerMaskParameter characterLayerMask = new LayerMaskParameter(0);

        [Tooltip("Controls character face/hair normal offset.")]
        public ClampedFloatParameter characterNormalOffset = new ClampedFloatParameter(0.001f, 0.0f, 0.05f);

        [Tooltip("Controls character face/hair half dir scale.")]
        public ClampedFloatParameter characterHalfDirScale = new ClampedFloatParameter(0.8f, 0.0f, 1.0f);

        [Tooltip("Shadow intensity.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);

        [Tooltip("Penumbra controls shadows soften width.")]
        public ClampedFloatParameter penumbra = new ClampedFloatParameter(1.0f, 0.001f, 3.0f);

        [Tooltip("Penumbra controls shadows soften width. (For Per Object Shadow)")]
        public ClampedFloatParameter perObjectShadowPenumbra = new ClampedFloatParameter(1.0f, 0.001f, 3.0f);

        [Tooltip("Shadow scatter mode.")]
        public ShadowScatterModeParameter shadowScatterMode = new ShadowScatterModeParameter(ShadowScatterMode.SubSurface);

        [Tooltip("Shadow ramp texture.")]
        public NoInterpTextureParameter shadowRampTex = new NoInterpTextureParameter(s_DefaultShadowRampTex);

        [Tooltip("Shadow subsurface R channel.")]
        public ClampedFloatParameter scatterR = new ClampedFloatParameter(0.3f, 0.01f, 1.0f);
        [Tooltip("Shadow subsurface G channel.")]
        public ClampedFloatParameter scatterG = new ClampedFloatParameter(0.1f, 0.01f, 1.0f);
        [Tooltip("Shadow subsurface B channel.")]
        public ClampedFloatParameter scatterB = new ClampedFloatParameter(0.07f, 0.01f, 1.0f);

        [Tooltip("Penumbra controls shadows scatter occlusion soften width.")]
        public ClampedFloatParameter occlusionPenumbra = new ClampedFloatParameter(1.0f, 0.001f, 3.0f);

        [Tooltip("Use ray tracing shadow denoiser.")]
        public BoolParameter denoiser = new BoolParameter(true);

        [Tooltip("Use Edge-Avoiding A-Trous Wavelet (EAW) filter.")]
        public BoolParameter edgeAvoidingWaveletBlur = new BoolParameter(true);

        /// <inheritdoc/>
        public bool IsActive() => true; // Always enable screenSpaceShadows.

        /// <inheritdoc/>
        [Obsolete("Unused #from(2023.1)", false)]
        public bool IsTileCompatible() => false;
    }
}