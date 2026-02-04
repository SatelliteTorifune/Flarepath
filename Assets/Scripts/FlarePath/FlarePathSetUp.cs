using ModApi.Craft.Parts;
using UnityEngine;

namespace Assets.Scripts
{
    
    public partial class Mod
    {
        
        private void PartSetUp(IPartScript partScript)
        {
            var partMesh = partScript.GameObject.GetComponentInChildren<MeshFilter>().mesh;
            if (partMesh == null)
            {return;}
            
            var EffectObject = Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/Effect.prefab") as GameObject);
            if (EffectObject == null) return;
            EffectObject.GetComponent<ReEntryEffectManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectManager>().part = partScript;
            EffectObject.GetComponent<MeshFilter>().mesh = partMesh;

        }

    }
}