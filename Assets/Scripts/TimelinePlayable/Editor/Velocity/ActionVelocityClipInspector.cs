using UnityEditor;
using UnityEngine;

/// <summary>
/// 自定义 Inspector：把 VelocityConfig 的字段平铺绘制（不嵌套 foldout），
/// 按语义分组（Direction / Speed / Curve / Gravity / Debug），并根据 directionMode
/// 条件显示 fixedLocalDirection（只在 Fixed 模式下显示）。
/// 
/// 与 ActionImpulseClipInspector 保持风格一致，方便策划在两种 Clip 之间切换时有一致体验。
/// </summary>
[CustomEditor(typeof(ActionVelocityClip))]
public class ActionVelocityClipInspector : Editor
{
    private SerializedProperty _config;
    private SerializedProperty _directionMode;
    private SerializedProperty _fixedLocalDirection;
    private SerializedProperty _horizontalSpeed;
    private SerializedProperty _verticalSpeed;
    private SerializedProperty _horizontalCurve;
    private SerializedProperty _verticalCurve;
    private SerializedProperty _gravityScale;
    private SerializedProperty _debugLog;

    private void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        if (_config == null) return;

        _directionMode       = _config.FindPropertyRelative("directionMode");
        _fixedLocalDirection = _config.FindPropertyRelative("fixedLocalDirection");
        _horizontalSpeed     = _config.FindPropertyRelative("horizontalSpeed");
        _verticalSpeed       = _config.FindPropertyRelative("verticalSpeed");
        _horizontalCurve     = _config.FindPropertyRelative("horizontalCurve");
        _verticalCurve       = _config.FindPropertyRelative("verticalCurve");
        _gravityScale        = _config.FindPropertyRelative("gravityScale");
        _debugLog            = _config.FindPropertyRelative("debugLog");
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
            new GUIContent("Mode", "速度方向来源（与 ImpulseConfig 共用枚举）"));

        if ((MotionDirectionMode)_directionMode.enumValueIndex == MotionDirectionMode.Fixed)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_fixedLocalDirection,
                new GUIContent("Fixed Direction", "本地方向（相对角色朝向）"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ── Speed ──
        EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_horizontalSpeed,
            new GUIContent("Horizontal Speed", "水平速度 (m/s)，沿解析方向。0=不启用水平"));
        EditorGUILayout.PropertyField(_verticalSpeed,
            new GUIContent("Vertical Speed", "垂直速度 (m/s)，正值向上。0=不启用垂直"));

        EditorGUILayout.Space(4);

        // ── Curve ──
        EditorGUILayout.LabelField("Curve (X: 0~1 归一化时间, Y: 速度乘子)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_horizontalCurve,
            new GUIContent("Horizontal Curve", "水平速度的 Clip 期间缩放曲线"));
        EditorGUILayout.PropertyField(_verticalCurve,
            new GUIContent("Vertical Curve", "垂直速度的 Clip 期间缩放曲线"));

        EditorGUILayout.Space(4);

        // ── Gravity ──
        EditorGUILayout.LabelField("Gravity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_gravityScale,
            new GUIContent("Gravity Scale",
                "Clip 期间的重力缩放。0=浮空（由 Clip 接管垂直），1=保留重力叠加"));

        EditorGUILayout.Space(4);

        // ── Options ──
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_debugLog,
            new GUIContent("Debug Log", "打印调试信息到控制台"));

        serializedObject.ApplyModifiedProperties();
    }
}
