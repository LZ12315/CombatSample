#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum ActionSequenceEditorCommandStatus
{
    Success,
    NoChange,
    UnsupportedTarget,
    InvalidArgument,
    NotFound,
    AmbiguousIdentity,
    Locked,
    DisallowedType,
    PhaseMismatch,
    InvalidTiming,
    ConfirmationRequired,
}

public readonly struct ActionSequenceEditorSelectionSuggestion
{
    public ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind kind, string editorId)
    {
        Kind = kind;
        EditorId = editorId;
    }

    public ActionSequenceEditorDocumentItemKind Kind { get; }
    public string EditorId { get; }
}

public sealed class ActionSequenceEditorCommandResult
{
    public ActionSequenceEditorCommandResult(
        ActionSequenceEditorCommandStatus status,
        string message,
        int documentRevision = 0,
        ActionSequenceEditorChangeSet changeSet = null,
        string affectedTrackId = null,
        string affectedClipId = null,
        ActionSequenceEditorSelectionSuggestion selectionSuggestion = default)
    {
        Status = status;
        Message = message;
        DocumentRevision = documentRevision;
        ChangeSet = changeSet;
        AffectedTrackId = affectedTrackId;
        AffectedClipId = affectedClipId;
        SelectionSuggestion = selectionSuggestion;
    }

    public ActionSequenceEditorCommandStatus Status { get; }
    public string Message { get; }
    public int DocumentRevision { get; }
    public ActionSequenceEditorChangeSet ChangeSet { get; }
    public string AffectedTrackId { get; }
    public string AffectedClipId { get; }
    public ActionSequenceEditorSelectionSuggestion SelectionSuggestion { get; }
    public bool Succeeded => Status == ActionSequenceEditorCommandStatus.Success;
}

public static class ActionSequenceEditorCommands
{
    public static event Action<Object, ActionSequenceEditorChangeSet> Changed;

    public static void NotifyExternalContentChanged(Object target, ActionSequenceEditorChangeSet changeSet)
    {
        if (target == null || changeSet == null)
            return;

        Changed?.Invoke(target, changeSet);
    }

    public static ActionSequenceEditorCommandResult SetFrameRate(Object target, int frameRate)
    {
        if (frameRate <= 0)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Frame rate must be greater than zero.");

        return RunSequenceCommand(target, "Set Action Sequence Frame Rate", ActionSequenceEditorChangeFlags.Timing | ActionSequenceEditorChangeFlags.Validation, root =>
        {
            SerializedProperty property = root.FindPropertyRelative("frameRate");
            if (property.intValue == frameRate)
                return false;

            property.intValue = frameRate;
            return true;
        });
    }

    public static ActionSequenceEditorCommandResult SetDurationMode(Object target, ActionSequenceDurationMode durationMode)
    {
        return RunSequenceCommand(target, "Set Action Sequence Duration Mode", ActionSequenceEditorChangeFlags.Timing | ActionSequenceEditorChangeFlags.Validation, root =>
        {
            SerializedProperty property = root.FindPropertyRelative("durationMode");
            int newValue = (int)durationMode;
            if (property.intValue == newValue)
                return false;

            property.intValue = newValue;
            return true;
        });
    }

    public static ActionSequenceEditorCommandResult SetFixedDurationFrames(Object target, int durationFrames)
    {
        if (durationFrames <= 0)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Fixed duration must be greater than zero.");

        return RunSequenceCommand(target, "Set Action Sequence Fixed Duration", ActionSequenceEditorChangeFlags.Timing | ActionSequenceEditorChangeFlags.Validation, root =>
        {
            SerializedProperty property = root.FindPropertyRelative("durationFrames");
            if (property.intValue == durationFrames)
                return false;

            property.intValue = durationFrames;
            return true;
        });
    }

    public static ActionSequenceEditorCommandResult AddTrack(Object target, Type trackType)
    {
        if (!ActionSequenceEditorTypeRegistry.IsCreatableTrackType(trackType))
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Track type is not creatable.");

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
            return Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");

        ActionSequenceTrackDefinition track = ActionSequenceEditorTypeRegistry.CreateTrack(trackType);
        if (track == null)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Track type could not be constructed.");

        ActionSequenceEditorIdentity.AssignNewIdToCreatedItem(target, track);
        int index = GetPhaseGroupEndIndex(document, track.Phase);

        return Commit(document, "Add Action Sequence Track", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            SerializedProperty tracks = document.GetTracksProperty();
            tracks.InsertArrayElementAtIndex(index);
            tracks.GetArrayElementAtIndex(index).managedReferenceValue = track;
            changeSet.AddTrack(track.EditorId);
            return new ActionSequenceEditorCommandResult(
                ActionSequenceEditorCommandStatus.Success,
                "Track added.",
                document.Revision + 1,
                changeSet,
                track.EditorId,
                null,
                new ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind.Track, track.EditorId));
        });
    }

    public static ActionSequenceEditorCommandResult DeleteTrack(Object target, string trackId, bool confirmNonEmpty = false)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveTrack(document, trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");
        if (track.Clips.Count > 0 && !confirmNonEmpty)
            return Fail(ActionSequenceEditorCommandStatus.ConfirmationRequired, "Deleting a non-empty track requires confirmation.");

        string suggestedTrackId = GetAdjacentTrackId(document, trackIndex);
        return Commit(document, "Delete Action Sequence Track", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            document.GetTracksProperty().DeleteArrayElementAtIndex(trackIndex);
            changeSet.AddTrack(trackId);
            return new ActionSequenceEditorCommandResult(
                ActionSequenceEditorCommandStatus.Success,
                "Track deleted.",
                document.Revision + 1,
                changeSet,
                trackId,
                null,
                new ActionSequenceEditorSelectionSuggestion(
                    string.IsNullOrEmpty(suggestedTrackId) ? ActionSequenceEditorDocumentItemKind.Sequence : ActionSequenceEditorDocumentItemKind.Track,
                    suggestedTrackId));
        });
    }

    public static ActionSequenceEditorCommandResult RenameTrack(Object target, string trackId, string displayName)
    {
        return SetTrackString(target, trackId, "displayName", displayName ?? string.Empty, "Rename Action Sequence Track", ActionSequenceEditorChangeFlags.Content);
    }

    public static ActionSequenceEditorCommandResult SetTrackMuted(Object target, string trackId, bool muted)
    {
        return SetTrackBool(target, trackId, "muted", muted, "Set Action Sequence Track Mute", ActionSequenceEditorChangeFlags.Content | ActionSequenceEditorChangeFlags.Validation, allowWhenLocked: false);
    }

    public static ActionSequenceEditorCommandResult SetTrackLocked(Object target, string trackId, bool locked)
    {
        return SetTrackBool(target, trackId, "locked", locked, "Set Action Sequence Track Lock", ActionSequenceEditorChangeFlags.Content | ActionSequenceEditorChangeFlags.Validation, allowWhenLocked: !locked);
    }

    public static ActionSequenceEditorCommandResult SetTrackCollapsed(Object target, string trackId, bool collapsed)
    {
        return SetTrackBool(target, trackId, "collapsed", collapsed, "Set Action Sequence Track Collapse", ActionSequenceEditorChangeFlags.Content, allowWhenLocked: false);
    }

    public static ActionSequenceEditorCommandResult ReorderTrackWithinPhase(Object target, string trackId, int phaseLocalIndex)
    {
        if (phaseLocalIndex < 0)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Phase-local index must be non-negative.");

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveTrack(document, trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");

        int targetIndex = GetIndexForPhaseLocalPosition(document, track.Phase, phaseLocalIndex);
        if (targetIndex < 0)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Phase-local index is out of range.");
        if (targetIndex == trackIndex)
            return Fail(ActionSequenceEditorCommandStatus.NoChange, "Track is already at that position.");

        return Commit(document, "Reorder Action Sequence Track", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            document.GetTracksProperty().MoveArrayElement(trackIndex, targetIndex);
            changeSet.AddTrack(trackId);
            return Success(document, changeSet, "Track reordered.", trackId, null, new ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind.Track, trackId));
        });
    }

    public static ActionSequenceEditorCommandResult AddClip(Object target, string trackId, Type clipType, int startFrame, int endFrame)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveTrack(document, trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot trackSnapshot = document.Tracks[trackIndex];
        if (trackSnapshot.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");

        SerializedProperty trackProperty = document.GetTrackProperty(trackIndex);
        ActionSequenceTrackDefinition track = trackProperty?.managedReferenceValue as ActionSequenceTrackDefinition;
        ActionSequenceClipDefinition clip = ActionSequenceEditorTypeRegistry.CreateClip(clipType);
        if (track == null || clip == null)
            return Fail(ActionSequenceEditorCommandStatus.InvalidArgument, "Clip type could not be constructed for the track.");
        if (!track.AllowsClipType(clipType))
            return Fail(ActionSequenceEditorCommandStatus.DisallowedType, "Track does not accept this clip type.");
        if (clip.Phase != track.Phase)
            return Fail(ActionSequenceEditorCommandStatus.PhaseMismatch, "Clip phase does not match track phase.");
        if (!IsValidTiming(document.Sequence, startFrame, endFrame))
            return Fail(ActionSequenceEditorCommandStatus.InvalidTiming, "Clip timing is invalid.");

        clip.startFrame = startFrame;
        clip.endFrame = endFrame;
        ActionSequenceEditorIdentity.AssignNewIdToCreatedItem(target, clip);

        return Commit(document, "Add Action Sequence Clip", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Timing | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            SerializedProperty clips = document.GetTrackClipsProperty(trackIndex);
            int clipIndex = clips.arraySize;
            clips.InsertArrayElementAtIndex(clipIndex);
            clips.GetArrayElementAtIndex(clipIndex).managedReferenceValue = clip;
            changeSet.AddTrack(trackId).AddClip(clip.EditorId);
            return Success(document, changeSet, "Clip added.", trackId, clip.EditorId, new ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind.Clip, clip.EditorId));
        });
    }

    public static ActionSequenceEditorCommandResult DeleteClip(Object target, string clipId)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveClip(document, clipId, out int trackIndex, out int clipIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");

        return Commit(document, "Delete Action Sequence Clip", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            document.GetTrackClipsProperty(trackIndex).DeleteArrayElementAtIndex(clipIndex);
            changeSet.AddTrack(track.EditorId).AddClip(clipId);
            return Success(document, changeSet, "Clip deleted.", track.EditorId, clipId, new ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind.Track, track.EditorId));
        });
    }

    public static ActionSequenceEditorCommandResult SetClipTiming(Object target, string clipId, int startFrame, int endFrame)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveClip(document, clipId, out int trackIndex, out int clipIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");
        if (!IsValidTiming(document.Sequence, startFrame, endFrame))
            return Fail(ActionSequenceEditorCommandStatus.InvalidTiming, "Clip timing is invalid.");

        ActionSequenceClipSnapshot clip = track.Clips[clipIndex];
        if (clip.StartFrame == startFrame && clip.EndFrame == endFrame)
            return Fail(ActionSequenceEditorCommandStatus.NoChange, "Clip timing is unchanged.");

        return Commit(document, "Set Action Sequence Clip Timing", ActionSequenceEditorChangeFlags.Timing | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            SerializedProperty clipProperty = document.GetClipProperty(trackIndex, clipIndex);
            clipProperty.FindPropertyRelative("startFrame").intValue = startFrame;
            clipProperty.FindPropertyRelative("endFrame").intValue = endFrame;
            changeSet.AddTrack(track.EditorId).AddClip(clipId);
            return Success(document, changeSet, "Clip timing changed.", track.EditorId, clipId, new ActionSequenceEditorSelectionSuggestion(ActionSequenceEditorDocumentItemKind.Clip, clipId));
        });
    }

    public static ActionSequenceEditorCommandResult RepairInvalidIds(Object target)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
            return Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");

        int changed = ActionSequenceEditorIdentity.RepairInvalidIds(target);
        if (changed == 0)
            return Fail(ActionSequenceEditorCommandStatus.NoChange, "No invalid IDs found.");

        var changeSet = new ActionSequenceEditorChangeSet(ActionSequenceEditorChangeFlags.Validation);
        Changed?.Invoke(target, changeSet);
        return new ActionSequenceEditorCommandResult(ActionSequenceEditorCommandStatus.Success, "Invalid IDs repaired.", document.Revision + 1, changeSet);
    }

    public static ActionSequenceEditorCommandResult MigrateLegacyClips(Object target)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
            return Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");
        if (document.LegacyClips.Count == 0)
            return Fail(ActionSequenceEditorCommandStatus.NoChange, "No legacy clips found.");

        return CommitDirect(document, "Migrate Action Sequence Legacy Clips", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            ActionSequenceData data = GetData(target);
            int migrated = data.EditorMigrateLegacyClips(track => !track.locked);
            if (migrated == 0)
                return Fail(ActionSequenceEditorCommandStatus.NoChange, "No legacy clips could be migrated.");

            ActionSequenceEditorIdentity.UpgradeMissingIdsWithoutUndo(target);
            return Success(document, changeSet, "Legacy clips migrated.");
        });
    }

    public static ActionSequenceEditorCommandResult RepairTrackPhaseOrder(Object target)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
            return Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");

        List<ActionSequenceTrackSnapshot> sorted = new List<ActionSequenceTrackSnapshot>(document.Tracks);
        sorted.Sort(CompareTrackSnapshotsStable);

        bool changed = false;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].TrackIndex != i)
            {
                changed = true;
                if (sorted[i].Locked)
                    return Fail(ActionSequenceEditorCommandStatus.Locked, "Repairing phase order would move a locked track.");
            }
        }

        if (!changed)
            return Fail(ActionSequenceEditorCommandStatus.NoChange, "Track phase order is already valid.");

        return CommitDirect(document, "Repair Action Sequence Track Phase Order", ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Validation, changeSet =>
        {
            List<ActionSequenceTrackDefinition> tracks = GetData(target).EditorTracks;
            var indexedTracks = new List<IndexedTrack>(tracks.Count);
            for (int i = 0; i < tracks.Count; i++)
                indexedTracks.Add(new IndexedTrack(tracks[i], i));

            indexedTracks.Sort(CompareIndexedTracksStable);
            tracks.Clear();
            for (int i = 0; i < indexedTracks.Count; i++)
                tracks.Add(indexedTracks[i].Track);

            return Success(document, changeSet, "Track phase order repaired.");
        });
    }

    private static int CompareTrackSnapshotsStable(ActionSequenceTrackSnapshot a, ActionSequenceTrackSnapshot b)
    {
        int phaseCompare = a.Phase.CompareTo(b.Phase);
        if (phaseCompare != 0)
            return phaseCompare;

        return a.TrackIndex.CompareTo(b.TrackIndex);
    }

    private static int CompareIndexedTracksStable(IndexedTrack a, IndexedTrack b)
    {
        if (a.Track == null && b.Track == null)
            return a.OriginalIndex.CompareTo(b.OriginalIndex);
        if (a.Track == null)
            return 1;
        if (b.Track == null)
            return -1;

        int phaseCompare = a.Track.Phase.CompareTo(b.Track.Phase);
        if (phaseCompare != 0)
            return phaseCompare;

        return a.OriginalIndex.CompareTo(b.OriginalIndex);
    }

    private readonly struct IndexedTrack
    {
        public IndexedTrack(ActionSequenceTrackDefinition track, int originalIndex)
        {
            Track = track;
            OriginalIndex = originalIndex;
        }

        public ActionSequenceTrackDefinition Track { get; }
        public int OriginalIndex { get; }
    }

    private static ActionSequenceEditorCommandResult RunSequenceCommand(
        Object target,
        string undoName,
        ActionSequenceEditorChangeFlags flags,
        Func<SerializedProperty, bool> edit)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
            return Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");

        return Commit(document, undoName, flags, changeSet =>
        {
            if (!edit(document.GetRootProperty()))
                return Fail(ActionSequenceEditorCommandStatus.NoChange, "Sequence value is unchanged.");

            return Success(document, changeSet, "Sequence changed.");
        });
    }

    private static ActionSequenceEditorCommandResult SetTrackString(Object target, string trackId, string propertyName, string value, string undoName, ActionSequenceEditorChangeFlags flags)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveTrack(document, trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");

        return Commit(document, undoName, flags, changeSet =>
        {
            SerializedProperty property = document.GetTrackProperty(trackIndex).FindPropertyRelative(propertyName);
            if (property.stringValue == value)
                return Fail(ActionSequenceEditorCommandStatus.NoChange, "Track value is unchanged.");

            property.stringValue = value;
            changeSet.AddTrack(trackId);
            return Success(document, changeSet, "Track changed.", trackId);
        });
    }

    private static ActionSequenceEditorCommandResult SetTrackBool(Object target, string trackId, string propertyName, bool value, string undoName, ActionSequenceEditorChangeFlags flags, bool allowWhenLocked)
    {
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!TryResolveTrack(document, trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure))
            return failure;

        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked && !allowWhenLocked)
            return Fail(ActionSequenceEditorCommandStatus.Locked, "Track is locked.");

        return Commit(document, undoName, flags, changeSet =>
        {
            SerializedProperty property = document.GetTrackProperty(trackIndex).FindPropertyRelative(propertyName);
            if (property.boolValue == value)
                return Fail(ActionSequenceEditorCommandStatus.NoChange, "Track value is unchanged.");

            property.boolValue = value;
            changeSet.AddTrack(trackId);
            return Success(document, changeSet, "Track changed.", trackId);
        });
    }

    private static ActionSequenceEditorCommandResult Commit(
        ActionSequenceSerializedDocument document,
        string undoName,
        ActionSequenceEditorChangeFlags flags,
        Func<ActionSequenceEditorChangeSet, ActionSequenceEditorCommandResult> edit)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        Undo.RecordObject(document.Target, undoName);

        var changeSet = new ActionSequenceEditorChangeSet(flags);
        ActionSequenceEditorCommandResult result = edit(changeSet);
        if (result.Status != ActionSequenceEditorCommandStatus.Success)
            return result;

        document.SerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(document.Target);
        Undo.CollapseUndoOperations(group);
        Changed?.Invoke(document.Target, result.ChangeSet);
        return result;
    }

    private static ActionSequenceEditorCommandResult CommitDirect(
        ActionSequenceSerializedDocument document,
        string undoName,
        ActionSequenceEditorChangeFlags flags,
        Func<ActionSequenceEditorChangeSet, ActionSequenceEditorCommandResult> edit)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        Undo.RecordObject(document.Target, undoName);

        var changeSet = new ActionSequenceEditorChangeSet(flags);
        ActionSequenceEditorCommandResult result = edit(changeSet);
        if (result.Status != ActionSequenceEditorCommandStatus.Success)
            return result;

        EditorUtility.SetDirty(document.Target);
        Undo.CollapseUndoOperations(group);
        Changed?.Invoke(document.Target, result.ChangeSet);
        return result;
    }

    private static ActionSequenceEditorCommandResult Success(
        ActionSequenceSerializedDocument document,
        ActionSequenceEditorChangeSet changeSet,
        string message,
        string affectedTrackId = null,
        string affectedClipId = null,
        ActionSequenceEditorSelectionSuggestion selectionSuggestion = default)
    {
        return new ActionSequenceEditorCommandResult(
            ActionSequenceEditorCommandStatus.Success,
            message,
            document.Revision + 1,
            changeSet,
            affectedTrackId,
            affectedClipId,
            selectionSuggestion);
    }

    private static ActionSequenceEditorCommandResult Fail(ActionSequenceEditorCommandStatus status, string message)
    {
        return new ActionSequenceEditorCommandResult(status, message);
    }

    private static bool TryResolveTrack(ActionSequenceSerializedDocument document, string trackId, out int trackIndex, out ActionSequenceEditorCommandResult failure)
    {
        trackIndex = -1;
        failure = null;
        if (document == null || !document.IsSupported)
        {
            failure = Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");
            return false;
        }

        ActionSequenceEditorResolveStatus status = document.ResolveTrack(trackId, out trackIndex);
        return ResolveStatusToFailure(status, "Track", out failure);
    }

    private static bool TryResolveClip(ActionSequenceSerializedDocument document, string clipId, out int trackIndex, out int clipIndex, out ActionSequenceEditorCommandResult failure)
    {
        trackIndex = -1;
        clipIndex = -1;
        failure = null;
        if (document == null || !document.IsSupported)
        {
            failure = Fail(ActionSequenceEditorCommandStatus.UnsupportedTarget, "Target is not an ActionSequence target.");
            return false;
        }

        ActionSequenceEditorResolveStatus status = document.ResolveClip(clipId, out trackIndex, out clipIndex);
        return ResolveStatusToFailure(status, "Clip", out failure);
    }

    private static bool ResolveStatusToFailure(ActionSequenceEditorResolveStatus status, string label, out ActionSequenceEditorCommandResult failure)
    {
        failure = null;
        switch (status)
        {
            case ActionSequenceEditorResolveStatus.Found:
                return true;
            case ActionSequenceEditorResolveStatus.Ambiguous:
                failure = Fail(ActionSequenceEditorCommandStatus.AmbiguousIdentity, $"{label} ID is duplicated.");
                return false;
            case ActionSequenceEditorResolveStatus.MissingId:
                failure = Fail(ActionSequenceEditorCommandStatus.InvalidArgument, $"{label} ID is missing.");
                return false;
            default:
                failure = Fail(ActionSequenceEditorCommandStatus.NotFound, $"{label} was not found.");
                return false;
        }
    }

    private static bool IsValidTiming(ActionSequenceSnapshot sequence, int startFrame, int endFrame)
    {
        if (startFrame < 0 || endFrame <= startFrame)
            return false;

        return sequence.DurationMode != ActionSequenceDurationMode.FixedFrames || endFrame <= sequence.FixedDurationFrames;
    }

    private static int GetPhaseGroupEndIndex(ActionSequenceSerializedDocument document, ActionSequenceClipPhase phase)
    {
        int insertIndex = document.Tracks.Count;
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = document.Tracks[i];
            if (track.IsNull || track.MissingType)
                continue;
            if (track.Phase.CompareTo(phase) > 0)
                return i;
            if (track.Phase == phase)
                insertIndex = i + 1;
        }

        return insertIndex;
    }

    private static int GetIndexForPhaseLocalPosition(ActionSequenceSerializedDocument document, ActionSequenceClipPhase phase, int phaseLocalIndex)
    {
        int countInPhase = 0;
        int lastPhaseIndex = -1;
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = document.Tracks[i];
            if (track.IsNull || track.MissingType || track.Phase != phase)
                continue;

            if (countInPhase == phaseLocalIndex)
                return i;

            countInPhase++;
            lastPhaseIndex = i;
        }

        return phaseLocalIndex == countInPhase && lastPhaseIndex >= 0 ? lastPhaseIndex : -1;
    }

    private static string GetAdjacentTrackId(ActionSequenceSerializedDocument document, int deletedTrackIndex)
    {
        if (document.Tracks.Count <= 1)
            return null;

        int next = Mathf.Min(deletedTrackIndex + 1, document.Tracks.Count - 1);
        if (next == deletedTrackIndex)
            next = deletedTrackIndex - 1;

        return next >= 0 && next < document.Tracks.Count ? document.Tracks[next].EditorId : null;
    }

    private static ActionSequenceData GetData(Object target)
    {
        if (target is ActionAsset actionAsset)
            return actionAsset.SequenceData;
        if (target is ActionSequenceAsset sequenceAsset)
            return sequenceAsset.Data;

        return null;
    }
}
#endif
