#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[CustomPropertyDrawer(typeof(InputCondition))]
public class InputConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 获取属性
        var dataCheckProp = property.FindPropertyRelative("dataCheck");

        // 关键修复：使用自定义标签而不是默认的 "Element x"
        GUIContent customLabel = new GUIContent("");

        // 绘制折叠菜单
        position.height = EditorGUIUtility.singleLineHeight;
        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, customLabel);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // 绘制类型选择按钮
            var buttonWidth = position.width / 2f - 5f;
            var buttonRect1 = new Rect(position.x, position.y, buttonWidth, EditorGUIUtility.singleLineHeight);
            var buttonRect2 = new Rect(position.x + buttonWidth + 10f, position.y, buttonWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            {
                // 检查当前是否是按钮数据
                bool isButtonData = dataCheckProp.managedReferenceValue is ButtonInputCondition;

                EditorGUI.BeginDisabledGroup(isButtonData);
                if (GUI.Button(buttonRect1, "Button Input"))
                {
                    // 创建新实例，避免共享引用
                    dataCheckProp.managedReferenceValue = new ButtonInputCondition();
                }
                EditorGUI.EndDisabledGroup();

                // 检查当前是否是摇杆数据
                bool isJoystickData = dataCheckProp.managedReferenceValue is JoystickInputCondition;

                EditorGUI.BeginDisabledGroup(isJoystickData);
                if (GUI.Button(buttonRect2, "Joystick Input"))
                {
                    // 创建新实例，避免共享引用
                    dataCheckProp.managedReferenceValue = new JoystickInputCondition();
                }
                EditorGUI.EndDisabledGroup();
            }
            if (EditorGUI.EndChangeCheck())
            {
                // 立即应用修改
                dataCheckProp.serializedObject.ApplyModifiedProperties();
            }

            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // 绘制具体数据
            if (dataCheckProp.managedReferenceValue != null)
            {
                // 关键修复：使用 dataCheckProp 而不是再查找一次
                var height = EditorGUI.GetPropertyHeight(dataCheckProp, true);
                var dataRect = new Rect(position.x, position.y, position.width, height);

                // 使用 GUIContent.none 隐藏内部标签
                EditorGUI.PropertyField(dataRect, dataCheckProp, GUIContent.none, true);

                position.y += height + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        var height = EditorGUIUtility.singleLineHeight * 2; // 基础高度（折叠标签和按钮）
        height += EditorGUIUtility.standardVerticalSpacing * 2;

        // 关键修复：使用正确的属性名
        var dataCheckProp = property.FindPropertyRelative("dataCheck");

        // 添加 null 检查
        if (dataCheckProp != null && dataCheckProp.managedReferenceValue != null)
        {
            height += EditorGUI.GetPropertyHeight(dataCheckProp, true);
        }

        return height;
    }
}
#endif