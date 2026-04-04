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

        private void BodySetUp(IBodyScript bodyScript)
        {
            if (bodyScript?.GameObject == null)
            {
                return;
            }

            if (!_initializedBodyIds.Add(bodyScript.GameObject.GetInstanceID()))
            {
                return;
            }

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