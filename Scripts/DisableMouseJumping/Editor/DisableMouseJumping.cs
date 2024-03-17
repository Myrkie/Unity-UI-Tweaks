using HarmonyLib;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MyrkieUiTweaks
{
    [InitializeOnLoad]
    public class DisableMouseJumping : Editor
    {
        private static Harmony _harmonyInstance;

        private static MethodInfo GetMethod(string MethodName,
            BindingFlags bindingAttributes = BindingFlags.NonPublic | BindingFlags.Static)
        {
            return typeof(DisableMouseJumping).GetMethod(MethodName, bindingAttributes);
        }

        static DisableMouseJumping()
        {
            if (!UserChoicePatcherUI.DisableMouseJumping) return;

            _harmonyInstance = new Harmony("DisableMouseJumping");

            try
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.Log("Attempting to patch EditorWindow.OnGUI");
                }

                var method = typeof(EditorWindow).GetMethod("BeginWindows",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _harmonyInstance.Patch(method, postfix: new HarmonyMethod(GetMethod(nameof(PostfixMethod))));
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.Log("Patched EditorWindow.OnGUI successfully!");
                }
            }
            catch (Exception ex)
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.LogError($"Failed to patch EditorWindow.OnGUI! - {ex}");
                }
            }
        }

        private static void PostfixMethod()
        {
            EditorGUIUtility.SetWantsMouseJumping(0);
        }
    }
}