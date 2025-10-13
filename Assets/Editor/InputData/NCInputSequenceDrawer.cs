#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NodeCanvas.Editor;
using ParadoxNotion;
using System.Reflection;

// 这是一个概念性示例，具体API可能需要根据NodeCanvas版本调整
[CustomPropertyDrawer(typeof(InputSequence))]
public class NCInputSequenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 1. 开始属性绘制
        EditorGUI.BeginProperty(position, label, property);

        // 2. 绘制一个折叠标题
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // 3. 绘制 waitTime 属性
            var waitTimeProp = property.FindPropertyRelative("waitTime");
            EditorGUI.PropertyField(new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight), waitTimeProp);
            yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // 4. 绘制 dataChecks 列表（关键部分）
            var dataChecksProp = property.FindPropertyRelative("dataChecks");
            if (dataChecksProp != null && dataChecksProp.isArray)
            {
                // 绘制数组大小
                dataChecksProp.arraySize = EditorGUI.IntField(new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight), "Size", dataChecksProp.arraySize);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 绘制每个数组元素
                for (int i = 0; i < dataChecksProp.arraySize; i++)
                {
                    var elementProp = dataChecksProp.GetArrayElementAtIndex(i);
                    // 重点：使用 Unity 默认的 PropertyField 来绘制 InputCondition。
                    // 这会尝试使用其自身的 PropertyDrawer，或者回退到默认的序列化属性绘制。
                    float elementHeight = EditorGUI.GetPropertyHeight(elementProp, true);
                    EditorGUI.PropertyField(new Rect(position.x, position.y + yOffset, position.width, elementHeight), elementProp, new GUIContent($"Element {i}"), true);
                    yOffset += elementHeight + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // 折叠行的高度

        if (property.isExpanded)
        {
            height += EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUIUtility.singleLineHeight; // waitTime 行的高度

            var dataChecksProp = property.FindPropertyRelative("dataChecks");
            if (dataChecksProp != null && dataChecksProp.isArray)
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUIUtility.singleLineHeight; // "Size" 行的高度

                for (int i = 0; i < dataChecksProp.arraySize; i++)
                {
                    var elementProp = dataChecksProp.GetArrayElementAtIndex(i);
                    height += EditorGUI.GetPropertyHeight(elementProp, true);
                    height += EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }
        return height;
    }
}
#endif