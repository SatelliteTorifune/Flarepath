using System;
using Assets.Scripts.FlarePath;
using UnityEngine;

public class testOcc : MonoBehaviour
{
    public Vector3 velocityDir = Vector3.forward; // 默认向前
    private OcclusionSampler sampler;
    public int sampleNumber=12;

    public bool DebugModeEnabled=true;
    
    void Start()
    {
        //sampler = new OcclusionSampler(gameObject.GetComponent<Renderer>().bounds, sampleNumber, gameObject.transform);
        sampler=new OcclusionSampler(this.gameObject.GetComponent<MeshFilter>(),sampleNumber,this.gameObject.transform);
        sampler.AddIgnore(this.gameObject);
        sampler.DebugModeEnabled = DebugModeEnabled;
    }

    private void Update()
    {
        // 重置采样器状态以便重新采样
        if (sampler.Ready)
        {
            sampler.Ready = false;
        }
      
        //Vector3 localVelocityDir = this.gameObject.transform.InverseTransformDirection(velocityDir.normalized);
        //sampler.SetDirection(localVelocityDir);
        sampler.SetDirection(velocityDir.normalized);
        sampler.Update();
       
        if (DebugModeEnabled)
        {
            sampler.DrawDebugRays();
        }
        
       
        float occlusion = sampler.Occlusion;
        Debug.Log("Occlusion: " + occlusion);
    }
    
    // 清理资源
    private void OnDestroy()
    {
        if (sampler != null)
        {
            sampler.ClearDebugLines();
        }
    }
}