using System;
using Assets.Scripts;
using Assets.Scripts.FlarePath;
using ModApi.Craft.Parts;
using UnityEngine;

public class ReEntryEffectManager:MonoBehaviour
{
        public ReEntryEffect Effect;
        public ParticleSystem ParticleSystem;
        public IPartScript part;
        private float minTemp;
        private float ignitionTemp;
        private float maxTemp;
        
        private OcclusionSampler _occlusionSampler;
    
        private void Awake()
        {
            minTemp = Effect.minTemp;
            ignitionTemp = Effect.ignitionTemp;
            maxTemp = Effect.maxTemp;
            
            

        }

        private void Start()
        {
            return;
            //set up occlusion sampler
            if (part == null)
            {
                Mod.Log("wocao part是null");
            }
            try
            {
                _occlusionSampler = new OcclusionSampler(part.GameObject.GetComponentInChildren<Renderer>().bounds, 9,part.GameObject.transform);
            }
            catch (Exception e)
            {
                Mod.Log(Environment.StackTrace);
            }
            
            if (_occlusionSampler==null)
            {
                Mod.Log("我操怎么是null");
                return;
            }
            _occlusionSampler.AddIgnore(this.gameObject);
            _occlusionSampler.AddIgnore(part.GameObject);
            _occlusionSampler.DebugModeEnabled = false;
        }

        public void Update()
        {
            //Debug.LogFormat($"{part.Data.Name} is occluded by{Effect.occlusionSampler.Occlusion}");
            
            ParticleEffectUpdate();
            ReEntryEffectUpdate();
            return;
            
            OcclusionSamplerUpdate();
            if (_occlusionSampler.Occlusion<0.5f)
            {
                Effect.effectRenderer.enabled = false;
               
            }
            else
            {
                Effect.effectRenderer.enabled = true;
                ParticleEffectUpdate();
                ReEntryEffectUpdate();
            }
            
            
        }
        private void OcclusionSamplerUpdate()
        {
            if (_occlusionSampler.Ready)
            {
                _occlusionSampler.Ready = false;
            }
      
            Vector3 localVelocityDir = this.gameObject.transform.InverseTransformDirection(-part.CraftScript.FlightData.SurfaceVelocity.normalized.ToVector3());
            _occlusionSampler.SetDirection(localVelocityDir);
            _occlusionSampler.Update();
        }

        private void ReEntryEffectUpdate()
        {
            this.gameObject.transform.position=part.GameObject.transform.position;
            this.gameObject.transform.rotation=part.GameObject.transform.rotation;
            this.gameObject.transform.localScale=part.GameObject.transform.localScale;
            Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
            Effect.entryStrength = GetEntryStrength();
            
        }

        private void ParticleEffectUpdate()
        {
            
        }
    
    
        private float GetEntryStrength()
        {
            return 3000f;
            float temp = part.Temperature;
            if (temp < ignitionTemp)
            {
                return 300f;
            }

            // 归一化
            float t = Mathf.Clamp((temp - ignitionTemp) / (maxTemp - ignitionTemp), 0f, 1f);

            // 用 2.5 次方曲线（高温爆发感强）
            float curve = Mathf.Pow(t, 2.5f);
            float strength = Mathf.Lerp(300f, 3000f, curve);
            return strength;
        }

       
       
        
}