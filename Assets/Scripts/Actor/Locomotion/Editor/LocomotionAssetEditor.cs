#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(LocomotionAsset), true)]
[CanEditMultipleObjects]
public sealed class LocomotionAssetEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        // Use the same card chrome as ActionAsset; field layout remains native IMGUI.
        ActionEditorTheme.Apply(root, "action-editor-asset-inspector");
        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;

        AddCard(root, "Mode Selection", () =>
        {
            DrawProperty("priority");
            DrawProperty("entryConditions");
        });
        AddCard(root, "Tags", () => DrawProperty("selfTags"));
        AddCard(root, "Movement Config", () => DrawChildren("movementConfig"));
        AddCard(root, "Move", DrawMove);
        if (serializedObject.FindProperty("start") != null)
        {
            AddCard(root, "Transitions", () =>
            {
                DrawProperty("transitionBlendDuration");
                DrawProperty("stopPlaybackMode");
                DrawProperty("transitionDecisionConfig");
                DrawProperty("start");
                DrawProperty("stop");
                DrawProperty("pivot");
            });
        }
        root.Add(new IMGUIContainer(() => DrawCard(() =>
            DrawPropertiesExcluding(serializedObject, "m_Script", "priority", "entryConditions", "selfTags",
                "movementConfig", "move", "transitionBlendDuration", "stopPlaybackMode", "transitionDecisionConfig",
                "start", "stop", "pivot"))));
        return root;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    private void AddCard(VisualElement root, string title, Action draw)
    {
        var card = new VisualElement();
        card.AddToClassList("action-editor-section");
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        heading.AddToClassList("action-editor-card-title");
        card.Add(heading);
        card.Add(new IMGUIContainer(() => DrawCard(draw)));
        root.Add(card);
    }

    private void DrawCard(Action draw)
    {
        serializedObject.UpdateIfRequiredOrScript();
        draw();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string path)
    {
        var property = serializedObject.FindProperty(path);
        if (property != null)
            EditorGUILayout.PropertyField(property, true);
    }

    private void DrawChildren(string path)
    {
        var property = serializedObject.FindProperty(path);
        if (property == null)
            return;
        var child = property.Copy();
        var end = property.GetEndProperty();
        if (!child.NextVisible(true))
            return;
        do
        {
            if (SerializedProperty.EqualContents(child, end))
                break;
            EditorGUILayout.PropertyField(child, true);
        } while (child.NextVisible(false));
    }

    private void DrawMove()
    {
        DrawProperty("move.blendType");
        var blendType = serializedObject.FindProperty("move.blendType");
        if (blendType == null || blendType.hasMultipleDifferentValues)
            return;

        string definition = blendType.enumValueIndex == (int)LocomotionMoveBlendType.TwoDimensional
            ? "move.twoDimensional" : "move.oneDimensional";
        DrawProperty(definition + ".parameter");
        DrawProperty(definition + ".samples");
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
