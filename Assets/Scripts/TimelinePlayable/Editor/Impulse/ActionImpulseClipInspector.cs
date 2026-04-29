using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for ActionImpulseClip — draws ImpulseConfig fields flat
/// (no nested foldout), groups them logically, and conditionally shows
/// fixedLocalDirection only when directionMode == Fixed.
/// </summary>
[CustomEditor(typeof(ActionImpulseClip))]
public class ActionImpulseClipInspector : Editor
{
    private SerializedProperty _config;
    private SerializedProperty _directionMode;
    private SerializedProperty _fixedLocalDirection;
    private SerializedProperty _horizontalForce;
    private SerializedProperty _verticalForce;
    private SerializedProperty _debugLog;

    private void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        if (_config == null) return;

        _directionMode = _config.FindPropertyRelative("directionMode");
        _fixedLocalDirection = _config.FindPropertyRelative("fixedLocalDirection");
        _horizontalForce = _config.FindPropertyRelative("horizontalForce");
        _verticalForce = _config.FindPropertyRelative("verticalForce");
        _debugLog = _config.FindPropertyRelative("debugLog");
    }

    public override void OnInspectorGUI()
    {
        if (_config == null)
        {
            base.OnInspectorGUI();
            return;
        }

        serializedObject.Update();

        // ── Direction ──
        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_directionMode,
            new GUIContent("Mode", "Direction source mode for this impulse"));

        if ((MotionDirectionMode)_directionMode.enumValueIndex == MotionDirectionMode.Fixed)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_fixedLocalDirection,
                new GUIContent("Fixed Direction", "Local direction relative to actor facing"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Initial Speed ──
        EditorGUILayout.LabelField("Initial Speed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_horizontalForce,
            new GUIContent("Horizontal Speed", "Horizontal initial speed (m/s) along resolved direction. 由 Movement 的 drag 自然衰减"));
        EditorGUILayout.PropertyField(_verticalForce,
            new GUIContent("Vertical Speed", "Vertical initial speed (m/s), positive = up. 由重力自然衰减"));

        EditorGUILayout.Space(4);

        // ── Options ──
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_debugLog,
            new GUIContent("Debug Log", "Print debug info to console"));

        serializedObject.ApplyModifiedProperties();
    }
}
