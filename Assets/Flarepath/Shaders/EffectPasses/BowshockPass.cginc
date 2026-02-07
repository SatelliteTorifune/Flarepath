float4 _ShockwaveColor;
float _LengthMultiplier;
int _DisableBowshock;
float _BowshockForwardDistance;
float _BowshockRadiusScale;

struct VS_INPUT
{
    float4 position : POSITION;
    float3 normal   : NORMAL;
    float2 uv       : TEXCOORD0;
};

struct GS_INPUT
{
    float4 position : POSITION;
    float3 normal   : NORMAL;
    float2 uv       : TEXCOORD0;

    float3 positionWS   : TEXCOORD3;
    float3 airstreamNDC : TEXCOORD4;
    float3 velocityOS   : TEXCOORD5;
    float3 normalOS     : TEXCOORD6;
    float3 viewDir      : TEXCOORD7;
};

struct GS_DATA
{
    float4 position : SV_POSITION;
    half4  color    : COLOR;
    float3 positionWS : TEXCOORD1;
};

// ---------------------------------------------------
// 小工具：避免和 MainPass 的 CreateVertex 同名
// ---------------------------------------------------
GS_DATA CreateVertex_Bow(float3 pos, half4 col, half a)
{
    GS_DATA o;
    o.position   = UnityObjectToClipPos(pos);
    o.color      = col;
    o.color.a    = a;
    o.positionWS = TransformObjectToWorld(pos);
    return o;
}

// ---------------------------------------------------
// 顶点着色器
// ---------------------------------------------------
GS_INPUT bs_gs_vert(VS_INPUT IN)
{
    GS_INPUT OUT;
    OUT.position = IN.position * float4(_EnvelopeScaleFactor, 1.0);
    OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);
    OUT.normal = normalize(UnityObjectToWorldNormal(IN.normal));
    OUT.normalOS = normalize(IN.normal);
    OUT.uv = IN.uv;

    float4 aird = mul(_AirstreamVP, float4(OUT.positionWS,1));
    OUT.airstreamNDC = aird.xyz / aird.w;

    
    OUT.velocityOS = UnityWorldToObjectDir(normalize(_Velocity));
    OUT.viewDir    = WorldSpaceViewDir(IN.position);
    return OUT;
}

// ---------------------------------------------------
// Geometry Shader - 向前复制几何体产生蓝色等离子层
// 根据论文：在物体前方复制几何体，只有与速度向量角度非常接近的面才产生
// ---------------------------------------------------
[maxvertexcount(10)]
void bs_gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
    // 仅在强度足够且未手动禁用时绘制
    if (_EntryStrength < 0.01 || _DisableBowshock > 0) return;

    // 计算三个顶点的遮挡和法线-速度点积
    float3 occlusion = float3(
        Shadow(vertex[0].airstreamNDC, -0.003, 1),
        Shadow(vertex[1].airstreamNDC, -0.003, 1),
        Shadow(vertex[2].airstreamNDC, -0.003, 1)
    );

    float3 normalDots = float3(
        dot(vertex[0].normal, normalize(_Velocity)),
        dot(vertex[1].normal, normalize(_Velocity)),
        dot(vertex[2].normal, normalize(_Velocity))
    );

    // 计算平均遮挡和点积（用于决定是否生成bowshock）
    float avgOccl = (occlusion.x + occlusion.y + occlusion.z) / 3.0;
    float avgDot = (normalDots.x + normalDots.y + normalDots.z) / 3.0;

    // 只有正面且未被遮挡的面才生成bowshock
    // 点积需要接近1（法线与速度方向一致），论文中提到"角度非常接近"
    if (avgOccl < 0.9 || avgDot < 0.7) return;

    // 计算进入速度，用于控制强度
    float entrySpeed = saturate(_EntryStrength / 4000.0);
    
    // 根据点积计算强度（越接近1越强）
    float intensity = pow(saturate(avgDot), 3.0) * entrySpeed;
    
    // 计算向前延伸的距离（基于速度和强度）
    // 使用对象空间的速度方向（已经在顶点着色器中归一化）
    float3 forwardDir = normalize(vertex[0].velocityOS);
    float forwardDist = _BowshockForwardDistance * intensity * _LengthMultiplier;

    // 计算每个顶点向前复制的位置
    float3 v0_forward = vertex[0].position.xyz + forwardDir * forwardDist;
    float3 v1_forward = vertex[1].position.xyz + forwardDir * forwardDist;
    float3 v2_forward = vertex[2].position.xyz + forwardDir * forwardDist;

    // 根据点积调整颜色强度（正面越正，蓝色越强）
    float3 colorIntensity = float3(
        pow(saturate(normalDots.x), 4.0),
        pow(saturate(normalDots.y), 4.0),
        pow(saturate(normalDots.z), 4.0)
    );

    // 计算颜色（蓝色高温等离子体，强度随角度变化）
    float4 col0 = _ShockwaveColor * colorIntensity.x * intensity;
    float4 col1 = _ShockwaveColor * colorIntensity.y * intensity;
    float4 col2 = _ShockwaveColor * colorIntensity.z * intensity;

    // 创建两个三角形：原始三角形和向前复制的三角形
    // 第一个三角形（原始位置，alpha较低）
    triStream.Append(CreateVertex_Bow(vertex[0].position.xyz, col0 * 0.3, col0.a * 0.1));
    triStream.Append(CreateVertex_Bow(vertex[1].position.xyz, col1 * 0.3, col1.a * 0.1));
    triStream.Append(CreateVertex_Bow(vertex[2].position.xyz, col2 * 0.3, col2.a * 0.1));
    triStream.RestartStrip();

    // 第二个三角形（向前复制的位置，alpha较高）
    triStream.Append(CreateVertex_Bow(v0_forward, col0, col0.a));
    triStream.Append(CreateVertex_Bow(v1_forward, col1, col1.a));
    triStream.Append(CreateVertex_Bow(v2_forward, col2, col2.a));
    triStream.RestartStrip();

    // 创建连接两个三角形的侧面（形成厚度）
    // 边 0-1
    triStream.Append(CreateVertex_Bow(vertex[0].position.xyz, col0 * 0.5, col0.a * 0.3));
    triStream.Append(CreateVertex_Bow(vertex[1].position.xyz, col1 * 0.5, col1.a * 0.3));
    triStream.Append(CreateVertex_Bow(v0_forward, col0, col0.a));
    triStream.RestartStrip();

    triStream.Append(CreateVertex_Bow(v0_forward, col0, col0.a));
    triStream.Append(CreateVertex_Bow(v1_forward, col1, col1.a));
    triStream.Append(CreateVertex_Bow(vertex[1].position.xyz, col1 * 0.5, col1.a * 0.3));
    triStream.RestartStrip();

    // 边 1-2
    triStream.Append(CreateVertex_Bow(vertex[1].position.xyz, col1 * 0.5, col1.a * 0.3));
    triStream.Append(CreateVertex_Bow(vertex[2].position.xyz, col2 * 0.5, col2.a * 0.3));
    triStream.Append(CreateVertex_Bow(v1_forward, col1, col1.a));
    triStream.RestartStrip();

    triStream.Append(CreateVertex_Bow(v1_forward, col1, col1.a));
    triStream.Append(CreateVertex_Bow(v2_forward, col2, col2.a));
    triStream.Append(CreateVertex_Bow(vertex[2].position.xyz, col2 * 0.5, col2.a * 0.3));
    triStream.RestartStrip();

    // 边 2-0
    triStream.Append(CreateVertex_Bow(vertex[2].position.xyz, col2 * 0.5, col2.a * 0.3));
    triStream.Append(CreateVertex_Bow(vertex[0].position.xyz, col0 * 0.5, col0.a * 0.3));
    triStream.Append(CreateVertex_Bow(v2_forward, col2, col2.a));
    triStream.RestartStrip();

    triStream.Append(CreateVertex_Bow(v2_forward, col2, col2.a));
    triStream.Append(CreateVertex_Bow(v0_forward, col0, col0.a));
    triStream.Append(CreateVertex_Bow(vertex[0].position.xyz, col0 * 0.5, col0.a * 0.3));
}

// ---------------------------------------------------
// 片元
// ---------------------------------------------------
half4 bs_gs_frag(GS_DATA IN) : SV_Target
{
    // 简单返回颜色（已经在几何阶段把 alpha 设好）
    float4 c = IN.color;
    // 为了在 HDR 下不超出 1，先把颜色乘上 alpha
    c.rgb *= c.a;
    return c;
}
