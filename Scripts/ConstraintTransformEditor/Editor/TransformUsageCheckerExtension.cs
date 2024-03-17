using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Collections.Generic;

namespace MyrkieUiTweaks
{
    [CustomEditor(typeof(Transform), true)]
    [CanEditMultipleObjects]
    public class TransformUsageCheckerExtension : Editor
    {
        Editor defaultEditor;
        private Transform _transform;

        void OnEnable()
        {
            defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
            _transform = target as Transform;
        }

        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();

            if (!UserChoicePatcherUI.ConstraintTransformEditor) return;
            if (_transform == null) return;
            DrawHorizontalGUILine();
            CheckConstraintUsage(_transform);
        }

        private void CheckConstraintUsage(Transform targetTransform)
        {
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (!obj.scene.IsValid())
                    continue;

                IConstraint[] constraints = obj.GetComponents<IConstraint>();

                foreach (IConstraint constraint in constraints)
                {
                    if (IsTransformUsedAsSource(constraint, targetTransform))
                    {
                        if (GUILayout.Button("Uses: " + obj.name + " | " + constraint.GetType().Name))
                        {
                            EditorGUIUtility.PingObject(obj);
                        }
                    }
                }
            }
        }

        // https://forum.unity.com/threads/horizontal-line-in-editor-window.520812/#post-8551211
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


        private static bool IsTransformUsedAsSource(IConstraint constraint, Transform targetTransform)
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
    }
}