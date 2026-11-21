// 教学遮罩·分离高斯模糊（2026-11-21 遮罩/高亮/暂停计划 v0.3 §3）：crispRT → 1/4 分辨率半 RT 上
// H/V 各一 pass（Graphics.Blit 逐方向调用，_Direction 切向）。单 Pass、混合写死、零 shader_feature
// （TideIconAlpha 教训：变体会被剥离出包）。半径按强度档在 C# 侧换算成 texel 数传入。
Shader "Tide/TutorialBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _Direction ("Blur Direction (xy)", Vector) = (1, 0, 0, 0)
        _Radius ("Radius (texels)", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent" // 逐帧离屏 Blit 用，不进场景队列——占位防透明排序干扰
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TutorialBlur"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Direction;
                float _Radius;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

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

            // 9-tap 高斯（对称权重和=1）；离屏全屏 quad，无光照无深度
            half4 frag(Varyings IN) : SV_Target
            {
                float2 step2 = _Direction.xy * _MainTex_TexelSize.xy * _Radius;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * 0.2270270270;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + step2 * 1.0) * 0.1945945946;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - step2 * 1.0) * 0.1945945946;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + step2 * 2.0) * 0.1216216216;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - step2 * 2.0) * 0.1216216216;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + step2 * 3.0) * 0.0540540541;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - step2 * 3.0) * 0.0540540541;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + step2 * 4.0) * 0.0162162162;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - step2 * 4.0) * 0.0162162162;
                return c;
            }
            ENDHLSL
        }
    }
}
