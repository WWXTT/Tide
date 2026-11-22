// 教学遮罩·成片拷贝 + 目标剪影（2026-11-21 遮罩计划 v0.4 实测修正；第七轮定稿）：TutorialMaskFeature
// 在 AfterRenderingPostProcessing 用本材质的**两个 pass**把同一帧写进持久缓存 RT：
// · Pass 0（SceneCopy）：全屏 quad 把相机成片（activeColorTexture，含 bloom）纯拷进缓存——
//   **RGB=成片，A 恒 0**（alpha 通道让给剪影）；
// · Pass 1（SilhouetteAlpha）：遮罩目标 renderer 平涂，**只写 alpha=1**（ColorMask 0 0 0 1），
//   于是同一张纹理里 alpha 通道 = 目标剪影。
// 为什么两个 pass 同一次 pass 里画：剪影与成片共用同一相机矩阵/同一渲染目标/同一朝向，
// **不存在任何 uv 翻转或屏幕矩形换算**——第七轮修复（用户实测"镂空位置不对"）的根因就是
// 旧做法用 CommandBuffer 手工 SetViewProjectionMatrices(相机原始投影) 直绘到独立 RT，
// 少了渲染进 RT 必需的 GPU Y 翻转（GL.GetGPUProjectionMatrix(, true)），剪影相对成片上下镜像。
// 现在剪影随成片同源，位置不可能再错位。单 Pass 内零 shader_feature（TideIconAlpha 教训）。
Shader "Tide/TutorialSceneCopy"
{
    Properties
    {
        // 成片缓存（R16G16B16A16_SFloat，保 alpha）——与相机色同尺寸，1 texel = 1 屏幕 px
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // ---------------- Pass 0：成片拷贝（RGB 有效，alpha 清 0 让位剪影） ----------------
        Pass
        {
            Name "SceneCopy"

            Blend Off
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TutorialSceneSrcTex); // 全局绑定（feature 侧 SetGlobalTexture 绑 activeColorTexture）
            SAMPLER(sampler_TutorialSceneSrcTex);

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
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0); // 全屏 quad：位置即裁剪空间
                OUT.uv = IN.uv;
                return OUT;
            }

            // A 恒 0：alpha 通道专门留给剪影（Pass 1 写入 1）
            float4 frag(Varyings IN) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_TutorialSceneSrcTex, sampler_TutorialSceneSrcTex, IN.uv).rgb;
                return float4(c, 0.0);
            }
            ENDHLSL
        }

        // ---------------- Pass 1：目标剪影（只写 alpha，RGB 不动） ----------------
        Pass
        {
            Name "SilhouetteAlpha"

            Blend Off
            ColorMask A      // 只写 A：RGB 保护 Pass 0 拷进来的成片（ShaderLab 语法=单掩码，不能写 0/0/0/1）
            ZWrite Off
            ZTest Always     // 剪影与深度无关（被遮挡部件也要算进轮廓，与描边蒙版同口径）
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 相机视/投影矩阵由管线在本 pass 前设好（与成片同源）——不要在这里手工设矩阵
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return float4(0.0, 0.0, 0.0, 1.0); // 只 alpha=1（RGB 已被 ColorMask 屏蔽）
            }
            ENDHLSL
        }
    }
}
