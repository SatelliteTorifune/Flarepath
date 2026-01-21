using System;
using Assets.Scripts;
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
    
        private void Awake()
        {
            minTemp = Effect.minTemp;
            ignitionTemp = Effect.ignitionTemp;
            maxTemp = Effect.maxTemp;
        }

        public void Update()
        {
            if (false)
            {
                if (part.CraftScript.FlightData.AtmosphereSample.AirDensity==0)
                {
                    return;
                }
             
            }
            ParticleEffectUpdate();
            ReEntryEffectUpdate();
            
            
           
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