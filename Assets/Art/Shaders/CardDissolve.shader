// 手牌卡离场消融（2026-10-18）：卡被放置/使用（或弃/夺）离开手牌时，对账移除瞬间叠在
// 卡面上的烧蚀演出层。骨架沿用 FogEdgeGlow 的 uGUI 约束（_MainTex 硬约束 / UnityUI 裁剪 /
// ZTest 接 uGUI）与 fwidth 卡面本地像素换算（悬停放大/CanvasScaler 免疫）。
// 视觉 = 圆角矩形卡形内：噪声抖动的烧蚀亮线自下而上扫过（加法混合暖白），
// 线下方已烧尽=透明，线上方余温随噪声明灭（卡先"发烫"再烧掉）；
// 卡面本体由 C# 侧 CanvasGroup 同步淡出（线扫到哪儿卡面隐到哪儿）。
// 零变体（无 multi_compile/shader_feature）——artstatic 分包 shader 变体剥离口径。
Shader "SynergyUI/CardDissolve"
{
    Properties
    {
        // uGUI 硬约束：CanvasRenderer 克隆 UI 材质时按 _MainTex 找贴图——缺此属性即报
        // "doesn't have a texture property '_MainTex'"；纯 shader 绘制用默认白图即可（乘法无害）。
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Burn Noise (R, repeat)", 2D) = "gray" {}
        _Color ("Burn Color", Color) = (1.0, 0.78, 0.45, 1)
        _CardHalf ("Card Half Size (px)", Vector) = (100, 180, 0, 0)
        _QuadSize ("Quad Size (px)", Vector) = (216, 376, 0, 0)
        _CornerRadius ("Corner Radius (px)", Float) = 18
        _Progress ("Progress", Range(0, 1)) = 0
        _BurnWidth ("Burn Line Width (uv)", Float) = 0.16
        _NoiseJitter ("Front Noise Jitter (uv)", Float) = 0.22
        _EmberGlow ("Ember Glow", Float) = 0.16
        _NoiseScale ("Noise Period (px)", Float) = 46
        _Intensity ("Intensity", Float) = 1.6
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One // 加法混合：烧蚀辉光叠亮底下的卡面

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
            float _CornerRadius, _Progress, _BurnWidth, _NoiseJitter, _EmberGlow;
            float _NoiseScale, _Intensity, _Seed;
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

            // 圆角矩形有符号距离（p 相对中心；b=半尺寸；r=圆角）——与 FogEdgeGlow 同式
            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 屏幕像素几何（fwidth 自推导）→ 换回卡面本地像素（与 FogEdgeGlow 同法）
                float2 dUV = max(fwidth(i.uv), 1e-6);
                float2 halfPx = 0.5 / dUV;
                float px2local = _QuadSize.x / (2.0 * halfPx.x);
                float2 p = (i.uv - 0.5) / dUV * px2local;

                // 卡形遮罩：SDF 边缘羽化 2px（quad 比卡面外扩数 px，烧蚀光贴边不裁切）
                float d = sdRoundRect(p, _CardHalf.xy, _CornerRadius);
                float shape = 1.0 - smoothstep(-2.0, 2.0, d);

                // 静态噪声域（消融是快闪演出，不滚动；双层细粗混合出絮团感）
                float n = tex2D(_NoiseTex, (p + _Seed) / _NoiseScale).r;
                n = n * 0.72 + 0.28 * tex2D(_NoiseTex, (p * 2.3 - _Seed * 1.7) / _NoiseScale).r;

                // 烧蚀线：progress 0→1 从卡底外扫到卡顶外（两端外扩 3σ+半抖动，收尾无残光）；
                // 前沿按噪声上下抖动（h 掺 n）——烧出来的边是毛边不是直线（line 是 HLSL 保留字勿用）
                float span = 1.0 + _NoiseJitter + 6.0 * _BurnWidth;
                float burnLine = _Progress * span + 0.5 - span * 0.5;
                float h = i.uv.y + (n - 0.5) * _NoiseJitter;
                float front = burnLine - h; // >0 已烧过（线下方）/ <0 未烧（线上方）

                // 线体辉光（高斯）+ 余温（未烧区随噪声微亮——卡先发烫再烧掉）；
                // 起手 8% 进度整体淡入防突现（收尾由 line 出界自然归零）
                float band = exp(-front * front / (2.0 * _BurnWidth * _BurnWidth));
                float ember = saturate(-front * 2.0) * n * _EmberGlow;
                float warmup = saturate(_Progress / 0.08);

                fixed4 tex = tex2D(_MainTex, i.uv); // 默认白图——颜色全由 _Color 驱动
                fixed4 col;
                col.rgb = _Color.rgb * i.color.rgb * tex.rgb * _Intensity * (band * (0.7 + 0.6 * n) + ember);
                col.a = saturate((band + ember) * shape * warmup) * i.color.a * tex.a;

                clip(UnityGet2DClipping(i.worldPosition.xy, _ClipRect) - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
