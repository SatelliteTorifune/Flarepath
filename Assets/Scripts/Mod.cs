using Assets.Packages.DevConsole;
using Assets.Scripts.Flight.Sim;
using HarmonyLib;
using ModApi.Craft;
using ModApi.Craft.Parts;
using ModApi.Flight.GameView;
using ModApi.Scenes.Events;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ModApi;
    using ModApi.Common;
    using ModApi.Mods;
    using UnityEngine;

    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : ModApi.Mods.GameMod
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod() : base()
        {
        }
        
        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();
        GameObject ReEntryPrefab;

        public int boundsExtensionz = 200;
        public int boundsExtensionx = 150;
        public int boundsExtensiony = 150;
        public override void OnModLoaded()
        {
            base.OnModLoaded();
            var harmony = new Harmony("com.SatelliteTorifune.BetterReentry");
            harmony.PatchAll();
            Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
            RegisterCommand();
            
        }
        

        private void OnSceneLoaded(object o, SceneEventArgs e)
        {
            if (!Game.InFlightScene)
            {
                return;
            }

            foreach (var pd in ModApi.Common.Game.Instance.FlightScene.CraftNode.CraftScript.Data.Assembly.Parts) 
            {
                PartSetUp(pd.PartScript);
            }
            
        }

        private void PartSetUp(IPartScript partScript)
        {
            var test = partScript.GameObject.GetComponentInChildren<MeshFilter>().mesh;
            //var test = partScript.PrimaryCollider.attachedRigidbody.gameObject.GetComponentInChildren<MeshFilter>().mesh;
            if (test==null)
            {
                Debug.Log("wocao!!!!!");
                return;
            }
            var EffectObject = UnityEngine.Object.Instantiate(Mod.ResourceLoader.LoadAsset<GameObject>("Assets/Resources/Effect.prefab") as GameObject); ;
            EffectObject.GetComponent<ReEntryEffectManager>().Effect = EffectObject.GetComponent<ReEntryEffect>();
            EffectObject.GetComponent<ReEntryEffectManager>().part = partScript;
            EffectObject.GetComponent<MeshFilter>().mesh = test;
        }

        private void RegisterCommand()
        {
            DevConsoleApi.RegisterCommand<int>("fuckUz",x=>this.boundsExtensionz=x);
            DevConsoleApi.RegisterCommand<int>("fuckUx",x=>this.boundsExtensionx=x);
            DevConsoleApi.RegisterCommand<int>("fuckUy",x=>this.boundsExtensiony=x);
        }

    }
}