#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ActionAssetList))]
public sealed class ActionAssetListInspector : Editor
{
    private ActionAuthoringListGUI _entries;

    public override VisualElement CreateInspectorGUI()
    {
        _entries = new ActionAuthoringListGUI(typeof(ActionListEntry));
        var root = new VisualElement();
        ActionEditorTheme.Apply(root, "action-editor-asset-inspector");
        // The native list already supplies a border and an Actions header; no second card/title.
        root.Add(new IMGUIContainer(() =>
        {
            serializedObject.UpdateIfRequiredOrScript();
            _entries.Draw(serializedObject.FindProperty("_entries"), "Actions");
            serializedObject.ApplyModifiedProperties();
        }));
        Undo.undoRedoPerformed -= OnUndoRedo;
        Undo.undoRedoPerformed += OnUndoRedo;
        return root;
    }

    private void OnUndoRedo()
    {
        _entries?.Reset();
        Repaint();
    }

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
}

[CustomPropertyDrawer(typeof(ActionListEntry))]
public sealed class ActionListEntryDrawer : PropertyDrawer
{
    private readonly ActionConditionListDrawers _conditionLists = new ActionConditionListDrawers();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;
        SerializedProperty conditions = property.FindPropertyRelative(nameof(ActionListEntry.conditions));
        return height * 2f + EditorGUIUtility.standardVerticalSpacing * 2f +
               _conditionLists.For(conditions).GetHeight(conditions);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using (new EditorGUI.PropertyScope(position, label, property))
        {
            int indent = EditorGUI.indentLevel;
            bool hierarchy = EditorGUIUtility.hierarchyMode;
            float labelWidth = EditorGUIUtility.labelWidth;
            try
            {
                position = EditorGUI.IndentedRect(position);
                EditorGUI.indentLevel = 0;
                EditorGUIUtility.hierarchyMode = false;
                var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                var foldout = row;
                foldout.width = 16f;
                property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, GUIContent.none);
                row.xMin += 20f;
                EditorGUI.PropertyField(row, property.FindPropertyRelative(nameof(ActionListEntry.action)), GUIContent.none);
                if (!property.isExpanded) return;

                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                EditorGUIUtility.labelWidth = Mathf.Max(1f, labelWidth - 20f);
                SerializedProperty neutral = property.FindPropertyRelative(nameof(ActionListEntry.allowFromNeutral));
                EditorGUI.PropertyField(row, neutral, new GUIContent("Allow From Neutral", neutral.tooltip));
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                // Match CancelRule's child-list hierarchy without shifting its right edge.
                row.xMin = position.x + ActionConditionListDrawers.NestedInset;
                EditorGUIUtility.labelWidth = labelWidth;
                SerializedProperty conditions = property.FindPropertyRelative(nameof(ActionListEntry.conditions));
                ActionAuthoringListGUI list = _conditionLists.For(conditions);
                row.height = list.GetHeight(conditions);
                // Keep inactive configuration visible and intact when switching this entry to combo-only.
                using (new EditorGUI.DisabledScope(!neutral.boolValue))
                    list.Draw(row, conditions, "Conditions");
            }
            finally
            {
                EditorGUI.indentLevel = indent;
                EditorGUIUtility.hierarchyMode = hierarchy;
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }
    }
}
#endif
