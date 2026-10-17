// 地牌锁色描边环（2026-10-17 用户定案）：地牌高亮不再用整块菲涅尔壳——颜色会与格面文字/元素图标
// 叠在一起，改为顶面六边形边缘围一圈 additive 色带；生物可行动扩散波仍走 BattleTileFresnel。
// 色带=恒定色×强度（平面色带无掠射角差异，恒亮比菲涅尔稳）；颜色由 C# MPB 按格覆写 _RingColor。
// Cull Off 双面渲染免绕序坑（固定俯拍机位永不从盘底看盘）。
Shader "Tide/BattleTileRing"
{
    Properties
    {
        _RingColor ("Color", Color) = (1, 1, 1, 1)
        _RingIntensity ("Intensity", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50" // 与菲涅尔壳同层：晚于地牌元素图标，叠加发光
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TileRing"

            Blend One One // additive：黑元素由 C# 侧以紫罗兰替代色传入（与菲涅尔壳同口径）
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half _RingIntensity;
            CBUFFER_END

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
                return half4(_RingColor.rgb * _RingIntensity, 1);
            }
            ENDHLSL
        }
    }
}
