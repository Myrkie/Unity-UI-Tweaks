using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkinnedMeshRenderer), true)]
[CanEditMultipleObjects]
public class BlendshapeSearch : Editor
{
    private static string _searchQuery;
    static Editor _defaultEditor;
    private static bool _showBlendshapes;
    class Styles
    {
        public static readonly GUIContent LegacyClampBlendShapeWeightsInfo =
            EditorGUIUtility.TrTextContent(
                "Note that BlendShape weight range is clamped. This can be disabled in Player Settings.");
    }

    static BlendshapeSearch()
    {
        var harmonyInstance = new Harmony("BlendshapeSearch");
        try
        {
            var method = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor").GetMethod(
                "OnBlendShapeUI",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            harmonyInstance.Patch(method,
                prefix: new HarmonyMethod(typeof(BlendshapeSearch).GetMethod(nameof(PrefixMethod))),
                postfix: new HarmonyMethod(typeof(BlendshapeSearch).GetMethod(nameof(PostfixMethod))));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to patch OnBlendShapeUI method: {ex}");
        }
    }

    public override void OnInspectorGUI()
    {
        _defaultEditor.OnInspectorGUI();
    }

    void OnEnable()
    {
        _defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.SkinnedMeshRendererEditor, UnityEditor"));
    }

    public static bool PrefixMethod()
    {
        return false;
    }

    public static void PostfixMethod()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blend Shape Search", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _searchQuery = EditorGUILayout.TextField("Search by:", _searchQuery);
        if (GUILayout.Button("Clear Search"))
        {
            _searchQuery = "";
        }

        EditorGUILayout.EndHorizontal();
        _showBlendshapes = EditorGUILayout.Foldout(_showBlendshapes, "Blendshapes");
        if (!_showBlendshapes) return;
        if (PlayerSettings.legacyClampBlendShapeWeights)
            EditorGUILayout.HelpBox(Styles.LegacyClampBlendShapeWeightsInfo.text, MessageType.Info);
        SearchAndDrawBlendShapes(_searchQuery);
    }

    private static void SearchAndDrawBlendShapes(string query)
    {
        SkinnedMeshRenderer renderer = (SkinnedMeshRenderer)_defaultEditor.target;
        var sharedMesh = renderer.sharedMesh;
        if (sharedMesh == null)
        {
            Debug.LogWarning("No shared mesh found.");
            return;
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty blendShapeWeightsProperty = serializedRenderer.FindProperty("m_BlendShapeWeights");
        if (blendShapeWeightsProperty == null)
        {
            Debug.LogWarning("No blend shape weights property found.");
            return;
        }

        int blendShapeCount = sharedMesh.blendShapeCount;
        blendShapeWeightsProperty.arraySize = blendShapeCount; // Adjust array size
        for (int i = 0; i < blendShapeCount; i++)
        {
            SerializedProperty blendShapeWeightProperty = blendShapeWeightsProperty.GetArrayElementAtIndex(i);
            if (blendShapeWeightProperty == null)
            {
                Debug.LogWarning($"Blend shape weight property at index {i} is null.");
                continue;
            }

            string blendShapeName = sharedMesh.GetBlendShapeName(i);
            if (string.IsNullOrEmpty(query) || blendShapeName.ToLower().Contains(query.ToLower()))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(blendShapeName);
                Rect labelRect = GUILayoutUtility.GetLastRect();
                float labelWidth = EditorGUIUtility.labelWidth;
                float sliderWidth = EditorGUIUtility.currentViewWidth - labelRect.x - labelWidth -
                                    EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.BeginProperty(labelRect, GUIContent.none, blendShapeWeightProperty);
                EditorGUI.BeginChangeCheck();
                float newValue =
                    EditorGUI.Slider(new Rect(labelRect.x + labelWidth, labelRect.y, sliderWidth, labelRect.height),
                        blendShapeWeightProperty.floatValue, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    blendShapeWeightProperty.floatValue = newValue;
                }

                EditorGUI.EndProperty();
                EditorGUILayout.EndHorizontal();
            }
        }

        serializedRenderer.ApplyModifiedProperties();
    }
}