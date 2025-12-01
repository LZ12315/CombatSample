#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(ActionTransition))]
public class RobustConditionListDrawer : PropertyDrawer
{
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 使用 Begin/EndProperty 确保撤销和预设覆盖正常工作
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty conditionsProp = property.FindPropertyRelative("conditions");

        // 创建唯一ID用于折叠状态
        string propertyId = property.propertyPath;

        // 绘制标题行
        Rect headerRect = new Rect(position.x, position.y, position.width, 20);
        DrawHeader(headerRect, conditionsProp, propertyId);

        // 绘制条件列表
        if (IsExpanded(propertyId))
        {
            Rect listRect = new Rect(position.x, position.y + 25, position.width, position.height - 25);
            DrawConditionList(listRect, conditionsProp);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty conditionsProp = property.FindPropertyRelative("conditions");
        string propertyId = property.propertyPath;

        float height = 25; // 标题高度

        if (IsExpanded(propertyId))
        {
            height += 5; // 上边距

            for (int i = 0; i < conditionsProp.arraySize; i++)
            {
                SerializedProperty conditionProp = conditionsProp.GetArrayElementAtIndex(i);
                height += GetConditionHeight(conditionProp) + 5; // 条件高度 + 间距
            }

            height += 25; // 添加按钮区域
        }

        return height;
    }

    private void DrawHeader(Rect position, SerializedProperty conditionsProp, string propertyId)
    {
        Rect bgRect = new Rect(position.x - 3, position.y, position.width + 6, 20);
        EditorGUI.DrawRect(bgRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

        // 折叠箭头
        Rect foldoutRect = new Rect(position.x, position.y, 20, 20);
        bool isExpanded = EditorGUI.Foldout(foldoutRect, IsExpanded(propertyId), "", true);
        SetExpanded(propertyId, isExpanded);

        // 标题
        Rect labelRect = new Rect(position.x + 20, position.y, 100, 20);
        EditorGUI.LabelField(labelRect, $"条件列表 ({conditionsProp.arraySize})", EditorStyles.boldLabel);

        // 添加按钮
        if (isExpanded)
        {
            Rect addButtonRect = new Rect(position.xMax - 120, position.y, 120, 20);
            if (GUI.Button(addButtonRect, "添加新条件"))
            {
                ShowAddConditionMenu(conditionsProp);
            }
        }
    }

    private void DrawConditionList(Rect position, SerializedProperty conditionsProp)
    {
        float yOffset = 0;
        int deleteIndex = -1;

        for (int i = 0; i < conditionsProp.arraySize; i++)
        {
            SerializedProperty conditionProp = conditionsProp.GetArrayElementAtIndex(i);
            float conditionHeight = GetConditionHeight(conditionProp);

            Rect conditionRect = new Rect(position.x, position.y + yOffset, position.width, conditionHeight);

            if (DrawCondition(conditionRect, conditionProp, i, ref deleteIndex))
            {
                break; // 如果删除了元素，立即重绘
            }

            yOffset += conditionHeight + 5;
        }

        // 执行删除操作
        if (deleteIndex >= 0)
        {
            conditionsProp.DeleteArrayElementAtIndex(deleteIndex);
            conditionsProp.serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI(); // 立即退出当前GUI绘制
        }

        // 空列表提示
        if (conditionsProp.arraySize == 0)
        {
            Rect helpRect = new Rect(position.x, position.y + yOffset, position.width, 40);
            EditorGUI.HelpBox(helpRect, "点击\"添加新条件\"来创建第一个条件", MessageType.Info);
        }
    }

    private bool DrawCondition(Rect position, SerializedProperty conditionProp, int index, ref int deleteIndex)
    {
        Rect bgRect = new Rect(position.x - 2, position.y, position.width + 4, position.height);
        EditorGUI.DrawRect(bgRect, new Color(0.15f, 0.15f, 0.15f, 0.2f));

        // 删除按钮（左上角，避免与内容冲突）
        Rect deleteRect = new Rect(position.x + 5, position.y + 3, 50, 16);
        GUIContent deleteContent = new GUIContent("× 删除", "删除此条件");

        // 修改：直接删除，无需确认对话框
        if (GUI.Button(deleteRect, deleteContent, EditorStyles.miniButton))
        {
            deleteIndex = index;
            return true; // 标记需要删除
        }

        // 条件属性（为删除按钮留出空间）
        Rect contentRect = new Rect(
            position.x + 60,
            position.y,
            position.width - 65,
            position.height
        );

        EditorGUI.PropertyField(contentRect, conditionProp, GUIContent.none, true);

        return false;
    }

    private float GetConditionHeight(SerializedProperty conditionProp)
    {
        return EditorGUI.GetPropertyHeight(conditionProp, true) + 6; // 额外边距
    }

    private bool IsExpanded(string propertyId)
    {
        return foldoutStates.ContainsKey(propertyId) ? foldoutStates[propertyId] : true;
    }

    private void SetExpanded(string propertyId, bool expanded)
    {
        foldoutStates[propertyId] = expanded;
    }

    private void ShowAddConditionMenu(SerializedProperty conditionsProp)
    {
        GenericMenu menu = new GenericMenu();
        var availableTypes = ConditionTypeRegistry.GetAvailableConditionTypes();

        if (availableTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("未找到条件类型"));
        }
        else
        {
            foreach (string typeName in availableTypes)
            {
                menu.AddItem(new GUIContent(typeName), false, () =>
                {
                    AddCondition(conditionsProp, typeName);
                });
            }
        }

        menu.ShowAsContext();
    }

    private void AddCondition(SerializedProperty conditionsProp, string typeName)
    {
        conditionsProp.arraySize++;
        SerializedProperty newElement = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);

        var newCondition = ConditionTypeRegistry.CreateInstance(typeName);
        if (newCondition != null)
        {
            newElement.managedReferenceValue = newCondition;
            conditionsProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif