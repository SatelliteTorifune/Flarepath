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
        
        
        void IFlightUpdate.FlightUpdate(in FlightFrameData frame)
        {
            if (frame.IsWarping||frame.DeltaTimeWorld == 0.0)
            {
                return;
            }

            if (part==null)
            {
               this.gameObject.SetActive(false); 
               return;
            }
            this.gameObject.transform.position=part.GameObject.transform.position;
            this.gameObject.transform.rotation=part.GameObject.transform.rotation;
            this.gameObject.transform.localScale=part.GameObject.transform.localScale;
            if (part.BodyScript.ReEntryEffectStrength<=0.001||part.CraftScript.FlightData.Grounded||part.CraftScript.FlightData.MachNumber<=0.1)
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
            Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
            Effect.entryStrength = Math.Max(Math.Min(3000,part.BodyScript.ReEntryEffectStrength*3000f),3);;
            
        }

        private void ParticleEffectUpdate()
        {
            
        }
        
        
        
}