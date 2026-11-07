#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LocomotionStartEntry))]
internal sealed class LocomotionStartEntryDrawer : PropertyDrawer
{
    private const int Rows = 3;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * Rows + EditorGUIUtility.standardVerticalSpacing * (Rows - 1);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        var row = new Rect(position.x, position.y, position.width, line);

        EditorGUI.PropertyField(row, property.FindPropertyRelative("animation"), label);

        row.y += line + spacing;
        var mode = property.FindPropertyRelative("facingMode");
        DrawLabelled(row, mode, "Facing Mode");

        row.y += line + spacing;
        bool turning = mode.enumValueIndex == (int)LocomotionStartFacingMode.TurnToMove;
        if (turning)
        {
            DrawLabelled(row, property.FindPropertyRelative("signedTurnAngleDegrees"), "Signed Turn Angle Degrees");
        }
        else
        {
            var direction = property.FindPropertyRelative("targetLocalDirection");
            var (labelRect, fieldRect) = Split(row);
            EditorGUI.LabelField(labelRect, "Target Local Direction");
            EditorGUI.BeginChangeCheck();
            // Draw both components in one row ourselves: the multi-field helper can decide to stack
            // them on a second line when it judges the row narrow, which escapes the element height.
            Vector2 value = EditorGUI.Vector2Field(fieldRect, GUIContent.none, direction.vector2Value);
            if (EditorGUI.EndChangeCheck()) direction.vector2Value = value;
        }
        EditorGUI.EndProperty();
    }

    private static void DrawLabelled(Rect row, SerializedProperty property, string label)
    {
        var (labelRect, fieldRect) = Split(row);
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.PropertyField(fieldRect, property, GUIContent.none);
    }

    private static (Rect Label, Rect Field) Split(Rect row)
    {
        float labelWidth = EditorGUIUtility.labelWidth;
        return (new Rect(row.x, row.y, labelWidth, row.height),
            new Rect(row.x + labelWidth, row.y, Mathf.Max(0f, row.width - labelWidth), row.height));
    }
}
#endif
