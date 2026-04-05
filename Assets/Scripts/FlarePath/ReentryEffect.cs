using System;
using System.Collections.Generic;
using Assets.Scripts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 将速度、模型尺寸、Airstream阴影等信息传递给 AtmosphericReentry Shader。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ReEntryEffect : MonoBehaviourBase, IFlightFixedUpdate
{
    private static Shader _depthOnlyShader;

    private static readonly Color PrimaryColor = new Color(1f, 0.8f, 0.4f, 1f);
    private static readonly Color SecondaryColor = new Color(0.9f, 0.2f, 0.1f, 1f);
    private static readonly Color TertiaryColor = new Color(0.6f, 0.05f, 0f, 1f);
    private static readonly Color StreakColor = new Color(1f, 0.6f, 0.2f, 1f);
    private static readonly Color LayerColor = new Color(0.4f, 0.6f, 1f, 1f);
    private static readonly Color LayerStreakColor = Color.white;

    [Header("动力学参数")] public float entryStrength = 2000f;
    public Vector3 velocityWorld = Vector3.zero;
    [Range(1, 10)] public int shadowRenderInterval = 3;

    [Header("性能优化")] public float nearCullDistance = 20f;
    public float nearFadeDistance = 45f;
    public float shadowDisableDistance = 35f;
    [Tooltip("全场景每帧最多为多少个再入特效渲染 Airstream 深度（按距相机远近优先）。")]
    public int maxShadowInstances = 12;

    private static readonly List<ReEntryEffect> ShadowBudgetInstances = new List<ReEntryEffect>(64);
    private static int s_shadowBudgetFrame = -1;
    private bool _shadowSlotGranted;

    private float angleOfAttack = 0f;
    private bool autoCalculateAngleOfAttack = true;
    private bool _effectActive = true;
    private float _visibilityFactor = 1f;

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
    private Camera _cachedMainCamera;

    void Awake()
    {

        effectRenderer = GetComponent<Renderer>();
        _mat = effectRenderer.material;
        effectRenderer.material = _mat;
        if (_depthOnlyShader == null)
        {
            _depthOnlyShader = Shader.Find("Hidden/DepthOnly");
        }

        _shadowRenderOffset = Math.Abs(GetInstanceID()) % Mathf.Max(1, shadowRenderInterval);
        ApplyStaticMaterialParams();

        mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            originalBounds = mf.sharedMesh.bounds;
        }
        else
        {
            originalBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
        }

        UpdateExtendedBounds();
    }

    void OnEnable()
    {
        if (!ShadowBudgetInstances.Contains(this))
        {
            ShadowBudgetInstances.Add(this);
        }
    }

    void OnDisable()
    {
        ShadowBudgetInstances.Remove(this);
    }

    void Update()
    {
        EnsureShadowBudgetForFrame();

        if (!_effectActive)
        {
            return;
        }

        if (autoCalculateAngleOfAttack && velocityWorld.sqrMagnitude > 0.01f)
        {
            Vector3 forward = transform.forward;
            float dot = Vector3.Dot(forward, velocityWorld.normalized);
            dot = Mathf.Clamp(dot, -1f, 1f);
            angleOfAttack = Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        if (velocityWorld.sqrMagnitude <= 0.01f)
        {
            if (effectRenderer.enabled)
            {
                effectRenderer.enabled = false;
            }
            ReleaseShadowResources();
            return;
        }

        float distanceToCamera = GetDistanceToMainCamera();
        _visibilityFactor = GetVisibilityFactor(distanceToCamera);

        if (_visibilityFactor <= 0.01f)
        {
            if (effectRenderer.enabled)
            {
                effectRenderer.enabled = false;
            }
            ReleaseShadowResources();
            return;
        }

        if (!effectRenderer.enabled)
        {
            effectRenderer.enabled = true;
        }

        Vector3 velocityDir = velocityWorld.normalized;
        bool shouldRenderShadow = ShouldRenderShadow(distanceToCamera, _shadowSlotGranted);

        if (shouldRenderShadow)
        {
            EnsureShadowResources();
            if (_airstreamCam != null && _shadowRT != null)
            {
                _airstreamCam.transform.position = transform.position - velocityDir * 0.5f;
                _airstreamCam.transform.rotation = Quaternion.LookRotation(velocityDir, Vector3.up);

                if (_depthOnlyShader != null)
                {
                    _airstreamCam.RenderWithShader(_depthOnlyShader, "RenderType=DepthOnly");
                    _mat.SetMatrix("_AirstreamVP", _airstreamCam.projectionMatrix * _airstreamCam.worldToCameraMatrix);
                }
            }
        }
        else
        {
            ReleaseShadowResources();
        }

        SetMat();
    }

    public void SetEffectActive(bool active)
    {
        _effectActive = active;
        if (!_effectActive)
        {
            if (effectRenderer != null && effectRenderer.enabled)
            {
                effectRenderer.enabled = false;
            }
            ReleaseShadowResources();
        }
    }

    void OnDestroy()
    {
        ReleaseShadowResources();

        if (mf != null)
        {
            mf.mesh = Instantiate(mf.sharedMesh);
            mf.mesh.bounds = originalBounds;
        }
    }

    private void SetMat()
    {
        _mat.SetFloat("_EntryStrength", entryStrength);
        _mat.SetVector("_Velocity", velocityWorld.normalized);
        _mat.SetFloat("_FxState", fxState);
        _mat.SetFloat("_AngleOfAttack", angleOfAttack);
        _mat.SetFloat("_LengthMultiplier", lengthMultiplier);
        _mat.SetFloat("_TrailAlphaMultiplier", trailAlphaMultiplier * _visibilityFactor);
        _mat.SetFloat("_OpacityMultiplier", opacityMultiplier * _visibilityFactor);
        _mat.SetFloat("_WrapOpacityMultiplier", wrapOpacityMultiplier * _visibilityFactor);
        _mat.SetFloat("_WrapFresnelModifier", wrapFresnelModifier);
        _mat.SetFloat("_StreakProbability", streakProbability);
        _mat.SetFloat("_StreakThreshold", streakThreshold);
        _mat.SetVector("_RandomnessFactor", randomnessFactor);
        _mat.SetVector("_ModelScale", transform.lossyScale);
        _mat.SetVector("_EnvelopeScaleFactor", new Vector4(1, 1, 1, 1));
        _mat.SetInt("_DisableBowshock", enableBowshock && _visibilityFactor > 0.25f ? 0 : 1);
        _mat.SetColor("_ShockwaveColor", shockwaveColor * bowshockIntensity * _visibilityFactor);
        _mat.SetFloat("_BowshockForwardDistance", bowshockForwardDistance);
        _mat.SetFloat("_BowshockRadiusScale", bowshockRadiusScale);
    }

    private static void EnsureShadowBudgetForFrame()
    {
        int frame = Time.frameCount;
        if (frame == s_shadowBudgetFrame)
        {
            return;
        }

        s_shadowBudgetFrame = frame;

        Camera cam = null;
        if (Game.Instance != null && Game.Instance.FlightScene?.ViewManager?.GameView?.GameCamera != null)
        {
            cam = Game.Instance.FlightScene.ViewManager.GameView.GameCamera.NearCamera;
        }

        int cap = 0;
        for (int i = 0; i < ShadowBudgetInstances.Count; i++)
        {
            ReEntryEffect e = ShadowBudgetInstances[i];
            if (e != null)
            {
                cap = Mathf.Max(cap, e.maxShadowInstances);
            }
        }

        if (cam == null || ShadowBudgetInstances.Count == 0)
        {
            for (int i = 0; i < ShadowBudgetInstances.Count; i++)
            {
                ReEntryEffect e = ShadowBudgetInstances[i];
                if (e != null)
                {
                    e._shadowSlotGranted = false;
                }
            }
            return;
        }

        Vector3 camPos = cam.transform.position;
        ShadowBudgetInstances.Sort((a, b) =>
        {
            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            float da = (a.transform.position - camPos).sqrMagnitude;
            float db = (b.transform.position - camPos).sqrMagnitude;
            return da.CompareTo(db);
        });

        for (int i = 0; i < ShadowBudgetInstances.Count; i++)
        {
            ReEntryEffect e = ShadowBudgetInstances[i];
            if (e == null)
            {
                continue;
            }

            e._shadowSlotGranted = i < cap;
        }
    }

    private bool ShouldRenderShadow(float distanceToCamera, bool shadowSlotGranted)
    {
        return _depthOnlyShader != null
               && distanceToCamera >= shadowDisableDistance
               && shadowSlotGranted;
    }
    

    private float GetDistanceToMainCamera()
    {
        if (_cachedMainCamera == null || Time.frameCount % 30 == 0)
        {
            _cachedMainCamera = Game.Instance.FlightScene.ViewManager.GameView.GameCamera.NearCamera;
        }

        if (_cachedMainCamera == null)
        {
            return float.PositiveInfinity;
        }

        return Vector3.Distance(_cachedMainCamera.transform.position, transform.position);
    }

    private float GetVisibilityFactor(float distanceToCamera)
    {
        if (distanceToCamera <= nearCullDistance)
        {
            return 0f;
        }

        if (distanceToCamera >= nearFadeDistance)
        {
            return 1f;
        }

        return Mathf.InverseLerp(nearCullDistance, nearFadeDistance, distanceToCamera);
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

    private void EnsureShadowResources()
    {
        if (_shadowRT == null)
        {
            _shadowRT = new RenderTexture(512, 512, 16, RenderTextureFormat.Depth)
            {
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _shadowRT.Create();
            _mat.SetTexture("_AirstreamTex", _shadowRT);
        }

        if (_airstreamCam == null)
        {
            GameObject camObj = new GameObject("AirstreamShadowCam");
            camObj.hideFlags = HideFlags.HideAndDontSave;
            _airstreamCam = camObj.AddComponent<Camera>();
            _airstreamCam.enabled = false;
            _airstreamCam.orthographic = false;
            _airstreamCam.clearFlags = CameraClearFlags.Depth;
            _airstreamCam.backgroundColor = Color.white;
            _airstreamCam.cullingMask = 1 << gameObject.layer;
            _airstreamCam.nearClipPlane = 0.01f;
            _airstreamCam.farClipPlane = 30f;
        }

        _airstreamCam.targetTexture = _shadowRT;
    }

    private void ReleaseShadowResources()
    {
        if (_airstreamCam != null)
        {
            DestroyImmediate(_airstreamCam.gameObject);
            _airstreamCam = null;
        }

        if (_shadowRT != null)
        {
            _shadowRT.Release();
            Destroy(_shadowRT);
            _shadowRT = null;
        }
    }

    private void LateUpdate()
    {
        if (_effectActive)
        {
            UpdateExtendedBounds();
        }
    }

    private void UpdateExtendedBounds()
    {
        if (effectRenderer == null)
        {
            return;
        }

        float estimatedTrailLength = entryStrength * 0.02f;
        estimatedTrailLength = Mathf.Max(150f, estimatedTrailLength);
        estimatedTrailLength *= lengthMultiplier;

        Vector3 velocityDir = velocityWorld.sqrMagnitude > 0.01f ? velocityWorld.normalized : transform.forward;
        Vector3 center = transform.position + velocityDir * (estimatedTrailLength * 0.4f);

        float sideRadius = 80f + estimatedTrailLength * 0.15f;
        float finalRadius = Mathf.Max(sideRadius, estimatedTrailLength * 0.6f);

        Bounds extended = new Bounds(center, new Vector3(finalRadius * 2f, finalRadius * 2f, finalRadius * 2f));
        effectRenderer.bounds = extended;

        if (mf != null && mf.mesh != null)
        {
            mf.mesh.bounds = extended;
        }
    }

    public void FlightFixedUpdate(in FlightFrameData frame)
    {
    }
}
