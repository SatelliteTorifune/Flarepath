using Assets.Scripts.Craft;
using ModApi.Craft;
using ModApi.Craft.Parts;
using UnityEngine;

namespace Assets.Scripts
{
    
    public partial class Mod
    {
        
        private void PartSetUp(IPartScript partScript)
        {
            if (partScript?.GameObject == null)
            {
                return;
            }

            if (partScript.GameObject.GetComponentInChildren<ReEntryEffectPartManager>() != null)
            {
                return;
            }

            var partMeshFilter = partScript.GameObject.GetComponentInChildren<MeshFilter>();
            if (partMeshFilter == null || partMeshFilter.mesh == null)
            {
                return;
            }
            
            var EffectObject = Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/Effect.prefab"));
            if (EffectObject == null) return;
            EffectObject.GetComponent<ReEntryEffectPartManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectPartManager>().part = partScript;
            EffectObject.GetComponent<MeshFilter>().mesh = partMeshFilter.mesh;
            EffectObject.gameObject.transform.SetParent(partScript.GameObject.transform, false);

        }
        

    }
}