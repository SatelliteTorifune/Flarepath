using Assets.Packages.DevConsole;
using Assets.Scripts.Craft.Parts.Modifiers.Fuselage;
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
    public partial class Mod : ModApi.Mods.GameMod
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

            AddEffectToCraft(ModApi.Common.Game.Instance.FlightScene.CraftNode.CraftScript);
            
        }

        public void AddEffectToCraft(ICraftScript craft)
        {
            foreach (var pd in craft.Data.Assembly.Parts)
            {
                PartSetUp(pd.PartScript);
            }
        }

        private void RegisterCommand()
        {
        }

    }
}