float4 _ShockwaveColor;
float _LengthMultiplier;
int _DisableBowshock;

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
// Geometry Shader（已极简化，只绘制一个“冲击波圆盘”）
// ---------------------------------------------------
[maxvertexcount(24)]
void bs_gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
    // 仅在强度足够且未手动禁用时绘制
    if (_EntryStrength < 0.01 || _DisableBowshock > 0) return;

    // 取第一个顶点做基准（这里直接用 vertex[0]，不做循环，因为激波只在正面出现一次）
    float occl = Shadow(vertex[0].airstreamNDC, -0.003, 1);
    float dotV = dot(vertex[0].normal, _Velocity);
    if (occl < 0.9 || dotV < 0.2) return; // 没有正面或被遮挡则退出

    // ----------------------------------------------------------------
    // 生成一个小圆环（6 条三角形）模拟冲击波 “bow‑shock”
    // ----------------------------------------------------------------
    const int SEG = 6;
    float radius = 0.5 * _LengthMultiplier; // 基础半径，可调
    float3 center = vertex[0].position;     // 冲击波中心位于模型正前方

    // 方向向量（向前的速度方向）
    float3 forward = normalize(_Velocity);

    // 生成环形
    for (int i = 0; i < SEG; ++i)
    {
        float ang0 = (i   / (float)SEG) * 6.2831853;
        float ang1 = ((i+1) / (float)SEG) * 6.2831853;

        float3 p0 = center + forward * 0.2
                 + float3(cos(ang0), sin(ang0), 0) * radius;
        float3 p1 = center + forward * 0.2
                 + float3(cos(ang1), sin(ang1), 0) * radius;
        float3 p2 = center; // 中心点

        // 三角形 0‑1‑2
        triStream.Append(CreateVertex_Bow(p0, _ShockwaveColor, 1));
        triStream.Append(CreateVertex_Bow(p1, _ShockwaveColor, 1));
        triStream.Append(CreateVertex_Bow(p2, _ShockwaveColor, 0));
        triStream.RestartStrip();
    }
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
