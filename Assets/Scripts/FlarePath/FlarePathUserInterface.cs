using System;
using Assets.Scripts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using ModApi.Ui.Inspector;
using UnityEngine;

namespace FlarePath
{
    public class FlarePathUserInterface : MonoBehaviourBase, IFlightFixedUpdate, IFlightUpdate
    {
        public static FlarePathUserInterface Instance { get; private set; }

        private IInspectorPanel inspectorPanel;
        private InspectorModel inspectorModel;

        private static FlarePathConfig runtimeConfig = FlarePathConfig.CreateDefault();
        public static FlarePathConfig RuntimeConfig => runtimeConfig;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OnToggleInspectorPanelState()
        {
            try
            {
                inspectorPanel.Visible = !inspectorPanel.Visible;
            }
            catch (Exception)
            {

                CreateInspectorPanel();
                inspectorPanel.Visible = true;
            }
        }

        public void CreateInspectorPanel()
        {
            inspectorModel = new InspectorModel(
                "FlarePathInspector",
                "<color=yellow>Reentry Effect Runtime Tuning</color>");

            AddSlider("entryStrength", () => runtimeConfig.entryStrength, value => runtimeConfig.entryStrength = value, 0f, 5000f);
            AddSlider("Fx State", () => runtimeConfig.fxState, value => runtimeConfig.fxState = value, 0f, 2f);
            AddSlider("Length Multiplier", () => runtimeConfig.lengthMultiplier, value => runtimeConfig.lengthMultiplier = value, 0.1f, 10f);
            AddSlider("Trail Alpha", () => runtimeConfig.trailAlphaMultiplier, value => runtimeConfig.trailAlphaMultiplier = value, 0f, 5f);
            AddSlider("Opacity", () => runtimeConfig.opacityMultiplier, value => runtimeConfig.opacityMultiplier = value, 0f, 5f);
            AddSlider("Wrap Opacity", () => runtimeConfig.wrapOpacityMultiplier, value => runtimeConfig.wrapOpacityMultiplier = value, 0f, 5f);
            AddSlider("Wrap Fresnel", () => runtimeConfig.wrapFresnelModifier, value => runtimeConfig.wrapFresnelModifier = value, -2f, 2f);
            AddSlider("Streak Probability", () => runtimeConfig.streakProbability, value => runtimeConfig.streakProbability = value, 0f, 2f);
            AddSlider("Streak Threshold", () => runtimeConfig.streakThreshold, value => runtimeConfig.streakThreshold = value, -1f, 1f);
            AddSlider("Min Temp", () => runtimeConfig.minTemp, value => runtimeConfig.minTemp = value, 0f, 3000f);
            AddSlider("Ignition Temp", () => runtimeConfig.ignitionTemp, value => runtimeConfig.ignitionTemp = value, 0f, 4000f);
            AddSlider("Max Temp", () => runtimeConfig.maxTemp, value => runtimeConfig.maxTemp = value, 0f, 5000f);

            inspectorPanel = Game.Instance.UserInterface.CreateInspectorPanel(
                inspectorModel,
                new InspectorPanelCreationInfo()
                {
                    PanelWidth = 420,
                    Resizable = true,
                });
        }

        private void AddSlider(string label, Func<float> getter, Action<float> setter, float min, float max)
        {
            inspectorModel.Add(new SliderModel(label, getter, setter, min, max, false, true));
        }
        

        public void FlightFixedUpdate(in FlightFrameData flightFrameData)
        {
        }

        public void FlightUpdate(in FlightFrameData flightFrameData)
        {
        }
    }
}
