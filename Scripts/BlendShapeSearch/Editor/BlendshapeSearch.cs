using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;

namespace MyrkieUiTweaks
{
    [CustomEditor(typeof(SkinnedMeshRenderer), true)]
    [CanEditMultipleObjects]
    public class BlendshapeSearch : Editor
    {
        private static string _searchQuery;
        private static bool _showBlendshapes;
        private static Editor _defaultEditor;

        class Styles
        {
            public static readonly GUIContent LegacyClampBlendShapeWeightsInfo =
                EditorGUIUtility.TrTextContent(
                    "Note that BlendShape weight range is clamped. This can be disabled in Player Settings.");

            public static readonly GUIContent NoActiveBlendShapes =
                EditorGUIUtility.TrTextContent("No BlendShapes exist on this Mesh.");

            public static readonly GUIStyle YellowTextStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.yellow }
            };
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
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.LogError($"Failed to patch OnBlendShapeUI method: {ex}");
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (_defaultEditor != null)
            {
                _defaultEditor.OnInspectorGUI();
            }
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
            // Collect common blend shapes among all selected objects
            HashSet<string> commonBlendShapes = new HashSet<string>();
            bool firstObject = true;
            foreach (var target in _defaultEditor.targets)
            {
                SkinnedMeshRenderer renderer = target as SkinnedMeshRenderer;
                if (renderer != null)
                {
                    Mesh sharedMesh = renderer.sharedMesh;
                    if (sharedMesh == null) continue;
                    List<string> blendShapes = new List<string>();
                    for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                    {
                        blendShapes.Add(sharedMesh.GetBlendShapeName(i));
                    }

                    if (firstObject)
                    {
                        commonBlendShapes.UnionWith(blendShapes);
                        firstObject = false;
                    }
                    else
                    {
                        commonBlendShapes.IntersectWith(blendShapes);
                    }
                }
            }

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
            SearchAndDrawBlendShapes(commonBlendShapes);
        }

        private static void SearchAndDrawBlendShapes(HashSet<string> commonBlendShapes)
        {
            foreach (var target in _defaultEditor.targets)
            {
                SkinnedMeshRenderer renderer = target as SkinnedMeshRenderer;
                if (renderer != null)
                {
                    SerializedObject serializedRenderer = new SerializedObject(renderer);
                    SerializedProperty blendShapeWeightsProperty =
                        serializedRenderer.FindProperty("m_BlendShapeWeights");
                    if (blendShapeWeightsProperty == null) continue;
                    Mesh sharedMesh = renderer.sharedMesh;
                    var blendShapeCount = renderer.sharedMesh.blendShapeCount;
                    if (sharedMesh == null) continue;
                    float maxLabelWidth = 0f;
                    if (blendShapeCount < 1)
                    {
                        EditorGUILayout.HelpBox(Styles.NoActiveBlendShapes.text, MessageType.Info);
                    }

                    for (int i = 0; i < blendShapeCount; i++)
                    {
                        string blendShapeName = sharedMesh.GetBlendShapeName(i);
                        if (commonBlendShapes.Contains(blendShapeName) && (string.IsNullOrEmpty(_searchQuery) ||
                                                                           blendShapeName.ToLower()
                                                                               .Contains(_searchQuery.ToLower())))
                        {
                            EditorGUILayout.BeginHorizontal();
                            if (_defaultEditor.targets.Length < 2)
                            {
                                EditorGUILayout.LabelField(blendShapeName);
                            }
                            else
                            {
                                EditorGUILayout.BeginVertical();
                                EditorGUILayout.LabelField(sharedMesh.name, Styles.YellowTextStyle);
                                EditorGUILayout.LabelField(blendShapeName);
                                EditorGUILayout.EndVertical();
                            }

                            SerializedProperty blendShapeWeightProperty =
                                blendShapeWeightsProperty.GetArrayElementAtIndex(i);
                            if (blendShapeWeightProperty != null)
                            {
                                Rect labelRect = GUILayoutUtility.GetLastRect();
                                float labelWidth = EditorGUIUtility.labelWidth;
                                float sliderWidth = EditorGUIUtility.currentViewWidth - labelRect.x - labelWidth -
                                                    EditorGUIUtility.standardVerticalSpacing;
                                EditorGUI.BeginProperty(labelRect, GUIContent.none, blendShapeWeightProperty);
                                EditorGUI.BeginChangeCheck();
                                float newValue = EditorGUI.Slider(
                                    new Rect(labelRect.x + labelWidth, labelRect.y, sliderWidth, labelRect.height),
                                    blendShapeWeightProperty.floatValue, 0f, 100f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    blendShapeWeightProperty.floatValue = newValue;
                                }

                                EditorGUI.EndProperty();
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    serializedRenderer.ApplyModifiedProperties();
                }
            }
        }
    }
}