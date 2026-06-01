using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BoneReference))]
public class BoneReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var mode = property.FindPropertyRelative("mode");
        var humanBone = property.FindPropertyRelative("humanBone");
        var bonePath = property.FindPropertyRelative("bonePath");

        var line = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        var r0 = new Rect(position.x, position.y, position.width, line);
        EditorGUI.PropertyField(r0, mode, new GUIContent(label.text, label.tooltip));

        var r1 = new Rect(position.x, r0.yMax + spacing, position.width, line);
        if (mode.intValue == (int)BoneReference.Mode.HumanBone)
        {
            EditorGUI.PropertyField(r1, humanBone, new GUIContent("Human Bone"));
        }
        else
        {
            string pathLabel = mode.intValue == (int)BoneReference.Mode.ActorPath
                ? "Actor Path"
                : "Bone Path";
            EditorGUI.PropertyField(r1, bonePath, new GUIContent(pathLabel));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
    }
}
