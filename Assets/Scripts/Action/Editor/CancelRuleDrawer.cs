#if UNITY_EDITOR
using DeiveEx.TagTree;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CancelRule))]
public sealed class CancelRuleDrawer : PropertyDrawer
{
    private const float ContentInset = 8f;
    private readonly ActionConditionListDrawers _conditionLists = new ActionConditionListDrawers();
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        height += ChildHeight(property.FindPropertyRelative(nameof(CancelRule.targetKind)));
        height += ChildHeight(TargetProperty(property));
        height += ChildHeight(property.FindPropertyRelative(nameof(CancelRule.window)));
        SerializedProperty conditions = property.FindPropertyRelative(nameof(CancelRule.conditions));
        height += EditorGUIUtility.standardVerticalSpacing + _conditionLists.For(conditions).GetHeight(conditions);
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        bool hierarchy = EditorGUIUtility.hierarchyMode;
        float labelWidth = EditorGUIUtility.labelWidth;
        try
        {
            position = EditorGUI.IndentedRect(position);
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.hierarchyMode = false;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, HeaderLabel(property, label), true);
            if (!property.isExpanded)
                return;

            // One fixed inset, independent of Unity's array depth and hierarchy mode.
            row.xMin += ContentInset;
            // Compensate for the content inset instead of recalculating a new label column.
            EditorGUIUtility.labelWidth = Mathf.Max(1f, labelWidth - ContentInset);
            DrawChild(ref row, property.FindPropertyRelative(nameof(CancelRule.targetKind)), "Target Kind");
            // Only draw the active target; switching kinds does not erase hidden configuration.
            SerializedProperty target = TargetProperty(property);
            if (target != null)
                DrawChild(ref row, target, target.displayName);
            DrawChild(ref row, property.FindPropertyRelative(nameof(CancelRule.window)), "Window");
            SerializedProperty conditions = property.FindPropertyRelative(nameof(CancelRule.conditions));
            ActionAuthoringListGUI list = _conditionLists.For(conditions);
            row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
            // Indent the whole child list, not just its header; keep the right edge unchanged.
            row.xMin = position.x + ContentInset;
            row.height = list.GetHeight(conditions);
            list.Draw(row, conditions, "Conditions");
        }
        finally
        {
            EditorGUI.indentLevel = indent;
            EditorGUIUtility.hierarchyMode = hierarchy;
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Collapsed row shows only the destination: Action name, Tag, Any or Locomotion.</summary>
    private static GUIContent HeaderLabel(SerializedProperty property, GUIContent fallback)
    {
        SerializedProperty kind = property.FindPropertyRelative(nameof(CancelRule.targetKind));
        if (kind == null || kind.hasMultipleDifferentValues)
            return fallback;

        var targetKind = (CancelTargetKind)kind.intValue;
        string title = ObjectNames.NicifyVariableName(targetKind.ToString());
        switch (targetKind)
        {
            case CancelTargetKind.SpecificAction:
                UnityEngine.Object target = property.FindPropertyRelative(nameof(CancelRule.specificTarget)).objectReferenceValue;
                if (target != null)
                    title = target.name;
                break;
            case CancelTargetKind.AnyWithTag:
                SerializedProperty tagId = property.FindPropertyRelative(nameof(CancelRule.targetTag))
                    ?.FindPropertyRelative("TagId");
                Tag tag = tagId != null ? Tag.GetTagFromId(tagId.intValue) : null;
                if (tag != null && !string.IsNullOrEmpty(tag.FullTagName))
                    title = tag.FullTagName;
                break;
        }
        return new GUIContent(title, property.tooltip);
    }

    private static SerializedProperty TargetProperty(SerializedProperty property)
    {
        SerializedProperty kind = property.FindPropertyRelative(nameof(CancelRule.targetKind));
        if (kind == null || kind.hasMultipleDifferentValues)
            return null;

        switch ((CancelTargetKind)kind.intValue)
        {
            case CancelTargetKind.SpecificAction:
                return property.FindPropertyRelative(nameof(CancelRule.specificTarget));
            case CancelTargetKind.AnyWithTag:
                return property.FindPropertyRelative(nameof(CancelRule.targetTag));
            case CancelTargetKind.Locomotion:
                return property.FindPropertyRelative(nameof(CancelRule.locomotionIntentThreshold));
            default:
                return null;
        }
    }

    private static float ChildHeight(SerializedProperty property) => property == null ? 0f
        : EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(property, true);

    private static void DrawChild(ref Rect row, SerializedProperty property, string label)
    {
        if (property == null)
            return;

        row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
        row.height = EditorGUI.GetPropertyHeight(property, true);
        EditorGUI.PropertyField(row, property, new GUIContent(label, property.tooltip), true);
    }
}
#endif
