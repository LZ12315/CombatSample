#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActionDataCondition))]
public class ActionDataConditionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float totalHeight = EditorGUIUtility.singleLineHeight;

        var checkTypeProp = property.FindPropertyRelative(nameof(ActionDataCondition.checkType));

        if (checkTypeProp != null)
        {
            int currentMask = checkTypeProp.intValue;

            // Progress ÐÐ
            if ((currentMask & (int)Enums.ActionDataType.Progress) != 0)
                totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return totalHeight + EditorGUIUtility.standardVerticalSpacing;
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

        // »æÖÆ CheckType
        EditorGUI.PropertyField(curRect, checkTypeProp, new GUIContent("Check Type"));

        int currentMask = checkTypeProp.intValue;

        // »æÖÆ Progress
        if ((currentMask & (int)Enums.ActionDataType.Progress) != 0 && minProgressProp != null && maxProgressProp != null)
        {
            curRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

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
