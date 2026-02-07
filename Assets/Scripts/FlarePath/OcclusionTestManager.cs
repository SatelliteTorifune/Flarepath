using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class OcclusionManager : MonoBehaviour
{
    [Tooltip("所有可能生成激波锥的前端零件（热盾、鼻锥等）")]
    public List<OcclusionConeTestLegacy> leadingCones = new List<OcclusionConeTestLegacy>();

    [Tooltip("要检查是否被遮挡的目标零件（可以动态添加）")]
    public List<Transform> targetParts = new List<Transform>();  // 或用 GameObject

    [FormerlySerializedAs("occludedColor")] [Header("Debug")]
    public Color 挡住了 = Color.green;
    [FormerlySerializedAs("exposedColor")] public Color 暴露 = Color.red;

    void Update()
    {
        foreach (var target in targetParts)
        {
            bool isOccluded = IsTargetOccluded(target.position);  // 或用 target 的中心/多个点

            // 可视反馈：改颜色
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isOccluded ? 挡住了 : 暴露;
            }
            
        }
    }

    /// <summary>
    /// 判断 targetPos 是否被任何一个 leading cone 遮挡
    /// </summary>
    public bool IsTargetOccluded(Vector3 targetPos)
    {
        foreach (var cone in leadingCones)
        {
            if (cone.IsPointOccluded(targetPos))
            {
                return true;  // 只要被任何一个 cone 包住，就算被遮挡
            }
        }
        return false;  // 没被任何一个遮挡
    }

    // 可选：添加/移除 leading cone 的方法
    public void AddLeadingCone(OcclusionConeTestLegacy cone)
    {
        if (!leadingCones.Contains(cone)) leadingCones.Add(cone);
    }
}