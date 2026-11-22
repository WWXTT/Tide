// 教学高亮·描边合成（2026-11-21 遮罩/高亮/暂停计划 v0.3 §4）：选择描边第二步——
// 读蒙版 RT，8 向采样取 max=膨胀，edge=膨胀-蒙版（目标外圈光环），以 HDR 色（hdr>bloom 阈值 0.8）
// additive 叠回相机色——注入点早于后处理，bloom 把边缘晕开成"边缘光"（计划口径）。
// 脉动=shader 内 _Time 驱动（C# 只设 _PulseSpeed/_PulseAmp 档位，不做逐帧驱动）。
// 单 Pass、零 shader_feature（TideIconAlpha 教训）。全屏 quad（RenderingUtils.fullscreenMesh，
// 位置即裁剪空间），蒙版与相机色同为中间 RT 同向免翻转。
Shader "Tide/TutorialOutlineEdge"
{
    Properties
    {
        _MaskTex ("Target Mask", 2D) = "black" {}
        _OutlineColor ("Color", Color) = (1, 0.84, 0.35, 1)
        _Hdr ("HDR Boost", Float) = 2.5
        _WidthTexel ("Width (mask texels)", Float) = 2
        _PulseSpeed ("Pulse Speed (Hz, 0=恒亮)", Float) = 1
        _PulseAmp ("Pulse Amplitude", Float) = 0.25
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
            Name "OutlineEdge"

            Blend One One // additive：只加光环不遮底
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _Hdr;
                float _WidthTexel;
                float _PulseSpeed;
                float _PulseAmp;
            CBUFFER_END

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MaskTex_TexelSize;

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
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0); // 全屏 quad：位置即裁剪空间
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 t = _MaskTex_TexelSize.xy * _WidthTexel;
                float mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).r;
                float m = mask;
                // 8 向膨胀（等权 max——形状稳，免两 pass 分离卷积）
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2( t.x, 0)).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2(-t.x, 0)).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2(0,  t.y)).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2(0, -t.y)).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2( t.x,  t.y) * 0.7071).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2(-t.x,  t.y) * 0.7071).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2( t.x, -t.y) * 0.7071).r);
                m = max(m, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv + float2(-t.x, -t.y) * 0.7071).r);
                float edge = saturate(m - mask); // 蒙版外圈（内圈=目标本体不涂）
                if (edge <= 0.0) return half4(0, 0, 0, 0);
                half pulse = 1.0 + _PulseAmp * sin(_Time.y * _PulseSpeed * 6.2831853);
                half3 rgb = _OutlineColor.rgb * _Hdr * pulse;
                return half4(rgb * edge, edge);
            }
            ENDHLSL
        }
    }
}
