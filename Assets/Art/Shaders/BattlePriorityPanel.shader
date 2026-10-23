// 优先权底部面片（2026-10-15 用户定案终版：战场底部双长方「面片」承载扳机切换——盒体侧立面
// 出高光已否决，只留面）。水平面片 additive 梯度：靠己方一侧最亮、向中线衰减到 0——
// k = pow(saturate((0.5-v)·2), _GradPow)（v=网格 UV 纵轴 0..1：0=己侧亮边、0.5=中线灭。
// 2026-10-23 v4 蜂窝改版：原 Quad 用物体空间 y∈[-0.5,0.5]（-y·2 同式）+ 对手片 Y 转 180°
// 翻边；蜂窝是平铺 XZ 网格无 y 幅——梯度改读 UV，对手片的翻转烘进 UV（v=北缘 0），
// 视觉梯度与原版逐点一致）。色与亮暗由 C# MaterialPropertyBlock 覆写
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
                float2 uv : TEXCOORD0;
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
                OUT.grad = saturate(-(IN.uv.y - 0.5) * 2.0);
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
