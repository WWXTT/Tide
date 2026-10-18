// 图标平贴专用透明无光照 shader（2026-10-18 立案）：元素图标/格前三键/结界图标这批平贴面片用。
// 缘起：Dbdrp(Lit/Unlit) 的透明路径是 shader_feature_local_fragment——YooAsset 把 shader 集中
// unityshaders.bundle、材质在别包，变体编译时看不到材质关键字 → 真机透明变体被剥离成不透明
// （黑色区域不透明），编辑器按需现编变体永远显示正常（假阳性）。本 shader 单 Pass 混合写死、
// 零 shader_feature 变体，出包确定性；属性名沿用 URP 的 _BaseMap/_BaseColor，材质换绑零迁移。
Shader "Tide/IconAlpha"
{
    Properties
    {
        [MainTexture] _BaseMap ("Icon", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "IconAlpha"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                return c;
            }
            ENDHLSL
        }
    }
}
