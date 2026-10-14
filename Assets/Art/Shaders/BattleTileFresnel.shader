// 地块高亮壳（2026-10-13 卡背方案退役改版；同日晚按用户反馈加扩散波模式）：
//   地牌=边缘菲涅尔（_WaveWeight=0）；生物可行动=顶面从格心向外扩散的淡蓝光波（_WaveWeight=1，MPB 覆写）。
// 叠加发光（Blend One One）只增不遮——套在 BattleHex 六棱柱外侧（C# 抬 4mm + 侧壁微外扩防叠面闪）。
// 波形：dist=片元到格心的水平距离（object space xz），环半径 r=frac(_Time·speed)·MaxR 循环推进，
// 三角脉冲锐化成环、随半径外扩衰减——顶面读作同心水波，侧壁在环扫过边缘时整圈亮（dist≈内切半径）。
// 颜色由 C# MaterialPropertyBlock 按格覆写 _FresnelColor；材质资产（BattleTile_Fresnel.mat）
// 承载全局手感参数——Inspector 调参即改全盘。
Shader "Tide/BattleTileFresnel"
{
    Properties
    {
        _FresnelColor ("Color", Color) = (1, 1, 1, 1)
        [Header(Fresnel Edge)]
        _Power ("Fresnel Power", Range(0.5, 8)) = 3
        _Intensity ("Fresnel Intensity", Range(0, 4)) = 1.4
        [Header(Diffuse Wave)]
        _WaveWeight ("Wave Weight (0=edge, 1=wave)", Range(0, 1)) = 0
        _WaveIntensity ("Wave Intensity", Range(0, 6)) = 2.2
        _WaveSpeed ("Wave Speed (cycles/s)", Range(0.1, 3)) = 0.8
        _WaveMaxR ("Wave Max Radius", Range(0.5, 2)) = 1.1
        _WaveWidth ("Wave Ring Width", Range(0.05, 0.6)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50" // 晚于地牌元素图标排序，叠加在图标之上
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TileGlow"

            Blend One One // additive：颜色即能量，黑元素由 C# 侧以紫色替代色传入
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FresnelColor;
                half _Power;
                half _Intensity;
                half _WaveWeight;
                half _WaveIntensity;
                half _WaveSpeed;
                half _WaveMaxR;
                half _WaveWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
                half centerDist : TEXCOORD2; // 片元到格心水平距离（object space xz；六边形角=1）
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS); // 未归一化，片元里归一
                OUT.centerDist = length(IN.positionOS.xz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 边缘菲涅尔（地牌档）：掠射角越强越亮
                half rim = pow(saturate(1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)))), _Power)
                           * _Intensity;

                // 扩散波（生物可行动档）：环从格心循环推出，外扩渐隐
                half ringR = frac(_Time.y * _WaveSpeed) * _WaveMaxR;
                half ring = saturate(1.0 - abs(IN.centerDist - ringR) / _WaveWidth);
                ring *= ring; // pow2 锐化
                half fade = 1.0 - saturate(ringR / _WaveMaxR) * 0.7; // 近强远淡
                half wave = ring * fade * _WaveIntensity;

                half k = lerp(rim, wave, _WaveWeight);
                return half4(_FresnelColor.rgb * k, 1);
            }
            ENDHLSL
        }
    }
}
