// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[TTFE_DrawerTitle] _TTFETERRAINSNOWSHADER( "(TTFE) TERRAIN SNOW SHADER", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS( "TEXTURE MAPS", Float ) = 0
		[NoScaleOffset][Space (10)][TTFE_Drawer_SingleLineTexture] _ColorMapProjection( "Color Map Projection", 2D ) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture] _NormalMap( "Normal Map", 2D ) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _HeatMap( "Heat Map", 2D ) = "white" {}
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTURESETTINGS( "TEXTURE SETTINGS", Float ) = 0
		[Header((Color Map))] _ColorMapIntensity( "Color Map Intensity", Range( 0, 1 ) ) = 1
		[Toggle] _ColorMap( "Color Map", Float ) = 1
		[Header((Normal))] _NormalIntensity( "Normal Intensity", Range( 0, 1 ) ) = 1
		[Toggle] _TerrainNormalMap( "Terrain Normal Map", Float ) = 0
		_SmoothnessIntensity( "Smoothness Intensity", Range( 0, 1 ) ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _SEASONSETTINGS( "SEASON SETTINGS", Float ) = 0
		[Header((Slope))] _SlopeMax( "Slope Max", Float ) = 0
		_SlopeMin( "Slope Min", Float ) = 0
		_SnowOffset( "Snow Offset", Float ) = 0
		_SnowScale( "Snow Scale", Float ) = 0
		[Toggle] _SnowSlope( "Snow Slope", Float ) = 1
		[Toggle] _NormalMapBasedSnow( "Normal Map Based Snow", Float ) = 0
		[Toggle( _TERRAIN_INSTANCED_PERPIXEL_NORMAL )] _EnableInstancedPerPixelNormal( "Enable Instanced Per-Pixel Normal", Float ) = 0
		[HideInInspector] _TerrainHolesTexture( "_TerrainHolesTexture", 2D ) = "white" {}
		[HideInInspector] _Control( "Control", 2D ) = "white" {}
		[HideInInspector] _Splat0( "Splat0", 2D ) = "white" {}
		[HideInInspector] _Normal0( "Normal0", 2D ) = "bump" {}
		[HideInInspector] _NormalScale0( "NormalScale0", Float ) = 1
		[HideInInspector] _Mask0( "Mask0", 2D ) = "gray" {}
		[HideInInspector] _Metallic0( "Metallic0", Range( 0, 1 ) ) = 0
		[HideInInspector] _Smoothness0( "Smoothness 0", Range( 0, 1 ) ) = 0
		[HideInInspector] _Splat1( "Splat1", 2D ) = "white" {}
		[HideInInspector] _Normal1( "Normal1", 2D ) = "bump" {}
		[HideInInspector] _NormalScale1( "NormalScale1", Float ) = 1
		[HideInInspector] _Mask1( "Mask1", 2D ) = "gray" {}
		[HideInInspector] _Metallic1( "Metallic1", Range( 0, 1 ) ) = 0
		[HideInInspector] _Smoothness1( "Smoothness1", Range( 0, 1 ) ) = 0
		[HideInInspector] _Splat2( "Splat2", 2D ) = "white" {}
		[HideInInspector] _Normal2( "Normal2", 2D ) = "bump" {}
		[HideInInspector] _NormalScale2( "NormalScale2", Float ) = 1
		[HideInInspector] _Mask2( "Mask2", 2D ) = "gray" {}
		[HideInInspector] _Metallic2( "Metallic2", Range( 0, 1 ) ) = 0
		[HideInInspector] _Smoothness2( "Smoothness2", Range( 0, 1 ) ) = 0
		[HideInInspector] _Splat3( "Splat3", 2D ) = "white" {}
		[HideInInspector] _Normal3( "Normal3", 2D ) = "bump" {}
		[HideInInspector] _NormalScale3( "_NormalScale3", Float ) = 1
		[HideInInspector] _Mask3( "Mask3", 2D ) = "gray" {}
		[HideInInspector] _Metallic3( "Metallic3", Range( 0, 1 ) ) = 0
		[HideInInspector] _Smoothness3( "Smoothness3", Range( 0, 1 ) ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _ADVANCEDSETTINGS( "ADVANCED SETTINGS", Float ) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		//_InstancedTerrainNormals("Instanced Terrain Normals", Float) = 1.0

		//[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		//[ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		//[ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
	}

	SubShader
	{
		LOD 0

		

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-100" "UniversalMaterialType"="Lit" "TerrainCompatible"="True" }

		Cull Back
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#if ( SHADER_TARGET > 35 ) && defined( SHADER_API_GLES3 )
			#error For WebGL2/GLES3, please set your shader target to 3.5 via SubShader options. URP shaders in ASE use target 4.5 by default.
		#endif

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForwardOnly" "TerrainCompatible"="True" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#ifdef UNITY_COLORSPACE_GAMMA//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(2.0, 2.0, 2.0, 2.0)//ASE Color Space Def
			#else // Linear values//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(4.59479380, 4.59479380, 4.59479380, 2.0)//ASE Color Space Def
			#endif//ASE Color Space Def
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _FORWARD_PLUS

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			TEXTURE2D(_Splat0);
			SAMPLER(sampler_Splat0);
			float4 _DiffuseRemapScale0;
			TEXTURE2D(_Splat1);
			float4 _DiffuseRemapScale1;
			TEXTURE2D(_Splat2);
			float4 _DiffuseRemapScale2;
			TEXTURE2D(_Splat3);
			float4 _DiffuseRemapScale3;
			TEXTURE2D(_TerrainHolesTexture);
			SAMPLER(sampler_TerrainHolesTexture);
			TEXTURE2D(_ColorMapProjection);
			SAMPLER(sampler_ColorMapProjection);
			TEXTURE2D(_HeatMap);
			SAMPLER(sampler_HeatMap);
			TEXTURE2D(_NormalMap);
			SAMPLER(sampler_NormalMap);
			float _SnowAmount;
			float _Wetness;
			TEXTURE2D(_Normal0);
			SAMPLER(sampler_Normal0);
			TEXTURE2D(_Normal1);
			TEXTURE2D(_Normal2);
			TEXTURE2D(_Normal3);
			TEXTURE2D(_Mask0);
			SAMPLER(sampler_Mask0);
			TEXTURE2D(_Mask1);
			TEXTURE2D(_Mask2);
			TEXTURE2D(_Mask3);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }
			inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }
			inline float valueNoise (float2 uv)
			{
				float2 i = floor(uv);
				float2 f = frac( uv );
				f = f* f * (3.0 - 2.0 * f);
				uv = abs( frac(uv) - 0.5);
				float2 c0 = i + float2( 0.0, 0.0 );
				float2 c1 = i + float2( 1.0, 0.0 );
				float2 c2 = i + float2( 0.0, 1.0 );
				float2 c3 = i + float2( 1.0, 1.0 );
				float r0 = noise_randomValue( c0 );
				float r1 = noise_randomValue( c1 );
				float r2 = noise_randomValue( c2 );
				float r3 = noise_randomValue( c3 );
				float bottomOfGrid = noise_interpolate( r0, r1, f.x );
				float topOfGrid = noise_interpolate( r2, r3, f.x );
				float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
				return t;
			}
			
			float SimpleNoise(float2 UV)
			{
				float t = 0.0;
				float freq = pow( 2.0, float( 0 ) );
				float amp = pow( 0.5, float( 3 - 0 ) );
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(1));
				amp = pow(0.5, float(3-1));
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(2));
				amp = pow(0.5, float(3-2));
				t += valueNoise( UV/freq )*amp;
				return t;
			}
			
			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord7.xy = vertexToFrag286_g41;
				
				output.ase_texcoord7.zw = input.texcoord.xy;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						output.fogFactorAndVertexLight.x = ComputeFogFactor(vertexInput.positionCS.z);
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag ( PackedVaryings input
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined( _SURFACE_TYPE_TRANSPARENT )
					const bool isTransparent = true;
				#else
					const bool isTransparent = false;
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float2 vertexToFrag286_g41 = input.ase_texcoord7.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				float localSplatClip276_g41 = ( dotResult278_g41 );
				float SplatWeight276_g41 = dotResult278_g41;
				{
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float4 Control26_g41 = ( tex2DNode283_g41 / ( localSplatClip276_g41 + 0.001 ) );
				float2 uv_Splat0 = input.ase_texcoord7.zw * _Splat0_ST.xy + _Splat0_ST.zw;
				float3 Splat0342_g41 = (SAMPLE_TEXTURE2D( _Splat0, sampler_Splat0, uv_Splat0 )).rgb;
				float2 uv_Splat1 = input.ase_texcoord7.zw * _Splat1_ST.xy + _Splat1_ST.zw;
				float3 Splat1379_g41 = (SAMPLE_TEXTURE2D( _Splat1, sampler_Splat0, uv_Splat1 )).rgb;
				float2 uv_Splat2 = input.ase_texcoord7.zw * _Splat2_ST.xy + _Splat2_ST.zw;
				float3 Splat2357_g41 = (SAMPLE_TEXTURE2D( _Splat2, sampler_Splat0, uv_Splat2 )).rgb;
				float2 uv_Splat3 = input.ase_texcoord7.zw * _Splat3_ST.xy + _Splat3_ST.zw;
				float3 Splat3390_g41 = (SAMPLE_TEXTURE2D( _Splat3, sampler_Splat0, uv_Splat3 )).rgb;
				float4 weightedBlendVar9_g41 = Control26_g41;
				float3 weightedBlend9_g41 = ( weightedBlendVar9_g41.x*( Splat0342_g41 * (_DiffuseRemapScale0).rgb ) + weightedBlendVar9_g41.y*( Splat1379_g41 * (_DiffuseRemapScale1).rgb ) + weightedBlendVar9_g41.z*( Splat2357_g41 * (_DiffuseRemapScale2).rgb ) + weightedBlendVar9_g41.w*( Splat3390_g41 * (_DiffuseRemapScale3).rgb ) );
				float3 localClipHoles453_g41 = ( weightedBlend9_g41 );
				float2 uv_TerrainHolesTexture = input.ase_texcoord7.zw * _TerrainHolesTexture_ST.xy + _TerrainHolesTexture_ST.zw;
				float Hole453_g41 = SAMPLE_TEXTURE2D( _TerrainHolesTexture, sampler_TerrainHolesTexture, uv_TerrainHolesTexture ).r;
				{
				#ifdef _ALPHATEST_ON
				clip(Hole453_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float3 BaseColorOut79 = localClipHoles453_g41;
				float2 uv_ColorMapProjection324 = input.ase_texcoord7.zw;
				float4 ColorMap_Top331 = ( unity_ColorSpaceDouble * SAMPLE_TEXTURE2D( _ColorMapProjection, sampler_ColorMapProjection, uv_ColorMapProjection324 ) );
				float4 lerpResult271 = lerp( float4( BaseColorOut79 , 0.0 ) , ( ColorMap_Top331 * float4( BaseColorOut79 , 0.0 ) ) , _ColorMapIntensity);
				float3 ase_parentObjectScale = ( 1.0 / float3( length( GetWorldToObjectMatrix()[ 0 ].xyz ), length( GetWorldToObjectMatrix()[ 1 ].xyz ), length( GetWorldToObjectMatrix()[ 2 ].xyz ) ) );
				float2 appendResult304 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
				float2 appendResult305 = (float2(PositionWS.x , PositionWS.z));
				float2 temp_output_308_0 = ( appendResult304 * appendResult305 );
				float simpleNoise314 = SimpleNoise( temp_output_308_0*400.0 );
				float temp_output_320_0 = (simpleNoise314*0.6 + 0.6);
				float4 temp_cast_4 = (temp_output_320_0).xxxx;
				float2 uv_HeatMap306 = input.ase_texcoord7.zw;
				float temp_output_310_0 = ( 1.0 - SAMPLE_TEXTURE2D( _HeatMap, sampler_HeatMap, uv_HeatMap306 ).b );
				float2 uv_NormalMap307 = input.ase_texcoord7.zw;
				float3 unpack307 = UnpackNormalScale( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap307 ), _NormalIntensity );
				unpack307.z = lerp( 1, unpack307.z, saturate(_NormalIntensity) );
				float3 tex2DNode307 = unpack307;
				float temp_output_317_0 = saturate( NormalWS.y );
				float3 _Tangent = float3(0,1,0);
				float3x3 ase_tangentToWorldFast = float3x3( TangentWS.x, BitangentWS.x, NormalWS.x, TangentWS.y, BitangentWS.y, NormalWS.y, TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tangentToWorldDir284 = normalize( mul( ase_tangentToWorldFast, _Tangent ) );
				float dotResult285 = dot( tangentToWorldDir284 , _Tangent );
				float lerpResult289 = lerp( _SlopeMax , _SlopeMin , abs( dotResult285 ));
				float Slope291 = saturate( lerpResult289 );
				float GlobalVar_SnowAmount410 = _SnowAmount;
				float temp_output_334_0 = saturate( ( (( _NormalMapBasedSnow )?( ( (temp_output_310_0*-4.39 + 1.1) * (tex2DNode307.g*_SnowScale + _SnowOffset) ) ):( (temp_output_310_0*_SnowScale + _SnowOffset) )) * (( _SnowSlope )?( ( temp_output_317_0 * Slope291 ) ):( temp_output_317_0 )) * GlobalVar_SnowAmount410 ) );
				float4 lerpResult277 = lerp( (( _ColorMap )?( lerpResult271 ):( float4( BaseColorOut79 , 0.0 ) )) , temp_cast_4 , temp_output_334_0);
				float4 AlbedoSnow_Output280 = lerpResult277;
				float GlobalVar_Wetness360 = _Wetness;
				float4 lerpResult378 = lerp( AlbedoSnow_Output280 , ( AlbedoSnow_Output280 * 0.7 ) , GlobalVar_Wetness360);
				float4 Albedo_Output380 = lerpResult378;
				
				float4 Normal0341_g41 = SAMPLE_TEXTURE2D( _Normal0, sampler_Normal0, uv_Splat0 );
				float3 unpack490_g41 = UnpackNormalScale( Normal0341_g41, _NormalScale0 );
				unpack490_g41.z = lerp( 1, unpack490_g41.z, saturate(_NormalScale0) );
				float4 Normal1378_g41 = SAMPLE_TEXTURE2D( _Normal1, sampler_Normal0, uv_Splat1 );
				float3 unpack496_g41 = UnpackNormalScale( Normal1378_g41, _NormalScale1 );
				unpack496_g41.z = lerp( 1, unpack496_g41.z, saturate(_NormalScale1) );
				float4 Normal2356_g41 = SAMPLE_TEXTURE2D( _Normal2, sampler_Normal0, uv_Splat2 );
				float3 unpack494_g41 = UnpackNormalScale( Normal2356_g41, _NormalScale2 );
				unpack494_g41.z = lerp( 1, unpack494_g41.z, saturate(_NormalScale2) );
				float4 Normal3398_g41 = SAMPLE_TEXTURE2D( _Normal3, sampler_Normal0, uv_Splat3 );
				float3 unpack491_g41 = UnpackNormalScale( Normal3398_g41, _NormalScale3 );
				unpack491_g41.z = lerp( 1, unpack491_g41.z, saturate(_NormalScale3) );
				float4 weightedBlendVar473_g41 = Control26_g41;
				float3 weightedBlend473_g41 = ( weightedBlendVar473_g41.x*unpack490_g41 + weightedBlendVar473_g41.y*unpack496_g41 + weightedBlendVar473_g41.z*unpack494_g41 + weightedBlendVar473_g41.w*unpack491_g41 );
				float3 break513_g41 = weightedBlend473_g41;
				float3 appendResult514_g41 = (float3(break513_g41.x , break513_g41.y , ( break513_g41.z + 0.001 )));
				#ifdef _TERRAIN_INSTANCED_PERPIXEL_NORMAL
				float3 staticSwitch503_g41 = appendResult514_g41;
				#else
				float3 staticSwitch503_g41 = appendResult514_g41;
				#endif
				float3 NormalOut80 = staticSwitch503_g41;
				float3 temp_output_111_0_g43 = ddx( PositionWS );
				float3 temp_output_113_0_g43 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g43 = dot( temp_output_111_0_g43 , temp_output_113_0_g43 );
				float simpleNoise319 = SimpleNoise( temp_output_308_0*2.0 );
				float temp_output_20_0_g43 = simpleNoise319;
				float3 normalizeResult130_g43 = normalize( ( ( abs( dotResult115_g43 ) * NormalWS ) - ( 8.0 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g43 ) * ( ( ddx( temp_output_20_0_g43 ) * temp_output_113_0_g43 ) + ( ddy( temp_output_20_0_g43 ) * cross( NormalWS , temp_output_111_0_g43 ) ) ) ) ) );
				float3x3 ase_worldToTangent = float3x3( TangentWS, BitangentWS, NormalWS );
				float3 worldToTangentDir42_g43 = mul( ase_worldToTangent, normalizeResult130_g43 );
				float3 temp_output_111_0_g42 = ddx( PositionWS );
				float3 temp_output_113_0_g42 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g42 = dot( temp_output_111_0_g42 , temp_output_113_0_g42 );
				float temp_output_20_0_g42 = temp_output_320_0;
				float3 normalizeResult130_g42 = normalize( ( ( abs( dotResult115_g42 ) * NormalWS ) - ( 0.06 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g42 ) * ( ( ddx( temp_output_20_0_g42 ) * temp_output_113_0_g42 ) + ( ddy( temp_output_20_0_g42 ) * cross( NormalWS , temp_output_111_0_g42 ) ) ) ) ) );
				float3 worldToTangentDir42_g42 = mul( ase_worldToTangent, normalizeResult130_g42 );
				float2 temp_output_1_0_g44 = BlendNormal( worldToTangentDir42_g43 , worldToTangentDir42_g42 ).xy;
				float2 break13_g44 = temp_output_1_0_g44;
				float dotResult4_g44 = dot( temp_output_1_0_g44 , temp_output_1_0_g44 );
				float3 appendResult10_g44 = (float3(break13_g44.x , break13_g44.y , sqrt( ( 1.0 - saturate( dotResult4_g44 ) ) )));
				float3 normalizeResult12_g44 = normalize( appendResult10_g44 );
				float3 lerpResult296 = lerp( NormalOut80 , normalizeResult12_g44 , temp_output_334_0);
				float3 NormalMap_Top336 = tex2DNode307;
				float3 NormalSnow_Output281 = (( _TerrainNormalMap )?( BlendNormal( NormalMap_Top336 , lerpResult296 ) ):( lerpResult296 ));
				
				float4 tex2DNode416_g41 = SAMPLE_TEXTURE2D( _Mask0, sampler_Mask0, uv_Splat0 );
				float Mask0R334_g41 = tex2DNode416_g41.r;
				float4 tex2DNode422_g41 = SAMPLE_TEXTURE2D( _Mask1, sampler_Mask0, uv_Splat1 );
				float Mask1R370_g41 = tex2DNode422_g41.r;
				float4 tex2DNode419_g41 = SAMPLE_TEXTURE2D( _Mask2, sampler_Mask0, uv_Splat2 );
				float Mask2R359_g41 = tex2DNode419_g41.r;
				float4 tex2DNode425_g41 = SAMPLE_TEXTURE2D( _Mask3, sampler_Mask0, uv_Splat3 );
				float Mask3R388_g41 = tex2DNode425_g41.r;
				float4 weightedBlendVar536_g41 = Control26_g41;
				float weightedBlend536_g41 = ( weightedBlendVar536_g41.x*max( _Metallic0 , Mask0R334_g41 ) + weightedBlendVar536_g41.y*max( _Metallic1 , Mask1R370_g41 ) + weightedBlendVar536_g41.z*max( _Metallic2 , Mask2R359_g41 ) + weightedBlendVar536_g41.w*max( _Metallic3 , Mask3R388_g41 ) );
				
				float fresnelNdotV401 = dot( normalize( NormalWS ), ViewDirWS );
				float fresnelNode401 = ( 1.0 + -1.0 * pow( max( 1.0 - fresnelNdotV401 , 0.0001 ), 1.0 ) );
				float4 appendResult1168_g41 = (float4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
				float Splat0A435_g41 = tex2DNode416_g41.a;
				float Mask1A369_g41 = tex2DNode422_g41.a;
				float Mask2A360_g41 = tex2DNode419_g41.a;
				float Mask3A391_g41 = tex2DNode425_g41.a;
				float4 appendResult1169_g41 = (float4(Splat0A435_g41 , Mask1A369_g41 , Mask2A360_g41 , Mask3A391_g41));
				float dotResult1166_g41 = dot( ( appendResult1168_g41 * appendResult1169_g41 ) , Control26_g41 );
				float SmoothOut157 = ( _SmoothnessIntensity * dotResult1166_g41 );
				float lerpResult279 = lerp( SmoothOut157 , (simpleNoise314*1.4 + 0.0) , temp_output_334_0);
				float Smoothness_Final282 = lerpResult279;
				float Smoothness_Output369 = saturate( ( ( GlobalVar_Wetness360 * fresnelNode401 ) + Smoothness_Final282 ) );
				
				float Mask0G409_g41 = tex2DNode416_g41.g;
				float Mask1G371_g41 = tex2DNode422_g41.g;
				float Mask2G358_g41 = tex2DNode419_g41.g;
				float Mask3G389_g41 = tex2DNode425_g41.g;
				float4 weightedBlendVar602_g41 = Control26_g41;
				float weightedBlend602_g41 = ( weightedBlendVar602_g41.x*saturate( Mask0G409_g41 ) + weightedBlendVar602_g41.y*saturate( Mask1G371_g41 ) + weightedBlendVar602_g41.z*saturate( Mask2G358_g41 ) + weightedBlendVar602_g41.w*saturate( Mask3G389_g41 ) );
				float lerpResult408 = lerp( saturate( weightedBlend602_g41 ) , 1.0 , GlobalVar_SnowAmount410);
				
				float3 temp_cast_12 = (( _TTFETERRAINSNOWSHADER + _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _ADVANCEDSETTINGS )).xxx;
				

				float3 BaseColor = Albedo_Output380.rgb;
				float3 Normal = NormalSnow_Output281;
				float3 Specular = 0.5;
				float Metallic = weightedBlend536_g41;
				float Smoothness = Smoothness_Output369;
				float Occlusion = lerpResult408;
				float3 Emission = temp_cast_12;
				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = ClipPos.z;
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = float4( input.positionCS.xy, ClipPos.zw / ClipPos.w );
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.viewDirectionWS = ViewDirWS;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = NormalWS;
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI( SH, GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask );
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
					#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
				#endif

				#ifdef ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( NormalWS,0 ) ).xyz * ( 1.0 - dot( NormalWS, ViewDirWS ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3(0,0,0), inputData.fogCoord);
					#else
						color.rgb = MixFog(color.rgb, inputData.fogCoord);
					#endif
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				#if defined( ASE_OPAQUE_KEEP_ALPHA )
					return half4( color.rgb, color.a );
				#else
					return half4( color.rgb, OutputAlpha( color.a, isTransparent ) );
				#endif
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off
			ColorMask 0

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			float3 _LightDirection;
			float3 _LightPosition;

			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.ase_texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord1.xy = vertexToFrag286_g41;
				
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				float3 normalWS = TransformObjectToWorldDir(input.normalOS);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				//code for UNITY_REVERSED_Z is moved into Shadows.hlsl from 6000.0.22 and or higher
				positionCS = ApplyShadowClamping(positionCS);

				output.positionCS = positionCS;
				output.positionWS = positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float2 vertexToFrag286_g41 = input.ase_texcoord1.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				

				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;
				float AlphaClipThresholdShadow = 0.5;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					#if defined( _ALPHATEST_SHADOW_ON )
						AlphaDiscard( Alpha, AlphaClipThresholdShadow );
					#else
						AlphaDiscard( Alpha, AlphaClipThreshold );
					#endif
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				return 0;
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.ase_texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord1.xy = vertexToFrag286_g41;
				
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float2 vertexToFrag286_g41 = input.ase_texcoord1.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				

				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				return 0;
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#define ASE_GEOMETRY
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#ifdef UNITY_COLORSPACE_GAMMA//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(2.0, 2.0, 2.0, 2.0)//ASE Color Space Def
			#else // Linear values//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(4.59479380, 4.59479380, 4.59479380, 2.0)//ASE Color Space Def
			#endif//ASE Color Space Def
			#define ASE_USING_SAMPLING_MACROS 1

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				#ifdef EDITOR_VISUALIZATION
					float4 VizUV : TEXCOORD1;
					float4 LightCoord : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			TEXTURE2D(_Splat0);
			SAMPLER(sampler_Splat0);
			float4 _DiffuseRemapScale0;
			TEXTURE2D(_Splat1);
			float4 _DiffuseRemapScale1;
			TEXTURE2D(_Splat2);
			float4 _DiffuseRemapScale2;
			TEXTURE2D(_Splat3);
			float4 _DiffuseRemapScale3;
			TEXTURE2D(_TerrainHolesTexture);
			SAMPLER(sampler_TerrainHolesTexture);
			TEXTURE2D(_ColorMapProjection);
			SAMPLER(sampler_ColorMapProjection);
			TEXTURE2D(_HeatMap);
			SAMPLER(sampler_HeatMap);
			TEXTURE2D(_NormalMap);
			SAMPLER(sampler_NormalMap);
			float _SnowAmount;
			float _Wetness;
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }
			inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }
			inline float valueNoise (float2 uv)
			{
				float2 i = floor(uv);
				float2 f = frac( uv );
				f = f* f * (3.0 - 2.0 * f);
				uv = abs( frac(uv) - 0.5);
				float2 c0 = i + float2( 0.0, 0.0 );
				float2 c1 = i + float2( 1.0, 0.0 );
				float2 c2 = i + float2( 0.0, 1.0 );
				float2 c3 = i + float2( 1.0, 1.0 );
				float r0 = noise_randomValue( c0 );
				float r1 = noise_randomValue( c1 );
				float r2 = noise_randomValue( c2 );
				float r3 = noise_randomValue( c3 );
				float bottomOfGrid = noise_interpolate( r0, r1, f.x );
				float topOfGrid = noise_interpolate( r2, r3, f.x );
				float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
				return t;
			}
			
			float SimpleNoise(float2 UV)
			{
				float t = 0.0;
				float freq = pow( 2.0, float( 0 ) );
				float amp = pow( 0.5, float( 3 - 0 ) );
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(1));
				amp = pow(0.5, float(3-1));
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(2));
				amp = pow(0.5, float(3-2));
				t += valueNoise( UV/freq )*amp;
				return t;
			}
			
			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.texcoord0.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.texcoord0.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord3.xy = vertexToFrag286_g41;
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				output.ase_texcoord4.xyz = ase_normalWS;
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				output.ase_texcoord5.xyz = ase_tangentWS;
				float ase_tangentSign = input.tangentOS.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord6.xyz = ase_bitangentWS;
				
				output.ase_texcoord3.zw = input.texcoord0.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord4.w = 0;
				output.ase_texcoord5.w = 0;
				output.ase_texcoord6.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				#ifdef EDITOR_VISUALIZATION
					float2 VizUV = 0;
					float4 LightCoord = 0;
					UnityEditorVizData(input.positionOS.xyz, input.texcoord0.xy, input.texcoord1.xy, input.texcoord2.xy, VizUV, LightCoord);
					output.VizUV = float4(VizUV, 0, 0);
					output.LightCoord = LightCoord;
				#endif

				output.positionCS = MetaVertexPosition( input.positionOS, input.texcoord1.xy, input.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );
				output.positionWS = TransformObjectToWorld( input.positionOS.xyz );
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;

				float2 vertexToFrag286_g41 = input.ase_texcoord3.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				float localSplatClip276_g41 = ( dotResult278_g41 );
				float SplatWeight276_g41 = dotResult278_g41;
				{
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float4 Control26_g41 = ( tex2DNode283_g41 / ( localSplatClip276_g41 + 0.001 ) );
				float2 uv_Splat0 = input.ase_texcoord3.zw * _Splat0_ST.xy + _Splat0_ST.zw;
				float3 Splat0342_g41 = (SAMPLE_TEXTURE2D( _Splat0, sampler_Splat0, uv_Splat0 )).rgb;
				float2 uv_Splat1 = input.ase_texcoord3.zw * _Splat1_ST.xy + _Splat1_ST.zw;
				float3 Splat1379_g41 = (SAMPLE_TEXTURE2D( _Splat1, sampler_Splat0, uv_Splat1 )).rgb;
				float2 uv_Splat2 = input.ase_texcoord3.zw * _Splat2_ST.xy + _Splat2_ST.zw;
				float3 Splat2357_g41 = (SAMPLE_TEXTURE2D( _Splat2, sampler_Splat0, uv_Splat2 )).rgb;
				float2 uv_Splat3 = input.ase_texcoord3.zw * _Splat3_ST.xy + _Splat3_ST.zw;
				float3 Splat3390_g41 = (SAMPLE_TEXTURE2D( _Splat3, sampler_Splat0, uv_Splat3 )).rgb;
				float4 weightedBlendVar9_g41 = Control26_g41;
				float3 weightedBlend9_g41 = ( weightedBlendVar9_g41.x*( Splat0342_g41 * (_DiffuseRemapScale0).rgb ) + weightedBlendVar9_g41.y*( Splat1379_g41 * (_DiffuseRemapScale1).rgb ) + weightedBlendVar9_g41.z*( Splat2357_g41 * (_DiffuseRemapScale2).rgb ) + weightedBlendVar9_g41.w*( Splat3390_g41 * (_DiffuseRemapScale3).rgb ) );
				float3 localClipHoles453_g41 = ( weightedBlend9_g41 );
				float2 uv_TerrainHolesTexture = input.ase_texcoord3.zw * _TerrainHolesTexture_ST.xy + _TerrainHolesTexture_ST.zw;
				float Hole453_g41 = SAMPLE_TEXTURE2D( _TerrainHolesTexture, sampler_TerrainHolesTexture, uv_TerrainHolesTexture ).r;
				{
				#ifdef _ALPHATEST_ON
				clip(Hole453_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float3 BaseColorOut79 = localClipHoles453_g41;
				float2 uv_ColorMapProjection324 = input.ase_texcoord3.zw;
				float4 ColorMap_Top331 = ( unity_ColorSpaceDouble * SAMPLE_TEXTURE2D( _ColorMapProjection, sampler_ColorMapProjection, uv_ColorMapProjection324 ) );
				float4 lerpResult271 = lerp( float4( BaseColorOut79 , 0.0 ) , ( ColorMap_Top331 * float4( BaseColorOut79 , 0.0 ) ) , _ColorMapIntensity);
				float3 ase_parentObjectScale = ( 1.0 / float3( length( GetWorldToObjectMatrix()[ 0 ].xyz ), length( GetWorldToObjectMatrix()[ 1 ].xyz ), length( GetWorldToObjectMatrix()[ 2 ].xyz ) ) );
				float2 appendResult304 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
				float2 appendResult305 = (float2(PositionWS.x , PositionWS.z));
				float2 temp_output_308_0 = ( appendResult304 * appendResult305 );
				float simpleNoise314 = SimpleNoise( temp_output_308_0*400.0 );
				float temp_output_320_0 = (simpleNoise314*0.6 + 0.6);
				float4 temp_cast_4 = (temp_output_320_0).xxxx;
				float2 uv_HeatMap306 = input.ase_texcoord3.zw;
				float temp_output_310_0 = ( 1.0 - SAMPLE_TEXTURE2D( _HeatMap, sampler_HeatMap, uv_HeatMap306 ).b );
				float2 uv_NormalMap307 = input.ase_texcoord3.zw;
				float3 unpack307 = UnpackNormalScale( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap307 ), _NormalIntensity );
				unpack307.z = lerp( 1, unpack307.z, saturate(_NormalIntensity) );
				float3 tex2DNode307 = unpack307;
				float3 ase_normalWS = input.ase_texcoord4.xyz;
				float temp_output_317_0 = saturate( ase_normalWS.y );
				float3 _Tangent = float3(0,1,0);
				float3 ase_tangentWS = input.ase_texcoord5.xyz;
				float3 ase_bitangentWS = input.ase_texcoord6.xyz;
				float3x3 ase_tangentToWorldFast = float3x3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x, ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y, ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 tangentToWorldDir284 = normalize( mul( ase_tangentToWorldFast, _Tangent ) );
				float dotResult285 = dot( tangentToWorldDir284 , _Tangent );
				float lerpResult289 = lerp( _SlopeMax , _SlopeMin , abs( dotResult285 ));
				float Slope291 = saturate( lerpResult289 );
				float GlobalVar_SnowAmount410 = _SnowAmount;
				float temp_output_334_0 = saturate( ( (( _NormalMapBasedSnow )?( ( (temp_output_310_0*-4.39 + 1.1) * (tex2DNode307.g*_SnowScale + _SnowOffset) ) ):( (temp_output_310_0*_SnowScale + _SnowOffset) )) * (( _SnowSlope )?( ( temp_output_317_0 * Slope291 ) ):( temp_output_317_0 )) * GlobalVar_SnowAmount410 ) );
				float4 lerpResult277 = lerp( (( _ColorMap )?( lerpResult271 ):( float4( BaseColorOut79 , 0.0 ) )) , temp_cast_4 , temp_output_334_0);
				float4 AlbedoSnow_Output280 = lerpResult277;
				float GlobalVar_Wetness360 = _Wetness;
				float4 lerpResult378 = lerp( AlbedoSnow_Output280 , ( AlbedoSnow_Output280 * 0.7 ) , GlobalVar_Wetness360);
				float4 Albedo_Output380 = lerpResult378;
				
				float3 temp_cast_6 = (( _TTFETERRAINSNOWSHADER + _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _ADVANCEDSETTINGS )).xxx;
				

				float3 BaseColor = Albedo_Output380.rgb;
				float3 Emission = temp_cast_6;
				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = BaseColor;
				metaInput.Emission = Emission;
				#ifdef EDITOR_VISUALIZATION
					metaInput.VizUV = input.VizUV.xy;
					metaInput.LightCoord = input.LightCoord;
				#endif

				return UnityMetaFragment(metaInput);
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#ifdef UNITY_COLORSPACE_GAMMA//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(2.0, 2.0, 2.0, 2.0)//ASE Color Space Def
			#else // Linear values//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(4.59479380, 4.59479380, 4.59479380, 2.0)//ASE Color Space Def
			#endif//ASE Color Space Def
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			TEXTURE2D(_Splat0);
			SAMPLER(sampler_Splat0);
			float4 _DiffuseRemapScale0;
			TEXTURE2D(_Splat1);
			float4 _DiffuseRemapScale1;
			TEXTURE2D(_Splat2);
			float4 _DiffuseRemapScale2;
			TEXTURE2D(_Splat3);
			float4 _DiffuseRemapScale3;
			TEXTURE2D(_TerrainHolesTexture);
			SAMPLER(sampler_TerrainHolesTexture);
			TEXTURE2D(_ColorMapProjection);
			SAMPLER(sampler_ColorMapProjection);
			TEXTURE2D(_HeatMap);
			SAMPLER(sampler_HeatMap);
			TEXTURE2D(_NormalMap);
			SAMPLER(sampler_NormalMap);
			float _SnowAmount;
			float _Wetness;
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }
			inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }
			inline float valueNoise (float2 uv)
			{
				float2 i = floor(uv);
				float2 f = frac( uv );
				f = f* f * (3.0 - 2.0 * f);
				uv = abs( frac(uv) - 0.5);
				float2 c0 = i + float2( 0.0, 0.0 );
				float2 c1 = i + float2( 1.0, 0.0 );
				float2 c2 = i + float2( 0.0, 1.0 );
				float2 c3 = i + float2( 1.0, 1.0 );
				float r0 = noise_randomValue( c0 );
				float r1 = noise_randomValue( c1 );
				float r2 = noise_randomValue( c2 );
				float r3 = noise_randomValue( c3 );
				float bottomOfGrid = noise_interpolate( r0, r1, f.x );
				float topOfGrid = noise_interpolate( r2, r3, f.x );
				float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
				return t;
			}
			
			float SimpleNoise(float2 UV)
			{
				float t = 0.0;
				float freq = pow( 2.0, float( 0 ) );
				float amp = pow( 0.5, float( 3 - 0 ) );
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(1));
				amp = pow(0.5, float(3-1));
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(2));
				amp = pow(0.5, float(3-2));
				t += valueNoise( UV/freq )*amp;
				return t;
			}
			
			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.ase_texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord1.xy = vertexToFrag286_g41;
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				output.ase_texcoord2.xyz = ase_normalWS;
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				output.ase_texcoord3.xyz = ase_tangentWS;
				float ase_tangentSign = input.tangentOS.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord4.xyz = ase_bitangentWS;
				
				output.ase_texcoord1.zw = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord2.w = 0;
				output.ase_texcoord3.w = 0;
				output.ase_texcoord4.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;

				float2 vertexToFrag286_g41 = input.ase_texcoord1.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				float localSplatClip276_g41 = ( dotResult278_g41 );
				float SplatWeight276_g41 = dotResult278_g41;
				{
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float4 Control26_g41 = ( tex2DNode283_g41 / ( localSplatClip276_g41 + 0.001 ) );
				float2 uv_Splat0 = input.ase_texcoord1.zw * _Splat0_ST.xy + _Splat0_ST.zw;
				float3 Splat0342_g41 = (SAMPLE_TEXTURE2D( _Splat0, sampler_Splat0, uv_Splat0 )).rgb;
				float2 uv_Splat1 = input.ase_texcoord1.zw * _Splat1_ST.xy + _Splat1_ST.zw;
				float3 Splat1379_g41 = (SAMPLE_TEXTURE2D( _Splat1, sampler_Splat0, uv_Splat1 )).rgb;
				float2 uv_Splat2 = input.ase_texcoord1.zw * _Splat2_ST.xy + _Splat2_ST.zw;
				float3 Splat2357_g41 = (SAMPLE_TEXTURE2D( _Splat2, sampler_Splat0, uv_Splat2 )).rgb;
				float2 uv_Splat3 = input.ase_texcoord1.zw * _Splat3_ST.xy + _Splat3_ST.zw;
				float3 Splat3390_g41 = (SAMPLE_TEXTURE2D( _Splat3, sampler_Splat0, uv_Splat3 )).rgb;
				float4 weightedBlendVar9_g41 = Control26_g41;
				float3 weightedBlend9_g41 = ( weightedBlendVar9_g41.x*( Splat0342_g41 * (_DiffuseRemapScale0).rgb ) + weightedBlendVar9_g41.y*( Splat1379_g41 * (_DiffuseRemapScale1).rgb ) + weightedBlendVar9_g41.z*( Splat2357_g41 * (_DiffuseRemapScale2).rgb ) + weightedBlendVar9_g41.w*( Splat3390_g41 * (_DiffuseRemapScale3).rgb ) );
				float3 localClipHoles453_g41 = ( weightedBlend9_g41 );
				float2 uv_TerrainHolesTexture = input.ase_texcoord1.zw * _TerrainHolesTexture_ST.xy + _TerrainHolesTexture_ST.zw;
				float Hole453_g41 = SAMPLE_TEXTURE2D( _TerrainHolesTexture, sampler_TerrainHolesTexture, uv_TerrainHolesTexture ).r;
				{
				#ifdef _ALPHATEST_ON
				clip(Hole453_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float3 BaseColorOut79 = localClipHoles453_g41;
				float2 uv_ColorMapProjection324 = input.ase_texcoord1.zw;
				float4 ColorMap_Top331 = ( unity_ColorSpaceDouble * SAMPLE_TEXTURE2D( _ColorMapProjection, sampler_ColorMapProjection, uv_ColorMapProjection324 ) );
				float4 lerpResult271 = lerp( float4( BaseColorOut79 , 0.0 ) , ( ColorMap_Top331 * float4( BaseColorOut79 , 0.0 ) ) , _ColorMapIntensity);
				float3 ase_parentObjectScale = ( 1.0 / float3( length( GetWorldToObjectMatrix()[ 0 ].xyz ), length( GetWorldToObjectMatrix()[ 1 ].xyz ), length( GetWorldToObjectMatrix()[ 2 ].xyz ) ) );
				float2 appendResult304 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
				float2 appendResult305 = (float2(PositionWS.x , PositionWS.z));
				float2 temp_output_308_0 = ( appendResult304 * appendResult305 );
				float simpleNoise314 = SimpleNoise( temp_output_308_0*400.0 );
				float temp_output_320_0 = (simpleNoise314*0.6 + 0.6);
				float4 temp_cast_4 = (temp_output_320_0).xxxx;
				float2 uv_HeatMap306 = input.ase_texcoord1.zw;
				float temp_output_310_0 = ( 1.0 - SAMPLE_TEXTURE2D( _HeatMap, sampler_HeatMap, uv_HeatMap306 ).b );
				float2 uv_NormalMap307 = input.ase_texcoord1.zw;
				float3 unpack307 = UnpackNormalScale( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap307 ), _NormalIntensity );
				unpack307.z = lerp( 1, unpack307.z, saturate(_NormalIntensity) );
				float3 tex2DNode307 = unpack307;
				float3 ase_normalWS = input.ase_texcoord2.xyz;
				float temp_output_317_0 = saturate( ase_normalWS.y );
				float3 _Tangent = float3(0,1,0);
				float3 ase_tangentWS = input.ase_texcoord3.xyz;
				float3 ase_bitangentWS = input.ase_texcoord4.xyz;
				float3x3 ase_tangentToWorldFast = float3x3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x, ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y, ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 tangentToWorldDir284 = normalize( mul( ase_tangentToWorldFast, _Tangent ) );
				float dotResult285 = dot( tangentToWorldDir284 , _Tangent );
				float lerpResult289 = lerp( _SlopeMax , _SlopeMin , abs( dotResult285 ));
				float Slope291 = saturate( lerpResult289 );
				float GlobalVar_SnowAmount410 = _SnowAmount;
				float temp_output_334_0 = saturate( ( (( _NormalMapBasedSnow )?( ( (temp_output_310_0*-4.39 + 1.1) * (tex2DNode307.g*_SnowScale + _SnowOffset) ) ):( (temp_output_310_0*_SnowScale + _SnowOffset) )) * (( _SnowSlope )?( ( temp_output_317_0 * Slope291 ) ):( temp_output_317_0 )) * GlobalVar_SnowAmount410 ) );
				float4 lerpResult277 = lerp( (( _ColorMap )?( lerpResult271 ):( float4( BaseColorOut79 , 0.0 ) )) , temp_cast_4 , temp_output_334_0);
				float4 AlbedoSnow_Output280 = lerpResult277;
				float GlobalVar_Wetness360 = _Wetness;
				float4 lerpResult378 = lerp( AlbedoSnow_Output280 , ( AlbedoSnow_Output280 * 0.7 ) , GlobalVar_Wetness360);
				float4 Albedo_Output380 = lerpResult378;
				

				float3 BaseColor = Albedo_Output380.rgb;
				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;

				half4 color = half4(BaseColor, Alpha );

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				return color;
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				half4 texcoord : TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			TEXTURE2D(_Normal0);
			TEXTURE2D(_Splat0);
			SAMPLER(sampler_Normal0);
			TEXTURE2D(_Normal1);
			TEXTURE2D(_Splat1);
			TEXTURE2D(_Normal2);
			TEXTURE2D(_Splat2);
			TEXTURE2D(_Normal3);
			TEXTURE2D(_Splat3);
			TEXTURE2D(_HeatMap);
			SAMPLER(sampler_HeatMap);
			TEXTURE2D(_NormalMap);
			SAMPLER(sampler_NormalMap);
			float _SnowAmount;
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }
			inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }
			inline float valueNoise (float2 uv)
			{
				float2 i = floor(uv);
				float2 f = frac( uv );
				f = f* f * (3.0 - 2.0 * f);
				uv = abs( frac(uv) - 0.5);
				float2 c0 = i + float2( 0.0, 0.0 );
				float2 c1 = i + float2( 1.0, 0.0 );
				float2 c2 = i + float2( 0.0, 1.0 );
				float2 c3 = i + float2( 1.0, 1.0 );
				float r0 = noise_randomValue( c0 );
				float r1 = noise_randomValue( c1 );
				float r2 = noise_randomValue( c2 );
				float r3 = noise_randomValue( c3 );
				float bottomOfGrid = noise_interpolate( r0, r1, f.x );
				float topOfGrid = noise_interpolate( r2, r3, f.x );
				float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
				return t;
			}
			
			float SimpleNoise(float2 UV)
			{
				float t = 0.0;
				float freq = pow( 2.0, float( 0 ) );
				float amp = pow( 0.5, float( 3 - 0 ) );
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(1));
				amp = pow(0.5, float(3-1));
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(2));
				amp = pow(0.5, float(3-2));
				t += valueNoise( UV/freq )*amp;
				return t;
			}
			
			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord3.xy = vertexToFrag286_g41;
				
				output.ase_texcoord3.zw = input.texcoord.xy;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float2 vertexToFrag286_g41 = input.ase_texcoord3.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				float localSplatClip276_g41 = ( dotResult278_g41 );
				float SplatWeight276_g41 = dotResult278_g41;
				{
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float4 Control26_g41 = ( tex2DNode283_g41 / ( localSplatClip276_g41 + 0.001 ) );
				float2 uv_Splat0 = input.ase_texcoord3.zw * _Splat0_ST.xy + _Splat0_ST.zw;
				float4 Normal0341_g41 = SAMPLE_TEXTURE2D( _Normal0, sampler_Normal0, uv_Splat0 );
				float3 unpack490_g41 = UnpackNormalScale( Normal0341_g41, _NormalScale0 );
				unpack490_g41.z = lerp( 1, unpack490_g41.z, saturate(_NormalScale0) );
				float2 uv_Splat1 = input.ase_texcoord3.zw * _Splat1_ST.xy + _Splat1_ST.zw;
				float4 Normal1378_g41 = SAMPLE_TEXTURE2D( _Normal1, sampler_Normal0, uv_Splat1 );
				float3 unpack496_g41 = UnpackNormalScale( Normal1378_g41, _NormalScale1 );
				unpack496_g41.z = lerp( 1, unpack496_g41.z, saturate(_NormalScale1) );
				float2 uv_Splat2 = input.ase_texcoord3.zw * _Splat2_ST.xy + _Splat2_ST.zw;
				float4 Normal2356_g41 = SAMPLE_TEXTURE2D( _Normal2, sampler_Normal0, uv_Splat2 );
				float3 unpack494_g41 = UnpackNormalScale( Normal2356_g41, _NormalScale2 );
				unpack494_g41.z = lerp( 1, unpack494_g41.z, saturate(_NormalScale2) );
				float2 uv_Splat3 = input.ase_texcoord3.zw * _Splat3_ST.xy + _Splat3_ST.zw;
				float4 Normal3398_g41 = SAMPLE_TEXTURE2D( _Normal3, sampler_Normal0, uv_Splat3 );
				float3 unpack491_g41 = UnpackNormalScale( Normal3398_g41, _NormalScale3 );
				unpack491_g41.z = lerp( 1, unpack491_g41.z, saturate(_NormalScale3) );
				float4 weightedBlendVar473_g41 = Control26_g41;
				float3 weightedBlend473_g41 = ( weightedBlendVar473_g41.x*unpack490_g41 + weightedBlendVar473_g41.y*unpack496_g41 + weightedBlendVar473_g41.z*unpack494_g41 + weightedBlendVar473_g41.w*unpack491_g41 );
				float3 break513_g41 = weightedBlend473_g41;
				float3 appendResult514_g41 = (float3(break513_g41.x , break513_g41.y , ( break513_g41.z + 0.001 )));
				#ifdef _TERRAIN_INSTANCED_PERPIXEL_NORMAL
				float3 staticSwitch503_g41 = appendResult514_g41;
				#else
				float3 staticSwitch503_g41 = appendResult514_g41;
				#endif
				float3 NormalOut80 = staticSwitch503_g41;
				float3 temp_output_111_0_g43 = ddx( PositionWS );
				float3 temp_output_113_0_g43 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g43 = dot( temp_output_111_0_g43 , temp_output_113_0_g43 );
				float3 ase_parentObjectScale = ( 1.0 / float3( length( GetWorldToObjectMatrix()[ 0 ].xyz ), length( GetWorldToObjectMatrix()[ 1 ].xyz ), length( GetWorldToObjectMatrix()[ 2 ].xyz ) ) );
				float2 appendResult304 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
				float2 appendResult305 = (float2(PositionWS.x , PositionWS.z));
				float2 temp_output_308_0 = ( appendResult304 * appendResult305 );
				float simpleNoise319 = SimpleNoise( temp_output_308_0*2.0 );
				float temp_output_20_0_g43 = simpleNoise319;
				float3 normalizeResult130_g43 = normalize( ( ( abs( dotResult115_g43 ) * NormalWS ) - ( 8.0 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g43 ) * ( ( ddx( temp_output_20_0_g43 ) * temp_output_113_0_g43 ) + ( ddy( temp_output_20_0_g43 ) * cross( NormalWS , temp_output_111_0_g43 ) ) ) ) ) );
				float3x3 ase_worldToTangent = float3x3( TangentWS, BitangentWS, NormalWS );
				float3 worldToTangentDir42_g43 = mul( ase_worldToTangent, normalizeResult130_g43 );
				float3 temp_output_111_0_g42 = ddx( PositionWS );
				float3 temp_output_113_0_g42 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g42 = dot( temp_output_111_0_g42 , temp_output_113_0_g42 );
				float simpleNoise314 = SimpleNoise( temp_output_308_0*400.0 );
				float temp_output_320_0 = (simpleNoise314*0.6 + 0.6);
				float temp_output_20_0_g42 = temp_output_320_0;
				float3 normalizeResult130_g42 = normalize( ( ( abs( dotResult115_g42 ) * NormalWS ) - ( 0.06 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g42 ) * ( ( ddx( temp_output_20_0_g42 ) * temp_output_113_0_g42 ) + ( ddy( temp_output_20_0_g42 ) * cross( NormalWS , temp_output_111_0_g42 ) ) ) ) ) );
				float3 worldToTangentDir42_g42 = mul( ase_worldToTangent, normalizeResult130_g42 );
				float2 temp_output_1_0_g44 = BlendNormal( worldToTangentDir42_g43 , worldToTangentDir42_g42 ).xy;
				float2 break13_g44 = temp_output_1_0_g44;
				float dotResult4_g44 = dot( temp_output_1_0_g44 , temp_output_1_0_g44 );
				float3 appendResult10_g44 = (float3(break13_g44.x , break13_g44.y , sqrt( ( 1.0 - saturate( dotResult4_g44 ) ) )));
				float3 normalizeResult12_g44 = normalize( appendResult10_g44 );
				float2 uv_HeatMap306 = input.ase_texcoord3.zw;
				float temp_output_310_0 = ( 1.0 - SAMPLE_TEXTURE2D( _HeatMap, sampler_HeatMap, uv_HeatMap306 ).b );
				float2 uv_NormalMap307 = input.ase_texcoord3.zw;
				float3 unpack307 = UnpackNormalScale( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap307 ), _NormalIntensity );
				unpack307.z = lerp( 1, unpack307.z, saturate(_NormalIntensity) );
				float3 tex2DNode307 = unpack307;
				float temp_output_317_0 = saturate( NormalWS.y );
				float3 _Tangent = float3(0,1,0);
				float3x3 ase_tangentToWorldFast = float3x3( TangentWS.x, BitangentWS.x, NormalWS.x, TangentWS.y, BitangentWS.y, NormalWS.y, TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tangentToWorldDir284 = normalize( mul( ase_tangentToWorldFast, _Tangent ) );
				float dotResult285 = dot( tangentToWorldDir284 , _Tangent );
				float lerpResult289 = lerp( _SlopeMax , _SlopeMin , abs( dotResult285 ));
				float Slope291 = saturate( lerpResult289 );
				float GlobalVar_SnowAmount410 = _SnowAmount;
				float temp_output_334_0 = saturate( ( (( _NormalMapBasedSnow )?( ( (temp_output_310_0*-4.39 + 1.1) * (tex2DNode307.g*_SnowScale + _SnowOffset) ) ):( (temp_output_310_0*_SnowScale + _SnowOffset) )) * (( _SnowSlope )?( ( temp_output_317_0 * Slope291 ) ):( temp_output_317_0 )) * GlobalVar_SnowAmount410 ) );
				float3 lerpResult296 = lerp( NormalOut80 , normalizeResult12_g44 , temp_output_334_0);
				float3 NormalMap_Top336 = tex2DNode307;
				float3 NormalSnow_Output281 = (( _TerrainNormalMap )?( BlendNormal( NormalMap_Top336 , lerpResult296 ) ):( lerpResult296 ));
				

				float3 Normal = NormalSnow_Output281;
				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(NormalWS);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = NormalWS;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
				#endif
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "GBuffer"
			Tags { "LightMode"="UniversalGBuffer" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#ifdef UNITY_COLORSPACE_GAMMA//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(2.0, 2.0, 2.0, 2.0)//ASE Color Space Def
			#else // Linear values//ASE Color Space Def
			#define unity_ColorSpaceDouble half4(4.59479380, 4.59479380, 4.59479380, 2.0)//ASE Color Space Def
			#endif//ASE Color Space Def
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_GBUFFER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			TEXTURE2D(_Splat0);
			SAMPLER(sampler_Splat0);
			float4 _DiffuseRemapScale0;
			TEXTURE2D(_Splat1);
			float4 _DiffuseRemapScale1;
			TEXTURE2D(_Splat2);
			float4 _DiffuseRemapScale2;
			TEXTURE2D(_Splat3);
			float4 _DiffuseRemapScale3;
			TEXTURE2D(_TerrainHolesTexture);
			SAMPLER(sampler_TerrainHolesTexture);
			TEXTURE2D(_ColorMapProjection);
			SAMPLER(sampler_ColorMapProjection);
			TEXTURE2D(_HeatMap);
			SAMPLER(sampler_HeatMap);
			TEXTURE2D(_NormalMap);
			SAMPLER(sampler_NormalMap);
			float _SnowAmount;
			float _Wetness;
			TEXTURE2D(_Normal0);
			SAMPLER(sampler_Normal0);
			TEXTURE2D(_Normal1);
			TEXTURE2D(_Normal2);
			TEXTURE2D(_Normal3);
			TEXTURE2D(_Mask0);
			SAMPLER(sampler_Mask0);
			TEXTURE2D(_Mask1);
			TEXTURE2D(_Mask2);
			TEXTURE2D(_Mask3);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

			inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }
			inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }
			inline float valueNoise (float2 uv)
			{
				float2 i = floor(uv);
				float2 f = frac( uv );
				f = f* f * (3.0 - 2.0 * f);
				uv = abs( frac(uv) - 0.5);
				float2 c0 = i + float2( 0.0, 0.0 );
				float2 c1 = i + float2( 1.0, 0.0 );
				float2 c2 = i + float2( 0.0, 1.0 );
				float2 c3 = i + float2( 1.0, 1.0 );
				float r0 = noise_randomValue( c0 );
				float r1 = noise_randomValue( c1 );
				float r2 = noise_randomValue( c2 );
				float r3 = noise_randomValue( c3 );
				float bottomOfGrid = noise_interpolate( r0, r1, f.x );
				float topOfGrid = noise_interpolate( r2, r3, f.x );
				float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
				return t;
			}
			
			float SimpleNoise(float2 UV)
			{
				float t = 0.0;
				float freq = pow( 2.0, float( 0 ) );
				float amp = pow( 0.5, float( 3 - 0 ) );
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(1));
				amp = pow(0.5, float(3-1));
				t += valueNoise( UV/freq )*amp;
				freq = pow(2.0, float(2));
				amp = pow(0.5, float(3-2));
				t += valueNoise( UV/freq )*amp;
				return t;
			}
			
			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float4 appendResult704_g41 = (float4(cross( input.normalOS , float3( 0, 0, 1 ) ) , -1.0));
				
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord7.xy = vertexToFrag286_g41;
				
				output.ase_texcoord7.zw = input.texcoord.xy;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = appendResult704_g41;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						// @diogo: no fog applied in GBuffer
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			FragmentOutput frag ( PackedVaryings input
								#if defined( ASE_DEPTH_WRITE_ON )
								,out float outputDepth : ASE_SV_DEPTH
								#endif
								 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float2 vertexToFrag286_g41 = input.ase_texcoord7.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				float localSplatClip276_g41 = ( dotResult278_g41 );
				float SplatWeight276_g41 = dotResult278_g41;
				{
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float4 Control26_g41 = ( tex2DNode283_g41 / ( localSplatClip276_g41 + 0.001 ) );
				float2 uv_Splat0 = input.ase_texcoord7.zw * _Splat0_ST.xy + _Splat0_ST.zw;
				float3 Splat0342_g41 = (SAMPLE_TEXTURE2D( _Splat0, sampler_Splat0, uv_Splat0 )).rgb;
				float2 uv_Splat1 = input.ase_texcoord7.zw * _Splat1_ST.xy + _Splat1_ST.zw;
				float3 Splat1379_g41 = (SAMPLE_TEXTURE2D( _Splat1, sampler_Splat0, uv_Splat1 )).rgb;
				float2 uv_Splat2 = input.ase_texcoord7.zw * _Splat2_ST.xy + _Splat2_ST.zw;
				float3 Splat2357_g41 = (SAMPLE_TEXTURE2D( _Splat2, sampler_Splat0, uv_Splat2 )).rgb;
				float2 uv_Splat3 = input.ase_texcoord7.zw * _Splat3_ST.xy + _Splat3_ST.zw;
				float3 Splat3390_g41 = (SAMPLE_TEXTURE2D( _Splat3, sampler_Splat0, uv_Splat3 )).rgb;
				float4 weightedBlendVar9_g41 = Control26_g41;
				float3 weightedBlend9_g41 = ( weightedBlendVar9_g41.x*( Splat0342_g41 * (_DiffuseRemapScale0).rgb ) + weightedBlendVar9_g41.y*( Splat1379_g41 * (_DiffuseRemapScale1).rgb ) + weightedBlendVar9_g41.z*( Splat2357_g41 * (_DiffuseRemapScale2).rgb ) + weightedBlendVar9_g41.w*( Splat3390_g41 * (_DiffuseRemapScale3).rgb ) );
				float3 localClipHoles453_g41 = ( weightedBlend9_g41 );
				float2 uv_TerrainHolesTexture = input.ase_texcoord7.zw * _TerrainHolesTexture_ST.xy + _TerrainHolesTexture_ST.zw;
				float Hole453_g41 = SAMPLE_TEXTURE2D( _TerrainHolesTexture, sampler_TerrainHolesTexture, uv_TerrainHolesTexture ).r;
				{
				#ifdef _ALPHATEST_ON
				clip(Hole453_g41 == 0.0f ? -1 : 1);
				#endif
				}
				float3 BaseColorOut79 = localClipHoles453_g41;
				float2 uv_ColorMapProjection324 = input.ase_texcoord7.zw;
				float4 ColorMap_Top331 = ( unity_ColorSpaceDouble * SAMPLE_TEXTURE2D( _ColorMapProjection, sampler_ColorMapProjection, uv_ColorMapProjection324 ) );
				float4 lerpResult271 = lerp( float4( BaseColorOut79 , 0.0 ) , ( ColorMap_Top331 * float4( BaseColorOut79 , 0.0 ) ) , _ColorMapIntensity);
				float3 ase_parentObjectScale = ( 1.0 / float3( length( GetWorldToObjectMatrix()[ 0 ].xyz ), length( GetWorldToObjectMatrix()[ 1 ].xyz ), length( GetWorldToObjectMatrix()[ 2 ].xyz ) ) );
				float2 appendResult304 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
				float2 appendResult305 = (float2(PositionWS.x , PositionWS.z));
				float2 temp_output_308_0 = ( appendResult304 * appendResult305 );
				float simpleNoise314 = SimpleNoise( temp_output_308_0*400.0 );
				float temp_output_320_0 = (simpleNoise314*0.6 + 0.6);
				float4 temp_cast_4 = (temp_output_320_0).xxxx;
				float2 uv_HeatMap306 = input.ase_texcoord7.zw;
				float temp_output_310_0 = ( 1.0 - SAMPLE_TEXTURE2D( _HeatMap, sampler_HeatMap, uv_HeatMap306 ).b );
				float2 uv_NormalMap307 = input.ase_texcoord7.zw;
				float3 unpack307 = UnpackNormalScale( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap307 ), _NormalIntensity );
				unpack307.z = lerp( 1, unpack307.z, saturate(_NormalIntensity) );
				float3 tex2DNode307 = unpack307;
				float temp_output_317_0 = saturate( NormalWS.y );
				float3 _Tangent = float3(0,1,0);
				float3x3 ase_tangentToWorldFast = float3x3( TangentWS.x, BitangentWS.x, NormalWS.x, TangentWS.y, BitangentWS.y, NormalWS.y, TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tangentToWorldDir284 = normalize( mul( ase_tangentToWorldFast, _Tangent ) );
				float dotResult285 = dot( tangentToWorldDir284 , _Tangent );
				float lerpResult289 = lerp( _SlopeMax , _SlopeMin , abs( dotResult285 ));
				float Slope291 = saturate( lerpResult289 );
				float GlobalVar_SnowAmount410 = _SnowAmount;
				float temp_output_334_0 = saturate( ( (( _NormalMapBasedSnow )?( ( (temp_output_310_0*-4.39 + 1.1) * (tex2DNode307.g*_SnowScale + _SnowOffset) ) ):( (temp_output_310_0*_SnowScale + _SnowOffset) )) * (( _SnowSlope )?( ( temp_output_317_0 * Slope291 ) ):( temp_output_317_0 )) * GlobalVar_SnowAmount410 ) );
				float4 lerpResult277 = lerp( (( _ColorMap )?( lerpResult271 ):( float4( BaseColorOut79 , 0.0 ) )) , temp_cast_4 , temp_output_334_0);
				float4 AlbedoSnow_Output280 = lerpResult277;
				float GlobalVar_Wetness360 = _Wetness;
				float4 lerpResult378 = lerp( AlbedoSnow_Output280 , ( AlbedoSnow_Output280 * 0.7 ) , GlobalVar_Wetness360);
				float4 Albedo_Output380 = lerpResult378;
				
				float4 Normal0341_g41 = SAMPLE_TEXTURE2D( _Normal0, sampler_Normal0, uv_Splat0 );
				float3 unpack490_g41 = UnpackNormalScale( Normal0341_g41, _NormalScale0 );
				unpack490_g41.z = lerp( 1, unpack490_g41.z, saturate(_NormalScale0) );
				float4 Normal1378_g41 = SAMPLE_TEXTURE2D( _Normal1, sampler_Normal0, uv_Splat1 );
				float3 unpack496_g41 = UnpackNormalScale( Normal1378_g41, _NormalScale1 );
				unpack496_g41.z = lerp( 1, unpack496_g41.z, saturate(_NormalScale1) );
				float4 Normal2356_g41 = SAMPLE_TEXTURE2D( _Normal2, sampler_Normal0, uv_Splat2 );
				float3 unpack494_g41 = UnpackNormalScale( Normal2356_g41, _NormalScale2 );
				unpack494_g41.z = lerp( 1, unpack494_g41.z, saturate(_NormalScale2) );
				float4 Normal3398_g41 = SAMPLE_TEXTURE2D( _Normal3, sampler_Normal0, uv_Splat3 );
				float3 unpack491_g41 = UnpackNormalScale( Normal3398_g41, _NormalScale3 );
				unpack491_g41.z = lerp( 1, unpack491_g41.z, saturate(_NormalScale3) );
				float4 weightedBlendVar473_g41 = Control26_g41;
				float3 weightedBlend473_g41 = ( weightedBlendVar473_g41.x*unpack490_g41 + weightedBlendVar473_g41.y*unpack496_g41 + weightedBlendVar473_g41.z*unpack494_g41 + weightedBlendVar473_g41.w*unpack491_g41 );
				float3 break513_g41 = weightedBlend473_g41;
				float3 appendResult514_g41 = (float3(break513_g41.x , break513_g41.y , ( break513_g41.z + 0.001 )));
				#ifdef _TERRAIN_INSTANCED_PERPIXEL_NORMAL
				float3 staticSwitch503_g41 = appendResult514_g41;
				#else
				float3 staticSwitch503_g41 = appendResult514_g41;
				#endif
				float3 NormalOut80 = staticSwitch503_g41;
				float3 temp_output_111_0_g43 = ddx( PositionWS );
				float3 temp_output_113_0_g43 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g43 = dot( temp_output_111_0_g43 , temp_output_113_0_g43 );
				float simpleNoise319 = SimpleNoise( temp_output_308_0*2.0 );
				float temp_output_20_0_g43 = simpleNoise319;
				float3 normalizeResult130_g43 = normalize( ( ( abs( dotResult115_g43 ) * NormalWS ) - ( 8.0 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g43 ) * ( ( ddx( temp_output_20_0_g43 ) * temp_output_113_0_g43 ) + ( ddy( temp_output_20_0_g43 ) * cross( NormalWS , temp_output_111_0_g43 ) ) ) ) ) );
				float3x3 ase_worldToTangent = float3x3( TangentWS, BitangentWS, NormalWS );
				float3 worldToTangentDir42_g43 = mul( ase_worldToTangent, normalizeResult130_g43 );
				float3 temp_output_111_0_g42 = ddx( PositionWS );
				float3 temp_output_113_0_g42 = cross( ddy( PositionWS ) , NormalWS );
				float dotResult115_g42 = dot( temp_output_111_0_g42 , temp_output_113_0_g42 );
				float temp_output_20_0_g42 = temp_output_320_0;
				float3 normalizeResult130_g42 = normalize( ( ( abs( dotResult115_g42 ) * NormalWS ) - ( 0.06 * float3( 0.05,0.05,0.05 ) * sign( dotResult115_g42 ) * ( ( ddx( temp_output_20_0_g42 ) * temp_output_113_0_g42 ) + ( ddy( temp_output_20_0_g42 ) * cross( NormalWS , temp_output_111_0_g42 ) ) ) ) ) );
				float3 worldToTangentDir42_g42 = mul( ase_worldToTangent, normalizeResult130_g42 );
				float2 temp_output_1_0_g44 = BlendNormal( worldToTangentDir42_g43 , worldToTangentDir42_g42 ).xy;
				float2 break13_g44 = temp_output_1_0_g44;
				float dotResult4_g44 = dot( temp_output_1_0_g44 , temp_output_1_0_g44 );
				float3 appendResult10_g44 = (float3(break13_g44.x , break13_g44.y , sqrt( ( 1.0 - saturate( dotResult4_g44 ) ) )));
				float3 normalizeResult12_g44 = normalize( appendResult10_g44 );
				float3 lerpResult296 = lerp( NormalOut80 , normalizeResult12_g44 , temp_output_334_0);
				float3 NormalMap_Top336 = tex2DNode307;
				float3 NormalSnow_Output281 = (( _TerrainNormalMap )?( BlendNormal( NormalMap_Top336 , lerpResult296 ) ):( lerpResult296 ));
				
				float4 tex2DNode416_g41 = SAMPLE_TEXTURE2D( _Mask0, sampler_Mask0, uv_Splat0 );
				float Mask0R334_g41 = tex2DNode416_g41.r;
				float4 tex2DNode422_g41 = SAMPLE_TEXTURE2D( _Mask1, sampler_Mask0, uv_Splat1 );
				float Mask1R370_g41 = tex2DNode422_g41.r;
				float4 tex2DNode419_g41 = SAMPLE_TEXTURE2D( _Mask2, sampler_Mask0, uv_Splat2 );
				float Mask2R359_g41 = tex2DNode419_g41.r;
				float4 tex2DNode425_g41 = SAMPLE_TEXTURE2D( _Mask3, sampler_Mask0, uv_Splat3 );
				float Mask3R388_g41 = tex2DNode425_g41.r;
				float4 weightedBlendVar536_g41 = Control26_g41;
				float weightedBlend536_g41 = ( weightedBlendVar536_g41.x*max( _Metallic0 , Mask0R334_g41 ) + weightedBlendVar536_g41.y*max( _Metallic1 , Mask1R370_g41 ) + weightedBlendVar536_g41.z*max( _Metallic2 , Mask2R359_g41 ) + weightedBlendVar536_g41.w*max( _Metallic3 , Mask3R388_g41 ) );
				
				float fresnelNdotV401 = dot( normalize( NormalWS ), ViewDirWS );
				float fresnelNode401 = ( 1.0 + -1.0 * pow( max( 1.0 - fresnelNdotV401 , 0.0001 ), 1.0 ) );
				float4 appendResult1168_g41 = (float4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
				float Splat0A435_g41 = tex2DNode416_g41.a;
				float Mask1A369_g41 = tex2DNode422_g41.a;
				float Mask2A360_g41 = tex2DNode419_g41.a;
				float Mask3A391_g41 = tex2DNode425_g41.a;
				float4 appendResult1169_g41 = (float4(Splat0A435_g41 , Mask1A369_g41 , Mask2A360_g41 , Mask3A391_g41));
				float dotResult1166_g41 = dot( ( appendResult1168_g41 * appendResult1169_g41 ) , Control26_g41 );
				float SmoothOut157 = ( _SmoothnessIntensity * dotResult1166_g41 );
				float lerpResult279 = lerp( SmoothOut157 , (simpleNoise314*1.4 + 0.0) , temp_output_334_0);
				float Smoothness_Final282 = lerpResult279;
				float Smoothness_Output369 = saturate( ( ( GlobalVar_Wetness360 * fresnelNode401 ) + Smoothness_Final282 ) );
				
				float Mask0G409_g41 = tex2DNode416_g41.g;
				float Mask1G371_g41 = tex2DNode422_g41.g;
				float Mask2G358_g41 = tex2DNode419_g41.g;
				float Mask3G389_g41 = tex2DNode425_g41.g;
				float4 weightedBlendVar602_g41 = Control26_g41;
				float weightedBlend602_g41 = ( weightedBlendVar602_g41.x*saturate( Mask0G409_g41 ) + weightedBlendVar602_g41.y*saturate( Mask1G371_g41 ) + weightedBlendVar602_g41.z*saturate( Mask2G358_g41 ) + weightedBlendVar602_g41.w*saturate( Mask3G389_g41 ) );
				float lerpResult408 = lerp( saturate( weightedBlend602_g41 ) , 1.0 , GlobalVar_SnowAmount410);
				
				float3 temp_cast_12 = (( _TTFETERRAINSNOWSHADER + _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _ADVANCEDSETTINGS )).xxx;
				

				float3 BaseColor = Albedo_Output380.rgb;
				float3 Normal = NormalSnow_Output281;
				float3 Specular = 0.5;
				float Metallic = weightedBlend536_g41;
				float Smoothness = Smoothness_Output369;
				float Occlusion = lerpResult408;
				float3 Emission = temp_cast_12;
				float Alpha = dotResult278_g41;
				float AlphaClipThreshold = 0.0;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = ClipPos.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = float4( input.positionCS.xy, ClipPos.zw / ClipPos.w );
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
						inputData.normalWS = TransformTangentToWorld(Normal, half3x3( TangentWS, BitangentWS, NormalWS ));
					#elif _NORMAL_DROPOFF_OS
						inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
						inputData.normalWS = Normal;
					#endif
				#else
					inputData.normalWS = NormalWS;
				#endif

				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.viewDirectionWS = SafeNormalize( ViewDirWS );

				#ifdef ASE_FOG
					// @diogo: no fog applied in GBuffer
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI(SH,
						GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask);
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				#ifdef _DBUFFER
					ApplyDecal(input.positionCS,
						BaseColor,
						Specular,
						inputData.normalWS,
						Metallic,
						Occlusion,
						Smoothness);
				#endif

				BRDFData brdfData;
				InitializeBRDFData(BaseColor, Metallic, Specular, Smoothness, Alpha, brdfData);

				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
				half4 color;
				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
				color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, Occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
				color.a = Alpha;

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				return BRDFDataToGbuffer(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			}

			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.ase_texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord1.xy = vertexToFrag286_g41;
				
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_DEPTH_WRITE_ON )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				float2 vertexToFrag286_g41 = input.ase_texcoord1.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				

				surfaceDescription.Alpha = dotResult278_g41;
				surfaceDescription.AlphaClipThreshold = 0.0;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				return half4( _ObjectId, _PassValue, 1.0, 1.0 );
			}

			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_TEXTURE_COORDINATES0
			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
			#define TERRAIN_SPLAT_FIRSTPASS 1


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			TEXTURE2D(_Control);
			SAMPLER(sampler_Control);
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				float2 break291_g41 = _Control_ST.zw;
				float2 appendResult293_g41 = (float2(( break291_g41.x + 0.001 ) , ( break291_g41.y + 0.0001 )));
				float2 vertexToFrag286_g41 = ( ( input.ase_texcoord.xy * _Control_ST.xy ) + appendResult293_g41 );
				output.ase_texcoord1.xy = vertexToFrag286_g41;
				
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_DEPTH_WRITE_ON )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				float2 vertexToFrag286_g41 = input.ase_texcoord1.xy;
				float4 tex2DNode283_g41 = SAMPLE_TEXTURE2D( _Control, sampler_Control, vertexToFrag286_g41 );
				float dotResult278_g41 = dot( tex2DNode283_g41 , half4( 1, 1, 1, 1 ) );
				

				surfaceDescription.Alpha = dotResult278_g41;
				surfaceDescription.AlphaClipThreshold = 0.0;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
						clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				return unity_SelectionID;
			}
			ENDHLSL
		}

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"

		Pass
		{
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_FINAL_COLOR_ALPHA_MULTIPLY 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

            #define SHADERPASS SHADERPASS_MOTION_VECTORS

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

			#define ASE_INSTANCED_TERRAIN
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_VERT_POSITION
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 positionOld : TEXCOORD4;
				#if _ADD_PRECOMPUTED_VELOCITY
					float3 alembicMotionVector : TEXCOORD5;
				#endif
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 positionCSNoJitter : TEXCOORD0;
				float4 previousPositionCSNoJitter : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Control_ST;
			float4 _Splat0_ST;
			float4 _Splat1_ST;
			float4 _Splat2_ST;
			float4 _Splat3_ST;
			float4 _TerrainHolesTexture_ST;
			float _ColorMap;
			float _Metallic2;
			float _Metallic3;
			float _SmoothnessIntensity;
			float _Smoothness0;
			float _Smoothness1;
			float _TTFETERRAINSNOWSHADER;
			float _Smoothness3;
			float _Metallic1;
			float _TEXTUREMAPS;
			float _TEXTURESETTINGS;
			float _Smoothness2;
			float _Metallic0;
			half _NormalScale0;
			half _NormalScale2;
			half _NormalScale1;
			float _SEASONSETTINGS;
			float _TerrainNormalMap;
			float _SlopeMin;
			float _SlopeMax;
			float _SnowSlope;
			float _NormalIntensity;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _ColorMapIntensity;
			half _NormalScale3;
			float _ADVANCEDSETTINGS;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				TEXTURE2D(_TerrainHeightmapTexture);//ASE Terrain Instancing
				TEXTURE2D( _TerrainNormalmapTexture);//ASE Terrain Instancing
				SAMPLER(sampler_TerrainNormalmapTexture);//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
				UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
			UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
			CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
				#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
					float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
					float4 _TerrainHeightmapScale;//ASE Terrain Instancing
				#endif//ASE Terrain Instancing
			CBUFFER_END//ASE Terrain Instancing


			Attributes ApplyMeshModification( Attributes input )
			{
			#ifdef UNITY_INSTANCING_ENABLED
				float2 patchVertex = input.positionOS.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP( Terrain, _TerrainPatchInstanceData );
				float2 sampleCoords = ( patchVertex.xy + instanceData.xy ) * instanceData.z;
				float height = UnpackHeightmap( _TerrainHeightmapTexture.Load( int3( sampleCoords, 0 ) ) );
				input.positionOS.xz = sampleCoords* _TerrainHeightmapScale.xz;
				input.positionOS.y = height* _TerrainHeightmapScale.y;
				#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
					input.normalOS = float3(0, 1, 0);
				#else
					input.normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb* 2 - 1;
				#endif
				input.ase_texcoord.xy = sampleCoords* _TerrainHeightmapRecipSize.zw;
			#endif
				return input;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				input = ApplyMeshModification(input);
				output.ase_texcoord3 = input.ase_texcoord;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(APLICATION_SPACE_WARP_MOTION)
					output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
					output.positionCS = output.positionCSNoJitter;
				#else
					output.positionCS = vertexInput.positionCS;
					output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
				#endif

				float4 prevPos = ( unity_MotionVectorsParams.x == 1 ) ? float4( input.positionOld, 1 ) : input.positionOS;

				#if _ADD_PRECOMPUTED_VELOCITY
					prevPos = prevPos - float4(input.alembicMotionVector, 0);
				#endif

				output.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, prevPos ) );

				output.positionWS = vertexInput.positionWS;

				// removed in ObjectMotionVectors.hlsl found in unity 6000.0.23 and higher
				//ApplyMotionVectorZBias( output.positionCS );
				return output;
			}

			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}

			half4 frag(	PackedVaryings input
				#if defined( ASE_DEPTH_WRITE_ON )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(ASE_CHANGES_WORLD_POS)
					float3 positionOS = mul( GetWorldToObjectMatrix(),  float4( PositionWS, 1.0 ) ).xyz;
					float3 previousPositionWS = mul( GetPrevObjectToWorldMatrix(),  float4( positionOS, 1.0 ) ).xyz;
					input.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, float4( PositionWS, 1.0 ) );
					input.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, float4( previousPositionWS, 1.0 ) );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				#if defined(APLICATION_SPACE_WARP_MOTION)
					return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
				#else
					return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
				#endif
			}
			ENDHLSL
		}

	
	}
	
	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"
	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass"

	Fallback Off
}

/*ASEBEGIN
Version=19904
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;261;-4400,-272;Inherit;False;3703.237;2599.825;Settings for season control, specular, and others.;1;262;Mesh Settings;0,1,0.1098039,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;407;-2530.354,-1536;Inherit;False;1207.251;460.3962;;7;405;404;403;402;401;400;366;Wetness + Smoothness;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;262;-4304,-96;Inherit;False;3531.514;2339.1;;7;339;268;267;266;265;264;263;Snow Control + Output;0.7490196,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;358;-944,-1504;Inherit;False;923.4883;554.4142;;7;368;378;375;376;373;372;371;Albedo Wetness_Output;0.2641509,0.2641509,0.2641509,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;260;-4288,-1648;Inherit;False;1386.182;442.4717;;9;291;290;289;288;287;286;285;284;283;Slope Function;0.5471698,0.2180016,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;263;-4208,32;Inherit;False;2036;1379;;26;340;338;336;334;332;331;329;328;327;324;323;322;321;318;317;316;315;313;312;311;310;309;307;306;303;412;Main Maps;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;264;-3920,1536;Inherit;False;580;411;Keeps the same UV scale no matter the object scaling.;5;308;305;304;302;301;Scale Independent Position;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;267;-2112,448;Inherit;False;1268;564;;9;299;297;295;293;292;280;277;273;271;Albedo_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;265;-3296,1616;Inherit;False;1332;544;Create snow noise and convert height to normals.;8;337;335;333;326;325;320;319;314;Noise Generator;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;268;-1856,1488;Inherit;False;676;340;;4;300;282;279;276;Smoothness_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;355;-4256,-1040;Inherit;False;659.6702;248.2775;Comment;4;410;330;360;357;Global Var;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;266;-2096,1104;Inherit;False;1220;308;;8;298;296;294;281;278;275;274;272;Normal_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;252;400,-592;Inherit;False;517;484;;6;354;345;256;254;253;258;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;366;-1712,-1472;Inherit;False;324;163;;1;369;Smoothness_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;368;-352,-1328;Inherit;False;292;163;;1;380;Albedo_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;399;-368,144;Inherit;False;(TTFE) Terrain 4 Layer;18;;41;cbdebdb4ac1110445ac18699eb78ddd1;2,504,1,102,1;0;8;FLOAT3;0;FLOAT3;14;FLOAT;56;FLOAT;45;FLOAT;200;FLOAT;282;FLOAT3;709;FLOAT4;701
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;283;-4240,-1392;Inherit;False;Constant;_Tangent;Tangent;33;0;Create;True;0;0;0;False;0;False;0,1,0;0,1,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TransformDirectionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;284;-4048,-1520;Inherit;False;Tangent;World;True;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DotProductOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;285;-3792,-1424;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;286;-3648,-1408;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;287;-3664,-1504;Inherit;False;Property;_SlopeMin;Slope Min;13;0;Create;True;0;0;0;False;0;False;0;0.31;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;288;-3680,-1584;Inherit;False;Property;_SlopeMax;Slope Max;12;1;[Header];Create;True;1;(Slope);0;0;False;0;False;0;0.01;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;303;-4160,544;Inherit;False;Property;_NormalIntensity;Normal Intensity;8;1;[Header];Create;True;1;(Normal);0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;289;-3488,-1472;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;290;-3312,-1376;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;306;-3840,704;Inherit;True;Property;_HeatMap;Heat Map;4;1;[NoScaleOffset];Create;True;3;______________(TTFE) TERRAIN SNOW________________;_____________________________________________________;Texture Maps;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;27e7f7e1dba8adc4aa99d88272615d39;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;311;-3424,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;291;-3136,-1376;Inherit;False;Slope;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorSpaceDouble, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;340;-3792,80;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;324;-3856,272;Inherit;True;Property;_ColorMapProjection;Color Map Projection;2;1;[NoScaleOffset];Create;True;0;0;0;False;2;Space (10);TTFE_Drawer_SingleLineTexture;False;-1;None;2ba8a82146d1460448cd7a0845be28ed;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;309;-3648,1040;Inherit;False;Property;_SnowOffset;Snow Offset;14;0;Create;True;0;0;0;False;0;False;0;0.79;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;310;-3552,800;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;312;-3648,944;Inherit;False;Property;_SnowScale;Snow Scale;15;0;Create;True;0;0;0;False;0;False;0;7.61;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;313;-3648,1136;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ObjectScaleNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;301;-3872,1584;Inherit;False;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;302;-3872,1760;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;315;-3360,864;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;3.33;False;2;FLOAT;-1.04;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;316;-3360,656;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;-4.39;False;2;FLOAT;1.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;318;-3648,1296;Inherit;False;291;Slope;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;328;-3552,192;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;317;-3456,1184;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;304;-3680,1616;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;305;-3680,1792;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;321;-3376,1024;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;3.33;False;2;FLOAT;-1.04;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;323;-3296,1264;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;331;-3392,192;Inherit;False;ColorMap_Top;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;223;-464,-48;Inherit;False;Property;_SmoothnessIntensity;Smoothness Intensity;10;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;308;-3520,1696;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;327;-2880,1024;Inherit;False;Property;_NormalMapBasedSnow;Normal Map Based Snow;17;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;329;-3136,1184;Inherit;False;Property;_SnowSlope;Snow Slope;16;0;Create;True;0;0;0;False;0;False;1;True;Create;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;293;-2064,496;Inherit;False;331;ColorMap_Top;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.NoiseGeneratorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;319;-3248,1904;Inherit;True;Simple;True;False;2;0;FLOAT2;1,1;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;222;-112,-112;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;314;-3248,1664;Inherit;True;Simple;True;False;2;0;FLOAT2;1,1;False;1;FLOAT;400;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;332;-2592,1104;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;295;-1840,512;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;297;-2064,656;Inherit;False;Property;_ColorMapIntensity;Color Map Intensity;6;1;[Header];Create;True;1;(Color Map);0;0;False;0;False;1;0.1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;325;-2752,1792;Inherit;False;Normal From Height;-1;;42;1942fe2c5f1a1f94881a33d532e4afeb;0;2;20;FLOAT;0;False;110;FLOAT;0.06;False;2;FLOAT3;40;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;326;-2752,1920;Inherit;False;Normal From Height;-1;;43;1942fe2c5f1a1f94881a33d532e4afeb;0;2;20;FLOAT;0;False;110;FLOAT;8;False;2;FLOAT3;40;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;337;-3008,1664;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1.4;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;292;-2064,576;Inherit;False;79;BaseColorOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;320;-3008,1792;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.6;False;2;FLOAT;0.6;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;271;-1680,560;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BlendNormalsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;333;-2464,1840;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;276;-1696,1744;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;339;-2176,1504;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;300;-1808,1536;Inherit;False;157;SmoothOut;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;273;-1504,656;Inherit;False;Property;_ColorMap;Color Map;7;0;Create;True;0;0;0;False;0;False;1;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;299;-1904,928;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;338;-2608,1072;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;357;-4208,-976;Inherit;False;Global;_Wetness;_Wetness;18;1;[Header];Create;True;1;(Wetness);0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;294;-2032,1248;Inherit;False;80;NormalOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;335;-2240,1856;Inherit;False;Normal Reconstruct Z;-1;;44;63ba85b764ae0c84ab3d698b86364ae9;1,15,1;1;1;FLOAT2;0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;279;-1616,1552;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;277;-1280,784;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;296;-1840,1248;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;336;-3536,496;Inherit;False;NormalMap_Top;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;282;-1440,1552;Inherit;False;Smoothness_Final;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;280;-1120,784;Inherit;False;AlbedoSnow_Output;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;371;-864,-1072;Inherit;False;Constant;_Float6;Float 6;48;0;Create;True;0;0;0;False;0;False;0.7;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;272;-1664,1328;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;298;-1840,1152;Inherit;False;336;NormalMap_Top;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;373;-832,-1360;Inherit;False;360;GlobalVar_Wetness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;372;-896,-1168;Inherit;False;280;AlbedoSnow_Output;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;376;-688,-1072;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;375;-624,-1200;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;274;-1616,1216;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;275;-1440,1328;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;378;-512,-1280;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;278;-1392,1248;Inherit;False;Property;_TerrainNormalMap;Terrain Normal Map;9;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;253;432,-528;Inherit;False;Property;_TTFETERRAINSNOWSHADER;(TTFE) TERRAIN SNOW SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;254;560,-448;Inherit;False;Property;_TEXTUREMAPS;TEXTURE MAPS;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;256;528,-368;Inherit;False;Property;_TEXTURESETTINGS;TEXTURE SETTINGS;5;1;[Header];Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;345;528,-288;Inherit;False;Property;_SEASONSETTINGS;SEASON SETTINGS;11;1;[Header];Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;354;512,-208;Inherit;False;Property;_ADVANCEDSETTINGS;ADVANCED SETTINGS;46;1;[Header];Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;281;-1136,1248;Inherit;False;NormalSnow_Output;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;258;768,-448;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;380;-320,-1280;Inherit;False;Albedo_Output;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;193;304,48;Inherit;False;281;NormalSnow_Output;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;391;336,240;Inherit;False;Constant;_AlphaClipThreshold1;AlphaClipThreshold;1;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;157;64,-48;Inherit;False;SmoothOut;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;79;64,48;Inherit;False;BaseColorOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;80;64,144;Inherit;False;NormalOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;192;304,-48;Inherit;False;380;Albedo_Output;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;322;-3104,768;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;307;-3856,496;Inherit;True;Property;_NormalMap;Normal Map;3;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;8e843cdc8a31a59438a600598713dc81;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;194;304,144;Inherit;False;369;Smoothness_Output;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;394;848,448;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;395;800,400;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;396;800,336;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;398;672,304;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;334;-2416,1104;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;369;-1664,-1408;Inherit;False;Smoothness_Output;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;400;-2384,-1472;Inherit;False;360;GlobalVar_Wetness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FresnelNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;401;-2480,-1392;Inherit;True;Standard;WorldNormal;ViewDir;True;True;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;1;False;2;FLOAT;-1;False;3;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;402;-2112,-1424;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;403;-2400,-1168;Inherit;False;282;Smoothness_Final;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;404;-1968,-1328;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;405;-1856,-1408;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;330;-4192,-880;Inherit;False;Global;_SnowAmount;_Snow Amount;16;1;[Header];Create;True;3;_____________________________________________________;Season Settings;(Snow Control);0;0;False;0;False;1;0;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;360;-3888,-976;Inherit;False;GlobalVar_Wetness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;410;-3904,-880;Inherit;False;GlobalVar_SnowAmount;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;412;-2864,1216;Inherit;False;410;GlobalVar_SnowAmount;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;414;896,496;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;408;-96,432;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;409;-368,544;Inherit;False;410;GlobalVar_SnowAmount;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;413;-272,448;Inherit;False;Constant;_Float0;Float 0;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;381;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;6;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;382;960,32;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=-100;UniversalMaterialType=Lit;TerrainCompatible=True;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;2;LightMode=UniversalForwardOnly;TerrainCompatible=True;False;False;4;Include;;False;;Native;False;0;0;;Define;TERRAIN_SPLAT_FIRSTPASS 1;False;;Custom;True;0;0;;Pragma;instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap;False;;Custom;True;0;0;Forward, GBuffer;Pragma;multi_compile_instancing;False;;Custom;True;0;0;Forward,GBuffer,ShadowCaster,DepthOnly,DepthNormals;;64;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;BaseMapShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/Dependencies/Terrain Extra Pass/(TTFE) Terrain Snow AddPass;0;Standard;51;Category;0;0;  Instanced Terrain Normals;1;0;Lighting Model;0;0;Workflow;1;638929394456478958;Surface;0;0;  Keep Alpha;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;1;0;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,True,_ASETransmissionShadow;0;Translucency;0;0;  Translucency Strength;1,True,_ASETranslucencyStrength;0;  Normal Distortion;0.5,True,_ASETranslucencyNormalDistortion;0;  Scattering;2,True,_ASETranslucencyScattering;0;  Direct;0.9,True,_ASETranslucencyDirect;0;  Ambient;0.1,True,_ASETranslucencyAmbient;0;  Shadow;0.5,True,_ASETranslucencyShadow;0;Cast Shadows;1;0;Receive Shadows;1;638929372097623758;Specular Highlights;1;638929372106259665;Environment Reflections;1;638929372116420515;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;  XR Motion Vectors;0;0;GPU Instancing;0;638931003139645199;LOD CrossFade;0;638929389731978513;Built-in Fog;1;0;_FinalColorxAlpha;1;638929390421031459;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,True,_TessellationPhong;0;  Type;0;0;  Tess;16,True,_TessellationStrength;0;  Min;10,True,_TessellationDistanceMin;0;  Max;25,True,_TessellationDistanceMax;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position;1;0;Debug Display;0;638929371445599660;Clear Coat;0;0;0;12;False;True;True;True;True;True;True;True;True;True;True;False;True;;True;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;383;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;384;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;385;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;386;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;387;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;388;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;389;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;390;960,32;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;415;960,132;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;416;960,132;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;XRMotionVectors;0;11;XRMotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;True;1;False;;255;False;;1;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;1;LightMode=XRMotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;269;-4368,-592;Inherit;False;3647.034;100;;0;MESH SETTINGS;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;270;-4368,-1984;Inherit;False;3276.735;100;;0;GLOBAL FUNCTIONS;1,1,1,1;0;0
WireConnection;284;0;283;0
WireConnection;285;0;284;0
WireConnection;285;1;283;0
WireConnection;286;0;285;0
WireConnection;289;0;288;0
WireConnection;289;1;287;0
WireConnection;289;2;286;0
WireConnection;290;0;289;0
WireConnection;311;0;307;2
WireConnection;291;0;290;0
WireConnection;310;0;306;3
WireConnection;315;0;311;0
WireConnection;315;1;312;0
WireConnection;315;2;309;0
WireConnection;316;0;310;0
WireConnection;328;0;340;0
WireConnection;328;1;324;0
WireConnection;317;0;313;2
WireConnection;304;0;301;1
WireConnection;304;1;301;3
WireConnection;305;0;302;1
WireConnection;305;1;302;3
WireConnection;321;0;310;0
WireConnection;321;1;312;0
WireConnection;321;2;309;0
WireConnection;323;0;317;0
WireConnection;323;1;318;0
WireConnection;331;0;328;0
WireConnection;308;0;304;0
WireConnection;308;1;305;0
WireConnection;327;0;321;0
WireConnection;327;1;322;0
WireConnection;329;0;317;0
WireConnection;329;1;323;0
WireConnection;319;0;308;0
WireConnection;222;0;223;0
WireConnection;222;1;399;45
WireConnection;314;0;308;0
WireConnection;332;0;327;0
WireConnection;332;1;329;0
WireConnection;332;2;412;0
WireConnection;295;0;293;0
WireConnection;295;1;292;0
WireConnection;325;20;320;0
WireConnection;326;20;319;0
WireConnection;337;0;314;0
WireConnection;320;0;314;0
WireConnection;271;0;292;0
WireConnection;271;1;295;0
WireConnection;271;2;297;0
WireConnection;333;0;326;40
WireConnection;333;1;325;40
WireConnection;276;0;337;0
WireConnection;339;0;334;0
WireConnection;273;0;292;0
WireConnection;273;1;271;0
WireConnection;299;0;334;0
WireConnection;338;0;320;0
WireConnection;335;1;333;0
WireConnection;279;0;300;0
WireConnection;279;1;276;0
WireConnection;279;2;339;0
WireConnection;277;0;273;0
WireConnection;277;1;338;0
WireConnection;277;2;299;0
WireConnection;296;0;294;0
WireConnection;296;1;335;0
WireConnection;296;2;334;0
WireConnection;336;0;307;0
WireConnection;282;0;279;0
WireConnection;280;0;277;0
WireConnection;272;0;296;0
WireConnection;376;0;372;0
WireConnection;376;1;371;0
WireConnection;375;0;373;0
WireConnection;274;0;298;0
WireConnection;274;1;296;0
WireConnection;275;0;272;0
WireConnection;378;0;372;0
WireConnection;378;1;376;0
WireConnection;378;2;375;0
WireConnection;278;0;275;0
WireConnection;278;1;274;0
WireConnection;281;0;278;0
WireConnection;258;0;253;0
WireConnection;258;1;254;0
WireConnection;258;2;256;0
WireConnection;258;3;345;0
WireConnection;258;4;354;0
WireConnection;380;0;378;0
WireConnection;157;0;222;0
WireConnection;79;0;399;0
WireConnection;80;0;399;14
WireConnection;322;0;316;0
WireConnection;322;1;315;0
WireConnection;307;5;303;0
WireConnection;394;0;399;701
WireConnection;395;0;399;709
WireConnection;396;0;399;282
WireConnection;398;0;399;56
WireConnection;334;0;332;0
WireConnection;369;0;405;0
WireConnection;402;0;400;0
WireConnection;402;1;401;0
WireConnection;404;0;402;0
WireConnection;404;1;403;0
WireConnection;405;0;404;0
WireConnection;360;0;357;0
WireConnection;410;0;330;0
WireConnection;414;0;408;0
WireConnection;408;0;399;200
WireConnection;408;1;413;0
WireConnection;408;2;409;0
WireConnection;382;0;192;0
WireConnection;382;1;193;0
WireConnection;382;3;398;0
WireConnection;382;4;194;0
WireConnection;382;5;414;0
WireConnection;382;2;258;0
WireConnection;382;6;396;0
WireConnection;382;7;391;0
WireConnection;382;10;395;0
WireConnection;382;30;394;0
ASEEND*/
//CHKSM=F9788471F3573DB359CB4A49298B490B66A0DA20