using System;
using UnityEditor;
using UnityEngine;
using DeiveEx.TagTree;
using UnityEditor.IMGUI.Controls;

namespace DeiveEx.TagTree.Editor
{
    [CustomPropertyDrawer(typeof(TagReference))]
    public class TagReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty tagIdProp = property.FindPropertyRelative("TagId");
            if (tagIdProp == null)
            {
                EditorGUI.LabelField(position, "Error: TagId not found");
                EditorGUI.EndProperty();
                return;
            }

            var current = new TagReference { TagId = tagIdProp.intValue };
            DrawField(position, label, current, null, selected =>
            {
                tagIdProp.intValue = selected.TagId;
                tagIdProp.serializedObject.ApplyModifiedProperties();
            });
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Shared tag picker for SerializedProperty and reflection-based inspectors such as NodeCanvas.
        /// Records the owner's Undo snapshot before mutating an existing reference; null references
        /// are created only on selection and returned through onChanged, never just by viewing.
        /// </summary>
        public static void DrawField(Rect position, GUIContent label, TagReference current,
            Action beforeChange, Action<TagReference> onChanged)
        {
            if (!TagManager.IsInitialized) TagManager.EditorInitialize();
            if (TagManager.Tags.Count == 0) TagManager.LoadTagsFromFiles();

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            const float buttonWidth = 25f;
            var dropdownRect = new Rect(position.x, position.y, Mathf.Max(0f, position.width - buttonWidth - 2f), position.height);
            var editButtonRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);
            int tagId = current != null ? current.TagId : 0;
            string currentName = tagId == 0 ? "None (Click to select)" : $"Missing Tag (ID: {tagId})";
            if (TagManager.Tags.TryGetValue(tagId, out var tag))
            {
                currentName = tag.FullTagName;
            }

            if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(currentName), FocusType.Keyboard))
            {
                var dropdown = new TagTreeSearchDropdown(new AdvancedDropdownState(), selectedTag =>
                {
                    if (current != null && current.TagId == selectedTag.Id)
                        return;
                    beforeChange?.Invoke();
                    var value = current ?? new TagReference();
                    value.TagId = selectedTag.Id;
                    onChanged?.Invoke(value);
                });
                dropdown.SetMinHeight(300);
                dropdown.Show(dropdownRect);
            }

            GUIContent iconContent = EditorGUIUtility.IconContent("d_FilterByLabel@2x");
            iconContent.tooltip = "Open Tag Editor"; 
            
            if (GUI.Button(editButtonRect, iconContent))
            {
                TagTreeEditorWindow.ShowWindow();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}