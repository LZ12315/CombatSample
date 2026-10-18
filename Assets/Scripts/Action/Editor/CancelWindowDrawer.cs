#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>A compact cancellation range, always visible without a nested foldout.</summary>
[CustomPropertyDrawer(typeof(CancelWindow))]
public sealed class CancelWindowDrawer : PropertyDrawer
{
    private const float StackedViewWidth = 480f;
    private const float AnchorWidth = 80f;
    private const float FrameWidth = 52f;
    private const float Gap = 4f;
    private const float SeparatorWidth = 18f;

    // Use the same view breakpoint for measurement and drawing. PropertyDrawer height
    // queries do not receive a Rect, and must not depend on a previous item's width.
    private static bool IsStacked => EditorGUIUtility.currentViewWidth < StackedViewWidth;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * (IsStacked ? 2f : 1f)
        + (IsStacked ? EditorGUIUtility.standardVerticalSpacing : 0f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using (new EditorGUI.PropertyScope(position, label, property))
        {
            int indent = EditorGUI.indentLevel;
            try
            {
                position = EditorGUI.IndentedRect(position);
                EditorGUI.indentLevel = 0;
                var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                Rect controls = EditorGUI.PrefixLabel(row, label);
                var startAnchor = property.FindPropertyRelative(nameof(CancelWindow.startAnchor));
                var startFrame = property.FindPropertyRelative(nameof(CancelWindow.startFrame));
                var endAnchor = property.FindPropertyRelative(nameof(CancelWindow.endAnchor));
                var endFrame = property.FindPropertyRelative(nameof(CancelWindow.endFrame));

                if (IsStacked)
                {
                    DrawStackedEndpoint(controls, startAnchor, startFrame, "From");
                    controls.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    DrawStackedEndpoint(controls, endAnchor, endFrame, "To");
                }
                else
                {
                    // Keep anchor menus compact instead of stretching them across the Inspector.
                    float width = Mathf.Min(AnchorWidth + Gap + FrameWidth,
                        Mathf.Max(0f, (controls.width - SeparatorWidth) * 0.5f));
                    var endpoint = new Rect(controls.x, controls.y, width, controls.height);
                    DrawEndpoint(endpoint, startAnchor, startFrame, "From");
                    var separator = new Rect(endpoint.xMax, endpoint.y, SeparatorWidth, endpoint.height);
                    EditorGUI.LabelField(separator, new GUIContent("–", "Cancellation range, inclusive."), EditorStyles.centeredGreyMiniLabel);
                    endpoint.x = separator.xMax;
                    DrawEndpoint(endpoint, endAnchor, endFrame, "To");
                }
            }
            finally
            {
                EditorGUI.indentLevel = indent;
            }
        }
    }

    private static void DrawStackedEndpoint(Rect row, SerializedProperty anchor, SerializedProperty frame, string title)
    {
        var label = new Rect(row.x, row.y, Mathf.Min(30f, row.width * 0.2f), row.height);
        EditorGUI.LabelField(label, title, EditorStyles.miniLabel);
        row.xMin = Mathf.Min(row.xMax, label.xMax + Gap);
        DrawEndpoint(row, anchor, frame, title);
    }

    private static void DrawEndpoint(Rect row, SerializedProperty anchor, SerializedProperty frame, string title)
    {
        // Reserve the numeric slot even when hidden so changing anchors does not shift the endpoints.
        float frameWidth = Mathf.Min(FrameWidth, row.width * 0.4f);
        float anchorWidth = Mathf.Min(AnchorWidth, Mathf.Max(0f, row.width - frameWidth - Gap));
        var anchorRect = new Rect(row.x, row.y, anchorWidth, row.height);
        EditorGUI.PropertyField(anchorRect, anchor, new GUIContent(string.Empty, title + " anchor"));

        // Draw the stored frame only for Custom (or mixed selections). Never erase hidden values.
        if (anchor.hasMultipleDifferentValues || anchor.intValue == (int)FrameAnchor.Custom)
        {
            var frameRect = new Rect(anchorRect.xMax + Gap, row.y, frameWidth, row.height);
            EditorGUI.PropertyField(frameRect, frame, new GUIContent(string.Empty, title + " frame (inclusive)"));
        }
    }
}
#endif
