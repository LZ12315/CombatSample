#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[CustomPropertyDrawer(typeof(InputButtonDataCheck))]
public class InputButtonDataCheckDrawer : PropertyDrawer
{
    public static Dictionary<string, bool> dropdownStates = new Dictionary<string, bool>();
    public static Dictionary<string, Rect> dropdownRects = new Dictionary<string, Rect>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 获取属性
        var inputButtonsProp = property.FindPropertyRelative("inputButtons");
        var inputStateProp = property.FindPropertyRelative("inputState");

        // 初始位置
        float y = position.y;

        // 绘制按钮选择下拉菜单
        var buttonsRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        y += DrawMultiSelectEnum(buttonsRect, inputButtonsProp, "Input Buttons", typeof(Enums.InputButton));
        y += EditorGUIUtility.standardVerticalSpacing;

        // 绘制状态选择下拉菜单
        var statesRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        y += DrawMultiSelectEnum(statesRect, inputStateProp, "Input States", typeof(Enums.ButtonState));

        EditorGUI.EndProperty();
    }

    private float DrawMultiSelectEnum(Rect position, SerializedProperty listProp, string label, System.Type enumType)
    {
        // 获取当前选中的值
        var currentValues = new List<int>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            currentValues.Add(listProp.GetArrayElementAtIndex(i).intValue);
        }

        // 创建选项列表
        var enumValues = System.Enum.GetValues(enumType).Cast<int>().ToList();
        var options = new string[enumValues.Count];
        var selected = new bool[enumValues.Count];

        for (int i = 0; i < enumValues.Count; i++)
        {
            options[i] = System.Enum.GetName(enumType, enumValues[i]);
            selected[i] = currentValues.Contains(enumValues[i]);
        }

        // 创建唯一标识符用于跟踪下拉状态
        string key = $"{listProp.propertyPath}_{enumType.Name}";
        if (!dropdownStates.ContainsKey(key))
        {
            dropdownStates[key] = false;
        }

        // 绘制下拉按钮
        var buttonContent = new GUIContent($"{label}: {GetSelectedText(selected, options)}");

        if (EditorGUI.DropdownButton(position, buttonContent, FocusType.Keyboard))
        {
            dropdownStates[key] = !dropdownStates[key];
        }

        // 绘制下拉菜单
        if (dropdownStates[key])
        {
            // 计算菜单位置
            float menuHeight = EditorGUIUtility.singleLineHeight * enumValues.Count;
            var menuRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight,
                                   position.width, menuHeight);

            // 存储菜单位置用于点击检测
            dropdownRects[key] = menuRect;

            // 绘制菜单背景
            GUI.Box(menuRect, "");

            // 绘制菜单项
            for (int i = 0; i < enumValues.Count; i++)
            {
                var itemRect = new Rect(menuRect.x, menuRect.y + i * EditorGUIUtility.singleLineHeight,
                                       menuRect.width, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                bool newValue = EditorGUI.ToggleLeft(itemRect, options[i], selected[i]);

                if (EditorGUI.EndChangeCheck())
                {
                    selected[i] = newValue;

                    // 更新列表
                    listProp.ClearArray();

                    for (int j = 0; j < selected.Length; j++)
                    {
                        if (selected[j])
                        {
                            listProp.InsertArrayElementAtIndex(listProp.arraySize);
                            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).intValue = enumValues[j];
                        }
                    }

                    // 立即应用修改
                    listProp.serializedObject.ApplyModifiedProperties();
                }
            }

            // 返回菜单高度（包括按钮和菜单）
            return EditorGUIUtility.singleLineHeight + menuHeight;
        }

        // 返回按钮高度
        return EditorGUIUtility.singleLineHeight;
    }

    private string GetSelectedText(bool[] selected, string[] options)
    {
        var selectedItems = new List<string>();
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i])
            {
                selectedItems.Add(options[i]);
            }
        }

        return selectedItems.Count > 0 ? string.Join(", ", selectedItems) : "None";
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0;

        // 检查是否有下拉菜单打开
        var inputButtonsProp = property.FindPropertyRelative("inputButtons");
        var inputStateProp = property.FindPropertyRelative("inputState");

        // 按钮下拉菜单高度
        string buttonsKey = $"{inputButtonsProp.propertyPath}_{typeof(Enums.InputButton).Name}";
        if (dropdownStates.ContainsKey(buttonsKey) && dropdownStates[buttonsKey])
        {
            var enumValues = System.Enum.GetValues(typeof(Enums.InputButton)).Cast<int>().ToList();
            height += EditorGUIUtility.singleLineHeight * (enumValues.Count + 1);
        }
        else
        {
            height += EditorGUIUtility.singleLineHeight;
        }

        // 状态下拉菜单高度
        string statesKey = $"{inputStateProp.propertyPath}_{typeof(Enums.ButtonState).Name}";
        if (dropdownStates.ContainsKey(statesKey) && dropdownStates[statesKey])
        {
            var enumValues = System.Enum.GetValues(typeof(Enums.ButtonState)).Cast<int>().ToList();
            height += EditorGUIUtility.singleLineHeight * (enumValues.Count + 1);
        }
        else
        {
            height += EditorGUIUtility.singleLineHeight;
        }

        // 添加间距
        height += EditorGUIUtility.standardVerticalSpacing;

        return height;
    }
}

[CustomPropertyDrawer(typeof(InputJoystickDataCheck))]
public class InputJoystickDataCheckDrawer : PropertyDrawer
{
    public static Dictionary<string, bool> dropdownStates = new Dictionary<string, bool>();
    public static Dictionary<string, Rect> dropdownRects = new Dictionary<string, Rect>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 获取属性
        var inputJoysticksProp = property.FindPropertyRelative("inputJoysticks");
        var joystickVigorsProp = property.FindPropertyRelative("joystickVigors");

        // 初始位置
        float y = position.y;

        // 绘制摇杆方向选择下拉菜单
        var joysticksRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        y += DrawMultiSelectEnum(joysticksRect, inputJoysticksProp, "Input Joysticks", typeof(Enums.InputJoystick));
        y += EditorGUIUtility.standardVerticalSpacing;

        // 绘制力度选择下拉菜单
        var vigorsRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        y += DrawMultiSelectEnum(vigorsRect, joystickVigorsProp, "Joystick Vigors", typeof(Enums.JoystickVigor));

        EditorGUI.EndProperty();
    }

    private float DrawMultiSelectEnum(Rect position, SerializedProperty listProp, string label, System.Type enumType)
    {
        // 获取当前选中的值
        var currentValues = new List<int>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            currentValues.Add(listProp.GetArrayElementAtIndex(i).intValue);
        }

        // 创建选项列表
        var enumValues = System.Enum.GetValues(enumType).Cast<int>().ToList();
        var options = new string[enumValues.Count];
        var selected = new bool[enumValues.Count];

        for (int i = 0; i < enumValues.Count; i++)
        {
            options[i] = System.Enum.GetName(enumType, enumValues[i]);
            selected[i] = currentValues.Contains(enumValues[i]);
        }

        // 创建唯一标识符用于跟踪下拉状态
        string key = $"{listProp.propertyPath}_{enumType.Name}";
        if (!dropdownStates.ContainsKey(key))
        {
            dropdownStates[key] = false;
        }

        // 绘制下拉按钮
        var buttonContent = new GUIContent($"{label}: {GetSelectedText(selected, options)}");

        if (EditorGUI.DropdownButton(position, buttonContent, FocusType.Keyboard))
        {
            dropdownStates[key] = !dropdownStates[key];
        }

        // 绘制下拉菜单
        if (dropdownStates[key])
        {
            // 计算菜单位置
            float menuHeight = EditorGUIUtility.singleLineHeight * enumValues.Count;
            var menuRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight,
                                   position.width, menuHeight);

            // 存储菜单位置用于点击检测
            dropdownRects[key] = menuRect;

            // 绘制菜单背景
            GUI.Box(menuRect, "");

            // 绘制菜单项
            for (int i = 0; i < enumValues.Count; i++)
            {
                var itemRect = new Rect(menuRect.x, menuRect.y + i * EditorGUIUtility.singleLineHeight,
                                       menuRect.width, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                bool newValue = EditorGUI.ToggleLeft(itemRect, options[i], selected[i]);

                if (EditorGUI.EndChangeCheck())
                {
                    selected[i] = newValue;

                    // 更新列表
                    listProp.ClearArray();

                    for (int j = 0; j < selected.Length; j++)
                    {
                        if (selected[j])
                        {
                            listProp.InsertArrayElementAtIndex(listProp.arraySize);
                            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).intValue = enumValues[j];
                        }
                    }

                    // 立即应用修改
                    listProp.serializedObject.ApplyModifiedProperties();
                }
            }

            // 返回菜单高度（包括按钮和菜单）
            return EditorGUIUtility.singleLineHeight + menuHeight;
        }

        // 返回按钮高度
        return EditorGUIUtility.singleLineHeight;
    }

    private string GetSelectedText(bool[] selected, string[] options)
    {
        var selectedItems = new List<string>();
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i])
            {
                selectedItems.Add(options[i]);
            }
        }

        return selectedItems.Count > 0 ? string.Join(", ", selectedItems) : "None";
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0;

        // 检查是否有下拉菜单打开
        var inputJoysticksProp = property.FindPropertyRelative("inputJoysticks");
        var joystickVigorsProp = property.FindPropertyRelative("joystickVigors");

        // 摇杆下拉菜单高度
        string joysticksKey = $"{inputJoysticksProp.propertyPath}_{typeof(Enums.InputJoystick).Name}";
        if (dropdownStates.ContainsKey(joysticksKey) && dropdownStates[joysticksKey])
        {
            var enumValues = System.Enum.GetValues(typeof(Enums.InputJoystick)).Cast<int>().ToList();
            height += EditorGUIUtility.singleLineHeight * (enumValues.Count + 1);
        }
        else
        {
            height += EditorGUIUtility.singleLineHeight;
        }

        // 力度下拉菜单高度
        string vigorsKey = $"{joystickVigorsProp.propertyPath}_{typeof(Enums.JoystickVigor).Name}";
        if (dropdownStates.ContainsKey(vigorsKey) && dropdownStates[vigorsKey])
        {
            var enumValues = System.Enum.GetValues(typeof(Enums.JoystickVigor)).Cast<int>().ToList();
            height += EditorGUIUtility.singleLineHeight * (enumValues.Count + 1);
        }
        else
        {
            height += EditorGUIUtility.singleLineHeight;
        }

        // 添加间距
        height += EditorGUIUtility.standardVerticalSpacing;

        return height;
    }
}

#endif