// 教学高亮·目标蒙版平涂（2026-11-21 遮罩/高亮/暂停计划 v0.4 §4）：选择描边第一步——
// TutorialOutlineFeature 把目标 renderer 逐个 DrawRenderer 到蒙版 RT（R8），本 shader=平涂白
//（只关心覆盖形状，不关心光照/材质——拼装刚体模型/3D TMP/格面 mesh 同一口径）。
// 单 Pass、零 shader_feature（TideIconAlpha 教训）。ZTest Always：蒙版与深度无关。
Shader "Tide/TutorialOutlineFlat"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OutlineFlat"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
