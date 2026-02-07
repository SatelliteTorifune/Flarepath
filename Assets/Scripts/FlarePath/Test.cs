using System;
using UnityEngine;

public class Test : MonoBehaviour
{
    public OcclusionConeTestLegacy clone;

    private void Update()
    {
        bool sb =clone.IsPointOccluded(this.transform.position);
        if (sb)
        {
            Debug.Log("挡住了");
        }
        else
        {
            Debug.Log("没挡住");
        }
    }
}