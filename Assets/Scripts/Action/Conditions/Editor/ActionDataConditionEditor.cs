#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActionDataCondition))]
public class ActionDataConditionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float totalHeight = EditorGUIUtility.singleLineHeight * 2f +
                            EditorGUIUtility.standardVerticalSpacing;

        var checkTypeProp = property.FindPropertyRelative(nameof(ActionDataCondition.checkType));

        if (checkTypeProp != null)
        {
            int currentMask = checkTypeProp.intValue;

            // Progress 行
            if ((currentMask & (int)Enums.ActionDataType.Progress) != 0)
                totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return totalHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var checkTypeProp = property.FindPropertyRelative(nameof(ActionDataCondition.checkType));
        var minProgressProp = property.FindPropertyRelative(nameof(ActionDataCondition.minProgress));
        var maxProgressProp = property.FindPropertyRelative(nameof(ActionDataCondition.maxProgress));

        if (checkTypeProp == null)
        {
            EditorGUI.LabelField(position, "Error: Property not found");
            EditorGUI.EndProperty();
            return;
        }

        Rect curRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(curRect, property.FindPropertyRelative(nameof(ActionCondition.invertResult)));
        curRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(curRect, checkTypeProp, new GUIContent("Check Type"));

        int currentMask = checkTypeProp.intValue;

        // 绘制 Progress
        if ((currentMask & (int)Enums.ActionDataType.Progress) != 0 && minProgressProp != null && maxProgressProp != null)
        {
            curRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            Rect sliderRect = EditorGUI.PrefixLabel(curRect, new GUIContent("Progress Range"));
            bool showSlider = sliderRect.width >= 130f;
            float fieldWidth = showSlider ? 40f : Mathf.Max(0f, (sliderRect.width - 5f) * 0.5f);
            float sliderWidth = Mathf.Max(0f, sliderRect.width - fieldWidth * 2f - 10f);

            Rect minFieldRect = new Rect(sliderRect.x, sliderRect.y, fieldWidth, sliderRect.height);
            Rect sliderControlRect = new Rect(sliderRect.x + fieldWidth + 5f, sliderRect.y, sliderWidth, sliderRect.height);
            Rect maxFieldRect = new Rect(sliderRect.xMax - fieldWidth, sliderRect.y, fieldWidth, sliderRect.height);

            float minVal = minProgressProp.floatValue;
            float maxVal = maxProgressProp.floatValue;

            minVal = EditorGUI.FloatField(minFieldRect, (float)System.Math.Round(minVal, 2));
            if (showSlider)
                EditorGUI.MinMaxSlider(sliderControlRect, ref minVal, ref maxVal, 0f, 1f);
            maxVal = EditorGUI.FloatField(maxFieldRect, (float)System.Math.Round(maxVal, 2));

            minProgressProp.floatValue = Mathf.Clamp01(minVal);
            maxProgressProp.floatValue = Mathf.Clamp01(maxVal);
        }

        EditorGUI.EndProperty();
    }
}
#endif
