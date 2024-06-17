using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;

namespace MyrkieUiTweaks
{
    [CustomEditor(typeof(SkinnedMeshRenderer), true)] 
    public class BlendshapeSearch : Editor
    {
        private static string _searchQuery;
        private static bool _showBlendshapes;
        private static Editor _defaultEditor;
        private static MethodInfo sliderMethod;
        private readonly BoxBoundsHandle _BoundsHandle = new();
        
        class Styles
        {
            public static readonly GUIContent LegacyClampBlendShapeWeightsInfo =
                EditorGUIUtility.TrTextContent(
                    "Note that BlendShape weight range is clamped. This can be disabled in Player Settings.");

            public static readonly GUIContent NoActiveBlendShapes =
                EditorGUIUtility.TrTextContent("No BlendShapes exist on this Mesh.");
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
            SliderPatch();
        }

        private static void SliderPatch()
        {
            var sliderMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => method.Name == "Slider")
                .ToArray();

            sliderMethod = sliderMethods.FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 7 &&
                       parameters[0].ParameterType == typeof(SerializedProperty) &&
                       parameters[1].ParameterType == typeof(float) &&
                       parameters[2].ParameterType == typeof(float) &&
                       parameters[3].ParameterType == typeof(float) &&
                       parameters[4].ParameterType == typeof(float) &&
                       parameters[5].ParameterType == typeof(GUIContent) &&
                       parameters[6].ParameterType == typeof(GUILayoutOption[]);
            });
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
            var renderer = (SkinnedMeshRenderer)_defaultEditor.target;

            if (renderer.updateWhenOffscreen)
            {
                var bounds = renderer.bounds;
                var center = bounds.center;
                var size = bounds.size;

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

        private void OnDisable()
        {
            // disposing of this to make sure it doesnt cause any leaks
            if (_defaultEditor != null)
            {
                DestroyImmediate(_defaultEditor);
            }
        }

        public static bool PrefixMethod()
        {
            return false;
        }

        public static void PostfixMethod()
        {
            // Collect common blend shapes among all selected objects
            var commonBlendShapes = new HashSet<string>();
            var firstObject = true;
            foreach (var target in _defaultEditor.targets)
            {
                var renderer = target as SkinnedMeshRenderer;
                if (renderer == null) continue;
                var sharedMesh = renderer.sharedMesh;
                if (sharedMesh == null) continue;
                var blendShapes = new List<string>();
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

        private static void SearchAndDrawBlendShapes(ICollection<string> commonBlendShapes)
        {
            foreach (var target in _defaultEditor.targets)
            {
                var renderer = target as SkinnedMeshRenderer;
                if (renderer == null) continue;
                var serializedRenderer = new SerializedObject(renderer);
                var blendShapeWeightsProperty = serializedRenderer.FindProperty("m_BlendShapeWeights");
                if (blendShapeWeightsProperty == null) continue;
                var sharedMesh = renderer.sharedMesh;
                if (sharedMesh == null) continue;
                var blendShapeCount = sharedMesh.blendShapeCount;
                var currentBlendShapeCount = blendShapeWeightsProperty.arraySize;

                #region Sync Shapes
                // Synchronize blend shape names and create a map
                // This is done because for some weird reason the blendshape `m_BlendShapeWeights` can be mismatched in the skinmesh renderer
                // this is done to sync the blendshapes from the mesh to the skinmesh renderer, probably terrible way to do this but it works 
                if (blendShapeCount != currentBlendShapeCount)
                {
                    blendShapeWeightsProperty.arraySize = blendShapeCount;
                }

                var blendShapeMap = new Dictionary<string, SerializedProperty>();
                for (int i = 0; i < blendShapeCount; i++)
                {
                    string blendShapeName = sharedMesh.GetBlendShapeName(i);
                    var blendShapeWeightProperty =
                        blendShapeWeightsProperty.GetArrayElementAtIndex(i);
                    blendShapeMap[blendShapeName] = blendShapeWeightProperty;
                }

                #endregion

                serializedRenderer.ApplyModifiedProperties();
                if (blendShapeCount < 1)
                {
                    EditorGUILayout.HelpBox(Styles.NoActiveBlendShapes.text, MessageType.Info);
                    return;
                }

                foreach (var blendShapeName in blendShapeMap.Keys.Where(blendShapeName => commonBlendShapes.Contains(blendShapeName) && 
                             (string.IsNullOrEmpty(_searchQuery) ||
                              blendShapeName.ToLower().Contains(_searchQuery.ToLower()))))
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    var blendShapeWeightProperty = blendShapeMap[blendShapeName];
                    if (blendShapeWeightProperty != null)
                    {
                        var content = _defaultEditor.targets.Length < 2
                            ? new GUIContent(blendShapeName)
                            : new GUIContent($"{sharedMesh.name}-{blendShapeName}");
                        
                        
                        EditorGUI.BeginChangeCheck();
                        if (sliderMethod != null && !PlayerSettings.legacyClampBlendShapeWeights)
                        {
                            sliderMethod.Invoke(null, new object[]{ blendShapeWeightProperty, 0f, 100f, float.MinValue, float.MaxValue, content, null });
                        }
                        else
                        {
                            EditorGUILayout.Slider(blendShapeWeightProperty, 0f, 100f, content);
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            blendShapeWeightProperty.serializedObject.ApplyModifiedProperties();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                serializedRenderer.ApplyModifiedProperties();
            }
        }
    }
}