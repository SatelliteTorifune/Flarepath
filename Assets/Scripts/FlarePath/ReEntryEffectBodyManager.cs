using System;
using Assets.Scripts;
using ModApi.Craft;
using ModApi.Craft.Parts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

public class ReEntryEffectBodyManager:MonoBehaviourBase,IFlightFixedUpdate
{
        public ReEntryEffect Effect;
        public ParticleSystem ParticleSystem;
        public IBodyScript BodyScript;
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
            if (BodyScript == null)
            {
                Mod.Log("wocao BodyScript是null");
                return;
            }
            
        }

        void IFlightFixedUpdate.FlightFixedUpdate(in FlightFrameData frame)
        {
            ParticleEffectUpdate();
            ReEntryEffectUpdate();
            
        }

    
    

        private void ReEntryEffectUpdate()
        {
            /*
            this.gameObject.transform.position=BodyScript.GameObject.transform.position;
            this.gameObject.transform.rotation=BodyScript.GameObject.transform.rotation;
            this.gameObject.transform.localScale=BodyScript.GameObject.transform.localScale;*/
            Effect.velocityWorld = BodyScript.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
            Effect.entryStrength = GetEntryStrength();
            
        }

        private void ParticleEffectUpdate()
        {
            
        }
    
    
        private float GetEntryStrength()
        {
            
            float getBodyTemp()
            {
                float tt=0;
                foreach (var pd in BodyScript.Data.Parts)
                {
                    tt+= pd.PartScript.Temperature;
                }
                return tt/BodyScript.Data.Parts.Count;
            }
            //return 3000f;
            float temp = getBodyTemp();
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