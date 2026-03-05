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
            if (!TagManager.IsInitialized) TagManager.EditorInitialize();
            if (TagManager.Tags.Count == 0) TagManager.LoadTagsFromFiles();

            EditorGUI.BeginProperty(position, label, property);

            // 1. 绘制左侧的 Label，并获取剩余的可用空间
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // ==========================================
            // 🌟 核心魔法：空间切割
            // 我们把剩余的空间切成两块：左边给下拉菜单，右边留 25 像素给小按钮
            // ==========================================
            float buttonWidth = 25f;
            Rect dropdownRect = new Rect(position.x, position.y, position.width - buttonWidth - 2f, position.height);
            Rect editButtonRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            SerializedProperty tagIdProp = property.FindPropertyRelative("TagId");
            if (tagIdProp == null)
            {
                EditorGUI.LabelField(position, "Error: TagId not found");
                EditorGUI.EndProperty();
                return;
            }

            string currentName = "None (Click to select)";
            if (TagManager.Tags.TryGetValue(tagIdProp.intValue, out var tag))
            {
                currentName = tag.FullTagName;
            }

            // 2. 在左侧区域绘制下拉菜单
            if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(currentName), FocusType.Keyboard))
            {
                var dropdown = new TagTreeSearchDropdown(new AdvancedDropdownState(), selectedTag =>
                {
                    tagIdProp.intValue = selectedTag.Id;
                    tagIdProp.serializedObject.ApplyModifiedProperties();
                });
                dropdown.SetMinHeight(300);
                dropdown.Show(dropdownRect); // 菜单现在对齐到左侧的矩形
            }

            // 3. 在右侧区域绘制那个“小标签按钮”
            // 提取 Unity 内置的标签图标 (这就和你原版用的一模一样)
            GUIContent iconContent = EditorGUIUtility.IconContent("d_FilterByLabel@2x");
            iconContent.tooltip = "Open Tag Editor"; 
            
            // 点击按钮时，呼出原版的 Tag 编辑器窗口
            if (GUI.Button(editButtonRect, iconContent))
            {
                TagTreeEditorWindow.ShowWindow();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}