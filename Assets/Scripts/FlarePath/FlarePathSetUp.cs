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
            var partMesh = partScript.GameObject.GetComponentInChildren<MeshFilter>().mesh;
            if (partMesh == null)
            {return;}
            
            var EffectObject = Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/Effect.prefab"));
            if (EffectObject == null) return;
            EffectObject.GetComponent<ReEntryEffectPartManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectPartManager>().part = partScript;
            EffectObject.GetComponent<MeshFilter>().mesh = partMesh;

        }

        private void BodySetUp(IBodyScript bodyScript)
        {
            var bodyMesh = bodyScript.GameObject.GetComponentInChildren<MeshFilter>().mesh;
            if (bodyScript.GameObject.GetComponent<Collider>()==null)
            {
                Log("碰撞箱为null");
            }
            
            if (bodyMesh==null)
            {
                LogError("body is null");
                return;
            }
            var EffectObject = Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/EffectBody.prefab") as GameObject);
            if (EffectObject == null) return;
            EffectObject.GetComponent<ReEntryEffectBodyManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectBodyManager>().BodyScript = bodyScript;
            EffectObject.GetComponent<MeshFilter>().mesh = bodyMesh;
        }

    }
}