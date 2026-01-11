using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Renderer))]
public class ReentryManager : MonoBehaviour
{
    [Header("Reentry Settings")]
    public Material reentryMaterial;        // 拖你的 Firefly 材质
    public float entryStrength = 1f;
    public float shadowBias = 0.001f;
    public float shadowStrength = 0.8f;

    [Header("Airstream Depth Texture")]
    public int textureSize = 512;
    public LayerMask layerMask = ~0;        // 默认全层，记得改成只飞船的 Layer！

    private RenderTexture airstreamDepthRT;
    private GameObject virtualCamObj;
    private Camera virtualCam;
    private CommandBuffer cmdBuffer;
    private Material depthMaterial;

    private static readonly int AirstreamTexID = Shader.PropertyToID("_AirstreamTex");
    private static readonly int AirstreamVPID   = Shader.PropertyToID("_AirstreamVP");
    private static readonly int EntryStrengthID = Shader.PropertyToID("_EntryStrength");
    private static readonly int VelocityID      = Shader.PropertyToID("_Velocity");

    void Awake()
    {
        // 创建深度 RT
        airstreamDepthRT = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.Depth);
        airstreamDepthRT.filterMode = FilterMode.Point;
        airstreamDepthRT.Create();
        if (airstreamDepthRT == null) Debug.LogError("Failed to create RenderTexture!");

        // 创建 Depth Material（用我们自己的 shader）
        Shader depthShader = Shader.Find("Hidden/MyDepthOnly");
        if (depthShader == null)
        {
            Debug.LogError("DepthOnly shader not found! Create it at Assets/Shaders/DepthOnly.shader");
            enabled = false;
            return;
        }
        depthMaterial = new Material(depthShader);

        // 创建虚拟相机
        virtualCamObj = new GameObject("AirstreamVirtualCam");
        virtualCamObj.hideFlags = HideFlags.HideAndDontSave;
        virtualCamObj.transform.SetParent(transform);
        virtualCam = virtualCamObj.AddComponent<Camera>();
        virtualCam.enabled = false;
        virtualCam.clearFlags = CameraClearFlags.Depth;
        virtualCam.cullingMask = layerMask;
        virtualCam.nearClipPlane = 0.01f;
        virtualCam.farClipPlane = 1000f;
        virtualCam.fieldOfView = 90f;
        virtualCam.orthographic = false;
    }

    void OnEnable()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("No main camera found!");
            return;
        }

        cmdBuffer = new CommandBuffer { name = "AirstreamDepthPass" };

        // 推荐用 BeforeForwardOpaque 或 AfterForwardOpaque，根据你的需求测试
        // BeforeDepthTexture 有时太早，相机还没准备好
        Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cmdBuffer);
        // 或者试试：Camera.main.AddCommandBuffer(CameraEvent.AfterDepthTexture, cmdBuffer);
    }

    void OnDisable()
    {
        if (Camera.main != null && cmdBuffer != null)
        {
            Camera.main.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, cmdBuffer);
        }

        if (cmdBuffer != null)
        {
            cmdBuffer.Release();
            cmdBuffer = null;
        }
    }

    void OnDestroy()
    {
        if (airstreamDepthRT != null)
        {
            airstreamDepthRT.Release();
            airstreamDepthRT = null;
        }
        if (depthMaterial != null) Destroy(depthMaterial);
        if (virtualCamObj != null) DestroyImmediate(virtualCamObj);
    }

    void LateUpdate()
    {
        if (!reentryMaterial || airstreamDepthRT == null || cmdBuffer == null || Camera.main == null) return;

        
        // 获取速度（假设有 Rigidbody，如果没有自己算）
        Vector3 velocity = Vector3.zero;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) velocity = rb.velocity;

        if (velocity == Vector3.zero) {
            velocity = new Vector3(0, -50, 0); // 强制向下高速
        }
        
        if (velocity.sqrMagnitude < 1f)
        {
            Shader.SetGlobalFloat(EntryStrengthID, 0f);
            return;
        }

        Shader.SetGlobalFloat(EntryStrengthID, entryStrength);
        Shader.SetGlobalVector(VelocityID, velocity);

        // 虚拟相机位置：稍微向后偏移，避免 clipping 自己
        Vector3 velDir = velocity.normalized;
        Vector3 camPos = transform.position - velDir * 5f; // 调整偏移量
        Quaternion camRot = Quaternion.LookRotation(velDir, Vector3.up);

        virtualCam.transform.position = camPos;
        virtualCam.transform.rotation = camRot;

        // 清空并重建 CommandBuffer（每帧重建最安全，避免残留命令）
        cmdBuffer.Clear();

        cmdBuffer.SetRenderTarget(airstreamDepthRT);
        cmdBuffer.ClearRenderTarget(true, false, Color.clear);

        Matrix4x4 view = virtualCam.worldToCameraMatrix;
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(virtualCam.projectionMatrix, false);
        Matrix4x4 vp = proj * view;
        cmdBuffer.SetGlobalMatrix(AirstreamVPID, vp);

        // 渲染飞船（用 depth material）
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (rend != null)
                cmdBuffer.DrawRenderer(rend, depthMaterial);
        }
        

        cmdBuffer.SetGlobalTexture(AirstreamTexID, airstreamDepthRT);
        Debug.Log("EntryStrength: " + entryStrength + " | Velocity mag: " + velocity.magnitude);
    }
}