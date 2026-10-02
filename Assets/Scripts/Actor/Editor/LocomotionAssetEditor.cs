#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocomotionAsset), true)]
public sealed class LocomotionAssetEditor : Editor
{
    private readonly List<string> _coverageIssues = new();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var asset = (LocomotionAsset)target;
        if (!asset.HasValidRuntimeConfig)
            EditorGUILayout.HelpBox("Runtime configuration contains invalid values; this asset cannot be selected.", MessageType.Error);
        _coverageIssues.Clear();
        asset.CollectAnimationCoverageIssues(_coverageIssues);
        if (_coverageIssues.Count > 0)
            EditorGUILayout.HelpBox("Animation coverage incomplete (Stage 3 visual acceptance):\n" +
                string.Join("\n", _coverageIssues), MessageType.Warning);
    }
}

[CustomPropertyDrawer(typeof(LocomotionMoveDefinition))]
public sealed class LocomotionMoveDefinitionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        SerializedProperty blendType = property.FindPropertyRelative("blendType");
        SerializedProperty oneDimensional = property.FindPropertyRelative("oneDimensional");
        SerializedProperty twoDimensional = property.FindPropertyRelative("twoDimensional");

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, blendType);
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty selected = blendType.enumValueIndex == (int)LocomotionMoveBlendType.TwoDimensional
            ? twoDimensional
            : oneDimensional;
        EditorGUI.PropertyField(line, selected, true);
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        SerializedProperty blendType = property.FindPropertyRelative("blendType");
        SerializedProperty selected = blendType.enumValueIndex == (int)LocomotionMoveBlendType.TwoDimensional
            ? property.FindPropertyRelative("twoDimensional")
            : property.FindPropertyRelative("oneDimensional");
        return EditorGUIUtility.singleLineHeight * 2f
               + EditorGUIUtility.standardVerticalSpacing * 2f
               + EditorGUI.GetPropertyHeight(selected, true);
    }
}
#endif
