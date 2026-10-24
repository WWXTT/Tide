// YGOMonster.shader — YGO 方块怪兽专用材质：受光 + Alpha 硬裁切（叶片式）。
// 贴图 alpha < _Cutoff 丢弃（翅膀膜镂空/背景黑），无混合、无半透明；ZWrite On + Cull Off（穿洞可见背面）。
// Queue=Transparent → dbdrp 透明物体纯前向路径（免 GBuffer/DepthNormals Pass）；
// ShadowCaster/DepthOnly 与正向同裁，保持深度/阴影一致（HexGrass 同式）。
// 惯例：include 走官方 universal 路径（本工程解析到 dbdrp fork）；UnityPerMaterial 颜色一律
// float4（dbdrp 中 half4 破坏 SRP Batcher 布局）；BRG 要求 DOTS instancing pragma（HexWater 先例）。
Shader "Tide/YGOMonster"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _Color ("染色", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha 裁切阈值", Range(0, 1)) = 0.5

        [Header(World Y Dissolve)]
        _DissolveY ("消融线世界Y（1000=不生效）", Float) = 1000
        _DissolveBand ("消融噪声边宽(米)", Range(0.01, 0.5)) = 0.12
        _DissolveEdgeColor ("消融边元素色(a=强度)", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        // ---------- 正向：主光 Lambert + SH 环境色 + 雾，alpha 硬裁 ----------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            // 关键字集对齐 HexGrass（dbdrp fork 场景实修）：主光阴影含 SCREEN、柔影四档（fork 扩展）、
            // fork 的 SH 求值/集群光/渲染层变体——缺变体时场景质量档触发编译失败=粉屏
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _Cutoff;
                float _DissolveY;
                float _DissolveBand;
                float4 _DissolveEdgeColor;
            CBUFFER_END

            // 世界Y消融（2026-10-15 出场浮现）：k>0=线以上=裁掉；k∈[-band,0]=噪声过渡边带。
            // 按片元世界坐标比较→多模型共享本 shader 零串染；默认 _DissolveY=1000 全场在线下=恒不生效。
            float YgoDHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float YgoDNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(YgoDHash(i), YgoDHash(i + float3(1, 0, 0)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 0)), YgoDHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(YgoDHash(i + float3(0, 0, 1)), YgoDHash(i + float3(1, 0, 1)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 1)), YgoDHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float YgoDissolveK(float3 positionWS)
            {
                return (positionWS.y - _DissolveY) + (YgoDNoise(positionWS * 14.0) - 0.5) * _DissolveBand;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _Color;
                clip(tex.a - _Cutoff);
                // 消融惰性分支（2026-10-18 性能）：常态 _DissolveY=1000 时 3D 值噪声（8 hash+三线性/片元）
                // 三个 Pass 都白付——uniform 分支常态整段跳过；边带光随分支归零（ inactive 时语义同旧：全显+无边）
                half edge = 0.0h;
                [branch] if (_DissolveY < 900.0)
                {
                    float dissolveK = YgoDissolveK(IN.positionWS);
                    clip(-dissolveK); // 线以上（含噪声扰动）裁掉——与阴影/深度同裁
                    edge = saturate(1.0h + dissolveK / max(_DissolveBand, 1e-4));
                }

                // 双面渲染：单面片（翅膀膜）从背面看时法线翻转（VFACE），两视角受光均正确；
                // 从镂空看到的体内表面也随之按朝向相机受光（体素风可接受）
                Light mainLight = GetMainLight();
                half3 n = normalize(IN.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                half ndl = saturate(dot(n, mainLight.direction));
                // 压亮 1/π（2026-10-24 用户定案：同光照下 YGO 模型恒亮于 Lit 台面，压到 Lit 同亮）。
                // 依据=dbdrp 源码：Lit 漫反射直射（DeferredLighting.compute DisneyDiffuse）与间接
                //（diffuseFGD·SH）均带 1/π，本 shader 原为裸 Lambert 缺此因子故偏亮 ~π 倍。
                // RT 探针字节域配平数值随战斗光照阶段漂移（延迟管线编码污染读数，不可作锚），
                // 观感校准=肉眼：仍偏亮则下调本常数（曾测 0.16~0.32 区间），单点只改这一处。
                half3 col = tex.rgb * (mainLight.color * ndl + SampleSH(n)) * INV_PI;
                // 消融边淡光：紧贴线下沿的噪声亮带（元素色；默认黑 a=0=无）
                col += _DissolveEdgeColor.rgb * (edge * _DissolveEdgeColor.a);
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------- 阴影投射：同裁，保证镂空不出影 ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _Cutoff;
                float _DissolveY;
                float _DissolveBand;
                float4 _DissolveEdgeColor;
            CBUFFER_END

            float YgoDHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float YgoDNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(YgoDHash(i), YgoDHash(i + float3(1, 0, 0)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 0)), YgoDHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(YgoDHash(i + float3(0, 0, 1)), YgoDHash(i + float3(1, 0, 1)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 1)), YgoDHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float YgoDissolveK(float3 positionWS)
            {
                return (positionWS.y - _DissolveY) + (YgoDNoise(positionWS * 14.0) - 0.5) * _DissolveBand;
            }

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _Color.a;
                clip(a - _Cutoff);
                [branch] if (_DissolveY < 900.0) // 消融惰性分支（同 ForwardLit：常态跳过噪声）
                    clip(-YgoDissolveK(IN.positionWS)); // 消融线以上不出影
                return 0;
            }
            ENDHLSL
        }

        // ---------- 深度 prepass：同裁 ----------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _Cutoff;
                float _DissolveY;
                float _DissolveBand;
                float4 _DissolveEdgeColor;
            CBUFFER_END

            float YgoDHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float YgoDNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(YgoDHash(i), YgoDHash(i + float3(1, 0, 0)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 0)), YgoDHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(YgoDHash(i + float3(0, 0, 1)), YgoDHash(i + float3(1, 0, 1)), f.x),
                                 lerp(YgoDHash(i + float3(0, 1, 1)), YgoDHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float YgoDissolveK(float3 positionWS)
            {
                return (positionWS.y - _DissolveY) + (YgoDNoise(positionWS * 14.0) - 0.5) * _DissolveBand;
            }

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _Color.a;
                clip(a - _Cutoff);
                [branch] if (_DissolveY < 900.0) // 消融惰性分支（同 ForwardLit：常态跳过噪声）
                    clip(-YgoDissolveK(IN.positionWS)); // 消融线以上不写深度
                return 0;
            }
            ENDHLSL
        }
    }
}
