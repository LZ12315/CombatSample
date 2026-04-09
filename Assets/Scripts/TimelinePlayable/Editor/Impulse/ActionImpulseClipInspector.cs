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
    private SerializedProperty _horizontalDecay;
    private SerializedProperty _verticalForce;
    private SerializedProperty _gravityScale;
    private SerializedProperty _lockFacing;
    private SerializedProperty _debugLog;

    private void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        if (_config == null) return;

        _directionMode = _config.FindPropertyRelative("directionMode");
        _fixedLocalDirection = _config.FindPropertyRelative("fixedLocalDirection");
        _horizontalForce = _config.FindPropertyRelative("horizontalForce");
        _horizontalDecay = _config.FindPropertyRelative("horizontalDecay");
        _verticalForce = _config.FindPropertyRelative("verticalForce");
        _gravityScale = _config.FindPropertyRelative("gravityScale");
        _lockFacing = _config.FindPropertyRelative("lockFacing");
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

        if ((ImpulseDirectionMode)_directionMode.enumValueIndex == ImpulseDirectionMode.Fixed)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_fixedLocalDirection,
                new GUIContent("Fixed Direction", "Local direction relative to actor facing"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Force ──
        EditorGUILayout.LabelField("Force", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_horizontalForce,
            new GUIContent("Horizontal Speed", "Horizontal initial speed (m/s)"));
        EditorGUILayout.PropertyField(_verticalForce,
            new GUIContent("Vertical Speed", "Vertical initial speed (m/s), positive = up"));

        EditorGUILayout.Space(4);

        // ── Decay & Gravity ──
        EditorGUILayout.LabelField("Decay & Gravity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_horizontalDecay,
            new GUIContent("Horizontal Decay", "Force decay curve (X: 0~1 time, Y: multiplier)"));
        EditorGUILayout.PropertyField(_gravityScale,
            new GUIContent("Gravity Scale", "0 = float, 1 = normal, 2 = fast fall"));

        EditorGUILayout.Space(4);

        // ── Options ──
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_lockFacing,
            new GUIContent("Lock Facing", "Lock actor facing during impulse"));
        EditorGUILayout.PropertyField(_debugLog,
            new GUIContent("Debug Log", "Print debug info to console"));

        serializedObject.ApplyModifiedProperties();
    }
}
