using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OcclusionConeTestLegacy : MonoBehaviour
{
    public bool show=true;
    [Header("Cone Parameters")]
    public Vector3 direction = Vector3.back;          // 锥轴方向（向后），会自动 normalized
    public float projectedRadius = 1.5f;              // 鼻部半径
    public float invFineness = 0.5f;                  // 1 / fineness (0=尖, 1+=钝)
    public float sqrtMach = 2.5f;                     // √Mach
    public float detachAngle = 0.5f;                  // 分离角阈值（弧度）

    [Header("Visualization")]
    public float coneLength = 20f;                    // 锥体显示长度
    public int radialSegments = 24;                   // 圆周分段（越高越圆）
    public int heightSegments = 8;                    // 高度分段（越多越平滑）
    public Material coneMaterial;                     // 拖一个半透明材质进来（或下面代码自动创建）

    // Computed
    private double shockAngle;
    private Vector3 nosePosition;

    private MeshFilter meshFilter;
    private Mesh mesh;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        nosePosition = transform.position;
        direction = direction.normalized;

        Setup();
        //GenerateConeMesh();
    }

    private void Setup()
    {
        double num3 = System.Math.Asin(invFineness);

        if (invFineness >= 1.0 || num3 > detachAngle)
        {
            // Detached shock - 宽锥
            shockAngle = UtilMathLerp(
                System.Math.PI * 9.0 / 25.0,   // ~20.5°
                System.Math.PI * 49.0 / 100.0, // ~88.2°
                sqrtMach * 0.05
            );
        }
        else
        {
            // Oblique shock - 窄锥
            double machAngle = System.Math.Asin(1.0 / (sqrtMach * sqrtMach));
            shockAngle = System.Math.Max(
                num3 * 1.05,
                machAngle * 0.8 + num3 * 0.25
            );
        }
        
    }

    public double GetShockRadius(double axialDistance)
    {
        double num = 0 - axialDistance;  // shockNoseDot 简化为 0
        return projectedRadius + num * System.Math.Tan(shockAngle);
    }

    public bool IsPointOccluded(Vector3 worldPoint)
    {
        Vector3 toPoint = worldPoint - nosePosition;
        double axialDistance = Vector3.Dot(toPoint, direction);
        if (axialDistance < 0) return false;

        Vector3 radialVec = toPoint - (float)axialDistance * direction;
        double radialDist = radialVec.magnitude;

        return radialDist <= GetShockRadius(axialDistance);
    }

    // 核心：生成锥体网格
    private void GenerateConeMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        else
        {
            mesh.Clear();
        }
        meshFilter.mesh = mesh;
    
        // 配置参数
        int ringVertexCount = radialSegments;  // 每层圆环顶点数（不 +1，因为我们用 % 处理闭合）
        int totalRings = heightSegments;       // 圆环层数
        int vertexCount = 1 + totalRings * ringVertexCount;  // 鼻尖 + 所有圆环顶点
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[radialSegments * totalRings * 3 * 2];  // 每个四边形 2 三角，鼻尖层特殊
    
        int vertIndex = 0;
    
        // 1. 鼻尖（顶点 0）
        vertices[vertIndex++] = Vector3.zero;
    
        // 2. 生成圆环层，从小距离开始
        float startDist = coneLength * 0.001f;  // 很小的偏移，避免顶端平坦
        float ringSpacing = (coneLength - startDist) / (totalRings - 1f);
    
        for (int h = 0; h < totalRings; h++)
        {
            float axialDist = startDist + h * ringSpacing;
            double radiusAtDist = GetShockRadius(axialDist);
    
            for (int r = 0; r < radialSegments; r++)
            {
                float angle = r * Mathf.PI * 2f / radialSegments;
                float x = Mathf.Cos(angle) * (float)radiusAtDist;
                float y = Mathf.Sin(angle) * (float)radiusAtDist;
                float z = -axialDist;  // 向负方向延伸
    
                vertices[vertIndex++] = new Vector3(x, y, z);
            }
        }
    
        // 3. 生成三角形索引
        int triIndex = 0;
    
        // A. 鼻尖连接到第一层圆环（三角扇）
        int firstRingStart = 1;
        for (int r = 0; r < radialSegments; r++)
        {
            int current = firstRingStart + r;
            int next = firstRingStart + (r + 1) % radialSegments;
    
            triangles[triIndex++] = 0;         // 鼻尖
            triangles[triIndex++] = current;
            triangles[triIndex++] = next;
        }
    
        // B. 层与层之间的带状表面（每个相邻层形成四边形 → 两个三角）
        int currentRingStart = firstRingStart;
        for (int h = 0; h < totalRings - 1; h++)
        {
            int nextRingStart = currentRingStart + radialSegments;
    
            for (int r = 0; r < radialSegments; r++)
            {
                int a = currentRingStart + r;
                int b = currentRingStart + (r + 1) % radialSegments;
                int c = nextRingStart + (r + 1) % radialSegments;
                int d = nextRingStart + r;
    
                // 三角1: a-b-d
                triangles[triIndex++] = a;
                triangles[triIndex++] = b;
                triangles[triIndex++] = d;
    
                // 三角2: b-c-d
                triangles[triIndex++] = b;
                triangles[triIndex++] = c;
                triangles[triIndex++] = d;
            }
    
            currentRingStart = nextRingStart;
        }
    
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static double UtilMathLerp(double a, double b, double t)
    {
        t = System.Math.Clamp(t, 0.0, 1.0);
        return a + (b - a) * t;
    }

    private void Update()
    {
        Setup();
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        if (show)
        {
            GenerateConeMesh();
        }

        if (!show)
        {
            mesh.Clear();
        }
        
    }
    

    
}