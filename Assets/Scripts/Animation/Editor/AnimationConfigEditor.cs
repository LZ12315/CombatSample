#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationConfig))]
public sealed class AnimationConfigEditor : Editor
{
    private string _lastOperationMessage;
    private MessageType _lastOperationMessageType = MessageType.None;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var config = (AnimationConfig)target;
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Root Motion Baking", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Baked trajectories are stored inside this AnimationConfig. No separate Root Motion assets are created.",
            MessageType.Info);

        if (!string.IsNullOrEmpty(_lastOperationMessage))
            EditorGUILayout.HelpBox(_lastOperationMessage, _lastOperationMessageType);

        for (int i = 0; i < config.Entries.Count; i++)
            DrawEntry(config, i);

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(config.Entries.Count == 0))
        {
            if (GUILayout.Button("Bake All"))
            {
                RootMotionBakeBatchResult result = RootMotionBakeWorkflow.BakeAll(config);
                _lastOperationMessage = result.Summary;
                _lastOperationMessageType = result.IsSuccess ? MessageType.Info : MessageType.Error;
                serializedObject.Update();
                Repaint();
            }
        }
    }

    private void DrawEntry(AnimationConfig config, int entryIndex)
    {
        AnimationConfigEntry entry = config.Entries[entryIndex];
        RootMotionEntryStatus status = RootMotionBakeWorkflow.GetEntryStatus(config, entryIndex);
        string key = entry != null && !string.IsNullOrWhiteSpace(entry.Key)
            ? entry.Key
            : $"Entry {entryIndex}";

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{entryIndex}: {key}", EditorStyles.boldLabel);
        RootMotionTrajectory trajectory = entry != null ? entry.RootMotionTrajectory : null;
        string dataSummary = trajectory == null
            ? "None"
            : $"{trajectory.SampleCount} samples, {trajectory.Duration:R}s at {trajectory.SampleRate} Hz";
        EditorGUILayout.LabelField("Embedded Data", dataSummary);

        EditorGUILayout.HelpBox($"{status.Code}: {status.Message}", MessageTypeFor(status.Code));

        bool canBake = status.Code == RootMotionEntryStatusCode.Missing
            || status.Code == RootMotionEntryStatusCode.Stale
            || status.Code == RootMotionEntryStatusCode.Ready;

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!canBake))
        {
            if (GUILayout.Button(trajectory == null ? "Bake" : "Rebake"))
                RunEntryOperation(config, entryIndex);
        }

        using (new EditorGUI.DisabledScope(trajectory == null))
        {
            if (GUILayout.Button("Clear Data"))
            {
                bool success = RootMotionBakeWorkflow.TryClearTrajectory(config, entryIndex, out string diagnostic);
                _lastOperationMessage = diagnostic;
                _lastOperationMessageType = success ? MessageType.Info : MessageType.Error;
                serializedObject.Update();
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void RunEntryOperation(AnimationConfig config, int entryIndex)
    {
        bool success = RootMotionBakeWorkflow.TryBakeEntry(config, entryIndex, out RootMotionBakeOperationResult result);
        _lastOperationMessage = result.Message;
        _lastOperationMessageType = success ? MessageType.Info : MessageType.Error;
        serializedObject.Update();
        Repaint();
    }

    private static MessageType MessageTypeFor(RootMotionEntryStatusCode code)
    {
        switch (code)
        {
            case RootMotionEntryStatusCode.Ready:
            case RootMotionEntryStatusCode.PoseOnly:
                return MessageType.Info;
            case RootMotionEntryStatusCode.Missing:
            case RootMotionEntryStatusCode.Stale:
                return MessageType.Warning;
            default:
                return MessageType.Error;
        }
    }
}
#endif
