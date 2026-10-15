// 可发动手牌卡的边缘雾光（2026-10-15 定案：替代「底环呼吸+光点巡游」——炉石式浓浓雾气边缘光）。
// 骨架沿用 GuideFrame 的 uGUI 约束（_MainTex 硬约束 / UnityUI 裁剪 / ZTest 接 uGUI）。
// 视觉 = 圆角矩形 SDF 边缘光带 × 双层平铺噪声相向漂移 × 加法混合：
// 边缘最亮（峰值略外移出卡缘）、向外指数衰减呈雾状扩散、quad 边前 smoothstep 归零（防硬切口）；
// 两层噪声（大格慢速+小格快速）交叉=雾气翻涌絮团。全程 _Time 自驱（C# 只缓动 _Fade 淡入淡出）。
// 几何经 fwidth 换算回卡面本地像素：卡面缩放（悬停放大/对手扇缩小）与 CanvasScaler 均免疫。
Shader "SynergyUI/FogEdgeGlow"
{
    Properties
    {
        // uGUI 硬约束：CanvasRenderer 克隆 UI 材质时按 _MainTex 找贴图——缺此属性即报
        // "doesn't have a texture property '_MainTex'"；纯 shader 绘制用默认白图即可（乘法无害）。
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Fog Noise (R, repeat)", 2D) = "gray" {}
        _Color ("Glow Color", Color) = (0.38, 1.0, 0.58, 1)
        _CardHalf ("Card Half Size (px)", Vector) = (100, 180, 0, 0)
        _QuadSize ("Quad Size (px)", Vector) = (172, 252, 0, 0)
        _Margin ("Fog Margin (px)", Float) = 36
        _CornerRadius ("Corner Radius (px)", Float) = 18
        _FogFalloff ("Fog Falloff (px)", Float) = 12
        _PeakShift ("Peak Shift (px)", Float) = 4
        _InnerWidth ("Inner Glow Width (px)", Float) = 26
        _NoiseScale ("Noise Period (px)", Float) = 52
        _ScrollSpeed ("Scroll Speed", Float) = 0.055
        _PulseSpeed ("Pulse Speed", Float) = 2.3
        _Intensity ("Intensity", Float) = 1.5
        _Seed ("Seed", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One // 加法混合：雾光叠亮底下的画面

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            fixed4 _Color;
            float4 _CardHalf, _QuadSize;
            float _Margin, _CornerRadius, _FogFalloff, _PeakShift, _InnerWidth;
            float _NoiseScale, _ScrollSpeed, _PulseSpeed, _Intensity, _Seed, _Fade;
            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _ClipRect;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD2;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            // 圆角矩形有符号距离（p 相对中心；b=半尺寸；r=圆角）
            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 屏幕像素几何（fwidth 自推导）→ 换回卡面本地像素：SDF 卡缘恒贴真实卡面边界。
                float2 dUV = max(fwidth(i.uv), 1e-6);
                float2 halfPx = 0.5 / dUV;                       // quad 半尺寸（屏幕 px）
                float px2local = _QuadSize.x / (2.0 * halfPx.x); // 本地 px / 屏幕 px（等比缩放）
                float2 p = (i.uv - 0.5) / dUV * px2local;        // 距中心坐标（卡面本地 px）

                float d = sdRoundRect(p, _CardHalf.xy, _CornerRadius);

                // 雾带：峰值外移 _PeakShift（雾抱住卡缘）、向外指数扩散（浓→稀的雾气剖面），
                // 到 quad 边（d→_Margin）前归零；内侧渐起（从卡角透明区/抗锯齿缝透出点亮边）。
                float outer = exp(-max(d - _PeakShift, 0.0) / _FogFalloff);
                float edgeFade = 1.0 - smoothstep(_Margin - 9.0, _Margin - 2.0, d);
                float inner = smoothstep(-_InnerWidth, -3.0, d);
                float band = outer * edgeFade * inner;

                // 翻涌雾气：两层平铺噪声相向漂移（小格快向右上 / 大格慢向左下）；
                // n1 乘方压暗低值、n2 抬底，remap 到 [0.5,1.3]——常亮雾体+滚动明暗絮团。
                float t = _Time.y * _ScrollSpeed;
                float n1 = tex2D(_NoiseTex, (p + _Seed) / _NoiseScale + t * float2(0.9, 0.35)).r;
                float n2 = tex2D(_NoiseTex, (p * 0.45 - _Seed * 1.7) / _NoiseScale - t * float2(0.5, 0.8)).r;
                float n = saturate(n1 * n1 * 0.55 + n2 * 0.8);
                float flicker = lerp(0.5, 1.3, n);

                // 整体呼吸（逐卡 _Seed 错相，避免全手牌同步胀缩）
                float pulse = 0.82 + 0.18 * sin(_Time.y * _PulseSpeed + _Seed);

                fixed4 tex = tex2D(_MainTex, i.uv); // 默认白图——颜色全由 _Color 驱动
                fixed4 col;
                col.rgb = _Color.rgb * i.color.rgb * tex.rgb * _Intensity;
                col.a = saturate(band * flicker * pulse * _Fade * _Color.a * i.color.a * tex.a);

                clip(UnityGet2DClipping(i.worldPosition.xy, _ClipRect) - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
