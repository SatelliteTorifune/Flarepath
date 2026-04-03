using System;
using Assets.Scripts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 将速度、模型尺寸、Airstream阴影等信息传递给 AtmosphericReentry Shader。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ReEntryEffect : MonoBehaviourBase,IFlightFixedUpdate
{
    private static Shader _depthOnlyShader;
    private static readonly Color PrimaryColor = new Color(1f, 0.8f, 0.4f, 1f);
    private static readonly Color SecondaryColor = new Color(0.9f, 0.2f, 0.1f, 1f);
    private static readonly Color TertiaryColor = new Color(0.6f, 0.05f, 0f, 1f);
    private static readonly Color StreakColor = new Color(1f, 0.6f, 0.2f, 1f);
    private static readonly Color LayerColor = new Color(0.4f, 0.6f, 1f, 1f);
    private static readonly Color LayerStreakColor = Color.white;

    [Header("动力学参数")] public float entryStrength = 2000f; // 由外部系统填入（如影响速度等）
    public Vector3 velocityWorld = Vector3.zero; // 世界空间速度 (m/s)
    [Range(1, 10)] public int shadowRenderInterval = 3;
    
    private float angleOfAttack = 0f;

    private bool autoCalculateAngleOfAttack = true;

    public float fxState = 0.8f;
    [Range(0, 10)] public float lengthMultiplier = 1f;
    [Range(0, 2)] public float trailAlphaMultiplier = 1f;
    [Range(0, 5)] public float opacityMultiplier = 1f;
    [Range(0, 1)] public float wrapOpacityMultiplier = 0.5f;
    [Range(0, 1)] public float wrapFresnelModifier = 0f;
    [Range(0, 1)] public float streakProbability = 0.1f;
    [Range(-1, 0)] public float streakThreshold = -0.2f;
    
    public Vector2 randomnessFactor = new Vector2(0.5f, 0.5f);
    public bool enableBowshock = true;
    public Color shockwaveColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);
    public float bowshockIntensity = 0.8f;
    public float bowshockForwardDistance = 0.3f;
    public float bowshockRadiusScale = 1.0f;
    
    private Bounds originalBounds;
    public float minTemp = 600f;
    public float ignitionTemp = 900f;
    public float maxTemp = 2800f;

    
    public Renderer effectRenderer;
    private Material _mat;
    private Camera _airstreamCam;
    private RenderTexture _shadowRT;
    private int _shadowRenderOffset;

    private MeshFilter mf;

    void Awake()
    {
        effectRenderer = GetComponent<Renderer>();
        // 为了不污染共享材质，实例化一份
        _mat = effectRenderer.material;
        effectRenderer.material = _mat;
        if (_depthOnlyShader == null)
        {
            _depthOnlyShader = Shader.Find("Hidden/DepthOnly");
        }
        _shadowRenderOffset = Math.Abs(GetInstanceID()) % Mathf.Max(1, shadowRenderInterval);

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
        ApplyStaticMaterialParams();

        mf = GetComponent<MeshFilter>();
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
    
    void Update()
    {
        if (autoCalculateAngleOfAttack && velocityWorld.magnitude > 0.1f)
        {
            Vector3 forward = transform.forward;
            Vector3 velocityDir = velocityWorld.normalized;

            // 计算速度方向与物体前方向的夹角（度）
            float dot = Vector3.Dot(forward, velocityDir);
            dot = Mathf.Clamp(dot, -1f, 1f);
            angleOfAttack = Mathf.Acos(dot) * Mathf.Rad2Deg;
        }
        if (velocityWorld.sqrMagnitude > 0.01f) // 避免除以零
        {
            Vector3 velocityDir = velocityWorld.normalized;

            _airstreamCam.transform.position = transform.position - velocityDir * 0.5f;
            _airstreamCam.transform.rotation = Quaternion.LookRotation(velocityDir, Vector3.up);

            if (_depthOnlyShader != null && ShouldRenderShadowThisFrame())
            {
                // 降低更新频率，减少大量实例时的 Camera.Render 压力
                _airstreamCam.RenderWithShader(_depthOnlyShader, "RenderType=DepthOnly");
                _mat.SetMatrix("_AirstreamVP", _airstreamCam.projectionMatrix * _airstreamCam.worldToCameraMatrix);
            }
            SetMat();
        }

        

    }
    
    
    void OnDestroy()
    {
        if (_shadowRT) _shadowRT.Release();
        if (_airstreamCam) DestroyImmediate(_airstreamCam.gameObject);
        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mf.mesh = Instantiate(mf.sharedMesh); // 或直接用 sharedMesh
            mf.mesh.bounds = originalBounds;
        }
    }

    private void SetMat()
    {
        // 2) 其它数值
        _mat.SetFloat("_EntryStrength", entryStrength);
        _mat.SetVector("_Velocity", velocityWorld.normalized);
        _mat.SetFloat("_FxState", fxState);
        _mat.SetFloat("_AngleOfAttack", angleOfAttack);
        _mat.SetFloat("_LengthMultiplier", lengthMultiplier);
        _mat.SetFloat("_TrailAlphaMultiplier", trailAlphaMultiplier);
        _mat.SetFloat("_OpacityMultiplier", opacityMultiplier);
        _mat.SetFloat("_WrapOpacityMultiplier", wrapOpacityMultiplier);
        _mat.SetFloat("_WrapFresnelModifier", wrapFresnelModifier);
        _mat.SetFloat("_StreakProbability", streakProbability);
        _mat.SetFloat("_StreakThreshold", streakThreshold);
        _mat.SetVector("_RandomnessFactor", randomnessFactor);
        _mat.SetVector("_ModelScale", transform.lossyScale);
        _mat.SetVector("_EnvelopeScaleFactor", new Vector4(1,1,1,1));

        // Bowshock 参数
        _mat.SetInt("_DisableBowshock", enableBowshock ? 0 : 1);
        _mat.SetColor("_ShockwaveColor", shockwaveColor * bowshockIntensity);
        _mat.SetFloat("_BowshockForwardDistance", bowshockForwardDistance);
        _mat.SetFloat("_BowshockRadiusScale", bowshockRadiusScale);
    }

    private bool ShouldRenderShadowThisFrame()
    {
        int interval = Mathf.Max(1, shadowRenderInterval);
        if (interval == 1)
        {
            return true;
        }

        return (Time.frameCount + _shadowRenderOffset) % interval == 0;
    }

    private void ApplyStaticMaterialParams()
    {
        _mat.SetColor("_PrimaryColor", PrimaryColor);
        _mat.SetColor("_SecondaryColor", SecondaryColor);
        _mat.SetColor("_TertiaryColor", TertiaryColor);
        _mat.SetColor("_StreakColor", StreakColor);
        _mat.SetColor("_LayerColor", LayerColor);
        _mat.SetColor("_LayerStreakColor", LayerStreakColor);
    }
    private void LateUpdate() => UpdateExtendedBounds();

    private void UpdateExtendedBounds()
    {
        if (effectRenderer == null) return;

        // 1. 估算尾迹实际长度
        float estimatedTrailLength = entryStrength * 0.02f; 
        estimatedTrailLength = Mathf.Max(150f, estimatedTrailLength); 
        estimatedTrailLength *= lengthMultiplier; 

        // 2. 中心点：物体位置 + 沿速度方向后移一半长度
        Vector3 velocityDir = velocityWorld.normalized;
        Vector3 center = transform.position + velocityDir * (estimatedTrailLength * 0.4f); 

        // 3. 半径：横向 + 纵向保守估计
        float sideRadius = 80f + estimatedTrailLength * 0.15f; 
        float finalRadius = Mathf.Max(sideRadius, estimatedTrailLength * 0.6f);  

        // 4. 用球形包围盒（最稳）
        Bounds extended = new Bounds(center, new Vector3(finalRadius * 2f,finalRadius * 2f,finalRadius * 2f));

        // 5. 应用
        effectRenderer.bounds = extended;

        // 可选：同步 mesh.bounds
        if (mf != null && mf.mesh != null)
        {
            mf.mesh.bounds = extended;
        }
        
    }
    

    public void FlightFixedUpdate(in FlightFrameData frame)
    {
        
    }
}