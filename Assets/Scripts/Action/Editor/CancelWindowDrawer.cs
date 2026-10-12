#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>A cancellation range is always visible inside its rule, without a nested foldout.</summary>
[CustomPropertyDrawer(typeof(CancelWindow))]
public sealed class CancelWindowDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * 3f + EditorGUIUtility.standardVerticalSpacing * 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using (new EditorGUI.PropertyScope(position, label, property))
        {
            int indent = EditorGUI.indentLevel;
            try
            {
                position = EditorGUI.IndentedRect(position);
                EditorGUI.indentLevel = 0;
                Rect row = position;
                row.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(row, label);
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                DrawEndpoint(row, property.FindPropertyRelative(nameof(CancelWindow.startAnchor)),
                    property.FindPropertyRelative(nameof(CancelWindow.startFrame)), "From");
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                DrawEndpoint(row, property.FindPropertyRelative(nameof(CancelWindow.endAnchor)),
                    property.FindPropertyRelative(nameof(CancelWindow.endFrame)), "To");
            }
            finally
            {
                EditorGUI.indentLevel = indent;
            }
        }
    }

    private static void DrawEndpoint(Rect row, SerializedProperty anchor, SerializedProperty frame, string title)
    {
        // Reserve the frame input only for Custom; switching anchors never erases stored frame values.
        bool showFrame = anchor.hasMultipleDifferentValues || anchor.intValue == (int)FrameAnchor.Custom;
        Rect anchorRect = row;
        Rect frameRect = row;
        if (showFrame)
        {
            frameRect.xMin = frameRect.xMax - Mathf.Min(60f, row.width * 0.25f);
            anchorRect.xMax = frameRect.xMin - EditorGUIUtility.standardVerticalSpacing;
        }
        EditorGUI.PropertyField(anchorRect, anchor, new GUIContent(title, anchor.tooltip));
        if (showFrame)
            EditorGUI.PropertyField(frameRect, frame, GUIContent.none);
    }
}
#endif
