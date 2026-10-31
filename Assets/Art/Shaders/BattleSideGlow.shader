// 半场操作提示面板（2026-10-30 定案：逐格菲涅尔壳退役——简化为每方一枚代码生成大包围 mesh，
// 形状=己方 676 三行+中线行地块剪影凸包、四周外扩一条地缝宽，见 BattleTileFx.BuildSideRegionMesh）。
// shader 只保留亮度过渡：两级渐变全部烘进 mesh UV——uv.x=边缘带（内缘 1→外缘 0，带宽=外扩缝宽，
// 四周软边不截断）、uv.y=纵向（自家后缘 0→中线侧外缘 1，方面板中线附近双双趋零不打架）。
// 叠加发光（Blend One One）只增不遮；Cull Off 免绕序；颜色由 C# MaterialPropertyBlock 按侧覆写。
Shader "Tide/BattleSideGlow"
{
    Properties
    {
        _SideColor ("Color", Color) = (0.45, 0.72, 1, 1)
        _Intensity ("Intensity", Range(0.05, 2)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40" // 早于格面元素图标（+50），图标叠在面板之上
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SideGlow"

            Blend One One // additive：颜色即能量
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SideColor;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 fade : TEXCOORD0; // x=边缘带(1内缘→0外缘) y=纵向(0自家后缘→1中线侧)
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.fade = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half k = IN.fade.x * (1.0 - IN.fade.y) * _Intensity; // 边缘带 × 纵向 亮度过渡
                return half4(_SideColor.rgb * k, 1);
            }
            ENDHLSL
        }
    }
}
