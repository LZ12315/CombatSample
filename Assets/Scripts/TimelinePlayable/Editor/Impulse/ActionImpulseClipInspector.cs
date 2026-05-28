using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionImpulseClip))]
public class ActionImpulseClipInspector : Editor
{
    private SerializedProperty _config;
    private SerializedProperty _directionMode;
    private SerializedProperty _localHorizontalDirection;
    private SerializedProperty _horizontalForce;
    private SerializedProperty _verticalForce;
    private SerializedProperty _debugLog;

    private void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        if (_config == null)
            return;

        _directionMode = _config.FindPropertyRelative("directionMode");
        _localHorizontalDirection = _config.FindPropertyRelative("localHorizontalDirection");
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

        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_directionMode, new GUIContent("Mode", "Horizontal direction source."));

        if (IsLocalHorizontalMode())
            DrawLocalHorizontalDirection();

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Initial Speed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _horizontalForce,
            new GUIContent("Horizontal Speed", "Horizontal initial speed (m/s) along the resolved direction."));
        EditorGUILayout.PropertyField(
            _verticalForce,
            new GUIContent("Vertical Speed", "Vertical initial speed (m/s), positive = up."));

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_debugLog, new GUIContent("Debug Log", "Print debug info to console."));

        serializedObject.ApplyModifiedProperties();
    }

    private bool IsLocalHorizontalMode()
    {
        return _directionMode.intValue == (int)MotionDirectionMode.LocalHorizontal;
    }

    private void DrawLocalHorizontalDirection()
    {
        Vector3 direction = _localHorizontalDirection.vector3Value;

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        float right = EditorGUILayout.FloatField(
            new GUIContent("Right (X)", "Actor-local right component."),
            direction.x);
        float forward = EditorGUILayout.FloatField(
            new GUIContent("Forward (Z)", "Actor-local forward component."),
            direction.z);
        if (EditorGUI.EndChangeCheck() || Mathf.Abs(direction.y) > 0.0001f)
            _localHorizontalDirection.vector3Value = new Vector3(right, 0f, forward);
        EditorGUI.indentLevel--;
    }
}
