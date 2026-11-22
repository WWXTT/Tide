// 教学遮罩·开窗合成（2026-11-21 遮罩计划 v0.4·第七轮定稿）：**blit 链尾**——模糊链成品（RGB，1/4
// 分辨率）与**目标剪影**（同帧成片缓存的 alpha 通道）合成到 qFinal：
// · 剪影来源 = TutorialMaskFeature 的成片缓存（Pass 0 拷 RGB、Pass 1 只写 alpha=1 到目标剪影处）——
//   与相机成片同一 pass/同一矩阵/同一朝向，**没有独立剪影 RT、没有屏幕矩形换算**；
//   旧做法（手工 CommandBuffer + 相机原始投影绘独立 RT）缺 RT 的 GPU Y 翻转，导致镂空上下镜像
//   （2026-11-21 用户实测"镂空位置不对"），已废弃。
// · 本 pass 对 alpha 做 9 tap 均值（_FeatherTexel 控软边宽度）：剪影处 alpha≈1 → 输出 alpha=0
//   （透明，露出真实 3D+UI）；其余 alpha→1（模糊图不透明，连 UI 一起盖掉）。
// 显示空间：D3D/Vulkan 后缓冲 top-down，RG/Blit 链产纹理直显上下颠倒——_UVFlip（C# =
// SystemInfo.graphicsUVStartsAtTop）翻 Y；模糊图与剪影**同用一个翻转后的 uv**（同轴不裂）。
// 成品直接写 alpha 洞、**Blend Off**（与目标混合会把上一帧洞内容留下=残影洞）。
// 单 Pass、混合写死、零 shader_feature（TideIconAlpha 教训）。无矩形 SDF（第六轮用户定案：
// 去掉方块限制，透明区域=目标轮廓）。
Shader "Tide/TutorialMask"
{
    Properties
    {
        _MainTex ("Blurred Scene", 2D) = "black" {}
        _SceneTex ("Scene Copy (alpha=silhouette)", 2D) = "black" {}
        _FeatherTexel ("Silhouette Softness (scene texels)", Float) = 12
        _UVFlip ("UV Y Flip (D3D/Vulkan=1)", Float) = 1
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

            // **必须 Blend Off**：本 pass 是 blit 链尾，写的是"成品"（RGB=模糊图、A=洞）。
            // 若与目标混合（SrcAlpha OneMinusSrcAlpha），洞处 a=0 → 目的地保留上一帧旧内容，
            // 洞就变成"上一帧/上一个目标的残影"（历轮"镂空位置不对"的第二个根因）。
            Blend Off
            ZWrite Off
            ZTest Always // blit 链尾合成口（非 UGUI 材质）——全屏 quad 无深度语义
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FeatherTexel;
                float _UVFlip;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SceneTex);
            SAMPLER(sampler_SceneTex);
            float4 _SceneTex_TexelSize;

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

            // 透明区域=目标剪影：成片缓存 alpha（1=目标）9 tap 均值 → 软边；剪影处输出 alpha=0
            half4 frag(Varyings IN) : SV_Target
            {
                float2 suv = float2(IN.uv.x, lerp(IN.uv.y, 1.0 - IN.uv.y, _UVFlip)); // 真屏幕 uv（左下原点）
                half3 blur = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, suv).rgb;
                // 软边半径（成片 texel；_SceneTex 与屏幕 1:1，故 = 屏幕 px）——均值而非 max：
                // max 只会把洞缩小且是硬边，均值给的是跨剪影边界的对称软过渡
                float2 t = _SceneTex_TexelSize.xy * max(_FeatherTexel * 0.5, 0.0);
                float m = SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2( t.x, 0)).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2(-t.x, 0)).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2(0,  t.y)).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2(0, -t.y)).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2( t.x,  t.y) * 0.7071).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2(-t.x,  t.y) * 0.7071).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2( t.x, -t.y) * 0.7071).a;
                m += SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, suv + float2(-t.x, -t.y) * 0.7071).a;
                float sil = smoothstep(0.30, 0.70, m * (1.0 / 9.0));
                return half4(blur, 1.0 - sil); // 剪影处全透（露真实场景）/ 其余=模糊图
            }
            ENDHLSL
        }
    }
}
