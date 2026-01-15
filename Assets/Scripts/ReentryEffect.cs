using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 将速度、模型尺寸、Airstream阴影等信息传递给 AtmosphericReentry Shader。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ReEntryEffect : MonoBehaviour
{
    // ----- 可在 Inspector 调整的属性 -----
    [Header("动力学参数")] public float entryStrength = 2000f; // 由外部系统填入（如影响速度等）
    public Vector3 velocityWorld = Vector3.zero; // 世界空间速度 (m/s)

    [Tooltip("攻角（度）。如果启用自动计算，将根据速度方向和物体朝向自动计算")]
    public float angleOfAttack = 0f; // 角度攻击度 (度)

    [Tooltip("是否自动计算攻角（基于速度方向和物体朝向）")] public bool autoCalculateAngleOfAttack = true;

    [Header("视觉调节")] [Range(0, 1)] public float fxState = 0.8f;
    [Range(0, 10)] public float lengthMultiplier = 1f;
    [Range(0, 2)] public float trailAlphaMultiplier = 1f;
    [Range(0, 5)] public float opacityMultiplier = 1f;
    [Range(0, 1)] public float wrapOpacityMultiplier = 0.5f;
    [Range(0, 1)] public float wrapFresnelModifier = 0f;
    [Range(0, 1)] public float streakProbability = 0.1f;
    [Range(-1, 0)] public float streakThreshold = -0.2f;

    [Header("随机性")] [Tooltip("x – streak 随机性  y – wrap 随机性")]
    public Vector2 randomnessFactor = new Vector2(0.5f, 0.5f);

    [Header("Bowshock (前向蓝色等离子层)")] [Tooltip("是否启用Bowshock效果")]
    public bool enableBowshock = true;

    [Tooltip("Bowshock强度 (0-1)")] [Range(0, 1)]
    public float bowshockIntensity = 0.8f;

    [Tooltip("Bowshock颜色 (蓝色高温等离子体)")] public Color shockwaveColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);

    [Tooltip("Bowshock向前延伸距离")] [Range(0, 2)]
    public float bowshockForwardDistance = 0.3f;

    [Tooltip("Bowshock半径缩放")] [Range(0, 3)]
    public float bowshockRadiusScale = 1.0f;

    [Header("剔除优化")] [Tooltip("扩展包围盒大小（单位：米），用于防止尾迹被视锥剔除")]
    public Vector3 boundsExtension = new Vector3(50f, 50f, 200f); // x/y 宽，z 向后长（根据尾迹长度调）

    private Bounds originalBounds;
    
    public float minTemp = 600f;
    public float ignitionTemp = 900f;
    public float maxTemp = 2800f;


    // ---------- 内部引用 ----------
    private Renderer _rend;
    private Material _mat;
    private Camera _airstreamCam;
    private RenderTexture _shadowRT;

    void Awake()
    {
        _rend = GetComponent<Renderer>();
        // 为了不污染共享材质，实例化一份
        _mat = _rend.material;
        _rend.material = _mat;

        // 创建用于 Airstream 深度的 RT（分辨率 512 按需求可调）
        _shadowRT = new RenderTexture(512, 512, 16, RenderTextureFormat.Depth);
        _shadowRT.useMipMap = false;
        _shadowRT.autoGenerateMips = false;
        _shadowRT.filterMode = FilterMode.Bilinear;
        _shadowRT.wrapMode = TextureWrapMode.Clamp;
        _shadowRT.Create();

        // 创建临时摄像机（不渲染到屏幕，只渲染深度）
        GameObject camObj = new GameObject("AirstreamShadowCam");
        camObj.hideFlags = HideFlags.HideAndDontSave;
        _airstreamCam = camObj.AddComponent<Camera>();
        _airstreamCam.enabled = false;
        _airstreamCam.orthographic = false;
        _airstreamCam.clearFlags = CameraClearFlags.Depth;
        _airstreamCam.backgroundColor = Color.white;
        _airstreamCam.cullingMask = 1 << gameObject.layer; // 只渲染本层（默认0）
        _airstreamCam.targetTexture = _shadowRT;
        _airstreamCam.nearClipPlane = 0.01f;
        _airstreamCam.farClipPlane = 30f; // 视实际需求而定

        // 把深度纹理绑定到 shader
        _mat.SetTexture("_AirstreamTex", _shadowRT);

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            originalBounds = mf.sharedMesh.bounds;
        }
        else
        {
            originalBounds = new Bounds(Vector3.zero, Vector3.one * 10f); // 默认
        }

        // 强制扩展 bounds（让 Unity 认为物体很大）
        UpdateExtendedBounds();
    }

    void OnDestroy()
    {
        if (_shadowRT) _shadowRT.Release();
        if (_airstreamCam) DestroyImmediate(_airstreamCam.gameObject);
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mf.mesh = Instantiate(mf.sharedMesh); // 或直接用 sharedMesh
            mf.mesh.bounds = originalBounds;
        }
    }

    void Update()
    {
        // 自动计算攻角（速度方向与物体前方向的夹角）
        float calculatedAOA = angleOfAttack;
        if (autoCalculateAngleOfAttack && velocityWorld.magnitude > 0.1f)
        {
            Vector3 forward = transform.forward;
            Vector3 velocityDir = velocityWorld.normalized;

            // 计算速度方向与物体前方向的夹角（度）
            float dot = Vector3.Dot(forward, velocityDir);
            dot = Mathf.Clamp(dot, -1f, 1f);
            calculatedAOA = Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        // 1) 传递矩阵（摄像机的 VP 矩阵，用于 Shadow 采样）
        _airstreamCam.transform.position = transform.position - velocityWorld.normalized * 0.5f;
        _airstreamCam.transform.rotation = Quaternion.LookRotation(velocityWorld.normalized, Vector3.up);
        // 重新渲染深度（一次帧一次，性能成本很低）
        _airstreamCam.RenderWithShader(Shader.Find("Hidden/DepthOnly"), "RenderType=DepthOnly");
        _mat.SetMatrix("_AirstreamVP", _airstreamCam.projectionMatrix * _airstreamCam.worldToCameraMatrix);

        // 2) 其它数值
        _mat.SetFloat("_EntryStrength", entryStrength);
        _mat.SetVector("_Velocity", velocityWorld);
        _mat.SetFloat("_FxState", fxState);
        _mat.SetFloat("_AngleOfAttack", calculatedAOA);
        _mat.SetFloat("_LengthMultiplier", lengthMultiplier);
        _mat.SetFloat("_TrailAlphaMultiplier", trailAlphaMultiplier);
        _mat.SetFloat("_OpacityMultiplier", opacityMultiplier);
        _mat.SetFloat("_WrapOpacityMultiplier", wrapOpacityMultiplier);
        _mat.SetFloat("_WrapFresnelModifier", wrapFresnelModifier);
        _mat.SetFloat("_StreakProbability", streakProbability);
        _mat.SetFloat("_StreakThreshold", streakThreshold);
        _mat.SetVector("_RandomnessFactor", randomnessFactor);
        _mat.SetVector("_ModelScale", transform.lossyScale);
        _mat.SetVector("_EnvelopeScaleFactor", new Vector4(1, 1, 1, 1));

        // 3) 颜色（可以随需求调）
        _mat.SetColor("_PrimaryColor", new Color(1, 0.8f, 0.4f, 1));
        _mat.SetColor("_SecondaryColor", new Color(0.9f, 0.2f, 0.1f, 1));
        _mat.SetColor("_TertiaryColor", new Color(0.6f, 0.05f, 0, 1));
        _mat.SetColor("_StreakColor", new Color(1, 0.6f, 0.2f, 1));
        _mat.SetColor("_LayerColor", new Color(0.4f, 0.6f, 1, 1));
        _mat.SetColor("_LayerStreakColor", new Color(1, 1, 1, 1));

        // 4) Bowshock 参数
        _mat.SetInt("_DisableBowshock", enableBowshock ? 0 : 1);
        _mat.SetColor("_ShockwaveColor", shockwaveColor * bowshockIntensity);
        _mat.SetFloat("_BowshockForwardDistance", bowshockForwardDistance);
        _mat.SetFloat("_BowshockRadiusScale", bowshockRadiusScale);

    }

    private void LateUpdate()
    {
        UpdateExtendedBounds();
    }

    private void UpdateExtendedBounds()
    {
        
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        // 计算扩展后的包围盒中心和大小
        Vector3 center = transform.position;
        Vector3 extendedSize = originalBounds.size + boundsExtension * 2f; // 两边扩展

        // 向后延伸（沿速度方向）
        Vector3 velocityDir = velocityWorld.normalized;
        center += velocityDir * (boundsExtension.z * 0.5f); // 中心后移

        Bounds extended = new Bounds(center, extendedSize);

        // 应用到 Mesh.bounds（注意：这里直接修改 sharedMesh.bounds 会影响所有实例，推荐用自定义 Mesh）
        // 更安全做法：创建一个自定义 Mesh 副本
        if (mf.mesh.bounds != extended)
        {
            Mesh customMesh = Instantiate(mf.sharedMesh); // 复制一份，避免影响原始
            customMesh.bounds = extended;
            mf.mesh = customMesh;
        }
    }

   
}