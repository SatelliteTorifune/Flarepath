using Assets.Scripts.Craft;
using ModApi.Craft;
using ModApi.Craft.Parts;
using UnityEngine;
using UnityEngine.UIElements;

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

            if (!_initializedPartIds.Add(partScript.GameObject.GetInstanceID()))
            {
                return;
            }

            var partMesh = partScript.GameObject.GetComponentInChildren<MeshFilter>().mesh;
            if (partMesh == null)
            {return;}
            
            var EffectObject = Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/Effect.prefab"));
            if (EffectObject == null) return;
            EffectObject.GetComponent<ReEntryEffectPartManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectPartManager>().part = partScript;
            EffectObject.GetComponent<MeshFilter>().mesh = partMesh;
            EffectObject.gameObject.transform.SetParent(partScript.GameObject.transform, false);

        }
        

    }
}