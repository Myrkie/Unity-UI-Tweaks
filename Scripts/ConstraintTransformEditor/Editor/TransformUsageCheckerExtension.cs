using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Collections.Generic;
#if VRC_SDK_VRCSDK3
using VRC.Dynamics;
#endif

namespace MyrkieUiTweaks
{
    [CustomEditor(typeof(Transform), true)]
    [CanEditMultipleObjects]
    public class TransformUsageCheckerExtension : Editor
    {
        Editor _defaultEditor;
        private Transform _transform;
        private readonly Dictionary<string, bool> _foldoutStates = new();

        #region unity event functions

        void OnEnable()
        {
            _defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
            _transform = _defaultEditor.target as Transform;
        }

        private void OnDisable()
        {
            // disposing of this to make sure it doesn't cause any leaks
            if (_defaultEditor != null)
            {
                DestroyImmediate(_defaultEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            _defaultEditor.OnInspectorGUI();

            if (!UserChoicePatcherUI.ConstraintTransformEditor) return;
            if (_transform is null) return;

            bool hasConstraints = CheckNativeConstraints();
#if VRC_SDK_VRCSDK3
            bool hasVrcConstraints = CheckVrcConstraints();
            if (!hasConstraints && hasVrcConstraints)
            {
                DrawHorizontalGUILine();
            }
#endif
        }
        #endregion

        #region check native constraints
        private bool CheckNativeConstraints()
        {
            bool foundConstraint = false;
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (!obj.scene.IsValid()) continue;

                IConstraint[] constraints = obj.GetComponents<IConstraint>();
                foreach (IConstraint constraint in constraints)
                {
                    if (!IsNativeConstraintTransformUsedAsSource(constraint, _transform)) continue;

                    if (!foundConstraint)
                    {
                        DrawHorizontalGUILine();
                        foundConstraint = true;
                    }

                    DrawConstraintButton(obj, constraint as Component);
                }
            }

            return foundConstraint;
        }
        #endregion

#if VRC_SDK_VRCSDK3
        private bool CheckVrcConstraints()
        {
            bool foundConstraint = false;
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (!obj.scene.IsValid()) continue;

                VRCConstraintBase[] constraints = obj.GetComponents<VRCConstraintBase>();
                foreach (VRCConstraintBase constraint in constraints)
                {
                    if (!IsVRCConstraintTransformUsedAsSource(constraint, _transform)) continue;

                    if (!foundConstraint)
                    {
                        DrawHorizontalGUILine();
                        foundConstraint = true;
                    }

                    DrawConstraintButton(obj, constraint);
                }
            }

            return foundConstraint;
        }
#endif

        #region draw main ui
        
        private void DrawConstraintButton(GameObject obj, Component constraint)
        {
            float availableWidth = EditorGUIUtility.currentViewWidth;
            float minWidthForHorizontal = 400; 

            if (availableWidth > minWidthForHorizontal)
            {
                DrawHorizontalLayout(obj, constraint);
            }
            else
            {
                DrawVerticalLayout(obj, constraint);
            }
        }

        private void DrawHorizontalLayout(GameObject obj, Component constraint)
        {
            EditorGUILayout.BeginHorizontal();

            Texture icon = GetComponentIconOrReturnDefault(constraint.GetType());
            GUILayout.Label(new GUIContent(icon), GUILayout.Width(20), GUILayout.Height(20));

            string objectName = obj.name;
            GUILayout.Label(objectName, EditorStyles.label, GUILayout.Width(GetLabelWidth(objectName)));

            GUILayout.Box(GUIContent.none, GUILayout.Width(2), GUILayout.ExpandHeight(true));

            GUILayout.Label(constraint.GetType().Name, GUILayout.Width(130));

            GUILayout.FlexibleSpace();

            DrawButtons(obj);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawVerticalLayout(GameObject obj, Component constraint)
        {
            string uniqueKey = GetUniqueKey(obj, constraint);

            EditorGUILayout.BeginHorizontal();

            Texture icon = GetComponentIconOrReturnDefault(constraint.GetType());
            GUILayout.Label(new GUIContent(icon), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Space(20);

            GUILayout.BeginVertical();

            _foldoutStates.TryAdd(uniqueKey, true);
            
            _foldoutStates[uniqueKey] = EditorGUILayout.Foldout(_foldoutStates[uniqueKey], $"{obj.name} - {constraint.GetType().Name}", true);

            if (_foldoutStates[uniqueKey])
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(constraint.GetType().Name, GUILayout.Width(130));
        
                DrawButtons(obj);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel--;
            }

            GUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawButtons(GameObject obj)
        {
            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(obj);
            }

            if (GUILayout.Button("Select", GUILayout.Width(50)))
            {
                Selection.activeGameObject = obj;
            }
        }
        
        #endregion

        #region UI helpers

        private static Texture GetComponentIconOrReturnDefault(Type type)
        {
            GUIContent content = EditorGUIUtility.ObjectContent(null, type);
            return content.image ?? (content.image = EditorGUIUtility.FindTexture("cs Script Icon"));
        }
        
        private string GetUniqueKey(GameObject obj, Component constraint)
        {
            return $"{obj.GetInstanceID()}_{constraint.GetType().FullName}";
        }

        private float GetLabelWidth(string objectName)
        {
            GUIStyle labelStyle = EditorStyles.label;
            float textWidth = labelStyle.CalcSize(new GUIContent(objectName)).x;
            float maxWidth = 200f;
            return Mathf.Min(textWidth, maxWidth);
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

        #region transforms used as source
        private static bool IsNativeConstraintTransformUsedAsSource(IConstraint constraint, Transform targetTransform)
        {
            List<ConstraintSource> sources = new List<ConstraintSource>();
            constraint.GetSources(sources);

            foreach (ConstraintSource source in sources)
            {
                if (source.sourceTransform == targetTransform)
                {
                    return true;
                }
            }

            return false;
        }
#if VRC_SDK_VRCSDK3
        private static bool IsVRCConstraintTransformUsedAsSource(VRCConstraintBase constraint, Transform targetTransform)
        {
            VRCConstraintSourceKeyableList sources = constraint.Sources;

            foreach (VRCConstraintSource source in sources)
            {
                if (source.SourceTransform == targetTransform)
                {
                    return true;
                }
            }
            return false;
        }
#endif
        #endregion
    }
}
