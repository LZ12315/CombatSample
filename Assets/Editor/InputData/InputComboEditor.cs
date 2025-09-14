#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[CustomEditor(typeof(InputCombo))]
public class InputComboEditor : Editor
{
    private ReorderableList commandsList;
    private SerializedProperty commandsProp;

    private void OnEnable()
    {
        commandsProp = serializedObject.FindProperty("commands");

        // 创建可重排序列表
        commandsList = new ReorderableList(
            serializedObject,
            commandsProp,
            true, true, true, true
        );

        // 设置列表头部
        commandsList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Input Commands");
        };

        // 设置元素高度
        commandsList.elementHeightCallback = (int index) => {
            var element = commandsProp.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true);
        };

        // 绘制列表元素
        commandsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var element = commandsProp.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };

        // 添加元素时的回调
        commandsList.onAddCallback = (ReorderableList list) => {
            int index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;

            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            var inputDataProp = element.FindPropertyRelative("inputData");

            // 创建新的实例，避免共享引用
            inputDataProp.managedReferenceValue = new InputButtonDataCheck();

            // 立即应用修改
            element.serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 绘制其他属性
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skillName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("waitTime"));

        EditorGUILayout.Space();

        // 绘制自定义列表
        commandsList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif