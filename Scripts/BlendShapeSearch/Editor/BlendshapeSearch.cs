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
    [CanEditMultipleObjects]
    public class BlendshapeSearch : Editor
    {
        private static string _searchQuery;
        private static bool _showBlendshapes;
        private static Editor _defaultEditor;
        private static MethodInfo sliderMethod;
        private readonly BoxBoundsHandle _BoundsHandle = new();
        
        private SerializedProperty sortingLayerID;
        private SerializedProperty sortingOrder;
        
        class Styles
        {
            public static GUIContent legacyClampBlendShapeWeightsInfo;
            public static GUIContent meshNotSupportingSkinningInfo;
            public static GUIContent bounds;
            public static GUIContent quality;
            public static GUIContent updateWhenOffscreen;
            public static GUIContent mesh;
            public static GUIContent rootBone;
            public static readonly GUIContent noactiveblendshapes = EditorGUIUtility.TrTextContent("No BlendShapes exist on this Mesh.");
        }

        static BlendshapeSearch()
        {
            #region Harmony patching
            
            var harmonyInstance = new Harmony("BlendshapeSearch");
            
            try
            {
                var bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var editorBlendShapeUI = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor").GetMethod("OnBlendShapeUI", bindings);
                
                var editorOnSceneGUI = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor").GetMethod("OnSceneGUI", bindings);
                
                // reflect code inside OnInspectorGUI
                harmonyInstance.Patch(editorBlendShapeUI,
                    prefix: new HarmonyMethod(typeof(BlendshapeSearch).GetMethod(nameof(PrefixMethod))),
                    postfix: new HarmonyMethod(typeof(BlendshapeSearch).GetMethod(nameof(PostfixMethod))));

                harmonyInstance.Patch(editorOnSceneGUI,
                    prefix: new HarmonyMethod(typeof(BlendshapeSearch).GetMethod(nameof(PrefixMethod))));
            }
            catch (Exception ex)
            {
                if (UserChoicePatcherUI.DebugLogging)
                {
                    Debug.LogError($"Failed to patch OnBlendShapeUI method: {ex}");
                }
            }
            
            #endregion
            ReflectSlider();
            ReflectStyles();
        }
        
        #region Unity event functions
        
        public override void OnInspectorGUI()
        {
            if (_defaultEditor != null)
            {
                _defaultEditor.OnInspectorGUI();
            }

            if (UserChoicePatcherUI.EnableSortingLayers)
            {
                SortingLayers();
            }
        }
        public void OnSceneGUI()
        {
            // was unable to figure out how to reflect this code, and it's not overridable.
            // so I had to copy most of it from unity CS reference and modify
            // https://github.com/Unity-Technologies/UnityCsReference/blob/6c8a95ff127619e73519662fa497e242b898f9af/Editor/Mono/Inspector/SkinnedMeshRendererEditor.cs#L170
            if (!_defaultEditor.target)
                return;

            var editModeCondition = EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(_defaultEditor);

            if (target is not SkinnedMeshRenderer renderer) return;
                
            if (renderer.updateWhenOffscreen)
            {
                var bounds = renderer.bounds;
                var center = bounds.center;
                var size = bounds.size;

                Handles.color = editModeCondition ? Color.yellow : Color.white;
                Handles.DrawWireCube(center, size);
            }
            else
            {
                using (new Handles.DrawingScope(renderer.rootBone.localToWorldMatrix))
                {
                    Bounds bounds = renderer.localBounds;
                    _BoundsHandle.center = bounds.center;
                    _BoundsHandle.size = bounds.size;

                    _BoundsHandle.handleColor = editModeCondition ? Color.yellow : Color.clear;
                    _BoundsHandle.wireframeColor = editModeCondition ? Color.yellow : Color.white;

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
            if (UserChoicePatcherUI.EnableSortingLayers)
            {
                #region Get Serialized Property
                sortingLayerID = serializedObject.FindProperty("m_SortingLayerID");
                sortingOrder = serializedObject.FindProperty("m_SortingOrder");
                #endregion
            }
            _BoundsHandle.SetColor(new Color(255, 255, 255, 150) / 255);
        }

        private void OnDisable()
        {
            // disposing of this to make sure it doesn't cause any leaks
            if (_defaultEditor != null)
            {
                DestroyImmediate(_defaultEditor);
            }
        }
        
        #endregion

        #region redirected ui logic
        /// <summary>
        /// Postfixed OnInspectorGUI
        /// </summary>
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
                var blendshapeCount = sharedMesh.blendShapeCount;
                
                if (blendshapeCount < 1)
                {
                    continue;
                }
                
                for (int i = 0; i < blendshapeCount; i++)
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
                _searchQuery = string.Empty;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                _showBlendshapes = true;
            }

            EditorGUILayout.EndHorizontal();
            _showBlendshapes = EditorGUILayout.Foldout(_showBlendshapes, "Blendshapes");
            if (!_showBlendshapes) return;
            if (PlayerSettings.legacyClampBlendShapeWeights)
                EditorGUILayout.HelpBox(Styles.legacyClampBlendShapeWeightsInfo.text, MessageType.Info);
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
                // This is done because for some weird reason the blend shape `m_BlendShapeWeights` can be mismatched in the skin mesh renderer
                // this is done to sync the blend shapes from the mesh to the skin mesh renderer, probably terrible way to do this, but it works 
                if (blendShapeCount != currentBlendShapeCount)
                {
                    blendShapeWeightsProperty.arraySize = blendShapeCount;
                }
                serializedRenderer.ApplyModifiedProperties();
                #endregion
                
                
                if (blendShapeCount < 1)
                {
                    if (_defaultEditor.targets.Length <= 1)
                    {
                        EditorGUILayout.HelpBox(Styles.noactiveblendshapes.text, MessageType.Info);
                    }
                    continue;
                }
                
                // build map of <Shape Name>, <SerializedProperty>
                var blendShapeMap = new Dictionary<string, SerializedProperty>();
                for (int i = 0; i < blendShapeCount; i++)
                {
                    string blendShapeName = sharedMesh.GetBlendShapeName(i);
                    var blendShapeWeightProperty = blendShapeWeightsProperty.GetArrayElementAtIndex(i);
                    blendShapeMap[blendShapeName] = blendShapeWeightProperty;
                }

                foreach (var blendShapeName in blendShapeMap.Keys.Where(blendShapeName => 
                             commonBlendShapes.Contains(blendShapeName) && (string.IsNullOrEmpty(_searchQuery) ||
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

        // https://discussions.unity.com/t/horizontal-line-in-editor-window/694105/12
        private static void DrawHorizontalGUILine(int height = 1)
        {
            GUILayout.Space(4);

            Rect rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
            rect.height = height;
            rect.xMin = 0;
            rect.xMax = EditorGUIUtility.currentViewWidth;

            Color lineColor = new Color(0.10196f, 0.10196f, 0.10196f, 1);
            EditorGUI.DrawRect(rect, lineColor);
            GUILayout.Space(4);
        }
        
        #endregion
        
        #region reflection
        
        public static bool PrefixMethod()
        {
            return false;
        }
        private static void ReflectStyles()
        {
            Type editorType = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor+Styles");
            if (editorType != null)
            {
                static T GetFieldValue<T>(Type type, string fieldName)
                {
                    FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public);
                    return field != null ? (T)field.GetValue(null) : default(T);
                }
                
                Styles.legacyClampBlendShapeWeightsInfo = GetFieldValue<GUIContent>(editorType, "legacyClampBlendShapeWeightsInfo");
                Styles.meshNotSupportingSkinningInfo = GetFieldValue<GUIContent>(editorType, "meshNotSupportingSkinningInfo");
                Styles.bounds = GetFieldValue<GUIContent>(editorType, "bounds");
                Styles.quality = GetFieldValue<GUIContent>(editorType, "quality");
                Styles.updateWhenOffscreen = GetFieldValue<GUIContent>(editorType, "updateWhenOffscreen");
                Styles.mesh = GetFieldValue<GUIContent>(editorType, "mesh");
                Styles.rootBone = GetFieldValue<GUIContent>(editorType, "rootBone");
            }
        }
        
        private static void ReflectSlider()
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

        #endregion
        
        #region sortinglayers

        private void SortingLayers()
        {
            EditorGUILayout.Space();
            DrawHorizontalGUILine();

            #region SortingLayer
            int selectedLayer = GetSelectedLayerIndex();
            string[] layerNames = GetSortingLayerNames();
            selectedLayer = EditorGUILayout.Popup("Sorting Layer", selectedLayer, layerNames);
            sortingLayerID.intValue = GetSortingLayerUniqueIDs()[selectedLayer];
            #endregion

            #region OrderInLayer
            sortingOrder ??= serializedObject.FindProperty("m_SortingOrder");
            EditorGUILayout.PropertyField(sortingOrder, new GUIContent("Order In Layer"));
            serializedObject.ApplyModifiedProperties();
            #endregion
        }


        private int GetSelectedLayerIndex()
        {
            int[] layerIDs = GetSortingLayerUniqueIDs();

            sortingLayerID ??= serializedObject.FindProperty("m_SortingLayerID");
            
            int selectedLayerID = sortingLayerID.intValue;

            int selectedIndex = Array.IndexOf(layerIDs, selectedLayerID);
            return selectedIndex != -1 ? selectedIndex : 0;
        }

        private string[] GetSortingLayerNames ()
        {
            return (string[])typeof(InternalEditorUtility)
                .GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null, null);
        }

        private int[] GetSortingLayerUniqueIDs ()
        {
            return (int[])typeof(InternalEditorUtility)
                .GetProperty("sortingLayerUniqueIDs", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null, null);
        }

        #endregion
    }
}