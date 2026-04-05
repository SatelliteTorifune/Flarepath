using System;
using System.Collections.Generic;
using Assets.Scripts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

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

    public float entryStrength = 2000f;
    public Vector3 velocityWorld = Vector3.zero;
    public int shadowRenderInterval = 3;

    public float nearCullDistance = 20f;
    public float nearFadeDistance = 45f;
    public float shadowDisableDistance = 35f;
    public int maxShadowInstances = 12;

    public bool enableScreenSpaceLod = true;
    public float lodScreenEstimateMaxWorldRadius = 220f;
    public float screenCoverageSmall = 0.04f;
    public float screenCoverageHeavy = 0.22f;
    public float screenCoverageMaxOverdraw = 0.55f;
    public float screenLodSmallDetail = 0.4f;
    public float screenLodHeavyDetail = 0.62f;
    public float screenCoverageBowshockOff = 0.38f;
    public float screenCoverageShadowOff = 0.42f;

    private static readonly List<ReEntryEffect> ShadowBudgetInstances = new List<ReEntryEffect>(64);
    private static int s_shadowBudgetFrame = -1;
    private bool _shadowSlotGranted;

    private float angleOfAttack = 0f;
    private bool autoCalculateAngleOfAttack = true;
    private bool _effectActive = true;
    private float _visibilityFactor = 1f;
    private float _screenCoverage;
    private float _detailLod = 1f;

    public float fxState = 0.8f;
    public float lengthMultiplier = 1f;
    public float trailAlphaMultiplier = 1f;
    public float opacityMultiplier = 1f;
    public float wrapOpacityMultiplier = 0.5f;
    public float wrapFresnelModifier = 0f;
    public float streakProbability = 0.1f;
    public float streakThreshold = -0.2f;

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
            float dot = Vector3.Dot(transform.forward, velocityWorld.normalized);
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
        ComputeScreenLod(_cachedMainCamera, distanceToCamera);

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
        bool shouldRenderShadow = ShouldRenderShadow(distanceToCamera, _shadowSlotGranted, _screenCoverage);

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
        float visLod = _visibilityFactor * _detailLod;
        float streakScale = 1f;
        if (enableScreenSpaceLod)
        {
            if (_screenCoverage < screenCoverageSmall)
            {
                streakScale = Mathf.Lerp(screenLodSmallDetail, 1f, _screenCoverage / Mathf.Max(screenCoverageSmall, 1e-4f));
            }

            streakScale *= _detailLod;
        }

        _mat.SetFloat("_EntryStrength", entryStrength);
        _mat.SetVector("_Velocity", velocityWorld.normalized);
        _mat.SetFloat("_FxState", fxState);
        _mat.SetFloat("_AngleOfAttack", angleOfAttack);
        _mat.SetFloat("_LengthMultiplier", lengthMultiplier);
        _mat.SetFloat("_TrailAlphaMultiplier", trailAlphaMultiplier * visLod);
        _mat.SetFloat("_OpacityMultiplier", opacityMultiplier * visLod);
        _mat.SetFloat("_WrapOpacityMultiplier", wrapOpacityMultiplier * visLod);
        _mat.SetFloat("_WrapFresnelModifier", wrapFresnelModifier);
        _mat.SetFloat("_StreakProbability", streakProbability * streakScale);
        _mat.SetFloat("_StreakThreshold", streakThreshold);
        _mat.SetVector("_RandomnessFactor", randomnessFactor);
        _mat.SetVector("_ModelScale", transform.lossyScale);
        _mat.SetVector("_EnvelopeScaleFactor", new Vector4(1, 1, 1, 1));

        bool bowshockOk = enableBowshock && _visibilityFactor > 0.25f
                                         && (!enableScreenSpaceLod || _screenCoverage < screenCoverageBowshockOff);
        _mat.SetInt("_DisableBowshock", bowshockOk ? 0 : 1);
        _mat.SetColor("_ShockwaveColor", shockwaveColor * bowshockIntensity * visLod);
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

    private bool ShouldRenderShadow(float distanceToCamera, bool shadowSlotGranted, float screenCoverage)
    {
        if (_depthOnlyShader == null
            || distanceToCamera < shadowDisableDistance
            || !shadowSlotGranted)
        {
            return false;
        }

        if (enableScreenSpaceLod && screenCoverage >= screenCoverageShadowOff)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 用 capped 的世界半径估算相对视场高度的占比，并得出 0~1 的细节系数（占屏过小或过大都会降低）。
    /// </summary>
    private void ComputeScreenLod(Camera cam, float distanceToCamera)
    {
        if (!enableScreenSpaceLod || cam == null)
        {
            _screenCoverage = 0f;
            _detailLod = 1f;
            return;
        }

        float estimatedTrailLength = entryStrength * 0.02f;
        estimatedTrailLength = Mathf.Max(150f, estimatedTrailLength);
        estimatedTrailLength *= lengthMultiplier;

        float sideRadius = 80f + estimatedTrailLength * 0.15f;
        float finalRadius = Mathf.Max(sideRadius, estimatedTrailLength * 0.6f);
        float lodRadius = Mathf.Min(finalRadius, lodScreenEstimateMaxWorldRadius);

        float angular = 2f * Mathf.Atan(lodRadius / Mathf.Max(distanceToCamera, 0.01f));
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        _screenCoverage = Mathf.Clamp01(angular / Mathf.Max(fovRad, 1e-4f));

        if (_screenCoverage < screenCoverageSmall)
        {
            _detailLod = Mathf.Lerp(screenLodSmallDetail, 1f, _screenCoverage / Mathf.Max(screenCoverageSmall, 1e-4f));
        }
        else if (_screenCoverage > screenCoverageHeavy)
        {
            float t = Mathf.InverseLerp(screenCoverageHeavy, Mathf.Max(screenCoverageMaxOverdraw, screenCoverageHeavy + 1e-4f), _screenCoverage);
            _detailLod = Mathf.Lerp(1f, screenLodHeavyDetail, Mathf.Clamp01(t));
        }
        else
        {
            _detailLod = 1f;
        }
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
