using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.FlarePath
{
    public class OcclusionSampler
    {
        private Vector3[] _samplePoints;
        private Vector3 _worldVelocityDirection; 
        private int _numOccluded = 0;
        private int _numSampled = 0;
        private int _currentSampleIndex = 0;
        private Transform _transform;
        private List<GameObject> _ignoreList = new List<GameObject>();
        
        // 射线调试相关的成员变量
        private GameObject _debugLineContainer;
        private Material _lineMaterial;
        private List<RayDebugInfo> _currentFrameRays = new List<RayDebugInfo>();
       

        // 射线调试信息结构
        
        
        //得加进config
        public float MaxDistance { get; set; } = 50f;
        public float Occlusion { get; private set; } = 0f;
        public bool Ready { get; set; }
        public bool DebugModeEnabled { get; set; } = false;
        // 采样点距离Mesh的偏移距离
        public float SampleOffsetDistance { get; set; } = 0;

        // 基于Mesh的构造函数,有bug,慎用
        public OcclusionSampler(MeshFilter meshFilter, int numSamples, Transform transform)
        {
            _transform = transform;
            _samplePoints = GenerateUniformSurfaceSamples(meshFilter, numSamples);
            MaxDistance = 100f;
        }

        /// 基于Bounds的构造函数
        /// 考虑到第一个构造函数有他妈诡异的问题,还是用这个把
        public OcclusionSampler(Bounds bounds, int numSamples, Transform transform)
        {
            _transform = transform;
            _samplePoints = GenerateBoundsSurfaceSamples(bounds, numSamples);
            MaxDistance = bounds.size.magnitude * 2f;
        }

        public void AddIgnore(GameObject go)
        {
            if (!_ignoreList.Contains(go)) _ignoreList.Add(go);
        }

        // 设置检测方向（世界坐标方向）
        public void SetDirection(Vector3 worldDirection)
        {
            _worldVelocityDirection = worldDirection.normalized;
        }

        public void Update()
        {
            if (!Ready)
            {
                // 清空上一帧的射线调试数据
                _currentFrameRays.Clear();
                if (_currentSampleIndex < _samplePoints.Length)
                {
                    SamplePoint(_samplePoints[_currentSampleIndex]);
                    _currentSampleIndex++;
                }

                if (_currentSampleIndex >= _samplePoints.Length)
                {
                    Occlusion = (float)_numOccluded / _numSampled;
                    _currentSampleIndex = 0;
                    _numSampled = 0;
                    _numOccluded = 0;
                    Ready = true;
                }
            }
        }

        // 生成均匀分布的表面采样点
        private Vector3[] GenerateUniformSurfaceSamples(MeshFilter meshFilter, int numSamples)
        {
            
            List<Vector3> samples = new List<Vector3>();
        
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                // fallback 到 Bounds 采样
                return GenerateBoundsSurfaceSamples(meshFilter.GetComponent<Renderer>().bounds, numSamples);
            }
        
            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;
        
            // 1. 计算每个三角面的面积，并累计总面积
            float totalArea = 0f;
            List<float> triangleAreas = new List<float>(triangles.Length / 3);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
        
                // 三角形面积 = 1/2 * || (v1-v0) × (v2-v0) ||
                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
                float area = cross.magnitude * 0.5f;
                triangleAreas.Add(area);
                totalArea += area;
            }
        
            // 2. 按面积比例分配采样点
            int remainingSamples = numSamples;
            List<int> samplesPerTriangle = new List<int>(triangleAreas.Count);
        
            for (int i = 0; i < triangleAreas.Count; i++)
            {
                float ratio = triangleAreas[i] / totalArea;
                int count = Mathf.RoundToInt(ratio * numSamples);
                count = Mathf.Clamp(count, 0, remainingSamples);
                samplesPerTriangle.Add(count);
                remainingSamples -= count;
            }
        
            // 补齐剩余采样点（加到最大面积三角形）
            if (remainingSamples > 0)
            {
                int maxIndex = 0;
                float maxArea = 0f;
                for (int i = 0; i < triangleAreas.Count; i++)
                {
                    if (triangleAreas[i] > maxArea)
                    {
                        maxArea = triangleAreas[i];
                        maxIndex = i;
                    }
                }
                samplesPerTriangle[maxIndex] += remainingSamples;
            }
        
            // 3. 为每个三角面生成采样点
            for (int tri = 0; tri < triangles.Length; tri += 3)
            {
                int sampleCount = samplesPerTriangle[tri / 3];
                if (sampleCount == 0) continue;
        
                Vector3 v0 = vertices[triangles[tri]];
                Vector3 v1 = vertices[triangles[tri + 1]];
                Vector3 v2 = vertices[triangles[tri + 2]];
                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
                Vector3 normal;

                if (cross.sqrMagnitude > 0.000001f)  // 很小的阈值
                {
                    normal = cross.normalized;
                }
                else
                {
                    normal = Vector3.forward;          
                    Mod.Log("OcclusionSampler.GenerateUniform SufaceSamples:Degenerate triangle detected, using fallback normal");
                }
        
                
                if (normals.Length > triangles[tri]) normal = normals[triangles[tri]]; // 用顶点法线更好
        
                for (int s = 0; s < sampleCount; s++)
                {
                    // 推荐使用这个版本
                    float r = Random.value;
                    float sqrt_r = Mathf.Sqrt(r);
                    float t = Random.value;

                    float u = 1f - sqrt_r;
                    float v = t * sqrt_r;
                    float w = 1f - u - v;

                    // 或者用写法1
                    // float u = Random.value;
                    // float v = Random.value * (1f - u);
                    // float w = 1f - u - v;

                    Vector3 baryPoint = u * v0 + v * v1 + w * v2;
                    Vector3 worldPosOnSurface  = _transform.TransformPoint(baryPoint);
                    Vector3 worldNormal        = _transform.TransformDirection(normal).normalized;
                    const float WORLD_OFFSET = 0.03f;
                    Vector3 sampleWorld = worldPosOnSurface + worldNormal * WORLD_OFFSET;
                    // 偏移（注意：如果模型有很尖锐的边或法线不连续，这里还是可能轻微出界）
                    Vector3 sampleLocal = _transform.InverseTransformPoint(sampleWorld);;// + normal * SampleOffsetDistance;

                    samples.Add(sampleLocal);
                }
            }
        
            // 如果采样点不足，用 Bounds 补充（fallback）
            if (samples.Count < numSamples)
            {
                Vector3[] additional = GenerateBoundsSurfaceSamples(meshFilter.GetComponent<Renderer>().bounds, numSamples - samples.Count);
                samples.AddRange(additional);
            }
        
            return samples.ToArray();
        }

       
        // 生成Bounds表面的均匀采样点
        private Vector3[] GenerateBoundsSurfaceSamples(Bounds bounds, int numSamples)
        {
            List<Vector3> samples = new List<Vector3>();
        
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
        
            // 1. 计算每个面的面积（忽略方向，只算正负一对）
            float areaXY = extents.x * extents.y * 2f;  // ±Z 面（前后）
            float areaXZ = extents.x * extents.z * 2f;  // ±Y 面（上下）
            float areaYZ = extents.y * extents.z * 2f;  // ±X 面（左右）
        
            float totalArea = areaXY + areaXZ + areaYZ;
        
            // 2. 按面积分配采样点数量
            int samplesXY = Mathf.Max(1, Mathf.RoundToInt(numSamples * (areaXY / totalArea)));
            int samplesXZ = Mathf.Max(1, Mathf.RoundToInt(numSamples * (areaXZ / totalArea)));
            int samplesYZ = Mathf.Max(1, Mathf.RoundToInt(numSamples * (areaYZ / totalArea)));
        
            // 调整总数，确保正好 numSamples
            int currentTotal = samplesXY + samplesXZ + samplesYZ;
            if (currentTotal != numSamples)
            {
                // 补齐或减去（优先加到最大面）
                int diff = numSamples - currentTotal;
                if (diff > 0)
                {
                    if (areaXY >= areaXZ && areaXY >= areaYZ) samplesXY += diff;
                    else if (areaXZ >= areaXY && areaXZ >= areaYZ) samplesXZ += diff;
                    else samplesYZ += diff;
                }
                else
                {
                    // 减去（从最小面减）
                    while (diff < 0)
                    {
                        if (samplesXY > 1) { samplesXY--; diff++; }
                        else if (samplesXZ > 1) { samplesXZ--; diff++; }
                        else if (samplesYZ > 1) { samplesYZ--; diff++; }
                    }
                }
            }
        
            // 3. 为每个面生成采样点 + 随机偏移
            GenerateFaceSamples(samples, center, extents, new Vector3(0, 0, extents.z),  new Vector3(0, 0, -extents.z),  samplesXY, true);   // ±Z 面
            GenerateFaceSamples(samples, center, extents, new Vector3(0, extents.y, 0), new Vector3(0, -extents.y, 0), samplesXZ, false);  // ±Y 面
            GenerateFaceSamples(samples, center, extents, new Vector3(extents.x, 0, 0), new Vector3(-extents.x, 0, 0), samplesYZ, false); // ±X 面
        
            // 4. 转为本地坐标
            Vector3[] localSamples = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                localSamples[i] = _transform.InverseTransformPoint(samples[i]);
            }
        
            return localSamples;
        }
        
        // 辅助函数：为一个面（正负一对）生成采样点
        private void GenerateFaceSamples(List<Vector3> samples, Vector3 center, Vector3 extents, Vector3 posNormal, Vector3 negNormal, int numSamples, bool isZFace)
        {
            if (numSamples <= 0) return;
        
            int sqrtNum = Mathf.CeilToInt(Mathf.Sqrt(numSamples));
            int count = 0;
        
            for (int i = 0; i < sqrtNum && count < numSamples; i++)
            {
                for (int j = 0; j < sqrtNum && count < numSamples; j++)
                {
                    float u = (float)i / (sqrtNum - 1);
                    float v = (float)j / (sqrtNum - 1);
        
                    // 随机偏移（±10%）
                    u += Random.Range(-0.1f, 0.1f) / sqrtNum;
                    v += Random.Range(-0.1f, 0.1f) / sqrtNum;
                    u = Mathf.Clamp01(u);
                    v = Mathf.Clamp01(v);
        
                    Vector3 point;
                    if (isZFace)
                    {
                        // ±Z 面
                        point = new Vector3(
                            Mathf.Lerp(-extents.x, extents.x, u),
                            Mathf.Lerp(-extents.y, extents.y, v),
                            0
                        );
                    }
                    else
                    {
                        // ±X 或 ±Y 面
                        point = new Vector3(
                            0,
                            Mathf.Lerp(-extents.y, extents.y, u),
                            Mathf.Lerp(-extents.z, extents.z, v)
                        );
                    }
        
                    // 正负面各采样一半（随机分配）
                    bool positiveSide = Random.value > 0.5f;
                    Vector3 offset = positiveSide ? posNormal : negNormal;
                    point += offset;
        
                    // 世界坐标
                    Vector3 worldPoint = center + point;
                    samples.Add(worldPoint);
                    count++;
                }
            }
        }


        // 采样单个点
        private void SamplePoint(Vector3 localPos)
        {
            Vector3 worldPos = _transform.TransformPoint(localPos);
            Vector3 worldDir=_worldVelocityDirection;
            // 添加零向量检查
            if (worldDir == Vector3.zero)
            {
               Mod.Log("OcclusionSampler: Zero direction vector detected!");
                worldDir = Vector3.forward; // 使用默认方向
            }

            // 如果起始点和结束点相同，也可能是问题
            Vector3 rayEnd = worldPos + worldDir * MaxDistance;
            if (Vector3.Distance(worldPos, rayEnd) < 0.001f)
            {
                Mod.Log("OcclusionSampler: Invalid ray length!");
                return;
            }
            
            Ray ray = new Ray(worldPos, worldDir);
            float rayDistance = MaxDistance;
            Color debugColor = Color.green;

            // 构建忽略层掩码
            int layerMask = -1; // 所有层
            
            // 排除忽略列表中的对象
            bool occluded = false;

            // 射线检测
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, MaxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                // 检查击中的物体是否在忽略列表中
                if (!_ignoreList.Contains(hit.collider.gameObject))
                {
                    occluded = true;
                    rayDistance = hit.distance;
                    rayEnd = hit.point;
                    debugColor = Color.red;
                }
            }

            // 添加调试射线信息
            if (DebugModeEnabled)
            {
                _currentFrameRays.Add(new RayDebugInfo(worldPos, rayEnd, debugColor));
            }

            if (occluded) _numOccluded++;
            _numSampled++;

            if (DebugModeEnabled)
            {
                CreateDebugSphere(localPos, 0.1f, occluded);
            }
        }

        private void CreateDebugSphere(Vector3 localPos, float diameter, bool occluded)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Collider>().enabled = false;
            sphere.transform.SetParent(_transform, false);
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.transform.localPosition = localPos;

            MeshRenderer mr = sphere.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = occluded ? Color.red : Color.green;
            mr.material = mat;

            // 自动销毁调试球体
            Object.Destroy(sphere, 0.1f);
        }
        
        // 清除调试线条
        public void ClearDebugLines()
        {
            if (_debugLineContainer != null)
            {
                Object.Destroy(_debugLineContainer);
                _debugLineContainer = null;
            }
        }

        // 绘制调试线条
        private bool _canDrawDebug = true;
        public void DrawDebugRays()
        {
            if (!DebugModeEnabled || !_canDrawDebug) return;

            try
            {
                // 清除旧的调试线条容器
                ClearDebugLines();
        
                _debugLineContainer = new GameObject("OcclusionDebugLines");
                _debugLineContainer.transform.SetParent(_transform);

                foreach (var rayInfo in _currentFrameRays)
                {
                    DrawDebugLine(rayInfo.start, rayInfo.end, rayInfo.color);
                }
            }
            catch (System.Exception ex)
            {
                _canDrawDebug = false;
                Mod.Log("OcclusionSampler DrawDebugRays error: " + ex.Message);
            }
        }

        // 绘制单条调试线的方法
        private void DrawDebugLine(Vector3 start, Vector3 end, Color color)
        {
            // 添加安全检查
            if (!_canDrawDebug) return;
    
            // 检查起点和终点是否有效且不相同
            if (Vector3.Distance(start, end) < 0.001f)
            {
                return; // 跳过绘制零长度线段
            }
    
            // 检查坐标是否有效（避免 NaN 或 Infinity）
            if (float.IsNaN(start.x) || float.IsNaN(start.y) || float.IsNaN(start.z) ||
                float.IsNaN(end.x) || float.IsNaN(end.y) || float.IsNaN(end.z) ||
                float.IsInfinity(start.x) || float.IsInfinity(start.y) || float.IsInfinity(start.z) ||
                float.IsInfinity(end.x) || float.IsInfinity(end.y) || float.IsInfinity(end.z))
            {
                return;
            }

            try
            {
                GameObject lineObj = new GameObject("DebugRay");
                lineObj.transform.SetParent(_debugLineContainer.transform);

                LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        
                // 初始化材质（如果还没有的话）
                if (_lineMaterial == null)
                {
                    Shader shader = Shader.Find("Unlit/Color");
                    if (shader != null)
                    {
                        _lineMaterial = new Material(shader);
                    }
                    else
                    {
                        _lineMaterial = new Material(Shader.Find("Standard"));
                    }
                }
        
                lineRenderer.material = _lineMaterial;
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                lineRenderer.startWidth = 0.02f;
                lineRenderer.endWidth = 0.02f;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
        
                // 自动销毁调试线条
                Object.Destroy(lineObj, 0.1f);
            }
            catch (System.Exception ex)
            {
                // 捕获任何可能的异常，避免影响主逻辑
                _canDrawDebug = false; // 如果出错就禁用调试绘制
                Mod.Log("OcclusionSampler DrawDebugLine error: " + ex.Message);
            }
        }
    }
    public struct RayDebugInfo
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;
            
        public RayDebugInfo(Vector3 start, Vector3 end, Color color)
        {
            this.start = start;
            this.end = end;
            this.color = color;
        }
    }
}
