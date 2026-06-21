// ================================================================
// Jundroo/ReEntry/ReEntryAirstreamMesh
// ================================================================
// FlarePath plasma trail shader. Replaces the game's reentry effect.
//
// Game uniforms (set by CraftAirstreamRenderer.UpdateMesh):
//   _AirstreamTex    - craft silhouette depth RT
//   _ConeField       - GPU cone field (R=depth, G=craftMask)
//   _FrustumDepth    - depth range of airstream camera
//   _ForwardBias     - forward bias to prevent z-fighting
//   _IntensityScale  - master intensity (0..1)
//   _NoiseTex        - noise texture
//   _NoiseConeShape  - Mach-cone shape factor
//   _NoiseConeMach   - Mach influence on cone noise
//   _NoiseAmount     - noise content
//   _NoiseStretch    - radial noise stretch
//   _NoiseScale      - noise UV scale
//   _ReentryTint     - reentry color (HDR)
//   _FrontBoost      - nose-tip boost
//   _SkirtStrength   - trail strength
//   _Verts           - cone grid verts per side
//
// FlarePath uniforms (injected by Harmony patch):
//   _FlarePathMode          0=game, 1=plasma, 2=plasma+streaks, 3=dramatic
//   _FlarePathIntensity     global strength multiplier
//   _FlarePathTrailScale   trail length scale
//   _FlarePathOpacity      master opacity
//   _FlarePathPlasmaHeat   hot core intensity (0-1)
//   _FlarePathStreakStrength streak intensity (0-1)
//   _FlarePathStreakThreshold streak threshold (-1 to 1)
//   _FlarePathWrapStrength Fresnel edge glow strength
//   _FlarePathBlueMultiplier cold tail blue factor
//   _FlarePathLengthBoost  extra trail length
// ================================================================

Shader "FlarePath/ReEntryPlasma"
{
    Properties
    {
        [Header(Game Uniforms)]
        _AirstreamTex ("Airstream Tex", 2D) = "" {}
        _ConeField ("Cone Field", 2D) = "" {}
        _FrustumDepth ("Frustum Depth", Float) = 0
        _ForwardBias ("Forward Bias", Float) = 0
        _IntensityScale ("Intensity Scale", Float) = 1
        _NoiseTex ("Noise Tex", 2D) = "" {}
        _NoiseConeShape ("Noise Cone Shape", Float) = 0
        _NoiseConeMach ("Noise Cone Mach", Float) = 0
        _NoiseAmount ("Noise Amount", Float) = 1
        _NoiseStretch ("Noise Stretch", Float) = 1
        _NoiseScale ("Noise Scale", Float) = 80
        _ReentryTint ("Reentry Tint", Color) = (1, 0.5, 0, 0)
        _FrontBoost ("Front Boost", Float) = 1
        _SkirtStrength ("Skirt Strength", Float) = 1
        _Verts ("Grid Verts", Float) = 64

        [Header(FlarePath Tuning)]
        _FlarePathMode ("FP Mode (0=game,1=plasma,2=streaks,3=dramatic)", Float) = 1
        _FlarePathIntensity ("FP Intensity", Float) = 1.0
        _FlarePathTrailScale ("FP Trail Scale", Float) = 1.0
        _FlarePathOpacity ("FP Opacity", Float) = 1.0
        _FlarePathPlasmaHeat ("FP Plasma Heat", Float) = 0.5
        _FlarePathStreakStrength ("FP Streak Strength", Float) = 0.5
        _FlarePathStreakThreshold ("FP Streak Threshold", Float) = -0.2
        _FlarePathWrapStrength ("FP Wrap Strength", Float) = 0.5
        _FlarePathBlueMultiplier ("FP Blue Multiplier", Float) = 0.1
        _FlarePathLengthBoost ("FP Length Boost", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }
        LOD 200

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        ColorMask RGB

        CGINCLUDE
        #include "UnityCG.cginc"
        ENDCG

        Pass
        {
            Name "ReEntry Airstream Mesh"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma exclude_renderers d3d9

            // ----------------------------------------
            // Game uniforms
            // ----------------------------------------
            sampler2D _AirstreamTex;
            sampler2D _ConeField;
            float _FrustumDepth;
            float _ForwardBias;
            float _IntensityScale;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float _NoiseConeShape;
            float _NoiseConeMach;
            float _NoiseAmount;
            float _NoiseStretch;
            float _NoiseScale;
            float4 _ReentryTint;
            float _FrontBoost;
            float _SkirtStrength;
            float _Verts;

            // ----------------------------------------
            // FlarePath tuning uniforms
            // ----------------------------------------
            float _FlarePathMode;
            float _FlarePathIntensity;
            float _FlarePathTrailScale;
            float _FlarePathOpacity;
            float _FlarePathPlasmaHeat;
            float _FlarePathStreakStrength;
            float _FlarePathStreakThreshold;
            float _FlarePathWrapStrength;
            float _FlarePathBlueMultiplier;
            float _FlarePathLengthBoost;

            // ----------------------------------------
            // Vertex input
            // ----------------------------------------
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
                float2 coneFieldUV : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
                float3 worldNormal : TEXCOORD5;
                float noiseDepth : TEXCOORD6;
                float coneShape : TEXCOORD7;
                float trailT : TEXCOORD8;   // 0=front(hot), 1=back(cold)
                float machCone : TEXCOORD9;  // 0=inside cone, 1=outside
                UNITY_FOG_COORDS(10)
            };

            // ----------------------------------------
            // Vertex Shader
            // ----------------------------------------
            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv;
                o.coneFieldUV = v.uv;

                float4 cone = tex2Dlod(_ConeField, float4(v.uv, 0, 0));
                float coneDepth = cone.r;

                float adjustedDepth = coneDepth * _FrustumDepth * 0.5 * _FlarePathLengthBoost
                                    + _ForwardBias;

                float3 displaced = v.vertex.xyz;
                displaced.z += adjustedDepth;

                o.pos = UnityObjectToClipPos(displaced);
                o.depth = coneDepth;
                o.noiseDepth = coneDepth;
                o.coneShape = cone.r;

                // trailT: front of cone (nose) = 0, tail = 1
                o.trailT = v.uv.y;

                // machCone: 0=deep inside (hot), 1=outside (cold)
                // coneDepth: 0=inside craft shadow, ~0.1-0.3=Mach surface, 1=sky
                o.machCone = saturate(coneDepth * 2.5);

                o.worldPos = UnityObjectToWorldDir(displaced.xyz);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.worldNormal = float3(0, 0, 1);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // ----------------------------------------
            // HDR plasma color palette — NOT tied to game tint
            // 0.0 = ice blue, 1.0 = white-hot core
            // ----------------------------------------
            float3 PlasmaColor(float temp)
            {
                temp = saturate(temp);
                // Ice blue → deep purple → red-orange → yellow-white
                float3 iceBlue   = float3(0.15, 0.3,  1.8);   // coldest edge
                float3 purple    = float3(0.6,  0.1,  0.9);
                float3 redOrange = float3(2.2,  0.4,  0.05);
                float3 yellow    = float3(3.0,  2.0,  0.2);
                float3 white     = float3(4.0,  4.0,  3.5);   // white-hot HDR

                if (temp < 0.2)
                    return lerp(iceBlue, purple,    temp * 5.0);
                else if (temp < 0.4)
                    return lerp(purple, redOrange,  (temp - 0.2) * 5.0);
                else if (temp < 0.65)
                    return lerp(redOrange, yellow,  (temp - 0.4) * 4.0);
                else
                    return lerp(yellow, white,       (temp - 0.65) * 2.86);
            }

            // ----------------------------------------
            // Multi-octave plasma noise
            // ----------------------------------------
            float PlasmaNoise(float2 uv, float time)
            {
                float2 scroll = float2(uv.x * _NoiseStretch, uv.y * 3.0 + time * 0.4);
                float n = tex2D(_NoiseTex, scroll * 0.5).r * 0.5;
                n += tex2D(_NoiseTex, scroll * 1.5 + float2(time * 0.15, -time * 0.25)).r * 0.25;
                n += tex2D(_NoiseTex, scroll * 3.0 - float2(time * 0.05, time * 0.1)).r * 0.125;
                n += tex2D(_NoiseTex, scroll * 6.0 + float2(-time * 0.08, time * 0.05)).r * 0.0625;
                return n;
            }

            // ----------------------------------------
            // Fast turbulence layer
            // ----------------------------------------
            float Turbulence(float2 uv, float time)
            {
                float t = tex2D(_NoiseTex, float2(uv.x * 8.0 - time * 0.3,
                                                   uv.y * 4.0 + time * 0.6)).r;
                t *= tex2D(_NoiseTex, float2(uv.x * 12.0 + time * 0.2,
                                             uv.y * 8.0 - time * 0.4)).g;
                return t;
            }

            // ----------------------------------------
            // Mode 0: game default — unchanged behavior
            // ----------------------------------------
            half4 GameMode(v2f i, float coneDepth, float coneMask)
            {
                float heatCore = saturate(1.0 - coneDepth * 3.0);
                float heatSkirt = saturate(coneDepth * 2.0) * _SkirtStrength;
                float heat = lerp(heatSkirt, heatCore, 0.7) * _IntensityScale;

                float3 col = PlasmaColor(heat);

                float4 tint = _ReentryTint;
                col = lerp(col, col * tint.rgb * 2.0, tint.a);

                float frontBoost = pow(saturate(1.0 - i.trailT), 2.0) * _FrontBoost;
                col += PlasmaColor(1.0) * frontBoost * _IntensityScale * 0.3;

                float noise = PlasmaNoise(i.uv, _Time.y * 0.3);
                col *= (1.0 + noise * 0.3 * _NoiseAmount);

                float a = saturate(heat * 2.0) * _IntensityScale;
                if (a < 0.005) discard;
                return half4(col, a);
            }

            // ----------------------------------------
            // Mode 1: FlarePath plasma — uses trail position,
            // not coneDepth, for completely different look from game
            // ----------------------------------------
            half4 PlasmaMode(v2f i, float coneDepth, float coneMask, float intensity)
            {
                // Heat is defined by position on the trail, NOT by coneDepth
                // Front 30% of trail (trailT < 0.3) = very hot plasma core
                // Back 70% = cooling exhaust
                float coreHeat  = smoothstep(0.3, 0.0, i.trailT);           // 1 at nose, 0 at 30%
                float skirtHeat = smoothstep(0.0, 0.6, i.trailT) * (1.0 - coreHeat); // rises after 30%, fades core

                // Cone shape: visible near Mach surface
                float coneHeat  = (1.0 - i.machCone) * (1.0 - i.trailT * 0.7);

                // Combine — core dominates
                float heat = max(coreHeat * (1.0 + _FlarePathPlasmaHeat * 2.0),
                                 skirtHeat * 0.4);
                heat = max(heat, coneHeat * 0.6);
                heat = saturate(heat);

                // Plasma turbulence
                float n1 = PlasmaNoise(i.uv, _Time.y);
                float n2 = Turbulence(i.uv, _Time.y);
                heat += n1 * 0.12;
                heat += n2 * 0.06 * heat;
                heat = saturate(heat);

                // Color
                float3 col = PlasmaColor(heat);

                // Blue Fresnel ring at Mach surface — strong signature
                float edge = abs(i.uv.x * 2.0 - 1.0);
                float fresnel = pow(1.0 - edge, 2.5);
                fresnel *= (1.0 - i.trailT * 0.8); // stronger at front
                fresnel *= _FlarePathWrapStrength;
                col += float3(0.2, 0.4, 2.5) * fresnel * 1.2;

                // Cold blue exhaust tail — starts at trailT=0.3, grows to full at 0.8
                float coldMask = smoothstep(0.3, 0.85, i.trailT);
                float3 coldTail = lerp(
                    float3(0.1, 0.2, 1.5),   // electric blue
                    float3(0.3, 0.1, 0.8),    // violet
                    n1
                ) * _FlarePathBlueMultiplier * 4.0;
                col = lerp(col, coldTail, coldMask * 0.8);

                // Nose hot spot (extra boost beyond PlasmaColor white)
                float nose = pow(saturate(1.0 - i.trailT), 3.5);
                col += float3(3.5, 3.0, 2.0) * nose * 0.6;

                // Slight game tint (less than game default to preserve FlarePath colors)
                col = lerp(col, col * _ReentryTint.rgb * 1.5, _ReentryTint.a * 0.4);

                // Alpha — plasma is opaque at core, transparent at tail
                float a = saturate(heat * 2.0);
                a = lerp(a, 1.0, fresnel * 0.4);
                a *= _FlarePathOpacity * intensity * 1.5;
                a = saturate(a);

                if (a < 0.005) discard;
                return half4(col, a);
            }

            // ----------------------------------------
            // Mode 2: Plasma + dramatic hot streaks
            // ----------------------------------------
            half4 StreaksMode(v2f i, float coneDepth, float coneMask, float intensity)
            {
                half4 base = PlasmaMode(i, coneDepth, coneMask, intensity);

                // Streaks: vertical-ish bolts down the trail
                float sNoise = tex2D(_NoiseTex,
                    float2(i.uv.x * 0.3 + _Time.y * 0.05,
                           i.trailT * 2.5 - _Time.y * 0.8)).r;

                // Threshold: strength=0 → threshold=1 (no streaks), strength=1 → threshold=0.3 (many streaks)
                float threshold = lerp(0.3, 1.0, 1.0 - _FlarePathStreakStrength);
                bool streakOn = sNoise > threshold
                             && i.trailT > 0.05
                             && _FlarePathStreakStrength > 0.05;

                if (streakOn)
                {
                    // Streak is a bright white-yellow bolt
                    float3 streakCol = lerp(
                        float3(3.0, 1.2, 0.1),   // orange streak
                        float3(5.0, 5.0, 4.5),   // white-hot core
                        sNoise
                    );
                    base.rgb = lerp(base.rgb, streakCol, 0.75);
                    base.a = saturate(base.a + 0.35);
                }

                return base;
            }

            // ----------------------------------------
            // Mode 3: Dramatic — maximum intensity, bloom-ready
            // ----------------------------------------
            half4 DramaticMode(v2f i, float coneDepth, float coneMask, float intensity)
            {
                half4 base = PlasmaMode(i, coneDepth, coneMask, intensity);

                // Sharper HDR contrast
                base.rgb = pow(base.rgb, 0.85);

                // Extra bright emission core at nose
                float core = pow(saturate(1.0 - i.trailT), 4.0)
                           * pow(saturate(1.0 - coneDepth * 4.0), 2.0);
                base.rgb += float3(5.0, 4.5, 3.0) * core * 2.0;

                // Mach shockwave rings — thin bright bands
                float ring = abs(i.uv.x * 10.0 - floor(i.uv.x * 10.0 + 0.5) - 0.5);
                ring = smoothstep(0.48, 0.5, ring);
                ring *= (1.0 - i.trailT) * (1.0 - i.machCone * 0.5);
                base.rgb += float3(2.5, 1.8, 0.5) * ring * 0.6;
                base.a = saturate(base.a + ring * 0.15);

                // Boost overall brightness for bloom
                base.rgb *= 1.4;

                return base;
            }

            // ----------------------------------------
            // Fragment Shader
            // ----------------------------------------
            half4 frag(v2f i) : SV_Target
            {
                float4 cone = tex2D(_ConeField, i.coneFieldUV);
                float coneDepth = cone.r;
                float coneMask = cone.g;

                if (coneDepth >= 1.9)
                    discard;

                float intensity = _IntensityScale * _FlarePathIntensity;
                int mode = (int)_FlarePathMode;

                half4 result;
                if (mode == 0)
                    result = GameMode(i, coneDepth, coneMask);
                else if (mode == 2)
                    result = StreaksMode(i, coneDepth, coneMask, intensity);
                else if (mode >= 3)
                    result = DramaticMode(i, coneDepth, coneMask, intensity);
                else
                    result = PlasmaMode(i, coneDepth, coneMask, intensity);

                result.a *= saturate(_FlarePathTrailScale);

                UNITY_APPLY_FOG_COLOR(i.fogCoord, result, half4(0, 0, 0, 0));
                return result;
            }
            ENDCG
        }
    }

    FallBack Off
}
