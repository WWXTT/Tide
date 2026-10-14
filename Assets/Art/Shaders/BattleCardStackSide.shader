// 卡组格卡堆侧面（2026-10-13 特殊格道具）：世界 Y 定条纹——每 0.02m 一张卡纸边，
// 卡堆高度伸缩不改纹距（缩放只拉几何，条纹锚在世界空间）；缝=深色纸缝，卡纸亮度逐张微抖
// （hash 卡序号）模拟真实卡堆参差。无光照依赖（URP unlit + 定向假光近似战场 45° 日出），
// 顶面卡背由 BattleCardBack_Back_Mat 单独出（subMesh 分材质，见 BattleZoneProps）。
Shader "Tide/BattleCardStackSide"
{
    Properties
    {
        _PaperColor ("Paper Color", Color) = (0.85, 0.80, 0.72, 1)
        _GapColor ("Seam Color", Color) = (0.14, 0.12, 0.10, 1)
        _CardHeight ("Card Height", Range(0.005, 0.05)) = 0.02
        _SeamFrac ("Seam Fraction", Range(0.05, 0.5)) = 0.22
        _Jitter ("Card Jitter", Range(0, 0.3)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StackSide"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PaperColor;
                half4 _GapColor;
                half _CardHeight;
                half _SeamFrac;
                half _Jitter;
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
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 假定向光（近似战场日出 45° 俯角）：顶面亮、四侧明暗有别，立体感不依赖管线光照
                half3 n = normalize(IN.normalWS);
                half ndl = saturate(dot(n, normalize(half3(0.35, 0.80, -0.50))));
                half face = 0.55 + 0.5 * ndl;

                // 世界 Y 卡纸条纹：f<SeamFrac=纸缝；fwidth 抗远距摩尔纹
                float coord = IN.positionWS.y / _CardHeight;
                float card = floor(coord);
                float f = coord - card;
                float aa = clamp(fwidth(coord) * 1.5, 0.02, 0.45);
                float seam = smoothstep(_SeamFrac + aa, _SeamFrac - aa, f);

                float h = frac(sin(card * 74.7) * 43758.55);
                half3 paper = _PaperColor.rgb * (face * (1.0 - _Jitter * h));
                half3 col = lerp(paper, _GapColor.rgb * face, seam);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
