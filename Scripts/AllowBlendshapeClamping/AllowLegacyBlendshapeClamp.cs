using HarmonyLib;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Editor;

[InitializeOnLoad]
public class AllowLegacyBlendshapeClamp : Editor
{
    private static Harmony _harmonyInstance;

    private static MethodInfo GetMethod(string MethodName,
        BindingFlags bindingAttributes = BindingFlags.NonPublic | BindingFlags.Static)
    {
        return typeof(Patches).GetMethod(MethodName, bindingAttributes);
    }

    static AllowLegacyBlendshapeClamp()
    {
        try
        {
            Debug.Log("SetPlayerSettings: Attempting to allow mesh Shapekey clamping to be disable");
            var method1 = typeof(EnvConfig).GetMethod("SetPlayerSettings", BindingFlags.NonPublic | BindingFlags.Static);
            _harmonyInstance.Patch(method1, postfix: new HarmonyMethod(typeof(Patches), nameof(PostfixShapekeys)));
            Debug.Log("SetPlayerSettings: Shapekey Clamping can now be turned off!");
        } catch (Exception ex) 
        {
            Debug.LogError($"SetPlayerSettings: Failed to patch SetPlayerSettings! - {ex}");
        }
    }
    static void PostfixShapekeys()
    {
        PlayerSettings.legacyClampBlendShapeWeights = false;
    }
}