using UnityEditor;
using UnityEngine;

namespace MyrkieUiTweaks
{
    public class UserChoicePatcherUI : EditorWindow
    {
        public static bool BlendShapeSearch = true;
        public static bool ConstraintTransformEditor = true;
        public static bool AllowBlendshapeClamping = true;
        public static bool DebugLogging = true;
        public static bool DisableMouseJumping = true;

        [MenuItem("Tools/Myrkur/UserChoicePatcherUI Patch Settings")]
        public static void ShowWindow()
        {
            GetWindow<UserChoicePatcherUI>("UserChoicePatcherUI Patch Settings");
        }

        private void Awake()
        {
            LoadToggleStates();
        }

        private void OnGUI()
        {
            GUILayout.Label("UserChoicePatcherUI Patch Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            DebugLogging = EditorGUILayout.Toggle("DebugLogging", DebugLogging);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("UserChoicePatcher_DebugLogging", DebugLogging);
            }

            EditorGUI.BeginChangeCheck();
            BlendShapeSearch = EditorGUILayout.Toggle("BlendShapeSearch", BlendShapeSearch);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("UserChoicePatcher_BlendShapeSearch", BlendShapeSearch);
            }

            EditorGUI.BeginChangeCheck();
            ConstraintTransformEditor = EditorGUILayout.Toggle("ConstraintTransformEditor", ConstraintTransformEditor);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("UserChoicePatcher_ConstraintTransformEditor", ConstraintTransformEditor);
            }

            EditorGUI.BeginChangeCheck();
            AllowBlendshapeClamping = EditorGUILayout.Toggle("AllowBlendshapeClamping", AllowBlendshapeClamping);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("UserChoicePatcher_AllowBlendshapeClamping", AllowBlendshapeClamping);
            }
            
            EditorGUI.BeginChangeCheck();
            DisableMouseJumping = EditorGUILayout.Toggle("DisableMouseJumping", DisableMouseJumping);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("UserChoicePatcher_DisableMouseJumping", DisableMouseJumping);
            }

            GUILayout.TextArea(
                "Changing any of these settings will require a restart of the project or reimport of the package.");
        }

        private void OnEnable()
        {
            LoadToggleStates();
        }

        private void LoadToggleStates()
        {
            DebugLogging = EditorPrefs.GetBool("UserChoicePatcher_DebugLogging", true);
            BlendShapeSearch = EditorPrefs.GetBool("UserChoicePatcher_BlendShapeSearch", true);
            ConstraintTransformEditor = EditorPrefs.GetBool("UserChoicePatcher_ConstraintTransformEditor", true);
            AllowBlendshapeClamping = EditorPrefs.GetBool("UserChoicePatcher_AllowBlendshapeClamping", true);
            DisableMouseJumping = EditorPrefs.GetBool("UserChoicePatcher_DisableMouseJumping", true);
        }
    }
}