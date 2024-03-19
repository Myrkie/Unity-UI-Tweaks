using HarmonyLib;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Editor;

namespace MyrkieUiTweaks
{
#if VRC_SDK_VRCSDK3
    [InitializeOnLoad]
    public class AllowLegacyBlendshapeClamp : Editor
    {
        private static Harmony _harmonyInstance;

        static AllowLegacyBlendshapeClamp()
        {
            if (!UserChoicePatcherUI.AllowBlendshapeClamping) return;
            var _harmonyInstance = new Harmony("AllowLegacyBlendshapeClamp");
            try
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.Log("SetPlayerSettings: Attempting to allow mesh Shapekey clamping to be disable");
                }

                var method1 = typeof(EnvConfig).GetMethod("SetPlayerSettings", BindingFlags.NonPublic | BindingFlags.Static);
                _harmonyInstance.Patch(method1,
                    postfix: new HarmonyMethod(typeof(AllowLegacyBlendshapeClamp), nameof(PostfixShapekeys)));
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.Log("SetPlayerSettings: Shapekey Clamping can now be turned off!");
                }
            }
            catch (Exception ex)
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.LogError($"SetPlayerSettings: Failed to patch SetPlayerSettings! - {ex}");
                }
            }
        }

        static void PostfixShapekeys()
        {
            PlayerSettings.legacyClampBlendShapeWeights = false;
        }
    }
#endif
}