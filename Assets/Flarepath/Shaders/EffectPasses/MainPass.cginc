// ================================================================
// EffectPasses/MainPass.cginc
// ================================================================
// Geometry‑Shader based plasma‑trail effect (Effects Pass)
// ------------------------------------------------
// 该文件被 "Firefly/Firefly" Shader 通过
//    #include "EffectPasses/MainPass.cginc"
// 引入。所有依赖函数（Shadow、Noise、Fresnel、Transform* 等）都在
// CommonFunctions.cginc 中实现。只需要保证 CommonFunctions 已
// 正确包含即可。
// ------------------------------------------------

#pragma target 5.0
#pragma require geometry

//-----------------------------------------------------------
// 参数（在主 Shader 的 Properties 中声明）
//-----------------------------------------------------------
float _FxState;                     // 全局强度 (0…1)
float _AngleOfAttack;               // 攻角（度）
int   _Hdr;                         // 是否 HDR（0/1）
float _TrailAlphaMultiplier;
float _BlueMultiplier;
float _SideMultiplier;
float _OpacityMultiplier;
float _WrapOpacityMultiplier;
float _WrapFresnelModifier;
float _StreakProbability;
float _StreakThreshold;
float _LengthMultiplier;            // 长度乘数

float4 _SecondaryColor;
float4 _PrimaryColor;
float4 _TertiaryColor;
float4 _StreakColor;
float4 _LayerColor;
float4 _LayerStreakColor;

float2 _RandomnessFactor;          // x = streak 随机性, y = wrap 随机性

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

    float3 positionWS   : TEXCOORD3;   // 世界空间位置（用于光照 / airstream）
    float4 airstreamNDC : TEXCOORD4; // airstream NDC (xy = screen, z = depth)
    float3 velocityOS  : TEXCOORD5; // 速度（对象空间）
    float3 normalOS    : TEXCOORD6; // 法线（对象空间）
    float3 viewDir     : TEXCOORD7; // 观察方向（世界空间）
};

struct GS_DATA
{
    float4 position   : SV_POSITION; // Clip‑space
    half4  color      : COLOR;       // 颜色 + alpha
    float3 positionWS : TEXCOORD1;   // 世界空间坐标（调试/后处理可用）
    float3 positionOS : TEXCOORD2;   // 对象空间坐标（噪声等）
    float4 screenPos  : TEXCOORD4;   // 同 position，用于 Dither
    float2 trailPos   : TEXCOORD6;   // (0,0) = 尾, (1,1) = 头
    float  layer      : TEXCOORD7;   // 0 = 主层, 1 = Wrap 层 (负值 = 剔除)
    float4 airstreamNDC : TEXCOORD8; // 用于深度遮挡
};

//-----------------------------------------------------------
// 辅助：创建一个输出顶点
//-----------------------------------------------------------
GS_DATA CreateVertex ( float3 pos,
                      float  layer,
                      float4 airstreamNDC,
                      float  trailX,
                      float  trailY,
                      half4  col,
                      half   a )
{
    GS_DATA o;
    o.position   = UnityObjectToClipPos(pos);
    o.color      = col;
    o.color.a    = a;
    o.positionWS = TransformObjectToWorld(pos);
    o.positionOS = pos;
    o.screenPos  = o.position;
    o.trailPos   = float2(trailX, trailY);
    o.layer      = layer;
    o.airstreamNDC = airstreamNDC;
    return o;
}

//-----------------------------------------------------------
// 顶点着色器（Vertex → Geometry）
//-----------------------------------------------------------
GS_INPUT eff_gs_vert ( VS_INPUT IN )
{
    GS_INPUT OUT;

    // 世界/对象空间法线
    OUT.normal   = UnityObjectToWorldNormal(IN.normal);
    OUT.normalOS = normalize(IN.normal);

    // 缩放（可在材质里控制）
    OUT.position = IN.position * float4(_EnvelopeScaleFactor,1);
    OUT.uv       = IN.uv;

    // 世界空间位置（后面需要计算 airstream NDC）
    OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);

    // airstream NDC（用于 Shadow()）
    float4 airstreamPos = mul(_AirstreamVP, float4(OUT.positionWS,1));
    OUT.airstreamNDC = float4(airstreamPos.xyz / airstreamPos.w,
                             airstreamPos.w);

    // 对象空间速度（全局速度向量）
    OUT.velocityOS = normalize(UnityWorldToObjectDir(_Velocity));

    // 观察方向（WorldSpaceViewDir 用 UnityCG 提供）
    OUT.viewDir = WorldSpaceViewDir(IN.position);
    return OUT;
}

// Geometry Shader

// maxvertexcount 限制：GS_DATA有25个标量组件，D3D11限制是1024
// 1024 / 25 = 40.96，所以最多40个顶点
// 每个三角形边最多生成：主层10个顶点 + Wrap层10个顶点 = 20个顶点
//
[maxvertexcount(20)]
void eff_gs_geom ( triangle GS_INPUT vertex[3],
                  inout TriangleStream<GS_DATA> triStream )
{
    // ---------- Early‑out ----------
    if (_EntryStrength < 50) return;

    // ---------- 基础值 ----------
    float entrySpeed = _EntryStrength / 4000.0 - 0.08 * _FxState;

    // ---------- 遮挡（Shadow） ----------
    float3 occlusion = float3(
        Shadow(vertex[0].airstreamNDC.xyz, -0.003, 1),
        Shadow(vertex[1].airstreamNDC.xyz, -0.003, 1),
        Shadow(vertex[2].airstreamNDC.xyz, -0.003, 1)
    );

    // ---------- 法线·速度 点积 ----------
    float3 velDots = float3(
        dot(-vertex[0].normal, _Velocity),
        dot(-vertex[1].normal, _Velocity),
        dot(-vertex[2].normal, _Velocity)
    );

    // ---------- 基础长度 & 噪声 ----------
    float baseLength = _EntryStrength * 0.0013;
    float3 noise = float3(
        Noise(vertex[0].position.xy + vertex[0].uv, 1) *
                baseLength * Noise(vertex[0].position.xy + vertex[0].uv, 1) * 5,
        Noise(vertex[1].position.xy + vertex[1].uv, 1) *
                baseLength * Noise(vertex[1].position.xy + vertex[1].uv, 1) * 5,
        Noise(vertex[2].position.xy + vertex[2].uv, 1) *
                baseLength * Noise(vertex[2].position.xy + vertex[2].uv, 1) * 5
    );

    // ---------- 计算每个顶点的效果长度 ----------
    float3 effectLength = (baseLength + noise) * _LengthMultiplier;
    float3 middleLength = effectLength * 0.2;

    float middleNormalMultiplier = 0.23 + lerp(0.1 * _FxState, 0,
                         saturate((entrySpeed - 0.2) * 4));

    // 最大可能的等离子体长度（用于 wrap 层上限）
    float maxEffectLength = (5.2 * 6.0) * _LengthMultiplier; // 5.2 = maxBaseLength

    // 对模型整体缩放做补偿，防止超大模型产生过长尾迹
    effectLength *= _ModelScale.y;
    middleLength *= _ModelScale.y;

    // =========================================================
    // 循环遍历三角形的三个顶点，决定是否生成几何体
    // =========================================================
    for (uint i = 0; i < 3; ++i)
    {
        float curVelDot   = velDots[i];
        float curOccl     = occlusion[i];

        // 只在迎风且未被遮挡的面上生成等离子体
        if (curVelDot > -0.1 && curOccl > 0.9)
        {
            // --- 找出相邻顶点 j，k（用于边长、方向） ---
            uint j = (i + 1) % 3;
            uint k = (j + 1) % 3;

            // ---------- 边长（用于宽度、密度） ----------
            float edgeLenJ = length(vertex[i].position - vertex[j].position);
            float edgeLenK = length(vertex[i].position - vertex[k].position);
            float edgeLen  = (edgeLenJ + edgeLenK) * 0.5 / _ModelScale.y;
            float edgeMul  = clamp(edgeLen / 0.05, 0.1, 40);
            float sideEdgeMul = 1.0 - saturate(edgeLen - 0.1);

            // 如果 k 的遮挡或速度点积更大，改用 k 作为“次 dominant”
            if (occlusion[k] > occlusion[j] || velDots[k] > velDots[j])
                j = k;

            // ---------- 计算几何宽度向量 ----------
            float3 sizeVec   = normalize(cross(vertex[i].velocityOS,
                                              vertex[i].normalOS));
            float3 side      = sizeVec * 0.6 * sideEdgeMul;
            float3 middleSide = side * 1.4 * clamp(entrySpeed, 0.2, 1);
            float3 endSide    = side * 2.5 * clamp(entrySpeed, 0.2, 1);
            side *= 0.3; // 基准收缩

            // ---------- 噪声（用于颜色、长度随机） ----------
            float vertNoise  = Noise(vertex[i].position.xy + vertex[i].uv
                                     + _Time.x, 0);
            float vertNoise1 = Noise(vertex[i].position.xy
                          - _Time.y * (1.0 - _RandomnessFactor.x),
                          1 + (int)round(_RandomnessFactor.x));
            float vertNoise2 = Noise(vertex[i].position.xy - _Time.x, 1);

            // ---------- 长度乘子 ----------
            float normalMultiplier = 0.8 *
                pow(_LengthMultiplier,
                    lerp(5,1,saturate(_LengthMultiplier))) +
                lerp(2 * _LengthMultiplier * _FxState, 0,
                     saturate((entrySpeed - 0.2) * 4));

            // ---------- 基础颜色 ----------
            float4 col       = lerp(_PrimaryColor, _SecondaryColor,
                                   vertNoise * 0.3);
            float4 middleCol = lerp(col, _SecondaryColor, 0.5);
            float4 endCol   = lerp(_SecondaryColor, _TertiaryColor,
                                  clamp(entrySpeed,0,1.7));
            endCol = lerp(middleCol, endCol, _Hdr);

            // ---------- 透明度 ----------
            float alpha = saturate(entrySpeed / 0.025) *
                (0.004 * _TrailAlphaMultiplier + vertNoise * 0.004);

            // ---------- Mach / 速度系数 ----------
            float t = saturate(entrySpeed / 0.25 - 1);
            float fxState = lerp(_FxState, _FxState * 0.05,
                                 saturate(sign(_Velocity.y)));
            float interp = saturate(t + _FxState);

            col       = lerp(1, col,       interp);
            middleCol = lerp(1, middleCol, interp);
            endCol    = lerp(1, endCol,    interp);

            // ---------- 攻角对亮度的影响 ----------
            // 攻角越大，效果越明显（0度时较弱，大角度时更强）
            // 将角度归一化到0-1范围，20度作为参考值
            float aoaNormalized = saturate(_AngleOfAttack / 20.0);
            // 使用更平滑的曲线，让效果更明显
            float aoa = pow(aoaNormalized, 2.0); // 平方曲线，让效果更平滑
            // 攻角影响alpha：角度越大，alpha越强（最小0.5倍，最大1.5倍）
            alpha *= lerp(0.5, 1.5, aoa) * saturate(t + 0.5);

            // ---------- 缩放至模型空间 ----------
            side       *= _ModelScale.x;
            middleSide *= _ModelScale.x;
            endSide    *= _ModelScale.x;
            normalMultiplier *= _ModelScale.x;

            // ---------- 取出当前顶点对应的长度 ----------
            float curEffectLen = (i == 0) ? effectLength.x :
                                 (i == 1) ? effectLength.y : effectLength.z;
            float curMiddleLen = (i == 0) ? middleLength.x :
                                 (i == 1) ? middleLength.y : middleLength.z;
            curEffectLen = abs(curEffectLen);
            curMiddleLen = abs(curMiddleLen);

            // ---------- 计算 trailDir 与 Fresnel ----------
            float3 trailDir = (vertex[i].position
                               - vertex[i].velocityOS * curEffectLen
                               + vertex[i].normalOS *
                                 normalMultiplier * entrySpeed)
                              - vertex[i].position;
            float3 normal    = normalize(cross(sizeVec, trailDir));

            float fresnel = Fresnel(vertex[i].normal,
                                   vertex[i].viewDir, 2);
            alpha *= saturate(fresnel + 0.5 + vertNoise * 0.3) * edgeMul;

            // ---------- 随机条纹 (Streak) ----------
            int streakValue = 0;
            float4 streakCol = lerp(float4(4,1,1,1),
                                  float4(1,4,1,1), vertNoise2);
            if (vertNoise1 > 0.73 - _StreakProbability &&
                entrySpeed > 0.5 + _StreakThreshold)
            {
                col       = lerp(_StreakColor, streakCol,
                                 _RandomnessFactor.x);
                middleCol = lerp(_StreakColor, streakCol,
                                 _RandomnessFactor.x);
                endCol    = lerp(_StreakColor, streakCol,
                                 _RandomnessFactor.x);
                curEffectLen *= 2.0;
                alpha        *= 2.0;
                streakValue   = 1;
            }

            // ---------- 计算细分顶点（底‑中下‑中‑中上‑顶，共5个点） ----------
            // 底部顶点
            float3 v_b0 = vertex[i].position - side;
            float3 v_b1 = vertex[j].position + side;

            // 中下部顶点（25%位置）
            float len_25 = curMiddleLen * 0.25;
            float3 v_md0 = vertex[i].position - side * 0.7
                         - vertex[i].velocityOS * len_25
                         + vertex[i].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier * 0.5;
            float3 v_md1 = vertex[j].position + side * 0.7
                         - vertex[j].velocityOS * len_25
                         + vertex[j].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier * 0.5;

            // 中部顶点（50%位置）
            float3 v_m0 = vertex[i].position - middleSide
                         - vertex[i].velocityOS * curMiddleLen
                         + vertex[i].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier;
            float3 v_m1 = vertex[j].position + middleSide
                         - vertex[j].velocityOS * curMiddleLen
                         + vertex[j].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier;

            // 中上部顶点（75%位置）
            float len_75 = curMiddleLen + (curEffectLen - curMiddleLen) * 0.5;
            float3 v_mu0 = vertex[i].position - endSide * 0.7
                         - vertex[i].velocityOS * len_75
                         + vertex[i].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier * 1.5;
            float3 v_mu1 = vertex[j].position + endSide * 0.7
                         - vertex[j].velocityOS * len_75
                         + vertex[j].normalOS *
                           normalMultiplier * entrySpeed *
                           middleNormalMultiplier * 1.5;

            // 顶部顶点
            float3 v_t0 = vertex[i].position - endSide
                         - vertex[i].velocityOS * curEffectLen
                         + vertex[i].normalOS *
                           normalMultiplier * entrySpeed;
            float3 v_t1 = vertex[j].position + endSide
                         - vertex[j].velocityOS * curEffectLen
                         + vertex[j].normalOS *
                           normalMultiplier * entrySpeed;

            // ---------- 深度剔除（防止几何穿透模型） ----------
            float3 ndcMid = GetAirstreamNDC(
                lerp(v_b0, v_m0,
                     lerp(0.5, 0.1, saturate(entrySpeed - 0.5))));
            float depthMask = Shadow(ndcMid, -0.003, 1);
            float discardSeg = (depthMask < 0.9) ? 2.0 : 0.0;

            // ---------- 主层 (layer = 0) - 细分三角形带（5个点，4个三角形） ----------
            // 计算中间颜色插值
            float4 col_25 = lerp(col, middleCol, 0.5);
            float4 col_75 = lerp(middleCol, endCol, 0.5);
            float alpha_25 = alpha * 0.8;
            float alpha_75 = alpha * 0.4;

            // 底部三角形 (0%)
            triStream.Append(CreateVertex(v_b0, 0 - discardSeg,
                                         vertex[i].airstreamNDC,
                                         0, 0.0, col, alpha));
            triStream.Append(CreateVertex(v_b1, 0 - discardSeg,
                                         vertex[j].airstreamNDC,
                                         1, 0.0, col, alpha));

            // 中下部三角形 (25%)
            triStream.Append(CreateVertex(v_md0, 0 - discardSeg,
                                         vertex[i].airstreamNDC,
                                         0, 0.25, col_25, alpha_25));
            triStream.Append(CreateVertex(v_md1, 0 - discardSeg,
                                         vertex[j].airstreamNDC,
                                         1, 0.25, col_25, alpha_25));

            // 中部三角形 (50%)
            triStream.Append(CreateVertex(v_m0, 0 - discardSeg,
                                         vertex[i].airstreamNDC,
                                         0, 0.5, middleCol, alpha));
            triStream.Append(CreateVertex(v_m1, 0 - discardSeg,
                                         vertex[j].airstreamNDC,
                                         1, 0.5, middleCol, alpha));

            // 中上部三角形 (75%)
            triStream.Append(CreateVertex(v_mu0, 0 - discardSeg,
                                         vertex[i].airstreamNDC,
                                         0, 0.75, col_75, alpha_75));
            triStream.Append(CreateVertex(v_mu1, 0 - discardSeg,
                                         vertex[j].airstreamNDC,
                                         1, 0.75, col_75, alpha_75));

            // 顶部三角形 (100%)
            triStream.Append(CreateVertex(v_t0, 0 - discardSeg,
                                         vertex[i].airstreamNDC,
                                         0, 1.0, endCol, 0));
            triStream.Append(CreateVertex(v_t1, 0 - discardSeg,
                                         vertex[j].airstreamNDC,
                                         1, 1.0, endCol, 0));

            triStream.RestartStrip();

            // ======================================================
            // --------------------- Wrap 层 (layer = 1) --------------
            // ======================================================
            if (_FxState < 0.6) continue;                // 强度不足直接跳过 wrap

            entrySpeed = clamp(entrySpeed,0,1);

            float fresnelWrap = Fresnel(vertex[i].normal,
                                        vertex[i].viewDir, 1);
            fresnelWrap += (1.0 - fresnelWrap) * _WrapFresnelModifier;

            // 调整长度上限
            middleLength = clamp(middleLength, 0,
                                 maxEffectLength * 0.2);
            curEffectLen = clamp(curEffectLen * 3.4 *
                                 clamp(entrySpeed,0,0.6) *
                                 _FxState,
                                 0, maxEffectLength * 1.6);
            float normalMulWrap = -2.325 *
                saturate(pow(_LengthMultiplier,3)) * _ModelScale.x;
            float middleNormalMulWrap = 0.05;

            alpha *= 0.5 * fresnelWrap *
                     min(entrySpeed,0.7) * _FxState;

            // Wrap 层颜色
            float4 wrapCol = _PrimaryColor;
            float4 wrapMidCol = lerp(_LayerColor,
                                    lerp(_LayerStreakColor,
                                         streakCol,
                                         _RandomnessFactor.y),
                                    streakValue);
            float4 wrapEndCol = lerp( lerp(_LayerColor,
                                          _SecondaryColor, 0.5),
                                      lerp(_LayerStreakColor,
                                           streakCol,
                                           _RandomnessFactor.y),
                                      streakValue );

            // 整体向后位移，防止几何刺穿模型
            float3 layerOffset = vertex[i].velocityOS *
                                 -0.05 * _LengthMultiplier *
                                 _ModelScale.y;

            // 重新计算 Wrap 层的细分顶点（5个点）
            // 底部顶点
            v_b0 = vertex[i].position - side + layerOffset;
            v_b1 = vertex[j].position + side + layerOffset;

            // 中下部顶点（25%位置）
            float wrap_len_25 = curMiddleLen * 0.25;
            float3 v_wrap_md0 = vertex[i].position - side * 0.7 + layerOffset
                               - vertex[i].velocityOS * wrap_len_25
                               + vertex[i].normalOS *
                                 normalMulWrap * entrySpeed *
                                 middleNormalMulWrap * 0.5;
            float3 v_wrap_md1 = vertex[j].position + side * 0.7 + layerOffset
                               - vertex[j].velocityOS * wrap_len_25
                               + vertex[j].normalOS *
                                 normalMulWrap * entrySpeed *
                                 middleNormalMulWrap * 0.5;

            // 中部顶点（50%位置）
            v_m0 = vertex[i].position - middleSide + layerOffset
                   - vertex[i].velocityOS * curMiddleLen
                   + vertex[i].normalOS *
                     normalMulWrap * entrySpeed *
                     middleNormalMulWrap;
            v_m1 = vertex[j].position + middleSide + layerOffset
                   - vertex[j].velocityOS * curMiddleLen
                   + vertex[j].normalOS *
                     normalMulWrap * entrySpeed *
                     middleNormalMulWrap;

            // 中上部顶点（75%位置）
            float wrap_len_75 = curMiddleLen + (curEffectLen - curMiddleLen) * 0.5;
            float3 v_wrap_mu0 = vertex[i].position - endSide * 0.7 + layerOffset
                               - vertex[i].velocityOS * wrap_len_75
                               + vertex[i].normalOS *
                                 normalMulWrap * entrySpeed *
                                 middleNormalMulWrap * 1.5;
            float3 v_wrap_mu1 = vertex[j].position + endSide * 0.7 + layerOffset
                               - vertex[j].velocityOS * wrap_len_75
                               + vertex[j].normalOS *
                                 normalMulWrap * entrySpeed *
                                 middleNormalMulWrap * 1.5;

            // 顶部顶点
            v_t0 = vertex[i].position - endSide + layerOffset
                   - vertex[i].velocityOS * curEffectLen
                   + vertex[i].normalOS *
                     normalMulWrap * entrySpeed;
            v_t1 = vertex[j].position + endSide + layerOffset
                   - vertex[j].velocityOS * curEffectLen
                   + vertex[j].normalOS *
                     normalMulWrap * entrySpeed;

            float discardWrap = (_FxState < 0.6) ? 2.0 : 0.0;

            // Wrap 层输出 - 细分三角形带（5个点，4个三角形）
            // 计算中间颜色插值
            float4 wrapCol_25 = lerp(wrapCol, wrapMidCol, 0.5);
            float4 wrapCol_75 = lerp(wrapMidCol, wrapEndCol, 0.5);
            float wrapAlpha_25 = alpha * 0.8;
            float wrapAlpha_75 = alpha * 0.4;

            // 底部三角形 (0%)
            triStream.Append(CreateVertex(v_b0, 1 - discardWrap,
                                         vertex[i].airstreamNDC,
                                         0, 0.0, wrapCol, alpha));
            triStream.Append(CreateVertex(v_b1, 1 - discardWrap,
                                         vertex[j].airstreamNDC,
                                         1, 0.0, wrapCol, alpha));

            // 中下部三角形 (25%)
            triStream.Append(CreateVertex(v_wrap_md0, 1 - discardWrap,
                                         vertex[i].airstreamNDC,
                                         0, 0.25, wrapCol_25, wrapAlpha_25));
            triStream.Append(CreateVertex(v_wrap_md1, 1 - discardWrap,
                                         vertex[j].airstreamNDC,
                                         1, 0.25, wrapCol_25, wrapAlpha_25));

            // 中部三角形 (50%)
            triStream.Append(CreateVertex(v_m0, 1 - discardWrap,
                                         vertex[i].airstreamNDC,
                                         0, 0.5, wrapMidCol, alpha));
            triStream.Append(CreateVertex(v_m1, 1 - discardWrap,
                                         vertex[j].airstreamNDC,
                                         1, 0.5, wrapMidCol, alpha));

            // 中上部三角形 (75%)
            triStream.Append(CreateVertex(v_wrap_mu0, 1 - discardWrap,
                                         vertex[i].airstreamNDC,
                                         0, 0.75, wrapCol_75, wrapAlpha_75));
            triStream.Append(CreateVertex(v_wrap_mu1, 1 - discardWrap,
                                         vertex[j].airstreamNDC,
                                         1, 0.75, wrapCol_75, wrapAlpha_75));

            // 顶部三角形 (100%)
            triStream.Append(CreateVertex(v_t0, 1 - discardWrap,
                                         vertex[i].airstreamNDC,
                                         0, 1.0, wrapEndCol, 0));
            triStream.Append(CreateVertex(v_t1, 1 - discardWrap,
                                         vertex[j].airstreamNDC,
                                         1, 1.0, wrapEndCol, 0));

            triStream.RestartStrip();
        } // if (velDot && occlusion)
    } // for each vertex
}

//-----------------------------------------------------------
// Fragment Shader
//-----------------------------------------------------------
half4 eff_gs_frag ( GS_DATA IN ) : SV_Target
{
    float4 col = IN.color;

    // 只渲染 layer >= 0 的几何体（负值表示 "已被裁掉"）
    clip(IN.layer >= 0 ? 1.0 : -1.0);

    // ---------- 基本参数 ----------
    float entrySpeed = _EntryStrength / 4000.0 - 0.08 * _FxState;
    float speedScalar = saturate(lerp(0.0, 2.5, entrySpeed));

    // ---------- 环形坐标 + 角度 ----------
    float3 ndcCoord = GetAirstreamNDC(normalize(IN.positionOS));
    float angle = atan2(ndcCoord.y, ndcCoord.x);

    // ---------- 尾迹位置 ----------
    float2 trailPos = 1.0 - IN.trailPos;          // (0,0)=头, (1,1)=尾
    float trailPosScalar0 = pow(trailPos.y, 2);
    float trailPosScalar1 = 0.2;
    float trailPosScalar = lerp(trailPosScalar0, trailPosScalar1, IN.layer);
    float invTrailPosScalar = 1.0 - trailPosScalar;

    // ========== 根据 EntryStrength 和 trailPos 调整颜色 ==========
    // 头部颜色：根据 EntryStrength 控制温度颜色
    float temperatureFactor = saturate(_EntryStrength / 8000.0); // 假设 8000 是最高强度
    
    // 颜色渐变：白 => 红 => 橙 => 黄
    float3 hotColors[4] = {
        float3(1.0, 1.0, 1.0),   // 白色 (高温)
        float3(1.0, 0.2, 0.2),   // 红色
        float3(1.0, 0.6, 0.0),   // 橙色
        float3(1.0, 1.0, 0.0)    // 黄色
    };
    
    // 根据温度因子选择颜色
    float colorIndex = temperatureFactor * 3.0;
    int index1 = (int)floor(colorIndex);
    int index2 = min(index1 + 1, 3);
    float blendFactor = frac(colorIndex);
    
    float3 headColor = lerp(hotColors[index1], hotColors[index2], blendFactor);
    
    // 蓝紫色（背风面尾部）
    float3 coolColor = float3(0.4, 0.2, 0.8); // 紫色
    float3 coldColor = float3(0.2, 0.4, 1.0); // 蓝色
    
    // 根据 trailPos 控制从头部高温到尾部低温的过渡
    float headToTail = trailPos.y; // 0=头部, 1=尾部
    
    // 在尾部区域添加蓝紫色
    float coolIntensity = smoothstep(0.7, 1.0, headToTail);
    float3 tailCoolColor = lerp(coolColor, coldColor, saturate((headToTail - 0.7) * 3.33)); // 0.7-1.0 映射到 0-1
    
    // 主要颜色混合：头部高温颜色过渡到尾部冷色
    float3 finalColor;
    if (headToTail < 0.7) {
        // 头部和中部：高温颜色
        finalColor = lerp(headColor, col.rgb, 0.3); // 保留一些原始颜色
    } else {
        // 尾部：冷色调
        float coolBlend = (headToTail - 0.7) * 3.33; // 0.7-1.0 映射到 0-1
        finalColor = lerp(col.rgb, tailCoolColor, coolBlend * _BlueMultiplier);
    }
    
    col.rgb = finalColor;
    // =================================================

    // ---------- UV 计算 (包含滚动、缩放) ----------
    float2 scrollScale = float2( lerp(0.6, 0.1, IN.layer),
                                 lerp(-8.0, -0.2, IN.layer) );
    float2 timeOffset  = float2(_Time.y * scrollScale.x,
                               _Time.y * scrollScale.y * (entrySpeed + 0.5));
    float2 scale0 = float2(0.1, 2);
    float2 scale1 = float2( lerp(1.0, 0.2, trailPosScalar0 + 0.5),
                           lerp(1.0, 0.1, trailPosScalar0 + 0.5) );
    float2 uv = lerp(scale0, scale1, IN.layer) *
                trailPos + float2(angle,0) - timeOffset;

    // ---------- 噪声 ----------
    int noiseChannel = (int)round(lerp(3.0, 2.0, IN.layer));   // 强制转 int
    float noise = NoiseStatic(uv, noiseChannel) *
                  speedScalar * trailPosScalar;
    noise = max(0.0, noise);

    // ---------- Alpha 组合 ----------
    float alpha0 = saturate(col.a + noise * 0.05);
    float alpha1 = saturate(col.a - (noise * col.a * 7.0));

    float scalar0 = 0.1 + invTrailPosScalar * 0.05;
    float scalar1 = 1.0 - trailPosScalar0;

    col.a = lerp(alpha0, alpha1, IN.layer) *
            lerp(scalar0, scalar1, IN.layer) *
            _OpacityMultiplier *
            lerp(1.0, _WrapOpacityMultiplier, IN.layer);

    // ---------- HDR 处理 ----------
    float aClamped = saturate(col.a);
    col.rgb *= lerp(aClamped * 1.3, 1.0, _Hdr);
    col.a    = lerp(1.0, aClamped, _Hdr);

    // ---------- Dither (减轻带状噪点) ----------
    float dithGran = 0.5 / 255.0;
    float dither = _DitherTex.SampleLevel(
                       sampler_DitherTex,
                       IN.screenPos.xy / _ScreenParams.xy * 500.0 + _Time.x,
                       0).r * trailPos.y;
    float3 cd = lerp(col.rgb, dither, dithGran);
    col.rgb = lerp(cd + (-dithGran / 4.0), col.rgb, _Hdr);

    return col;
}



