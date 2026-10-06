// 步骤引导/校验高亮框（2026-10-05 定案：替代 Outline 闪烁——Outline 网格复制在多层叠层下会互相染色）。
// 中心全透明（配合 raycastTarget=false 不挡点击），边缘红色脉冲描边；闪烁由 _Time 驱动，无 C# 逐帧控制。
//
// 几何自推导（v2，修"只有上方有边"）：不接收 C# 传入的矩形尺寸——四边形自身即矩形真值，
// 用 fwidth(uv) 现场换算"像素距中心"坐标与半尺寸，SDF 边缘恒与四边形边对齐（布局时序/父级缩放均免疫）。
// 片元只存在于四边形内——描边只有内半段可渲染（外半段无片元），C# 侧外扩 pad 补偿。
Shader "SynergyUI/GuideFrame"
{
    Properties
    {
        // uGUI 硬约束：CanvasRenderer 克隆 UI 材质时按 _MainTex 找贴图——缺此属性即报
        // "doesn't have a texture property '_MainTex'"；纯色描边用默认白图即可（乘法无害）。
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Edge Color", Color) = (1.0, 0.24, 0.24, 1)
        _EdgeWidth ("Edge Width (px)", Float) = 3
        _CornerRadius ("Corner Radius (px)", Float) = 8
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.3
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 1
        _FlashSpeed ("Flash Speed (rad/s)", Float) = 6.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            fixed4 _Color;
            float _EdgeWidth;
            float _CornerRadius;
            float _MinAlpha;
            float _MaxAlpha;
            float _FlashSpeed;
            float4 _ClipRect;
            sampler2D _MainTex;

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
                // 自推导像素几何：uv 每像素变化量（fwidth）≈ 1/四边形像素尺寸。
                // rel = 距中心的像素坐标（带符号）；halfPx = 半尺寸（px）——与四边形边严格对齐。
                float2 dUV = max(fwidth(i.uv), 1e-6);
                float2 rel = (i.uv - 0.5) / dUV;         // px，中心为原点
                float2 halfPx = 0.5 / dUV;               // 半宽/半高（px）

                float d = abs(sdRoundRect(rel, halfPx, _CornerRadius));
                float band = 1.0 - smoothstep(_EdgeWidth - 1.0, _EdgeWidth + 1.0, d);

                // 闪烁：shader 时间驱动（0.5+0.5·sin）映射到 [MinAlpha, MaxAlpha]
                float pulse = 0.5 + 0.5 * sin(_Time.y * _FlashSpeed);
                float a = band * lerp(_MinAlpha, _MaxAlpha, pulse);

                fixed4 tex = tex2D(_MainTex, i.uv); // 默认白图——纯色描边不受影响
                fixed4 col;
                col.rgb = _Color.rgb * i.color.rgb * tex.rgb;
                col.a = a * _Color.a * i.color.a * tex.a;

                clip(UnityGet2DClipping(i.worldPosition.xy, _ClipRect) - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
