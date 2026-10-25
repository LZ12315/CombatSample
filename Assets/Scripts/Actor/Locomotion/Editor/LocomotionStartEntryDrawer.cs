#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LocomotionStartEntry))]
internal sealed class LocomotionStartEntryDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * 3f + EditorGUIUtility.standardVerticalSpacing * 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("animation"), label);
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        var mode = property.FindPropertyRelative("facingMode");
        EditorGUI.PropertyField(position, mode);
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        bool turning = mode.enumValueIndex == (int)LocomotionStartFacingMode.TurnToMove;
        EditorGUI.PropertyField(position, property.FindPropertyRelative(
            turning ? "signedTurnAngleDegrees" : "targetLocalDirection"));
        EditorGUI.EndProperty();
    }
}
#endif
