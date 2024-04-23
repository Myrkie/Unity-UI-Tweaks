using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;

namespace MyrkieUiTweaks
{
    [CustomEditor(typeof(SkinnedMeshRenderer), true)]
    [CanEditMultipleObjects]
    public class BlendshapeSearch : Editor
    {
        private static string _searchQuery;
        private static bool _showBlendshapes;
        private static Editor _defaultEditor;
        private readonly BoxBoundsHandle _BoundsHandle = new();
        
        class Styles
        {
            public static readonly GUIContent LegacyClampBlendShapeWeightsInfo =
                EditorGUIUtility.TrTextContent(
                    "Note that BlendShape weight range is clamped. This can be disabled in Player Settings.");

            public static readonly GUIContent NoActiveBlendShapes =
                EditorGUIUtility.TrTextContent("No BlendShapes exist on this Mesh.");
            
            public static readonly GUIStyle YellowTextStyle = new(EditorStyles.label)
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

        public void OnSceneGUI()
        {
            // was unable to figure out how to reflect this code, and its not overridable.
            // so I had to copy most of it from unity CS reference and modify
            // https://github.com/Unity-Technologies/UnityCsReference/blob/6c8a95ff127619e73519662fa497e242b898f9af/Editor/Mono/Inspector/SkinnedMeshRendererEditor.cs#L170
            if (!_defaultEditor.target)
                return;
            SkinnedMeshRenderer renderer = (SkinnedMeshRenderer)_defaultEditor.target;

            if (renderer.updateWhenOffscreen)
            {
                Bounds bounds = renderer.bounds;
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;

                Handles.DrawWireCube(center, size);
            }
            else
            {
                using (new Handles.DrawingScope(renderer.rootBone.localToWorldMatrix))
                {
                    Bounds bounds = renderer.localBounds;
                    _BoundsHandle.center = bounds.center;
                    _BoundsHandle.size = bounds.size;
                    
                    _BoundsHandle.handleColor = EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(_defaultEditor) ?
                        _BoundsHandle.wireframeColor : Color.clear;
                    

                    EditorGUI.BeginChangeCheck();
                    _BoundsHandle.DrawHandle();
                    if (!EditorGUI.EndChangeCheck()) return;
                    Undo.RecordObject(renderer, "Resize Bounds");
                    renderer.localBounds = new Bounds(_BoundsHandle.center, _BoundsHandle.size);
                }
            }
        }

        void OnEnable()
        {
            _defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.SkinnedMeshRendererEditor, UnityEditor"));
            _BoundsHandle.SetColor(new Color(255, 255, 255, 150) / 255);
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
                    if (sharedMesh == null) continue;
                    int blendShapeCount = sharedMesh.blendShapeCount;
                    int currentBlendShapeCount = blendShapeWeightsProperty.arraySize;

                    #region Sync Shapes
                    // Synchronize blend shape names and create a map
                    // This is done because for some weird reason the blendshape `m_BlendShapeWeights` can be mismatched in the skinmesh renderer
                    // this is done to sync the blendshapes from the mesh to the skinmesh renderer, probably terrible way to do this but it works 
                    
                    if (blendShapeCount != currentBlendShapeCount)
                    {
                        blendShapeWeightsProperty.arraySize = blendShapeCount;
                    }

                    Dictionary<string, SerializedProperty> blendShapeMap = new Dictionary<string, SerializedProperty>();
                    
                    for (int i = 0; i < blendShapeCount; i++)
                    {
                        string blendShapeName = sharedMesh.GetBlendShapeName(i);
                        SerializedProperty blendShapeWeightProperty =
                            blendShapeWeightsProperty.GetArrayElementAtIndex(i);
                        blendShapeMap[blendShapeName] = blendShapeWeightProperty;
                    }

                    #endregion

                    serializedRenderer.ApplyModifiedProperties();

                    if (blendShapeCount < 1)
                    {
                        EditorGUILayout.HelpBox(Styles.NoActiveBlendShapes.text, MessageType.Info);
                    }

                    foreach (var blendShapeName in blendShapeMap.Keys)
                    {
                        if (commonBlendShapes.Contains(blendShapeName) && (string.IsNullOrEmpty(_searchQuery) ||
                                                                           blendShapeName.ToLower()
                                                                               .Contains(_searchQuery.ToLower())))
                        {
                            EditorGUILayout.BeginHorizontal();
                           SerializedProperty blendShapeWeightProperty = blendShapeMap[blendShapeName];
                           if (blendShapeWeightProperty != null)
                           {
                               if (_defaultEditor.targets.Length < 2)
                               {
                                   GUIContent content = new GUIContent(blendShapeName);
    
                                   EditorGUI.BeginChangeCheck();
                                   EditorGUILayout.Slider(blendShapeWeightProperty, 0f, 100f, content);
                                   if (EditorGUI.EndChangeCheck())
                                   {
                                       blendShapeWeightProperty.serializedObject.ApplyModifiedProperties();
                                   }
                               }
                               else
                               {
                                   GUIContent content = new GUIContent($"{sharedMesh.name}-{blendShapeName}");
    
                                   EditorGUI.BeginChangeCheck();
                                   EditorGUILayout.Slider(blendShapeWeightProperty, 0f, 100f, content);
                                   if (EditorGUI.EndChangeCheck())
                                   {
                                       blendShapeWeightProperty.serializedObject.ApplyModifiedProperties();
                                   }
                               }
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