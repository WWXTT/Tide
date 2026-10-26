// 优先权底部面片（2026-10-15 用户定案终版：战场底部双长方「面片」承载扳机切换——盒体侧立面
// 出高光已否决，只留面）。水平面片 additive 梯度：靠己方一侧最亮、向中线衰减到 0——
// k = pow(saturate(1-v), _GradPow)（v=网格 UV 纵轴 0..1：0=己侧亮边、1=面板内缘（贴中线缝）灭）。
// 2026-10-26 灭线修复：旧式 (0.5-v)·2 把亮度在 v=0.5（世界 z≈0.8，己方第 1/2 行之间）就
// 夹断——中线实际在 z≈3.75，光只亮半场前 1/3 后 2/3 mesh 全黑（「没过渡到战场中间」根因，
// 且 pow&lt;1 时灭线残值高=亮带硬坠）。改 1-v 后衰减铺满整条 5m 跨度、在内缘贴中线处归 0。
// 2026-10-23 v4 蜂窝改版：原 Quad 用物体空间 y∈[-0.5,0.5]（-y·2 同式）+ 对手片 Y 转 180°
// 翻边；蜂窝是平铺 XZ 网格无 y 幅——梯度改读 UV，对手片的翻转烘进 UV（v=北缘 0）。
// 色与亮暗由 C# MaterialPropertyBlock 覆写
// _PanelColor（持有=本色/非持有=×DimK）。
// 2026-10-26 用户定案：_GradPow 1.6→1.0 纯线性——旧指数亮边掉太快外拖暗尾（过渡发硬），
// 线性=全跨度匀速衰减。
// 队列 Transparent+60：晚于地块菲涅尔壳(+50)与元素图标——additive 叠加不互斥，
// 地格玩法高光（单位可行动波/地牌锁色）保留各自表达，不再共用壳互相覆盖。
Shader "Tide/BattlePriorityPanel"
{
    Properties
    {
        _PanelColor ("Color", Color) = (1, 1, 1, 1)
        _GradPow ("Gradient Power", Range(0.25, 8)) = 1.0
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
                OUT.grad = saturate(1.0 - IN.uv.y); // 亮边 v=0 → 内缘贴中线 v=1 灭（2026-10-26 全跨度版）
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
