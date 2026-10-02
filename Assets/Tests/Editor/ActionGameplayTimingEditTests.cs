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

public sealed class ActionRootMotionFullClipTests
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
                Assert.IsTrue(ActionEditorCommands.UseFullRootMotionClip(fixture.Action, fixture.Root.EditorId));
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

            Assert.IsFalse(ActionEditorCommands.UseFullRootMotionClip(fixture.Action, fixture.Root.EditorId));
            Assert.AreEqual(12, fixture.Root.DurationFrames);
            Assert.AreEqual(0.25f, fixture.Root.Config.sourceStartTime);
            Assert.AreEqual(2f, fixture.Root.Config.playRate);
            Assert.AreEqual(version, ActionEditorContext.Shared.DocumentVersion);
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
