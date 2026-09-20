// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage (Low-End)"
{
	Properties
	{
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[TTFE_DrawerTitle] _TTFEGRASSFOLIAGESHADERLOWEND( "(TTFE) GRASS FOLIAGE SHADER (LOW-END)", Float ) = 0
		[TTFE_DrawerFeatureBorder][Space (10)] _FACERENDERING( "FACE RENDERING", Float ) = 0
		[Space (10)] _AlphaClip( "Alpha Clip", Float ) = 0.4
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTUREMAPS1( "TEXTURE MAPS", Float ) = 0
		[TTFE_Drawer_SingleLineTexture][Space (10)] _AlbedoMap( "Albedo Map", 2D ) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture] _NormalMap( "Normal Map", 2D ) = "bump" {}
		[TTFE_Drawer_SingleLineTexture] _MaskMapRGBA( "Mask Map *RGB(A)", 2D ) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture] _EmissionMap( "Emission Map", 2D ) = "white" {}
		[TTFE_DrawerFeatureBorder][Space (10)] _TEXTURESETTINGS( "TEXTURE SETTINGS", Float ) = 0
		[Header((Albedo))] _AlbedoColor( "Albedo Color", Color ) = ( 1, 1, 1, 0 )
		[Header((Normal))] _NormalIntensity( "Normal Intensity", Range( -3, 3 ) ) = 1
		[Header((Emission))] _EmissionIntensity1( "Emission Intensity", Float ) = 0
		_EmissionColor( "Emission Color", Color ) = ( 1, 1, 1, 0 )
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				
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
				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord7.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				float3 temp_output_751_0_g64018 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.ase_texcoord8.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 color792_g64018 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64018 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64018 = ( ( SphericalMaskProxySphere780_g64018 * color792_g64018 ) + ( color793_g64018 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64018 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64018;
				#else
				float4 staticSwitch4373 = ( _AlbedoColor * tex2DNode4411 );
				#endif
				
				float2 uv_NormalMap4402 = input.ase_texcoord7.xy;
				float3 unpack4402 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap4402 ), _NormalIntensity );
				unpack4402.z = lerp( 1, unpack4402.z, saturate(_NormalIntensity) );
				
				float3 temp_cast_1 = (0.0).xxx;
				
				float2 uv_MaskMapRGBA = input.ase_texcoord7.xy * _MaskMapRGBA_ST.xy + _MaskMapRGBA_ST.zw;
				float4 tex2DNode4421 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA );
				
				float2 uv_EmissionMap4405 = input.ase_texcoord7.xy;
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Normal = unpack4402;
				float3 Specular = temp_cast_1;
				float Metallic = 0;
				float Smoothness = tex2DNode4421.a;
				float Occlusion = tex2DNode4421.g;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADERLOWEND + _FACERENDERING + _ADVANCEDSETTINGS + ( float4( tex2D( _EmissionMap, uv_EmissionMap4405 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity1 ) + _TEXTUREMAPS1 + _TEXTURESETTINGS ).rgb;
				float Alpha = tex2DNode4411.a;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord1.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				

				float Alpha = tex2DNode4411.a;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord1.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				

				float Alpha = tex2DNode4411.a;
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
				output.ase_texcoord3.xy = input.texcoord0.xy;
				output.ase_texcoord4 = input.positionOS;
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord3.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				float3 temp_output_751_0_g64018 = ( ( input.ase_texcoord4.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.ase_texcoord4.xyz );
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.ase_texcoord4.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 color792_g64018 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64018 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64018 = ( ( SphericalMaskProxySphere780_g64018 * color792_g64018 ) + ( color793_g64018 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64018 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64018;
				#else
				float4 staticSwitch4373 = ( _AlbedoColor * tex2DNode4411 );
				#endif
				
				float2 uv_EmissionMap4405 = input.ase_texcoord3.xy;
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADERLOWEND + _FACERENDERING + _ADVANCEDSETTINGS + ( float4( tex2D( _EmissionMap, uv_EmissionMap4405 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity1 ) + _TEXTUREMAPS1 + _TEXTURESETTINGS ).rgb;
				float Alpha = tex2DNode4411.a;
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
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
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				output.ase_texcoord2 = input.positionOS;
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord1.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				float3 temp_output_751_0_g64018 = ( ( input.ase_texcoord2.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.ase_texcoord2.xyz );
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.ase_texcoord2.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 color792_g64018 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64018 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64018 = ( ( SphericalMaskProxySphere780_g64018 * color792_g64018 ) + ( color793_g64018 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64018 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64018;
				#else
				float4 staticSwitch4373 = ( _AlbedoColor * tex2DNode4411 );
				#endif
				

				float3 BaseColor = staticSwitch4373.rgb;
				float Alpha = tex2DNode4411.a;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_NormalMap4402 = input.ase_texcoord3.xy;
				float3 unpack4402 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap4402 ), _NormalIntensity );
				unpack4402.z = lerp( 1, unpack4402.z, saturate(_NormalIntensity) );
				
				float2 uv_AlbedoMap = input.ase_texcoord3.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				

				float3 Normal = unpack4402;
				float Alpha = tex2DNode4411.a;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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

			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature _SAVEPERFORMANCEDEACTIVATEWIND_ON
			#pragma multi_compile _WINDTYPE_GENTLEBREEZE _WINDTYPE_STRONGWIND
			#pragma shader_feature_local _PHYSISCSINTERACTION_ON
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
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
				output.ase_texcoord7.xy = input.texcoord.xy;
				output.ase_texcoord8 = input.positionOS;
				
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

				input.normalOS = float3( 0, 1, 0 );
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

				float2 uv_AlbedoMap = input.ase_texcoord7.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				float3 temp_output_751_0_g64018 = ( ( input.ase_texcoord8.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.ase_texcoord8.xyz );
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.ase_texcoord8.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 color792_g64018 = IsGammaSpace() ? float4( 1, 0, 0, 0 ) : float4( 1, 0, 0, 0 );
				float4 color793_g64018 = IsGammaSpace() ? float4( 0.02197886, 0, 1, 0 ) : float4( 0.00170115, 0, 1, 0 );
				float4 DEBUGVisualizeWindMask796_g64018 = ( ( SphericalMaskProxySphere780_g64018 * color792_g64018 ) + ( color793_g64018 * saturate( ( 1.0 - SphericalMaskProxySphere780_g64018 ) ) ) );
				#ifdef _DEBUGVISUALIZEWINDMASK_ON
				float4 staticSwitch4373 = DEBUGVisualizeWindMask796_g64018;
				#else
				float4 staticSwitch4373 = ( _AlbedoColor * tex2DNode4411 );
				#endif
				
				float2 uv_NormalMap4402 = input.ase_texcoord7.xy;
				float3 unpack4402 = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap4402 ), _NormalIntensity );
				unpack4402.z = lerp( 1, unpack4402.z, saturate(_NormalIntensity) );
				
				float3 temp_cast_1 = (0.0).xxx;
				
				float2 uv_MaskMapRGBA = input.ase_texcoord7.xy * _MaskMapRGBA_ST.xy + _MaskMapRGBA_ST.zw;
				float4 tex2DNode4421 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA );
				
				float2 uv_EmissionMap4405 = input.ase_texcoord7.xy;
				

				float3 BaseColor = staticSwitch4373.rgb;
				float3 Normal = unpack4402;
				float3 Specular = temp_cast_1;
				float Metallic = 0;
				float Smoothness = tex2DNode4421.a;
				float Occlusion = tex2DNode4421.g;
				float3 Emission = ( _TTFEGRASSFOLIAGESHADERLOWEND + _FACERENDERING + _ADVANCEDSETTINGS + ( float4( tex2D( _EmissionMap, uv_EmissionMap4405 ).rgb , 0.0 ) * _EmissionColor * _EmissionIntensity1 ) + _TEXTUREMAPS1 + _TEXTURESETTINGS ).rgb;
				float Alpha = tex2DNode4411.a;
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
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

				input.normalOS = float3( 0, 1, 0 );

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

				float2 uv_AlbedoMap = input.ase_texcoord1.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				

				surfaceDescription.Alpha = tex2DNode4411.a;
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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

				float GlobalVar_WindStrength1163_g64018 = _GlobalWindStrength;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult1270_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 BasicWorldPositionXY_Out1273_g64018 = appendResult1270_g64018;
				float4 WindDirection1065_g64018 = _WindDirection;
				float GlobalVar_WindSpeed921_g64018 = _StrongWindSpeed;
				float2 NoiseRotation_Output1269_g64018 = ( -(WindDirection1065_g64018).xz * _TimeParameters.x * GlobalVar_WindSpeed921_g64018 );
				float2 WPRG2D1115_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + NoiseRotation_Output1269_g64018 );
				float simpleNoise1180_g64018 = SimpleNoise( WPRG2D1115_g64018 );
				float3 break1192_g64018 = sin( ( ( _TimeParameters.y * 10.0 ) + ( ase_positionWS * 10.0 ) + input.tangentOS.xyz ) );
				float3 appendResult1195_g64018 = (float3(break1192_g64018.x , ( break1192_g64018.y * 0.3 ) , break1192_g64018.z));
				float3 smoothstepResult1181_g64018 = smoothstep( float3( 0,0,0 ) , float3( 2,2,2 ) , appendResult1195_g64018);
				float3 worldToObjDir1188_g64018 = mul( GetWorldToObjectMatrix(), float4( ( (simpleNoise1180_g64018*2.2 + -1.05) * ( smoothstepResult1181_g64018 * 0.2 ) * input.positionOS.xyz.y * sin( _TimeParameters.x * 0.125 ) ), 0.0 ) ).xyz;
				float3 NormaliseRotationAxis218_g64018 = float3( 0, 1, 0 );
				float2 appendResult1201_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1205_g64018 = snoise( ( appendResult1201_g64018 + ( _TimeParameters.x * GlobalVar_WindSpeed921_g64018 ) )*0.6 );
				simplePerlin2D1205_g64018 = simplePerlin2D1205_g64018*0.5 + 0.5;
				float NoiseLarge_BASIC1207_g64018 = simplePerlin2D1205_g64018;
				float mulTime702_g64018 = _TimeParameters.x * 2.0;
				float3 rotatedValue711_g64018 = RotateAroundAxis( ( saturate( input.positionOS.xyz.y ) * input.positionOS.xyz ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime702_g64018 ) ) * input.positionOS.xyz.y * float3( 0.5, 0.05, 0.5 ) * _GrassSwayPowerGentle ), NormaliseRotationAxis218_g64018, NoiseLarge_BASIC1207_g64018 );
				float3 worldToObjDir715_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindDirection1065_g64018 * float4( rotatedValue711_g64018 , 0.0 ) ).xyz, 0.0 ) ).xyz;
				float simplePerlin2D211_g64018 = snoise( WPRG2D1115_g64018*0.1 );
				float MaskRotation268_g64018 = saturate( simplePerlin2D211_g64018 );
				float3 clampResult148_g64018 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
				float3 Scale_MaskA177_g64018 = clampResult148_g64018;
				float simplePerlin2D651_g64018 = snoise( WPRG2D1115_g64018*0.09995 );
				simplePerlin2D651_g64018 = simplePerlin2D651_g64018*0.5 + 0.5;
				float MaskRotation2649_g64018 = saturate( simplePerlin2D651_g64018 );
				float3 temp_output_725_0_g64018 = ( input.positionOS.xyz * 0.2 );
				float3 rotatedValue727_g64018 = RotateAroundAxis( float3( 0,0,0 ), temp_output_725_0_g64018, NormaliseRotationAxis218_g64018, ( input.positionOS.xyz.y * NoiseLarge_BASIC1207_g64018 ) );
				float3 worldToObjDir732_g64018 = mul( GetWorldToObjectMatrix(), float4( ( rotatedValue727_g64018 - temp_output_725_0_g64018 ), 0.0 ) ).xyz;
				float4 normalizeResult1119_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 break1122_g64018 = (normalizeResult1119_g64018).xyz;
				float4 appendResult1124_g64018 = (float4(break1122_g64018.x , ( break1122_g64018.y + _MotionBendingDownwardStrength ) , break1122_g64018.z , 0.0));
				float4 temp_output_1127_0_g64018 = ( appendResult1124_g64018 * saturate( input.positionOS.xyz.y ) );
				float4 WindMotion_Base1297_g64018 = temp_output_1127_0_g64018;
				float GlobalVar_WindMotion1165_g64018 = _WindMotion;
				float2 WPRG2D_S41140_g64018 = ( BasicWorldPositionXY_Out1273_g64018 + ( NoiseRotation_Output1269_g64018 * 4.0 ) );
				float simplePerlin2D1136_g64018 = snoise( WPRG2D_S41140_g64018*0.2 );
				simplePerlin2D1136_g64018 = simplePerlin2D1136_g64018*0.5 + 0.5;
				float WindMotionNoise1300_g64018 = simplePerlin2D1136_g64018;
				float3 worldToObjDir1292_g64018 = mul( GetWorldToObjectMatrix(), float4( ( WindMotion_Base1297_g64018 *  (0.0 + ( GlobalVar_WindMotion1165_g64018 - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * WindMotionNoise1300_g64018 ).xyz, 0.0 ) ).xyz;
				float3 MotionBendingGentleWind1290_g64018 = ( worldToObjDir1292_g64018 * ase_objectScale * Scale_MaskA177_g64018 );
				float4 MotionBendingGentleRandom1291_g64018 = ( WindMotion_Base1297_g64018 * _MotionBendingRandomnessGentleWind * WindMotionNoise1300_g64018 );
				float mulTime1551_g64018 = _TimeParameters.x * 5.2;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float dotResult1485_g64018 = dot( ase_objectPosition , float3( 69.42, 37.86, 82.03 ) );
				float RandomSeedVector_E1488_g64018 = dotResult1485_g64018;
				float dotResult1487_g64018 = dot( ase_objectPosition , float3( 44.18, 51.13, 22.05 ) );
				float RandomSeedVector_D1489_g64018 = dotResult1487_g64018;
				float mulTime1550_g64018 = _TimeParameters.x * 4.3;
				float dotResult1486_g64018 = dot( ase_objectPosition , float3( 93.44, 53.12, 48.9 ) );
				float RandomSeedVector_F1490_g64018 = dotResult1486_g64018;
				float3 rotatedValue1564_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, normalize( float3( 1.2, 1, 0.8 ) ), ( ( ( cos( ( mulTime1551_g64018 + ( float3( 0.3, 0.55, 0.25 ) * 0.3 * RandomSeedVector_E1488_g64018 ) + ( RandomSeedVector_D1489_g64018 * 0.02 ) ) ) + sin( ( mulTime1550_g64018 + ( float3( 0.8, 0.33, 0.6 ) * 0.6 * RandomSeedVector_F1490_g64018 ) + ( input.positionOS.xyz * 1 ) ) ) ) * 0.1 ) * saturate( input.positionOS.xyz.y ) ).x );
				float3 RandomPivotMotiton1572_g64018 = ( ( ( rotatedValue1564_g64018 - input.positionOS.xyz ) * _PivotSwayPower ) * 0.2 );
				float3 PivotSwayGentle1576_g64018 = ( (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) * 0.5 );
				float dotResult1677_g64018 = dot( ase_objectPosition , float3( 0.05, 0, 0.05 ) );
				float3 temp_output_751_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / _Radius2 );
				float dotResult749_g64018 = dot( temp_output_751_0_g64018 , temp_output_751_0_g64018 );
				float saferPower755_g64018 = abs( saturate( dotResult749_g64018 ) );
				float3 normalizeResult1001_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float CenterOfMass999_g64018 = (distance( normalizeResult1001_g64018 , ( NormaliseRotationAxis218_g64018 * _CenterofMassintensity ) )*1.0 + 0.0);
				float SphericalMaskProxySphere780_g64018 = ( ( saturate( input.positionOS.xyz.y ) * pow( saferPower755_g64018 , _Hardness2 ) ) * CenterOfMass999_g64018 );
				float4 normalizeResult1694_g64018 = ASESafeNormalize( WindDirection1065_g64018 );
				float3 worldToObjDir1699_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( ( ( sin( ( ( dotResult1677_g64018 + _TimeParameters.x + ( RandomSeedVector_F1490_g64018 * 0.002 ) ) * GlobalVar_WindSpeed921_g64018 ) ) + 1.0 ) * 0.5 ) * ( GlobalVar_WindMotion1165_g64018 * 0.45 ) ) * SphericalMaskProxySphere780_g64018 * normalizeResult1694_g64018 * saturate( ( input.positionOS.xyz.y / 1.0 ) ) ).xyz, 0.0 ) ).xyz;
				float3 normalizeResult476_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime477_g64018 = _TimeParameters.x * 0.2;
				float simplePerlin2D479_g64018 = snoise( ( normalizeResult476_g64018 + mulTime477_g64018 ).xy*0.4 );
				float NoiseMask_LargeA482_g64018 = ( simplePerlin2D479_g64018 * 1.5 );
				float3 normalizeResult494_g64018 = ASESafeNormalize( ase_positionWS );
				float mulTime499_g64018 = _TimeParameters.x * 0.26;
				float simplePerlin2D498_g64018 = snoise( ( normalizeResult494_g64018 + mulTime499_g64018 ).xy*0.7 );
				float NoiseMask_LargeB502_g64018 = ( simplePerlin2D498_g64018 * 1.5 );
				float3 worldToObjDir980_g64018 = mul( GetWorldToObjectMatrix(), float4( ( tex2Dlod( _WindNoiseMap, float4( ( WPRG2D_S41140_g64018 * 0.1 ), 0, 0.0) ) * NoiseMask_LargeA482_g64018 * NoiseMask_LargeB502_g64018 * float4( float3( -1, 0.1, -1 ) , 0.0 ) ).rgb, 0.0 ) ).xyz;
				float mulTime953_g64018 = _TimeParameters.x * 5.0;
				float3 temp_cast_14 = (0.0).xxx;
				float mulTime946_g64018 = _TimeParameters.x * 0.3;
				float dotResult4_g64019 = dot( float2( 1,1 ) , float2( 12.9898,78.233 ) );
				float lerpResult10_g64019 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g64019 ) * 43758.55 ) ));
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 temp_output_766_0_g64018 = ( ( input.positionOS.xyz - float3( 0, -1, 0 ) ) / 2.0 );
				float dotResult764_g64018 = dot( temp_output_766_0_g64018 , temp_output_766_0_g64018 );
				float saferPower768_g64018 = abs( saturate( dotResult764_g64018 ) );
				float3 normalizeResult772_g64018 = ASESafeNormalize( input.positionOS.xyz );
				float SphericalMaskConstant785_g64018 = (( ( saturate( input.positionOS.xyz.y ) * pow( saferPower768_g64018 , -1.0 ) ) * (distance( normalizeResult772_g64018 , ( NormaliseRotationAxis218_g64018 * 0.5 ) )*1.3 + 0.0) )*1.7 + 0.0);
				float3 worldToObjDir1132_g64018 = mul( GetWorldToObjectMatrix(), float4( ( ( temp_output_1127_0_g64018 * GlobalVar_WindMotion1165_g64018 ) * simplePerlin2D1136_g64018 ).xyz, 0.0 ) ).xyz;
				float3 temp_cast_16 = (1.0).xxx;
				float3 WindMotion_Output1289_g64018 = ( worldToObjDir1132_g64018 * ase_objectScale * (( _WindMotionScaleMask )?( Scale_MaskA177_g64018 ):( temp_cast_16 )) );
				#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#elif defined( _WINDTYPE_STRONGWIND )
				float4 staticSwitch1011_g64018 = float4(  (float3( 0,0,0 ) + ( ( ( worldToObjDir1699_g64018 * ase_objectScale ) + ( ( ( worldToObjDir980_g64018 * saturate( input.positionOS.xyz.y ) * ase_objectScale ) * 0.4 ) + ( ( ( sin( ( ( ( RandomSeedVector_D1489_g64018 + input.positionOS.xyz ) * _MotionFlutterScale ) + ( mulTime953_g64018 * GlobalVar_WindSpeed921_g64018 ) ) ) * 0.05 ) * float3( -1, 0.4, -1 ) * saturate( input.positionOS.xyz.y ) * Scale_MaskA177_g64018 ) * _MotionFlutterIntensity ) + (( _QuadScatterOnlybasiccards )?( ( ( ( sin( ( ( GlobalVar_WindSpeed921_g64018 * mulTime946_g64018 ) * ( 20.0 * lerpResult10_g64019 * (( _QuadScatterWorldNormals )?( ase_normalWS ):( input.tangentOS.xyz )) ) ) ) * saturate( input.positionOS.xyz.y ) * SphericalMaskConstant785_g64018 * Scale_MaskA177_g64018 ) * 0.1 ) * _QuadScatterIntensity ) ):( temp_cast_14 )) ) + WindMotion_Output1289_g64018 + (( _PivotSway )?( RandomPivotMotiton1572_g64018 ):( float3( 0,0,0 ) )) ) - float3( 0,0,0 ) ) * ( float3( 1,1,1 ) - float3( 0,0,0 ) ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) , 0.0 );
				#else
				float4 staticSwitch1011_g64018 =  (float4( 0,0,0,0 ) + ( ( float4( ( ase_objectScale * worldToObjDir1188_g64018 ) , 0.0 ) + float4( ( worldToObjDir715_g64018 * input.positionOS.xyz.y * ase_objectScale * MaskRotation268_g64018 * Scale_MaskA177_g64018 ) , 0.0 ) + float4( ( MaskRotation2649_g64018 * worldToObjDir732_g64018 * ase_objectScale * Scale_MaskA177_g64018 ) , 0.0 ) + float4( MotionBendingGentleWind1290_g64018 , 0.0 ) + MotionBendingGentleRandom1291_g64018 + float4( PivotSwayGentle1576_g64018 , 0.0 ) ) - float4( 0,0,0,0 ) ) * ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) / ( float4( 1,1,1,1 ) - float4( 0,0,0,0 ) ) );
				#endif
				float3 temp_output_1442_0_g64018 = ( ( ase_positionWS - _PlayerPosition ) * _BendDirection );
				float temp_output_1440_0_g64018 = ( 1.0 - saturate( ( distance( ase_positionWS , _PlayerPosition ) / _BendRadius ) ) );
				float temp_output_1441_0_g64018 = saturate( input.positionOS.xyz.y );
				float mulTime1424_g64018 = _TimeParameters.x * 0.5;
				float2 appendResult1422_g64018 = (float2(ase_positionWS.x , ase_positionWS.z));
				float simplePerlin2D1431_g64018 = snoise( ( _TimeParameters.x + appendResult1422_g64018 ) );
				simplePerlin2D1431_g64018 = simplePerlin2D1431_g64018*0.5 + 0.5;
				float3 InteractionNoise1445_g64018 = ( ( sin( ( mulTime1424_g64018 * ( 20.0 * input.tangentOS.xyz ) ) ) * simplePerlin2D1431_g64018 ) * 0.4 );
				float4 transform1455_g64018 = mul(GetWorldToObjectMatrix(),float4( ( ( ( _Disturbance * temp_output_1442_0_g64018 ) * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 * InteractionNoise1445_g64018 ) + ( ( temp_output_1442_0_g64018 * _BendAmountGrass * temp_output_1440_0_g64018 * temp_output_1441_0_g64018 ) * 0.5 ) ) , 0.0 ));
				float smoothstepResult1603_g64018 = smoothstep( 0.0 , 0.1 , input.ase_color.g);
				float4 ShaderInteraction_Output1456_g64018 = ( transform1455_g64018 * saturate( smoothstepResult1603_g64018 ) );
				float3 normalizeResult1587_g64018 = ASESafeNormalize( ( _WorldSpaceCameraPos - ase_positionWS ) );
				float dotResult1592_g64018 = dot( float3( 0, 1, 0 ) , (normalizeResult1587_g64018*1.0 + -0.5) );
				float PerspectiveCorrection1600_g64018 = (( _PerspectiveCorrection )?( ( ( saturate( dotResult1592_g64018 ) * _PerspectiveIntensity ) * saturate( input.positionOS.xyz.y ) ) ):( 0.0 ));
				float temp_output_1626_0_g64018 = ( ( saturate( ( input.positionOS.xyz.y / _HeightMax ) ) * _BendAmount ) * ( PI / 180.0 ) );
				float3 normalizeResult1629_g64018 = ASESafeNormalize( _WindAxis );
				float3 rotatedValue1636_g64018 = RotateAroundAxis( float3( 0,0,0 ), input.positionOS.xyz, -_BendAxis, (( _PhysicsWind )?( ( ( temp_output_1626_0_g64018 * ( sin( ( input.positionOS.xyz.x + ( _TimeParameters.x * _WindFrequency ) ) ) * _WindAmplitude ) ) * normalizeResult1629_g64018 ) ):( ( temp_output_1626_0_g64018 * normalizeResult1629_g64018 ) )).x );
				#ifdef _PHYSISCSINTERACTION_ON
				float3 staticSwitch1640_g64018 = ( rotatedValue1636_g64018 - input.positionOS.xyz );
				#else
				float3 staticSwitch1640_g64018 = float3( 0,0,0 );
				#endif
				float3 PhysicsInteraction1638_g64018 = staticSwitch1640_g64018;
				float4 FinalWind_Output350_g64018 = ( ( GlobalVar_WindStrength1163_g64018 * ( staticSwitch1011_g64018 + _TEXTUREMAPS + _WINDMASKSETTINGS ) ) + (( _GrassInteraction )?( ShaderInteraction_Output1456_g64018 ):( float4( 0,0,0,0 ) )) + PerspectiveCorrection1600_g64018 + float4( PhysicsInteraction1638_g64018 , 0.0 ) );
				#ifdef _SAVEPERFORMANCEDEACTIVATEWIND_ON
				float4 staticSwitch4381 = float4( 0,0,0,0 );
				#else
				float4 staticSwitch4381 = FinalWind_Output350_g64018;
				#endif
				
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

				input.normalOS = float3( 0, 1, 0 );

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

				float2 uv_AlbedoMap = input.ase_texcoord1.xy * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
				float4 tex2DNode4411 = tex2D( _AlbedoMap, uv_AlbedoMap );
				

				surfaceDescription.Alpha = tex2DNode4411.a;
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
			#pragma multi_compile _ LOD_FADE_CROSSFADE
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
			float4 _AlbedoMap_ST;
			float4 _EmissionColor;
			float4 _MaskMapRGBA_ST;
			float4 _AlbedoColor;
			float3 _BendDirection;
			float3 _WindAxis;
			float3 _BendAxis;
			float _PhysicsWind;
			float _HeightMax;
			float _BendAmount;
			float _WindFrequency;
			float _GrassSwayPowerGentle;
			float _NormalIntensity;
			float _TTFEGRASSFOLIAGESHADERLOWEND;
			float _FACERENDERING;
			float _ADVANCEDSETTINGS;
			float _EmissionIntensity1;
			float _TEXTUREMAPS1;
			float _WindAmplitude;
			float _PerspectiveIntensity;
			float _PerspectiveCorrection;
			float _BendRadius;
			float _MotionBendingDownwardStrength;
			float _MotionBendingRandomnessGentleWind;
			float _PivotSway;
			float _PivotSwayPower;
			float _Radius2;
			float _Hardness2;
			float _CenterofMassintensity;
			half _MotionFlutterScale;
			float _MotionFlutterIntensity;
			float _QuadScatterOnlybasiccards;
			float _QuadScatterWorldNormals;
			float _QuadScatterIntensity;
			float _WindMotionScaleMask;
			float _TEXTUREMAPS;
			float _WINDMASKSETTINGS;
			float _GrassInteraction;
			float _BendAmountGrass;
			float _TEXTURESETTINGS;
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
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4194;-688,-944;Inherit;False;586.2498;798.045;;8;4018;4427;4295;4020;4005;4358;4301;4426;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4400;-1040,672;Inherit;False;(TTFE) Grass Foliage_Wind System (Mobile);13;;64018;132823e840f443447bf2e73c56fa733c;0;0;2;FLOAT4;0;COLOR;1007
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4423;-896,-176;Inherit;False;Property;_AlbedoColor;Albedo Color;9;1;[Header];Create;True;1;(Albedo);0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4409;-784,1232;Inherit;False;Property;_EmissionIntensity1;Emission Intensity;11;1;[Header];Create;True;1;(Emission);0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4411;-960,32;Inherit;True;Property;_AlbedoMap;Albedo Map;4;0;Create;True;0;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (10);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4407;-784,1024;Inherit;False;Property;_EmissionColor;Emission Color;12;0;Create;True;0;0;0;False;0;False;1,1,1,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4301;-544,-784;Inherit;False;Property;_FACERENDERING;FACE RENDERING;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4005;-640,-880;Inherit;False;Property;_TTFEGRASSFOLIAGESHADERLOWEND;(TTFE) GRASS FOLIAGE SHADER (LOW-END);0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4426;-544,-320;Inherit;False;Property;_TEXTURESETTINGS;TEXTURE SETTINGS;8;1;[Header];Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4427;-544,-416;Inherit;False;Property;_TEXTUREMAPS1;TEXTURE MAPS;3;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4424;-640,-80;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4408;-528,992;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4358;-592,-512;Inherit;False;Property;_ADVANCEDSETTINGS;ADVANCED SETTINGS;48;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4401;-1264,288;Inherit;False;Property;_NormalIntensity;Normal Intensity;10;1;[Header];Create;True;1;(Normal);0;0;False;0;False;1;1;-3;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4020;-256,-672;Inherit;False;6;6;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;COLOR;0,0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4422;-192,80;Inherit;False;Constant;_Float0;Float 0;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4373;-400,-80;Inherit;False;Property;_DEBUGVisualizeWindMask;[DEBUG] Visualize Wind Mask;46;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4381;-528,672;Inherit;False;Property;_SAVEPERFORMANCEDeactivateWind;[SAVE PERFORMANCE] Deactivate Wind;49;0;Create;True;0;0;0;False;1;Space (10);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4402;-960,240;Inherit;True;Property;_NormalMap;Normal Map;5;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;73d15f41912fea842a8cc384ba511891;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4421;-960,448;Inherit;True;Property;_MaskMapRGBA;Mask Map *RGB(A);6;0;Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4428;-304,464;Inherit;False;Property;_AlphaClip;Alpha Clip;2;0;Create;True;0;0;0;False;1;Space (10);False;0.4;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4410;-288,848;Inherit;False;Constant;_Vector4;Vector 4;17;0;Create;True;0;0;0;False;0;False;0,1,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4018;-560,-608;Inherit;False;Property;_DEBUGWINDMASK;DEBUG WIND MASK;47;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4295;-544,-688;Inherit;False;Constant;_BackfaceCulling;Backface Culling;3;1;[Enum];Create;True;0;3;Off;0;Front;1;Back;2;0;True;1;Space (10);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4405;-848,800;Inherit;True;Property;_EmissionMap;Emission Map;7;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4429;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;6;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4430;0,0;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage (Low-End);94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;51;Category;0;0;  Instanced Terrain Normals;1;0;Lighting Model;0;0;Workflow;0;638929287583126351;Surface;0;0;  Keep Alpha;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;0;638929287842818766;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;2;0;Specular Highlights;2;0;Environment Reflections;2;0;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;  XR Motion Vectors;0;0;GPU Instancing;1;0;LOD CrossFade;1;0;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position;1;0;Debug Display;1;0;Clear Coat;0;0;0;12;False;True;True;True;True;True;True;True;True;True;True;False;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4431;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4432;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4433;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4434;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4435;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4436;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4437;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4438;0,0;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4439;0,100;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4440;0,100;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;XRMotionVectors;0;11;XRMotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;True;1;False;;255;False;;1;False;;7;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;1;LightMode=XRMotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;4424;0;4423;0
WireConnection;4424;1;4411;0
WireConnection;4408;0;4405;5
WireConnection;4408;1;4407;0
WireConnection;4408;2;4409;0
WireConnection;4020;0;4005;0
WireConnection;4020;1;4301;0
WireConnection;4020;2;4358;0
WireConnection;4020;3;4408;0
WireConnection;4020;4;4427;0
WireConnection;4020;5;4426;0
WireConnection;4373;1;4424;0
WireConnection;4373;0;4400;1007
WireConnection;4381;1;4400;0
WireConnection;4402;5;4401;0
WireConnection;4430;0;4373;0
WireConnection;4430;1;4402;0
WireConnection;4430;9;4422;0
WireConnection;4430;4;4421;4
WireConnection;4430;5;4421;2
WireConnection;4430;2;4020;0
WireConnection;4430;6;4411;4
WireConnection;4430;7;4428;0
WireConnection;4430;8;4381;0
WireConnection;4430;10;4410;0
ASEEND*/
//CHKSM=FCA141E04B9AE085C4E0F11D459358314A0B27CD