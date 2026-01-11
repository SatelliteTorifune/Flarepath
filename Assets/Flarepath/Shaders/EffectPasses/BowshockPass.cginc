// BowshockPass.cginc (修复版)
// 适用于 Unity Built-in RP + Geometry Shader
// 修复了动态数组赋值和变量冲突问题

float4 _ShockwaveColor;
float _LengthMultiplier;

int _DisableBowshock;

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
    float3 airstreamNDC : TEXCOORD4;
    float3 velocityOS : TEXCOORD5;
    float3 normalOS : TEXCOORD6;
    float3 viewDir : TEXCOORD7; 
};

struct GS_DATA
{
    float4 position : SV_POSITION;
    half4 color : COLOR;
    float3 positionWS : TEXCOORD1;
};

// 创建顶点辅助函数
GS_DATA CreateVertex(float3 pos, half4 color, half a) 
{
    GS_DATA o;
    o.position = UnityObjectToClipPos(pos);
    o.color = color;
    o.color.a = a;
    o.positionWS = TransformObjectToWorld(pos);
    return o;
}

GS_INPUT bs_gs_vert(VS_INPUT IN)
{
    GS_INPUT OUT;

    // 应用缩放
    OUT.position = IN.position * float4(_EnvelopeScaleFactor, 1.0);
    OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);

    OUT.normal = normalize(UnityObjectToWorldNormal(IN.normal));
    OUT.normalOS = normalize(IN.normal);

    OUT.uv = IN.uv;

    // 计算 Airstream NDC 用于阴影采样
    float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
    OUT.airstreamNDC = airstreamPosition.xyz / airstreamPosition.w;

    // 物体空间速度方向
    OUT.velocityOS = UnityWorldToObjectDir(normalize(_Velocity));
    
    OUT.viewDir = WorldSpaceViewDir(IN.position);

    return OUT;
}

[maxvertexcount(24)]  // 根据实际输出顶点数可调大到 18 或 24
void bs_gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
    // 强度太低或禁用时直接跳过
    //if (_EntryStrength < 0.01 || _DisableBowshock > 0) return;

    // =======================================
    // 1. 计算每个顶点的 occlusion (阴影遮挡)
    // =======================================
    float occlusion0 = Shadow(vertex[0].airstreamNDC, -0.003, 1);
    float occlusion1 = Shadow(vertex[1].airstreamNDC, -0.003, 1);
    float occlusion2 = Shadow(vertex[2].airstreamNDC, -0.003, 1);

    float3 occlusion = float3(occlusion0, occlusion1, occlusion2);

    // =======================================
    // 2. 计算基础长度 & 噪声 (全部展开，避免动态索引赋值)
    // =======================================
    float baseLength = min(_EntryStrength, 2300) * 0.0005;

    float entrySpeed = min(_EntryStrength / 4000, 0.57);
    float scaledEntrySpeed = lerp(0, entrySpeed + 0.55, saturate((entrySpeed - 0.32) * 2));

    // 噪声展开计算
    float noise0 = Noise(vertex[0].position.xy + vertex[0].uv, 1) * baseLength *
                   Noise(vertex[0].position.xy + vertex[0].uv, 2) * 10;
    float noise1 = Noise(vertex[1].position.xy + vertex[1].uv, 1) * baseLength *
                   Noise(vertex[1].position.xy + vertex[1].uv, 2) * 10;
    float noise2 = Noise(vertex[2].position.xy + vertex[2].uv, 1) * baseLength *
                   Noise(vertex[2].position.xy + vertex[2].uv, 2) * 10;

    float3 noise = float3(noise0, noise1, noise2);

    // 效果长度
    float3 effectLength = (baseLength + noise * 0.3) * scaledEntrySpeed * 1.5;
    float3 middleLength = effectLength * 0.54;

    // 前向激波侧向长度
    float3 effectSideLength = (3 + noise) * scaledEntrySpeed * 0.9;
    float3 middleSideLength = effectSideLength * 0.45;

    // =======================================
    // 3. Fresnel & 速度点积 (全部展开)
    // =======================================
    float fresnel0 = Fresnel(vertex[0].normal, vertex[0].viewDir, 1) * 
                     Fresnel(-vertex[0].normal, vertex[0].viewDir, 1) -
                     (Fresnel(vertex[0].normal, vertex[0].viewDir, 3) - 0.3) -
                     (1 - Fresnel(vertex[0].normal, vertex[0].viewDir, 1) - 0.8);
    float fresnel1 = Fresnel(vertex[1].normal, vertex[1].viewDir, 1) * 
                     Fresnel(-vertex[1].normal, vertex[1].viewDir, 1) -
                     (Fresnel(vertex[1].normal, vertex[1].viewDir, 3) - 0.3) -
                     (1 - Fresnel(vertex[1].normal, vertex[1].viewDir, 1) - 0.8);
    float fresnel2 = Fresnel(vertex[2].normal, vertex[2].viewDir, 1) * 
                     Fresnel(-vertex[2].normal, vertex[2].viewDir, 1) -
                     (Fresnel(vertex[2].normal, vertex[2].viewDir, 3) - 0.3) -
                     (1 - Fresnel(vertex[2].normal, vertex[2].viewDir, 1) - 0.8);

    float3 vertFresnel = float3(fresnel0, fresnel1, fresnel2);

    float velDotInv0 = dot(vertex[0].normal, _Velocity);
    float velDotInv1 = dot(vertex[1].normal, _Velocity);
    float velDotInv2 = dot(vertex[2].normal, _Velocity);

    float3 velDotInv = float3(velDotInv0, velDotInv1, velDotInv2);
    float3 velDot = -velDotInv;

    // =======================================
    // 4. 生成 "bowl" 形状 (前方激波碗)
    // =======================================
    float3 offset = (vertex[0].velocityOS * 0.4 * entrySpeed) * _ModelScale.y;
    float3 trailOffset = offset * 1.1;

    if (occlusion0 > 0.9 && velDotInv0 > 0.2)
    {
        for (int idx = 0; idx < 3; idx++)  // 这里用 idx 避免与其它 i 冲突
        {
            float fresnel = Fresnel(vertex[idx].normal, vertex[idx].viewDir, 2);
            float fresnelInv = Fresnel(-vertex[idx].normal, vertex[idx].viewDir, 2);
            float softFresnel = Fresnel(vertex[idx].normal, vertex[idx].viewDir, 2);

            float alpha = 0.85 * fresnel * scaledEntrySpeed * (1 - softFresnel);

            triStream.Append(CreateVertex(vertex[idx].position + offset, 
                                          _ShockwaveColor * velDotInv[idx] + fresnel * 0.6, 
                                          alpha));
        }
        triStream.RestartStrip();
    }

    // =======================================
    // 5. 缩放长度 & 防止负值
    // =======================================
    effectLength *= _ModelScale.y;
    middleLength *= _ModelScale.y;
    middleSideLength *= _ModelScale.y;
    effectSideLength *= _ModelScale.y;

    effectLength = abs(effectLength);
    middleLength = abs(middleLength);
    middleSideLength = abs(middleSideLength);
    effectSideLength = abs(effectSideLength);

    // =======================================
    // 6. 生成尾迹条带 (只处理 2 条边)
    // =======================================
    for (uint edge = 0; edge < 2; edge++)
    {
        uint i = edge;
        uint j = (edge + 1) % 3;
        // uint k = (j + 1) % 3;  // 如果后面需要可再加

        if (occlusion[i] > 0.9 && velDot[i] > -0.4 && velDot[i] < 0 && pow(vertFresnel[i], 2) > 0.2)
        {
            // 边长平均
            float edgeLength_j = length(vertex[i].position - vertex[j].position);
            float edgeLength = edgeLength_j * (1 / _ModelScale.y);  // 简化版，实际可再平均
            float edgeMul = clamp(edgeLength / 0.1, 0.1, 1);

            // 噪声采样（只用当前顶点）
            float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);

            float3 sizeVector = -normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));
            float3 side = sizeVector * 0.2;
            float3 middleSide = side * 2;
            float3 endSide = side * 2;

            side *= _ModelScale.x;
            middleSide *= _ModelScale.x;
            endSide *= _ModelScale.x;

            // 透明度计算
            float alpha = 0.02 * 0.5 * scaledEntrySpeed * saturate(pow(vertFresnel[i], 3));
            float middleAlpha = alpha * 0.6;
            alpha *= edgeMul;
            middleAlpha *= edgeMul;

            // 顶点位置定义
            float3 vertex_b0 = vertex[i].position + trailOffset - side;
            float3 vertex_b1 = vertex[j].position + trailOffset + side;

            float3 vertex_m0 = vertex[i].position + trailOffset - middleSide + 
                               middleLength[i] * vertex[i].normalOS - 
                               vertex[i].velocityOS * middleSideLength[i];
            float3 vertex_m1 = vertex[j].position + trailOffset + middleSide + 
                               middleLength[j] * vertex[j].normalOS - 
                               vertex[j].velocityOS * middleSideLength[j];

            float3 vertex_t0 = vertex[i].position + trailOffset - endSide + 
                               effectLength[i] * vertex[i].normalOS - 
                               vertex[i].velocityOS * effectSideLength[i];
            float3 vertex_t1 = vertex[j].position + trailOffset + endSide + 
                               effectLength[j] * vertex[j].normalOS - 
                               vertex[j].velocityOS * effectSideLength[j];

            // 输出三角带
            triStream.Append(CreateVertex(vertex_b0, _ShockwaveColor, alpha));
            triStream.Append(CreateVertex(vertex_b1, _ShockwaveColor, alpha));

            triStream.Append(CreateVertex(vertex_m0, _ShockwaveColor, middleAlpha));
            triStream.Append(CreateVertex(vertex_m1, _ShockwaveColor, middleAlpha));

            triStream.Append(CreateVertex(vertex_t0, _ShockwaveColor, 0));
            triStream.Append(CreateVertex(vertex_t1, _ShockwaveColor, 0));

            triStream.RestartStrip();
        }
    }
}

half4 bs_gs_frag(GS_DATA IN) : SV_Target
{
    float4 c = IN.color;
    // 提前乘 alpha，防止 HDR 模式下颜色溢出
    c.rgb *= c.a;
    c.a = 1;
    return c;
}