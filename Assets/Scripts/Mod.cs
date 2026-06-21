using Assets.Packages.DevConsole;
using Assets.Scripts.Flight.GameView;
using FlarePath;
using HarmonyLib;
using ModApi.Scenes.Events;
using UnityEngine;

namespace Assets.Scripts
{
    using System;
    using ModApi;
    using ModApi.Common;
    using ModApi.Mods;
    using UnityEngine;

    /// <summary>
    /// FlarePath mod entry point.
    /// Patches the game's CraftAirstreamRenderer to use FlarePath's
    /// custom reentry shader while reusing the game's GPU cone-field pipeline.
    /// </summary>
    public partial class Mod : GameMod
    {
        private static Harmony _harmony;

        private Mod() : base() { }

        public static Mod Instance { get; } = GetModInstance<Mod>();

        public override void OnModLoaded()
        {
            base.OnModLoaded();

            // ---- Apply Harmony patches ----
            _harmony = new Harmony("com.SatelliteTorifune.FlarePath");
            CraftAirstreamRendererPatch.Apply(_harmony);
            Log("FlarePath: Harmony patches applied.");

            // ---- Start UI ----
            GameObject ui = new GameObject("FlarePathUI");
            ui.AddComponent<FlarePathUserInterface>();
            UnityEngine.Object.DontDestroyOnLoad(ui);
            ui.SetActive(true);

            RegisterCommand();
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
                Debug.unityLogger.Log(message);
        }

        public static void Log(string format, params object[] args)
        {
            if (ModSettings.Instance.ShowDevLog)
                Debug.unityLogger.LogFormat(LogType.Log, format, args);
        }

        public static void LogError(string format, params object[] args)
        {
            if (ModSettings.Instance.ShowDevLog)
            {
                Debug.unityLogger.LogFormat(LogType.Error, format, args);
                Debug.LogFormat(Environment.StackTrace);
            }
        }
        #endregion
    }
}
