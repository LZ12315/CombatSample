#if UNITY_EDITOR
using System;
using UnityEditor;

internal enum ActionGameplayTimingField
{
    Start,
    End,
    Duration,
}

/// <summary>One selected Gameplay Item's delayed field edits, committed through the Timeline rules.</summary>
internal sealed class ActionGameplayTimingEdit
{
    private readonly ActionAsset _action;
    private readonly GameplayItem _item;
    private readonly string _editorId;
    private readonly int _session;

    internal ActionGameplayTimingEdit(ActionAsset action, GameplayItem item)
    {
        _action = action;
        _item = item;
        _editorId = item.EditorId;
        _session = ActionEditorContext.Shared.SessionVersion;
    }

    internal long Start => _item is RangeGameplayItem range ? range.StartFrame : ((PointGameplayItem)_item).Frame;
    internal long Duration => _item is RangeGameplayItem range ? range.DurationFrames : 1;
    internal long End => Start + Duration;

    internal bool IsCurrent
    {
        get
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            return _action != null && context.CurrentAction == _action && context.SessionVersion == _session &&
                   context.Document.Readiness == ActionEditorReadiness.Ready &&
                   context.PrimarySelection.Kind == ActionSelectionKind.GameplayItem &&
                   string.Equals(context.PrimarySelection.EditorId, _editorId, StringComparison.Ordinal) &&
                   context.Document.ById.TryGetValue(_editorId, out ActionDocumentEntry entry) &&
                   ReferenceEquals(entry.Source, _item);
        }
    }

    internal bool TryCommit(ActionGameplayTimingField field, long value)
    {
        if (!IsCurrent || ActionEditorInteractionGate.IsActive) return false;
        ActionEditorContext context = ActionEditorContext.Shared;
        context.FlushQueuedBindingChange();
        context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
        if (!IsCurrent) return false;

        long start = Start;
        long duration = Duration;
        if (field == ActionGameplayTimingField.Start)
            start = value; // Moving Start preserves Duration.
        else if (_item is RangeGameplayItem && field == ActionGameplayTimingField.Duration)
            duration = value;
        else if (_item is RangeGameplayItem && field == ActionGameplayTimingField.End)
        {
            // Check before subtracting, including LongField input outside supported frame values.
            if (start < 0 || value <= start || value > start + int.MaxValue) return false;
            duration = value - start;
        }
        else
            return false;

        if (start < 0 || start > int.MaxValue || duration < 1 || duration > int.MaxValue) return false;
        ActionTimelineOperationKind kind = _item is PointGameplayItem
            ? ActionTimelineOperationKind.SetPointTiming : ActionTimelineOperationKind.SetRangeTiming;
        ActionTimelineOperationSnapshot snapshot = ActionTimelineOperationSnapshot.Capture(
            context.Document, kind, new[] { _editorId });
        if (snapshot == null) return false;
        ActionTimelineOperationInput input = ActionTimelineOperationInput.Absolute((int)start, (int)duration);
        ActionTimelineOperationResult result = snapshot.Evaluate(input);
        if (result.State == ActionTimelineOperationState.NoChange) return true;
        if (result.State != ActionTimelineOperationState.Allowed) return false;
        // Each completed field is its own Undo step, including consecutive Enter/Tab edits.
        Undo.IncrementCurrentGroup();
        try { return ActionEditorCommands.CommitTimelineOperation(snapshot, input, out _); }
        finally { Undo.IncrementCurrentGroup(); }
    }
}
#endif
