#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActionDataCondition))]
public class ActionDataConditionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 基础高度：第一行枚举
        float totalHeight = EditorGUIUtility.singleLineHeight;

        // 【健壮性改进 1】使用 nameof 替代字符串，重构变量名时代码会报错提醒，而不是运行时崩溃
        var checkTypeProp = property.FindPropertyRelative(nameof(ActionDataCondition.checkType));

        // 【健壮性改进 2】防御性判空，防止属性丢失导致编辑器卡死
        if (checkTypeProp != null)
        {
            int currentMask = checkTypeProp.intValue;

            // Phase 行高
            if ((currentMask & (int)Enums.ActionDataType.Phase) != 0)
                totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Progress 行高
            if ((currentMask & (int)Enums.ActionDataType.Progress) != 0)
                totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        // 底部留白
        return totalHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 【健壮性改进 1】使用 nameof 强绑定
        var checkTypeProp = property.FindPropertyRelative(nameof(ActionDataCondition.checkType));
        var requiredPhaseProp = property.FindPropertyRelative(nameof(ActionDataCondition.requiredPhase));
        var minProgressProp = property.FindPropertyRelative(nameof(ActionDataCondition.minProgress));
        var maxProgressProp = property.FindPropertyRelative(nameof(ActionDataCondition.maxProgress));

        // 如果找不到核心属性，直接绘制默认样式（兜底策略）
        if (checkTypeProp == null)
        {
            EditorGUI.LabelField(position, "Error: Property not found");
            EditorGUI.EndProperty();
            return;
        }

        Rect curRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // 绘制 CheckType
        EditorGUI.PropertyField(curRect, checkTypeProp, new GUIContent("Check Type"));

        int currentMask = checkTypeProp.intValue;

        // 绘制 Phase
        if ((currentMask & (int)Enums.ActionDataType.Phase) != 0 && requiredPhaseProp != null)
        {
            curRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(curRect, requiredPhaseProp);
        }

        // 绘制 Progress
        if ((currentMask & (int)Enums.ActionDataType.Progress) != 0 && minProgressProp != null && maxProgressProp != null)
        {
            curRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // 手动布局逻辑保持不变...
            float labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new Rect(curRect.x, curRect.y, labelWidth, curRect.height);
            EditorGUI.LabelField(labelRect, "Progress Range");

            Rect sliderRect = new Rect(curRect.x + labelWidth, curRect.y, curRect.width - labelWidth, curRect.height);
            float fieldWidth = 40f;
            float sliderWidth = sliderRect.width - (fieldWidth * 2) - 10;

            Rect minFieldRect = new Rect(sliderRect.x, sliderRect.y, fieldWidth, sliderRect.height);
            Rect sliderControlRect = new Rect(sliderRect.x + fieldWidth + 5, sliderRect.y, sliderWidth, sliderRect.height);
            Rect maxFieldRect = new Rect(sliderRect.x + fieldWidth + sliderWidth + 10, sliderRect.y, fieldWidth, sliderRect.height);

            float minVal = minProgressProp.floatValue;
            float maxVal = maxProgressProp.floatValue;

            minVal = EditorGUI.FloatField(minFieldRect, (float)System.Math.Round(minVal, 2));
            EditorGUI.MinMaxSlider(sliderControlRect, ref minVal, ref maxVal, 0f, 1f);
            maxVal = EditorGUI.FloatField(maxFieldRect, (float)System.Math.Round(maxVal, 2));

            minProgressProp.floatValue = Mathf.Clamp01(minVal);
            maxProgressProp.floatValue = Mathf.Clamp01(maxVal);
        }

        EditorGUI.EndProperty();
    }
}
#endif