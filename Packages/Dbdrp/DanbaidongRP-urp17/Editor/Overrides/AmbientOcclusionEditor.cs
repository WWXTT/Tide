using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(AmbientOcclusion))]
    sealed class AmbientOcclusionEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Enabled;
        SerializedDataParameter m_RayTracing;

        // Core AO
        SerializedDataParameter m_Intensity;
        SerializedDataParameter m_DirectLightingStrength;
        SerializedDataParameter m_Radius;

        // XeGTAO parameters
        SerializedDataParameter m_RadiusMultiplier;
        SerializedDataParameter m_FalloffRange;
        SerializedDataParameter m_SampleDistributionPower;
        SerializedDataParameter m_ThinOccluderCompensation;
        SerializedDataParameter m_FinalValuePower;
        SerializedDataParameter m_DepthMIPSamplingOffset;
        SerializedDataParameter m_HalfResolution;

        // RayTracing AO
        SerializedDataParameter m_RayLength;
        SerializedDataParameter m_RaySampleCount;
        SerializedDataParameter m_LayerMask;
        SerializedDataParameter m_Denoiser;
        SerializedDataParameter m_EdgeAvoidingWaveletBlur;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<AmbientOcclusion>(serializedObject);
            m_Enabled = Unpack(o.Find(x => x.enabled));
            m_RayTracing = Unpack(o.Find(x => x.rayTracing));

            m_Intensity = Unpack(o.Find(x => x.intensity));
            m_DirectLightingStrength = Unpack(o.Find(x => x.directLightingStrength));
            m_Radius = Unpack(o.Find(x => x.radius));

            m_RadiusMultiplier = Unpack(o.Find(x => x.radiusMultiplier));
            m_FalloffRange = Unpack(o.Find(x => x.falloffRange));
            m_SampleDistributionPower = Unpack(o.Find(x => x.sampleDistributionPower));
            m_ThinOccluderCompensation = Unpack(o.Find(x => x.thinOccluderCompensation));
            m_FinalValuePower = Unpack(o.Find(x => x.finalValuePower));
            m_DepthMIPSamplingOffset = Unpack(o.Find(x => x.depthMIPSamplingOffset));
            m_HalfResolution = Unpack(o.Find(x => x.halfResolution));

            m_RayLength = Unpack(o.Find(x => x.rayLength));
            m_RaySampleCount = Unpack(o.Find(x => x.raySampleCount));
            m_LayerMask = Unpack(o.Find(x => x.layerMask));
            m_Denoiser = Unpack(o.Find(x => x.denoiser));
            m_EdgeAvoidingWaveletBlur = Unpack(o.Find(x => x.edgeAvoidingWaveletBlur));
        }

        void RayTracedAmbientOcclusionGUI()
        {
            var pipelineAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            if (pipelineAsset == null || !pipelineAsset.supportsRayTracing)
            {
                EditorGUILayout.HelpBox("Check RayTracing in pipeline asset (" + pipelineAsset.name +") rendering settings.", MessageType.Error, true);
                return;
            }
            PropertyField(m_Intensity);
            PropertyField(m_DirectLightingStrength);
            PropertyField(m_RayLength);

            EditorGUILayout.Space(5);
            PropertyField(m_HalfResolution);
            PropertyField(m_RaySampleCount);
            PropertyField(m_LayerMask);
            PropertyField(m_Denoiser);
            PropertyField(m_EdgeAvoidingWaveletBlur);
        }

        void GTAOGUI()
        {
            PropertyField(m_Intensity);
            PropertyField(m_DirectLightingStrength);
            PropertyField(m_Radius);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("XeGTAO Advanced Settings");

            PropertyField(m_RadiusMultiplier);
            PropertyField(m_FalloffRange);
            PropertyField(m_SampleDistributionPower);
            PropertyField(m_ThinOccluderCompensation);
            PropertyField(m_FinalValuePower);
            PropertyField(m_DepthMIPSamplingOffset);

            EditorGUILayout.Space(5);
            PropertyField(m_Denoiser);
            PropertyField(m_EdgeAvoidingWaveletBlur);
            PropertyField(m_HalfResolution);
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_Enabled);
            PropertyField(m_RayTracing);
            EditorGUILayout.Space(10);

            // Flag to track if the ray tracing parameters were displayed
            bool rayTracingSettingsDisplayed = m_RayTracing.overrideState.boolValue
                && m_RayTracing.value.boolValue;

            // The rest of the ray tracing UI is only displayed if the asset supports ray tracing and the checkbox is checked.
            if (rayTracingSettingsDisplayed)
            {
                RayTracedAmbientOcclusionGUI();
            }
            else
            {
                GTAOGUI();
            }
            
        }
    }
}