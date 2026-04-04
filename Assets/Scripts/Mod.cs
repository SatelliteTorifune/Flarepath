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
        private readonly HashSet<int> _initializedPartIds = new HashSet<int>();
        private readonly HashSet<int> _initializedBodyIds = new HashSet<int>();

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
            //new Harmony("com.SatelliteTorifune.FlarePath").PatchAll();
            Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
            RegisterCommand();
            
        }
        

        private void OnSceneLoaded(object o, SceneEventArgs e)
        {
            if (!Game.InFlightScene)
            {
                return;
            }

            _initializedPartIds.Clear();
            _initializedBodyIds.Clear();
            ModApi.Common.Game.Instance.FlightScene.CraftChanged += OnCraftChanged;
            AddEffectToCraftParts(ModApi.Common.Game.Instance.FlightScene.CraftNode.CraftScript);
            
        }

        public void AddEffectToCraftParts(ICraftScript craft)
        {
            foreach (var pd in craft.Data.Assembly.Parts)
            {
                PartSetUp(pd.PartScript);
            }
        }

        public void AddEffectToCraftBodies(ICraftScript craft)
        {
            foreach (var bodyData in craft.Data.Assembly.Bodies)
            {
                BodySetUp(bodyData.BodyScript);
            }
        }

        private void OnCraftChanged(ICraftNode craft)
        {
            AddEffectToCraftParts(craft.CraftScript);
        }

        private void RegisterCommand()
        {
        }
        #region LOG
        public static void Log(object message)
        {
            if (ModSettings.Instance.ShowDevLog)
            {
                Debug.unityLogger.Log(message);
            }
        }
        public static void Log(string format, params object[] args)
        {
            if (ModSettings.Instance.ShowDevLog)
            {
                Debug.unityLogger.LogFormat(LogType.Log, format, args);
            }
        }
        public static void Log(UnityEngine.Object context, string format, params object[] args)
        {
            if (ModSettings.Instance.ShowDevLog)
            {
                Debug.unityLogger.LogFormat(LogType.Log, context, format, args);
            }
        }

        public static void LogError(string format, params object[] args)
        {
            if (ModSettings.Instance.ShowDevLog)
            {
                Debug.unityLogger.LogFormat(LogType.Log, format, args);
                Debug.LogFormat(Environment.StackTrace);
            }
        }
        #endregion

    }
}