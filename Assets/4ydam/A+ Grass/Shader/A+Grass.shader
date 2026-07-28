Shader "4ydam/A+Grass"
{
	Properties
	{
		// Base Texture
		_TextureSample( "Texture Sample", 2D ) = "white" {}
		_TextureRamp( "Texture Ramp", 2D ) = "white" {}

		// Colour
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		_ColorTint( "Color Tint", Color ) = (1,1,1,1)
		
		[Toggle(_GRADIENT_ON)] _UseGradient( "Use Gradient", Float ) = 0
		_GradientTopColor( "Gradient Top Color", Color ) = (1,1,1,1)
		_GradientBottomColor( "Gradient Bottom Color", Color ) = (0.5,0.5,0.5,1)
		_GradientOffset( "Gradient Offset", Range( -1, 1 ) ) = .5
		_GradientContrast( "Gradient Contrast", Range( 0.1, 3 ) ) = .5

		// Colour Variation
		_ColorNoiseTexture( "Color Noise Texture", 2D ) = "white" {}
		_ColorNoiseScale( "Color Noise Scale", Range( 0.01, 2 ) ) = 0.1
		_ColorNoiseStrength( "Color Noise Strength", Range( 0, 1 ) ) = 0.6	
		_ColorNoiseLowColor( "Color Noise Low Color", Color ) = (1,1,1,1)
		_ColorNoiseHighColor( "Color Noise High Color", Color ) = (1,1,1,1)
		
		// Interaction
		_InteractionStrength( "Interaction Strength", Range( 0, 1 ) ) = 0.2
		_PushDownAmount( "Push Down Amount", Range( 0, 1 ) ) = 0.1
		[HideInInspector] _InteractionMultiplier( "Interaction Multiplier", Float ) = 1
		_BendPivotOffset( "Bend Pivot Offset", Range( -1, 1 ) ) = 0

		// Trail
		_TrailTintTopColor( "Trail Tint Top Color", Color ) = (0,0,0,1)
		_TrailTintColor( "Trail Tint Color", Color ) = (0,0,0,1)
		_TrailTintStrength( "Trail Tint Strength", Range(0, 2) ) = 1

		// Wind
		_WindNoiseTexture( "Wind Noise Texture", 2D ) = "black" {}
		_WindScroll( "Wind Scroll", Range( 0, 3 ) ) = 0.05
		_WindJitter( "Wind Jitter", Range( 0, 3 ) ) = 0.3
		
		_WindNoiseTexture2( "Wind Noise Texture 2", 2D ) = "black" {}
		_WindScroll2( "2nd Wind Scroll", Range( 0, 3 ) ) = 0.3
		_WindJitter2( "2nd Wind Jitter", Range( 0, 3 ) ) = 0.5

		_WindBlend( "2nd Wind Blend", Range( 0, 1 ) ) = 0.3
		
		// Perspective Correction
		[Toggle] _UsePerspectiveCorrection( "Use Perspective Correction", Float ) = 0
		_PerspectiveCorrectionStrength( "Perspective Correction Strength", Range( 0, 1 ) ) = 0.35
		_PerspectiveTopDownStart( "Perspective Top-Down Start", Range( 0, 1 ) ) = 0.45
		_PerspectiveHeightStart( "Perspective Height Start", Range( 0, 1 ) ) = 0
		_PerspectiveHeightPower( "Perspective Height Power", Range( 0.1, 4 ) ) = 1.5
		_PerspectiveMaxOffset( "Perspective Max Offset", Range( 0, 1 ) ) = 0.2

		// Distance Fade
		[Toggle] _UseDistanceFade( "Use Distance Fade", Float ) = 0
		[Enum(Smooth Fade,0,Dither Fade,1)] _DistanceFadeMode( "Distance Fade Mode", Float ) = 0
		_DistanceFadeStart( "Distance Fade Start", Float ) = 60
		_DistanceFadeEnd( "Distance Fade End", Float ) = 90
		[Toggle] _CullAtFadeEnd( "Cull At Fade End", Float ) = 0

		// Rendering
		[HideInInspector] _CullMode("Render Face", Float) = 0
		_AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1

		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

		Cull [_CullMode]
		AlphaToMask Off

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles


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
		#endif
		float _AGrassGlobalWind1Multiplier;
		float _AGrassGlobalWind2Multiplier;
		float4 _AGrassGlobalWindDirection;

		float2 GetAGrassWindDirection()
		{
			float2 direction = _AGrassGlobalWindDirection.xy;
			float directionLengthSq = dot(direction, direction);
			if (directionLengthSq <= 0.0001)
			{
				return float2(0.7071068, 0.7071068);
			}

			return direction * rsqrt(directionLengthSq);
		}

		float2 RotateAGrassWindUV(float2 uv, float2 direction)
		{
			return float2(
				(uv.x * direction.y) - (uv.y * direction.x),
				(uv.x * direction.x) + (uv.y * direction.y)
			);
		}

		float3 AGrassPerspectiveCorrectionOffset(
			float3 positionWS,
			float3 cameraPositionWS,
			float bladeHeight01,
			float correctionStrength,
			float topDownStart,
			float heightStart,
			float heightPower,
			float maxOffset)
		{
			if (correctionStrength <= 0.0001 || bladeHeight01 <= 0.0001)
			{
				return float3(0, 0, 0);
			}

			float3 basePosWS   = float3(positionWS.x, 0.0, positionWS.z);
			float3 toCameraWS  = cameraPositionWS - basePosWS;
			float  toCamLenSq  = dot(toCameraWS, toCameraWS);
			if (toCamLenSq <= 0.000001)
			{
				return float3(0, 0, 0);
			}

			float3 viewDirWS   = toCameraWS * rsqrt(toCamLenSq);

			float topDownFactor = saturate((abs(viewDirWS.y) - topDownStart) / max(0.0001, 1.0 - topDownStart));

			float2 planarDir   = viewDirWS.xz;
			float  planarLenSq = dot(planarDir, planarDir);
			if (planarLenSq <= 0.000001)
			{
				return float3(0, 0, 0);
			}
			planarDir *= rsqrt(planarLenSq);

			float height01 = saturate((bladeHeight01 - heightStart) / max(0.0001, 1.0 - heightStart));
			float heightMask = pow(height01, max(0.1, heightPower));
			float correction = min(maxOffset, correctionStrength * topDownFactor * heightMask);
			return float3(-planarDir.x, 0.0, -planarDir.y) * correction;
		}

		float AGrassDistanceFade(float3 objectCenterWS, float3 cameraPositionWS, float4 positionCS, float useDistanceFade, float fadeMode, float fadeStart, float fadeEnd, float cullAtFadeEnd)
		{
			if (useDistanceFade <= 0.5 || fadeEnd <= fadeStart)
			{
				return 1.0;
			}

			float distanceToCamera = distance(objectCenterWS, cameraPositionWS);
			if (cullAtFadeEnd > 0.5 && distanceToCamera >= fadeEnd)
			{
				return 0.0;
			}

			float fadeRange = max(0.0001, fadeEnd - fadeStart);
			float t = saturate((distanceToCamera - fadeStart) / fadeRange);
			float smoothFade = 1.0 - smoothstep(0.0, 1.0, t);

			if (fadeMode < 0.5)
			{
				return smoothFade;
			}

			float2 pixelPos = floor(positionCS.xy);
			float ditherNoise = frac(52.9829189 * frac(dot(pixelPos, float2(0.06711056, 0.00583715))));
			return step(ditherNoise, smoothFade);
		}
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

			#define _ALPHATEST_ON 1
			#pragma shader_feature_local _GRADIENT_ON
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_UNLIT

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_SHADOWCOORDS
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma shader_feature_local_fragment _ _RECEIVE_SHADOWS_OFF
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON



			#pragma multi_compile _ _LIGHT_LAYERS


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
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 positionWSAndFogFactor : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				float4 dynamicLightmapUV : TEXCOORD4;
				#if defined(OUTPUT_SH4) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
				float4 probeOcclusion : TEXCOORD5;
				#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _TextureRamp_ST;
			float4 _ColorNoiseTexture_ST;
			float4 _ColorTint;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureRamp);       SAMPLER(sampler_TextureRamp);
			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_ColorNoiseTexture); SAMPLER(sampler_ColorNoiseTexture);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			half3 ASEIndirectDiffuse( PackedVaryings input, half3 normalWS, float3 positionWS, half3 viewDirWS )
			{
			#if defined( DYNAMICLIGHTMAP_ON )
				return SAMPLE_GI( input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, 0, normalWS );
			#elif defined( LIGHTMAP_ON )
				return SAMPLE_GI( input.lightmapUVOrVertexSH.xy, 0, normalWS );
			#elif defined( PROBE_VOLUMES_L1 ) || defined( PROBE_VOLUMES_L2 )
				return SampleProbeVolumePixel( SampleSH( normalWS ), positionWS, normalWS, viewDirWS, input.positionCS.xy );
			#else
				return SampleSH( normalWS );
			#endif
			}
			
			half4 CalculateShadowMask343_g61147( half4 shadowMaskInput )
			{
				#if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
				half4 shadowMask = shadowMaskInput;
				#elif !defined (LIGHTMAP_ON)
				half4 shadowMask = unity_ProbesOcclusion;
				#else
				half4 shadowMask = half4(1, 1, 1, 1);
				#endif
				return shadowMask;
			}
			
			float3 AdditionalLightsFlatMask171x( float3 WorldPosition, float2 ScreenUV, float4 ShadowMask )
			{
				float3 Color = 0;
				#if defined(_ADDITIONAL_LIGHTS)
					#ifdef _RECEIVE_SHADOWS_OFF
					#define SUM_LIGHTFLAT(Light)\
						Color += Light.color * ( Light.distanceAttenuation );
					#else
					#define SUM_LIGHTFLAT(Light)\
						Color += Light.color * ( Light.distanceAttenuation * Light.shadowAttenuation );
					#endif
					InputData inputData = (InputData)0;
					inputData.normalizedScreenSpaceUV = ScreenUV;
					inputData.positionWS = WorldPosition;
					uint meshRenderingLayers = GetMeshRenderingLayer();
					uint pixelLightCount = GetAdditionalLightsCount();	
					#if USE_CLUSTER_LIGHT_LOOP
					[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
					{
						CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
						Light light = GetAdditionalLight(lightIndex, WorldPosition, ShadowMask);
						#ifdef _LIGHT_LAYERS
						if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
						#endif
						{
							SUM_LIGHTFLAT( light );
						}
					}
					#endif
					LIGHT_LOOP_BEGIN( pixelLightCount )
						Light light = GetAdditionalLight(lightIndex, WorldPosition, ShadowMask);
						#ifdef _LIGHT_LAYERS
						if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
						#endif
						{
							SUM_LIGHTFLAT( light );
						}
					LIGHT_LOOP_END
				#endif
				return Color;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				

				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				OUTPUT_LIGHTMAP_UV( input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy );
				#if !defined( OUTPUT_SH4 )
				OUTPUT_SH( ase_positionWS, ase_normalWS, GetWorldSpaceNormalizeViewDir( ase_positionWS ), output.lightmapUVOrVertexSH.xyz );
				#elif UNITY_VERSION > 60000009
				OUTPUT_SH4( ase_positionWS, ase_normalWS, GetWorldSpaceNormalizeViewDir( ase_positionWS ), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion );
				#else
				OUTPUT_SH4( ase_positionWS, ase_normalWS, GetWorldSpaceNormalizeViewDir( ase_positionWS ), output.lightmapUVOrVertexSH.xyz );
				#endif
				#if defined( DYNAMICLIGHTMAP_ON )
				output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				
				output.ase_texcoord2.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord2.z = AGrassGetInteractionStrength(ase_positionWS, input.ase_color);
				output.ase_texcoord2.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS );

				float fogFactor = 0;
				#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
					fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
				#endif

				output.positionCS = vertexInput.positionCS;
				output.positionWSAndFogFactor = float4( vertexInput.positionWS, fogFactor );
				output.normalWS = normalInput.normalWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;

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
				output.ase_color = input.ase_color;
				output.ase_texcoord = input.ase_texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
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
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
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
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWSAndFogFactor.xyz );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWSAndFogFactor.xyz;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				half3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				half3 NormalWS = normalize( input.normalWS );

				float3 normalizedWorldNormal = normalize( NormalWS );
				float dotResult164 = dot( normalizedWorldNormal , SafeNormalize( _MainLightPosition.xyz ) );
				float temp_output_168_0 = (dotResult164*0.495 + 0.5);
				float2 temp_cast_0 = (temp_output_168_0).xx;
				float ase_lightAtten = 0;
				Light ase_mainLight = GetMainLight( ShadowCoord );
				#ifdef _RECEIVE_SHADOWS_OFF
				ase_lightAtten = ase_mainLight.distanceAttenuation;
				#else
				ase_lightAtten = ase_mainLight.distanceAttenuation * ase_mainLight.shadowAttenuation;
				#endif
				float ase_lightIntensity = max( max( _MainLightColor.r, _MainLightColor.g ), _MainLightColor.b ) + 1e-7;
				float4 ase_lightColor = float4( _MainLightColor.rgb / ase_lightIntensity, ase_lightIntensity );
				float3 temp_output_160_0 = ( ase_lightAtten * ase_lightColor.rgb * ase_lightColor.a );
				float3 break161 = temp_output_160_0;
				float3 temp_cast_1 = (1.0).xxx;
				#ifdef UNITY_PASS_FORWARDBASE
				float3 staticSwitch171 = temp_cast_1;
				#else
				float3 staticSwitch171 = temp_output_160_0;
				#endif
				float2 uv_TextureSample = input.ase_texcoord2.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float2 uv_TextureRamp = temp_cast_0 * _TextureRamp_ST.xy + _TextureRamp_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float3 bakedGI174 = ASEIndirectDiffuse( input, NormalWS, PositionWS, ViewDirWS );
				MixRealtimeAndBakedGI( ase_mainLight, NormalWS, bakedGI174, half4( 0, 0, 0, 0 ) );
				float3 WorldPosition288_g61147 = PositionWS;
				float3 WorldPosition339_g61147 = WorldPosition288_g61147;
				float2 ScreenUV286_g61147 = (ScreenPosNorm).xy;
				float2 ScreenUV339_g61147 = ScreenUV286_g61147;
				half4 shadowMaskInput = half4(1, 1, 1, 1);
				#if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
					shadowMaskInput = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif
				half4 localCalculateShadowMask343_g61147 = CalculateShadowMask343_g61147( shadowMaskInput );
				float4 ShadowMask360_g61147 = localCalculateShadowMask343_g61147;
				float4 ShadowMask339_g61147 = ShadowMask360_g61147;
				float3 localAdditionalLightsFlatMask171x339_g61147 = AdditionalLightsFlatMask171x( WorldPosition339_g61147 , ScreenUV339_g61147 , ShadowMask339_g61147 );
				float4 CustomLighting180 = ( ( ( SAMPLE_TEXTURE2D( _TextureRamp,       sampler_TextureRamp,       uv_TextureRamp ) * ( temp_output_168_0 * max( max( break161.x , break161.y ) , break161.z ) ) ) * ( float4( staticSwitch171 , 0.0 ) * tex2DNode156 ) ) + ( tex2DNode156 * float4( bakedGI174 , 0.0 ) ) + ( tex2DNode156 * float4( localAdditionalLightsFlatMask171x339_g61147 , 0.0 ) ) );
				

				float2 colorNoiseUV = ( PositionWS.xz * _ColorNoiseScale ) * _ColorNoiseTexture_ST.xy + _ColorNoiseTexture_ST.zw;
				float colorNoiseValue = saturate( SAMPLE_TEXTURE2D( _ColorNoiseTexture, sampler_ColorNoiseTexture, colorNoiseUV ).r );
				float3 colorNoiseTint = lerp( _ColorNoiseLowColor.rgb, _ColorNoiseHighColor.rgb, colorNoiseValue );
				float3 colorVariation = lerp( float3( 1.0, 1.0, 1.0 ), colorNoiseTint, _ColorNoiseStrength );
				

				float4 finalColor = CustomLighting180;
				finalColor.rgb *= colorVariation;
				
				#if defined(_GRADIENT_ON)

					float gradientFactor = saturate((input.ase_texcoord2.y + _GradientOffset) * _GradientContrast);
					float tintFactor = saturate(input.ase_texcoord2.z * _TrailTintStrength);
					float3 baseGradColor = lerp(_GradientBottomColor.rgb, _GradientTopColor.rgb, gradientFactor);
					float3 trailTintColor = lerp(_TrailTintColor.rgb, _TrailTintTopColor.rgb, gradientFactor);
					finalColor.rgb *= lerp(baseGradColor, trailTintColor, tintFactor);
				#else
					float tintFactor = saturate(input.ase_texcoord2.z * _TrailTintStrength);
					finalColor.rgb *= lerp(_ColorTint.rgb, _TrailTintColor.rgb, tintFactor);
				#endif
				
				float OpacityMask157 = tex2DNode156.a;
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = finalColor.rgb;
				float Alpha = OpacityMask157;
				Alpha *= AGrassDistanceFade(PositionWS, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;
				float AlphaClipThresholdShadow = _AlphaCutoff;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
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
				inputData.normalWS = NormalWS;
				inputData.viewDirectionWS = ViewDirWS;

				#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
					float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
					AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
					Color.rgb *= aoFactor.directAmbientOcclusion;
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.positionWSAndFogFactor.w);
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(input.positionCS, Color);
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						Color.rgb = MixFogColor(Color.rgb, half3(0,0,0), inputData.fogCoord);
					#else
						Color.rgb = MixFog(Color.rgb, inputData.fogCoord);
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
					return half4( Color, Alpha );
				#else
					return half4( Color, OutputAlpha( Alpha, isTransparent ) );
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

			#define _ALPHATEST_ON 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0


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
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorTint;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			
			float3 _LightDirection;
			float3 _LightPosition;

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				
				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				output.ase_texcoord.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				half3 normalWS = TransformObjectToWorldDir(input.normalOS);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				output.positionCS = positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			half4 frag(PackedVaryings input
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float2 uv_TextureSample = input.ase_texcoord.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float OpacityMask157 = tex2DNode156.a;
				

				float Alpha = OpacityMask157;
				Alpha *= AGrassDistanceFade(GetObjectToWorldMatrix()._m03_m13_m23, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;
				float AlphaClipThresholdShadow = _AlphaCutoff;

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

			#define _ALPHATEST_ON 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0


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
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorTint;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				
				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				output.ase_texcoord.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			half4 frag(PackedVaryings input
						#if defined( ASE_DEPTH_WRITE_ON )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float2 uv_TextureSample = input.ase_texcoord.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float OpacityMask157 = tex2DNode156.a;
				

				float Alpha = OpacityMask157;
				Alpha *= AGrassDistanceFade(GetObjectToWorldMatrix()._m03_m13_m23, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;

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
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull [_CullMode]
			AlphaToMask Off

			HLSLPROGRAM

			#define _ALPHATEST_ON 1
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_TEXTURE_COORDINATES0


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorTint;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			
			int _ObjectId;
			int _PassValue;

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

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				

				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				output.ase_texcoord.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			half4 frag(PackedVaryings input ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float2 uv_TextureSample = input.ase_texcoord.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float OpacityMask157 = tex2DNode156.a;
				

				surfaceDescription.Alpha = OpacityMask157;
				surfaceDescription.Alpha *= AGrassDistanceFade(GetObjectToWorldMatrix()._m03_m13_m23, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#define _ALPHATEST_ON 1
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorTint;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			
			float4 _SelectionID;

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

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				

				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				output.ase_texcoord.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			half4 frag(PackedVaryings input ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float2 uv_TextureSample = input.ase_texcoord.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float OpacityMask157 = tex2DNode156.a;
				

				surfaceDescription.Alpha = OpacityMask157;
				surfaceDescription.Alpha *= AGrassDistanceFade(GetObjectToWorldMatrix()._m03_m13_m23, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				surfaceDescription.AlphaClipThreshold = _AlphaCutoff;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;
				outColor = unity_SelectionID;

				return outColor;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormalsOnly" }

			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

        	#define _ALPHATEST_ON 1
        	#pragma multi_compile_instancing
        	#pragma multi_compile _ LOD_FADE_CROSSFADE
        	#define ASE_FOG 1
        	#define ASE_VERSION 19904
        	#define ASE_SRP_VERSION 170100


        	#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define VARYINGS_NEED_NORMAL_WS

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0


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
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				half3 normalWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorTint;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
			float _TessPhongStrength;
			float _TessValue;
			float _TessMin;
			float _TessMax;
			float _TessEdgeLength;
			float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);


			
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

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );
				

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );
				
				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );
				
				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );
				
				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);
				
				output.ase_texcoord1.xy = input.ase_texcoord.xy;
				

				output.ase_texcoord1.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS );

				output.positionCS = vertexInput.positionCS;
				output.normalWS = normalInput.normalWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			void frag(PackedVaryings input
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

				half3 NormalWS = normalize( input.normalWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float2 uv_TextureSample = input.ase_texcoord1.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );
				float OpacityMask157 = tex2DNode156.a;
				

				float Alpha = OpacityMask157;
				Alpha *= AGrassDistanceFade(GetObjectToWorldMatrix()._m03_m13_m23, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
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
					outNormalWS = half4(NormalizeNormalPerPixel( NormalWS ), 0.0);
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
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#define _ALPHATEST_ON 1
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			#pragma vertex vert
			#pragma fragment frag

            #define SHADERPASS SHADERPASS_MOTION_VECTORS

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
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
			float4 _TextureSample_ST;
			float4 _ColorTint;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
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
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

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

				#if defined(APPLICATION_SPACE_WARP_MOTION)
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
				Alpha *= AGrassDistanceFade(PositionWS, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined( ASE_CHANGES_WORLD_POS )
					float3 positionOS = mul( GetWorldToObjectMatrix(),  float4( PositionWS, 1.0 ) ).xyz;
					float3 previousPositionWS = mul( GetPrevObjectToWorldMatrix(),  float4( positionOS, 1.0 ) ).xyz;
					input.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, float4( PositionWS, 1.0 ) );
					input.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, float4( previousPositionWS, 1.0 ) );
				#endif

				#if defined( LOD_FADE_CROSSFADE )
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				#if defined(APPLICATION_SPACE_WARP_MOTION)
					return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
				#else
					return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
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

			Cull [_CullMode]

			HLSLPROGRAM

			

			#define _ALPHATEST_ON 1
			#pragma shader_feature_local _GRADIENT_ON
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170100


			

			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_GBUFFER

			
			#if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0

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
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(DYNAMICLIGHTMAP_ON)
				float4 dynamicLightmapUV : TEXCOORD4;
				#endif
				#if defined(OUTPUT_SH4) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
				float4 probeOcclusion : TEXCOORD5;
				#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _WindNoiseTexture_ST;
			float4 _WindNoiseTexture2_ST;
			float4 _ColorNoiseTexture_ST;
			float4 _ColorTint;
			float4 _TrailTintColor;
			float4 _TrailTintTopColor;
			float _TrailTintStrength;
			float4 _ColorNoiseLowColor;
			float4 _ColorNoiseHighColor;
			float4 _GradientTopColor;
			float4 _GradientBottomColor;
			float _GradientOffset;
			float _GradientContrast;
			float _InteractionStrength;
			float _PushDownAmount;
			float _InteractionMultiplier;
			float _WindJitter;
			float _WindScroll;
			float _WindJitter2;
			float _WindScroll2;
			float _WindBlend;
			float _ColorNoiseScale;
			float _ColorNoiseStrength;
			float _UsePerspectiveCorrection;
			float _PerspectiveCorrectionStrength;
			float _PerspectiveTopDownStart;
			float _PerspectiveHeightPower;
			float _PerspectiveHeightStart;
			float _PerspectiveMaxOffset;
			float _UseDistanceFade;
			float _DistanceFadeMode;
			float _DistanceFadeStart;
			float _DistanceFadeEnd;
			float _CullAtFadeEnd;
			float _AlphaCutoff;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			float _BendPivotOffset;
			CBUFFER_END


			#include "AGrassInstancing.hlsl"

			TEXTURE2D(_TextureSample);     SAMPLER(sampler_TextureSample);
			TEXTURE2D(_ColorNoiseTexture); SAMPLER(sampler_ColorNoiseTexture);
			TEXTURE2D(_WindNoiseTexture);  SAMPLER(sampler_WindNoiseTexture);
			TEXTURE2D(_WindNoiseTexture2); SAMPLER(sampler_WindNoiseTexture2);

			#if UNITY_VERSION >= 600000
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
			#else
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
			#endif

			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float2 appendResult60 = (float2(ase_positionWS.x , ase_positionWS.z));
				float2 globalWindDirection = GetAGrassWindDirection();
				float2 temp_output_61_0 = RotateAGrassWindUV( appendResult60 * 0.1 , globalWindDirection );

				float2 panner63 = ( (  (0.0 + ( ( _WindScroll * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 saferPower72 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner63,   0.0 ) );
				float2 panner74 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter * _AGrassGlobalWind1Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture_ST.xy + _WindNoiseTexture_ST.zw;
				float4 wind1 = ( pow( saferPower72 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture,  sampler_WindNoiseTexture,  panner74,   0.0 ) );

				float2 panner63_2 = ( (  (0.0 + ( ( _WindScroll2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.3 - 0.0 ) / ( 1.0 - 0.0 ) ) * _TimeParameters.x ) * globalWindDirection + temp_output_61_0) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 saferPower72_2 = abs( SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner63_2, 0.0 ) );
				float2 panner74_2 = ( ( _TimeParameters.x *  (0.0 + ( ( _WindJitter2 * _AGrassGlobalWind2Multiplier ) - 0.0 ) * ( 0.5 - 0.0 ) / ( 1.0 - 0.0 ) ) ) * globalWindDirection + ( temp_output_61_0 * float2( 2,2 ) )) * _WindNoiseTexture2_ST.xy + _WindNoiseTexture2_ST.zw;
				float4 wind2 = ( pow( saferPower72_2 , 2.5 ) * SAMPLE_TEXTURE2D_LOD( _WindNoiseTexture2, sampler_WindNoiseTexture2, panner74_2, 0.0 ) );

				float4 _bendAdjColor = input.ase_color;
				_bendAdjColor.rgb *= saturate((input.ase_texcoord.y - _BendPivotOffset) / max(0.001, 1.0 - _BendPivotOffset));
				_bendAdjColor.a = input.ase_color.a;
				float4 WindScroll68 = ( lerp(wind1, wind2, _WindBlend) * _bendAdjColor );

				float3 interactionOffset = AGrassComputeInteractionOffset(ase_positionWS, _bendAdjColor);

				output.ase_texcoord2.xy = input.ase_texcoord.xy;
				output.ase_texcoord2.z = AGrassGetInteractionStrength(ase_positionWS, input.ase_color);
				output.ase_texcoord2.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float perspectiveStrength = _PerspectiveCorrectionStrength * saturate(_UsePerspectiveCorrection);
				float3 perspectiveOffset = AGrassPerspectiveCorrectionOffset(
					ase_positionWS,
					GetCameraPositionWS(),
					input.ase_texcoord.y,
					perspectiveStrength,
					_PerspectiveTopDownStart,
					_PerspectiveHeightPower,
					_PerspectiveHeightStart,
					_PerspectiveMaxOffset);

				float3 vertexValue = WindScroll68.rgb + interactionOffset + perspectiveOffset;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS );

				OUTPUT_LIGHTMAP_UV( input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy );
				#if !defined( OUTPUT_SH4 )
				OUTPUT_SH( vertexInput.positionWS, normalInput.normalWS, GetWorldSpaceNormalizeViewDir( vertexInput.positionWS ), output.lightmapUVOrVertexSH.xyz );
				#elif UNITY_VERSION > 60000009
				OUTPUT_SH4( vertexInput.positionWS, normalInput.normalWS, GetWorldSpaceNormalizeViewDir( vertexInput.positionWS ), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion );
				#else
				OUTPUT_SH4( vertexInput.positionWS, normalInput.normalWS, GetWorldSpaceNormalizeViewDir( vertexInput.positionWS ), output.lightmapUVOrVertexSH.xyz );
				#endif
				#if defined( DYNAMICLIGHTMAP_ON )
				output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
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

			GBufferFragOutput frag ( PackedVaryings input
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

				float3 PositionWS = input.positionWS;
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				half3 NormalWS = normalize( input.normalWS );

				float2 uv_TextureSample = input.ase_texcoord2.xy * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float4 tex2DNode156 = SAMPLE_TEXTURE2D( _TextureSample,     sampler_TextureSample,     uv_TextureSample );

				float2 colorNoiseUV = ( PositionWS.xz * _ColorNoiseScale ) * _ColorNoiseTexture_ST.xy + _ColorNoiseTexture_ST.zw;
				float colorNoiseValue = saturate( SAMPLE_TEXTURE2D( _ColorNoiseTexture, sampler_ColorNoiseTexture, colorNoiseUV ).r );
				float3 colorNoiseTint = lerp( _ColorNoiseLowColor.rgb, _ColorNoiseHighColor.rgb, colorNoiseValue );
				float3 colorVariation = lerp( float3( 1.0, 1.0, 1.0 ), colorNoiseTint, _ColorNoiseStrength );

				float3 albedo = tex2DNode156.rgb * colorVariation;

				#if defined(_GRADIENT_ON)
					float gradientFactor = saturate((input.ase_texcoord2.y + _GradientOffset) * _GradientContrast);
					float tintFactor = saturate(input.ase_texcoord2.z * _TrailTintStrength);
					float3 baseGradColor = lerp(_GradientBottomColor.rgb, _GradientTopColor.rgb, gradientFactor);
					float3 trailTintColor = lerp(_TrailTintColor.rgb, _TrailTintTopColor.rgb, gradientFactor);
					albedo *= lerp(baseGradColor, trailTintColor, tintFactor);
				#else
					float tintFactor = saturate(input.ase_texcoord2.z * _TrailTintStrength);
					albedo *= lerp(_ColorTint.rgb, _TrailTintColor.rgb, tintFactor);
				#endif

				float OpacityMask157 = tex2DNode156.a;

				float3 Color = albedo;
				float Alpha = OpacityMask157;
				Alpha *= AGrassDistanceFade(PositionWS, GetCameraPositionWS(), input.positionCS, _UseDistanceFade, _DistanceFadeMode, _DistanceFadeStart, _DistanceFadeEnd, _CullAtFadeEnd);
				float AlphaClipThreshold = _AlphaCutoff;

				#if defined( ASE_DEPTH_WRITE_ON )
					float DeviceDepth = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = float4( input.positionCS.xy, ClipPos.zw / ClipPos.w );
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.normalWS = NormalWS;
				inputData.viewDirectionWS = ViewDirWS;

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(input.positionCS, Color);
				#endif

				#if defined( ASE_DEPTH_WRITE_ON )
					outputDepth = DeviceDepth;
				#endif

				SurfaceData surfaceData = (SurfaceData)0;
				surfaceData.albedo = Color;
				surfaceData.alpha = Alpha;

			#if defined( _SCREEN_SPACE_OCCLUSION )
				float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV( input.positionCS );
				AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion( normalizedScreenSpaceUV );
				surfaceData.occlusion = aoFactor.directAmbientOcclusion;
			#else
				surfaceData.occlusion = 1;
			#endif

				#if defined( DYNAMICLIGHTMAP_ON )
					half3 bakedGI = SAMPLE_GI( input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, 0, NormalWS );
				#elif defined( LIGHTMAP_ON )
					half3 bakedGI = SAMPLE_GI( input.lightmapUVOrVertexSH.xy, 0, NormalWS );
				#elif defined( PROBE_VOLUMES_L1 ) || defined( PROBE_VOLUMES_L2 )
					half3 bakedGI = SampleProbeVolumePixel( SampleSH( NormalWS ), PositionWS, NormalWS, ViewDirWS, input.positionCS.xy );
				#else
					half3 bakedGI = SampleSH( NormalWS );
				#endif
				inputData.bakedGI = bakedGI;
				BRDFData brdfData;
				InitializeBRDFData(surfaceData, brdfData);
				half3 globalIllumination = bakedGI * brdfData.diffuse;
				return PackGBuffersSurfaceData( surfaceData, inputData, globalIllumination );
			}

			ENDHLSL
		}
		
	}

	CustomEditor "AGrassShaderGUI"
}



