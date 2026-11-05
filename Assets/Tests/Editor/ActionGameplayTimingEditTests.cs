#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionGameplayTimingEditTests
{
    [TestCase((int)ActionGameplayTimingField.Start, 25L, 25, 10, 35L)]
    [TestCase((int)ActionGameplayTimingField.End, 35L, 20, 15, 35L)]
    [TestCase((int)ActionGameplayTimingField.Duration, 5L, 20, 5, 25L)]
    public void CompletedRangeEdit_UpdatesLinkedTimingAndPublishesOnce(
        int field, long value, int start, int duration, long end)
    {
        using (var fixture = new Fixture())
        {
            int notifications = 0;
            ActionEditorChangeFlags observed = ActionEditorChangeFlags.None;
            void Observe(ActionEditorChange change) { notifications++; observed |= change.Flags; }
            ActionEditorContext.Changed += Observe;
            try
            {
                Assert.IsTrue(fixture.Edit.TryCommit((ActionGameplayTimingField)field, value));
                Assert.AreEqual(start, fixture.Range.StartFrame);
                Assert.AreEqual(duration, fixture.Range.DurationFrames);
                Assert.AreEqual(end, fixture.Edit.End);
                Assert.AreEqual(1, notifications);
                Assert.AreNotEqual(ActionEditorChangeFlags.None, observed & ActionEditorChangeFlags.Timing);
                Assert.AreNotEqual(ActionEditorChangeFlags.None, observed & ActionEditorChangeFlags.Preview);
                Assert.AreEqual(start, ActionEditorContext.Shared.Document.ById[fixture.Range.EditorId].StartFrame);
            }
            finally { ActionEditorContext.Changed -= Observe; }
        }
    }

    [TestCase((int)ActionGameplayTimingField.Start, -1L)]
    [TestCase((int)ActionGameplayTimingField.Start, 2147483648L)]
    [TestCase((int)ActionGameplayTimingField.End, 20L)]
    [TestCase((int)ActionGameplayTimingField.End, -9223372036854775808L)]
    [TestCase((int)ActionGameplayTimingField.End, 9223372036854775807L)]
    [TestCase((int)ActionGameplayTimingField.Duration, 0L)]
    [TestCase((int)ActionGameplayTimingField.Duration, -1L)]
    public void InvalidRangeEdit_PreservesAppliedValuesWithoutPublishing(
        int field, long value)
    {
        using (var fixture = new Fixture())
        {
            int version = ActionEditorContext.Shared.DocumentVersion;
            Assert.IsFalse(fixture.Edit.TryCommit((ActionGameplayTimingField)field, value));
            Assert.AreEqual(20L, fixture.Edit.Start);
            Assert.AreEqual(30L, fixture.Edit.End);
            Assert.AreEqual(10L, fixture.Edit.Duration);
            Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
        }
    }

    [Test]
    public void OverlapIsRejectedButAdjacentEndAndMutedConfigurationArePreserved()
    {
        using (var fixture = new Fixture())
        {
            var next = new MotionPolicyItem();
            next.EditorSetTiming(40, 5);
            fixture.Lane.EditorItems.Add(next);
            ActionAuthoringIdentity.RepairInvalidIds(fixture.Action);
            ActionEditorContext.Shared.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Structure);
            Assert.IsFalse(fixture.Edit.TryCommit(ActionGameplayTimingField.End, 41));
            Assert.AreEqual(30L, fixture.Edit.End);

            fixture.Range.EditorSetMuted(true);
            fixture.Range.Config.useGravityScale = true;
            fixture.Range.Config.gravityScale = 0.4f;
            ActionEditorContext.Shared.QueueBindingChange(fixture.Action, ActionEditorChangeFlags.Content);
            Assert.IsTrue(fixture.Edit.TryCommit(ActionGameplayTimingField.End, 40));
            Assert.AreEqual(20, fixture.Range.DurationFrames);
            Assert.IsTrue(fixture.Range.Muted);
            Assert.AreEqual(0.4f, fixture.Range.Config.gravityScale);
        }
    }

    [Test]
    public void PointHasOnlyFrameAndNoChangeDoesNotPublish()
    {
        using (var fixture = new Fixture())
        {
            var point = new ImpulseItem();
            point.EditorSetFrame(10);
            fixture.Lane.EditorItems.Add(point);
            ActionAuthoringIdentity.RepairInvalidIds(fixture.Action);
            ActionEditorContext context = ActionEditorContext.Shared;
            context.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Structure);
            context.Select(ActionSelectionKind.GameplayItem, point.EditorId, false, false);
            var edit = new ActionGameplayTimingEdit(fixture.Action, point);
            Assert.IsFalse(edit.TryCommit(ActionGameplayTimingField.Duration, 5));
            Assert.IsFalse(edit.TryCommit(ActionGameplayTimingField.End, 15));
            Assert.IsTrue(edit.TryCommit(ActionGameplayTimingField.Start, 11));
            Assert.AreEqual(11, point.Frame);
            int version = context.DocumentVersion;
            Assert.IsTrue(edit.TryCommit(ActionGameplayTimingField.Start, 11));
            Assert.AreEqual(version, context.DocumentVersion);
        }
    }

    [Test]
    public void OldSelectionAndSessionCannotCommitDelayedEdits()
    {
        using (var fixture = new Fixture())
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            context.Select(ActionSelectionKind.Action, string.Empty, false, false);
            Assert.IsFalse(fixture.Edit.TryCommit(ActionGameplayTimingField.Start, 25));
            context.Select(ActionSelectionKind.GameplayItem, fixture.Range.EditorId, false, false);
            context.SetAction(null);
            context.SetAction(fixture.Action);
            context.Select(ActionSelectionKind.GameplayItem, fixture.Range.EditorId, false, false);
            Assert.IsFalse(fixture.Edit.TryCommit(ActionGameplayTimingField.Start, 25));
            Assert.AreEqual(20, fixture.Range.StartFrame);
        }
    }

    [Test]
    public void RangeEndRetainsLongPrecisionAndOneEditIsOneUndo()
    {
        using (var fixture = new Fixture())
        {
            Undo.IncrementCurrentGroup();
            Assert.IsTrue(fixture.Edit.TryCommit(ActionGameplayTimingField.Start, int.MaxValue));
            Assert.AreEqual((long)int.MaxValue + 10L, fixture.Edit.End);
            Assert.IsTrue(fixture.Edit.TryCommit(ActionGameplayTimingField.End, (long)int.MaxValue + 5L));
            Assert.AreEqual(5, fixture.Range.DurationFrames);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            ActionEditorContext.Shared.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            var restored = (RangeGameplayItem)fixture.Action.Timeline.GameplayLanes[0].Items[0];
            Assert.AreEqual(int.MaxValue, restored.StartFrame);
            Assert.AreEqual(10, restored.DurationFrames);
            Undo.PerformUndo();
            ActionEditorContext.Shared.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            restored = (RangeGameplayItem)fixture.Action.Timeline.GameplayLanes[0].Items[0];
            Assert.AreEqual(20, restored.StartFrame);
            Assert.AreEqual(10, restored.DurationFrames);
        }
    }

    private sealed class Fixture : System.IDisposable
    {
        internal readonly ActionAsset Action;
        internal readonly GameplayLane Lane;
        internal readonly MotionPolicyItem Range;
        internal readonly ActionGameplayTimingEdit Edit;
        private readonly ActionAsset _previousAction;
        private readonly double _previousPosition;

        internal Fixture()
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            _previousAction = context.CurrentAction;
            _previousPosition = context.PreviewPosition;
            Action = ScriptableObject.CreateInstance<ActionAsset>();
            Lane = new GameplayLane();
            Range = new MotionPolicyItem();
            Range.EditorSetTiming(20, 10);
            Lane.EditorItems.Add(Range);
            Action.Timeline.EditorGameplayLanes.Add(Lane);
            ActionAuthoringIdentity.RepairInvalidIds(Action);
            context.SetAction(Action);
            context.Select(ActionSelectionKind.GameplayItem, Range.EditorId, false, false);
            Edit = new ActionGameplayTimingEdit(Action, Range);
        }

        public void Dispose()
        {
            Undo.ClearUndo(Action);
            ActionEditorPlayback.Stop(false);
            ActionEditorContext.Shared.SetAction(_previousAction);
            ActionEditorContext.Shared.SetPreviewPosition(_previousPosition);
            Object.DestroyImmediate(Action);
        }
    }
}

public sealed class ActionSourceTimingEditTests
{
    [Test]
    public void FullClip_KeepsRateAndCommitsSourceStartWithDuration()
    {
        using (var fixture = new Fixture())
        {
            int notifications = 0;
            void Observe(ActionEditorChange change) { notifications++; }
            ActionEditorContext.Changed += Observe;
            try
            {
                ActionDetailsWindow.TimingDraft draft = fixture.Draft(fixture.Root);
                Assert.IsTrue(draft.UseFullClip());
                Assert.AreEqual(12, fixture.Root.DurationFrames, "Full Clip must only change the draft.");
                Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
                Assert.AreEqual(0, notifications);
                Assert.IsTrue(draft.TryApply(out string message), message);
                Assert.AreEqual(10, fixture.Root.StartFrame);
                Assert.AreEqual(30, fixture.Root.DurationFrames);
                Assert.AreEqual(0f, fixture.Root.Config.sourceStartTime);
                Assert.AreEqual(2f, fixture.Root.Config.playRate);
                Assert.AreEqual(1, notifications);

                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                ActionEditorContext.Shared.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
                var restored = (RootMotionItem)fixture.Action.Timeline.GameplayLanes[0].Items[0];
                Assert.AreEqual(12, restored.DurationFrames);
                Assert.AreEqual(0.25f, restored.Config.sourceStartTime);
                Assert.AreEqual(2f, restored.Config.playRate);
                Assert.AreEqual(0.25f + (float)(12d / 60d * 2f), restored.SourceEndTime);
                Undo.PerformRedo();
                ActionEditorContext.Shared.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
                restored = (RootMotionItem)fixture.Action.Timeline.GameplayLanes[0].Items[0];
                Assert.AreEqual(1f, restored.SourceEndTime);
                Assert.AreEqual(30, restored.DurationFrames);
            }
            finally { ActionEditorContext.Changed -= Observe; }
        }
    }

    [Test]
    public void FullClip_RejectsOverlapWithoutChangingSourceStartOrDuration()
    {
        using (var fixture = new Fixture())
        {
            var neighbor = new MotionPolicyItem();
            neighbor.EditorSetTiming(35, 5);
            fixture.Lane.EditorItems.Add(neighbor);
            ActionAuthoringIdentity.RepairInvalidIds(fixture.Action);
            ActionEditorContext.Shared.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Structure);
            int version = ActionEditorContext.Shared.DocumentVersion;

            ActionDetailsWindow.TimingDraft draft = fixture.Draft(fixture.Root);
            Assert.IsTrue(draft.UseFullClip());
            Assert.IsFalse(draft.TryApply(out string message));
            StringAssert.Contains(neighbor.EditorId, message);
            Assert.AreEqual(0f, draft.SourceStartTime, "A rejected Apply must retain the draft.");
            Assert.AreEqual(1f, draft.SourceEndTime);
            Assert.AreEqual(12, fixture.Root.DurationFrames);
            Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
            Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
        }
    }

    [Test]
    public void AnimationConflictRejectsWholeDraftAndNamesTheBlockingSegment()
    {
        using (var fixture = new Fixture())
        {
            AnimationSegment animation = fixture.AddAnimation();
            var neighbor = new AnimationSegment();
            neighbor.EditorSetData(40, animation.AnimationAsset, 0f, 0.125f, 1f);
            fixture.Action.Timeline.EditorAnimationSegments.Add(neighbor);
            ActionAuthoringIdentity.RepairInvalidIds(fixture.Action);
            ActionEditorContext.Shared.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Structure);
            var draft = fixture.Draft(animation);
            draft.PlayRate = 0.5f;
            Assert.IsFalse(draft.TryApply(out string message));
            StringAssert.Contains(neighbor.EditorId, message);
            Assert.AreEqual(2f, animation.PlayRate);
            Assert.AreEqual(0.5f, draft.PlayRate);
            Assert.AreEqual(40, neighbor.StartFrame);
        }
    }

    [Test]
    public void RootMotionRangeResizeKeepsSourceStartAndRateWhileMoveKeepsWholeSourceRange()
    {
        using (var fixture = new Fixture())
        {
            fixture.Root.EditorSetSource(10, fixture.Root.Config.animationAsset, 0.25f, 0.75f, 1f);
            ActionEditorContext context = ActionEditorContext.Shared;
            context.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Timing);
            var snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                ActionTimelineOperationKind.ResizeLeft, new[] { fixture.Root.EditorId });
            Assert.IsTrue(ActionEditorCommands.CommitTimelineOperation(snapshot,
                ActionTimelineOperationInput.Delta(15), out string message), message);
            Assert.AreEqual(25, fixture.Root.StartFrame);
            Assert.AreEqual(15, fixture.Root.DurationFrames);
            Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
            Assert.AreEqual(0.5f, fixture.Root.SourceEndTime);
            Assert.AreEqual(1f, fixture.Root.Config.playRate);
            snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                ActionTimelineOperationKind.Move, new[] { fixture.Root.EditorId });
            Assert.IsTrue(ActionEditorCommands.CommitTimelineOperation(snapshot,
                ActionTimelineOperationInput.Delta(10), out message), message);
            Assert.AreEqual(35, fixture.Root.StartFrame);
            Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
            Assert.AreEqual(0.5f, fixture.Root.SourceEndTime);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SourceDraft_RatePreservesWindowAndDoesNotAffectAppliedDataBeforeApply(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            AnimationSegment animation = fixture.AddAnimation();
            object source = rootMotion ? (object)fixture.Root : animation;
            var draft = fixture.Draft(source);
            float sourceStart = draft.SourceStartTime;
            float sourceEnd = draft.SourceEndTime;
            int version = ActionEditorContext.Shared.DocumentVersion;
            draft.PlayRate = 1f;
            Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
            Assert.AreEqual(12, fixture.Root.DurationFrames);
            Assert.AreEqual(ActionTimelineOperationState.Allowed, draft.Evaluate().State);
            Assert.IsTrue(draft.TryApply(out string message), message);
            if (rootMotion)
            {
                Assert.AreEqual(sourceStart, fixture.Root.Config.sourceStartTime);
                Assert.AreEqual(sourceEnd, fixture.Root.SourceEndTime);
                Assert.AreEqual(24, fixture.Root.DurationFrames);
                Assert.AreEqual(2f, animation.PlayRate, "RootMotion edits must not synchronize Animation.");
            }
            else
            {
                Assert.AreEqual(sourceStart, animation.SourceStartTime);
                Assert.AreEqual(sourceEnd, animation.SourceEndTime);
                Assert.AreEqual(24, animation.DerivedDurationFrames);
                Assert.AreEqual(2f, fixture.Root.Config.playRate, "Animation edits must not synchronize RootMotion.");
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FullClipDraft_KeepsPendingRateAndCanBeDiscardedWithoutPublishing(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            object source = rootMotion ? (object)fixture.Root : fixture.AddAnimation();
            var draft = fixture.Draft(source);
            int version = ActionEditorContext.Shared.DocumentVersion;
            draft.PlayRate = 0.5f;
            Assert.IsTrue(draft.UseFullClip());
            Assert.AreEqual(0.5f, draft.PlayRate);
            Assert.AreEqual(0f, draft.SourceStartTime);
            Assert.AreEqual(1f, draft.SourceEndTime);
            Assert.AreEqual(120, draft.Evaluate().Candidates[0].DurationFrames);
            var reverted = fixture.Draft(source); // Discarding a draft is simply re-reading applied data.
            Assert.AreEqual(2f, reverted.PlayRate);
            Assert.AreEqual(0.25f, reverted.SourceStartTime);
            Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReplacingSourceAssetUsesNewFullClipAndKeepsRateWithoutChangingAppliedData(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            var replacement = ScriptableObject.CreateInstance<AnimationAsset>();
            var clip = new AnimationClip();
            try
            {
                clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 0.125f, 1f));
                replacement.EditorSetClip(clip);
                AnimationSegment animation = fixture.AddAnimation();
                object source = rootMotion ? (object)fixture.Root : animation;
                var draft = fixture.Draft(source);
                string applied = EditorJsonUtility.ToJson(fixture.Action);
                draft.PlayRate = 0.5f;
                draft.SetAnimationAsset(replacement);
                Assert.AreEqual(0f, draft.SourceStartTime);
                Assert.AreEqual(clip.length, draft.SourceEndTime);
                Assert.AreEqual(0.5f, draft.PlayRate);
                Assert.AreEqual(ActionTimelineOperationState.Allowed, draft.Evaluate().State);
                ActionEditorContext context = ActionEditorContext.Shared;
                Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
                Assert.AreEqual(applied, EditorJsonUtility.ToJson(fixture.Action));
                object preview = context.PreviewDocument.ById[context.PrimarySelection.EditorId].Source;
                Assert.AreSame(replacement, rootMotion ? ((RootMotionItem)preview).Config.animationAsset :
                    ((AnimationSegment)preview).AnimationAsset);
                Assert.AreEqual(15, rootMotion ? ((RootMotionItem)preview).DurationFrames :
                    ((AnimationSegment)preview).DerivedDurationFrames);
                context.ClearSourcePreview(draft);
            }
            finally
            {
                ActionEditorContext.Shared.ClearSourcePreview();
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(clip);
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ValidSourceDraftPreviewsWithoutChangingAuthorityAndRevertRestoresAppliedDuration(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            AnimationSegment animation = fixture.AddAnimation();
            var draft = fixture.Draft(rootMotion ? (object)fixture.Root : animation);
            ActionEditorContext context = ActionEditorContext.Shared;
            ActionEditorDocument document = context.Document;
            int version = context.DocumentVersion;
            string applied = EditorJsonUtility.ToJson(fixture.Action);
            draft.PlayRate = 1f;
            Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            ActionAsset preview = context.PreviewAction;
            Assert.AreNotSame(fixture.Action, preview);
            Assert.AreSame(fixture.Action, context.CurrentAction);
            Assert.AreSame(document, context.Document);
            Assert.AreEqual(version, context.DocumentVersion);
            Assert.AreEqual(applied, EditorJsonUtility.ToJson(fixture.Action));
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreSame(preview, context.PreviewAction, "Preview object notifications must not invalidate the unchanged source asset.");
            Assert.AreEqual(34, context.PreviewDurationFrames);
            object edited = context.PreviewDocument.ById[context.PrimarySelection.EditorId].Source;
            Assert.AreEqual(1f, rootMotion ? ((RootMotionItem)edited).Config.playRate : ((AnimationSegment)edited).PlayRate);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
            Assert.AreEqual(2f, animation.PlayRate);
            context.SetPreviewPosition(30d);
            Assert.AreEqual(30d, context.PreviewPosition, "Scrubbing must use the draft duration.");
            context.ClearSourcePreview(draft);
            Assert.IsTrue(preview == null, "Discarded preview copies must be destroyed.");
            Assert.AreSame(fixture.Action, context.PreviewAction);
            Assert.AreEqual(document.DurationFrames, context.PreviewDurationFrames);
            Assert.LessOrEqual(context.PreviewPosition, document.DurationFrames);
            Assert.AreEqual(applied, EditorJsonUtility.ToJson(fixture.Action));
            Assert.AreEqual(version, context.DocumentVersion);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ApplyingPreviewCommitsOnlyToOriginalAndUndoRestoresOriginalSource(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            AnimationSegment animation = fixture.AddAnimation();
            var draft = fixture.Draft(rootMotion ? (object)fixture.Root : animation);
            ActionEditorContext context = ActionEditorContext.Shared;
            string id = context.PrimarySelection.EditorId;
            draft.PlayRate = 1f;
            Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            ActionAsset preview = context.PreviewAction;
            Assert.IsTrue(draft.TryApply(out string message), message);
            Assert.IsTrue(preview == null);
            Assert.AreSame(fixture.Action, context.PreviewAction);
            Assert.AreEqual(1f, rootMotion ? fixture.Root.Config.playRate : animation.PlayRate);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            context.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            object restored = context.Document.ById[id].Source;
            Assert.AreEqual(2f, rootMotion ? ((RootMotionItem)restored).Config.playRate : ((AnimationSegment)restored).PlayRate);
            Assert.AreSame(fixture.Action, context.PreviewAction);
            Undo.PerformRedo();
            context.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            restored = context.Document.ById[id].Source;
            Assert.AreEqual(1f, rootMotion ? ((RootMotionItem)restored).Config.playRate : ((AnimationSegment)restored).PlayRate);
        }
    }

    [Test]
    public void UndoSourceEndChangeInvalidatesPreviewEvenWhenRoundedDurationDoesNotChange()
    {
        using (var fixture = new Fixture())
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            float originalEnd = fixture.Root.SourceEndTime;
            int originalDuration = fixture.Root.DurationFrames;
            var draft = fixture.Draft(fixture.Root);
            draft.SourceEndTime -= 0.002f;
            Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            Assert.IsTrue(draft.TryApply(out string message), message);
            Assert.AreEqual(originalDuration, fixture.Root.DurationFrames);
            bool invalidated = false;
            void Observe(ActionEditorChange change)
            {
                if (change.Action == fixture.Action && change.Origin == ActionEditorChangeOrigin.UndoRedo)
                    invalidated |= ActionPreviewPanel.RequiresDataInvalidation(change.Flags);
            }
            ActionEditorContext.Changed += Observe;
            try
            {
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                context.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
                var restored = (RootMotionItem)context.Document.ById[fixture.Root.EditorId].Source;
                Assert.AreEqual(originalEnd, restored.SourceEndTime);
                Assert.AreEqual(originalDuration, restored.DurationFrames);
                Assert.IsTrue(invalidated, "Undo must restore the applied motion window, not leave cached draft output.");
            }
            finally { ActionEditorContext.Changed -= Observe; }
        }
    }

    [Test]
    public void ConflictAndSelectionChangeDiscardPreviewCopiesWithoutDiscardingRejectedInput()
    {
        using (var fixture = new Fixture())
        {
            var neighbor = new MotionPolicyItem();
            neighbor.EditorSetTiming(35, 5);
            fixture.Lane.EditorItems.Add(neighbor);
            ActionAuthoringIdentity.RepairInvalidIds(fixture.Action);
            ActionEditorContext context = ActionEditorContext.Shared;
            context.ApplyAssetChange(fixture.Action, ActionEditorChangeFlags.Structure);
            var draft = fixture.Draft(fixture.Root);
            draft.PlayRate = 1f;
            Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            ActionAsset preview = context.PreviewAction;
            draft.PlayRate = 0.5f;
            Assert.IsFalse(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            Assert.IsTrue(preview == null);
            Assert.AreSame(fixture.Action, context.PreviewAction);
            Assert.AreEqual(0.5f, draft.PlayRate);
            StringAssert.Contains(neighbor.EditorId, draft.Evaluate().Message);
            draft.PlayRate = 1f;
            Assert.IsTrue(context.SetSourcePreview(draft, draft.Snapshot, draft.Input));
            preview = context.PreviewAction;
            context.SelectAction();
            Assert.IsTrue(preview == null);
            Assert.AreSame(fixture.Action, context.PreviewAction);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidRateCannotPartiallyApplyRootMotion(float rate)
    {
        using (var fixture = new Fixture())
        {
            var draft = fixture.Draft(fixture.Root);
            draft.PlayRate = rate;
            draft.SourceStartTime = 0f;
            Assert.IsFalse(draft.TryApply(out string message));
            StringAssert.Contains("Play Rate", message);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
            Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
            Assert.AreEqual(12, fixture.Root.DurationFrames);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SourceEndMustBeFiniteIncreasingAndWithinClip(bool rootMotion)
    {
        using (var fixture = new Fixture())
        {
            object source = rootMotion ? (object)fixture.Root : fixture.AddAnimation();
            var draft = fixture.Draft(source);
            int version = ActionEditorContext.Shared.DocumentVersion;
            foreach (float invalidEnd in new[] { 1.5f, 0.125f, float.NaN })
            {
                draft.SourceEndTime = invalidEnd;
                Assert.IsFalse(draft.TryApply(out string message));
                StringAssert.Contains("Source End", message);
                Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
            }
        }
    }

    [Test]
    public void RootMotionDraftExpiresWhenAppliedSourceChangesEvenIfDurationDoesNot()
    {
        using (var fixture = new Fixture())
        {
            var draft = fixture.Draft(fixture.Root);
            fixture.Root.Config.animationAsset = null;
            Assert.IsFalse(draft.TryApply(out string message));
            Assert.IsNotEmpty(message);
            Assert.IsNull(fixture.Root.Config.animationAsset);
        }
    }

    [Test]
    public void LegacySourceRangeIsNotClampedAndInvalidRangeRequiresExplicitRepair()
    {
        using (var fixture = new Fixture())
        {
            fixture.Root.EditorSetTiming(10, 60);
            float oldEnd = fixture.Root.SourceEndTime;
            Assert.Greater(oldEnd, 1f);
            var draft = fixture.Draft(fixture.Root);
            Assert.AreEqual(oldEnd, draft.SourceEndTime);
            Assert.IsFalse(draft.TryApply(out _));
            Assert.AreEqual(oldEnd, fixture.Root.SourceEndTime);
            Assert.AreEqual(60, fixture.Root.DurationFrames);
            Assert.IsTrue(draft.UseFullClip());
            Assert.IsTrue(draft.TryApply(out string message), message);
            Assert.AreEqual(1f, fixture.Root.SourceEndTime);
            Assert.AreEqual(30, fixture.Root.DurationFrames);
        }
    }

    private sealed class Fixture : System.IDisposable
    {
        internal readonly ActionAsset Action;
        internal readonly GameplayLane Lane;
        internal readonly RootMotionItem Root;
        private readonly AnimationAsset _animation;
        private readonly AnimationClip _clip;
        private readonly ActionAsset _previousAction;
        private readonly double _previousPosition;

        internal Fixture()
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            _previousAction = context.CurrentAction;
            _previousPosition = context.PreviewPosition;
            Action = ScriptableObject.CreateInstance<ActionAsset>();
            _animation = ScriptableObject.CreateInstance<AnimationAsset>();
            _clip = new AnimationClip();
            _clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            _animation.EditorSetClip(_clip);
            Lane = new GameplayLane();
            Root = new RootMotionItem();
            Root.EditorSetTiming(10, 12);
            Root.Config.animationAsset = _animation;
            Root.Config.sourceStartTime = 0.25f;
            Root.Config.playRate = 2f;
            Lane.EditorItems.Add(Root);
            Action.Timeline.EditorGameplayLanes.Add(Lane);
            ActionAuthoringIdentity.RepairInvalidIds(Action);
            context.SetAction(Action);
            context.Select(ActionSelectionKind.GameplayItem, Root.EditorId, false, false);
        }

        internal AnimationSegment AddAnimation()
        {
            var animation = new AnimationSegment();
            animation.EditorSetData(10, _animation, 0.25f, Root.SourceEndTime, 2f);
            Action.Timeline.EditorAnimationSegments.Add(animation);
            ActionAuthoringIdentity.RepairInvalidIds(Action);
            ActionEditorContext.Shared.ApplyAssetChange(Action, ActionEditorChangeFlags.Structure);
            return animation;
        }

        internal ActionDetailsWindow.TimingDraft Draft(object source)
        {
            string id = source is RootMotionItem root ? root.EditorId : ((AnimationSegment)source).EditorId;
            var kind = source is RootMotionItem ? ActionSelectionKind.GameplayItem : ActionSelectionKind.AnimationSegment;
            ActionEditorContext context = ActionEditorContext.Shared;
            if (context.PrimarySelection.Kind != kind || context.PrimarySelection.EditorId != id)
                context.Select(kind, id, false, false);
            return ActionDetailsWindow.TimingDraft.Create(context.Document, context.Document.ById[id]);
        }

        public void Dispose()
        {
            Undo.ClearUndo(Action);
            ActionEditorPlayback.Stop(false);
            ActionEditorContext.Shared.SetAction(_previousAction);
            ActionEditorContext.Shared.SetPreviewPosition(_previousPosition);
            Object.DestroyImmediate(Action);
            Object.DestroyImmediate(_animation);
            Object.DestroyImmediate(_clip);
        }
    }
}
#endif
