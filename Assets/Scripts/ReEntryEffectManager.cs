using System;
using ModApi.Craft.Parts;
using UnityEngine;

public class ReEntryEffectManager:MonoBehaviour
{
        public ReEntryEffect Effect;
        public IPartScript part;
    
        private void Awake()
        {
        }

        public void Update()
        {
            this.gameObject.transform.position=part.GameObject.transform.position;
            this.gameObject.transform.rotation=part.GameObject.transform.rotation;
            Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
        }
}