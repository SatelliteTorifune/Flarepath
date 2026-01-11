// MainPass.cginc (修复版)

float _FxState;
float _AngleOfAttack;
int _Hdr;
int _UnityEditor = 0;
int _VertexSamples = 3;

float _LengthMultiplier;

float _TrailAlphaMultiplier;
float _BlueMultiplier;
float _SideMultiplier;
float _OpacityMultiplier;
float _WrapOpacityMultiplier;
float _WrapFresnelModifier;
float _StreakProbability;
float _StreakThreshold;

float4 _SecondaryColor;
float4 _PrimaryColor;
float4 _TertiaryColor;
float4 _StreakColor;
float4 _LayerColor;
float4 _LayerStreakColor;

float2 _RandomnessFactor;

struct VS_INPUT
{
    float4 position : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct GS_INPUT
{
    float4 position : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD3;
    float4 airstreamNDC : TEXCOORD4;
    float3 velocityOS : TEXCOORD5;
    float3 normalOS : TEXCOORD6;
    float3 viewDir : TEXCOORD7;
}; 

struct GS_DATA
{
    float4 position : SV_POSITION;
    half4 color : COLOR;
    float3 positionWS : TEXCOORD1;
    float3 positionOS : TEXCOORD2;
    float4 screenPos : TEXCOORD4;
    float2 trailPos : TEXCOORD6;
    float layer : TEXCOORD7;
    float4 airstreamNDC : TEXCOORD8;
};

GS_DATA CreateVertex(float3 pos, float layer, float4 airstreamNDC, float trailPosX, float trailPosY, half4 color, half a) 
{
    GS_DATA o;
    o.position = UnityObjectToClipPos(pos);
    o.color = color;
    o.color.a = a;
    o.positionWS = TransformObjectToWorld(pos);
    o.positionOS = pos;
    o.screenPos = o.position;
    o.trailPos = float2(trailPosX, trailPosY);
    o.layer = layer;
    o.airstreamNDC = airstreamNDC;
    return o;
}

GS_INPUT eff_gs_vert(VS_INPUT IN)
{
    GS_INPUT OUT;
    OUT.normal = UnityObjectToWorldNormal(IN.normal);
    OUT.normalOS = normalize(IN.normal);
    OUT.position = IN.position * float4(_EnvelopeScaleFactor, 1);
    OUT.uv = IN.uv;
    OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);
    float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
    OUT.airstreamNDC = float4(airstreamPosition.xyz / airstreamPosition.w, airstreamPosition.w);
    OUT.velocityOS = normalize(UnityWorldToObjectDir(_Velocity));
    OUT.viewDir = WorldSpaceViewDir(IN.position);
    return OUT;
}

[maxvertexcount(36)]
void eff_gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
    /*
    if (_EntryStrength < 50) return;
    uint i = 0;

    float entrySpeed = _EntryStrength / 4000 - 0.08 * _FxState;
    float3 occlusion = float3(
        Shadow(vertex[0].airstreamNDC, -0.003, 1),
        Shadow(vertex[1].airstreamNDC, -0.003, 1),
        Shadow(vertex[2].airstreamNDC, -0.003, 1)
    );
                
    float3 velDots;
    velDots.x = dot(-vertex[0].normal, _Velocity);
    velDots.y = dot(-vertex[1].normal, _Velocity);
    velDots.z = dot(-vertex[2].normal, _Velocity);

    float baseLength = _EntryStrength * 0.0013;
    float maxBaseLength = 5.2;
    float3 noise;
    noise.x = Noise(vertex[0].position.xy + vertex[0].uv, 1) * baseLength * Noise(vertex[0].position.xy + vertex[0].uv, 1) * 5;
    noise.y = Noise(vertex[1].position.xy + vertex[1].uv, 1) * baseLength * Noise(vertex[1].position.xy + vertex[1].uv, 1) * 5;
    noise.z = Noise(vertex[2].position.xy + vertex[2].uv, 1) * baseLength * Noise(vertex[2].position.xy + vertex[2].uv, 1) * 5;

    float3 effectLength = (baseLength + noise) * _LengthMultiplier;
    float3 middleLength = effectLength * 0.2;
    float middleNormalMultiplier = 0.23 + lerp(0.1 * _FxState, 0, saturate((entrySpeed - 0.2) * 4));
    float maxEffectLength = (maxBaseLength * 6) * _LengthMultiplier;
    effectLength *= _ModelScale.y;
    middleLength *= _ModelScale.y;

    for (i = 0; i < 3; i++)
    {
        float velDot = velDots[i];
        if (velDot > -0.1 && occlusion[i] > 0.9)
        {
            uint j = (i + 1) % 3;
            uint k = (j + 1) % 3;
                        
            float edgeLength_j = length(vertex[i].position - vertex[j].position);
            float edgeLength_k = length(vertex[i].position - vertex[k].position);
            float edgeLength = (edgeLength_j + edgeLength_k) / 2 * (1 / _ModelScale.y);
            float edgeMul = clamp(edgeLength / 0.05, 0.1, 40);
            float sideEdgeMul = 1 - saturate(edgeLength - 0.1);
                        
            if (occlusion[k] > occlusion[j] || velDots[k] > velDots[j]) j = k;
                        
            float3 sizeVector = normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));
            float3 side = sizeVector * 0.6 * sideEdgeMul;
            float3 middleSide = side * 1.4 * clamp(entrySpeed, 0.2, 1);
            float3 endSide = side * 2.5 * clamp(entrySpeed, 0.2, 1);
            side *= 0.3;
                        
            float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);
            float vertNoise1 = Noise(vertex[i].position.xy - _Time.y * (1 - _RandomnessFactor.x), 1 + round(_RandomnessFactor.x));
            float vertNoise2 = Noise(vertex[i].position.xy - _Time.x, 1);
                        
            float normalMultiplier = 0.8 * pow(_LengthMultiplier, lerp(5, 1, saturate(_LengthMultiplier))) + lerp(2 * _LengthMultiplier * _FxState, 0, saturate((entrySpeed - 0.2) * 4));

            float4 col = lerp(_PrimaryColor, _SecondaryColor, vertNoise * 0.3);
            float4 middleCol = lerp(col, _SecondaryColor, 0.5);
            float4 endCol = lerp(_SecondaryColor, _TertiaryColor, clamp(entrySpeed, 0, 1.7));
            endCol = lerp(middleCol, endCol, _Hdr);

            float alpha = saturate(entrySpeed / 0.025) * (0.004 * _TrailAlphaMultiplier + vertNoise * 0.004);

            float t = saturate(entrySpeed / 0.25 - 1);
            float fxState = lerp(_FxState, _FxState * 0.05, saturate(sign(_Velocity.y)));
            float interpolation = saturate(t + _FxState);

            col = lerp(1, col, interpolation);
            middleCol = lerp(1, middleCol, interpolation);
            endCol = lerp(1, endCol, interpolation);
                        
            float aoa = pow(saturate(_AngleOfAttack / 20), 4);
            alpha *= saturate(aoa + t + 0.5);
                        
            side *= _ModelScale.x;
            middleSide *= _ModelScale.x;
            endSide *= _ModelScale.x;
            normalMultiplier *= _ModelScale.x;
                        
            effectLength = abs(effectLength);
            middleLength = abs(middleLength);
                        
            float3 trailDir = (vertex[i].position - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed) - vertex[i].position;
            float3 normal = normalize(cross(sizeVector, trailDir));
                        
            float vertFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 2);
            alpha *= saturate(vertFresnel + 0.5 + vertNoise * 0.3) * edgeMul;
                        
            int streakValue = 0;
            float streakNoise = vertNoise2;
            float4 streakColor = lerp(float4(4, 1, 1, 1), float4(1, 4, 1, 1), streakNoise);
            if (vertNoise1 > 0.73 - _StreakProbability && entrySpeed > 0.5 + _StreakThreshold)
            {
                col = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
                middleCol = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
                endCol = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
                effectLength *= 2;
                alpha *= 2;
                streakValue = 1;
            }
                        
            float3 vertex_b0 = vertex[i].position - side;
            float3 vertex_b1 = vertex[j].position + side;
                        
            float3 vertex_m0 = vertex[i].position - middleSide - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
            float3 vertex_m1 = vertex[j].position + middleSide - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
                        
            float3 vertex_t0 = vertex[i].position - endSide - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed;
            float3 vertex_t1 = vertex[j].position + endSide - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed;
                        
            float3 m0_ndc = GetAirstreamNDC(lerp(vertex_b0, vertex_m0, lerp(0.5, 0.1, saturate(entrySpeed - 0.5))));
            float depth = Shadow(m0_ndc, -0.003, 1);
            
            float discardSegment = (depth < 0.9) ? 2.0 : 0.0;

            triStream.Append(CreateVertex(vertex_b0, 0 - discardSegment, vertex[i].airstreamNDC, 0, 0, col, alpha));
            triStream.Append(CreateVertex(vertex_b1, 0 - discardSegment, vertex[j].airstreamNDC, 1, 0, col, alpha));
                        
            triStream.Append(CreateVertex(vertex_m0, 0 - discardSegment, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
            triStream.Append(CreateVertex(vertex_m1, 0 - discardSegment, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
                        
            triStream.Append(CreateVertex(vertex_t0, 0 - discardSegment, vertex[i].airstreamNDC, 0, 1, endCol, 0));
            triStream.Append(CreateVertex(vertex_t1, 0 - discardSegment, vertex[j].airstreamNDC, 1, 1, endCol, 0));

            triStream.RestartStrip();
            
            entrySpeed = clamp(entrySpeed, 0, 1);
                        
            float vertFresnelWrap = Fresnel(vertex[i].normal, vertex[i].viewDir, 1); // 修复：改名避免重复
            vertFresnelWrap += (1 - vertFresnelWrap) * _WrapFresnelModifier;
                        
            middleLength = clamp(middleLength, 0, maxEffectLength * 0.2);
            effectLength = clamp(effectLength * 3.4 * clamp(entrySpeed, 0, 0.6) * _FxState, 0, maxEffectLength * 1.6);
            float normalMultiplierWrap = -2.325 * saturate(pow(_LengthMultiplier, 3)) * _ModelScale.x;
            float middleNormalMultiplierWrap = 0.05;

            alpha *= 0.5 * vertFresnelWrap * min(entrySpeed, 0.7) * _FxState;

            col = _PrimaryColor;
            middleCol = lerp(_LayerColor, lerp(_LayerStreakColor, streakColor, _RandomnessFactor.y), streakValue);
            endCol = lerp(lerp(_LayerColor, _SecondaryColor, 0.5), lerp(_LayerStreakColor, streakColor, _RandomnessFactor.y), streakValue);

            float3 layerOffset = vertex[i].velocityOS * -0.05 * _LengthMultiplier * _ModelScale.y;
                        
            vertex_b0 = vertex[i].position - side + layerOffset;
            vertex_b1 = vertex[j].position + side + layerOffset;
                        
            vertex_m0 = vertex[i].position - middleSide + layerOffset - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplierWrap * entrySpeed * middleNormalMultiplierWrap;
            vertex_m1 = vertex[j].position + middleSide + layerOffset - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplierWrap * entrySpeed * middleNormalMultiplierWrap;
                        
            vertex_t0 = vertex[i].position - endSide + layerOffset - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplierWrap * entrySpeed;
            vertex_t1 = vertex[j].position + endSide + layerOffset - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplierWrap * entrySpeed;
            
            float discardWrap = (_FxState < 0.6) ? 2.0 : 0.0;

            triStream.Append(CreateVertex(vertex_b0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 0, col, alpha));
            triStream.Append(CreateVertex(vertex_b1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 0, col, alpha));
                        
            triStream.Append(CreateVertex(vertex_m0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
            triStream.Append(CreateVertex(vertex_m1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
                        
            triStream.Append(CreateVertex(vertex_t0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 1, endCol, 0));
            triStream.Append(CreateVertex(vertex_t1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 1, endCol, 0));
                        
            triStream.RestartStrip();
        }
    }
    */
    for (uint v = 0; v < 3; v++) {
        float3 base = vertex[v].position;
        GS_DATA o;
        o.position = UnityObjectToClipPos(base + float3(-1, -1, 0));
        o.color = half4(1,1,0,1);
        o.positionWS = TransformObjectToWorld(base);
        o.positionOS = base;
        o.screenPos = o.position;
        o.trailPos = float2(0,0);
        o.layer = 0;
        o.airstreamNDC = vertex[v].airstreamNDC;
        triStream.Append(o);

        o.position = UnityObjectToClipPos(base + float3(1, -1, 0));
        triStream.Append(o);

        o.position = UnityObjectToClipPos(base + float3(-1, 1, -5));
        triStream.Append(o);

        o.position = UnityObjectToClipPos(base + float3(1, 1, -5));
        triStream.Append(o);

        triStream.RestartStrip();
    }
}

half4 eff_gs_frag(GS_DATA IN) : SV_Target
{
    float4 c = IN.color;
    
    // 修复：确保 clip 正确工作
    clip(IN.layer >= 0 ? 1.0 : -1.0);
    
    float entrySpeed = _EntryStrength / 4000 - 0.08 * _FxState;
    float speedScalar = saturate(lerp(0, 2.5, entrySpeed));
                
    float2 circleCoord = GetAirstreamNDC(normalize(IN.positionOS));
    float angle = atan2(circleCoord.y, circleCoord.x);
                
    float2 trailPos = 1 - IN.trailPos;
    float trailPosScalar0 = pow(trailPos.y, 2);
    float trailPosScalar1 = 0.2;
    float trailPosScalar = lerp(trailPosScalar0, trailPosScalar1, IN.layer);
    float invTrailPosScalar = 1 - trailPosScalar;
                
    float2 scrollScale = float2(lerp(0.6, 0.1, IN.layer), lerp(-8, -0.2, IN.layer));
    float2 timeOffset = float2(_Time.y * scrollScale.x, _Time.y * scrollScale.y * (entrySpeed + 0.5));
    float2 scale0 = float2(0.1, 2);
    float2 scale1 = float2(lerp(1, 0.2, trailPosScalar0 + 0.5), lerp(1, 0.1, trailPosScalar0 + 0.5));
    float2 uv = lerp(scale0, scale1, IN.layer) * trailPos + float2(angle, 0) - timeOffset;
                
    float noise = NoiseStatic(uv, lerp(3, 2, IN.layer)) * speedScalar * trailPosScalar;
    float noiseSign = lerp(1, -1, IN.layer);
                
    noise = max(0, noise);
                
    float alpha0 = saturate(c.a + noise * 0.05);
    float alpha1 = saturate(c.a - (noise * c.a * 7));
                
    float scalar0 = (0.1 + invTrailPosScalar * 0.05);
    float scalar1 = 1 - trailPosScalar0;
                
    c.a = lerp(alpha0, alpha1, IN.layer) * lerp(scalar0, scalar1, IN.layer) * _OpacityMultiplier * lerp(1.0, _WrapOpacityMultiplier, IN.layer);
                
    float c_a = saturate(c.a);
    c.rgb *= lerp(c_a * 1.3, 1.0, _Hdr);
    c.a = lerp(1, c_a, _Hdr);
                
    float DitheringGrain = 0.5 / 255.0;
    float dither = _DitherTex.SampleLevel(sampler_DitherTex, IN.screenPos.xy / _ScreenParams.xy * 500 + _Time.x, 0).r * trailPos.y;
    float3 cd = lerp(c.rgb, dither, DitheringGrain);
    c.rgb = lerp(cd + (-DitheringGrain / 4), c.rgb, _Hdr);

    return c;
}