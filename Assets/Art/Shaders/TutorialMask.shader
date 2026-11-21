// 教学遮罩·全屏遮罩图挖窗（2026-11-21 遮罩/高亮/暂停计划 v0.3 §3）：mask-img 全屏 RawImage 采样
// 1/4 分辨率模糊 RT，窗位圆角矩形 SDF 挖洞——窗外=模糊图不透明（连 UI 一起盖掉）、窗内=全透
//（露真实 3D+UI 可点）。几何量全走**屏幕像素**（C# 逐帧传窗 AABB/圆角/羽化，抗画布缩放漂移）；
// raycastTarget 由节点关掉（窗内点击穿透到真实 UI）。单 Pass、混合写死、零 shader_feature
//（TideIconAlpha 教训）。脉动不做（窗是教学读点，稳定优于呼吸）。
Shader "Tide/TutorialMask"
{
    Properties
    {
        _MainTex ("Blurred Scene", 2D) = "black" {}
        _ScreenSize ("Screen Size (px)", Vector) = (1920, 1080, 0, 0)
        _WinCenter ("Window Center (px, 左下原点)", Vector) = (960, 540, 0, 0)
        _WinHalfPx ("Window Half Size (px)", Vector) = (160, 90, 0, 0)
        _RadiusPx ("Corner Radius (px)", Float) = 12
        _FeatherPx ("Edge Feather (px)", Float) = 24
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "TutorialMask"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScreenSize;
                float4 _WinCenter;
                float4 _WinHalfPx;
                float _RadiusPx;
                float _FeatherPx;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // 圆角矩形 SDF（px 空间）：负=窗内。全屏 RawImage 的 uv(0,0)=屏幕左下，与 C# 窗 AABB 同轴。
            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = (IN.uv - _WinCenter.xy) * _ScreenSize.xy;
                float r = max(_RadiusPx, 0.0);
                float2 q = abs(p) - _WinHalfPx.xy + r;
                float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float f = max(_FeatherPx, 0.001);
                float cover = smoothstep(-f, f, sd); // 0=窗内全透 / 1=窗外显模糊图
                half4 blur = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return half4(blur.rgb, cover);
            }
            ENDHLSL
        }
    }
}
