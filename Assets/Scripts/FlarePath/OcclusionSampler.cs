using System.Collections.Generic;
using UnityEngine;
namespace Assets.Scripts.FlarePath
{ 

    public class OcclusionSampler
    {
    private float _cellSize;
    private Vector2i _currentCell;
    private GameObject[] _debugObjects;
    private Vector3 _localCenter;
    private Vector3 _localDirection;
    private int _numOccluded = 0;
    private int _numSampled = 0;
    private int _numSamplesPerDimension;
    private Collider[] _overlapSphereTestResults = new Collider[1];
    private float _spacingX;
    private float _spacingY;
    private Transform _transform;
    private List<GameObject> _ignoreList = new List<GameObject>(); 

    public float MaxDistance { get; set; } = 100f;
    public float Occlusion { get; private set; } = 0f;
    public bool Ready { get; set; }
    public bool DebugModeEnabled { get; set; } = false;
    public bool SkipCorners { get; set; } = true;
    
    

    public OcclusionSampler(Vector2 scale, int numSamplesPerDimension, Transform transform, Vector3 localCenter, Vector3 localDirection)
    {
        _numSamplesPerDimension = numSamplesPerDimension;
        _cellSize = Mathf.Min(scale.x, scale.y) / numSamplesPerDimension;
        _spacingX = scale.x / numSamplesPerDimension;
        _spacingY = scale.y / numSamplesPerDimension;
        _transform = transform;
        _localDirection = localDirection.normalized;
        _localCenter = localCenter;

        MaxDistance = Mathf.Max(scale.x, scale.y) * 2f;
    }

    public void AddIgnore(GameObject go)
    {
        if (!_ignoreList.Contains(go)) _ignoreList.Add(go);
    }

    public void Update()
    {
        
        if (!Ready)
        {
            if (!SkipCorners || !IsCornerCell())
            {
                SampleCell(_currentCell);
            }

            _currentCell.x++;
            if (_currentCell.x >= _numSamplesPerDimension)
            {
                _currentCell.x = 0;
                _currentCell.y++;
                if (_currentCell.y >= _numSamplesPerDimension)
                {
                    Occlusion = (float)_numOccluded / _numSampled;
                    _currentCell = new Vector2i(0, 0);
                    _numSampled = 0;
                    _numOccluded = 0;
                    Ready = true;
                }
            }
        }
    }

    private bool IsCornerCell()
    {
        int num = _numSamplesPerDimension - 1;
        return (_currentCell.x == 0 && _currentCell.y == 0) ||
               (_currentCell.x == num && _currentCell.y == 0) ||
               (_currentCell.x == 0 && _currentCell.y == num) ||
               (_currentCell.x == num && _currentCell.y == num);
    }

    private Vector3 CalculateCellPosition(Vector2i cell)
    {
        return _localCenter + new Vector3(
            (-(_numSamplesPerDimension - 1) / 2f + cell.x) * _spacingX,
            0f,
            (-(_numSamplesPerDimension - 1) / 2f + cell.y) * _spacingY);
    }

    private void SampleCell(Vector2i cell)
    {
        Vector3 localPos = CalculateCellPosition(cell);
        Vector3 worldPos = _transform.TransformPoint(localPos);
        Vector3 worldDir = _transform.TransformDirection(_localDirection);

        Ray ray = new Ray(worldPos, worldDir);

        // 忽略自身和其他指定物体
        int layerMask = -1; // 所有层
        bool occluded = false;

        // 小球检测（快速判断附近是否有物体）
        if (Physics.OverlapSphereNonAlloc(worldPos, _cellSize * 0.25f, _overlapSphereTestResults, layerMask, QueryTriggerInteraction.Ignore) > 0)
        {
            occluded = true;
        }
        else
        {
            // 射线检测
            if (Physics.Raycast(ray, MaxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                occluded = true;
            }
        }

        if (occluded) _numOccluded++;
        _numSampled++;

        if (DebugModeEnabled)
        {
            CreateDebugSphere(localPos, _cellSize * 0.5f, occluded);
        }
    }

    private void CreateDebugSphere(Vector3 localPos, float diameter, bool occluded)
    {
        if (_debugObjects == null)
            _debugObjects = new GameObject[_numSamplesPerDimension * _numSamplesPerDimension];

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.GetComponent<Collider>().enabled = false;
        sphere.transform.SetParent(_transform, false);
        sphere.transform.localScale = Vector3.one * diameter;
        sphere.transform.localPosition = localPos;

        MeshRenderer mr = sphere.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = occluded ? Color.red : Color.green;
        mr.material = mat;

        if (_debugObjects[_numSampled] != null)
        {
            GameObject.Destroy(_debugObjects[_numSampled]);
        }
            

        _debugObjects[_numSampled] = sphere;
    }
    }
}