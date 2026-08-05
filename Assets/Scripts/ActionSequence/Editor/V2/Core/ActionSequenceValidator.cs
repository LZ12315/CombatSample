#if UNITY_EDITOR
using System.Collections.Generic;

public enum ActionSequenceEditorValidationSeverity
{
    Info,
    Warning,
    Error,
}

public enum ActionSequenceEditorValidationCode
{
    MissingEditorId,
    MalformedEditorId,
    DuplicateEditorId,
    NullTrack,
    NullClip,
    MissingManagedReferenceType,
    DisallowedClipType,
    PhaseMismatch,
    InvalidStartFrame,
    InvalidEndFrame,
    ClipExceedsFixedDuration,
    InvalidFrameRate,
    InvalidFixedDuration,
    LegacyClip,
    TrackPhaseOrder,
}

public sealed class ActionSequenceEditorValidationIssue
{
    public ActionSequenceEditorValidationIssue(
        ActionSequenceEditorValidationSeverity severity,
        ActionSequenceEditorValidationCode code,
        string message,
        ActionSequenceEditorDocumentItemKind itemKind,
        string editorId,
        int trackIndex,
        int clipIndex,
        int legacyClipIndex,
        long managedReferenceId,
        string repairCommandId = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        ItemKind = itemKind;
        EditorId = editorId;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        LegacyClipIndex = legacyClipIndex;
        ManagedReferenceId = managedReferenceId;
        RepairCommandId = repairCommandId;
    }

    public ActionSequenceEditorValidationSeverity Severity { get; }
    public ActionSequenceEditorValidationCode Code { get; }
    public string Message { get; }
    public ActionSequenceEditorDocumentItemKind ItemKind { get; }
    public string EditorId { get; }
    public int TrackIndex { get; }
    public int ClipIndex { get; }
    public int LegacyClipIndex { get; }
    public long ManagedReferenceId { get; }
    public string RepairCommandId { get; }
}

public sealed class ActionSequenceEditorValidationResult
{
    private readonly List<ActionSequenceEditorValidationIssue> issues = new List<ActionSequenceEditorValidationIssue>();

    public IReadOnlyList<ActionSequenceEditorValidationIssue> Issues => issues;
    public bool HasIssues => issues.Count > 0;
    public bool HasErrors { get; private set; }

    public void Add(ActionSequenceEditorValidationIssue issue)
    {
        if (issue == null)
            return;

        issues.Add(issue);
        if (issue.Severity == ActionSequenceEditorValidationSeverity.Error)
            HasErrors = true;
    }
}

public static class ActionSequenceValidator
{
    public const string RepairInvalidIdsCommandId = "RepairInvalidIds";
    public const string MigrateLegacyClipsCommandId = "MigrateLegacyClips";
    public const string RepairTrackPhaseOrderCommandId = "RepairTrackPhaseOrder";

    public static ActionSequenceEditorValidationResult Validate(UnityEngine.Object target)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        return Validate(document);
    }

    public static ActionSequenceEditorValidationResult Validate(ActionSequenceSerializedDocument document)
    {
        var result = new ActionSequenceEditorValidationResult();
        if (document == null || !document.IsSupported)
            return result;

        ValidateSequence(document, result);
        ValidateTracks(document, result);
        ValidateLegacyClips(document, result);
        ValidatePhaseOrder(document, result);
        return result;
    }

    private static void ValidateSequence(ActionSequenceSerializedDocument document, ActionSequenceEditorValidationResult result)
    {
        ActionSequenceSnapshot sequence = document.Sequence;
        if (sequence.FrameRate <= 0)
        {
            result.Add(new ActionSequenceEditorValidationIssue(
                ActionSequenceEditorValidationSeverity.Error,
                ActionSequenceEditorValidationCode.InvalidFrameRate,
                "Frame rate must be greater than zero.",
                ActionSequenceEditorDocumentItemKind.Sequence,
                null,
                -1,
                -1,
                -1,
                0));
        }

        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames && sequence.FixedDurationFrames <= 0)
        {
            result.Add(new ActionSequenceEditorValidationIssue(
                ActionSequenceEditorValidationSeverity.Error,
                ActionSequenceEditorValidationCode.InvalidFixedDuration,
                "Fixed duration frames must be greater than zero.",
                ActionSequenceEditorDocumentItemKind.Sequence,
                null,
                -1,
                -1,
                -1,
                0));
        }
    }

    private static void ValidateTracks(ActionSequenceSerializedDocument document, ActionSequenceEditorValidationResult result)
    {
        IReadOnlyList<ActionSequenceTrackSnapshot> tracks = document.Tracks;
        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            ActionSequenceTrackSnapshot track = tracks[trackIndex];
            ValidateIdentity(document, result, ActionSequenceEditorDocumentItemKind.Track, track.EditorId, track.TrackIndex, -1, -1, track.ManagedReferenceId);

            if (track.IsNull)
            {
                result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Warning, ActionSequenceEditorValidationCode.NullTrack, "Track is null.", ActionSequenceEditorDocumentItemKind.Track, track.EditorId, track.TrackIndex, -1, -1, track.ManagedReferenceId));
                continue;
            }

            if (track.MissingType)
            {
                result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.MissingManagedReferenceType, BuildMissingTypeMessage("Track", track.MissingTypeInfo), ActionSequenceEditorDocumentItemKind.Track, track.EditorId, track.TrackIndex, -1, -1, track.ManagedReferenceId));
                continue;
            }

            IReadOnlyList<ActionSequenceClipSnapshot> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                ValidateClip(document, result, clips[clipIndex], false);
        }
    }

    private static void ValidateLegacyClips(ActionSequenceSerializedDocument document, ActionSequenceEditorValidationResult result)
    {
        IReadOnlyList<ActionSequenceClipSnapshot> legacyClips = document.LegacyClips;
        for (int i = 0; i < legacyClips.Count; i++)
        {
            ActionSequenceClipSnapshot clip = legacyClips[i];
            ValidateClip(document, result, clip, true);

            if (!clip.IsNull)
            {
                result.Add(NewIssue(
                    ActionSequenceEditorValidationSeverity.Warning,
                    ActionSequenceEditorValidationCode.LegacyClip,
                    "Legacy flat clip has not been migrated.",
                    ActionSequenceEditorDocumentItemKind.LegacyClip,
                    clip.EditorId,
                    -1,
                    -1,
                    clip.LegacyClipIndex,
                    clip.ManagedReferenceId,
                    MigrateLegacyClipsCommandId));
            }
        }
    }

    private static void ValidateClip(
        ActionSequenceSerializedDocument document,
        ActionSequenceEditorValidationResult result,
        ActionSequenceClipSnapshot clip,
        bool isLegacy)
    {
        ActionSequenceEditorDocumentItemKind kind = isLegacy ? ActionSequenceEditorDocumentItemKind.LegacyClip : ActionSequenceEditorDocumentItemKind.Clip;
        ValidateIdentity(document, result, kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId);

        if (clip.IsNull)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Warning, ActionSequenceEditorValidationCode.NullClip, "Clip is null.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
            return;
        }

        if (clip.MissingType)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.MissingManagedReferenceType, BuildMissingTypeMessage("Clip", clip.MissingTypeInfo), kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
            return;
        }

        if (!clip.AllowedByTrack)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.DisallowedClipType, "Track does not accept this clip type.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
        }

        if (!clip.PhaseMatchesTrack)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.PhaseMismatch, "Clip phase does not match track phase.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
        }

        if (clip.StartFrame < 0)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.InvalidStartFrame, "Clip start frame must be non-negative.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
        }

        if (clip.EndFrame <= clip.StartFrame)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.InvalidEndFrame, "Clip end frame must be greater than start frame.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
        }

        ActionSequenceSnapshot sequence = document.Sequence;
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames && clip.EndFrame > sequence.FixedDurationFrames)
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.ClipExceedsFixedDuration, "Clip exceeds fixed duration.", kind, clip.EditorId, clip.TrackIndex, clip.ClipIndex, clip.LegacyClipIndex, clip.ManagedReferenceId));
        }
    }

    private static void ValidateIdentity(
        ActionSequenceSerializedDocument document,
        ActionSequenceEditorValidationResult result,
        ActionSequenceEditorDocumentItemKind kind,
        string editorId,
        int trackIndex,
        int clipIndex,
        int legacyClipIndex,
        long managedReferenceId)
    {
        if (string.IsNullOrEmpty(editorId))
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.MissingEditorId, "Editor ID is missing.", kind, editorId, trackIndex, clipIndex, legacyClipIndex, managedReferenceId, RepairInvalidIdsCommandId));
            return;
        }

        if (!ActionSequenceEditorIdentity.IsValidEditorId(editorId))
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.MalformedEditorId, "Editor ID is malformed.", kind, editorId, trackIndex, clipIndex, legacyClipIndex, managedReferenceId, RepairInvalidIdsCommandId));
            return;
        }

        if (document.HasDuplicateId(editorId))
        {
            result.Add(NewIssue(ActionSequenceEditorValidationSeverity.Error, ActionSequenceEditorValidationCode.DuplicateEditorId, "Editor ID is duplicated.", kind, editorId, trackIndex, clipIndex, legacyClipIndex, managedReferenceId, RepairInvalidIdsCommandId));
        }
    }

    private static void ValidatePhaseOrder(ActionSequenceSerializedDocument document, ActionSequenceEditorValidationResult result)
    {
        IReadOnlyList<ActionSequenceTrackSnapshot> tracks = document.Tracks;
        ActionSequenceClipPhase previous = default;
        bool hasPrevious = false;

        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = tracks[i];
            if (track.IsNull || track.MissingType)
                continue;

            if (hasPrevious && track.Phase.CompareTo(previous) < 0)
            {
                result.Add(NewIssue(
                    ActionSequenceEditorValidationSeverity.Warning,
                    ActionSequenceEditorValidationCode.TrackPhaseOrder,
                    "Track phase order differs from V2 display and execution order.",
                    ActionSequenceEditorDocumentItemKind.Track,
                    track.EditorId,
                    track.TrackIndex,
                    -1,
                    -1,
                    track.ManagedReferenceId,
                    RepairTrackPhaseOrderCommandId));
            }

            previous = track.Phase;
            hasPrevious = true;
        }
    }

    private static ActionSequenceEditorValidationIssue NewIssue(
        ActionSequenceEditorValidationSeverity severity,
        ActionSequenceEditorValidationCode code,
        string message,
        ActionSequenceEditorDocumentItemKind itemKind,
        string editorId,
        int trackIndex,
        int clipIndex,
        int legacyClipIndex,
        long managedReferenceId,
        string repairCommandId = null)
    {
        return new ActionSequenceEditorValidationIssue(
            severity,
            code,
            message,
            itemKind,
            editorId,
            trackIndex,
            clipIndex,
            legacyClipIndex,
            managedReferenceId,
            repairCommandId);
    }

    private static string BuildMissingTypeMessage(string itemLabel, ActionSequenceMissingManagedReferenceInfo info)
    {
        string typeName = info.Tooltip;
        return string.IsNullOrEmpty(typeName)
            ? itemLabel + " managed reference type is missing."
            : itemLabel + " managed reference type is missing: " + typeName;
    }
}
#endif
