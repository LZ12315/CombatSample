#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InputDataSetting))]
public class InputDataSettingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // 获取属性
        var inputDataProp = property.FindPropertyRelative("inputData");
        
        // 绘制折叠菜单
        position.height = EditorGUIUtility.singleLineHeight;
        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label);
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
                EditorGUI.BeginDisabledGroup(inputDataProp.managedReferenceValue is InputButtonData);
                if (GUI.Button(buttonRect1, "Button Input"))
                {
                    inputDataProp.managedReferenceValue = new InputButtonData();
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.BeginDisabledGroup(inputDataProp.managedReferenceValue is InputJoystickData);
                if (GUI.Button(buttonRect2, "Joystick Input"))
                {
                    inputDataProp.managedReferenceValue = new InputJoystickData();
                }
                EditorGUI.EndDisabledGroup();
            }
            if (EditorGUI.EndChangeCheck())
            {
                inputDataProp.serializedObject.ApplyModifiedProperties();
            }
            
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // 绘制具体数据
            if (inputDataProp.managedReferenceValue != null)
            {
                var dataProp = property.FindPropertyRelative("inputData");
                var height = EditorGUI.GetPropertyHeight(dataProp, true);
                var dataRect = new Rect(position.x, position.y, position.width, height);
                
                EditorGUI.PropertyField(dataRect, dataProp, new GUIContent("Input Data"), true);
                
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
        
        var inputDataProp = property.FindPropertyRelative("inputData");
        if (inputDataProp.managedReferenceValue != null)
        {
            height += EditorGUI.GetPropertyHeight(inputDataProp, true);
        }
        
        return height;
    }
}
#endif