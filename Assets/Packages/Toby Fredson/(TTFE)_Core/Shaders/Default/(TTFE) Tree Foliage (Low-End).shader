// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Foliage (Low-End)"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[TTFE_DrawerTitle] _TTFETREEFOLIAGESHADERLOWEND( "(TTFE) TREE FOLIAGE SHADER (LOW-END)", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _FACERENDERING( "FACE RENDERING", Float ) = 0
		[Space (10)] _AlphaClip( "Alpha Clip", Float ) = 0.4
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS( "TEXTURE MAPS", Float ) = 0
		[NoScaleOffset][Space (10)][TTFE_Drawer_SingleLineTexture] _AlbedoMap( "Albedo Map", 2D ) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture] _NormalMap( "Normal Map", 2D ) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _MaskMapRGBA( "Mask Map *RGB(A)", 2D ) = "white" {}
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTURESETTINGS( "TEXTURE SETTINGS", Float ) = 0
		[Space (10)][Toggle] _NormalBackFaceFixBranch( "Normal Back Face Fix (Branch)", Float ) = 0
		[Header((Self Shading))] _VertexLighting( "Vertex Lighting", Float ) = 3
		_VertexShadow( "Vertex Shadow", Float ) = 0
		_VertexAo( "Vertex Ao", Range( 0, 1 ) ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS1( "WIND SETTINGS", Float ) = 0
		[Header((Wind Texture Maps))][NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)] _WindNoiseMap( "Wind Noise Map", 2D ) = "white" {}
		[Header((Pivot))] _GrassSwayPowerGentleWind( "Grass Sway Power (Gentle Wind)", Float ) = 1
		[HideInInspector][Header((Interaction))] _BendRadius( "Bend Radius", Float ) = 1
		[Header((Trunk and Branch))] _PivotRandomnessStrength( "Pivot Randomness Strength", Float ) = 0.5
		_PivotRandomness( "Pivot Randomness ", Float ) = 1
		[KeywordEnum( GentleBreeze,StrongWind )] _WindType( "Wind Type", Float ) = 1
		[HideInInspector] _BendAmountGrass( "Bend Amount Grass", Float ) = 1
		_BranchWindLarge( "Branch Wind Large", Range( 0, 5 ) ) = 1
		_BranchWindSmall( "Branch Wind Small", Range( 0, 5 ) ) = 1
		_BranchSwayPower( "Branch Sway Power", Float ) = 1
		[HideInInspector] _BendDirection( "Bend Direction", Vector ) = ( 1, 0.5, 1, 0 )
		_BranchFoldStrength( "Branch Fold Strength", Float ) = 0.5
		[Header((Leaf Noise))] _MotionFlutterIntensity( "Motion Flutter Intensity", Range( 0, 1 ) ) = 1
		_FlutterSize( "Flutter Size", Float ) = 10
		_FlutterPower( "Flutter Power", Float ) = 1
		[HideInInspector] _BendAmount( "_BendAmount", Float ) = 0
		[HideInInspector] _WindFrequency( "_WindFrequency", Float ) = 1
		[HideInInspector] _WindAmplitude( "_WindAmplitude", Float ) = 0
		_MotionBendingGentleRandom( "Motion Bending Gentle Random", Float ) = 0.1
		_DownwardStrength( "Downward Strength", Float ) = -0.5
		[HideInInspector] _BendAxis( "_BendAxis", Vector ) = ( 0, 0, 1, 0 )
		[Toggle] _LeafInteraction( "Leaf Interaction", Float ) = 0
		[Toggle] _PhysicsInteraction( "Physics Interaction", Float ) = 0
		[Toggle] _PhysicsWind( "Physics Wind", Float ) = 0
		[HideInInspector] _HeightMax( "_HeightMax", Float ) = 1
		[HideInInspector] _WindAxis( "_WindAxis", Vector ) = ( 0, 0, 1, 0 )
		[TTFE_DrawerFeatureBorder][Space (10)] _WINDMASKSETTINGS1( "WIND MASK SETTINGS", Float ) = 0
		[Header((Trunk Mask))] _TrunkHeightandThickness( "Trunk Height and Thickness", Float ) = 0.01
		[Toggle] _MaskRoots( "Mask Roots", Float ) = 1
		[Toggle] _MaskRootsAuto( "Mask Roots (Auto)", Float ) = 0
		[Toggle] _CenterofMass( "Center of Mass", Float ) = 0
		[Header((Spherical Mask))] _Radius( "Radius", Float ) = 2
		_Hardness( "Hardness", Float ) = 1
		[Header((Branch Mask))] _BranchMaskScale( "Branch Mask Scale", Float ) = 0.1
		_BranchMaskRadious( "Branch Mask Radius", Float ) = 0.5
		[Toggle] _SkipBranchWindCloth( "Skip Branch Wind (Cloth)", Float ) = 0
		[Header((Roots Mask))] _RootsRadius( "Roots Radius", Float ) = 2
		_RootsHardness( "Roots Hardness", Float ) = 1
		_RootsPosition( "Roots Position", Float ) = 0
		[Header((Vertex Color Mask))] _VertexColorPower( "Vertex Color Power", Float ) = 1
		[Toggle] _SwitchVGreenToRGBA( "Switch VGreen To RGBA", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _DEBUGWINDMASK( "DEBUG WIND MASK", Float ) = 0
		[Space (10)][Toggle( _DEBUGVISUALIZEWINDMASK_ON )] _DEBUGVisualizeWindMask( "[DEBUG] Visualize Wind Mask", Float ) = 0
		[Toggle] _DEBUGComputeVertexColors( "[DEBUG] Compute Vertex Colors", Float ) = 0
		[Toggle] _DEBUGVertexR( "[DEBUG] VertexR", Float ) = 0
		[Toggle] _DEBUGVertexG( "[DEBUG] VertexG", Float ) = 0
		[Toggle] _DEBUGVertexB( "[DEBUG] VertexB", Float ) = 0
		[Toggle] _DEBUGVertexA( "[DEBUG] VertexA", Float ) = 0
		[Toggle] _DEBUGVertexRGBA( "[DEBUG] VertexRGBA", Float ) = 0
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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
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
				float4 ase_color : COLOR;
				float4 ase_texcoord8 : TEXCOORD8;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			sampler2D _NormalMap;
			sampler2D _MaskMapRGBA;


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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord8 = input.positionOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord7.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				float saferPower3392 = abs( input.ase_color.r );
				float3 temp_output_3381_0 = ( ( input.ase_texcoord8.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult3382 = dot( temp_output_3381_0 , temp_output_3381_0 );
				float saferPower3385 = abs( saturate( dotResult3382 ) );
				float3 normalizeResult3396 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float SelfShading3438 = saturate( ( saturate( pow( saferPower3392 , _VertexAo ) ) * (( pow( saferPower3385 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult3396 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 color2802_g2284 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_1635_0_g2284 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord8.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord8).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float temp_output_2764_0_g2284 = ( SphericalMaskProxySphere1924_g2284 * BranchMask2026_g2284 );
				float4 color2776_g2284 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2767_g2284 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.ase_texcoord8.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float4 lerpResult2804_g2284 = lerp( color2802_g2284 , ( ( temp_output_2764_0_g2284 * color2776_g2284 ) + ( saturate( ( 1.0 - temp_output_2764_0_g2284 ) ) * color2767_g2284 ) ) , RootsMask_ProxySphere2794_g2284);
				float4 DEBUGVisualizeWindMask2775_g2284 = lerpResult2804_g2284;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch3024 = DEBUGVisualizeWindMask2775_g2284;
				#else
				float4 staticSwitch3024 = ( tex2DNode3356 * (SelfShading3438*_VertexLighting + _VertexShadow) );
				#endif
				float4 color2804 = IsGammaSpace() ? float4( 1, 0, 0, 1 ) : float4( 1, 0, 0, 1 );
				float4 color2805 = IsGammaSpace() ? float4( 0.1020105, 1, 0, 0 ) : float4( 0.01033768, 1, 0, 0 );
				float4 color2806 = IsGammaSpace() ? float4( 0, 0.09082031, 1, 0 ) : float4( 0, 0.008656799, 1, 0 );
				
				float2 uv_NormalMap3357 = input.ase_texcoord7.xy;
				float3 unpack3357 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap3357 ), 1.0 );
				unpack3357.z = lerp( 1, unpack3357.z, saturate(1.0) );
				float3 tex2DNode3357 = unpack3357;
				float3 switchResult3468 = (((ase_vface>0)?(tex2DNode3357):(-tex2DNode3357)));
				
				float3 temp_cast_1 = (0.02).xxx;
				
				float2 uv_MaskMapRGBA3358 = input.ase_texcoord7.xy;
				float4 tex2DNode3358 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA3358 );
				
				float3 temp_cast_2 = (( _TTFETREEFOLIAGESHADERLOWEND + _FACERENDERING + _DEBUGWINDMASK + _ADVANCEDSETTINGS + _TEXTUREMAPS + _TEXTURESETTINGS )).xxx;
				

				float3 BaseColor = (( _DEBUGComputeVertexColors )?( ( (( _DEBUGVertexR )?( ( color2804 * input.ase_color.r ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexG )?( ( color2805 * input.ase_color.g ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexB )?( ( color2806 * input.ase_color.b ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexRGBA )?( input.ase_color ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexA )?( input.ase_color.a ):( 0.0 )) ) ):( staticSwitch3024 )).rgb;
				float3 Normal = (( _NormalBackFaceFixBranch )?( switchResult3468 ):( tex2DNode3357 ));
				float3 Specular = temp_cast_1;
				float Metallic = 0;
				float Smoothness = tex2DNode3358.a;
				float Occlusion = tex2DNode3358.g;
				float3 Emission = temp_cast_2;
				float Alpha = tex2DNode3356.a;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND


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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord1.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				

				float Alpha = tex2DNode3356.a;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND


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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord1.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				

				float Alpha = tex2DNode3356.a;
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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
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
				float4 ase_color : COLOR;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord3.xy = input.texcoord0.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord4 = input.positionOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord3.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				float saferPower3392 = abs( input.ase_color.r );
				float3 temp_output_3381_0 = ( ( input.ase_texcoord4.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult3382 = dot( temp_output_3381_0 , temp_output_3381_0 );
				float saferPower3385 = abs( saturate( dotResult3382 ) );
				float3 normalizeResult3396 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float SelfShading3438 = saturate( ( saturate( pow( saferPower3392 , _VertexAo ) ) * (( pow( saferPower3385 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult3396 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 color2802_g2284 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_1635_0_g2284 = ( ( input.ase_texcoord4.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord4.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord4).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float temp_output_2764_0_g2284 = ( SphericalMaskProxySphere1924_g2284 * BranchMask2026_g2284 );
				float4 color2776_g2284 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2767_g2284 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.ase_texcoord4.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float4 lerpResult2804_g2284 = lerp( color2802_g2284 , ( ( temp_output_2764_0_g2284 * color2776_g2284 ) + ( saturate( ( 1.0 - temp_output_2764_0_g2284 ) ) * color2767_g2284 ) ) , RootsMask_ProxySphere2794_g2284);
				float4 DEBUGVisualizeWindMask2775_g2284 = lerpResult2804_g2284;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch3024 = DEBUGVisualizeWindMask2775_g2284;
				#else
				float4 staticSwitch3024 = ( tex2DNode3356 * (SelfShading3438*_VertexLighting + _VertexShadow) );
				#endif
				float4 color2804 = IsGammaSpace() ? float4( 1, 0, 0, 1 ) : float4( 1, 0, 0, 1 );
				float4 color2805 = IsGammaSpace() ? float4( 0.1020105, 1, 0, 0 ) : float4( 0.01033768, 1, 0, 0 );
				float4 color2806 = IsGammaSpace() ? float4( 0, 0.09082031, 1, 0 ) : float4( 0, 0.008656799, 1, 0 );
				
				float3 temp_cast_1 = (( _TTFETREEFOLIAGESHADERLOWEND + _FACERENDERING + _DEBUGWINDMASK + _ADVANCEDSETTINGS + _TEXTUREMAPS + _TEXTURESETTINGS )).xxx;
				

				float3 BaseColor = (( _DEBUGComputeVertexColors )?( ( (( _DEBUGVertexR )?( ( color2804 * input.ase_color.r ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexG )?( ( color2805 * input.ase_color.g ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexB )?( ( color2806 * input.ase_color.b ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexRGBA )?( input.ase_color ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexA )?( input.ase_color.a ):( 0.0 )) ) ):( staticSwitch3024 )).rgb;
				float3 Emission = temp_cast_1;
				float Alpha = tex2DNode3356.a;
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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature _DEBUGVISUALIZEWINDMASK_ON


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
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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
			
			float4 ASESafeNormalize(float4 inVec)
			{
				float dp3 = max(1.175494351e-38, dot(inVec, inVec));
				return inVec* rsqrt(dp3);
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord2 = input.positionOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord1.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				float saferPower3392 = abs( input.ase_color.r );
				float3 temp_output_3381_0 = ( ( input.ase_texcoord2.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult3382 = dot( temp_output_3381_0 , temp_output_3381_0 );
				float saferPower3385 = abs( saturate( dotResult3382 ) );
				float3 normalizeResult3396 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float SelfShading3438 = saturate( ( saturate( pow( saferPower3392 , _VertexAo ) ) * (( pow( saferPower3385 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult3396 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 color2802_g2284 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_1635_0_g2284 = ( ( input.ase_texcoord2.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord2.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord2).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float temp_output_2764_0_g2284 = ( SphericalMaskProxySphere1924_g2284 * BranchMask2026_g2284 );
				float4 color2776_g2284 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2767_g2284 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.ase_texcoord2.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float4 lerpResult2804_g2284 = lerp( color2802_g2284 , ( ( temp_output_2764_0_g2284 * color2776_g2284 ) + ( saturate( ( 1.0 - temp_output_2764_0_g2284 ) ) * color2767_g2284 ) ) , RootsMask_ProxySphere2794_g2284);
				float4 DEBUGVisualizeWindMask2775_g2284 = lerpResult2804_g2284;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch3024 = DEBUGVisualizeWindMask2775_g2284;
				#else
				float4 staticSwitch3024 = ( tex2DNode3356 * (SelfShading3438*_VertexLighting + _VertexShadow) );
				#endif
				float4 color2804 = IsGammaSpace() ? float4( 1, 0, 0, 1 ) : float4( 1, 0, 0, 1 );
				float4 color2805 = IsGammaSpace() ? float4( 0.1020105, 1, 0, 0 ) : float4( 0.01033768, 1, 0, 0 );
				float4 color2806 = IsGammaSpace() ? float4( 0, 0.09082031, 1, 0 ) : float4( 0, 0.008656799, 1, 0 );
				

				float3 BaseColor = (( _DEBUGComputeVertexColors )?( ( (( _DEBUGVertexR )?( ( color2804 * input.ase_color.r ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexG )?( ( color2805 * input.ase_color.g ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexB )?( ( color2806 * input.ase_color.b ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexRGBA )?( input.ase_color ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexA )?( input.ase_color.a ):( 0.0 )) ) ):( staticSwitch3024 )).rgb;
				float Alpha = tex2DNode3356.a;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND


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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord3.xy = input.texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_NormalMap3357 = input.ase_texcoord3.xy;
				float3 unpack3357 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap3357 ), 1.0 );
				unpack3357.z = lerp( 1, unpack3357.z, saturate(1.0) );
				float3 tex2DNode3357 = unpack3357;
				float3 switchResult3468 = (((ase_vface>0)?(tex2DNode3357):(-tex2DNode3357)));
				
				float2 uv_AlbedoMap3356 = input.ase_texcoord3.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				

				float3 Normal = (( _NormalBackFaceFixBranch )?( switchResult3468 ):( tex2DNode3357 ));
				float Alpha = tex2DNode3356.a;
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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
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
				float4 ase_color : COLOR;
				float4 ase_texcoord8 : TEXCOORD8;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
			sampler2D _AlbedoMap;
			sampler2D _NormalMap;
			sampler2D _MaskMapRGBA;


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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord8 = input.positionOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
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

				float2 uv_AlbedoMap3356 = input.ase_texcoord7.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				float saferPower3392 = abs( input.ase_color.r );
				float3 temp_output_3381_0 = ( ( input.ase_texcoord8.xyz * float3( 2,1.3,2 ) ) / 25.0 );
				float dotResult3382 = dot( temp_output_3381_0 , temp_output_3381_0 );
				float saferPower3385 = abs( saturate( dotResult3382 ) );
				float3 normalizeResult3396 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float SelfShading3438 = saturate( ( saturate( pow( saferPower3392 , _VertexAo ) ) * (( pow( saferPower3385 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult3396 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) ) );
				float4 color2802_g2284 = IsGammaSpace() ? float4( 0.1175598, 0.3113208, 0, 0 ) : float4( 0.01296729, 0.07896997, 0, 0 );
				float3 temp_output_1635_0_g2284 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.ase_texcoord8.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.ase_texcoord8).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float temp_output_2764_0_g2284 = ( SphericalMaskProxySphere1924_g2284 * BranchMask2026_g2284 );
				float4 color2776_g2284 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color2767_g2284 = IsGammaSpace() ? float4( 0.01265267, 0, 0.5377358, 0 ) : float4( 0.0009793089, 0, 0.2506461, 0 );
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.ase_texcoord8.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float4 lerpResult2804_g2284 = lerp( color2802_g2284 , ( ( temp_output_2764_0_g2284 * color2776_g2284 ) + ( saturate( ( 1.0 - temp_output_2764_0_g2284 ) ) * color2767_g2284 ) ) , RootsMask_ProxySphere2794_g2284);
				float4 DEBUGVisualizeWindMask2775_g2284 = lerpResult2804_g2284;
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch3024 = DEBUGVisualizeWindMask2775_g2284;
				#else
				float4 staticSwitch3024 = ( tex2DNode3356 * (SelfShading3438*_VertexLighting + _VertexShadow) );
				#endif
				float4 color2804 = IsGammaSpace() ? float4( 1, 0, 0, 1 ) : float4( 1, 0, 0, 1 );
				float4 color2805 = IsGammaSpace() ? float4( 0.1020105, 1, 0, 0 ) : float4( 0.01033768, 1, 0, 0 );
				float4 color2806 = IsGammaSpace() ? float4( 0, 0.09082031, 1, 0 ) : float4( 0, 0.008656799, 1, 0 );
				
				float2 uv_NormalMap3357 = input.ase_texcoord7.xy;
				float3 unpack3357 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap3357 ), 1.0 );
				unpack3357.z = lerp( 1, unpack3357.z, saturate(1.0) );
				float3 tex2DNode3357 = unpack3357;
				float3 switchResult3468 = (((ase_vface>0)?(tex2DNode3357):(-tex2DNode3357)));
				
				float3 temp_cast_1 = (0.02).xxx;
				
				float2 uv_MaskMapRGBA3358 = input.ase_texcoord7.xy;
				float4 tex2DNode3358 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA3358 );
				
				float3 temp_cast_2 = (( _TTFETREEFOLIAGESHADERLOWEND + _FACERENDERING + _DEBUGWINDMASK + _ADVANCEDSETTINGS + _TEXTUREMAPS + _TEXTURESETTINGS )).xxx;
				

				float3 BaseColor = (( _DEBUGComputeVertexColors )?( ( (( _DEBUGVertexR )?( ( color2804 * input.ase_color.r ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexG )?( ( color2805 * input.ase_color.g ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexB )?( ( color2806 * input.ase_color.b ) ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexRGBA )?( input.ase_color ):( float4( 0,0,0,0 ) )) + (( _DEBUGVertexA )?( input.ase_color.a ):( 0.0 )) ) ):( staticSwitch3024 )).rgb;
				float3 Normal = (( _NormalBackFaceFixBranch )?( switchResult3468 ):( tex2DNode3357 ));
				float3 Specular = temp_cast_1;
				float Metallic = 0;
				float Smoothness = tex2DNode3358.a;
				float Occlusion = tex2DNode3358.g;
				float3 Emission = temp_cast_2;
				float Alpha = tex2DNode3356.a;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND


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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

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

				float2 uv_AlbedoMap3356 = input.ase_texcoord1.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				

				surfaceDescription.Alpha = tex2DNode3356.a;
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
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma shader_feature _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND


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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
			sampler2D _WindNoiseMap;
			float _Disturbance;
			float3 _PlayerPosition;
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

				float GlobalVar_WindStrength2401_g2284 = _GlobalWindStrength;
				float mulTime1797_g2284 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1669_g2284 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1703_g2284 = dotResult1669_g2284;
				float dotResult1671_g2284 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1704_g2284 = dotResult1671_g2284;
				float mulTime1796_g2284 = _TimeParameters.x * 4.3;
				float dotResult1670_g2284 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1705_g2284 = dotResult1670_g2284;
				float3 normalizeResult1764_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMassTrunkUP1996_g2284 = saturate( (distance( normalizeResult1764_g2284 , float3( 0, 1, 0 ) )*1.0 + -0.05) );
				float3 temp_output_1635_0_g2284 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius );
				float dotResult1653_g2284 = dot( temp_output_1635_0_g2284 , temp_output_1635_0_g2284 );
				float saferPower1713_g2284 = abs( saturate( dotResult1653_g2284 ) );
				float temp_output_1761_0_g2284 = ( (( _MaskRootsAuto )?( saturate( input.positionOS.xyz.y ) ):( 1.0 )) * pow( saferPower1713_g2284 , _Hardness ) );
				float3 normalizeResult1623_g2284 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass1717_g2284 = saturate( (distance( normalizeResult1623_g2284 , float3( 0, 1, 0 ) )*2.0 + 0.0) );
				float SphericalMaskProxySphere1924_g2284 = (( _CenterofMass )?( ( temp_output_1761_0_g2284 * CenterOfMass1717_g2284 ) ):( temp_output_1761_0_g2284 ));
				float saferPower2686_g2284 = abs( saturate( ( -100.0 / input.positionOS.xyz.y ) ) );
				float MaskRoots2691_g2284 = (( _MaskRoots )?( saturate( ( 1.0 - pow( saferPower2686_g2284 , 0.1 ) ) ) ):( 1.0 ));
				float3 break2782_g2284 = float3( 0, -1, 0 );
				float3 appendResult2785_g2284 = (float3(break2782_g2284.x , ( break2782_g2284.y * _RootsPosition ) , break2782_g2284.z));
				float3 temp_output_2789_0_g2284 = ( ( input.positionOS.xyz - appendResult2785_g2284 ) / _RootsRadius );
				float dotResult2790_g2284 = dot( temp_output_2789_0_g2284 , temp_output_2789_0_g2284 );
				float saferPower2793_g2284 = abs( saturate( dotResult2790_g2284 ) );
				float RootsMask_ProxySphere2794_g2284 = pow( saferPower2793_g2284 , _RootsHardness );
				float smoothstepResult1959_g2284 = smoothstep( _BranchMaskScale , _BranchMaskRadious , saturate(  (0.0 + ( length( (input.positionOS).xzw ) - 0.0 ) * ( 1.0 - 0.0 ) / ( 10.0 - 0.0 ) ) ));
				float BranchMask2026_g2284 = smoothstepResult1959_g2284;
				float3 rotatedValue2147_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1797_g2284 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1703_g2284 ) + ( RandomSeedVector_D1704_g2284 * 0.02 ) ) ) + sin( ( mulTime1796_g2284 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1705_g2284 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * CenterOfMassTrunkUP1996_g2284 * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * BranchMask2026_g2284 ).x );
				float3 appendResult2083_g2284 = (float3(0.0 , 0.0 , saturate( input.positionOS.xyz ).z));
				float3 break1775_g2284 = input.positionOS.xyz;
				float3 appendResult1885_g2284 = (float3(break1775_g2284.x , ( break1775_g2284.y * 0.15 ) , 0.0));
				float mulTime1956_g2284 = _TimeParameters.x * 2.1;
				float dotResult1831_g2284 = dot( ase_objectPosition , float3( 75.09, 24.54, 63.12 ) );
				float RandomSeedVector_K1889_g2284 = dotResult1831_g2284;
				float dotResult1836_g2284 = dot( ase_objectPosition , float3( 45.3, 35.05, 58.9 ) );
				float RandomSeedVector_O1892_g2284 = dotResult1836_g2284;
				float3 temp_cast_1 = (input.positionOS.xyz.y).xxx;
				float2 appendResult1604_g2284 = (float2(input.positionOS.xyz.x , input.positionOS.xyz.z));
				float3 temp_cast_3 = (input.positionOS.xyz.x).xxx;
				float3 smoothstepResult1652_g2284 = smoothstep( float3( 0,0,0 ) , float3( 1,1,1 ) , saturate( ( cross( temp_cast_1 , float3( appendResult1604_g2284 ,  0.0 ) ) + cross( temp_cast_3 , float3( appendResult1604_g2284 ,  0.0 ) ) ) ));
				float3 RandomIDBranchPositionMask1760_g2284 = saturate( ( smoothstepResult1652_g2284 * 0.5 ) );
				float3 appendResult2078_g2284 = (float3(0.0 , input.positionOS.xyz.y , 0.0));
				float3 break1772_g2284 = input.positionOS.xyz;
				float3 appendResult1880_g2284 = (float3(break1772_g2284.x , 0.0 , ( break1772_g2284.z * 0.15 )));
				float mulTime1949_g2284 = _TimeParameters.x * 2.3;
				float dotResult1754_g2284 = dot( ase_objectPosition , float3( 12.34, 56.78, 90.12 ) );
				float RandomSeedVector_A1810_g2284 = dotResult1754_g2284;
				float dotResult1640_g2284 = dot( ase_objectPosition , float3( 39.4, 97.33, 55.12 ) );
				float RandomSeedVector_G1666_g2284 = dotResult1640_g2284;
				float3 appendResult2019_g2284 = (float3(input.positionOS.xyz.x , 0.0 , 0.0));
				float3 break1728_g2284 = input.positionOS.xyz;
				float3 appendResult1828_g2284 = (float3(0.0 , ( break1728_g2284.y * 0.2 ) , ( break1728_g2284.z * 0.4 )));
				float mulTime1882_g2284 = _TimeParameters.x * 2.0;
				float dotResult1832_g2284 = dot( ase_objectPosition , float3( 63.47, 32.7, 12.05 ) );
				float RandomSeedVector_J1888_g2284 = dotResult1832_g2284;
				float dotResult1835_g2284 = dot( ase_objectPosition , float3( 35.35, 68.4, 30.24 ) );
				float RandomSeedVector_N1891_g2284 = dotResult1835_g2284;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float3 normalizeResult2066_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime2067_g2284 = _TimeParameters.x * 0.25;
				float simplePerlin2D3191_g2284 = snoise( ( normalizeResult2066_g2284 + mulTime2067_g2284 ).xy*0.43 );
				float WindMask_LargeB2270_g2284 = ( simplePerlin2D3191_g2284 * 1.5 );
				float mulTime1940_g2284 = _TimeParameters.x * 3.2;
				float3 temp_output_2070_0_g2284 = ( ( mulTime1940_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_K1889_g2284 ) + float3( 0.4, 0.3, 0.1 ) + ( input.positionOS.xyz.x * 0.02 ) + ( 0.14 * input.positionOS.xyz.y ) + ( input.positionOS.xyz.z * 0.16 ) );
				float dotResult1893_g2284 = dot( (input.positionOS.xyz*0.02 + 0.0) , input.positionOS.xyz );
				float CeneterOfMassThickness_Mask2025_g2284 = saturate( dotResult1893_g2284 );
				float mulTime1937_g2284 = _TimeParameters.x * 2.3;
				float dotResult1833_g2284 = dot( ase_objectPosition , float3( 51.59, 33.79, 38.54 ) );
				float RandomSeedVector_L1890_g2284 = dotResult1833_g2284;
				float dotResult1834_g2284 = dot( ase_objectPosition , float3( 14.19, 63.9, 24.6 ) );
				float RandomSeedVector_M1887_g2284 = dotResult1834_g2284;
				float3 temp_output_2073_0_g2284 = ( ( mulTime1937_g2284 + RandomSeedVector_L1890_g2284 + RandomSeedVector_M1887_g2284 ) + ( 0.2 * input.positionOS.xyz ) + float3( 0.4, 0.3, 0.1 ) );
				float mulTime1934_g2284 = _TimeParameters.x * 3.6;
				float temp_output_2076_0_g2284 = ( ( mulTime1934_g2284 + RandomSeedVector_N1891_g2284 + RandomSeedVector_O1892_g2284 ) + ( 0.2 * input.positionOS.xyz.x ) );
				float3 normalizeResult1817_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1818_g2284 = _TimeParameters.x * 0.26;
				float simplePerlin2D1923_g2284 = snoise( ( normalizeResult1817_g2284 + mulTime1818_g2284 ).xy*0.7 );
				float WindMask_LargeC2062_g2284 = ( simplePerlin2D1923_g2284 * 1.5 );
				float3 PIWI_02Gentle2481_g2284 = ( (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) )) * 0.4 );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 appendResult1711_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPisitionXY_Out1759_g2284 = appendResult1711_g2284;
				float4 WindDirection1609_g2284 = _WindDirection;
				float GlobalVar_WindSpeed1633_g2284 = _StrongWindSpeed;
				float2 NoiseRotation_Output1710_g2284 = ( -(WindDirection1609_g2284).xz * _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 );
				float2 WPRG2D1990_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + NoiseRotation_Output1710_g2284 );
				float simpleNoise2607_g2284 = SimpleNoise( WPRG2D1990_g2284 );
				float3 break2587_g2284 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult2598_g2284 = (float3(break2587_g2284.x , ( break2587_g2284.y * 0.3 ) , break2587_g2284.z));
				float3 smoothstepResult2606_g2284 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult2598_g2284);
				float smoothstepResult1925_g2284 = smoothstep( 0.0 , _VertexColorPower , input.ase_color.g);
				float4 temp_cast_9 = (saturate( smoothstepResult1925_g2284 )).xxxx;
				float4 LeafVertexColor_Main2117_g2284 = (( _SwitchVGreenToRGBA )?( input.ase_color ):( temp_cast_9 ));
				float3 worldToObjDir2632_g2284 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise2607_g2284*2.2 + -1.05) * float4( ( smoothstepResult2606_g2284 * 0.2 ) , 0.0 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) * LeafVertexColor_Main2117_g2284 ).rgb, 0.0 ) ).xyz;
				float3 NormaliseRotationAxis2409_g2284 = float3( 0, 1, 0 );
				float simplePerlin2D2430_g2284 = snoise( WPRG2D1990_g2284*0.6 );
				simplePerlin2D2430_g2284 = simplePerlin2D2430_g2284*0.5 + 0.5;
				float NoiseLarge2431_g2284 = simplePerlin2D2430_g2284;
				float mulTime2580_g2284 = _TimeParameters.x * 2.0;
				float3 rotatedValue2609_g2284 = RotateAroundAxis( float3( 0,0,0 ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime2580_g2284 ) ) * _GrassSwayPowerGentleWind * saturate( input.positionOS.xyz.y ) ), NormaliseRotationAxis2409_g2284, NoiseLarge2431_g2284 );
				float3 worldToObjDir2623_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1609_g2284 * float4( rotatedValue2609_g2284 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D2557_g2284 = snoise( WPRG2D1990_g2284*0.1 );
				float MaskRotation2559_g2284 = saturate( simplePerlin2D2557_g2284 );
				float3 temp_output_2605_0_g2284 = ( input.positionOS.xyz * 0.25 );
				float3 rotatedValue2610_g2284 = RotateAroundAxis( float3( 0,0,0 ), temp_output_2605_0_g2284, NormaliseRotationAxis2409_g2284, ( saturate( input.positionOS.xyz.y ) * NoiseLarge2431_g2284 ) );
				float3 worldToObjDir2628_g2284 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue2610_g2284 - temp_output_2605_0_g2284 ), 0.0 ) ).xyz;
				float4 GentleNoise2639_g2284 = ( float4( ( ase_objectScale * worldToObjDir2632_g2284 ) , 0.0 ) + ( ( float4( worldToObjDir2623_g2284 , 0.0 ) * MaskRotation2559_g2284 * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 ) + ( MaskRotation2559_g2284 * float4( worldToObjDir2628_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) );
				float4 normalizeResult2396_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 break2391_g2284 = (normalizeResult2396_g2284).xyz;
				float4 appendResult2387_g2284 = (float4(break2391_g2284.x , ( break2391_g2284.y + _DownwardStrength ) , break2391_g2284.z , 0.0));
				float temp_output_2645_0_g2284 = saturate( input.positionOS.xyz.y );
				float4 WindMotion_BaseG2644_g2284 = ( appendResult2387_g2284 * temp_output_2645_0_g2284 );
				float2 WPRG2D_S41918_g2284 = ( BasicWorldPisitionXY_Out1759_g2284 + ( NoiseRotation_Output1710_g2284 * 4.0 ) );
				float simplePerlin2D2394_g2284 = snoise( WPRG2D_S41918_g2284*0.2 );
				simplePerlin2D2394_g2284 = simplePerlin2D2394_g2284*0.5 + 0.5;
				float WindMotionNoise2407_g2284 = simplePerlin2D2394_g2284;
				float saferPower1873_g2284 = abs( saturate( ( _TrunkHeightandThickness / input.positionOS.xyz.y ) ) );
				float smoothstepResult1999_g2284 = smoothstep( 0.2 , 0.8 , ( 1.0 - pow( saferPower1873_g2284 , 0.1 ) ));
				float TrunkHeightMask2118_g2284 = saturate( smoothstepResult1999_g2284 );
				float4 MotionBendingGentleRandom2426_g2284 = ( WindMotion_BaseG2644_g2284 * _MotionBendingGentleRandom * WindMotionNoise2407_g2284 * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float GlobalVar_WindMotion1991_g2284 = _WindMotion;
				float3 worldToObjDir2379_g2284 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_BaseG2644_g2284 *  (0.0 + ( GlobalVar_WindMotion1991_g2284 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise2407_g2284 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind2427_g2284 = ( worldToObjDir2379_g2284 * ase_objectScale * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 );
				float4 MotionBendingGentleWind22811_g2284 = ( float4( worldToObjDir2379_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 );
				float dotResult1755_g2284 = dot( ase_objectPosition , float3( 34.56, 78.9, 12.34 ) );
				float RandomSeedVector_B1811_g2284 = dotResult1755_g2284;
				float3 appendResult2093_g2284 = (float3(( sin( ( RandomSeedVector_A1810_g2284 + _TimeParameters.x ) ) * _PivotRandomnessStrength ) , 0.0 , ( sin( ( _TimeParameters.x + RandomSeedVector_B1811_g2284 ) ) * _PivotRandomnessStrength )));
				float dotResult1767_g2284 = dot( ase_objectPosition , float3( 78.9, 12.34, 56.78 ) );
				float RandomSeedVector_C1819_g2284 = dotResult1767_g2284;
				float4 normalizeResult2158_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2240_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( appendResult2093_g2284 * ( sin( ( _TimeParameters.x + RandomSeedVector_C1819_g2284 ) ) * _PivotRandomness ) ) , 0.0 ) * normalizeResult2158_g2284 * input.positionOS.xyz.y * TrunkHeightMask2118_g2284 ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2279_0_g2284 = ( worldToObjDir2240_g2284 * ase_objectScale * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 * saturate( input.positionOS.xyz.y ) );
				float mulTime1748_g2284 = _TimeParameters.x * 4.0;
				float dotResult1641_g2284 = dot( ase_objectPosition , float3( 13.17, 65.8, 80.42 ) );
				float RandomSeedVector_H1667_g2284 = dotResult1641_g2284;
				float mulTime1749_g2284 = _TimeParameters.x * 5.2;
				float dotResult1642_g2284 = dot( ase_objectPosition , float3( 85.9, 12.56, 43.1 ) );
				float RandomSeedVector_I1668_g2284 = dotResult1642_g2284;
				float3 rotatedValue2089_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 0.6, 1, 0.1 ) ), ( ( ( cos( ( ( RandomSeedVector_G1666_g2284 * 0.02 ) + mulTime1748_g2284 + ( float3( 0.6, 1, 0.8 ) * 0.3 * RandomSeedVector_H1667_g2284 ) ) ) + sin( ( mulTime1749_g2284 + ( float3( 0.3, 0.4, 1 ) * RandomSeedVector_I1668_g2284 * 0.5 ) + ( input.positionOS.xyz * 0.2 ) ) ) ) * 0.1 ) * SphericalMaskProxySphere1924_g2284 * MaskRoots2691_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ).x );
				float3 worldToObjDir2238_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( rotatedValue2089_g2284 - input.positionOS.xyz ) , 0.0 ) * 1.5 * WindDirection1609_g2284 * saturate( input.positionOS.xyz.y ) ).xyz, 0.0 ) ).xyz;
				float3 temp_output_2302_0_g2284 = ( ( worldToObjDir2238_g2284 * ase_objectScale ) * 0.4 );
				float3 PIWI_01Gentle2815_g2284 = ( ( temp_output_2279_0_g2284 + temp_output_2302_0_g2284 ) * 0.2 );
				float dotResult2713_g2284 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float4 normalizeResult2727_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 worldToObjDir2730_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult2713_g2284 + _TimeParameters.x ) * GlobalVar_WindSpeed1633_g2284 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1991_g2284 * 0.45 ) ) * SphericalMaskProxySphere1924_g2284 * normalizeResult2727_g2284 * input.positionOS.xyz.y ).xyz, 0.0 ) ).xyz;
				float3 worldToObjDir2707_g2284 = mul( GetWorldToObjectMatrix(), float4( ( float4( ( ( ( sin( ( ( _TimeParameters.x * GlobalVar_WindSpeed1633_g2284 ) + saturate( ase_positionWS.x ) + RandomIDBranchPositionMask1760_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_C1819_g2284 ) ) + 1.0 ) * 0.1 ) * GlobalVar_WindMotion1991_g2284 * _BranchFoldStrength ) , 0.0 ) * WindDirection1609_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_012320_g2284 = ( ( worldToObjDir2730_g2284 * ase_objectScale * TrunkHeightMask2118_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( worldToObjDir2707_g2284 * ase_objectScale * input.positionOS.xyz.y * BranchMask2026_g2284 * MaskRoots2691_g2284 * SphericalMaskProxySphere1924_g2284 * TrunkHeightMask2118_g2284 * RootsMask_ProxySphere2794_g2284 ) + temp_output_2279_0_g2284 + temp_output_2302_0_g2284 );
				float3 PIWI_022322_g2284 = (( _SkipBranchWindCloth )?( float3( 0,0,0 ) ):( ( ( ( ( rotatedValue2147_g2284 - input.positionOS.xyz ) * _BranchSwayPower ) * 0.2 ) + ( ( ( ( ( ( appendResult2083_g2284 + ( appendResult1885_g2284 * cos( mulTime1956_g2284 ) ) + ( cross( float3( 1.2, 0.6, 1 ) , ( float3( 0.7, 1, 0.8 ) * appendResult1885_g2284 ) ) * sin( ( mulTime1956_g2284 + RandomSeedVector_K1889_g2284 + RandomSeedVector_O1892_g2284 ) ) ) ) * 0.1 ) * BranchMask2026_g2284 * RandomIDBranchPositionMask1760_g2284 ) + ( ( ( appendResult2078_g2284 + ( appendResult1880_g2284 * cos( ( mulTime1949_g2284 + RandomSeedVector_A1810_g2284 + RandomSeedVector_G1666_g2284 ) ) ) + ( cross( float3( 0.9, 1, 1.2 ) , ( float3( 1, 1, 1 ) * appendResult1880_g2284 ) ) * sin( ( mulTime1949_g2284 + RandomSeedVector_D1704_g2284 ) ) ) ) * BranchMask2026_g2284 ) * 0.1 ) + ( ( ( ( appendResult2019_g2284 + ( appendResult1828_g2284 * cos( mulTime1882_g2284 ) ) + ( cross( float3( 1.1, 1.3, 0.8 ) , ( float3( 1.4, 0.8, 1.1 ) * appendResult1828_g2284 ) ) * sin( ( mulTime1882_g2284 + RandomSeedVector_J1888_g2284 + RandomSeedVector_N1891_g2284 ) ) ) ) * 0.1 ) * RandomIDBranchPositionMask1760_g2284 * BranchMask2026_g2284 ) * 0.05 ) ) * 0.2 ) * _BranchWindLarge * WindMask_LargeB2270_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) + ( ( ( ( cos( temp_output_2070_0_g2284 ) * sin( temp_output_2070_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( cos( temp_output_2073_0_g2284 ) * sin( temp_output_2073_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) + ( ( sin( temp_output_2076_0_g2284 ) * cos( temp_output_2076_0_g2284 ) * ( BranchMask2026_g2284 * CeneterOfMassThickness_Mask2025_g2284 ) ) * 0.1 ) ) * _BranchWindSmall * WindMask_LargeC2062_g2284 * MaskRoots2691_g2284 * RootsMask_ProxySphere2794_g2284 ) ) ));
				float4 normalizeResult2050_g2284 = ASESafeNormalize( WindDirection1609_g2284 );
				float3 temp_output_1753_0_g2284 = ( ase_positionWS + ( _TimeParameters.x * ( 4.0 + GlobalVar_WindSpeed1633_g2284 ) ) );
				float simpleNoise1803_g2284 = SimpleNoise( temp_output_1753_0_g2284.xy*2.0 );
				simpleNoise1803_g2284 = simpleNoise1803_g2284*2 - 1;
				float simpleNoise1988_g2284 = SimpleNoise( ( ( RandomIDBranchPositionMask1760_g2284 + input.tangentOS.xyz + ( temp_output_1753_0_g2284 * 1.0 ) ) * 0.5 ).xy*2.0 );
				simpleNoise1988_g2284 = simpleNoise1988_g2284*2 - 1;
				float3 worldToObjDir2285_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( normalizeResult2050_g2284 * ( sin( simpleNoise1803_g2284 ) * 0.5 ) ) + float4( ( sin( simpleNoise1988_g2284 ) * float3( 0, 1, 0 ) ) , 0.0 ) ) * GlobalVar_WindMotion1991_g2284 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ).xyz, 0.0 ) ).xyz;
				float3 PIWI_032321_g2284 = ( worldToObjDir2285_g2284 * ase_objectScale );
				float mulTime1976_g2284 = _TimeParameters.x * 5.0;
				float4 PIWI_042319_g2284 = ( ( ( float4( ( sin( ( ( input.normalOS * _FlutterSize ) + ( mulTime1976_g2284 * GlobalVar_WindSpeed1633_g2284 ) ) ) * 0.05 ) , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 ) * _FlutterPower ) * _MotionFlutterIntensity );
				float3 normalizeResult1770_g2284 = ASESafeNormalize( ase_positionWS );
				float mulTime1771_g2284 = _TimeParameters.x * 0.2;
				float simplePerlin2D3189_g2284 = snoise( ( normalizeResult1770_g2284 + mulTime1771_g2284 ).xy*0.4 );
				float NoiseMask_LargeA2003_g2284 = ( simplePerlin2D3189_g2284 * 1.5 );
				float3 worldToObjDir2214_g2284 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41918_g2284 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA2003_g2284 * WindMask_LargeC2062_g2284 * float4( float3( -1, -0.8, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float4 PIWI_052323_g2284 = ( ( float4( worldToObjDir2214_g2284 , 0.0 ) * saturate( input.positionOS.xyz.y ) * LeafVertexColor_Main2117_g2284 * float4( ase_objectScale , 0.0 ) ) * 0.4 );
				float3 worldToObjDir2403_g2284 = mul( GetWorldToObjectMatrix(), float4( ( ( ( appendResult2387_g2284 * temp_output_2645_0_g2284 ) * GlobalVar_WindMotion1991_g2284 ) * simplePerlin2D2394_g2284 ).xyz, 0.0 ) ).xyz;
				float4 WindMotion_Output2425_g2284 = ( ( float4( worldToObjDir2403_g2284 , 0.0 ) * float4( ase_objectScale , 0.0 ) * LeafVertexColor_Main2117_g2284 ) * 0.4 );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2284 = ( float4( PIWI_02Gentle2481_g2284 , 0.0 ) + GentleNoise2639_g2284 + MotionBendingGentleRandom2426_g2284 + float4( MotionBendingGentleWind2427_g2284 , 0.0 ) + MotionBendingGentleWind22811_g2284 + float4( PIWI_01Gentle2815_g2284 , 0.0 ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#else
				float4 staticSwitch1496_g2284 =  (float4( 0,0,0,0 ) + ( ( float4( PIWI_012320_g2284 , 0.0 ) + float4( PIWI_022322_g2284 , 0.0 ) + float4( PIWI_032321_g2284 , 0.0 ) + PIWI_042319_g2284 + PIWI_052323_g2284 + WindMotion_Output2425_g2284 + _TEXTUREMAPS1 + _WINDMASKSETTINGS1 ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_2902_0_g2284 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_2900_0_g2284 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_2901_0_g2284 = saturate( input.positionOS.xyz.y );
				float mulTime2884_g2284 = _TimeParameters.x * 0.5;
				float2 appendResult2882_g2284 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D2891_g2284 = snoise( ( _TimeParameters.x + appendResult2882_g2284 ) );
				simplePerlin2D2891_g2284 = simplePerlin2D2891_g2284*0.5 + 0.5;
				float3 InteractionNoise2905_g2284 = ( ( sin( ( mulTime2884_g2284 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D2891_g2284 ) * 0.4 );
				float4 transform2915_g2284 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_2902_0_g2284 ) * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 * InteractionNoise2905_g2284 ) + ( ( temp_output_2902_0_g2284 * _BendAmountGrass * temp_output_2900_0_g2284 * temp_output_2901_0_g2284 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult3143_g2284 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 Interaction_Output2916_g2284 = ( transform2915_g2284 * saturate( smoothstepResult3143_g2284 ) );
				float temp_output_3165_0_g2284 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult3168_g2284 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue3175_g2284 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_3165_0_g2284 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult3168_g2284 ) ):( ( temp_output_3165_0_g2284 * normalizeResult3168_g2284 ) )).x );
				float3 PhysiscsInteraction3177_g2284 = (( _PhysicsInteraction )?( ( rotatedValue3175_g2284 - input.positionOS.xyz ) ):( float3( 0,0,0 ) ));
				float4 FinalWind_Output163_g2284 = ( ( GlobalVar_WindStrength2401_g2284 * staticSwitch1496_g2284 ) + (( _LeafInteraction )?( Interaction_Output2916_g2284 ):( float4( 0,0,0,0 ) )) + float4( PhysiscsInteraction3177_g2284 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch3028 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch3028 = FinalWind_Output163_g2284;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = staticSwitch3028.rgb;

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

				float2 uv_AlbedoMap3356 = input.ase_texcoord1.xy;
				float4 tex2DNode3356 = tex2D( _AlbedoMap, uv_AlbedoMap3356 );
				

				surfaceDescription.Alpha = tex2DNode3356.a;
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
			float3 _BendAxis;
			float3 _WindAxis;
			float3 _BendDirection;
			float _SkipBranchWindCloth;
			float _BendAmountGrass;
			float _BendRadius;
			float _PhysicsInteraction;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _WindAmplitude;
			float _DEBUGComputeVertexColors;
			float _VertexAo;
			float _VertexLighting;
			float _DEBUGVertexR;
			float _DEBUGVertexG;
			float _DEBUGVertexB;
			float _DEBUGVertexRGBA;
			float _DEBUGVertexA;
			float _NormalBackFaceFixBranch;
			float _TTFETREEFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _DEBUGWINDMASK;
			float _ADVANCEDSETTINGS;
			float _TEXTUREMAPS;
			float _VertexShadow;
			float _TEXTURESETTINGS;
			float _LeafInteraction;
			float _TEXTUREMAPS1;
			float _CenterofMass;
			float _MaskRootsAuto;
			float _Radius;
			float _Hardness;
			float _MaskRoots;
			float _RootsPosition;
			float _RootsRadius;
			float _RootsHardness;
			float _BranchMaskScale;
			float _BranchMaskRadious;
			float _BranchSwayPower;
			float _BranchWindLarge;
			float _BranchWindSmall;
			float _SwitchVGreenToRGBA;
			float _VertexColorPower;
			float _GrassSwayPowerGentleWind;
			float _DownwardStrength;
			float _MotionBendingGentleRandom;
			float _TrunkHeightandThickness;
			float _PivotRandomnessStrength;
			float _PivotRandomness;
			float _BranchFoldStrength;
			half _FlutterSize;
			float _FlutterPower;
			float _MotionFlutterIntensity;
			float _WINDMASKSETTINGS1;
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
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3370;-2352,704;Inherit;False;2327.49;1149.345;Creates a shading volume based on vertex position.;5;3394;3374;3373;3372;3371;Self Shading;0.7490196,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3371;-2288,1584;Inherit;False;1062.82;214.1602;;6;3400;3399;3398;3397;3396;3395;Center of Mass_Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3372;-2272,800;Inherit;False;1043.326;361.795;;8;3385;3384;3383;3382;3381;3380;3379;3378;Main Shading_Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3373;-1872,1200;Inherit;False;636.7156;322.8198;;4;3431;3393;3392;3391;Vertex Ao;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3374;-1152,1248;Inherit;False;1064.932;380.8687;;7;3438;3401;3390;3389;3388;3387;3386;Main Mask_Output;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2801;-704,-368;Inherit;False;927.4823;879.2003;;15;2803;2814;2813;2812;2811;2810;2804;2815;2807;2808;2809;2806;2805;2830;2831;[DEBUG] Compute Vertex Colors;0.08283591,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3375;592,160;Inherit;False;533.6575;344.2466;;5;3436;3435;3434;3427;3446;Self Shading_Output;0.7490196,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3364;1280,944;Inherit;False;448.7511;320.1864;;2;3468;3469;Fix Backface Branch;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2878;1648,-784;Inherit;False;518.1274;631.0885;;8;3449;3450;3018;2860;3016;2859;2862;3015;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3467;272,800;Inherit;False;(TTFE) Tree Foliage_Wind System (Mobile);12;;2284;66519fe4703a06e4796e4962d99c1984;0;0;2;COLOR;0;COLOR;669
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3447;816,1424;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PosVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3378;-2240,848;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3395;-2256,1632;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3379;-2048,848;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;2,1.3,2;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3380;-2000,1072;Inherit;False;Constant;_Radius1;Radius;11;0;Create;True;0;0;0;False;0;False;25;25;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3396;-2064,1648;Inherit;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3381;-1808,960;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DistanceOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3397;-1920,1648;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0.8,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3382;-1680,944;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3398;-1760,1648;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.5;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3383;-1552,944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3384;-1552,1024;Inherit;False;Constant;_Hardness1;Hardness;13;0;Create;True;0;0;0;False;0;False;1.5;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3391;-1744,1264;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3399;-1552,1632;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3431;-1840,1440;Inherit;False;Property;_VertexAo;Vertex Ao;11;1;[Header];Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3400;-1392,1632;Inherit;False;0.6;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3387;-1120,1312;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3388;-1104,1440;Inherit;False;Constant;_Dic_S;Dic_S;32;0;Create;True;0;0;0;False;0;False;0.92;0.92;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3389;-1104,1520;Inherit;False;Constant;_Dic_O;Dic_O;30;0;Create;True;0;0;0;False;0;False;-0.16;-0.16;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3393;-1392,1360;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3390;-880,1424;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3394;-1216,1408;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3386;-656,1344;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3401;-480,1360;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3438;-320,1360;Inherit;False;SelfShading;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2805;-656,-112;Inherit;False;Constant;_Color6;Color 2;10;0;Create;True;0;0;0;False;0;False;0.1020105,1,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2806;-656,80;Inherit;False;Constant;_Color7;Color 2;10;0;Create;True;0;0;0;False;0;False;0,0.09082031,1,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2804;-640,-304;Inherit;False;Constant;_Color5;Color 2;10;0;Create;True;0;0;0;False;0;False;1,0,0,1;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2803;-640,304;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3369;626.0125,585.3443;Inherit;False;Constant;_Float6;Float 6;19;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3434;656,320;Inherit;False;Property;_VertexLighting;Vertex Lighting;9;1;[Header];Create;True;1;(Self Shading);0;0;False;0;False;3;3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3435;656,400;Inherit;False;Property;_VertexShadow;Vertex Shadow;10;0;Create;True;0;0;0;False;0;False;0;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3436;656,224;Inherit;False;3438;SelfShading;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2809;-384,-208;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2808;-384,-16;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2807;-384,208;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2830;-256,336;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2813;-192,304;Inherit;False;Property;_DEBUGVertexA;[DEBUG] VertexA;61;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3357;800,736;Inherit;True;Property;_NormalMap;Normal Map;5;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3356;816,528;Inherit;True;Property;_AlbedoMap;Albedo Map;4;1;[NoScaleOffset];Create;True;0;0;0;False;2;Space (10);TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3427;880,288;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;11;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2811;-192,-48;Inherit;False;Property;_DEBUGVertexG;[DEBUG] VertexG;59;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2810;-192,-176;Inherit;False;Property;_DEBUGVertexR;[DEBUG] VertexR;58;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2831;64,240;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2812;-192,64;Inherit;False;Property;_DEBUGVertexB;[DEBUG] VertexB;60;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2814;-192,192;Inherit;False;Property;_DEBUGVertexRGBA;[DEBUG] VertexRGBA;62;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3445;1168,528;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2815;96,16;Inherit;False;5;5;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3439;1312,384;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3446;720,368;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3358;784,960;Inherit;True;Property;_MaskMapRGBA;Mask Map *RGB(A);6;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3015;1744,-656;Inherit;False;Property;_FACERENDERING;FACE RENDERING;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2862;1744,-496;Inherit;False;Property;_DEBUGWINDMASK;DEBUG WIND MASK;55;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2859;1680,-736;Inherit;False;Property;_TTFETREEFOLIAGESHADERLOWEND;(TTFE) TREE FOLIAGE SHADER (LOW-END);0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2823;341.4378,26.69406;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3024;1488,176;Inherit;False;Property;_DEBUGVisualizeWindMask;[DEBUG] Visualize Wind Mask;56;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3028;1424,1488;Inherit;False;Property;_SAVEPERFORMANCEDeactivateWind;[SAVE PERFORMANCE] Deactivate Wind;64;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3018;1712,-416;Inherit;False;Property;_ADVANCEDSETTINGS;ADVANCED SETTINGS;63;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3450;1776,-336;Inherit;False;Property;_TEXTUREMAPS;TEXTURE MAPS;3;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3449;1744,-256;Inherit;False;Property;_TEXTURESETTINGS;TEXTURE SETTINGS;7;1;[Header];Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2860;2032,-576;Inherit;False;6;6;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3360;2000,304;Inherit;False;Constant;_Float5;Float 5;19;0;Create;True;0;0;0;False;0;False;0.02;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2818;1872,32;Inherit;False;Property;_DEBUGComputeVertexColors;[DEBUG] Compute Vertex Colors;57;0;Create;True;0;0;0;False;0;False;0;True;Create;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3452;2016,432;Inherit;False;Property;_AlphaClip;Alpha Clip;2;0;Create;True;0;0;0;False;1;Space (10);False;0.4;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3464;1216,1312;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3466;1216,1264;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3465;1904,1168;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3463;1952,1232;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3392;-1552,1344;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3385;-1392,960;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3016;1744,-576;Inherit;False;Constant;_BackfaceCulling;Backface Culling;2;1;[Enum];Create;True;0;3;Off;0;Front;1;Back;2;0;True;1;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3440;1808,736;Inherit;False;Property;_NormalBackFaceFixBranch;Normal Back Face Fix (Branch);8;0;Create;True;0;0;0;False;1;Space (10);False;0;True;Create;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SwitchByFaceNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3468;1473.251,1027.083;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NegateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3469;1329.251,1139.083;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3453;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;6;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3454;2304,224;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Foliage (Low-End);94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;51;Category;0;0;  Instanced Terrain Normals;1;0;Lighting Model;0;0;Workflow;0;638929303198246729;Surface;0;0;  Keep Alpha;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;0;638929303878319954;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;2;0;Specular Highlights;2;0;Environment Reflections;2;0;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;  XR Motion Vectors;0;0;GPU Instancing;1;0;LOD CrossFade;0;638937905774912511;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position;1;0;Debug Display;1;0;Clear Coat;0;0;0;12;False;True;True;True;True;True;True;True;True;True;True;False;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3455;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3456;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3457;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3458;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3459;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3460;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3461;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3462;2304,224;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3470;2304,324;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3471;2304,324;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;XRMotionVectors;0;11;XRMotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;True;1;False;;255;False;;1;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;1;LightMode=XRMotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;3447;0;3467;0
WireConnection;3379;0;3378;0
WireConnection;3396;0;3395;0
WireConnection;3381;0;3379;0
WireConnection;3381;1;3380;0
WireConnection;3397;0;3396;0
WireConnection;3382;0;3381;0
WireConnection;3382;1;3381;0
WireConnection;3398;0;3397;0
WireConnection;3383;0;3382;0
WireConnection;3399;0;3398;0
WireConnection;3400;0;3399;0
WireConnection;3387;0;3385;0
WireConnection;3387;1;3400;0
WireConnection;3393;0;3392;0
WireConnection;3390;0;3387;0
WireConnection;3390;1;3388;0
WireConnection;3390;2;3389;0
WireConnection;3394;0;3393;0
WireConnection;3386;0;3394;0
WireConnection;3386;1;3390;0
WireConnection;3401;0;3386;0
WireConnection;3438;0;3401;0
WireConnection;2809;0;2804;0
WireConnection;2809;1;2803;1
WireConnection;2808;0;2805;0
WireConnection;2808;1;2803;2
WireConnection;2807;0;2806;0
WireConnection;2807;1;2803;3
WireConnection;2830;0;2803;0
WireConnection;2813;1;2803;4
WireConnection;3357;5;3369;0
WireConnection;3427;0;3436;0
WireConnection;3427;1;3434;0
WireConnection;3427;2;3435;0
WireConnection;2811;1;2808;0
WireConnection;2810;1;2809;0
WireConnection;2831;0;2813;0
WireConnection;2812;1;2807;0
WireConnection;2814;1;2830;0
WireConnection;3445;0;3356;0
WireConnection;2815;0;2810;0
WireConnection;2815;1;2811;0
WireConnection;2815;2;2812;0
WireConnection;2815;3;2814;0
WireConnection;2815;4;2831;0
WireConnection;3439;0;3445;0
WireConnection;3439;1;3427;0
WireConnection;3446;0;3467;669
WireConnection;2823;0;2815;0
WireConnection;3024;1;3439;0
WireConnection;3024;0;3446;0
WireConnection;3028;1;3447;0
WireConnection;2860;0;2859;0
WireConnection;2860;1;3015;0
WireConnection;2860;2;2862;0
WireConnection;2860;3;3018;0
WireConnection;2860;4;3450;0
WireConnection;2860;5;3449;0
WireConnection;2818;0;3024;0
WireConnection;2818;1;2823;0
WireConnection;3464;0;3358;4
WireConnection;3466;0;3358;2
WireConnection;3465;0;3466;0
WireConnection;3463;0;3464;0
WireConnection;3392;0;3391;1
WireConnection;3392;1;3431;0
WireConnection;3385;0;3383;0
WireConnection;3385;1;3384;0
WireConnection;3440;0;3357;0
WireConnection;3440;1;3468;0
WireConnection;3468;0;3357;0
WireConnection;3468;1;3469;0
WireConnection;3469;0;3357;0
WireConnection;3454;0;2818;0
WireConnection;3454;1;3440;0
WireConnection;3454;9;3360;0
WireConnection;3454;4;3463;0
WireConnection;3454;5;3465;0
WireConnection;3454;2;2860;0
WireConnection;3454;6;3356;4
WireConnection;3454;7;3452;0
WireConnection;3454;8;3028;0
ASEEND*/
//CHKSM=E20F634FAD2838D7587040480BD29B4AEF2EA7E9