Shader "Custom/HexTerrain"
{
    Properties
    {
        [Header(Terrain Texture Arrays)]
        _TerrainAlbedoArray("Albedo Array", 2DArray) = "" {}
        _TerrainNormalArray("Normal Array", 2DArray) = "" {}
        _TerrainHeightArray("Height Array", 2DArray) = "" {}
        _TerrainMetallicSmoothnessArray("MetallicSmoothness Array", 2DArray) = "" {}
        _TerrainOcclusionArray("Occlusion Array", 2DArray) = "" {}

        [Header(Height Blend)]
        // offset/strength sets the visible transition width in strip-space.
        // ~1.0 spreads the mutual invasion across the whole 0.2 connection strip.
        _HeightBlendStrength("Height Blend Strength", Range(0.01, 10.0)) = 0.5
        _HeightBlendOffset("Height blend Offset", Range(0.0, 1.0)) = 0.5

        [Header(PBR)]
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 1.0
        _NormalScale("Normal Scale", Range(0.0, 2.0)) = 1.0
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex HexTerrainVert
            #pragma fragment HexTerrainFrag

            // -------------------------------------
            // 可选纹理数组：keyword 关闭（数组未绑定）时走中性回退，
            // 避免采样空纹理槽的未定义垃圾值（彩色噪点/闪烁）
            #pragma shader_feature_local_fragment _TERRAIN_NORMAL_MAP
            #pragma shader_feature_local_fragment _TERRAIN_HEIGHT_MAP
            #pragma shader_feature_local_fragment _TERRAIN_MS_MAP
            #pragma shader_feature_local_fragment _TERRAIN_OCCLUSION_MAP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Fog.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "HexTerrainInput.hlsl"
            #include "HexTerrainForwardPass.hlsl"

            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  ShadowCaster pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  DepthOnly pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthOnlyVertex(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(DepthVaryings input) : SV_TARGET
            {
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  DepthNormals pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DepthNormalsVertex(DNAttributes input)
            {
                DNVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                return output;
            }

            half4 DepthNormalsFragment(DNVaryings input) : SV_TARGET
            {
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
                #endif

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                #else
                half3 packedNormalWS = half3(NormalizeNormalPerPixel(input.normalWS));
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                uint renderingLayers = EncodeMeshRenderingLayer();
                return half4(packedNormalWS, (half)renderingLayers / 255.0);
                #else
                return half4(packedNormalWS, 0);
                #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  GBuffer pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex HexTerrainGBufferVert
            #pragma fragment HexTerrainGBufferFrag

            // 可选纹理数组（与 ForwardLit 一致）：未绑定走中性回退
            #pragma shader_feature_local_fragment _TERRAIN_NORMAL_MAP
            #pragma shader_feature_local_fragment _TERRAIN_HEIGHT_MAP
            #pragma shader_feature_local_fragment _TERRAIN_MS_MAP
            #pragma shader_feature_local_fragment _TERRAIN_OCCLUSION_MAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            #include "HexTerrainInput.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Shadows.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct GBAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;            // splat 权重
                float2 uvCorrection : TEXCOORD0;      // 坡面 UV 补偿向量（mesh 逐顶点烘焙）
                float3 terrainIndices : TEXCOORD1;    // splat 3 个地形索引
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GBVaryings
            {
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 terrainData  : TEXCOORD2;      // splat 权重
                half3 vertexSH      : TEXCOORD3;
                half fogFactor      : TEXCOORD4;
                float4 shadowCoord  : TEXCOORD5;
                #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD6;
                #endif
                float4 terrainIndices : TEXCOORD7;    // xyz：splat 索引；w：uvCorrection.y
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            GBVaryings HexTerrainGBufferVert(GBAttributes input)
            {
                GBVaryings output = (GBVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                // alpha 槽位搭 uvCorrection.x（TEXCOORD 插值器已满，借道传递）
                output.terrainData = float4(input.color.rgb, input.uvCorrection.x);
                output.terrainIndices = float4(input.terrainIndices, input.uvCorrection.y);

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactor = fogFactor;

                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
                    GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

                output.shadowCoord = GetShadowCoord(vertexInput);
                return output;
            }

            FragmentOutput HexTerrainGBufferFrag(GBVaryings input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
                #endif

                uint3 idx = DecodeSplatIndices(input.terrainIndices);
                float3 weights = input.terrainData.rgb;
                weights /= (weights.x + weights.y + weights.z + 1e-4);

                float3 normalWS = SafeNormalize(input.normalWS);

                // 坡面补偿 UV（逐顶点烘焙，防陡壁拉伸）：插值连续不逐面错位，平地恒 0
                float2 uvCorrection = float2(input.terrainData.a, input.terrainIndices.w);
                float2 uv = ChunkUV(input.positionWS.xz + uvCorrection);

                half3 finalAlbedo, finalNormalTS;
                half finalMetallic, finalSmoothness, finalOcclusion;
                SampleSplatSurface(uv, idx, weights,
                    finalAlbedo, finalNormalTS, finalMetallic, finalSmoothness, finalOcclusion);

                float3x3 TBN = CreateTangentFrame(normalWS);
                float3 surfNormal = SafeNormalize(TransformTangentToWorld(finalNormalTS, TBN));

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = finalMetallic * _Metallic;
                surfaceData.smoothness = finalSmoothness * _Smoothness;
                surfaceData.normalTS = finalNormalTS;
                surfaceData.occlusion = lerp(1.0, finalOcclusion, _OcclusionStrength);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(surfNormal);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_SCREEN_SPACE_IRRADIANCE)
                inputData.bakedGI = SAMPLE_GI(input.positionCS.xy);
                #elif defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(0, 0, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(0);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                inputData.bakedGI = SAMPLE_GI(input.vertexSH,
                    GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
                    inputData.viewDirectionWS, input.positionCS.xy,
                    input.probeOcclusion, inputData.shadowMask);
                #else
                inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(0);
                #endif

                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, half3(0,0,0),
                    surfaceData.smoothness, surfaceData.alpha, brdfData);

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
                half3 gi = GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion,
                    inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);

                return BRDFDataToGbuffer(brdfData, inputData, surfaceData.smoothness,
                    surfaceData.emission + gi, surfaceData.occlusion);
            }
            ENDHLSL
        }
    }

    // 按可选纹理数组是否绑定自动开关 keyword（HexTerrainShaderGUI 位于 Assets/Editor）
    CustomEditor "HexTerrainShaderGUI"

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
