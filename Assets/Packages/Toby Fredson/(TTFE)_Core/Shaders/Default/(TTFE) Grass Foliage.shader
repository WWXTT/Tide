// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage"
{
	Properties
	{
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[TTFE_DrawerTitle] _TTFEGRASSFOLIAGESHADER( "(TTFE) GRASS FOLIAGE SHADER", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _FACERENDERING( "FACE RENDERING", Float ) = 0
		[Space (10)] _AlphaClip( "Alpha Clip", Float ) = 0.4
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS( "TEXTURE MAPS", Float ) = 0
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (10)] _AlbedoMap( "Albedo Map", 2D ) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture] _NormalMap( "Normal Map", 2D ) = "bump" {}
		[Normal][TTFE_Drawer_SingleLineTexture] _SecondaryNormalMapCustomNormals( "Secondary Normal Map (Custom Normals)", 2D ) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _SpecularMapGrayscale( "Specular Map (Grayscale)", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _MaskMapRGBA( "Mask Map *RGB(A)", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _EmissionMap( "Emission Map", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _NoiseMapGrayscale( "Noise Map (Grayscale)", 2D ) = "white" {}
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTURESETTINGS( "TEXTURE SETTINGS", Float ) = 0
		[Header((Albedo))] _AlbedoColor( "Albedo Color", Color ) = ( 1, 1, 1, 0 )
		[Header((Normal))] _NormalIntensity( "Normal Intensity", Range( -3, 3 ) ) = 1
		[Toggle] _NormalBackFaceFixBranch( "Normal Back Face Fix (Branch)", Float ) = 0
		[Header((Secondary Normal))] _SecondaryNormalIntensity( "Secondary Normal Intensity", Float ) = 0
		_SecondaryNormalScale( "Secondary Normal Scale", Float ) = 0
		[Header((Smoothness))] _SmoothnessIntensity( "Smoothness Intensity", Range( -3, 3 ) ) = 1
		[Header((Ambient Occlusion))] _AmbientOcclusionIntensity( "Ambient Occlusion Intensity", Range( 0, 1 ) ) = 1
		[Header((Specular))] _SpecularPower( "Specular Power", Range( 0, 1 ) ) = 1
		_SpecularStrength( "Specular Strength", Float ) = 2
		_SpecularBias( "Specular Bias", Float ) = 1
		_SpecularScale( "Specular Scale", Float ) = -5
		_SpecularMapScale( "Specular Map Scale", Float ) = 1
		_SpecularMapOffset( "Specular Map Offset", Float ) = 0
		[Header((Translucency))] _TranslucencyPower( "Translucency Power", Range( 1, 30 ) ) = 3
		_TranslucencyRange( "Translucency Range", Float ) = 1
		[Toggle] _TranslucencyFluffiness( "Translucency Fluffiness", Float ) = 0
		[Header((Emission))] _EmissionIntensity( "Emission Intensity", Float ) = 0
		_EmissionColor( "Emission Color", Color ) = ( 1, 1, 1, 0 )
		[TTFE_DrawerFeatureBorder][Space (10)] _SHADINGSETTINGS( "SHADING SETTINGS", Float ) = 0
		[Space (10)][Toggle] _MobileShadingWorldUp( "Mobile Shading (World Up)", Float ) = 1
		[TTFE_DrawerFeatureBorder][Space (10)] _SEASONSETTINGS( "SEASON SETTINGS", Float ) = 0
		[Header((Color Variation))] _ColorVariation( "Color Variation", Range( 0, 1 ) ) = 0
		_RandomColorScale( "Random Color Scale", Float ) = 1
		[Header((Texture Based Color Variation))] _TBCVMapIntenisty( "TBCV Map Intenisty", Float ) = 1
		_TBCVMapOffset( "TBCV Map Offset", Float ) = 0
		_ZaWorldoScale( "TBCV Wold Scale", Float ) = 1
		[Toggle] _TBCVOnUsesNoiseMap( "TBCV On (Uses Noise Map)", Float ) = 0
		[Header((Random Contrast))] _RandomContrast( "Random Contrast", Range( 0, 1 ) ) = 0
		[Toggle( _RANDOMCONTRASTONOFF_ON )] _RandomContrastOnOff( "Random Contrast On/Off", Float ) = 0
		[Header((Texture Based Random Contrast))] _TBRCMapIntenisty( "TBRC Map Intenisty", Float ) = 1
		_TBRCMapOffset( "TBRC Map Offset", Float ) = 0
		[Toggle] _TBRCOnUsesNoiseMap( "TBRC On (Uses Noise Map)", Float ) = 0
		[Header((Season Control))] _DryLeafColor( "Dry Leaf Color", Color ) = ( 0.5568628, 0.3730685, 0.1764706, 0 )
		_DryLeavesScale( "Dry Leaves - Scale", Float ) = 1
		_DryLeavesOffset( "Dry Leaves - Offset", Float ) = 1
		[Toggle] _SeasonVertexColorR( "Season Vertex Color (R)", Float ) = 1
		[Toggle] _BranchMaskR( "Branch Mask *(R)", Float ) = 0
		[Header((Snow Control))] _SnowScale( "Snow Scale", Float ) = 0
		_SnowOffset( "Snow Offset", Float ) = 0
		[Toggle] _NormalMapBasedSnow( "Normal Map Based Snow", Float ) = 0
		[Toggle] _SnowAccumulationGroundSinking( "Snow Accumulation (Ground Sinking)", Float ) = 0
		_SinkingNoiseScale( "Accumulation Noise Scale", Float ) = 0.2
		_SnowValue( "Snow Value", Float ) = 1
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS( "WIND SETTINGS", Float ) = 0
		[Header((Wind Texture Maps))][NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)] _WindNoiseMap( "Wind Noise Map", 2D ) = "white" {}
		[HideInInspector] _HeightMax( "_HeightMax", Float ) = 1
		[Header((Pivot))] _GrassSwayPowerGentle( "Grass Sway Power (Gentle Wind)", Float ) = 1
		[HideInInspector] _BendDirection( "Bend Direction", Vector ) = ( 1, 0.5, 1, 0 )
		_MotionBendingRandomnessGentleWind( "Motion Bending Randomness (Gentle Wind)", Float ) = 0.1
		_MotionBendingDownwardStrength( "Motion Bending Downward Strength", Float ) = -0.5
		[Toggle] _PivotSway( "Pivot Sway", Float ) = 0
		_PivotSwayPower( "Pivot  Sway Power", Float ) = 1
		[HideInInspector] _BendAmountGrass( "Bend Amount Grass ", Float ) = 1
		[KeywordEnum( GentleBreeze,StrongWind )] _WindType( "Wind Type", Float ) = 0
		[Header((Noise))] _MotionFlutterIntensity( "Motion Flutter Intensity", Range( 0, 5 ) ) = 1
		_MotionFlutterScale( "Motion Flutter Scale", Float ) = 10
		_QuadScatterIntensity( "Quad Scatter Intensity", Range( 0, 5 ) ) = 0
		[Toggle] _QuadScatterOnlybasiccards( "Quad Scatter (Only basic cards)", Float ) = 0
		[Toggle] _QuadScatterWorldNormals( "Quad Scatter World Normals", Float ) = 0
		[Toggle] _WindMotionScaleMask( "Wind Motion Scale Mask", Float ) = 1
		[Toggle] _GrassInteraction( "Grass Interaction", Float ) = 1
		[Toggle( _PHYSISCSINTERACTION_ON )] _PhysiscsInteraction( "Physiscs Interaction", Float ) = 0
		[Toggle] _PhysicsWind( "Physics Wind", Float ) = 0
		[HideInInspector][Header((Interaction))] _BendRadius( "Bend Radius", Float ) = 1
		[Header((Perspective))] _PerspectiveIntensity( "Perspective Intensity", Float ) = -1
		[HideInInspector] _WindFrequency( "_WindFrequency", Float ) = 1
		[HideInInspector] _BendAmount( "_BendAmount", Float ) = 0
		[Toggle] _PerspectiveCorrection( "Perspective Correction", Float ) = 0
		[HideInInspector] _WindAmplitude( "_WindAmplitude", Float ) = 0
		[HideInInspector] _WindAxis( "_WindAxis", Vector ) = ( 0, 0, 1, 0 )
		[HideInInspector] _BendAxis( "_BendAxis", Vector ) = ( 0, 0, 1, 0 )
		[TTFE_DrawerFeatureBorder][Space (10)] _WINDMASKSETTINGS( "WIND MASK SETTINGS", Float ) = 0
		[Header((Wind Mask))] _Radius2( "Radius", Float ) = 1
		_Hardness2( "Hardness", Float ) = 1
		_CenterofMassintensity( "Center of Mass", Range( 0, 1 ) ) = 0
		[Toggle] _GroundBlendingExperimental( "Ground Blending (Experimental)", Float ) = 0
		_GroundBlendingOffset( "Ground Blending Offset", Float ) = -0.08
		[TTFE_DrawerFeatureBorder][Space (10)] _DEBUGWINDMASK( "DEBUG WIND MASK", Float ) = 0
		[Space (10)][Toggle( _DEBUGVISUALIZEWINDMASK_ON )] _DEBUGVisualizeWindMask( "[DEBUG] Visualize Wind Mask", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _ADVANCEDSETTINGS( "ADVANCED SETTINGS", Float ) = 0
		[Space (10)][Toggle( _SAVEPERFORMANCEDEACTIVATEWIND_ON )] _SAVEPERFORMANCEDeactivateWind( "[SAVE PERFORMANCE] Deactivate Wind", Float ) = 0
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

		Cull Off
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON
			#pragma shader_feature_local _RANDOMCONTRASTONOFF_ON


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			float _Wetness;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _SpecularMapGrayscale;
			sampler2D _EmissionMap;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = LightDetectBackface_Output156_g64201;
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
						, uint ase_vface : SV_IsFrontFace ) : SV_Target
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

				float Wetness594_g64201 = _Wetness;
				float fresnelNdotV597_g64201 = dot( normalize( TangentWS ), ViewDirWS );
				float fresnelNode597_g64201 = ( 1.0 + -5.0 * pow( max( 1.0 - fresnelNdotV597_g64201 , 0.0001 ), 0.8 ) );
				float CustomDRAWERS440_g64201 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SHADINGSETTINGS + _SEASONSETTINGS );
				float2 uv_AlbedoMap285_g64201 = input.ase_texcoord7.xy;
				float2 uv_AlbedoMap295_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode295_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap295_g64201 );
				float2 uv_NoiseMapGrayscale302_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode302_g64201 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale302_g64201 );
				float4 transform452_g64201 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break447_g64201 = transform452_g64201;
				float RandomColorFix451_g64201 = floor( ( ( break447_g64201.x + break447_g64201.z ) * _RandomColorScale ) );
				float2 temp_cast_0 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64202 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64202 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64202 ) * 43758.55 ) ));
				float temp_output_457_0_g64201 = saturate( lerpResult10_g64202 );
				float3 normalizeResult259_g64201 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float DryLeafPositionMask263_g64201 = ( (distance( normalizeResult259_g64201 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_301_0_g64201 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange537_g64201 = _SeasonChangeGlobal;
				float4 lerpResult294_g64201 = lerp( ( _DryLeafColor * ( tex2DNode295_g64201.g * 2 ) ) , tex2DNode295_g64201 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_301_0_g64201 * 0.9 ) + ( temp_output_301_0_g64201 * DryLeafPositionMask263_g64201 * tex2DNode302_g64201.r ) + temp_output_457_0_g64201 ) ):( ( tex2DNode302_g64201.r * temp_output_457_0_g64201 * DryLeafPositionMask263_g64201 ) )) - GlobalVar_SeasonChange537_g64201 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output311_g64201 = lerpResult294_g64201;
				Gradient gradient271_g64201 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_1 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64203 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64203 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64203 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 tex2DNode559_g64201 = tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy );
				float4 TextureBasedColorVariation573_g64201 = (tex2DNode559_g64201*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult282_g64201 = lerp( SeasonControl_Output311_g64201 , ( ( SeasonControl_Output311_g64201 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient271_g64201, TextureBasedColorVariation573_g64201.r ) ):( SampleGradient( gradient271_g64201, saturate( lerpResult10_g64203 ) ) )) * SeasonControl_Output311_g64201 ) ) , _ColorVariation);
				float4 transform279_g64201 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g64204 = dot( transform279_g64201.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g64204 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g64204 ) * 43758.55 ) ));
				float TextureBasedRandomContrast571_g64201 = (tex2DNode559_g64201*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_5 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast571_g64201 ):( lerpResult10_g64204 ))).xxxx;
				float4 blendOpSrc281_g64201 = temp_cast_5;
				float4 blendOpDest281_g64201 = lerpResult282_g64201;
				float4 lerpBlendMode281_g64201 = lerp(blendOpDest281_g64201,(( blendOpDest281_g64201 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest281_g64201 ) * ( 1.0 - blendOpSrc281_g64201 ) ) : ( 2.0 * blendOpDest281_g64201 * blendOpSrc281_g64201 ) ),_RandomContrast);
				#ifdef _RANDOMCONTRASTONOFF_ON
				float4 staticSwitch288_g64201 = ( saturate( lerpBlendMode281_g64201 ));
				#else
				float4 staticSwitch288_g64201 = lerpResult282_g64201;
				#endif
				float2 uv_MaskMapRGBA313_g64201 = input.ase_texcoord7.xy;
				float4 lerpResult283_g64201 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap285_g64201 ) , staticSwitch288_g64201 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA313_g64201 ).r ):( 1.0 )));
				float4 GrassColorVariation_Output109_g64201 = lerpResult283_g64201;
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float2 uv_AlbedoMap362_g64201 = input.ase_texcoord7.xy;
				float2 uv_NormalMap361_g64201 = input.ase_texcoord7.xy;
				float4 lerpResult351_g64201 = lerp( ( ( CustomDRAWERS440_g64201 + _AlbedoColor ) * GrassColorVariation_Output109_g64201 ) , temp_cast_6 , saturate( ( saturate( input.ase_texcoord8.xyz.y ) * GlobalVar_SnowAmount395_g64201 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap361_g64201 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap362_g64201 ).g*_SnowScale + _SnowOffset) )) ) ));
				float4 Snow_Output385_g64201 = lerpResult351_g64201;
				float3 normalizeResult492_g64201 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform39_g64201 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord8.xyz , 0.0 ));
				float3 normalizeResult493_g64201 = ASESafeNormalize( ViewDirWS );
				float dotResult52_g64201 = dot( -( float4( normalizeResult492_g64201 , 0.0 ) + ( (( _TranslucencyFluffiness )?( transform39_g64201 ):( float4( input.ase_texcoord8.xyz , 0.0 ) )) * _TranslucencyRange ) ) , float4( normalizeResult493_g64201 , 0.0 ) );
				float2 uv_MaskMapRGBA58_g64201 = input.ase_texcoord7.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency66_g64201 = ( saturate( dotResult52_g64201 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA58_g64201 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity72_g64201 = _TranslucencyPower;
				float4 AlbedoFinal160_g64201 = ( Snow_Output385_g64201 *  (1.0 + ( TobyTranslucency66_g64201 - 0.0 ) * ( TranslucencyIntensity72_g64201 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float4 lerpResult607_g64201 = lerp( AlbedoFinal160_g64201 , ( AlbedoFinal160_g64201 * 0.65 ) , Wetness594_g64201);
				float4 Albedo_Output621_g64201 = ( ( Wetness594_g64201 * ( saturate( fresnelNode597_g64201 ) * 0.35 ) ) + lerpResult607_g64201 );
				float3 temp_output_751_0_g64190 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.ase_texcoord8.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float4 color792_g64190 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64190 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64190 = ( ( SphericalMaskProxySphere780_g64190 * color792_g64190 ) + ( color793_g64190 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64190 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64190;
				#else
				float4 staticSwitch4373 = Albedo_Output621_g64201;
				#endif
				
				float2 temp_cast_12 = (_SecondaryNormalScale).xx;
				float2 texCoord434_g64201 = input.ase_texcoord7.xy * temp_cast_12 + float2( 0,0 );
				float3 unpack435_g64201 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord434_g64201 ), _SecondaryNormalIntensity );
				unpack435_g64201.z = lerp( 1, unpack435_g64201.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap189_g64201 = input.ase_texcoord7.xy;
				float3 unpack189_g64201 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap189_g64201 ), _NormalIntensity );
				unpack189_g64201.z = lerp( 1, unpack189_g64201.z, saturate(_NormalIntensity) );
				float3 tex2DNode189_g64201 = unpack189_g64201;
				float3 switchResult638_g64201 = (((ase_vface>0)?(tex2DNode189_g64201):(-tex2DNode189_g64201)));
				float3 Normal_Output154_g64201 = BlendNormal( unpack435_g64201 , (( _NormalBackFaceFixBranch )?( switchResult638_g64201 ):( tex2DNode189_g64201 )) );
				
				float2 uv_SpecularMapGrayscale533_g64201 = input.ase_texcoord7.xy;
				float fresnelNdotV519_g64201 = dot( normalize( TangentWS ), SafeNormalize( _MainLightPosition.xyz ) );
				float fresnelNode519_g64201 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV519_g64201 , 0.0001 ), _SpecularStrength ) );
				float SpecularRecalculate516_g64201 = saturate( fresnelNode519_g64201 );
				float Specular_Output522_g64201 = ( ( 0.2 * _SpecularPower ) * saturate( (tex2D( _SpecularMapGrayscale, uv_SpecularMapGrayscale533_g64201 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecularRecalculate516_g64201 );
				float3 temp_cast_13 = (Specular_Output522_g64201).xxx;
				
				float2 uv_MaskMapRGBA89_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode89_g64201 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA89_g64201 );
				float fresnelNdotV596_g64201 = dot( normalize( NormalWS ), ViewDirWS );
				float fresnelNode596_g64201 = ( 1.0 + -1.0 * pow( max( 1.0 - fresnelNdotV596_g64201 , 0.0001 ), 1.0 ) );
				float Smoothness_Output159_g64201 = saturate( ( ( tex2DNode89_g64201.a * _SmoothnessIntensity ) + ( Wetness594_g64201 * fresnelNode596_g64201 ) ) );
				
				float AoMapBase103_g64201 = tex2DNode89_g64201.g;
				float saferPower118_g64201 = abs( AoMapBase103_g64201 );
				float Ao_Output157_g64201 = ( pow( saferPower118_g64201 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency66_g64201 ) * TranslucencyIntensity72_g64201 ) + 1.5 ) ) );
				
				float2 uv_EmissionMap578_g64201 = input.ase_texcoord7.xy;
				float4 Emission_Output579_g64201 = ( float4( tex2D( _EmissionMap, uv_EmissionMap578_g64201 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity );
				
				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Normal = Normal_Output154_g64201;
				float3 Specular = temp_cast_13;
				float Metallic = 0;
				float Smoothness = Smoothness_Output159_g64201;
				float Occlusion = Ao_Output157_g64201;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADER + _FACERENDERING + _DEBUGWINDMASK + Emission_Output579_g64201 + _ADVANCEDSETTINGS ).rgb;
				float Alpha = Opacity_Output155_g64201;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON


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
				float4 ase_color : COLOR;
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			float _SnowAmount;


			float3 _LightDirection;
			float3 _LightPosition;

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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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
				float4 ase_color : COLOR;
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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord1.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float Alpha = Opacity_Output155_g64201;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON


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
				float4 ase_color : COLOR;
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			float _SnowAmount;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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
				float4 ase_color : COLOR;
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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord1.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float Alpha = Opacity_Output155_g64201;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON
			#pragma shader_feature_local _RANDOMCONTRASTONOFF_ON


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
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			float _Wetness;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			sampler2D _EmissionMap;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				output.ase_texcoord3.xyz = ase_tangentWS;
				
				output.ase_texcoord4.xy = input.texcoord0.xy;
				output.ase_texcoord5 = input.positionOS;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.w = 0;
				output.ase_texcoord4.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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

				float Wetness594_g64201 = _Wetness;
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - PositionWS );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float3 ase_tangentWS = input.ase_texcoord3.xyz;
				float fresnelNdotV597_g64201 = dot( normalize( ase_tangentWS ), ase_viewDirWS );
				float fresnelNode597_g64201 = ( 1.0 + -5.0 * pow( max( 1.0 - fresnelNdotV597_g64201 , 0.0001 ), 0.8 ) );
				float CustomDRAWERS440_g64201 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SHADINGSETTINGS + _SEASONSETTINGS );
				float2 uv_AlbedoMap285_g64201 = input.ase_texcoord4.xy;
				float2 uv_AlbedoMap295_g64201 = input.ase_texcoord4.xy;
				float4 tex2DNode295_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap295_g64201 );
				float2 uv_NoiseMapGrayscale302_g64201 = input.ase_texcoord4.xy;
				float4 tex2DNode302_g64201 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale302_g64201 );
				float4 transform452_g64201 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break447_g64201 = transform452_g64201;
				float RandomColorFix451_g64201 = floor( ( ( break447_g64201.x + break447_g64201.z ) * _RandomColorScale ) );
				float2 temp_cast_0 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64202 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64202 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64202 ) * 43758.55 ) ));
				float temp_output_457_0_g64201 = saturate( lerpResult10_g64202 );
				float3 normalizeResult259_g64201 = ASESafeNormalize( input.ase_texcoord5.xyz );
				float DryLeafPositionMask263_g64201 = ( (distance( normalizeResult259_g64201 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_301_0_g64201 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange537_g64201 = _SeasonChangeGlobal;
				float4 lerpResult294_g64201 = lerp( ( _DryLeafColor * ( tex2DNode295_g64201.g * 2 ) ) , tex2DNode295_g64201 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_301_0_g64201 * 0.9 ) + ( temp_output_301_0_g64201 * DryLeafPositionMask263_g64201 * tex2DNode302_g64201.r ) + temp_output_457_0_g64201 ) ):( ( tex2DNode302_g64201.r * temp_output_457_0_g64201 * DryLeafPositionMask263_g64201 ) )) - GlobalVar_SeasonChange537_g64201 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output311_g64201 = lerpResult294_g64201;
				Gradient gradient271_g64201 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_1 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64203 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64203 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64203 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 tex2DNode559_g64201 = tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy );
				float4 TextureBasedColorVariation573_g64201 = (tex2DNode559_g64201*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult282_g64201 = lerp( SeasonControl_Output311_g64201 , ( ( SeasonControl_Output311_g64201 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient271_g64201, TextureBasedColorVariation573_g64201.r ) ):( SampleGradient( gradient271_g64201, saturate( lerpResult10_g64203 ) ) )) * SeasonControl_Output311_g64201 ) ) , _ColorVariation);
				float4 transform279_g64201 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g64204 = dot( transform279_g64201.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g64204 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g64204 ) * 43758.55 ) ));
				float TextureBasedRandomContrast571_g64201 = (tex2DNode559_g64201*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_5 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast571_g64201 ):( lerpResult10_g64204 ))).xxxx;
				float4 blendOpSrc281_g64201 = temp_cast_5;
				float4 blendOpDest281_g64201 = lerpResult282_g64201;
				float4 lerpBlendMode281_g64201 = lerp(blendOpDest281_g64201,(( blendOpDest281_g64201 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest281_g64201 ) * ( 1.0 - blendOpSrc281_g64201 ) ) : ( 2.0 * blendOpDest281_g64201 * blendOpSrc281_g64201 ) ),_RandomContrast);
				#ifdef _RANDOMCONTRASTONOFF_ON
				float4 staticSwitch288_g64201 = ( saturate( lerpBlendMode281_g64201 ));
				#else
				float4 staticSwitch288_g64201 = lerpResult282_g64201;
				#endif
				float2 uv_MaskMapRGBA313_g64201 = input.ase_texcoord4.xy;
				float4 lerpResult283_g64201 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap285_g64201 ) , staticSwitch288_g64201 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA313_g64201 ).r ):( 1.0 )));
				float4 GrassColorVariation_Output109_g64201 = lerpResult283_g64201;
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float2 uv_AlbedoMap362_g64201 = input.ase_texcoord4.xy;
				float2 uv_NormalMap361_g64201 = input.ase_texcoord4.xy;
				float4 lerpResult351_g64201 = lerp( ( ( CustomDRAWERS440_g64201 + _AlbedoColor ) * GrassColorVariation_Output109_g64201 ) , temp_cast_6 , saturate( ( saturate( input.ase_texcoord5.xyz.y ) * GlobalVar_SnowAmount395_g64201 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap361_g64201 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap362_g64201 ).g*_SnowScale + _SnowOffset) )) ) ));
				float4 Snow_Output385_g64201 = lerpResult351_g64201;
				float3 normalizeResult492_g64201 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform39_g64201 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord5.xyz , 0.0 ));
				float3 normalizeResult493_g64201 = ASESafeNormalize( ase_viewDirWS );
				float dotResult52_g64201 = dot( -( float4( normalizeResult492_g64201 , 0.0 ) + ( (( _TranslucencyFluffiness )?( transform39_g64201 ):( float4( input.ase_texcoord5.xyz , 0.0 ) )) * _TranslucencyRange ) ) , float4( normalizeResult493_g64201 , 0.0 ) );
				float2 uv_MaskMapRGBA58_g64201 = input.ase_texcoord4.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency66_g64201 = ( saturate( dotResult52_g64201 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA58_g64201 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity72_g64201 = _TranslucencyPower;
				float4 AlbedoFinal160_g64201 = ( Snow_Output385_g64201 *  (1.0 + ( TobyTranslucency66_g64201 - 0.0 ) * ( TranslucencyIntensity72_g64201 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float4 lerpResult607_g64201 = lerp( AlbedoFinal160_g64201 , ( AlbedoFinal160_g64201 * 0.65 ) , Wetness594_g64201);
				float4 Albedo_Output621_g64201 = ( ( Wetness594_g64201 * ( saturate( fresnelNode597_g64201 ) * 0.35 ) ) + lerpResult607_g64201 );
				float3 temp_output_751_0_g64190 = ( ( input.ase_texcoord5.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.ase_texcoord5.xyz );
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.ase_texcoord5.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float4 color792_g64190 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64190 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64190 = ( ( SphericalMaskProxySphere780_g64190 * color792_g64190 ) + ( color793_g64190 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64190 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64190;
				#else
				float4 staticSwitch4373 = Albedo_Output621_g64201;
				#endif
				
				float2 uv_EmissionMap578_g64201 = input.ase_texcoord4.xy;
				float4 Emission_Output579_g64201 = ( float4( tex2D( _EmissionMap, uv_EmissionMap578_g64201 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity );
				
				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord4.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADER + _FACERENDERING + _DEBUGWINDMASK + Emission_Output579_g64201 + _ADVANCEDSETTINGS ).rgb;
				float Alpha = Opacity_Output155_g64201;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON
			#pragma shader_feature_local _RANDOMCONTRASTONOFF_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
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
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			float _Wetness;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				output.ase_texcoord1.xyz = ase_tangentWS;
				
				output.ase_texcoord2.xy = input.ase_texcoord.xy;
				output.ase_texcoord3 = input.positionOS;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.w = 0;
				output.ase_texcoord2.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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
				float4 ase_color : COLOR;
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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float Wetness594_g64201 = _Wetness;
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - PositionWS );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float3 ase_tangentWS = input.ase_texcoord1.xyz;
				float fresnelNdotV597_g64201 = dot( normalize( ase_tangentWS ), ase_viewDirWS );
				float fresnelNode597_g64201 = ( 1.0 + -5.0 * pow( max( 1.0 - fresnelNdotV597_g64201 , 0.0001 ), 0.8 ) );
				float CustomDRAWERS440_g64201 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SHADINGSETTINGS + _SEASONSETTINGS );
				float2 uv_AlbedoMap285_g64201 = input.ase_texcoord2.xy;
				float2 uv_AlbedoMap295_g64201 = input.ase_texcoord2.xy;
				float4 tex2DNode295_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap295_g64201 );
				float2 uv_NoiseMapGrayscale302_g64201 = input.ase_texcoord2.xy;
				float4 tex2DNode302_g64201 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale302_g64201 );
				float4 transform452_g64201 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break447_g64201 = transform452_g64201;
				float RandomColorFix451_g64201 = floor( ( ( break447_g64201.x + break447_g64201.z ) * _RandomColorScale ) );
				float2 temp_cast_0 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64202 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64202 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64202 ) * 43758.55 ) ));
				float temp_output_457_0_g64201 = saturate( lerpResult10_g64202 );
				float3 normalizeResult259_g64201 = ASESafeNormalize( input.ase_texcoord3.xyz );
				float DryLeafPositionMask263_g64201 = ( (distance( normalizeResult259_g64201 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_301_0_g64201 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange537_g64201 = _SeasonChangeGlobal;
				float4 lerpResult294_g64201 = lerp( ( _DryLeafColor * ( tex2DNode295_g64201.g * 2 ) ) , tex2DNode295_g64201 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_301_0_g64201 * 0.9 ) + ( temp_output_301_0_g64201 * DryLeafPositionMask263_g64201 * tex2DNode302_g64201.r ) + temp_output_457_0_g64201 ) ):( ( tex2DNode302_g64201.r * temp_output_457_0_g64201 * DryLeafPositionMask263_g64201 ) )) - GlobalVar_SeasonChange537_g64201 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output311_g64201 = lerpResult294_g64201;
				Gradient gradient271_g64201 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_1 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64203 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64203 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64203 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 tex2DNode559_g64201 = tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy );
				float4 TextureBasedColorVariation573_g64201 = (tex2DNode559_g64201*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult282_g64201 = lerp( SeasonControl_Output311_g64201 , ( ( SeasonControl_Output311_g64201 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient271_g64201, TextureBasedColorVariation573_g64201.r ) ):( SampleGradient( gradient271_g64201, saturate( lerpResult10_g64203 ) ) )) * SeasonControl_Output311_g64201 ) ) , _ColorVariation);
				float4 transform279_g64201 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g64204 = dot( transform279_g64201.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g64204 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g64204 ) * 43758.55 ) ));
				float TextureBasedRandomContrast571_g64201 = (tex2DNode559_g64201*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_5 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast571_g64201 ):( lerpResult10_g64204 ))).xxxx;
				float4 blendOpSrc281_g64201 = temp_cast_5;
				float4 blendOpDest281_g64201 = lerpResult282_g64201;
				float4 lerpBlendMode281_g64201 = lerp(blendOpDest281_g64201,(( blendOpDest281_g64201 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest281_g64201 ) * ( 1.0 - blendOpSrc281_g64201 ) ) : ( 2.0 * blendOpDest281_g64201 * blendOpSrc281_g64201 ) ),_RandomContrast);
				#ifdef _RANDOMCONTRASTONOFF_ON
				float4 staticSwitch288_g64201 = ( saturate( lerpBlendMode281_g64201 ));
				#else
				float4 staticSwitch288_g64201 = lerpResult282_g64201;
				#endif
				float2 uv_MaskMapRGBA313_g64201 = input.ase_texcoord2.xy;
				float4 lerpResult283_g64201 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap285_g64201 ) , staticSwitch288_g64201 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA313_g64201 ).r ):( 1.0 )));
				float4 GrassColorVariation_Output109_g64201 = lerpResult283_g64201;
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float2 uv_AlbedoMap362_g64201 = input.ase_texcoord2.xy;
				float2 uv_NormalMap361_g64201 = input.ase_texcoord2.xy;
				float4 lerpResult351_g64201 = lerp( ( ( CustomDRAWERS440_g64201 + _AlbedoColor ) * GrassColorVariation_Output109_g64201 ) , temp_cast_6 , saturate( ( saturate( input.ase_texcoord3.xyz.y ) * GlobalVar_SnowAmount395_g64201 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap361_g64201 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap362_g64201 ).g*_SnowScale + _SnowOffset) )) ) ));
				float4 Snow_Output385_g64201 = lerpResult351_g64201;
				float3 normalizeResult492_g64201 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform39_g64201 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord3.xyz , 0.0 ));
				float3 normalizeResult493_g64201 = ASESafeNormalize( ase_viewDirWS );
				float dotResult52_g64201 = dot( -( float4( normalizeResult492_g64201 , 0.0 ) + ( (( _TranslucencyFluffiness )?( transform39_g64201 ):( float4( input.ase_texcoord3.xyz , 0.0 ) )) * _TranslucencyRange ) ) , float4( normalizeResult493_g64201 , 0.0 ) );
				float2 uv_MaskMapRGBA58_g64201 = input.ase_texcoord2.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency66_g64201 = ( saturate( dotResult52_g64201 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA58_g64201 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity72_g64201 = _TranslucencyPower;
				float4 AlbedoFinal160_g64201 = ( Snow_Output385_g64201 *  (1.0 + ( TobyTranslucency66_g64201 - 0.0 ) * ( TranslucencyIntensity72_g64201 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float4 lerpResult607_g64201 = lerp( AlbedoFinal160_g64201 , ( AlbedoFinal160_g64201 * 0.65 ) , Wetness594_g64201);
				float4 Albedo_Output621_g64201 = ( ( Wetness594_g64201 * ( saturate( fresnelNode597_g64201 ) * 0.35 ) ) + lerpResult607_g64201 );
				float3 temp_output_751_0_g64190 = ( ( input.ase_texcoord3.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.ase_texcoord3.xyz );
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.ase_texcoord3.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float4 color792_g64190 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64190 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64190 = ( ( SphericalMaskProxySphere780_g64190 * color792_g64190 ) + ( color793_g64190 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64190 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64190;
				#else
				float4 staticSwitch4373 = Albedo_Output621_g64201;
				#endif
				
				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord2.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float3 BaseColor = staticSwitch4373.rgb;
				float Alpha = Opacity_Output155_g64201;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON


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
				float4 ase_color : COLOR;
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _NormalMap;
			sampler2D _AlbedoMap;
			float _SnowAmount;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord3.xy = input.texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						, uint ase_vface : SV_IsFrontFace )
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
				float2 texCoord434_g64201 = input.ase_texcoord3.xy * temp_cast_0 + float2( 0,0 );
				float3 unpack435_g64201 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord434_g64201 ), _SecondaryNormalIntensity );
				unpack435_g64201.z = lerp( 1, unpack435_g64201.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap189_g64201 = input.ase_texcoord3.xy;
				float3 unpack189_g64201 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap189_g64201 ), _NormalIntensity );
				unpack189_g64201.z = lerp( 1, unpack189_g64201.z, saturate(_NormalIntensity) );
				float3 tex2DNode189_g64201 = unpack189_g64201;
				float3 switchResult638_g64201 = (((ase_vface>0)?(tex2DNode189_g64201):(-tex2DNode189_g64201)));
				float3 Normal_Output154_g64201 = BlendNormal( unpack435_g64201 , (( _NormalBackFaceFixBranch )?( switchResult638_g64201 ):( tex2DNode189_g64201 )) );
				
				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord3.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float3 Normal = Normal_Output154_g64201;
				float Alpha = Opacity_Output155_g64201;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON
			#pragma shader_feature_local _RANDOMCONTRASTONOFF_ON


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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			float _Wetness;
			sampler2D _AlbedoMap;
			sampler2D _NoiseMapGrayscale;
			float _SeasonChangeGlobal;
			sampler2D _MaskMapRGBA;
			float _SnowAmount;
			sampler2D _NormalMap;
			sampler2D _SecondaryNormalMapCustomNormals;
			sampler2D _SpecularMapGrayscale;
			sampler2D _EmissionMap;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;
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
								, uint ase_vface : SV_IsFrontFace )
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

				float Wetness594_g64201 = _Wetness;
				float fresnelNdotV597_g64201 = dot( normalize( TangentWS ), ViewDirWS );
				float fresnelNode597_g64201 = ( 1.0 + -5.0 * pow( max( 1.0 - fresnelNdotV597_g64201 , 0.0001 ), 0.8 ) );
				float CustomDRAWERS440_g64201 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _SHADINGSETTINGS + _SEASONSETTINGS );
				float2 uv_AlbedoMap285_g64201 = input.ase_texcoord7.xy;
				float2 uv_AlbedoMap295_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode295_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap295_g64201 );
				float2 uv_NoiseMapGrayscale302_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode302_g64201 = tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale302_g64201 );
				float4 transform452_g64201 = mul(GetObjectToWorldMatrix(),float4( 1,1,1,1 ));
				float4 break447_g64201 = transform452_g64201;
				float RandomColorFix451_g64201 = floor( ( ( break447_g64201.x + break447_g64201.z ) * _RandomColorScale ) );
				float2 temp_cast_0 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64202 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64202 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64202 ) * 43758.55 ) ));
				float temp_output_457_0_g64201 = saturate( lerpResult10_g64202 );
				float3 normalizeResult259_g64201 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float DryLeafPositionMask263_g64201 = ( (distance( normalizeResult259_g64201 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
				float temp_output_301_0_g64201 = ( 1.0 - input.ase_color.r );
				float GlobalVar_SeasonChange537_g64201 = _SeasonChangeGlobal;
				float4 lerpResult294_g64201 = lerp( ( _DryLeafColor * ( tex2DNode295_g64201.g * 2 ) ) , tex2DNode295_g64201 , saturate( (( (( _SeasonVertexColorR )?( ( ( temp_output_301_0_g64201 * 0.9 ) + ( temp_output_301_0_g64201 * DryLeafPositionMask263_g64201 * tex2DNode302_g64201.r ) + temp_output_457_0_g64201 ) ):( ( tex2DNode302_g64201.r * temp_output_457_0_g64201 * DryLeafPositionMask263_g64201 ) )) - GlobalVar_SeasonChange537_g64201 )*_DryLeavesScale + _DryLeavesOffset) ));
				float4 SeasonControl_Output311_g64201 = lerpResult294_g64201;
				Gradient gradient271_g64201 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
				float2 temp_cast_1 = (RandomColorFix451_g64201).xx;
				float dotResult4_g64203 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
				float lerpResult10_g64203 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64203 ) * 43758.55 ) ));
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 tex2DNode559_g64201 = tex2D( _NoiseMapGrayscale, ( ase_objectPosition * _ZaWorldoScale ).xy );
				float4 TextureBasedColorVariation573_g64201 = (tex2DNode559_g64201*_TBCVMapIntenisty + _TBCVMapOffset);
				float4 lerpResult282_g64201 = lerp( SeasonControl_Output311_g64201 , ( ( SeasonControl_Output311_g64201 * 0.5 ) + ( (( _TBCVOnUsesNoiseMap )?( SampleGradient( gradient271_g64201, TextureBasedColorVariation573_g64201.r ) ):( SampleGradient( gradient271_g64201, saturate( lerpResult10_g64203 ) ) )) * SeasonControl_Output311_g64201 ) ) , _ColorVariation);
				float4 transform279_g64201 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				float dotResult4_g64204 = dot( transform279_g64201.xy , float2( 12.9898,78.233 ) );
				float lerpResult10_g64204 = lerp( 0.2 , 1.2 , frac( ( sin( dotResult4_g64204 ) * 43758.55 ) ));
				float TextureBasedRandomContrast571_g64201 = (tex2DNode559_g64201*_TBRCMapIntenisty + _TBRCMapOffset).r;
				float4 temp_cast_5 = ((( _TBRCOnUsesNoiseMap )?( TextureBasedRandomContrast571_g64201 ):( lerpResult10_g64204 ))).xxxx;
				float4 blendOpSrc281_g64201 = temp_cast_5;
				float4 blendOpDest281_g64201 = lerpResult282_g64201;
				float4 lerpBlendMode281_g64201 = lerp(blendOpDest281_g64201,(( blendOpDest281_g64201 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest281_g64201 ) * ( 1.0 - blendOpSrc281_g64201 ) ) : ( 2.0 * blendOpDest281_g64201 * blendOpSrc281_g64201 ) ),_RandomContrast);
				#ifdef _RANDOMCONTRASTONOFF_ON
				float4 staticSwitch288_g64201 = ( saturate( lerpBlendMode281_g64201 ));
				#else
				float4 staticSwitch288_g64201 = lerpResult282_g64201;
				#endif
				float2 uv_MaskMapRGBA313_g64201 = input.ase_texcoord7.xy;
				float4 lerpResult283_g64201 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap285_g64201 ) , staticSwitch288_g64201 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA313_g64201 ).r ):( 1.0 )));
				float4 GrassColorVariation_Output109_g64201 = lerpResult283_g64201;
				float4 temp_cast_6 = (saturate( _SnowValue )).xxxx;
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float2 uv_AlbedoMap362_g64201 = input.ase_texcoord7.xy;
				float2 uv_NormalMap361_g64201 = input.ase_texcoord7.xy;
				float4 lerpResult351_g64201 = lerp( ( ( CustomDRAWERS440_g64201 + _AlbedoColor ) * GrassColorVariation_Output109_g64201 ) , temp_cast_6 , saturate( ( saturate( input.ase_texcoord8.xyz.y ) * GlobalVar_SnowAmount395_g64201 * (( _NormalMapBasedSnow )?( (tex2D( _NormalMap, uv_NormalMap361_g64201 ).g*_SnowScale + _SnowOffset) ):( (tex2D( _AlbedoMap, uv_AlbedoMap362_g64201 ).g*_SnowScale + _SnowOffset) )) ) ));
				float4 Snow_Output385_g64201 = lerpResult351_g64201;
				float3 normalizeResult492_g64201 = ASESafeNormalize( ( SafeNormalize( _MainLightPosition.xyz ) + _MainLightPosition.xyz ) );
				float4 transform39_g64201 = mul(GetObjectToWorldMatrix(),float4( input.ase_texcoord8.xyz , 0.0 ));
				float3 normalizeResult493_g64201 = ASESafeNormalize( ViewDirWS );
				float dotResult52_g64201 = dot( -( float4( normalizeResult492_g64201 , 0.0 ) + ( (( _TranslucencyFluffiness )?( transform39_g64201 ):( float4( input.ase_texcoord8.xyz , 0.0 ) )) * _TranslucencyRange ) ) , float4( normalizeResult493_g64201 , 0.0 ) );
				float2 uv_MaskMapRGBA58_g64201 = input.ase_texcoord7.xy;
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float TobyTranslucency66_g64201 = ( saturate( dotResult52_g64201 ) * tex2D( _MaskMapRGBA, uv_MaskMapRGBA58_g64201 ).b * max( ase_lightColor.a , 0.0 ) );
				float TranslucencyIntensity72_g64201 = _TranslucencyPower;
				float4 AlbedoFinal160_g64201 = ( Snow_Output385_g64201 *  (1.0 + ( TobyTranslucency66_g64201 - 0.0 ) * ( TranslucencyIntensity72_g64201 - 1.0 ) / ( 1.0 - 0.0 ) ) );
				float4 lerpResult607_g64201 = lerp( AlbedoFinal160_g64201 , ( AlbedoFinal160_g64201 * 0.65 ) , Wetness594_g64201);
				float4 Albedo_Output621_g64201 = ( ( Wetness594_g64201 * ( saturate( fresnelNode597_g64201 ) * 0.35 ) ) + lerpResult607_g64201 );
				float3 temp_output_751_0_g64190 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.ase_texcoord8.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float4 color792_g64190 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64190 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64190 = ( ( SphericalMaskProxySphere780_g64190 * color792_g64190 ) + ( color793_g64190 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64190 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64190;
				#else
				float4 staticSwitch4373 = Albedo_Output621_g64201;
				#endif
				
				float2 temp_cast_12 = (_SecondaryNormalScale).xx;
				float2 texCoord434_g64201 = input.ase_texcoord7.xy * temp_cast_12 + float2( 0,0 );
				float3 unpack435_g64201 = UnpackNormalScale( tex2D( _SecondaryNormalMapCustomNormals, texCoord434_g64201 ), _SecondaryNormalIntensity );
				unpack435_g64201.z = lerp( 1, unpack435_g64201.z, saturate(_SecondaryNormalIntensity) );
				float2 uv_NormalMap189_g64201 = input.ase_texcoord7.xy;
				float3 unpack189_g64201 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap189_g64201 ), _NormalIntensity );
				unpack189_g64201.z = lerp( 1, unpack189_g64201.z, saturate(_NormalIntensity) );
				float3 tex2DNode189_g64201 = unpack189_g64201;
				float3 switchResult638_g64201 = (((ase_vface>0)?(tex2DNode189_g64201):(-tex2DNode189_g64201)));
				float3 Normal_Output154_g64201 = BlendNormal( unpack435_g64201 , (( _NormalBackFaceFixBranch )?( switchResult638_g64201 ):( tex2DNode189_g64201 )) );
				
				float2 uv_SpecularMapGrayscale533_g64201 = input.ase_texcoord7.xy;
				float fresnelNdotV519_g64201 = dot( normalize( TangentWS ), SafeNormalize( _MainLightPosition.xyz ) );
				float fresnelNode519_g64201 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV519_g64201 , 0.0001 ), _SpecularStrength ) );
				float SpecularRecalculate516_g64201 = saturate( fresnelNode519_g64201 );
				float Specular_Output522_g64201 = ( ( 0.2 * _SpecularPower ) * saturate( (tex2D( _SpecularMapGrayscale, uv_SpecularMapGrayscale533_g64201 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecularRecalculate516_g64201 );
				float3 temp_cast_13 = (Specular_Output522_g64201).xxx;
				
				float2 uv_MaskMapRGBA89_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode89_g64201 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA89_g64201 );
				float fresnelNdotV596_g64201 = dot( normalize( NormalWS ), ViewDirWS );
				float fresnelNode596_g64201 = ( 1.0 + -1.0 * pow( max( 1.0 - fresnelNdotV596_g64201 , 0.0001 ), 1.0 ) );
				float Smoothness_Output159_g64201 = saturate( ( ( tex2DNode89_g64201.a * _SmoothnessIntensity ) + ( Wetness594_g64201 * fresnelNode596_g64201 ) ) );
				
				float AoMapBase103_g64201 = tex2DNode89_g64201.g;
				float saferPower118_g64201 = abs( AoMapBase103_g64201 );
				float Ao_Output157_g64201 = ( pow( saferPower118_g64201 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency66_g64201 ) * TranslucencyIntensity72_g64201 ) + 1.5 ) ) );
				
				float2 uv_EmissionMap578_g64201 = input.ase_texcoord7.xy;
				float4 Emission_Output579_g64201 = ( float4( tex2D( _EmissionMap, uv_EmissionMap578_g64201 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity );
				
				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord7.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Normal = Normal_Output154_g64201;
				float3 Specular = temp_cast_13;
				float Metallic = 0;
				float Smoothness = Smoothness_Output159_g64201;
				float Occlusion = Ao_Output157_g64201;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADER + _FACERENDERING + _DEBUGWINDMASK + Emission_Output579_g64201 + _ADVANCEDSETTINGS ).rgb;
				float Alpha = Opacity_Output155_g64201;
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON


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
				float4 ase_color : COLOR;
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			float _SnowAmount;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;

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
				float4 ase_color : COLOR;
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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord1.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				surfaceDescription.Alpha = Opacity_Output155_g64201;
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON


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
				float4 ase_color : COLOR;
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];
			float _GlobalWindStrength;
			float4 _WindDirection;
			float _StrongWindSpeed;
			float _WindMotion;
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			float _SnowAmount;


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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float3 ASESafeNormalize(float3 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			
			float ASESafeNormalize(float inVec)
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

				float GlobalVar_WindStrength1163_g64190 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64190 = appendResult1270_g64190;
				float4 WindDirection1065_g64190 = _WindDirection;
				float GlobalVar_WindSpeed921_g64190 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64190 = ( -(WindDirection1065_g64190).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64190 );
				float2 WPRG2D1115_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + NoiseRotation_Output1269_g64190 );
				float simpleNoise1180_g64190 = SimpleNoise( WPRG2D1115_g64190 );
				float3 break1192_g64190 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64190 = (float3(break1192_g64190.x , ( break1192_g64190.y * 0.3 ) , break1192_g64190.z));
				float3 smoothstepResult1181_g64190 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64190);
				float3 worldToObjDir1188_g64190 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64190*2.2 + -1.05) * ( smoothstepResult1181_g64190 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64190 = float3( 0, 1, 0 );
				float2 appendResult1201_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64190 = snoise( ( appendResult1201_g64190 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64190 ) )*0.6 );
				simplePerlin2D1205_g64190 = simplePerlin2D1205_g64190*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64190 = simplePerlin2D1205_g64190;
				float mulTime702_g64190 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64190 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64190 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64190, NoiseLarge_BASIC1207_g64190 );
				float3 worldToObjDir715_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( rotatedValue711_g64190 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64190 = snoise( WPRG2D1115_g64190*0.1 );
				float MaskRotation268_g64190 = saturate( simplePerlin2D211_g64190 );
				float3 clampResult148_g64190 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64190 = clampResult148_g64190;
				float simplePerlin2D651_g64190 = snoise( WPRG2D1115_g64190*0.09995 );
				simplePerlin2D651_g64190 = simplePerlin2D651_g64190*0.5 + 0.5;
				float MaskRotation2649_g64190 = saturate( simplePerlin2D651_g64190 );
				float3 temp_output_725_0_g64190 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64190 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64190, NormaliseRotationAxis218_g64190, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64190 ) );
				float3 worldToObjDir732_g64190 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64190 - temp_output_725_0_g64190 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64190 = ASESafeNormalize( WindDirection1065_g64190 );
				float3 break1122_g64190 = (normalizeResult1119_g64190).xyz;
				float4 appendResult1124_g64190 = (float4(break1122_g64190.x , ( break1122_g64190.y + _MotionBendingDownwardStrength ) , break1122_g64190.z , 0.0));
				float4 temp_output_1127_0_g64190 = ( appendResult1124_g64190 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64190 = temp_output_1127_0_g64190;
				float GlobalVar_WindMotion1165_g64190 = _WindMotion;
				float2 WPRG2D_S41140_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 4.0 ) );
				float simplePerlin2D1136_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1136_g64190 = simplePerlin2D1136_g64190*0.5 + 0.5;
				float WindMotionNoise1300_g64190 = simplePerlin2D1136_g64190;
				float3 worldToObjDir1292_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64190 *  (0.0 + ( GlobalVar_WindMotion1165_g64190 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64190 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64190 = ( worldToObjDir1292_g64190 * ase_objectScale * Scale_MaskA177_g64190 );
				float4 MotionBendingGentleRandom1291_g64190 = ( WindMotion_Base1297_g64190 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64190 );
				float mulTime1551_g64190 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64190 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64190 = dotResult1485_g64190;
				float dotResult1487_g64190 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64190 = dotResult1487_g64190;
				float mulTime1550_g64190 = _TimeParameters.x * 4.3;
				float dotResult1486_g64190 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64190 = dotResult1486_g64190;
				float3 rotatedValue1564_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64190 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64190 ) + ( RandomSeedVector_D1489_g64190 * 0.02 ) ) ) + sin( ( mulTime1550_g64190 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64190 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64190 = ( ( ( rotatedValue1564_g64190 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64190 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) * 0.5 );
				float3 temp_output_751_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64190 = dot( temp_output_751_0_g64190 , temp_output_751_0_g64190 );
				float saferPower755_g64190 = abs( saturate( dotResult749_g64190 ) );
				float3 normalizeResult1001_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64190 = (distance( normalizeResult1001_g64190 , ( NormaliseRotationAxis218_g64190 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64190 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64190 , _Hardness2 ) ) * CenterOfMass999_g64190 );
				float simplePerlin2D1225_g64190 = snoise( WPRG2D_S41140_g64190*0.2 );
				simplePerlin2D1225_g64190 = simplePerlin2D1225_g64190*0.5 + 0.5;
				float simplePerlin2D1224_g64190 = snoise( WPRG2D1115_g64190*0.2 );
				simplePerlin2D1224_g64190 = simplePerlin2D1224_g64190*0.5 + 0.5;
				float3 appendResult1227_g64190 = (float3(( simplePerlin2D1225_g64190 * simplePerlin2D1224_g64190 ) , 0.0 , simplePerlin2D1224_g64190));
				float3 worldToObjDir1233_g64190 = mul( GetWorldToObjectMatrix(), float4( ( appendResult1227_g64190 * 1.2 ), 0.0 ) ).xyz;
				float dotResult4_g64193 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64193 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64193 ) * 43758.55 ) ));
				float mulTime860_g64190 = _TimeParameters.x * 2.0;
				float3 worldToObjDir878_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * float4( ( ( cos( ( ( float3( 0.6, 0, 0.8 ) * 3.0 * input.positionOS.xyz ) + cos( _TimeParameters.x * 0.125 ) + mulTime860_g64190 ) ) * saturate( input.positionOS.xyz.y ) ) * 0.2 ) , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float2 WPRG2D_S71263_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 7.0 ) );
				float dotResult4_g64192 = dot( float2( 0.9,0.8 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64192 = lerp( 0.0 , 0.8 , frac( ( sin( dotResult4_g64192 ) * 43758.55 ) ));
				float dotResult4_g64191 = dot( float2( 2,3 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64191 = lerp( 0.0 , 1.3 , frac( ( sin( dotResult4_g64191 ) * 43758.55 ) ));
				float simplePerlin2D1250_g64190 = snoise( ( float3( WPRG2D_S71263_g64190 ,  0.0 ) * lerpResult10_g64192 * lerpResult10_g64191 * float3( 1.4, -0.3, 0.6 ) ).xy*0.05 );
				float3 worldToObjDir1257_g64190 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64190 * simplePerlin2D1250_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_892_0_g64190 = ( input.positionOS.xyz * 0.1 );
				float simplePerlin2D69_g64190 = snoise( WPRG2D1115_g64190*0.6 );
				simplePerlin2D69_g64190 = simplePerlin2D69_g64190*0.5 + 0.5;
				float NoiseLarge101_g64190 = simplePerlin2D69_g64190;
				float3 worldToObjDir889_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( saturate( input.positionOS.xyz.y ) * NoiseLarge101_g64190 ) * 3.0 * float3( -1, 0, -1 ) ), 0.0 ) ).xyz;
				float dotResult4_g64194 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64194 = lerp( 0.0 , 0.05 , frac( ( sin( dotResult4_g64194 ) * 43758.55 ) ));
				float3 temp_cast_15 = (lerpResult10_g64194).xxx;
				float3 rotatedValue938_g64190 = RotateAroundAxis( temp_cast_15, temp_output_892_0_g64190, NormaliseRotationAxis218_g64190, worldToObjDir889_g64190.x );
				float3 worldToObjDir1354_g64190 = mul( GetWorldToObjectMatrix(), float4( ( temp_output_892_0_g64190 - rotatedValue938_g64190 ), 0.0 ) ).xyz;
				float3 normalizeResult476_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64190 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64190 = snoise( ( normalizeResult476_g64190 + mulTime477_g64190 ).xy*0.4 );
				float NoiseMask_LargeA482_g64190 = ( simplePerlin2D479_g64190 * 1.5 );
				float3 normalizeResult494_g64190 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64190 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64190 = snoise( ( normalizeResult494_g64190 + mulTime499_g64190 ).xy*0.7 );
				float NoiseMask_LargeB502_g64190 = ( simplePerlin2D498_g64190 * 1.5 );
				float3 worldToObjDir980_g64190 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64190 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64190 * NoiseMask_LargeB502_g64190 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float dotResult4_g64195 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64195 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g64195 ) * 43758.55 ) ));
				float2 WPRG2D_S2p51334_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 2.5 ) );
				float simpleNoise908_g64190 = SimpleNoise( WPRG2D_S2p51334_g64190*4.0 );
				simpleNoise908_g64190 = simpleNoise908_g64190*2 - 1;
				float2 WPRG2D_S0p41336_g64190 = ( BasicWorldPositionXY_Out1273_g64190 + ( NoiseRotation_Output1269_g64190 * 0.4 ) );
				float simpleNoise907_g64190 = SimpleNoise( WPRG2D_S0p41336_g64190 );
				simpleNoise907_g64190 = simpleNoise907_g64190*2 - 1;
				float3 appendResult906_g64190 = (float3(simpleNoise908_g64190 , 0.0 , simpleNoise907_g64190));
				float3 worldToObjDir937_g64190 = mul( GetWorldToObjectMatrix(), float4( ( float3( -1, -1, -1 ) * lerpResult10_g64195 * appendResult906_g64190 * simpleNoise907_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ), 0.0 ) ).xyz;
				float mulTime953_g64190 = _TimeParameters.x * 5.0;
				float3 temp_cast_20 = (0.0).xxx;
				float mulTime946_g64190 = _TimeParameters.x * 0.3;
				float dotResult4_g64196 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64196 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64196 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64190 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64190 = dot( temp_output_766_0_g64190 , temp_output_766_0_g64190 );
				float saferPower768_g64190 = abs( saturate( dotResult764_g64190 ) );
				float3 normalizeResult772_g64190 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64190 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64190 , -1.0 ) ) * (distance( normalizeResult772_g64190 , ( NormaliseRotationAxis218_g64190 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64190 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64190 * GlobalVar_WindMotion1165_g64190 ) * simplePerlin2D1136_g64190 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_22 = (1.0).xxx;
				float3 WindMotion_Output1289_g64190 = ( worldToObjDir1132_g64190 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64190 ):( temp_cast_22 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64190 = float4(  (float3( 0,0,0 ) + ( ( ( SphericalMaskProxySphere780_g64190 * ( ( ( worldToObjDir1233_g64190 * float3( -0.5, 0.02, -0.5 ) * lerpResult10_g64193 * saturate( input.positionOS.xyz.y ) * ase_objectScale * Scale_MaskA177_g64190 ) * 0.5 ) + ( worldToObjDir878_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1257_g64190 * saturate( input.positionOS.xyz.y ) * float3( -0.5, 0, -0.5 ) * ase_objectScale * Scale_MaskA177_g64190 ) + ( worldToObjDir1354_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) ) ) + ( ( ( worldToObjDir980_g64190 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + worldToObjDir937_g64190 + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64190 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64190 * GlobalVar_WindSpeed921_g64190 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64190 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64190 * mulTime946_g64190 ) * ( 20.0 * lerpResult10_g64196 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64190 * Scale_MaskA177_g64190 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_20 )) ) + WindMotion_Output1289_g64190 + (( _PivotSway )?( RandomPivotMotiton1572_g64190 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64190 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64190 ) , 0.0 ) + float4( ( worldToObjDir715_g64190 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64190 * Scale_MaskA177_g64190 ) , 0.0 ) + float4( ( MaskRotation2649_g64190 * worldToObjDir732_g64190 * ase_objectScale * Scale_MaskA177_g64190 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64190 , 0.0 ) + MotionBendingGentleRandom1291_g64190 + float4( PivotSwayGentle1576_g64190 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float normalizeResult441_g64190 = ASESafeNormalize( input.positionOS.xyz.y );
				float3 lerpResult442_g64190 = lerp( ( ( NormaliseRotationAxis218_g64190 * ( 1.0 - input.positionOS.xyz.y ) ) * _GroundBlendingOffset ) , float3( 0,0,0 ) , saturate( normalizeResult441_g64190 ));
				float3 GroundBlendingExperimental446_g64190 =  (float3( 0,0,0 ) + ( lerpResult442_g64190 - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) );
				float4 temp_output_341_0_g64190 = ( GlobalVar_WindStrength1163_g64190 * ( staticSwitch1011_g64190 + float4( (( _GroundBlendingExperimental )?( GroundBlendingExperimental446_g64190 ):( float3( 0,0,0 ) )) , 0.0 ) + _TEXTUREMAPS + _WINDMASKSETTINGS ) );
				float3 temp_output_1442_0_g64190 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64190 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64190 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64190 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64190 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64190 = snoise( ( _TimeParameters.x + appendResult1422_g64190 ) );
				simplePerlin2D1431_g64190 = simplePerlin2D1431_g64190*0.5 + 0.5;
				float3 InteractionNoise1445_g64190 = ( ( sin( ( mulTime1424_g64190 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64190 ) * 0.4 );
				float4 transform1455_g64190 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64190 ) * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 * InteractionNoise1445_g64190 ) + ( ( temp_output_1442_0_g64190 * _BendAmountGrass * temp_output_1440_0_g64190 * temp_output_1441_0_g64190 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64190 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64190 = ( transform1455_g64190 * saturate( smoothstepResult1603_g64190 ) );
				float3 normalizeResult1587_g64190 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64190 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64190*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64190 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64190 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64190 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64190 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64190 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64190 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64190 ) ):( ( temp_output_1626_0_g64190 * normalizeResult1629_g64190 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64190 = ( rotatedValue1636_g64190 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64190 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64190 = staticSwitch1640_g64190;
				float4 FinalWind_Output350_g64190 = ( temp_output_341_0_g64190 + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64190 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64190 + float4( PhysicsInteraction1638_g64190 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64190;
				#endif
				
				float3 LightDetectBackface_Output156_g64201 = (( _MobileShadingWorldUp )?( float3( 0, 1, 0 ) ):( input.normalOS ));
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch4381.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = LightDetectBackface_Output156_g64201;

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
				float4 ase_color : COLOR;
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
				output.ase_color = input.ase_color;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
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

				float2 uv_AlbedoMap86_g64201 = input.ase_texcoord1.xy;
				float4 tex2DNode86_g64201 = tex2D( _AlbedoMap, uv_AlbedoMap86_g64201 );
				float temp_output_200_0_g64201 = ( unity_LODFade.x > 0.0 ? ( unity_LODFade.x * tex2DNode86_g64201.a ) : tex2DNode86_g64201.a );
				float AlphaFinal421_g64201 = temp_output_200_0_g64201;
				float2 appendResult405_g64201 = (float2(PositionWS.x , PositionWS.z));
				float simpleNoise408_g64201 = SimpleNoise( appendResult405_g64201*_SinkingNoiseScale );
				float temp_output_409_0_g64201 = saturate( simpleNoise408_g64201 );
				float GlobalVar_SnowAmount395_g64201 = _SnowAmount;
				float lerpResult414_g64201 = lerp( AlphaFinal421_g64201 , ( temp_output_409_0_g64201 * AlphaFinal421_g64201 ) , saturate( ( temp_output_409_0_g64201 * GlobalVar_SnowAmount395_g64201 ) ));
				float SnowSinking_Output424_g64201 = saturate( lerpResult414_g64201 );
				float Opacity_Output155_g64201 = (( _SnowAccumulationGroundSinking )?( SnowSinking_Output424_g64201 ):( temp_output_200_0_g64201 ));
				

				surfaceDescription.Alpha = Opacity_Output155_g64201;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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
			float4 _DryLeafColor;
			float4 _EmissionColor;
			float4 _AlbedoColor;
			float3 _BendAxis;
			float3 _BendDirection;
			float3 _WindAxis;
			float _GrassSwayPowerGentle;
			float _TranslucencyRange;
			float _TranslucencyFluffiness;
			float _SnowOffset;
			float _SnowScale;
			float _NormalMapBasedSnow;
			float _BranchMaskR;
			float _RandomContrast;
			float _TBRCMapOffset;
			float _TBRCMapIntenisty;
			float _TBRCOnUsesNoiseMap;
			float _ColorVariation;
			float _TBCVMapOffset;
			float _SnowValue;
			float _TranslucencyPower;
			float _SecondaryNormalScale;
			float _SecondaryNormalIntensity;
			float _SnowAccumulationGroundSinking;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity;
			float _DEBUGWINDMASK;
			float _FACERENDERING;
			float _TTFEGRASSFOLIAGESHADER;
			float _AmbientOcclusionIntensity;
			float _TBCVMapIntenisty;
			float _SmoothnessIntensity;
			float _SpecularScale;
			float _SpecularBias;
			float _SpecularMapOffset;
			float _SpecularMapScale;
			float _SpecularPower;
			float _NormalIntensity;
			float _NormalBackFaceFixBranch;
			float _SpecularStrength;
			float _ZaWorldoScale;
			float _DryLeavesScale;
			float _DryLeavesOffset;
			float _GroundBlendingOffset;
			float _GroundBlendingExperimental;
			float _WindMotionScaleMask;
			float _QuadScatterIntensity;
			float _QuadScatterWorldNormals;
			float _QuadScatterOnlybasiccards;
			float _MotionFlutterIntensity;
			half _MotionFlutterScale;
			float _CenterofMassintensity;
			float _Hardness2;
			float _Radius2;
			float _PivotSwayPower;
			float _PivotSway;
			float _MotionBendingRandomnessGentleWind;
			float _MotionBendingDownwardStrength;
			float _TEXTUREMAPS;
			float _TBCVOnUsesNoiseMap;
			float _WINDMASKSETTINGS;
			float _BendAmountGrass;
			float _SinkingNoiseScale;
			float _RandomColorScale;
			float _SeasonVertexColorR;
			float _SEASONSETTINGS;
			float _SHADINGSETTINGS;
			float _TEXTURESETTINGS;
			float _MobileShadingWorldUp;
			float _WindAmplitude;
			float _WindFrequency;
			float _BendAmount;
			float _HeightMax;
			float _PhysicsWind;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _GrassInteraction;
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

			float4 _TTFE_InteractPositions[8];
			float _TTFE_BendRadii[8];
			float _TTFE_TrampleStrengths[8];


			
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
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4194;-768,-720;Inherit;False;584.2366;656.0368;;6;4005;4295;4301;4020;4018;4019;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4385;-704,288;Inherit;False;(TTFE) Grass Foliage_Wind System;56;;64190;587ec12ad6a469d48af93c852f23db70;0;0;2;FLOAT4;0;COLOR;1007
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4018;-624,-368;Inherit;False;Property;_DEBUGWINDMASK;DEBUG WIND MASK;94;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4020;-336,-448;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;COLOR;0,0,0,0;False;4;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4373;-336,-48;Inherit;False;Property;_DEBUGVisualizeWindMask;[DEBUG] Visualize Wind Mask;95;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4381;-400,400;Inherit;False;Property;_SAVEPERFORMANCEDeactivateWind;[SAVE PERFORMANCE] Deactivate Wind;97;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4005;-720,-656;Inherit;False;Property;_TTFEGRASSFOLIAGESHADER;(TTFE) GRASS FOLIAGE SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4301;-624,-560;Inherit;False;Property;_FACERENDERING;FACE RENDERING;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4435;-224,272;Inherit;False;Property;_AlphaClip;Alpha Clip;2;0;Create;True;0;0;0;False;1;Space (10);False;0.4;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4358;-640,-288;Inherit;False;Property;_ADVANCEDSETTINGS;ADVANCED SETTINGS;96;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4019;-592,-192;Inherit;False;Property;_DIVIDER_00;DIVIDER_00;98;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4295;-624,-464;Inherit;False;Constant;_BackfaceCulling;Backface Culling;3;1;[Enum];Create;True;0;3;Off;0;Front;1;Back;2;0;True;1;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4436;-688,0;Inherit;False;(TTFE) Grass Foliage_Shading;3;;64201;c37c82b3855ad564a8be75e0d3f5f6bc;0;0;8;COLOR;0;FLOAT3;180;FLOAT;186;FLOAT;182;FLOAT;183;FLOAT;184;FLOAT3;188;COLOR;583
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4425;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;6;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4426;0,0;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;51;Category;0;0;  Instanced Terrain Normals;1;0;Lighting Model;0;0;Workflow;0;638928527524938211;Surface;0;0;  Keep Alpha;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;0;638928527740781302;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;2;0;Specular Highlights;2;0;Environment Reflections;2;0;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;  XR Motion Vectors;0;0;GPU Instancing;1;0;LOD CrossFade;1;638937903544166478;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position;1;0;Debug Display;1;0;Clear Coat;0;0;0;12;False;True;True;True;True;True;True;True;True;True;True;False;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4427;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4428;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4429;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4430;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4431;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4432;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4433;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4434;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4437;0,100;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4438;0,100;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;XRMotionVectors;0;11;XRMotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;True;1;False;;255;False;;1;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;1;LightMode=XRMotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;4020;0;4005;0
WireConnection;4020;1;4301;0
WireConnection;4020;2;4018;0
WireConnection;4020;3;4436;583
WireConnection;4020;4;4358;0
WireConnection;4373;1;4436;0
WireConnection;4373;0;4385;1007
WireConnection;4381;1;4385;0
WireConnection;4426;0;4373;0
WireConnection;4426;1;4436;180
WireConnection;4426;9;4436;186
WireConnection;4426;4;4436;182
WireConnection;4426;5;4436;183
WireConnection;4426;2;4020;0
WireConnection;4426;6;4436;184
WireConnection;4426;7;4435;0
WireConnection;4426;8;4381;0
WireConnection;4426;10;4436;188
ASEEND*/
//CHKSM=889C0E6638C8D1A4FF7EA57EBBB17E6BD821DBFB