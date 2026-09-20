// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Billboard"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[TTFE_DrawerTitle] _TTFETREEBILLBOARDSHADER( "(TTFE) TREE BILLBOARD SHADER", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _FACERENDERING( "FACE RENDERING", Float ) = 0
		[Space (10)] _AlphaClip( "Alpha Clip", Float ) = 0.4
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS( "TEXTURE MAPS", Float ) = 0
		[NoScaleOffset][Space (10)][TTFE_Drawer_SingleLineTexture] _AlbedoMap( "Albedo Map", 2D ) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture] _NormalMap( "Normal Map", 2D ) = "bump" {}
		[Normal][TTFE_Drawer_SingleLineTexture] _SecondaryNormalMapCustomNormals( "Secondary Normal Map (Custom Normals)", 2D ) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _SpecularMap( "Specular Map (Grayscale)", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _MaskMapRGBA( "Mask Map *RGB(A)", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _NoiseMapGrayscale( "Noise Map (Grayscale)", 2D ) = "white" {}
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTURESETTINGS( "TEXTURE SETTINGS", Float ) = 0
		[Header((Albedo))] _AlbedoColor( "Albedo Color", Color ) = ( 1, 1, 1, 0 )
		[Header((Normal))] _NormalIntenisty( "Normal Intenisty", Float ) = 1
		_SecondaryNormalIntensity( "Secondary Normal Intensity", Float ) = 0
		_SecondaryNormalScale( "Secondary Normal Scale", Float ) = 0
		[Header((Smoothness))] _SmoothnessIntensity( "Smoothness Intensity", Range( 0, 1 ) ) = 1
		[Header((Ambient Occlusion))] _AmbientOcclusionIntensity( "Ambient Occlusion Intensity", Range( 0, 1 ) ) = 1
		[Header((Specular))] _SpecularPower( "Specular Power", Range( 0, 1 ) ) = 1
		[Toggle] _SpecularBackfaceOcclusion( "Specular Backface Occlusion", Float ) = 0
		_SpecularStrength( "Specular Strength", Float ) = 2
		_SpecularBias( "Specular Bias", Float ) = 1
		_SpecularScale( "Specular Scale", Float ) = -5
		_SpecularMapScale( "Specular Map Scale", Float ) = 1
		_SpecularMapOffset( "Specular Map Offset", Float ) = 0
		[Header((Translucency))] _TranslucencyPower( "Translucency Power", Range( 1, 10 ) ) = 1
		_TranslucencyRange( "Translucency Range", Float ) = 1
		[Toggle] _TranslucencyTreeTangents( "Translucency Tree Tangents", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _SHADINGSETTINGS( "SHADING SETTINGS", Float ) = 0
		[Header((Self Shading))] _VertexLighting( "Vertex Lighting", Float ) = 3
		_VertexShadow( "Vertex Shadow", Float ) = 0
		_VertexAo( "Vertex Ao", Range( 0, 1 ) ) = 0
		[Toggle] _SelfShadingVertexColor( "Self Shading (Vertex Color)", Float ) = 0
		[Toggle] _MobileShadingWorldUp( "Mobile Shading (World Up)", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _SEASONSETTINGS( "SEASON SETTINGS", Float ) = 0
		[Header((Color Variation))] _ColorVariation( "Color Variation", Range( 0, 1 ) ) = 0.2
		_RandomColorScale( "Random Color Scale", Float ) = 1
		[Header((Texture Based Color Variation))] _TBCVMapIntenisty( "TBCV Map Intenisty", Float ) = 2
		_ZaWorldoScale( "TBCV Wold Scale", Float ) = 1
		_TBCVMapOffset( "TBCV Map Offset", Float ) = -0.35
		[Toggle] _TBCVOnUsesNoiseMap( "TBCV On (Uses Noise Map)", Float ) = 0
		[Header((Random Contrast))] _RandomContrast( "Random Contrast", Range( 0, 1 ) ) = 0.3
		[Header((Texture Based Random Contrast))] _TBRCMapIntenisty( "TBRC Map Intenisty", Float ) = 1.6
		_TBRCWoldScale( "TBRC Wold Scale", Float ) = 2
		_TBRCMapOffset( "TBRC Map Offset", Float ) = -0.3
		[Toggle] _TBRCOnUsesNoiseMap( "TBRC On (Uses Noise Map)", Float ) = 0
		[Header((Season Controll))] _DryLeafColor( "Dry Leaf Color", Color ) = ( 0.5568628, 0.3730685, 0.1764706, 0 )
		_DryLeavesScale( "Dry Leaves - Scale", Float ) = 1
		_DryLeavesOffset( "Dry Leaves - Offset", Float ) = -0.5
		[Toggle] _SeasonVertexColorR( "Season Vertex Color (R)", Float ) = 1
		[Toggle] _BranchMaskR( "Branch Mask *(R)", Float ) = 1
		[Header((Snow))] _SnowScale( "Snow Scale", Float ) = 20
		_SnowOffset( "Snow Offset", Float ) = -5
		[Toggle] _NormalMapBasedSnow( "Normal Map Based Snow", Float ) = 0
		[Toggle] _SnowMaskVertexR( "Snow Mask Vertex (R)", Float ) = 0
		_SnowValue( "Snow Value", Float ) = 1
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS1( "WIND SETTINGS", Float ) = 0
		[Header((Trunk and Branch))] _PivotRandomnessStrength( "Pivot Randomness Strength", Float ) = 0.5
		_PivotRandomness( "Pivot Randomness ", Float ) = 1
		[KeywordEnum( GentleBreeze,StrongWind,WindOff )] _WindType( "Wind Type", Float ) = 1
		_BranchWindLarge( "Branch Wind Large", Range( 0, 20 ) ) = 1
		_BranchWindSmall( "Branch Wind Small", Range( 0, 20 ) ) = 1
		_BranchSwayPower( "Branch Sway Power", Float ) = 1
		_BranchFoldStrength( "Branch Fold Strength", Float ) = 0.5
		_MotionBendingGentleRandom( "Motion Bending Gentle Random", Float ) = 0.1
		_DownwardStrength( "Downward Strength", Float ) = -0.5
		[TTFE_DrawerFeatureBorder][Space (10)] _WINDMASKSETTINGS1( "WIND MASK SETTINGS", Float ) = 0
		[Header((Trunk Mask))] _TrunkHeightThickness( "Trunk Height and Thickness", Float ) = 0.01
		[Toggle] _CenterofMass( "Center of Mass", Float ) = 0
		[Toggle] _MaskRoots( "Mask Roots", Float ) = 1
		[Toggle] _MaskRootsAuto( "Mask Roots (Auto)", Float ) = 0
		[Header((Spherical Mask))] _Radius( "Radius", Float ) = 2
		_Hardness( "Hardness", Float ) = 1
		[Header((Branch Mask))] _BranchMaskScale( "Branch Mask Scale", Float ) = 0.1
		_BranchMaskRadious( "Branch Mask Radius", Float ) = 0.5
		[Header((Roots Mask))] _RootsRadius( "Roots Radius", Float ) = 2
		_RootsHardness( "Roots Hardness", Float ) = 1
		_RootsPosition( "Roots Position", Float ) = 0
		[Space (10)][Toggle( _DEBUGVISUALIZEWINDMASK_ON )] _DEBUGVisualizeWindMask( "[DEBUG] Visualize Wind Mask", Float ) = 0
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

		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		[ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

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

		

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

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

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON


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
				float4 ase_color : COLOR;
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
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			float _Wetness;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _SpecularMap;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			
			float4 SampleGradient( Gradient gradient, float time )
			{
				float3 color = gradient.colors[0].rgb;
				UNITY_UNROLL
				for (int c = 1; c < 8; c++)
				{
				float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, gradient.colorsLength-1));
				color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
				}
				#ifndef UNITY_COLORSPACE_GAMMA
				color = SRGBToLinear(color);
				#endif
				float alpha = gradient.alphas[0].x;
				UNITY_UNROLL
				for (int a = 1; a < 8; a++)
				{
				float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, gradient.alphasLength-1));
				alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
				}
				return float4(color, alpha);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				output.ase_color = input.ase_color;
				output.ase_normal = input.normalOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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
				float4 ase_color : COLOR;

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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float CustomDRAWERS1461_g1531 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _SHADINGSETTINGS );
				float2 uv_AlbedoMap1564_g1531 = input.ase_texcoord7.xy;
				float4 transform1416_g1531 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g1533 = dot( transform1416_g1531.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g1533 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g1533 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float2 appendResult1549_g1531 = (float2(ase_objectPosition.x , ase_objectPosition.z));
				float TextureBasedRandomContrast1437_g1531 = (( 1.0 - tex2D( _NoiseMapGrayscale, ( _TBRCWoldScale * appendResult1549_g1531 ) ) )*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_1 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast1437_g1531 ):( lerpResult10_g1533 ))).xxxx;
				float2 uv_AlbedoMap1415_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1415_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1415_g1531 );
				float2 uv_NoiseMapGrayscale1320_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1320_g1531 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale1320_g1531 );
				float4 transform1449_g1531 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break1445_g1531 = transform1449_g1531;
				float RandomColorFix1451_g1531 = floor( ( ( break1445_g1531.x + break1445_g1531.z ) * _RandomColorScale ) );
				float2 temp_cast_2 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1532 = dot( temp_cast_2 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1532 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1532 ) * 43758.55 ) ));
				float temp_output_1399_0_g1531 = saturate( lerpResult10_g1532 );
				float3 normalizeResult1401_g1531 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float DryLeafPositionMask1405_g1531 = ( (distance( normalizeResult1401_g1531 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_1315_0_g1531 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange1463_g1531 = _SeasonChangeGlobal;
				float4 lerpResult1412_g1531 = lerp( ( _DryLeafColor * ( tex2DNode1415_g1531.g * 2 ) ) , tex2DNode1415_g1531 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_1315_0_g1531 * 0.9 ) + ( temp_output_1315_0_g1531 * DryLeafPositionMask1405_g1531 * tex2DNode1320_g1531.r ) + temp_output_1399_0_g1531 ) ):( ( tex2DNode1320_g1531.r * temp_output_1399_0_g1531 * DryLeafPositionMask1405_g1531 ) )) - GlobalVar_SeasonChange1463_g1531 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output1409_g1531 = lerpResult1412_g1531;
				Gradient gradient1425_g1531 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_3 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1534 = dot( temp_cast_3 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1534 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1534 ) * 43758.55 ) ));
				float4 TextureBasedColorVariation1435_g1531 = (tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy )*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult1421_g1531 = lerp( SeasonControl_Output1409_g1531 , ( ( SeasonControl_Output1409_g1531 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient1425_g1531, TextureBasedColorVariation1435_g1531.r ) ):( SampleGradient( gradient1425_g1531, saturate( lerpResult10_g1534 ) ) )) * SeasonControl_Output1409_g1531 ) ) , _ColorVariation);
				float4 blendOpSrc1419_g1531 = temp_cast_1;
				float4 blendOpDest1419_g1531 = lerpResult1421_g1531;
				float4 lerpBlendMode1419_g1531 = lerp(blendOpDest1419_g1531,(( blendOpDest1419_g1531 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest1419_g1531 ) * ( 1.0 - blendOpSrc1419_g1531 ) ) : ( 2.0 * blendOpDest1419_g1531 * blendOpSrc1419_g1531 ) ),_RandomContrast);
				float2 uv_MaskMapRGBA1561_g1531 = input.ase_texcoord7.xy;
				float4 lerpResult1563_g1531 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap1564_g1531 ) , ( saturate( lerpBlendMode1419_g1531 )) , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA1561_g1531 ).r ):( 1.0 )));
				float4 LeafColorVariationSeasons_Output1439_g1531 = lerpResult1563_g1531;
				float saferPower1482_g1531 = abs( input.ase_color.r );
				float3 temp_output_1471_0_g1531 = ( ( input.ase_texcoord8.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult1472_g1531 = dot( temp_output_1471_0_g1531 , temp_output_1471_0_g1531 );
				float saferPower1475_g1531 = abs( saturate( dotResult1472_g1531 ) );
				float3 normalizeResult1486_g1531 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float SelfShading1492_g1531 = saturate( ( saturate( pow( saferPower1482_g1531 , _VertexAo ) ) * (( pow( saferPower1475_g1531 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult1486_g1531 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 GrassColorVariation_Output1520_g1531 = (( _SelfShadingVertexColor )?( ( LeafColorVariationSeasons_Output1439_g1531 * (SelfShading1492_g1531*_VertexLighting + _VertexShadow) ) ):( LeafColorVariationSeasons_Output1439_g1531 ));
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount1464_g1531 = _SnowAmount;
				float2 uv_AlbedoMap1496_g1531 = input.ase_texcoord7.xy;
				float2 uv_NormalMap1517_g1531 = input.ase_texcoord7.xy;
				float4 lerpResult1516_g1531 = lerp( ( ( CustomDRAWERS1461_g1531 + _AlbedoColor ) * GrassColorVariation_Output1520_g1531 ) , temp_cast_6 , saturate( ( saturate( PositionWS.y ) * GlobalVar_SnowAmount1464_g1531 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap1517_g1531 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap1496_g1531 ).g*_SnowScale + _SnowOffset) )) * (( _SnowMaskVertexR )?( input.ase_color.r ):( 1.0 )) * input.ase_color.g ) ));
				float4 Snow_Output1497_g1531 = lerpResult1516_g1531;
				float3 normalizeResult1390_g1531 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform1368_g1531 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord8.xyz , 0.0 ));
				float dotResult1376_g1531 = dot( float4( ViewDirWS , 0.0 ) , -( float4( normalizeResult1390_g1531 , 0.0 ) + ( (( _TranslucencyTreeTangents )?( float4( input.ase_normal , 0.0 ) ):( transform1368_g1531 )) * _TranslucencyRange ) ) );
				float2 uv_MaskMapRGBA1377_g1531 = input.ase_texcoord7.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency1381_g1531 = ( saturate( dotResult1376_g1531 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA1377_g1531 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity1465_g1531 = _TranslucencyPower;
				float4 AlbedoFinal1335_g1531 = ( Snow_Output1497_g1531 *  (1.0 + ( TobyTranslucency1381_g1531 - 0.0 ) * ( TranslucencyIntensity1465_g1531 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float Wetness1585_g1531 = _Wetness;
				float4 lerpResult1575_g1531 = lerp( AlbedoFinal1335_g1531 , ( AlbedoFinal1335_g1531 * 0.65 ) , Wetness1585_g1531);
				float4 Albedo_Output1579_g1531 = lerpResult1575_g1531;
				float4 color2192_g1530 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_2047_0_g1530 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord8.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord8).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float temp_output_2188_0_g1530 = ( SphericalMaskProxySphere2097_g1530 * BRANCH_MASK2126_g1530 );
				float4 color2182_g1530 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2181_g1530 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.ase_texcoord8.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float4 lerpResult2184_g1530 = lerp( color2192_g1530 , ( ( temp_output_2188_0_g1530 * color2182_g1530 ) + ( color2181_g1530 * saturate( ( 1.0 - temp_output_2188_0_g1530 ) ) ) ) , RootsMask_ProxySphere2062_g1530);
				float4 DEBUGVisualizeWindMask2187_g1530 = lerpResult2184_g1530;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch813 = DEBUGVisualizeWindMask2187_g1530;
				#else
				float4 staticSwitch813 = Albedo_Output1579_g1531;
				#endif
				
				float2 temp_cast_12 = (_SecondaryNormalScale).xx;
				float2 texCoord1349_g1531 = input.ase_texcoord7.xy * temp_cast_12 + float2( 0,0 );
				float3 unpack1347_g1531 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord1349_g1531 ), _SecondaryNormalIntensity );
				unpack1347_g1531.z = lerp( 1, unpack1347_g1531.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap1345_g1531 = input.ase_texcoord7.xy;
				float3 unpack1345_g1531 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap1345_g1531 ), _NormalIntenisty );
				unpack1345_g1531.z = lerp( 1, unpack1345_g1531.z, saturate(_NormalIntenisty) );
				float3 Normal_Output1344_g1531 = BlendNormal( unpack1347_g1531 , unpack1345_g1531 );
				
				float2 uv_SpecularMap1327_g1531 = input.ase_texcoord7.xy;
				float3 tanToWorld0 = float3( TangentWS.x, BitangentWS.x, NormalWS.x );
				float3 tanToWorld1 = float3( TangentWS.y, BitangentWS.y, NormalWS.y );
				float3 tanToWorld2 = float3( TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tanNormal1444_g1531 = TangentWS;
				float fresnelNdotV1444_g1531 = dot( float3( dot( tanToWorld0, tanNormal1444_g1531 ), dot( tanToWorld1, tanNormal1444_g1531 ), dot( tanToWorld2, tanNormal1444_g1531 ) ), SafeNormalize( _MainLightPosition.xyz ) );
				float fresnelNode1444_g1531 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV1444_g1531 , 0.0001 ), _SpecularStrength ) );
				float SpecularRecalculate1523_g1531 = saturate( fresnelNode1444_g1531 );
				float Specular_Output1326_g1531 = (( _SpecularBackfaceOcclusion )?( ( ( 0.2 * _SpecularPower ) * saturate( (tex2D( _SpecularMap, uv_SpecularMap1327_g1531 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecularRecalculate1523_g1531 ) ):( 0.04 ));
				float3 temp_cast_13 = (Specular_Output1326_g1531).xxx;
				
				float2 uv_MaskMapRGBA1354_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1354_g1531 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA1354_g1531 );
				float Smoothness_Output1352_g1531 = ( tex2DNode1354_g1531.a * _SmoothnessIntensity );
				
				float AoMapBase1353_g1531 = tex2DNode1354_g1531.g;
				float saferPower1362_g1531 = abs( AoMapBase1353_g1531 );
				float AmbientOcclusion_Output1365_g1531 = ( pow( saferPower1362_g1531 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency1381_g1531 ) * TranslucencyIntensity1465_g1531 ) + 1.5 ) ) );
				
				float3 temp_cast_14 = (( _TTFETREEBILLBOARDSHADER + _FACERENDERING + _ADVANCEDSETTINGS )).xxx;
				
				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord7.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float3 BaseColor = staticSwitch813.rgb;
				float3 Normal = Normal_Output1344_g1531;
				float3 Specular = temp_cast_13;
				float Metallic = 0;
				float Smoothness = Smoothness_Output1352_g1531;
				float Occlusion = AmbientOcclusion_Output1365_g1531;
				float3 Emission = temp_cast_14;
				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;
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
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;


			float3 _LightDirection;
			float3 _LightPosition;

			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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

				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord1.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;
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
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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

				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord1.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;

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
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003

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

			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
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
				float4 ase_color : COLOR;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			float _Wetness;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			
			float4 SampleGradient( Gradient gradient, float time )
			{
				float3 color = gradient.colors[0].rgb;
				UNITY_UNROLL
				for (int c = 1; c < 8; c++)
				{
				float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, gradient.colorsLength-1));
				color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
				}
				#ifndef UNITY_COLORSPACE_GAMMA
				color = SRGBToLinear(color);
				#endif
				float alpha = gradient.alphas[0].x;
				UNITY_UNROLL
				for (int a = 1; a < 8; a++)
				{
				float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, gradient.alphasLength-1));
				alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
				}
				return float4(color, alpha);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord3.xy = input.texcoord0.xy;
				output.ase_texcoord4 = input.positionOS;
				output.ase_color = input.ase_color;
				output.ase_normal = input.normalOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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
				float4 ase_color : COLOR;

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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float CustomDRAWERS1461_g1531 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _SHADINGSETTINGS );
				float2 uv_AlbedoMap1564_g1531 = input.ase_texcoord3.xy;
				float4 transform1416_g1531 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g1533 = dot( transform1416_g1531.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g1533 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g1533 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float2 appendResult1549_g1531 = (float2(ase_objectPosition.x , ase_objectPosition.z));
				float TextureBasedRandomContrast1437_g1531 = (( 1.0 - tex2D( _NoiseMapGrayscale, ( _TBRCWoldScale * appendResult1549_g1531 ) ) )*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_1 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast1437_g1531 ):( lerpResult10_g1533 ))).xxxx;
				float2 uv_AlbedoMap1415_g1531 = input.ase_texcoord3.xy;
				float4 tex2DNode1415_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1415_g1531 );
				float2 uv_NoiseMapGrayscale1320_g1531 = input.ase_texcoord3.xy;
				float4 tex2DNode1320_g1531 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale1320_g1531 );
				float4 transform1449_g1531 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break1445_g1531 = transform1449_g1531;
				float RandomColorFix1451_g1531 = floor( ( ( break1445_g1531.x + break1445_g1531.z ) * _RandomColorScale ) );
				float2 temp_cast_2 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1532 = dot( temp_cast_2 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1532 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1532 ) * 43758.55 ) ));
				float temp_output_1399_0_g1531 = saturate( lerpResult10_g1532 );
				float3 normalizeResult1401_g1531 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float DryLeafPositionMask1405_g1531 = ( (distance( normalizeResult1401_g1531 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_1315_0_g1531 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange1463_g1531 = _SeasonChangeGlobal;
				float4 lerpResult1412_g1531 = lerp( ( _DryLeafColor * ( tex2DNode1415_g1531.g * 2 ) ) , tex2DNode1415_g1531 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_1315_0_g1531 * 0.9 ) + ( temp_output_1315_0_g1531 * DryLeafPositionMask1405_g1531 * tex2DNode1320_g1531.r ) + temp_output_1399_0_g1531 ) ):( ( tex2DNode1320_g1531.r * temp_output_1399_0_g1531 * DryLeafPositionMask1405_g1531 ) )) - GlobalVar_SeasonChange1463_g1531 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output1409_g1531 = lerpResult1412_g1531;
				Gradient gradient1425_g1531 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_3 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1534 = dot( temp_cast_3 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1534 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1534 ) * 43758.55 ) ));
				float4 TextureBasedColorVariation1435_g1531 = (tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy )*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult1421_g1531 = lerp( SeasonControl_Output1409_g1531 , ( ( SeasonControl_Output1409_g1531 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient1425_g1531, TextureBasedColorVariation1435_g1531.r ) ):( SampleGradient( gradient1425_g1531, saturate( lerpResult10_g1534 ) ) )) * SeasonControl_Output1409_g1531 ) ) , _ColorVariation);
				float4 blendOpSrc1419_g1531 = temp_cast_1;
				float4 blendOpDest1419_g1531 = lerpResult1421_g1531;
				float4 lerpBlendMode1419_g1531 = lerp(blendOpDest1419_g1531,(( blendOpDest1419_g1531 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest1419_g1531 ) * ( 1.0 - blendOpSrc1419_g1531 ) ) : ( 2.0 * blendOpDest1419_g1531 * blendOpSrc1419_g1531 ) ),_RandomContrast);
				float2 uv_MaskMapRGBA1561_g1531 = input.ase_texcoord3.xy;
				float4 lerpResult1563_g1531 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap1564_g1531 ) , ( saturate( lerpBlendMode1419_g1531 )) , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA1561_g1531 ).r ):( 1.0 )));
				float4 LeafColorVariationSeasons_Output1439_g1531 = lerpResult1563_g1531;
				float saferPower1482_g1531 = abs( input.ase_color.r );
				float3 temp_output_1471_0_g1531 = ( ( input.ase_texcoord4.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult1472_g1531 = dot( temp_output_1471_0_g1531 , temp_output_1471_0_g1531 );
				float saferPower1475_g1531 = abs( saturate( dotResult1472_g1531 ) );
				float3 normalizeResult1486_g1531 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float SelfShading1492_g1531 = saturate( ( saturate( pow( saferPower1482_g1531 , _VertexAo ) ) * (( pow( saferPower1475_g1531 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult1486_g1531 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 GrassColorVariation_Output1520_g1531 = (( _SelfShadingVertexColor )?( ( LeafColorVariationSeasons_Output1439_g1531 * (SelfShading1492_g1531*_VertexLighting + _VertexShadow) ) ):( LeafColorVariationSeasons_Output1439_g1531 ));
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount1464_g1531 = _SnowAmount;
				float2 uv_AlbedoMap1496_g1531 = input.ase_texcoord3.xy;
				float2 uv_NormalMap1517_g1531 = input.ase_texcoord3.xy;
				float4 lerpResult1516_g1531 = lerp( ( ( CustomDRAWERS1461_g1531 + _AlbedoColor ) * GrassColorVariation_Output1520_g1531 ) , temp_cast_6 , saturate( ( saturate( PositionWS.y ) * GlobalVar_SnowAmount1464_g1531 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap1517_g1531 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap1496_g1531 ).g*_SnowScale + _SnowOffset) )) * (( _SnowMaskVertexR )?( input.ase_color.r ):( 1.0 )) * input.ase_color.g ) ));
				float4 Snow_Output1497_g1531 = lerpResult1516_g1531;
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - PositionWS );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float3 normalizeResult1390_g1531 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform1368_g1531 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord4.xyz , 0.0 ));
				float dotResult1376_g1531 = dot( float4( ase_viewDirWS , 0.0 ) , -( float4( normalizeResult1390_g1531 , 0.0 ) + ( (( _TranslucencyTreeTangents )?( float4( input.ase_normal , 0.0 ) ):( transform1368_g1531 )) * _TranslucencyRange ) ) );
				float2 uv_MaskMapRGBA1377_g1531 = input.ase_texcoord3.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency1381_g1531 = ( saturate( dotResult1376_g1531 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA1377_g1531 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity1465_g1531 = _TranslucencyPower;
				float4 AlbedoFinal1335_g1531 = ( Snow_Output1497_g1531 *  (1.0 + ( TobyTranslucency1381_g1531 - 0.0 ) * ( TranslucencyIntensity1465_g1531 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float Wetness1585_g1531 = _Wetness;
				float4 lerpResult1575_g1531 = lerp( AlbedoFinal1335_g1531 , ( AlbedoFinal1335_g1531 * 0.65 ) , Wetness1585_g1531);
				float4 Albedo_Output1579_g1531 = lerpResult1575_g1531;
				float4 color2192_g1530 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_2047_0_g1530 = ( ( input.ase_texcoord4.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord4.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord4).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float temp_output_2188_0_g1530 = ( SphericalMaskProxySphere2097_g1530 * BRANCH_MASK2126_g1530 );
				float4 color2182_g1530 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2181_g1530 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.ase_texcoord4.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float4 lerpResult2184_g1530 = lerp( color2192_g1530 , ( ( temp_output_2188_0_g1530 * color2182_g1530 ) + ( color2181_g1530 * saturate( ( 1.0 - temp_output_2188_0_g1530 ) ) ) ) , RootsMask_ProxySphere2062_g1530);
				float4 DEBUGVisualizeWindMask2187_g1530 = lerpResult2184_g1530;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch813 = DEBUGVisualizeWindMask2187_g1530;
				#else
				float4 staticSwitch813 = Albedo_Output1579_g1531;
				#endif
				
				float3 temp_cast_12 = (( _TTFETREEBILLBOARDSHADER + _FACERENDERING + _ADVANCEDSETTINGS )).xxx;
				
				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord3.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float3 BaseColor = staticSwitch813.rgb;
				float3 Emission = temp_cast_12;
				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;

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
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			float _Wetness;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			
			float4 SampleGradient( Gradient gradient, float time )
			{
				float3 color = gradient.colors[0].rgb;
				UNITY_UNROLL
				for (int c = 1; c < 8; c++)
				{
				float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, gradient.colorsLength-1));
				color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
				}
				#ifndef UNITY_COLORSPACE_GAMMA
				color = SRGBToLinear(color);
				#endif
				float alpha = gradient.alphas[0].x;
				UNITY_UNROLL
				for (int a = 1; a < 8; a++)
				{
				float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, gradient.alphasLength-1));
				alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
				}
				return float4(color, alpha);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				output.ase_texcoord2 = input.positionOS;
				output.ase_color = input.ase_color;
				output.ase_normal = input.normalOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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
				float4 ase_color : COLOR;

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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float CustomDRAWERS1461_g1531 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _SHADINGSETTINGS );
				float2 uv_AlbedoMap1564_g1531 = input.ase_texcoord1.xy;
				float4 transform1416_g1531 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g1533 = dot( transform1416_g1531.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g1533 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g1533 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float2 appendResult1549_g1531 = (float2(ase_objectPosition.x , ase_objectPosition.z));
				float TextureBasedRandomContrast1437_g1531 = (( 1.0 - tex2D( _NoiseMapGrayscale, ( _TBRCWoldScale * appendResult1549_g1531 ) ) )*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_1 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast1437_g1531 ):( lerpResult10_g1533 ))).xxxx;
				float2 uv_AlbedoMap1415_g1531 = input.ase_texcoord1.xy;
				float4 tex2DNode1415_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1415_g1531 );
				float2 uv_NoiseMapGrayscale1320_g1531 = input.ase_texcoord1.xy;
				float4 tex2DNode1320_g1531 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale1320_g1531 );
				float4 transform1449_g1531 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break1445_g1531 = transform1449_g1531;
				float RandomColorFix1451_g1531 = floor( ( ( break1445_g1531.x + break1445_g1531.z ) * _RandomColorScale ) );
				float2 temp_cast_2 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1532 = dot( temp_cast_2 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1532 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1532 ) * 43758.55 ) ));
				float temp_output_1399_0_g1531 = saturate( lerpResult10_g1532 );
				float3 normalizeResult1401_g1531 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float DryLeafPositionMask1405_g1531 = ( (distance( normalizeResult1401_g1531 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_1315_0_g1531 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange1463_g1531 = _SeasonChangeGlobal;
				float4 lerpResult1412_g1531 = lerp( ( _DryLeafColor * ( tex2DNode1415_g1531.g * 2 ) ) , tex2DNode1415_g1531 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_1315_0_g1531 * 0.9 ) + ( temp_output_1315_0_g1531 * DryLeafPositionMask1405_g1531 * tex2DNode1320_g1531.r ) + temp_output_1399_0_g1531 ) ):( ( tex2DNode1320_g1531.r * temp_output_1399_0_g1531 * DryLeafPositionMask1405_g1531 ) )) - GlobalVar_SeasonChange1463_g1531 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output1409_g1531 = lerpResult1412_g1531;
				Gradient gradient1425_g1531 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_3 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1534 = dot( temp_cast_3 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1534 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1534 ) * 43758.55 ) ));
				float4 TextureBasedColorVariation1435_g1531 = (tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy )*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult1421_g1531 = lerp( SeasonControl_Output1409_g1531 , ( ( SeasonControl_Output1409_g1531 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient1425_g1531, TextureBasedColorVariation1435_g1531.r ) ):( SampleGradient( gradient1425_g1531, saturate( lerpResult10_g1534 ) ) )) * SeasonControl_Output1409_g1531 ) ) , _ColorVariation);
				float4 blendOpSrc1419_g1531 = temp_cast_1;
				float4 blendOpDest1419_g1531 = lerpResult1421_g1531;
				float4 lerpBlendMode1419_g1531 = lerp(blendOpDest1419_g1531,(( blendOpDest1419_g1531 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest1419_g1531 ) * ( 1.0 - blendOpSrc1419_g1531 ) ) : ( 2.0 * blendOpDest1419_g1531 * blendOpSrc1419_g1531 ) ),_RandomContrast);
				float2 uv_MaskMapRGBA1561_g1531 = input.ase_texcoord1.xy;
				float4 lerpResult1563_g1531 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap1564_g1531 ) , ( saturate( lerpBlendMode1419_g1531 )) , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA1561_g1531 ).r ):( 1.0 )));
				float4 LeafColorVariationSeasons_Output1439_g1531 = lerpResult1563_g1531;
				float saferPower1482_g1531 = abs( input.ase_color.r );
				float3 temp_output_1471_0_g1531 = ( ( input.ase_texcoord2.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult1472_g1531 = dot( temp_output_1471_0_g1531 , temp_output_1471_0_g1531 );
				float saferPower1475_g1531 = abs( saturate( dotResult1472_g1531 ) );
				float3 normalizeResult1486_g1531 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float SelfShading1492_g1531 = saturate( ( saturate( pow( saferPower1482_g1531 , _VertexAo ) ) * (( pow( saferPower1475_g1531 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult1486_g1531 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 GrassColorVariation_Output1520_g1531 = (( _SelfShadingVertexColor )?( ( LeafColorVariationSeasons_Output1439_g1531 * (SelfShading1492_g1531*_VertexLighting + _VertexShadow) ) ):( LeafColorVariationSeasons_Output1439_g1531 ));
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount1464_g1531 = _SnowAmount;
				float2 uv_AlbedoMap1496_g1531 = input.ase_texcoord1.xy;
				float2 uv_NormalMap1517_g1531 = input.ase_texcoord1.xy;
				float4 lerpResult1516_g1531 = lerp( ( ( CustomDRAWERS1461_g1531 + _AlbedoColor ) * GrassColorVariation_Output1520_g1531 ) , temp_cast_6 , saturate( ( saturate( PositionWS.y ) * GlobalVar_SnowAmount1464_g1531 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap1517_g1531 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap1496_g1531 ).g*_SnowScale + _SnowOffset) )) * (( _SnowMaskVertexR )?( input.ase_color.r ):( 1.0 )) * input.ase_color.g ) ));
				float4 Snow_Output1497_g1531 = lerpResult1516_g1531;
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - PositionWS );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float3 normalizeResult1390_g1531 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform1368_g1531 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord2.xyz , 0.0 ));
				float dotResult1376_g1531 = dot( float4( ase_viewDirWS , 0.0 ) , -( float4( normalizeResult1390_g1531 , 0.0 ) + ( (( _TranslucencyTreeTangents )?( float4( input.ase_normal , 0.0 ) ):( transform1368_g1531 )) * _TranslucencyRange ) ) );
				float2 uv_MaskMapRGBA1377_g1531 = input.ase_texcoord1.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency1381_g1531 = ( saturate( dotResult1376_g1531 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA1377_g1531 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity1465_g1531 = _TranslucencyPower;
				float4 AlbedoFinal1335_g1531 = ( Snow_Output1497_g1531 *  (1.0 + ( TobyTranslucency1381_g1531 - 0.0 ) * ( TranslucencyIntensity1465_g1531 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float Wetness1585_g1531 = _Wetness;
				float4 lerpResult1575_g1531 = lerp( AlbedoFinal1335_g1531 , ( AlbedoFinal1335_g1531 * 0.65 ) , Wetness1585_g1531);
				float4 Albedo_Output1579_g1531 = lerpResult1575_g1531;
				float4 color2192_g1530 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_2047_0_g1530 = ( ( input.ase_texcoord2.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord2.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord2).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float temp_output_2188_0_g1530 = ( SphericalMaskProxySphere2097_g1530 * BRANCH_MASK2126_g1530 );
				float4 color2182_g1530 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2181_g1530 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.ase_texcoord2.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float4 lerpResult2184_g1530 = lerp( color2192_g1530 , ( ( temp_output_2188_0_g1530 * color2182_g1530 ) + ( color2181_g1530 * saturate( ( 1.0 - temp_output_2188_0_g1530 ) ) ) ) , RootsMask_ProxySphere2062_g1530);
				float4 DEBUGVisualizeWindMask2187_g1530 = lerpResult2184_g1530;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch813 = DEBUGVisualizeWindMask2187_g1530;
				#else
				float4 staticSwitch813 = Albedo_Output1579_g1531;
				#endif
				
				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord1.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float3 BaseColor = staticSwitch813.rgb;
				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;

				half4 color = half4(BaseColor, Alpha );

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				return color;
			}
			ENDHLSL
		}

		
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
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _NormalMap;
			sampler2D _AlbedoMap;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord3.xy = input.texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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

				float2 temp_cast_0 = (_SecondaryNormalScale).xx;
				float2 texCoord1349_g1531 = input.ase_texcoord3.xy * temp_cast_0 + float2( 0,0 );
				float3 unpack1347_g1531 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord1349_g1531 ), _SecondaryNormalIntensity );
				unpack1347_g1531.z = lerp( 1, unpack1347_g1531.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap1345_g1531 = input.ase_texcoord3.xy;
				float3 unpack1345_g1531 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap1345_g1531 ), _NormalIntenisty );
				unpack1345_g1531.z = lerp( 1, unpack1345_g1531.z, saturate(_NormalIntenisty) );
				float3 Normal_Output1344_g1531 = BlendNormal( unpack1347_g1531 , unpack1345_g1531 );
				
				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord3.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float3 Normal = Normal_Output1344_g1531;
				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;

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
			#pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON


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
				float4 ase_color : COLOR;
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
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			float _Wetness;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _SpecularMap;


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			
			float4 SampleGradient( Gradient gradient, float time )
			{
				float3 color = gradient.colors[0].rgb;
				UNITY_UNROLL
				for (int c = 1; c < 8; c++)
				{
				float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, gradient.colorsLength-1));
				color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
				}
				#ifndef UNITY_COLORSPACE_GAMMA
				color = SRGBToLinear(color);
				#endif
				float alpha = gradient.alphas[0].x;
				UNITY_UNROLL
				for (int a = 1; a < 8; a++)
				{
				float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, gradient.alphasLength-1));
				alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
				}
				return float4(color, alpha);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				output.ase_color = input.ase_color;
				output.ase_normal = input.normalOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;
				input.tangentOS = input.tangentOS;

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
				float4 ase_color : COLOR;

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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float CustomDRAWERS1461_g1531 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SEASONSETTINGS + _SHADINGSETTINGS );
				float2 uv_AlbedoMap1564_g1531 = input.ase_texcoord7.xy;
				float4 transform1416_g1531 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g1533 = dot( transform1416_g1531.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g1533 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g1533 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float2 appendResult1549_g1531 = (float2(ase_objectPosition.x , ase_objectPosition.z));
				float TextureBasedRandomContrast1437_g1531 = (( 1.0 - tex2D( _NoiseMapGrayscale, ( _TBRCWoldScale * appendResult1549_g1531 ) ) )*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_1 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast1437_g1531 ):( lerpResult10_g1533 ))).xxxx;
				float2 uv_AlbedoMap1415_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1415_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1415_g1531 );
				float2 uv_NoiseMapGrayscale1320_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1320_g1531 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale1320_g1531 );
				float4 transform1449_g1531 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break1445_g1531 = transform1449_g1531;
				float RandomColorFix1451_g1531 = floor( ( ( break1445_g1531.x + break1445_g1531.z ) * _RandomColorScale ) );
				float2 temp_cast_2 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1532 = dot( temp_cast_2 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1532 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1532 ) * 43758.55 ) ));
				float temp_output_1399_0_g1531 = saturate( lerpResult10_g1532 );
				float3 normalizeResult1401_g1531 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float DryLeafPositionMask1405_g1531 = ( (distance( normalizeResult1401_g1531 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_1315_0_g1531 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange1463_g1531 = _SeasonChangeGlobal;
				float4 lerpResult1412_g1531 = lerp( ( _DryLeafColor * ( tex2DNode1415_g1531.g * 2 ) ) , tex2DNode1415_g1531 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_1315_0_g1531 * 0.9 ) + ( temp_output_1315_0_g1531 * DryLeafPositionMask1405_g1531 * tex2DNode1320_g1531.r ) + temp_output_1399_0_g1531 ) ):( ( tex2DNode1320_g1531.r * temp_output_1399_0_g1531 * DryLeafPositionMask1405_g1531 ) )) - GlobalVar_SeasonChange1463_g1531 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output1409_g1531 = lerpResult1412_g1531;
				Gradient gradient1425_g1531 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_3 = (RandomColorFix1451_g1531).xx;
				float dotResult4_g1534 = dot( temp_cast_3 , float2( 12.9898,78.233 ) );
				float lerpResult10_g1534 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g1534 ) * 43758.55 ) ));
				float4 TextureBasedColorVariation1435_g1531 = (tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy )*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult1421_g1531 = lerp( SeasonControl_Output1409_g1531 , ( ( SeasonControl_Output1409_g1531 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient1425_g1531, TextureBasedColorVariation1435_g1531.r ) ):( SampleGradient( gradient1425_g1531, saturate( lerpResult10_g1534 ) ) )) * SeasonControl_Output1409_g1531 ) ) , _ColorVariation);
				float4 blendOpSrc1419_g1531 = temp_cast_1;
				float4 blendOpDest1419_g1531 = lerpResult1421_g1531;
				float4 lerpBlendMode1419_g1531 = lerp(blendOpDest1419_g1531,(( blendOpDest1419_g1531 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest1419_g1531 ) * ( 1.0 - blendOpSrc1419_g1531 ) ) : ( 2.0 * blendOpDest1419_g1531 * blendOpSrc1419_g1531 ) ),_RandomContrast);
				float2 uv_MaskMapRGBA1561_g1531 = input.ase_texcoord7.xy;
				float4 lerpResult1563_g1531 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap1564_g1531 ) , ( saturate( lerpBlendMode1419_g1531 )) , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA1561_g1531 ).r ):( 1.0 )));
				float4 LeafColorVariationSeasons_Output1439_g1531 = lerpResult1563_g1531;
				float saferPower1482_g1531 = abs( input.ase_color.r );
				float3 temp_output_1471_0_g1531 = ( ( input.ase_texcoord8.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult1472_g1531 = dot( temp_output_1471_0_g1531 , temp_output_1471_0_g1531 );
				float saferPower1475_g1531 = abs( saturate( dotResult1472_g1531 ) );
				float3 normalizeResult1486_g1531 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float SelfShading1492_g1531 = saturate( ( saturate( pow( saferPower1482_g1531 , _VertexAo ) ) * (( pow( saferPower1475_g1531 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult1486_g1531 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 GrassColorVariation_Output1520_g1531 = (( _SelfShadingVertexColor )?( ( LeafColorVariationSeasons_Output1439_g1531 * (SelfShading1492_g1531*_VertexLighting + _VertexShadow) ) ):( LeafColorVariationSeasons_Output1439_g1531 ));
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount1464_g1531 = _SnowAmount;
				float2 uv_AlbedoMap1496_g1531 = input.ase_texcoord7.xy;
				float2 uv_NormalMap1517_g1531 = input.ase_texcoord7.xy;
				float4 lerpResult1516_g1531 = lerp( ( ( CustomDRAWERS1461_g1531 + _AlbedoColor ) * GrassColorVariation_Output1520_g1531 ) , temp_cast_6 , saturate( ( saturate( PositionWS.y ) * GlobalVar_SnowAmount1464_g1531 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap1517_g1531 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap1496_g1531 ).g*_SnowScale + _SnowOffset) )) * (( _SnowMaskVertexR )?( input.ase_color.r ):( 1.0 )) * input.ase_color.g ) ));
				float4 Snow_Output1497_g1531 = lerpResult1516_g1531;
				float3 normalizeResult1390_g1531 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform1368_g1531 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord8.xyz , 0.0 ));
				float dotResult1376_g1531 = dot( float4( ViewDirWS , 0.0 ) , -( float4( normalizeResult1390_g1531 , 0.0 ) + ( (( _TranslucencyTreeTangents )?( float4( input.ase_normal , 0.0 ) ):( transform1368_g1531 )) * _TranslucencyRange ) ) );
				float2 uv_MaskMapRGBA1377_g1531 = input.ase_texcoord7.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency1381_g1531 = ( saturate( dotResult1376_g1531 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA1377_g1531 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity1465_g1531 = _TranslucencyPower;
				float4 AlbedoFinal1335_g1531 = ( Snow_Output1497_g1531 *  (1.0 + ( TobyTranslucency1381_g1531 - 0.0 ) * ( TranslucencyIntensity1465_g1531 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float Wetness1585_g1531 = _Wetness;
				float4 lerpResult1575_g1531 = lerp( AlbedoFinal1335_g1531 , ( AlbedoFinal1335_g1531 * 0.65 ) , Wetness1585_g1531);
				float4 Albedo_Output1579_g1531 = lerpResult1575_g1531;
				float4 color2192_g1530 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_2047_0_g1530 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord8.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord8).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float temp_output_2188_0_g1530 = ( SphericalMaskProxySphere2097_g1530 * BRANCH_MASK2126_g1530 );
				float4 color2182_g1530 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2181_g1530 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.ase_texcoord8.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float4 lerpResult2184_g1530 = lerp( color2192_g1530 , ( ( temp_output_2188_0_g1530 * color2182_g1530 ) + ( color2181_g1530 * saturate( ( 1.0 - temp_output_2188_0_g1530 ) ) ) ) , RootsMask_ProxySphere2062_g1530);
				float4 DEBUGVisualizeWindMask2187_g1530 = lerpResult2184_g1530;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch813 = DEBUGVisualizeWindMask2187_g1530;
				#else
				float4 staticSwitch813 = Albedo_Output1579_g1531;
				#endif
				
				float2 temp_cast_12 = (_SecondaryNormalScale).xx;
				float2 texCoord1349_g1531 = input.ase_texcoord7.xy * temp_cast_12 + float2( 0,0 );
				float3 unpack1347_g1531 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord1349_g1531 ), _SecondaryNormalIntensity );
				unpack1347_g1531.z = lerp( 1, unpack1347_g1531.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap1345_g1531 = input.ase_texcoord7.xy;
				float3 unpack1345_g1531 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap1345_g1531 ), _NormalIntenisty );
				unpack1345_g1531.z = lerp( 1, unpack1345_g1531.z, saturate(_NormalIntenisty) );
				float3 Normal_Output1344_g1531 = BlendNormal( unpack1347_g1531 , unpack1345_g1531 );
				
				float2 uv_SpecularMap1327_g1531 = input.ase_texcoord7.xy;
				float3 tanToWorld0 = float3( TangentWS.x, BitangentWS.x, NormalWS.x );
				float3 tanToWorld1 = float3( TangentWS.y, BitangentWS.y, NormalWS.y );
				float3 tanToWorld2 = float3( TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tanNormal1444_g1531 = TangentWS;
				float fresnelNdotV1444_g1531 = dot( float3( dot( tanToWorld0, tanNormal1444_g1531 ), dot( tanToWorld1, tanNormal1444_g1531 ), dot( tanToWorld2, tanNormal1444_g1531 ) ), SafeNormalize( _MainLightPosition.xyz ) );
				float fresnelNode1444_g1531 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV1444_g1531 , 0.0001 ), _SpecularStrength ) );
				float SpecularRecalculate1523_g1531 = saturate( fresnelNode1444_g1531 );
				float Specular_Output1326_g1531 = (( _SpecularBackfaceOcclusion )?( ( ( 0.2 * _SpecularPower ) * saturate( (tex2D( _SpecularMap, uv_SpecularMap1327_g1531 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecularRecalculate1523_g1531 ) ):( 0.04 ));
				float3 temp_cast_13 = (Specular_Output1326_g1531).xxx;
				
				float2 uv_MaskMapRGBA1354_g1531 = input.ase_texcoord7.xy;
				float4 tex2DNode1354_g1531 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA1354_g1531 );
				float Smoothness_Output1352_g1531 = ( tex2DNode1354_g1531.a * _SmoothnessIntensity );
				
				float AoMapBase1353_g1531 = tex2DNode1354_g1531.g;
				float saferPower1362_g1531 = abs( AoMapBase1353_g1531 );
				float AmbientOcclusion_Output1365_g1531 = ( pow( saferPower1362_g1531 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency1381_g1531 ) * TranslucencyIntensity1465_g1531 ) + 1.5 ) ) );
				
				float3 temp_cast_14 = (( _TTFETREEBILLBOARDSHADER + _FACERENDERING + _ADVANCEDSETTINGS )).xxx;
				
				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord7.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				float3 BaseColor = staticSwitch813.rgb;
				float3 Normal = Normal_Output1344_g1531;
				float3 Specular = temp_cast_13;
				float Metallic = 0;
				float Smoothness = Smoothness_Output1352_g1531;
				float Occlusion = AmbientOcclusion_Output1365_g1531;
				float3 Emission = temp_cast_14;
				float Alpha = Opacity_Output1334_g1531;
				float AlphaClipThreshold = _AlphaClip;
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
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
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

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;

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

				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord1.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				surfaceDescription.Alpha = Opacity_Output1334_g1531;
				surfaceDescription.AlphaClipThreshold = _AlphaClip;

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

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _AlbedoMap;


			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
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

				float GlobalVar_WindStrength2209_g1530 = _GlobalWindStrength;
				float mulTime1664_g1530 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1974_g1530 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1978_g1530 = dotResult1974_g1530;
				float dotResult1976_g1530 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1979_g1530 = dotResult1976_g1530;
				float mulTime1663_g1530 = _TimeParameters.x * 4.3;
				float dotResult1975_g1530 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1980_g1530 = dotResult1975_g1530;
				float3 normalizeResult2129_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP2139_g1530 = saturate( (distance( normalizeResult2129_g1530 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_2047_0_g1530 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult2048_g1530 = dot( temp_output_2047_0_g1530 , temp_output_2047_0_g1530 );
				float saferPower2096_g1530 = abs( saturate( dotResult2048_g1530 ) );
				float temp_output_2093_0_g1530 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower2096_g1530 , _Hardness ) );
				float3 normalizeResult2087_g1530 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass2091_g1530 = saturate( (distance( normalizeResult2087_g1530 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere2097_g1530 = (( _CenterofMass )?( ( temp_output_2093_0_g1530 * CenterOfMass2091_g1530 ) ):( temp_output_2093_0_g1530 ));
				float saferPower2122_g1530 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2124_g1530 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2122_g1530 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2051_g1530 = float3( 0, -1, 0 );
				float3 appendResult2054_g1530 = (float3(break2051_g1530.x , ( break2051_g1530.y * _RootsPosition ) , break2051_g1530.z));
				float3 temp_output_2058_0_g1530 = ( ( input.positionOS.xyz - appendResult2054_g1530 ) / _RootsRadius );
				float dotResult2059_g1530 = dot( temp_output_2058_0_g1530 , temp_output_2058_0_g1530 );
				float saferPower2084_g1530 = abs( saturate( dotResult2059_g1530 ) );
				float RootsMask_ProxySphere2062_g1530 = pow( saferPower2084_g1530 , _RootsHardness );
				float3 rotatedValue1836_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1664_g1530 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1978_g1530 ) + ( RandomSeedVector_D1979_g1530 * 0.02 ) ) ) + sin( ( mulTime1663_g1530 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1980_g1530 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP2139_g1530 * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ).x );
				float3 appendResult1754_g1530 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1741_g1530 = input.positionOS.xyz;
				float3 appendResult1743_g1530 = (float3(break1741_g1530.x , ( break1741_g1530.y * 0.15 ) , 0.0));
				float mulTime1747_g1530 = _TimeParameters.x * 2.1;
				float smoothstepResult2104_g1530 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BRANCH_MASK2126_g1530 = smoothstepResult2104_g1530;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult2166_g1530 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult2172_g1530 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult2166_g1530 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult2166_g1530 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask2175_g1530 = saturate( ( smoothstepResult2172_g1530 * 0.5 ) );
				float3 appendResult1769_g1530 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1758_g1530 = input.positionOS.xyz;
				float3 appendResult1760_g1530 = (float3(break1758_g1530.x , 0.0 , ( break1758_g1530.z * 0.15 )));
				float mulTime1764_g1530 = _TimeParameters.x * 2.3;
				float3 appendResult1808_g1530 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1798_g1530 = input.positionOS.xyz;
				float3 appendResult1801_g1530 = (float3(0.0 , ( break1798_g1530.y * 0.2 ) , ( break1798_g1530.z * 0.4 )));
				float mulTime1815_g1530 = _TimeParameters.x * 2.0;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2158_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2159_g1530 = _TimeParameters.x * 0.25;
				float simplePerlin2D2162_g1530 = snoise( ( normalizeResult2158_g1530 + mulTime2159_g1530 ).xy*0.43 );
				float WindMask_LargeB2164_g1530 = ( simplePerlin2D2162_g1530 * 1.5 );
				float mulTime1856_g1530 = _TimeParameters.x * 3.2;
				float dotResult2006_g1530 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J2012_g1530 = dotResult2006_g1530;
				float dotResult2005_g1530 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K2013_g1530 = dotResult2005_g1530;
				float3 temp_output_1873_0_g1530 = ( ( mulTime1856_g1530 + RandomSeedVector_J2012_g1530 + RandomSeedVector_K2013_g1530 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult2133_g1530 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2138_g1530 = saturate( dotResult2133_g1530 );
				float mulTime1853_g1530 = _TimeParameters.x * 2.3;
				float dotResult2007_g1530 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L2014_g1530 = dotResult2007_g1530;
				float dotResult2008_g1530 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M2011_g1530 = dotResult2008_g1530;
				float3 temp_output_1876_0_g1530 = ( ( mulTime1853_g1530 + RandomSeedVector_L2014_g1530 + RandomSeedVector_M2011_g1530 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1850_g1530 = _TimeParameters.x * 3.6;
				float dotResult2009_g1530 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N2015_g1530 = dotResult2009_g1530;
				float dotResult2010_g1530 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O2016_g1530 = dotResult2010_g1530;
				float temp_output_1879_0_g1530 = ( ( mulTime1850_g1530 + RandomSeedVector_N2015_g1530 + RandomSeedVector_O2016_g1530 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult2150_g1530 = ASESafeNormalize( ase_positionWS );
				float mulTime2151_g1530 = _TimeParameters.x * 0.26;
				float simplePerlin2D2154_g1530 = snoise( ( normalizeResult2150_g1530 + mulTime2151_g1530 ).xy*0.7 );
				float WindMask_LargeC2157_g1530 = ( simplePerlin2D2154_g1530 * 1.5 );
				float3 temp_output_1842_0_g1530 = ( ( ( ( rotatedValue1836_g1530 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( appendResult1754_g1530 + ( appendResult1743_g1530 * cos( mulTime1747_g1530 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1743_g1530 ) ) * sin( mulTime1747_g1530 ) ) ) * 0.1 ) * BRANCH_MASK2126_g1530 * RandomIDBranchPositionMask2175_g1530 ) + ( ( ( appendResult1769_g1530 + ( appendResult1760_g1530 * cos( mulTime1764_g1530 ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1760_g1530 ) ) * sin( mulTime1764_g1530 ) ) ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.1 ) + ( ( ( ( appendResult1808_g1530 + ( appendResult1801_g1530 * cos( mulTime1815_g1530 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1801_g1530 ) ) * sin( mulTime1815_g1530 ) ) ) * 0.1 ) * RandomIDBranchPositionMask2175_g1530 * BRANCH_MASK2126_g1530 ) * 0.05 ) ) * _BranchWindLarge * WindMask_LargeB2164_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) + ( ( ( ( cos( temp_output_1873_0_g1530 ) * sin( temp_output_1873_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( cos( temp_output_1876_0_g1530 ) * sin( temp_output_1876_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) + ( ( sin( temp_output_1879_0_g1530 ) * cos( temp_output_1879_0_g1530 ) * ( BRANCH_MASK2126_g1530 * CeneterOfMassThickness_Mask2138_g1530 ) ) * 0.2 ) ) * _BranchWindSmall * WindMask_LargeC2157_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 ) );
				float3 PIWI_02Gentle1844_g1530 = ( temp_output_1842_0_g1530 * 0.4 );
				float4 WindDirection2029_g1530 = _WindDirection;
				float4 normalizeResult2021_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 break2018_g1530 = (normalizeResult2021_g1530).xyz;
				float4 appendResult2026_g1530 = (float4(break2018_g1530.x , ( break2018_g1530.y + _DownwardStrength ) , break2018_g1530.z , 0.0));
				float4 WindMotion_BaseG2145_g1530 = ( appendResult2026_g1530 * saturate( input.positionOS.xyz.y ) );
				float2 appendResult2031_g1530 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out2032_g1530 = appendResult2031_g1530;
				float GlobalVar_WindSpeed2033_g1530 = _StrongWindSpeed;
				float2 NoiseRotation_Output2144_g1530 = ( -(WindDirection2029_g1530).xz * _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 );
				float2 WPRG2D_S42143_g1530 = ( BasicWorldPisitionXY_Out2032_g1530 + ( NoiseRotation_Output2144_g1530 * 4.0 ) );
				float simplePerlin2D2146_g1530 = snoise( WPRG2D_S42143_g1530*0.2 );
				simplePerlin2D2146_g1530 = simplePerlin2D2146_g1530*0.5 + 0.5;
				float Wind_Motion_Noise2148_g1530 = simplePerlin2D2146_g1530;
				float saferPower2121_g1530 = abs( saturate( ( _TrunkHeightThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult2117_g1530 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower2121_g1530 , 0.1 ) ));
				float TrunkHeightMask2125_g1530 = saturate( smoothstepResult2117_g1530 );
				float4 Motion_Bending_Gentle_Random2080_g1530 = ( WindMotion_BaseG2145_g1530 * _MotionBendingGentleRandom * Wind_Motion_Noise2148_g1530 * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float GlobalVar_WindMotion2034_g1530 = _WindMotion;
				float3 worldToObjDir2077_g1530 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2145_g1530 *  (0.0 + ( GlobalVar_WindMotion2034_g1530 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * Wind_Motion_Noise2148_g1530 ).xyz, 0.0 ) ).xyz;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 Motion_Bending_Gentle_Wind2083_g1530 = ( worldToObjDir2077_g1530 * ase_objectScale * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float dotResult1987_g1530 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A2002_g1530 = dotResult1987_g1530;
				float dotResult1988_g1530 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B2003_g1530 = dotResult1988_g1530;
				float3 appendResult1687_g1530 = (float3(( sin( ( RandomSeedVector_A2002_g1530 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B2003_g1530 ) ) * _PivotRandomnessStrength )));
				float dotResult1989_g1530 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C2004_g1530 = dotResult1989_g1530;
				float4 normalizeResult1734_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1911_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult1687_g1530 * ( sin( ( _TimeParameters.x + RandomSeedVector_C2004_g1530 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult1734_g1530 * input.positionOS.xyz.y * TrunkHeightMask2125_g1530 ).xyz, 0.0 ) ).xyz;
				float3 SIMPLE_SWAYING1915_g1530 = ( worldToObjDir1911_g1530 * ase_objectScale * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) );
				float dotResult1959_g1530 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1971_g1530 = dotResult1959_g1530;
				float mulTime1657_g1530 = _TimeParameters.x * 4.0;
				float dotResult1960_g1530 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1972_g1530 = dotResult1960_g1530;
				float mulTime1658_g1530 = _TimeParameters.x * 5.2;
				float dotResult1961_g1530 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1973_g1530 = dotResult1961_g1530;
				float3 rotatedValue1686_g1530 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1971_g1530 * 0.02 ) + mulTime1657_g1530 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1972_g1530 ) ) ) + sin( ( mulTime1658_g1530 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1973_g1530 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere2097_g1530 * MaskRoots2124_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 * saturate( input.positionOS.xyz.y ) ).x );
				float3 worldToObjDir1694_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue1686_g1530 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection2029_g1530 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_1697_0_g1530 = ( ( worldToObjDir1694_g1530 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle1929_g1530 = ( ( SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 ) * 0.2 );
				float dotResult1701_g1530 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult1710_g1530 = ASESafeNormalize( WindDirection2029_g1530 );
				float3 worldToObjDir1781_g1530 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1701_g1530 + _TimeParameters.x ) * GlobalVar_WindSpeed2033_g1530 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion2034_g1530 * 0.45 ) ) * SphericalMaskProxySphere2097_g1530 * normalizeResult1710_g1530 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 BARK_SWAY_STOP_CENTER1903_g1530 = ( worldToObjDir1781_g1530 * ase_objectScale * TrunkHeightMask2125_g1530 * MaskRoots2124_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 worldToObjDir1787_g1530 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed2033_g1530 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask2175_g1530 + RandomSeedVector_A2002_g1530 + RandomSeedVector_C2004_g1530 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion2034_g1530 * _BranchFoldStrength ) , 0.0 ) * WindDirection2029_g1530 ).xyz, 0.0 ) ).xyz;
				float3 BranchMotion1796_g1530 = ( worldToObjDir1787_g1530 * ase_objectScale * input.positionOS.xyz.y * BRANCH_MASK2126_g1530 * MaskRoots2124_g1530 * SphericalMaskProxySphere2097_g1530 * TrunkHeightMask2125_g1530 * RootsMask_ProxySphere2062_g1530 );
				float3 PIWI_011926_g1530 = ( BARK_SWAY_STOP_CENTER1903_g1530 + BranchMotion1796_g1530 + SIMPLE_SWAYING1915_g1530 + temp_output_1697_0_g1530 );
				float3 PIWI_021845_g1530 = temp_output_1842_0_g1530;
				float4 temp_cast_20 = (0.0).xxxx;
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1939_g1530 = ( float4( PIWI_02Gentle1844_g1530 , 0.0 ) + Motion_Bending_Gentle_Random2080_g1530 + float4( Motion_Bending_Gentle_Wind2083_g1530 , 0.0 ) + float4( PIWI_01Gentle1929_g1530 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1939_g1530 = temp_cast_20;
				#else
				float4 staticSwitch1939_g1530 = float4(  (float3( 0,0,0 ) + ( ( PIWI_011926_g1530 + PIWI_021845_g1530 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#endif
				float4 FinalWind_Output1950_g1530 = ( GlobalVar_WindStrength2209_g1530 * staticSwitch1939_g1530 );
				
				float3 LightDetect_Output1313_g1531 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = FinalWind_Output1950_g1530.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetect_Output1313_g1531;

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

				float2 uv_AlbedoMap1336_g1531 = input.ase_texcoord1.xy;
				float Opacity_Output1334_g1531 = tex2D( _AlbedoMap, uv_AlbedoMap1336_g1531 ).a;
				

				surfaceDescription.Alpha = Opacity_Output1334_g1531;
				surfaceDescription.AlphaClipThreshold = _AlphaClip;

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
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define _SPECULAR_SETUP 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170003


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

			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND _WINDTYPE_WINDOFF


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
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 positionCSNoJitter : TEXCOORD0;
				float4 previousPositionCSNoJitter : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoColor;
			float4 _DryLeafColor;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _SnowValue;
			float _VertexShadow;
			float _VertexLighting;
			float _VertexAo;
			float _CenterofMass;
			float _BranchMaskR;
			float _RandomContrast;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _TBCVMapIntenisty;
			float _ZaWorldoScale;
			float _SnowOffset;
			float _SnowMaskVertexR;
			float _TranslucencyTreeTangents;
			float _TranslucencyRange;
			float _FACERENDERING;
			float _TTFETREEBILLBOARDSHADER;
			float _AmbientOcclusionIntensity;
			float _SmoothnessIntensity;
			float _SpecularStrength;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _SpecularBackfaceOcclusion;
			float _NormalIntenisty;
			float _SecondaryNormalIntensity;
			float _SecondaryNormalScale;
			float _TranslucencyPower;
			float _TBCVOnUsesNoiseMap;
			float _DryLeavesOffset;
			float _DryLeavesScale;
			float _RandomColorScale;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchSwayPower;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _SeasonVertexColorR;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCWoldScale;
			float _TBRCOnUsesNoiseMap;
			float _SelfShadingVertexColor;
			float _ADVANCEDSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _TEXTUREMAPS;
			float _MobileShadingWorldUp;
			float _WINDMASKSETTINGS1;
			float _TEXTUREMAPS1;
			float _BranchFoldStrength;
			float _SEASONSETTINGS;
			float _AlphaClip;
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

			

			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				

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
	
	Fallback Off
}

/*ASEBEGIN
Version=19904
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;809;-416,-528;Inherit;False;548;403;;5;754;806;799;807;808;Drawers;0,0,0,1;0;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;834;-528,272;Inherit;False;(TTFE) Tree Billboard_Wind System;56;;1530;7781363c3f1900c46819cf845d29a41f;0;0;2;FLOAT4;2194;COLOR;2193
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;835;-528,16;Inherit;False;(TTFE) Tree Billboard_Shading;3;;1531;0f57c3e4aefb35640bedd1f6e47c6f57;0;0;7;COLOR;1209;FLOAT3;1208;FLOAT;1207;FLOAT;1206;FLOAT;1210;FLOAT;1211;FLOAT3;1205
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;807;-320,-240;Inherit;False;Property;_ADVANCEDSETTINGS;ADVANCED SETTINGS;80;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;754;-368,-480;Inherit;False;Property;_TTFETREEBILLBOARDSHADER;(TTFE) TREE BILLBOARD SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;806;-288,-400;Inherit;False;Property;_FACERENDERING;FACE RENDERING;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;808;-32,-400;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;813;-208,-64;Inherit;False;Property;_DEBUGVisualizeWindMask;[DEBUG] Visualize Wind Mask;79;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;831;-128,320;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;832;-64,272;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;820;-176,192;Inherit;False;Property;_AlphaClip;Alpha Clip;2;0;Create;True;0;0;0;False;1;Space (10);False;0.4;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;799;-288,-320;Inherit;False;Constant;_BackfaceCulling;Backface Culling;2;1;[Enum];Create;True;0;3;Off;0;Front;1;Back;2;0;True;1;Space (10);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;821;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;6;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;822;117.2345,-37.59569;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Billboard;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;51;Category;0;0;  Instanced Terrain Normals;1;0;Lighting Model;0;0;Workflow;0;638928646715322422;Surface;0;0;  Keep Alpha;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;1;0;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;2;0;Specular Highlights;2;0;Environment Reflections;2;0;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;  XR Motion Vectors;0;0;GPU Instancing;1;0;LOD CrossFade;0;638937905016276066;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position;1;0;Debug Display;1;0;Clear Coat;0;0;0;12;False;True;True;True;True;True;True;True;True;True;True;False;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;823;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;824;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;825;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;826;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;827;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;828;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;829;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;830;117.2345,-37.59569;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;836;117.2345,62.40431;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;837;117.2345,62.40431;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;XRMotionVectors;0;11;XRMotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;True;1;False;;255;False;;1;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;1;LightMode=XRMotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;808;0;754;0
WireConnection;808;1;806;0
WireConnection;808;2;807;0
WireConnection;813;1;835;1209
WireConnection;813;0;834;2193
WireConnection;831;0;835;1205
WireConnection;832;0;834;2194
WireConnection;822;0;813;0
WireConnection;822;1;835;1208
WireConnection;822;9;835;1207
WireConnection;822;4;835;1206
WireConnection;822;5;835;1210
WireConnection;822;2;808;0
WireConnection;822;6;835;1211
WireConnection;822;7;820;0
WireConnection;822;8;832;0
WireConnection;822;10;831;0
ASEEND*/
//CHKSM=79279E851B031106CEF89CCBF6A5960EDA003D37