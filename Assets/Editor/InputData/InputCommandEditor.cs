#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[CustomEditor(typeof(InputCommand))]
public class InputCommandEditor : Editor
{
    private ReorderableList dataChecksList;
    private SerializedProperty dataChecksProp;
    private SerializedProperty waitTimeProp;

    private void OnEnable()
    {
        // 获取属性
        waitTimeProp = serializedObject.FindProperty("waitTime");
        dataChecksProp = serializedObject.FindProperty("dataChecks");

        // 创建可重排序列表
        dataChecksList = new ReorderableList(
            serializedObject,
            dataChecksProp,
            true,  // 可拖动
            true,  // 显示头部
            true,  // 显示添加按钮
            true   // 显示删除按钮
        );

        // 设置列表头部
        dataChecksList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Data Checks");
        };

        // 设置元素高度
        dataChecksList.elementHeightCallback = (int index) => {
            var element = dataChecksProp.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true);
        };

        // 绘制列表元素
        dataChecksList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var element = dataChecksProp.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };

        // 添加元素时的回调
        dataChecksList.onAddCallback = (ReorderableList list) => {
            int index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;
            
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            var dataCheckProp = element.FindPropertyRelative("dataCheck");
            
            // 创建新的实例，避免共享引用
            dataCheckProp.managedReferenceValue = new ButtonCheckSetting();
            
            // 立即应用修改
            element.serializedObject.ApplyModifiedProperties();
        };
        
        // 添加元素背景色
        dataChecksList.drawElementBackgroundCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            if (Event.current.type == EventType.Repaint)
            {
                // 交替行背景色
                Color bgColor = index % 2 == 0 
                    ? new Color(0.85f, 0.85f, 0.85f, 1f) 
                    : new Color(0.75f, 0.75f, 0.75f, 1f);
                
                EditorGUI.DrawRect(rect, bgColor);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // 绘制其他属性
        EditorGUILayout.PropertyField(waitTimeProp);
        
        EditorGUILayout.Space();
        
        // 绘制自定义列表
        dataChecksList.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif