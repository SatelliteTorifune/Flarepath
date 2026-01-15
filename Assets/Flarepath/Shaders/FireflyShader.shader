Shader "Firefly/Firefly"
{
	Properties
	{
		[Header(Textures)]
		_AirstreamTex ("Airstream Texture", 2D) = "" {}
		_NoiseTex ("Noise Texture", 2D) = "" {}
		_DitherTex ("Dither Texture", 2D) = "" {}

		[Space]
		[Header(Values)]
		_TrailAlphaMultiplier ("Trail Alpha Multiplier", Float) = 1
		_BlueMultiplier ("Blue Multiplier", Float) = 0.1
		_OpacityMultiplier ("Opacity Multiplier", Float) = 1
		_WrapFresnelModifier ("Wrap layer fresnel modifier", Float) = 0
		_StreakProbability ("Streak Probability", Float) = 0.1
		_StreakThreshold ("Streak Threshold", Float) = -0.2
		
		[Space]
		[Header(Bowshock)]
		_ShockwaveColor ("Shockwave Color", Color) = (0.2, 0.6, 1.0, 1.0)
		_DisableBowshock ("Disable Bowshock", Int) = 0
		_BowshockForwardDistance ("Bowshock Forward Distance", Float) = 0.3
		_BowshockRadiusScale ("Bowshock Radius Scale", Float) = 1.0
		
	}

	SubShader
	{
		Tags { 
			"Queue"="Transparent" 
			"RenderType" = "Transparent" 
			"IgnoreProjector" = "True"
			"DisableBatching" = "True"
		}
		LOD 100

		HLSLINCLUDE
		#include "UnityCG.cginc"
		#include "EffectPasses/CommonFunctions.cginc"
		
		// 禁用反射探针和环境反射
		#pragma multi_compile_fog
		#pragma exclude_renderers d3d9
		ENDHLSL

		Pass
		{
			Name "Glow Pass"

			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha
			ColorMask RGB
			
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile_fog
			#pragma exclude_renderers d3d9
			
			#include "EffectPasses/GlowPass.cginc"

			ENDHLSL
		}
		
		Pass
		{
			
			Name "Effects Pass"

			ZWrite Off
			ZTest LEqual
			Blend SrcAlpha One
			Cull Off
			ColorMask RGB
			
			HLSLPROGRAM
			
			#pragma require geometry
			#pragma multi_compile_fog
			#pragma exclude_renderers d3d9

			#pragma vertex eff_gs_vert
			#pragma geometry eff_gs_geom
			#pragma fragment eff_gs_frag
			#pragma target 5.0
			#include "EffectPasses/MainPass.cginc"
			
			
			ENDHLSL
		}
		
		Pass
		{
			Name "Bowshock Pass"
			
			ZWrite Off
			Cull Off
			Blend SrcAlpha One
			ZTest LEqual
			ColorMask RGB
			
			HLSLPROGRAM
			
			#pragma require geometry
			#pragma multi_compile_fog
			#pragma exclude_renderers d3d9

			#pragma vertex bs_gs_vert
			#pragma geometry bs_gs_geom
			#pragma fragment bs_gs_frag
			#pragma target 5.0
			
			#include "EffectPasses/BowshockPass.cginc"
			
			ENDHLSL
		}
	}
}
