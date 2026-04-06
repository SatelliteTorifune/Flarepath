using System;
using Assets.Scripts;
using ModApi.Craft.Parts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

public class ReEntryEffectPartManager : MonoBehaviourBase, IFlightUpdate
{
    public ReEntryEffect Effect;
    public ParticleSystem ParticleSystem;
    public IPartScript part;

    void IFlightUpdate.FlightUpdate(in FlightFrameData frame)
    {
        if (frame.IsWarping || frame.DeltaTimeWorld == 0.0)
        {
            return;
        }

        if (part == null)
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = part.GameObject.transform.position;
        transform.rotation = part.GameObject.transform.rotation;
        transform.localScale = part.GameObject.transform.localScale;
        //debug用,记得删注释
        /*
        if (part.BodyScript.ReEntryEffectStrength <= 0.001 || part.CraftScript.FlightData.Grounded || part.CraftScript.FlightData.MachNumber <= 0.1)
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
        }*/
        ParticleEffectUpdate();
        ReEntryEffectUpdate();
    }

    private void ReEntryEffectUpdate()
    {
        Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
        //Effect.entryStrength = Math.Max(Math.Min(3000, part.BodyScript.ReEntryEffectStrength * 3000f), 3);

        var config = FlarePath.FlarePathUserInterface.RuntimeConfig;
        Effect.entryStrength = config.entryStrength;
        Effect.fxState = config.fxState;
        Effect.lengthMultiplier = config.lengthMultiplier;
        Effect.trailAlphaMultiplier = config.trailAlphaMultiplier;
        Effect.opacityMultiplier = config.opacityMultiplier;
        Effect.wrapOpacityMultiplier = config.wrapOpacityMultiplier;
        Effect.wrapFresnelModifier = config.wrapFresnelModifier;
        Effect.streakProbability = config.streakProbability;
        Effect.streakThreshold = config.streakThreshold;
        Effect.minTemp = config.minTemp;
        Effect.ignitionTemp = config.ignitionTemp;
        Effect.maxTemp = config.maxTemp;
    }

    private void ParticleEffectUpdate()
    {
    }
}
