using System;

namespace UnityEngine.Rendering.Universal
{

    [Serializable, VolumeComponentMenu("Lighting/Ambient Occlusion")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed partial class AmbientOcclusion : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Enable Ambient Occlusion.")]
        public BoolParameter enabled = new BoolParameter(false, BoolParameter.DisplayType.EnumPopup);

        [Tooltip("Use RayTracing for ao.")]
        public BoolParameter rayTracing = new BoolParameter(false);

        [Tooltip("Controls the strength of the ambient occlusion effect. Increase this value to produce darker areas.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1.0f, 0.0f, 4.0f);

        [Tooltip("Controls how much the ambient occlusion affects direct lighting.")]
        public ClampedFloatParameter directLightingStrength = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Sampling radius. Bigger the radius, wider AO will be achieved, risking to lose fine details and increasing cost of the effect due to increasing cache misses.")]
        public ClampedFloatParameter radius = new ClampedFloatParameter(0.5f, 0.01f, 5.0f);

        [Tooltip("Render ambient occlusion at half resolution for better performance.")]
        public BoolParameter halfResolution = new BoolParameter(false);


        // XeGTAO
        [Tooltip("Multiplies the 'Effect Radius' to match raytraced ground truth.")]
        public ClampedFloatParameter radiusMultiplier = new ClampedFloatParameter(1.457f, 0.3f, 3.0f);

        [Tooltip("Gently reduce sample impact as it gets out of 'Effect radius' bounds.")]
        public ClampedFloatParameter falloffRange = new ClampedFloatParameter(0.615f, 0.0f, 1.0f);

        [Tooltip("Make samples on a slice equally distributed (1.0) or focus more towards the center (>1.0).")]
        public ClampedFloatParameter sampleDistributionPower = new ClampedFloatParameter(2.0f, 1.0f, 3.0f);

        [Tooltip("Reduce the impact of samples further back to counter the bias from incomplete depth geometry data.")]
        public ClampedFloatParameter thinOccluderCompensation = new ClampedFloatParameter(0.3f, 0.0f, 0.7f);

        [Tooltip("Applies power function to the final value: occlusion = pow(occlusion, finalPower).")]
        public ClampedFloatParameter finalValuePower = new ClampedFloatParameter(2.2f, 0.5f, 5.0f);

        [Tooltip("Depth MIP sampling offset. Higher values increase performance but may cause instability and thin-object artifacts.")]
        public ClampedFloatParameter depthMIPSamplingOffset = new ClampedFloatParameter(3.3f, 0.0f, 30.0f);


        // RayTracing AO
        [Tooltip("Defines the layers that ray traced ambient occlusion should include.")]
        public LayerMaskParameter layerMask = new LayerMaskParameter(-1);

        [Tooltip("Max ray length.")]
        public ClampedFloatParameter rayLength = new ClampedFloatParameter(10.0f, 0.01f, 50.0f);

        [Tooltip("Number of ray traced AO samples per pixel.")]
        public ClampedIntParameter raySampleCount = new ClampedIntParameter(1, 1, 32);

        [Tooltip("Use ambient occlusion denoiser.")]
        public BoolParameter denoiser = new BoolParameter(true);

        [Tooltip("Use Edge-Avoiding A-Trous Wavelet (EAW) filter.")]
        public BoolParameter edgeAvoidingWaveletBlur = new BoolParameter(true);


        /// <inheritdoc/>
        public bool IsActive() => enabled.value;

        /// <inheritdoc/>
        public bool IsTileCompatible() => false;
    }
}