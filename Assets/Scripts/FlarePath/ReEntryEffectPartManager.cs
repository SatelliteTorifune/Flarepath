using System;
using Assets.Scripts;
using ModApi.Craft.Parts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

public class ReEntryEffectPartManager:MonoBehaviourBase,IFlightUpdate
{
        public ReEntryEffect Effect;
        public ParticleSystem ParticleSystem;
        public IPartScript part;
        private float minTemp;
        private float ignitionTemp;
        private float maxTemp;
        
    
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
            
        }
        
        public void FlightUpdate(in FlightFrameData frame)
        {
            if (part.BodyScript.ReEntryEffectStrength<=0.001)
            {
                if (Effect.effectRenderer.enabled)
                {
                    Effect.effectRenderer.enabled = false;
                }
            }
            if (part.BodyScript.ReEntryEffectStrength > 0.001)
            {
                if (!Effect.effectRenderer.enabled)
                {
                    Effect.effectRenderer.enabled = true;
                }
                ParticleEffectUpdate();
                ReEntryEffectUpdate();
            }
        }


        private void ReEntryEffectUpdate()
        {
            this.gameObject.transform.position=part.GameObject.transform.position;
            this.gameObject.transform.rotation=part.GameObject.transform.rotation;
            this.gameObject.transform.localScale=part.GameObject.transform.localScale;
            Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
            Effect.entryStrength = GetEntryStrength(part.BodyScript.ReEntryEffectStrength);
            
        }

        private void ParticleEffectUpdate()
        {
            
        }
    
    
        private float GetEntryStrength(float Strength)
        {
            return Math.Max(Math.Min(3000,Strength*3000f),3);
            
            float temp = part.Temperature;
            if (temp < ignitionTemp)
            {
                return 300f;
            }
            /*
            this._plasmaTemperature = Mathf.Sqrt(this.part.BodyScript.FluidDensity) * Mathf.Clamp01(Mathf.Clamp(50000f * part.BodyScript., 1f, 2000f) * 0.01f);
            
            float reentryEffectStrength = Mathf.Clamp01((float) (((double) (0.75f * temp + 0.25f * this._plasmaTemperature) - 670.0) / 1070.0));

            */
            
            
            // 归一化
            float t = Mathf.Clamp((temp - ignitionTemp) / (maxTemp - ignitionTemp), 0f, 1f);

            // 用 2.5 次方曲线（高温爆发感强）
            float curve = Mathf.Pow(t, 2.5f);
            float strength = Mathf.Lerp(300f, 3000f, curve);
            return strength;
        }
        
}