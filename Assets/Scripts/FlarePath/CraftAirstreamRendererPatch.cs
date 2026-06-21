using System;
using System.Reflection;
using FlarePath;
using HarmonyLib;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    /// Harmony patches for the game's CraftAirstreamRenderer.
    ///
    /// Patches UpdateMesh to inject FlarePath tuning uniforms after the game
    /// has set its own uniforms.
    ///
    /// Architecture:
    /// - Prefix: captures _meshMaterial from the instance into a static field
    /// - Postfix: reads from the static field, sets FlarePath uniforms on the material.
    /// This avoids any IL parameter-name matching issues with the game's DLL.
    public static class CraftAirstreamRendererPatch
    {
        // ---- Cached reflection handles ----
        private static Type _craftType;
        private static FieldInfo _meshMatField;
        private static MethodInfo _updateMeshMethod;

        private static Type CraftType =>
            _craftType ??= AccessTools.TypeByName(
                "Assets.Scripts.Flight.GameView.CraftAirstreamRenderer");

        // ---- Static state passed from Prefix → Postfix ----
        // Thread-safety note: this is safe because UpdateMesh is called on the
        // main thread, and the Prefix always runs before the Postfix for the same call.
        [ThreadStatic]
        private static Material _capturedMat;

        // Set once after the first UpdateMesh run so we don't spam logs
        private static bool _shaderVerified;
        private static bool _nullLogged;
        private static bool _stateLogged;

        // ---- Shader property IDs ----
        private static readonly int PROP_FP_MODE      = Shader.PropertyToID("_FlarePathMode");
        private static readonly int PROP_FP_INTENSITY = Shader.PropertyToID("_FlarePathIntensity");
        private static readonly int PROP_FP_TRAIL     = Shader.PropertyToID("_FlarePathTrailScale");
        private static readonly int PROP_FP_OPACITY   = Shader.PropertyToID("_FlarePathOpacity");
        private static readonly int PROP_FP_HEAT      = Shader.PropertyToID("_FlarePathPlasmaHeat");
        private static readonly int PROP_FP_STREAK    = Shader.PropertyToID("_FlarePathStreakStrength");
        private static readonly int PROP_FP_STREAK_T  = Shader.PropertyToID("_FlarePathStreakThreshold");
        private static readonly int PROP_FP_WRAP       = Shader.PropertyToID("_FlarePathWrapStrength");
        private static readonly int PROP_FP_BLUE      = Shader.PropertyToID("_FlarePathBlueMultiplier");
        private static readonly int PROP_FP_BOOST      = Shader.PropertyToID("_FlarePathLengthBoost");

        // ---- Shader name constants ----
        private const string FP_SHADER_NAME = "FlarePath/ReEntryPlasma";

        // Cached FlarePath shader so we can assign it to the material if needed
        private static Shader _fpShader;

        private static Shader GetFpShader()
        {
            if (_fpShader != null) return _fpShader;
            _fpShader = Mod.Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/FlarePathReEntryPlasma.shader");//Shader.Find("FlarePath/ReEntryPlasma"));
            return _fpShader;
        }

        /// <summary>
        /// Applies Harmony patches. Called from Mod.OnModLoaded().
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            if (CraftType == null)
            {
                Mod.LogError(
                    "FlarePath: Could not resolve CraftAirstreamRenderer type. " +
                    "Patching aborted.");
                return;
            }

            _meshMatField = AccessTools.Field(CraftType, "_meshMaterial");
            if (_meshMatField == null)
            {
                Mod.LogError(
                    "FlarePath: Could not find _meshMaterial field. Patching aborted.");
                return;
            }

            _updateMeshMethod = AccessTools.Method(
                CraftType,
                "UpdateMesh",
                new[] {
                    AccessTools.TypeByName("ModApi.Craft.ICraftScript"),
                    typeof(Bounds)
                });

            if (_updateMeshMethod == null)
            {
                Mod.LogError(
                    "FlarePath: Could not find UpdateMesh method. Patching aborted.");
                return;
            }

            harmony.Patch(
                original: _updateMeshMethod,
                prefix: new HarmonyMethod(
                    typeof(CraftAirstreamRendererPatch),
                    nameof(UpdateMeshPrefix)),
                postfix: new HarmonyMethod(
                    typeof(CraftAirstreamRendererPatch),
                    nameof(UpdateMeshPostfix))
            );

            // ---- Patch InitializeMesh to log Shader.Find result ----
            MethodInfo initMesh = AccessTools.Method(CraftType, "InitializeMesh");
            if (initMesh != null)
            {
                harmony.Patch(
                    original: initMesh,
                    postfix: new HarmonyMethod(
                        typeof(CraftAirstreamRendererPatch),
                        nameof(InitializeMeshPostfix))
                );
            }
            else
            {
                Mod.LogError("FlarePath: Could not find InitializeMesh method.");
            }

            Mod.Log($"FlarePath: CraftAirstreamRenderer patched. Type: {CraftType.FullName}");
        }

        /// <summary>
        /// Prefix: capture _meshMaterial before UpdateMesh runs.
        /// </summary>
        private static void UpdateMeshPrefix(object __instance)
        {
            _capturedMat = _meshMatField?.GetValue(__instance) as Material;
        }

        /// <summary>
        /// Runs after InitializeMesh. Logs which shader the game actually loaded.
        /// </summary>
        private static void InitializeMeshPostfix(object __instance)
        {
            if (__instance == null) return;

            Shader fpShader = GetFpShader();
            Mod.Log($"[FlarePath INIT] Shader.Find(\"{FP_SHADER_NAME}\") = " +
                    $"{(fpShader != null ? fpShader.name + $" (id={fpShader.GetInstanceID()})" : "NULL")}");

            Material mat = _meshMatField?.GetValue(__instance) as Material;
            if (mat != null && mat.shader != null)
            {
                Mod.Log($"[FlarePath INIT] _meshMaterial.shader = \"{mat.shader.name}\" " +
                        $"(id={mat.shader.GetInstanceID()})");
            }
            else
            {
                Mod.Log("[FlarePath INIT] _meshMaterial is null — reentry is disabled.");
            }
        }

        /// <summary>
        /// Postfix: runs after UpdateMesh returns. Sets FlarePath uniforms on
        /// the material that was captured in the Prefix.
        /// </summary>
        private static void UpdateMeshPostfix()
        {
            Material mat = _capturedMat;
            if (mat == null)
            {
                if (!_nullLogged)
                {
                    _nullLogged = true;
                    Mod.Log("[FlarePath] _capturedMat is null — _meshMaterial field returned null");
                }
                return;
            }

            // ---- Per-frame: force FlarePath shader onto this material ----
            // The game may reset the shader inside UpdateMesh, so we must
            // re-apply it EVERY frame (not just once).
            Shader fpShader = GetFpShader();
            if (fpShader == null)
            {
                if (Time.frameCount % 120 == 0)
                    Mod.LogError("[FlarePath] Shader.Find(\"Jundroo/ReEntry/ReEntryAirstreamMesh\") = NULL — shader not loaded!");
                return;
            }

            if (mat.shader != fpShader)
            {
                mat.shader = fpShader;
                if (!_shaderVerified)
                {
                    _shaderVerified = true;
                    Mod.Log($"[FlarePath VERIFY] FORCED mat.shader swap -> \"{fpShader.name}\" (fpId={fpShader.GetInstanceID()} matWasId={mat.GetInstanceID()})");
                }
                else if (Time.frameCount % 60 == 0)
                {
                    Mod.Log($"[FlarePath] game reset mat.shader -> re-swapped to \"{fpShader.name}\"");
                }
            }
            else if (!_shaderVerified)
            {
                _shaderVerified = true;
                Mod.Log($"[FlarePath VERIFY] mat already has \"{fpShader.name}\" — no swap needed");
            }

            // ---- Runtime config ----
            bool uiExists = FlarePathUserInterface.Instance != null;
            bool useConfig = uiExists && FlarePathUserInterface.Instance.UseRuntimeConfig;

            if (!_stateLogged)
            {
                _stateLogged = true;
                Mod.Log($"[FlarePath STATE] uiExists={uiExists} | useConfig={useConfig}");
            }

            FlarePathConfig cfg = useConfig
                ? FlarePathUserInterface.RuntimeConfig
                : FlarePathConfig.CreateDefault();

            float fx       = Mathf.Clamp(cfg.fxState, 0f, 2f);
            float opacity  = Mathf.Clamp(cfg.opacityMultiplier, 0f, 5f);
            float heat     = Mathf.Clamp01(cfg.ignitionTemp / 4000f);
            float streak   = Mathf.Clamp(cfg.streakProbability, 0f, 2f);
            float streakT  = Mathf.Clamp(cfg.streakThreshold, -1f, 1f);
            float wrap     = Mathf.Clamp(cfg.wrapOpacityMultiplier, 0f, 5f);
            float blue     = Mathf.Clamp01(cfg.minTemp / 3000f);
            float length   = Mathf.Clamp(cfg.lengthMultiplier, 0.1f, 10f);
            int   mode     = useConfig
                           ? Mathf.Clamp(cfg.shaderMode, 0, 3)
                           : 1;

            // Log what we're actually setting every few frames
            if (Time.frameCount % 60 == 0)
            {
                Mod.Log($"[FlarePath SET] mode={mode} fx={fx:F2} opacity={opacity:F2} " +
                        $"heat={heat:F3} streak={streak:F2} streakT={streakT:F2} " +
                        $"wrap={wrap:F2} blue={blue:F3} length={length:F2} " +
                        $"| mat={mat.name} shaderId={mat.shader.GetInstanceID()}");
            }

            mat.SetFloat(PROP_FP_MODE,      (float)mode);
            mat.SetFloat(PROP_FP_INTENSITY, fx);
            mat.SetFloat(PROP_FP_TRAIL,     length);
            mat.SetFloat(PROP_FP_OPACITY,   opacity);
            mat.SetFloat(PROP_FP_HEAT,      heat);
            mat.SetFloat(PROP_FP_STREAK,    streak);
            mat.SetFloat(PROP_FP_STREAK_T,  streakT);
            mat.SetFloat(PROP_FP_WRAP,      wrap);
            mat.SetFloat(PROP_FP_BLUE,     blue);
            mat.SetFloat(PROP_FP_BOOST,     length);
        }
    }
}
