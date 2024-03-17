using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Collections.Generic;

[CustomEditor(typeof(Transform), true)]
[CanEditMultipleObjects]
public class TransformUsageCheckerExtension : Editor
{
    Editor defaultEditor;
    private Transform _transform;
    void OnEnable(){
        defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
        _transform = target as Transform;
 
    }
    
    public override void OnInspectorGUI(){
        defaultEditor.OnInspectorGUI();
        
        if (_transform == null) return;
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
                    if (GUILayout.Button("Constraint usage: " + obj.name + " - " + constraint.GetType().Name))
                    {
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            }
        }
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
