namespace UnityEngine.Rendering.Universal
{
    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    unsafe struct ShaderVariablesScreenSpaceAmbientOcclusion
    {
        public Vector4 _NDCToViewParams;

        public float _Intensity;
        public float _DirectLightingStrength;
        public float _Radius;
        public float _RadiusMultiplier;

        public float _FalloffRange;
        public float _SampleDistributionPower;
        public float _ThinOccluderCompensation;
        public float _FinalValuePower;

        public float _DepthMIPSamplingOffset;
    }
}