using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionVelocityClip))]
public class ActionVelocityClipInspector : Editor
{
    private SerializedProperty _config;
    private SerializedProperty _directionMode;
    private SerializedProperty _localHorizontalDirection;
    private SerializedProperty _useHorizontalVelocity;
    private SerializedProperty _horizontalSpeed;
    private SerializedProperty _useVerticalVelocity;
    private SerializedProperty _verticalSpeed;
    private SerializedProperty _horizontalCurve;
    private SerializedProperty _verticalCurve;
    private SerializedProperty _debugLog;

    private void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        if (_config == null)
            return;

        _directionMode = _config.FindPropertyRelative("directionMode");
        _localHorizontalDirection = _config.FindPropertyRelative("localHorizontalDirection");
        _useHorizontalVelocity = _config.FindPropertyRelative("useHorizontalVelocity");
        _horizontalSpeed = _config.FindPropertyRelative("horizontalSpeed");
        _useVerticalVelocity = _config.FindPropertyRelative("useVerticalVelocity");
        _verticalSpeed = _config.FindPropertyRelative("verticalSpeed");
        _horizontalCurve = _config.FindPropertyRelative("horizontalCurve");
        _verticalCurve = _config.FindPropertyRelative("verticalCurve");
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

        EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _useHorizontalVelocity,
            new GUIContent("Use Horizontal", "Take ownership of horizontal velocity, even when speed is zero."));
        EditorGUILayout.PropertyField(
            _horizontalSpeed,
            new GUIContent("Horizontal Speed", "Horizontal velocity (m/s) along the resolved direction."));
        EditorGUILayout.PropertyField(
            _useVerticalVelocity,
            new GUIContent("Use Vertical", "Take ownership of vertical velocity, even when speed is zero."));
        EditorGUILayout.PropertyField(
            _verticalSpeed,
            new GUIContent("Vertical Speed", "Vertical velocity (m/s), positive = up."));

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Curve", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _horizontalCurve,
            new GUIContent("Horizontal Curve", "Horizontal speed multiplier over normalized clip time."));
        EditorGUILayout.PropertyField(
            _verticalCurve,
            new GUIContent("Vertical Curve", "Vertical speed multiplier over normalized clip time."));

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
