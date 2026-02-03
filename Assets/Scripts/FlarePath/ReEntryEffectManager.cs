using System;
using Assets.Scripts;
using Assets.Scripts.FlarePath;
using ModApi.Craft.Parts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

public class ReEntryEffectManager:MonoBehaviourBase,IFlightFixedUpdate
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
            //return;
            //set up occlusion sampler
            if (part == null)
            {
                Mod.Log("wocao part是null");
                return;
            }
            try
            {
                _occlusionSampler = new OcclusionSampler(part.GameObject.GetComponentInChildren<Renderer>().bounds, 9,part.GameObject.transform);
            }
            catch (Exception e)
            {
                try
                {
                    Mod.Log("这他妈又是啥我日你妈");
                    Mod.Log(Environment.StackTrace);
                }
                catch (Exception exception)
                {
                  
                }
                
            }
            
            if (_occlusionSampler==null)
            {
                Mod.Log("我操怎么是null");
                return;
            }
            _occlusionSampler.AddIgnore(this.gameObject);
            _occlusionSampler.AddIgnore(part.GameObject);
            _occlusionSampler.DebugModeEnabled = true;
        }

        void IFlightFixedUpdate.FlightFixedUpdate(in FlightFrameData frame)
        {
            Mod.Log("FlightFixedUpdate");
            OcclusionSamplerUpdate();
            ParticleEffectUpdate();
            ReEntryEffectUpdate();
            //
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

        public void Update()
        {
            return;
            //Debug.LogFormat($"{part.Data.Name} is occluded by{Effect.occlusionSampler.Occlusion}");
            
            Mod.Log("Update0");
            if (false)
            {
                ParticleEffectUpdate();
                ReEntryEffectUpdate();
                return;
            }
            ReEntryEffectUpdate();
            Mod.Log("Update1");
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
            Mod.Log("Update2");
            
        }
        private void OcclusionSamplerUpdate()
        {
            
            if (_occlusionSampler.Ready)
            {
                _occlusionSampler.Ready = false;
            }
            _occlusionSampler.SetDirection(part.CraftScript.FlightData.SurfaceVelocity.normalized.ToVector3());
            _occlusionSampler.Update();
            if (_occlusionSampler != null && _occlusionSampler.DebugModeEnabled)
            {
                _occlusionSampler.DrawDebugRays();
            }
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


        public bool StartMethodCalled { get; set; }
}