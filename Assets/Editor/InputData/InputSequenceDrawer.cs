#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(InputSequence))]
public class InputSequenceDrawer : PropertyDrawer
{
    // 存储每个属性的可重排序列表
    private Dictionary<string, ReorderableList> _reorderableLists = new Dictionary<string, ReorderableList>();

    // 存储每个属性的展开状态
    private Dictionary<string, bool> _expandedStates = new Dictionary<string, bool>();

    // 单行高度
    private float LineHeight => EditorGUIUtility.singleLineHeight;

    // 垂直间距
    private float VerticalSpacing => EditorGUIUtility.standardVerticalSpacing;

    // 暗色主题下的背景色
    private Color DarkThemeBgColor => new Color(0.22f, 0.22f, 0.22f, 0.7f);

    // 亮色主题下的背景色
    private Color LightThemeBgColor => new Color(0.92f, 0.92f, 0.92f, 0.7f);

    // 暗色主题下的元素背景色（奇数行）
    private Color DarkThemeElementBgColor1 => new Color(0.25f, 0.25f, 0.25f, 0.7f);

    // 暗色主题下的元素背景色（偶数行）
    private Color DarkThemeElementBgColor2 => new Color(0.28f, 0.28f, 0.28f, 0.7f);

    // 亮色主题下的元素背景色（奇数行）
    private Color LightThemeElementBgColor1 => new Color(0.94f, 0.94f, 0.94f, 0.7f);

    // 亮色主题下的元素背景色（偶数行）
    private Color LightThemeElementBgColor2 => new Color(0.92f, 0.92f, 0.92f, 0.7f);

    /// <summary>
    /// 绘制属性 GUI
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 确保属性对象是最新的
        property.serializedObject.Update();

        // 获取当前属性的唯一键（基于属性路径）
        string key = property.propertyPath;

        // 初始化展开状态（如果不存在）
        if (!_expandedStates.ContainsKey(key))
        {
            _expandedStates[key] = false;
        }

        // 绘制折叠标题
        var labelRect = new Rect(position.x, position.y, position.width, LineHeight);
        _expandedStates[key] = EditorGUI.Foldout(labelRect, _expandedStates[key], label);

        // 如果属性是展开状态
        if (_expandedStates[key])
        {
            // 计算内容区域位置（从标题下方开始）
            var contentRect = new Rect(
                position.x,
                position.y + LineHeight + VerticalSpacing,
                position.width,
                position.height - LineHeight - VerticalSpacing
            );

            // 设置背景色（根据当前主题）
            Color bgColor = EditorGUIUtility.isProSkin ? DarkThemeBgColor : LightThemeBgColor;

            // 绘制背景
            EditorGUI.DrawRect(contentRect, bgColor);

            // 开始属性绘制
            EditorGUI.BeginProperty(contentRect, label, property);

            // 添加内边距和缩进
            var innerRect = new Rect(
                contentRect.x + 10,
                contentRect.y + 8,
                contentRect.width - 20,
                contentRect.height - 16
            );

            // 保存原始缩进级别
            int originalIndent = EditorGUI.indentLevel;

            // 增加缩进级别
            EditorGUI.indentLevel = 1;

            // 获取子属性
            var waitTimeProp = property.FindPropertyRelative("waitTime");
            var dataChecksProp = property.FindPropertyRelative("dataChecks");

            // 绘制 FrameDelay 属性
            var waitTimeRect = new Rect(innerRect.x, innerRect.y, innerRect.width, LineHeight);
            EditorGUI.PropertyField(waitTimeRect, waitTimeProp, new GUIContent("Frame Delay"));

            // 移动到下一个位置（FrameDelay下方）
            innerRect.y += LineHeight + VerticalSpacing * 3;

            // 绘制 Data Checks 列表标题
            var dataChecksLabelRect = new Rect(innerRect.x, innerRect.y, innerRect.width, LineHeight);
            EditorGUI.LabelField(dataChecksLabelRect, "Data Checks", EditorStyles.boldLabel);
            innerRect.y += LineHeight + VerticalSpacing;

            // 绘制 Data Checks 列表
            var listRect = new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height);
            DrawDataChecksList(listRect, dataChecksProp);

            // 恢复原始缩进级别
            EditorGUI.indentLevel = originalIndent;

            // 结束属性绘制
            EditorGUI.EndProperty();
        }

        // 应用修改
        property.serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 绘制 Data Checks 列表
    /// </summary>
    private void DrawDataChecksList(Rect position, SerializedProperty listProperty)
    {
        // 获取或创建 ReorderableList
        string key = listProperty.propertyPath;
        if (!_reorderableLists.TryGetValue(key, out ReorderableList list))
        {
            list = CreateReorderableList(listProperty);
            _reorderableLists[key] = list;
        }

        // 禁用列表头部（因为我们手动绘制了标题）
        list.headerHeight = 0;

        // 绘制列表
        list.DoList(position);
    }

    /// <summary>
    /// 创建可重排序列表
    /// </summary>
    private ReorderableList CreateReorderableList(SerializedProperty listProperty)
    {
        // 创建新的 ReorderableList
        var list = new ReorderableList(
            listProperty.serializedObject,
            listProperty,
            true,  // 可拖动
            false, // 不显示头部
            true,  // 显示添加按钮
            true   // 显示删除按钮
        );

        // 设置元素高度回调
        list.elementHeightCallback = (int index) =>
        {
            var element = listProperty.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true);
        };

        // 设置元素绘制回调
        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = listProperty.GetArrayElementAtIndex(index);

            // 获取类型名称
            var dataCheckProp = element.FindPropertyRelative("dataCheck");
            string typeName = GetTypeName(dataCheckProp.managedReferenceValue);

            // 绘制类型标签
            var typeRect = new Rect(rect.x, rect.y, 60, LineHeight);
            EditorGUI.LabelField(typeRect, typeName);

            // 绘制属性
            var propRect = new Rect(rect.x + 65, rect.y, rect.width - 65, rect.height);
            EditorGUI.PropertyField(propRect, element, GUIContent.none, false);
        };

        // 添加元素时的回调
        list.onAddCallback = (ReorderableList l) =>
        {
            int index = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            l.index = index;

            var element = l.serializedProperty.GetArrayElementAtIndex(index);
            var dataCheckProp = element.FindPropertyRelative("dataCheck");

            // 创建新的实例（默认按钮检查设置）
            dataCheckProp.managedReferenceValue = new ButtonInputCondition();

            // 应用修改
            element.serializedObject.ApplyModifiedProperties();

            // 强制重绘
            EditorWindow.focusedWindow?.Repaint();
        };

        // 设置元素背景色
        list.drawElementBackgroundCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (Event.current.type == EventType.Repaint)
            {
                // 根据主题和行号选择背景色
                Color bgColor;
                if (EditorGUIUtility.isProSkin)
                {
                    bgColor = index % 2 == 0 ? DarkThemeElementBgColor1 : DarkThemeElementBgColor2;
                }
                else
                {
                    bgColor = index % 2 == 0 ? LightThemeElementBgColor1 : LightThemeElementBgColor2;
                }

                EditorGUI.DrawRect(rect, bgColor);
            }
        };

        return list;
    }

    /// <summary>
    /// 获取类型名称
    /// </summary>
    private string GetTypeName(object value)
    {
        if (value is ButtonInputCondition) return "Button";
        if (value is JoystickInputCondition) return "Joystick";
        return "Unknown";
    }

    /// <summary>
    /// 计算属性高度
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string key = property.propertyPath;

        // 如果属性未展开，只返回标题高度
        if (!_expandedStates.ContainsKey(key) || !_expandedStates[key])
            return LineHeight;

        float height = LineHeight; // 标题高度

        // 添加 FrameDelay 高度
        height += LineHeight;

        // 添加 FrameDelay 和 Data Checks 之间的间距
        height += VerticalSpacing * 3;

        // 添加 Data Checks 标题高度
        height += LineHeight;

        // 添加 Data Checks 标题和列表之间的间距
        height += VerticalSpacing;

        // 添加 Data Checks 列表高度
        var dataChecksProp = property.FindPropertyRelative("dataChecks");

        if (_reorderableLists.TryGetValue(dataChecksProp.propertyPath, out ReorderableList list))
        {
            height += list.GetHeight();
        }
        else
        {
            height += EditorGUI.GetPropertyHeight(dataChecksProp, true);
        }

        // 添加内边距
        height += 16;

        return height;
    }
}
#endif