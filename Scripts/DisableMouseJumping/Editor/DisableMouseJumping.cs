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
        private static readonly Harmony _harmonyInstance = new("DisableMouseJumping");

        static DisableMouseJumping()
        {
            if (!UserChoicePatcherUI.DisableMouseJumping) return;

            try
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.Log("Attempting to patch EditorWindow.OnGUI");
                }

                var method = GetEditorWindowMethod();
                _harmonyInstance.Patch(method, postfix: new HarmonyMethod(GetPostfixMethod()));
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
        private static MethodInfo GetPostfixMethod()
        {
            return typeof(DisableMouseJumping).GetMethod(nameof(DisableMouseJump), BindingFlags.NonPublic | BindingFlags.Static);
        }
        
        private static MethodBase GetEditorWindowMethod()
        {
            return typeof(EditorWindow).GetMethod("BeginWindows", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static void DisableMouseJump()
        {
            EditorGUIUtility.SetWantsMouseJumping(0);
        }
    }
}
