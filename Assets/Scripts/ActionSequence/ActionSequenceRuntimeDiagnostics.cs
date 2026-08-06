using System;
using System.Collections.Generic;
using System.Text;

public enum ActionSequenceRuntimeDiagnosticCode
{
    NullTrack,
    NullClip,
    PhaseMismatch,
    DisallowedClipType,
    TimingAdjusted,
    FixedDurationClipSkipped,
    FixedDurationClipTruncated,
    LegacyClipProjection,
    NullClipRuntime,
}

public readonly struct ActionSequenceRuntimeDiagnostic
{
    public readonly ActionSequenceRuntimeDiagnosticCode Code;
    public readonly string Message;
    public readonly int TrackIndex;
    public readonly int ClipIndex;
    public readonly bool IsLegacyClip;

    public ActionSequenceRuntimeDiagnostic(
        ActionSequenceRuntimeDiagnosticCode code,
        string message,
        int trackIndex = -1,
        int clipIndex = -1,
        bool isLegacyClip = false)
    {
        Code = code;
        Message = message;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        IsLegacyClip = isLegacyClip;
    }
}

public sealed class ActionSequenceRuntimeDiagnostics
{
    private readonly List<ActionSequenceRuntimeDiagnostic> _issues = new List<ActionSequenceRuntimeDiagnostic>();

    public IReadOnlyList<ActionSequenceRuntimeDiagnostic> Issues => _issues;
    public bool HasIssues => _issues.Count > 0;

    public void Add(ActionSequenceRuntimeDiagnostic issue)
    {
        _issues.Add(issue);
    }

    public void Clear()
    {
        _issues.Clear();
    }

    public string ToSummary(string header = "ActionSequence runtime initialized with diagnostics")
    {
        if (_issues.Count == 0)
            return string.Empty;

        var builder = new StringBuilder(header);
        builder.Append(':');

        for (int i = 0; i < _issues.Count; i++)
        {
            ActionSequenceRuntimeDiagnostic issue = _issues[i];
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(issue.Code);

            if (issue.TrackIndex >= 0)
            {
                builder.Append(" track ");
                builder.Append(issue.TrackIndex);
            }

            if (issue.ClipIndex >= 0)
            {
                builder.Append(issue.IsLegacyClip ? " legacy clip " : " clip ");
                builder.Append(issue.ClipIndex);
            }

            if (!string.IsNullOrWhiteSpace(issue.Message))
            {
                builder.Append(": ");
                builder.Append(issue.Message);
            }
        }

        return builder.ToString();
    }
}
