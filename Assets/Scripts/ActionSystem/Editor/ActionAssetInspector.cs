#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ActionAsset))]
public sealed class ActionAssetInspector : Editor
{
    private VisualElement _root;

    public override VisualElement CreateInspectorGUI()
    {
        _root = new VisualElement();
        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;
        ActionEditorInteractionGate.Changed -= Repaint;
        ActionEditorInteractionGate.Changed += Repaint;

        _root.Add(new IMGUIContainer(DrawActionInspector));
        return _root;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        ActionEditorInteractionGate.Changed -= Repaint;
        _root = null;
    }

    private void DrawActionInspector()
    {
        ActionAsset action = target as ActionAsset;
        if (action == null)
            return;

        serializedObject.UpdateIfRequiredOrScript();
        bool lockedByTimeline = ActionEditorInteractionGate.IsActive &&
                                action == ActionEditorContext.Shared.CurrentAction;
        using (new EditorGUI.DisabledScope(lockedByTimeline))
        {
            EditorGUILayout.Space(8f);
            DrawGroup("Timeline", () =>
            {
                if (GUILayout.Button("Open Action Timeline"))
                    ActionTimelineWindow.Open(action);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField("Duration", Mathf.Max(1, action.Timeline?.DurationFrames ?? 1));
            });

            DrawGroup("Playback", () =>
            {
                DrawBoolean("isLoop", new GUIContent("Loop", "Loop this Action at runtime. This is separate from Timeline preview looping."));
                DrawEnum("_priorityLayer", new GUIContent("Priority Layer"));
                DrawInteger("_priorityValue", new GUIContent("Priority Value"));
                DrawBoolean("_allowReenterWhilePlaying", new GUIContent("Allow Reentry While Playing"));
            });

            DrawGroup("Trigger & Start Context", () =>
            {
                SerializedProperty triggerMode = DrawEnum("_triggerMode", new GUIContent("Trigger Mode"));
                DrawEnum("_startContextMode", new GUIContent("Start Context"));
                if (triggerMode != null && triggerMode.enumValueIndex == (int)ActionTriggerMode.Event)
                    DrawProperty("_eventTriggerTag", new GUIContent("Event Trigger Tag"), true);
            });

            DrawGroup("Conditions", () =>
            {
                DrawFoldoutProperty("Entry Conditions", "_entryConditions");
                DrawFoldoutProperty("Exit Conditions", "_exitConditions");
            });

            DrawGroup("Cancellation", () => DrawFoldoutProperty("Cancel Rules", "_cancelRules"));

            DrawGroup("Tags", () => DrawFoldoutProperty("Self Tags", "_selfTags"));
        }

        if (lockedByTimeline)
            EditorGUILayout.HelpBox("Finish the active Timeline gesture before editing this Action.", MessageType.Info);
        if (serializedObject.ApplyModifiedProperties())
            ReportAssetChange(action);
    }

    private SerializedProperty DrawProperty(string path, GUIContent label, bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            EditorGUILayout.PropertyField(property, label, includeChildren);
        return property;
    }

    private static void DrawGroup(string title, System.Action draw)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (!string.IsNullOrEmpty(title))
            {
                var heading = new GUIStyle(EditorStyles.boldLabel);
                heading.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.59f, 0.71f, 0.84f)
                    : new Color(0.20f, 0.38f, 0.58f);
                EditorGUILayout.LabelField(title, heading);
            }
            draw?.Invoke();
        }
        EditorGUILayout.Space(6f);
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
