// 河湖水面（计划 4.5）：透明混合、ZWrite Off、Cull Off（俯视/水下双面可见），
// 世界 UV 微波纹（sin 扰动亮度，备用扩展：深度渐变/stencil 合并）。
// 管线 = DanbaidongRP（URP 17.5 同 GUID 顶替），include 走 danbaidong 包路径；
// BRG（Entities Graphics/GPU Resident Drawer）要求 shader 必须存在 DOTS_INSTANCING_ON
// 变体（UnityPerMaterial 全为批次常量也不例外），声明方式与 HexTerrain 一致：
// include_with_pragmas DOTS.hlsl。
Shader "HexMap/Water"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.13, 0.38, 0.62, 0.62)
        [Header(Ripples)]
        _WaveStrength("Wave Strength", Range(0.0, 0.5)) = 0.06
        _WaveFrequency("Wave Frequency", Float) = 0.8
        _WaveSpeed("Wave Speed", Float) = 0.7
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
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _WaveStrength;
                half _WaveFrequency;
                half _WaveSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 世界 UV 微波纹：双频 sin/cos 叠加扰动亮度
                half w = sin(IN.positionWS.x * _WaveFrequency + _Time.y * _WaveSpeed)
                       * cos(IN.positionWS.z * _WaveFrequency * 1.31 - _Time.y * _WaveSpeed * 0.83);
                half4 c = _BaseColor;
                c.rgb += w * _WaveStrength;
                return c;
            }
            ENDHLSL
        }
    }
}
