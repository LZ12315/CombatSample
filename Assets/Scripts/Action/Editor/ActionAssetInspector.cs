#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ActionAsset))]
public sealed class ActionAssetInspector : Editor
{
    private VisualElement _root;
    private ActionAuthoringListGUI _requirements;
    private ActionAuthoringListGUI _cancelRules;

    public override VisualElement CreateInspectorGUI()
    {
        _root = new VisualElement();
        ActionEditorTheme.Apply(_root, "action-editor-asset-inspector");
        _requirements = new ActionAuthoringListGUI(typeof(ActionCondition));
        _cancelRules = new ActionAuthoringListGUI();
        Undo.undoRedoPerformed -= OnUndoRedo;
        Undo.undoRedoPerformed += OnUndoRedo;
        ActionEditorInteractionGate.Changed -= Repaint;
        ActionEditorInteractionGate.Changed += Repaint;

        AddCard("Timeline & Playback", () =>
        {
            ActionAsset action = target as ActionAsset;
            if (GUILayout.Button("Open Action Timeline"))
                ActionTimelineWindow.Open(action);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Duration", Mathf.Max(1, action.Timeline?.DurationFrames ?? 1));
            GUILayout.Space(4f);
            DrawBoolean("isLoop", new GUIContent("Loop", "Loop this Action at runtime. This is separate from Timeline preview looping."));
            DrawEnum("_priorityLayer", new GUIContent("Priority Layer"));
            DrawInteger("_priorityValue", new GUIContent("Priority Value"));
            DrawBoolean("_allowReenterWhilePlaying", new GUIContent("Allow Reentry While Playing"));
        });
        AddCard("Trigger & Start Context", () =>
        {
            SerializedProperty triggerMode = DrawEnum("_triggerMode", new GUIContent("Trigger Mode"));
            DrawEnum("_startContextMode", new GUIContent("Start Context"));
            if (triggerMode != null && triggerMode.enumValueIndex == (int)ActionTriggerMode.Event)
                DrawProperty("_eventTriggerTag", new GUIContent("Event Trigger Tag"), true);
        });
        AddCard("Tags", () => DrawFoldoutProperty("Self Tags", "_selfTags"));
        AddCard("Motion Control", () => ActionMotionPolicyGUI.DrawDefaults(serializedObject.FindProperty("_motionPolicy")));
        AddCard("Rules", () =>
        {
            DrawAuthoringList(_requirements, "Requirements", "_entryConditions");
            GUILayout.Space(6f);
            DrawAuthoringList(_cancelRules, "Cancel Rules", "_cancelRules");
        });
        _root.Add(new IMGUIContainer(DrawInteractionMessage));
        return _root;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        ActionEditorInteractionGate.Changed -= Repaint;
        _root = null;
    }

    private void OnUndoRedo()
    {
        _requirements?.Reset();
        _cancelRules?.Reset();
        Repaint();
    }

    private void DrawAuthoringList(ActionAuthoringListGUI list, string title, string path)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            list.Draw(property, title);
    }

    private void AddCard(string title, Action draw)
    {
        var card = new VisualElement();
        card.AddToClassList("action-editor-section");
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        heading.AddToClassList("action-editor-card-title");
        card.Add(heading);
        card.Add(new IMGUIContainer(() => DrawCard(draw)));
        _root.Add(card);
    }

    private void DrawCard(Action draw)
    {
        ActionAsset action = target as ActionAsset;
        if (action == null)
            return;

        serializedObject.UpdateIfRequiredOrScript();
        bool lockedByTimeline = ActionEditorInteractionGate.IsActive &&
                                action == ActionEditorContext.Shared.CurrentAction;
        using (new EditorGUI.DisabledScope(lockedByTimeline))
            draw();
        if (serializedObject.ApplyModifiedProperties())
            ReportAssetChange(action);
    }

    private void DrawInteractionMessage()
    {
        if (ActionEditorInteractionGate.IsActive && target == ActionEditorContext.Shared.CurrentAction)
            EditorGUILayout.HelpBox("Finish the active Timeline gesture before editing this Action.", MessageType.Info);
    }

    private SerializedProperty DrawProperty(string path, GUIContent label, bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            EditorGUILayout.PropertyField(property, label, includeChildren);
        return property;
    }

    private SerializedProperty DrawBoolean(string path, GUIContent label)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);
        return property;
    }

    private SerializedProperty DrawInteger(string path, GUIContent label)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            property.intValue = EditorGUILayout.IntField(label, property.intValue);
        return property;
    }

    private SerializedProperty DrawEnum(string path, GUIContent label)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            property.enumValueIndex = EditorGUILayout.Popup(label, property.enumValueIndex, property.enumDisplayNames);
        return property;
    }

    private void DrawFoldoutProperty(string label, string path)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    private static void ReportAssetChange(ActionAsset action)
    {
        if (action == ActionEditorContext.Shared.CurrentAction)
            ActionEditorContext.Shared.QueueBindingChange(action, ActionEditorChangeFlags.Content);
        else
            EditorUtility.SetDirty(action);
    }
}
#endif
