Shader "Custom/HexTerrainDebug"
{
    Properties
    {
        [Header(Debug Visualization)]
        [Enum(Off, 0, Albedo, 1, ChunkUV, 2, WorldPos, 3, NormalWS, 4, SplatIndices, 5, SplatWeights, 6, WeightSum, 7, IndexBounds, 8, ChunkSizeProbe, 9, Height, 10, MipLevel, 11)]
        _DebugMode("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma shader_feature_local_fragment _TERRAIN_HEIGHT_MAP
            #pragma shader_feature_local_fragment _TERRAIN_MS_MAP
            #pragma shader_feature_local_fragment _TERRAIN_OCCLUSION_MAP
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"
            #include "HexTerrainDebugCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float3 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 splatIndices : TEXCOORD2;
                float3 splatWeights : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.splatIndices = input.uv1;
                output.splatWeights = input.color.rgb;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                HexDebugInput dbg;
                dbg.positionWS = input.positionWS;
                dbg.normalWS = input.normalWS;
                dbg.splatIndicesRaw = input.splatIndices;
                dbg.splatWeights = input.splatWeights;

                return half4(HexTerrainDebugColor(dbg), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma shader_feature_local_fragment _TERRAIN_HEIGHT_MAP
            #pragma shader_feature_local_fragment _TERRAIN_MS_MAP
            #pragma shader_feature_local_fragment _TERRAIN_OCCLUSION_MAP
            #include_with_pragmas "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/UnityGBuffer.hlsl"
            #include "HexTerrainDebugCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float3 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 splatIndices : TEXCOORD2;
                float3 splatWeights : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.splatIndices = input.uv1;
                output.splatWeights = input.color.rgb;
                return output;
            }

            FragmentOutput frag(Varyings input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                HexDebugInput dbg;
                dbg.positionWS = input.positionWS;
                dbg.normalWS = input.normalWS;
                dbg.splatIndicesRaw = input.splatIndices;
                dbg.splatWeights = input.splatWeights;

                half3 debugColor = HexTerrainDebugColor(dbg);

                // GBuffer: albedo 写入 emission，让调试颜色直接点亮
                BRDFData brdfData = (BRDFData)0;
                brdfData.albedo = 0;
                brdfData.diffuse = 0;
                brdfData.specular = 0;
                brdfData.reflectivity = 0;
                brdfData.perceptualRoughness = 1;
                brdfData.roughness = 1;
                brdfData.roughness2 = 1;
                brdfData.grazingTerm = 0;
                brdfData.normalizationTerm = 0;
                brdfData.roughness2MinusOne = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                return BRDFDataToGbuffer(brdfData, inputData, 0, debugColor, 1);
            }
            ENDHLSL
        }
    }
}
