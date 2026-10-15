// 优先权底部面片（2026-10-15 用户定案终版：战场底部双长方「面片」承载扳机切换——盒体侧立面
// 出高光已否决，只留面）。水平面片 additive 梯度：靠己方一侧最亮、向中线衰减到 0——
// k = pow(saturate(-y·2), _GradPow)（Quad 网格纵轴 y∈[-0.5,0.5]：-0.5=己侧边 1、+0.5=中线 0；
// 对手片 Y 转 180° 复用同式=亮边换到北侧）。色与亮暗由 C# MaterialPropertyBlock 覆写
// _PanelColor（持有=本色/非持有=×DimK）。
// 队列 Transparent+60：晚于地块菲涅尔壳(+50)与元素图标——additive 叠加不互斥，
// 地格玩法高光（单位可行动波/地牌锁色）保留各自表达，不再共用壳互相覆盖。
Shader "Tide/BattlePriorityPanel"
{
    Properties
    {
        _PanelColor ("Color", Color) = (1, 1, 1, 1)
        _GradPow ("Gradient Power", Range(0.25, 8)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PriorityPanel"

            Blend One One // additive：黑=不可见，暗置=低能量色
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PanelColor;
                half _GradPow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half grad : TEXCOORD0; // 纵向梯度因子：己侧边=1 → 中线=0
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.grad = saturate(-IN.positionOS.y * 2.0);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half k = pow(IN.grad, _GradPow);
                return half4(_PanelColor.rgb * k, 1);
            }
            ENDHLSL
        }
    }
}
