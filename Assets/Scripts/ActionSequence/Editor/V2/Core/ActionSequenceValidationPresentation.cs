#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

public readonly struct ActionSequenceIssueState
{
    public ActionSequenceIssueState(ActionSequenceEditorValidationSeverity highestSeverity, int count, string tooltip)
    {
        HighestSeverity = highestSeverity;
        Count = count;
        Tooltip = tooltip ?? string.Empty;
    }

    public ActionSequenceEditorValidationSeverity HighestSeverity { get; }
    public int Count { get; }
    public string Tooltip { get; }
    public bool HasIssues => Count > 0;
}

public sealed class ActionSequenceValidationPresentation
{
    private readonly Dictionary<string, List<ActionSequenceEditorValidationIssue>> trackIssues =
        new Dictionary<string, List<ActionSequenceEditorValidationIssue>>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ActionSequenceEditorValidationIssue>> clipIssues =
        new Dictionary<string, List<ActionSequenceEditorValidationIssue>>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ActionSequenceEditorValidationIssue>> issuesByRepairCommand =
        new Dictionary<string, List<ActionSequenceEditorValidationIssue>>(StringComparer.Ordinal);

    private ActionSequenceValidationPresentation(ActionSequenceEditorValidationResult validation)
    {
        Validation = validation ?? new ActionSequenceEditorValidationResult();
        BuildIndexes();
    }

    public ActionSequenceEditorValidationResult Validation { get; }
    public int InfoCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }
    public bool HasIssues => Validation.HasIssues;
    public bool HasErrors => ErrorCount > 0;
    public IReadOnlyDictionary<string, List<ActionSequenceEditorValidationIssue>> IssuesByRepairCommand => issuesByRepairCommand;

    public static ActionSequenceValidationPresentation Create(ActionSequenceEditorValidationResult validation)
    {
        return new ActionSequenceValidationPresentation(validation);
    }

    public ActionSequenceIssueState GetTrackIssueState(ActionSequenceTrackSnapshot track)
    {
        if (track == null)
            return default;

        return BuildIssueState(GetTrackKey(track), trackIssues);
    }

    public ActionSequenceIssueState GetClipIssueState(ActionSequenceClipSnapshot clip)
    {
        if (clip == null)
            return default;

        return BuildIssueState(GetClipKey(clip), clipIssues);
    }

    public bool HasRepairCommand(string repairCommandId)
    {
        return !string.IsNullOrEmpty(repairCommandId)
            && issuesByRepairCommand.TryGetValue(repairCommandId, out List<ActionSequenceEditorValidationIssue> issues)
            && issues.Count > 0;
    }

    public string BuildSummaryText()
    {
        if (!HasIssues)
            return "Valid";

        var builder = new StringBuilder();
        if (ErrorCount > 0)
            builder.Append(ErrorCount).Append(" error(s)");
        if (WarningCount > 0)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(WarningCount).Append(" warning(s)");
        }
        if (InfoCount > 0)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(InfoCount).Append(" info");
        }

        return builder.ToString();
    }

    private void BuildIndexes()
    {
        IReadOnlyList<ActionSequenceEditorValidationIssue> issues = Validation.Issues;
        for (int i = 0; i < issues.Count; i++)
        {
            ActionSequenceEditorValidationIssue issue = issues[i];
            Count(issue.Severity);

            if (!string.IsNullOrEmpty(issue.RepairCommandId))
                Add(issuesByRepairCommand, issue.RepairCommandId, issue);

            switch (issue.ItemKind)
            {
                case ActionSequenceEditorDocumentItemKind.Track:
                    Add(trackIssues, GetIssueTrackKey(issue), issue);
                    break;
                case ActionSequenceEditorDocumentItemKind.Clip:
                    Add(clipIssues, GetIssueClipKey(issue), issue);
                    Add(trackIssues, GetIssueTrackKey(issue), issue);
                    break;
                case ActionSequenceEditorDocumentItemKind.LegacyClip:
                    Add(trackIssues, "legacy", issue);
                    break;
            }
        }
    }

    private void Count(ActionSequenceEditorValidationSeverity severity)
    {
        switch (severity)
        {
            case ActionSequenceEditorValidationSeverity.Error:
                ErrorCount++;
                break;
            case ActionSequenceEditorValidationSeverity.Warning:
                WarningCount++;
                break;
            default:
                InfoCount++;
                break;
        }
    }

    private ActionSequenceIssueState BuildIssueState(string key, Dictionary<string, List<ActionSequenceEditorValidationIssue>> map)
    {
        if (string.IsNullOrEmpty(key) || !map.TryGetValue(key, out List<ActionSequenceEditorValidationIssue> issues) || issues.Count == 0)
            return default;

        ActionSequenceEditorValidationSeverity highest = ActionSequenceEditorValidationSeverity.Info;
        var builder = new StringBuilder();
        for (int i = 0; i < issues.Count; i++)
        {
            ActionSequenceEditorValidationIssue issue = issues[i];
            if (CompareSeverity(issue.Severity, highest) > 0)
                highest = issue.Severity;

            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append(issue.Code).Append(": ").Append(issue.Message);
        }

        return new ActionSequenceIssueState(highest, issues.Count, builder.ToString());
    }

    private static int CompareSeverity(ActionSequenceEditorValidationSeverity left, ActionSequenceEditorValidationSeverity right)
    {
        return SeverityRank(left).CompareTo(SeverityRank(right));
    }

    private static int SeverityRank(ActionSequenceEditorValidationSeverity severity)
    {
        return severity switch
        {
            ActionSequenceEditorValidationSeverity.Error => 3,
            ActionSequenceEditorValidationSeverity.Warning => 2,
            _ => 1,
        };
    }

    private static void Add(Dictionary<string, List<ActionSequenceEditorValidationIssue>> map, string key, ActionSequenceEditorValidationIssue issue)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (!map.TryGetValue(key, out List<ActionSequenceEditorValidationIssue> issues))
        {
            issues = new List<ActionSequenceEditorValidationIssue>();
            map.Add(key, issues);
        }

        issues.Add(issue);
    }

    private static string GetTrackKey(ActionSequenceTrackSnapshot track)
    {
        if (!string.IsNullOrEmpty(track.EditorId))
            return "id:" + track.EditorId;
        return "track:" + track.TrackIndex + ":" + track.ManagedReferenceId;
    }

    private static string GetClipKey(ActionSequenceClipSnapshot clip)
    {
        if (!string.IsNullOrEmpty(clip.EditorId))
            return "id:" + clip.EditorId;
        return "clip:" + clip.TrackIndex + ":" + clip.ClipIndex + ":" + clip.LegacyClipIndex + ":" + clip.ManagedReferenceId;
    }

    private static string GetIssueTrackKey(ActionSequenceEditorValidationIssue issue)
    {
        if (!string.IsNullOrEmpty(issue.EditorId))
            return "id:" + issue.EditorId;
        return "track:" + issue.TrackIndex + ":" + issue.ManagedReferenceId;
    }

    private static string GetIssueClipKey(ActionSequenceEditorValidationIssue issue)
    {
        if (!string.IsNullOrEmpty(issue.EditorId))
            return "id:" + issue.EditorId;
        return "clip:" + issue.TrackIndex + ":" + issue.ClipIndex + ":" + issue.LegacyClipIndex + ":" + issue.ManagedReferenceId;
    }
}
#endif
