// 墓地/除外格顶面漩涡（2026-10-13 特殊格道具）：叠加发光螺旋——刚性旋转 + 静态扭臂 + 有界呼吸摆动。
// 蓝=墓地/紫=除外（材质资产承载配色，Inspector 调 _Color/_Speed/_Arms/_Twist 即改手感）。
//
// 相位纪律（2026-10-13 摩尔纹修复定案）：phase 各分量必须有界——
// 旧版差速旋转 a += t*(内圈快外圈慢) 使内外圈相位差随 t 线性累积，螺旋臂越缠越紧直至亚像素摩尔纹；
// 现版密度=常数（_Twist×_Arms 圈数与时间无关）：旋转全半径同速（rot 经 fmod 2π 回绕，整数 _Arms 下
// sin 周期无损；非整数 _Arms 每圈有一帧接缝），扭度摆动为正弦有界振幅（永不累积缠绕）；
// 另以 fwidth(phase) 检测像素不可分辨的相位，臂对比塌向均值——将来调大 _Twist/_Arms 也不出硬纹。
// 面片=C# 侧 BattleZoneProps 摆平方块，边沿径向渐隐收圆形（半径≈0.72 < 六边形地块内径 0.814）。
Shader "Tide/BattleVortex"
{
    Properties
    {
        _Color ("Vortex Color", Color) = (0.30, 0.52, 1.0, 1)
        _Speed ("Speed (neg=reverse)", Range(-4, 4)) = 1.0
        _Arms ("Arms", Range(1, 8)) = 3
        _Twist ("Twist Rad", Range(0, 20)) = 7.5
        _WobbleSpeed ("Wobble Speed", Range(0, 4)) = 0.6
        _WobbleAmp ("Wobble Amp", Range(0, 1)) = 0.25
        _Intensity ("Intensity", Range(0.1, 4)) = 1.1
        _CoreBoost ("Core Boost", Range(0, 4)) = 1.8
        _AASoften ("AA Soften", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60" // 晚于地块菲涅尔（+50），叠于其上
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Vortex"

            Blend One One // additive：颜色即能量
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Speed;
                half _Arms;
                half _Twist;
                half _WobbleSpeed;
                half _WobbleAmp;
                half _Intensity;
                half _CoreBoost;
                half _AASoften;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0; // 0=中心，1=内切圆沿（外沿渐隐至无）
                float a = atan2(p.y, p.x);
                float t = _Time.y * _Speed;

                // 刚性旋转：全半径同速（_Speed 负值=反向）；fmod 2π 回绕保 phase 有界（整数 _Arms 下 sin 周期无损）
                float rot = fmod(t, TAU);
                // 有界呼吸：内圈扭度小幅正弦摆动（振幅≤_WobbleAmp，永不累积缠绕）
                float wobble = sin(fmod(t * _WobbleSpeed, TAU)) * _WobbleAmp * (1.0 - saturate(r));
                // 静态扭臂：径向相位恒定——螺旋密度=常数 _Twist×_Arms/2π 圈，与时间无关。
                // 旋转取 a−rot（2026-10-13 用户定案：a+rot 方向反了）：外端臂尾随旋转=内吸观感
                float phase = _Arms * (a - rot) + (r + wobble) * _Twist;

                float armVal = 0.5 + 0.5 * sin(phase);
                // 摩尔纹保险：像素级不可分辨（fwidth 大）的相位 → 臂对比塌向均值 0.5（_AASoften=0 关闭）
                float soften = saturate(1.0 - fwidth(phase) * _AASoften);
                armVal = lerp(0.5, armVal, soften);
                float arms = armVal * armVal; // 拉开臂与隙的对比

                float mask = smoothstep(1.0, 0.5, r); // 外沿收圆
                float core = exp(-r * 4.5);
                float glow = arms * mask * _Intensity + core * _CoreBoost;

                half3 col = _Color.rgb * glow + core * 0.30 * saturate(_Color.rgb); // 芯部偏白
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
