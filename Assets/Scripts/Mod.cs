using System.Windows.Forms;
using Assets.Packages.DevConsole;
using Assets.Scripts.Craft.Parts.Modifiers.Fuselage;
using Assets.Scripts.Flight.Sim;
using FlarePath;
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
            //new Harmony("com.SatelliteTorifune.FlarePath").PatchAll();
            Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
            GameObject ui=new GameObject("UI");
            ui.AddComponent<FlarePathUserInterface>();
            GameObject.DontDestroyOnLoad(ui);
            ui.SetActive(true);
            RegisterCommand();
            
        }
        

        private void OnSceneLoaded(object o, SceneEventArgs e)
        {
            if (!Game.InFlightScene)
            {
                return;
            }

           
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
        
        private void OnCraftChanged(ICraftNode craft)
        {
            AddEffectToCraftParts(craft.CraftScript);
        }

        private void RegisterCommand()
        {
            DevConsoleApi.RegisterCommand("FPUI", () =>
            {
                FlarePathUserInterface.Instance.OnToggleInspectorPanelState();
            });
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