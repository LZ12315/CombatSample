#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionTimelineSnappingTests
{
    [TestCase(7.8d, 40d, true)]
    [TestCase(7.6d, 40d, false)]
    [TestCase(6.8d, 8d, true)]
    [TestCase(6.7d, 8d, false)]
    public void MagneticCapture_UsesActualPointerDistanceBeforeFrameRounding(double delta, double scale, bool snaps)
    {
        Assert.AreEqual(snaps, ActionTimelineInteractionMath.TrySnap(
            delta, new[] { 10 }, new[] { 18 }, scale, _ => true, out var result));
        if (snaps) Assert.AreEqual(8, result.Delta);
    }

    [Test]
    public void MagneticCapture_RetainsTargetThroughJitterThenReleasesForAnotherTarget()
    {
        int[] edges = { 10 };
        int[] targets = { 18, 19, 20 };
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(8d, edges, targets, 10d, _ => true, out var initial));
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(8.8d, edges, targets, 10d, _ => true,
            out var retained, initial));
        Assert.AreEqual(18, retained.TargetFrame, "Small movement should not switch to the newly closer target.");
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(9.7d, edges, targets, 10d, _ => true,
            out var released, retained));
        Assert.AreEqual(20, released.TargetFrame);
    }

    [Test]
    public void MagneticCapture_DropsRemovedOrIllegalRetainedTargets()
    {
        var retained = new ActionTimelineSnapResult(8, 18, 0);
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(8.8d, new[] { 10 }, new[] { 18, 19 }, 10d,
            delta => delta != 8, out var legal, retained));
        Assert.AreEqual(19, legal.TargetFrame);
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(8.8d, new[] { 10 }, new[] { 19 }, 10d,
            _ => true, out var remaining, retained));
        Assert.AreEqual(19, remaining.TargetFrame);
    }

    [Test]
    public void MagneticCapture_PlayheadWinsEqualDistanceButDoesNotOverrideNearerBoundaries()
    {
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(0d, new[] { 10 }, new[] { 9, 11 }, 2d,
            _ => true, out var tied, preferredTargetFrame: 11));
        Assert.AreEqual(11, tied.TargetFrame);
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(-0.8d, new[] { 10 }, new[] { 9, 11 }, 2d,
            _ => true, out var nearer, preferredTargetFrame: 11));
        Assert.AreEqual(9, nearer.TargetFrame);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void PointerMove_StopsAtZeroWhileAuthoringStillRejectsNegativeStarts(int kind)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        AnimationAsset animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var clip = new AnimationClip();
        try
        {
            var lane = new GameplayLane();
            action.Timeline.EditorGameplayLanes.Add(lane);
            if (kind == 0)
            {
                var range = new MotionPolicyItem();
                range.EditorSetTiming(10, 3);
                lane.EditorItems.Add(range);
            }
            else if (kind == 1)
            {
                var point = new ImpulseItem();
                point.EditorSetFrame(10);
                lane.EditorItems.Add(point);
            }
            else
            {
                clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                animation.EditorSetClip(clip);
                var segment = new AnimationSegment();
                segment.EditorSetData(10, animation, 0f, 1f, 1f);
                action.Timeline.EditorAnimationSegments.Add(segment);
            }
            ActionAuthoringIdentity.RepairInvalidIds(action);
            ActionEditorDocument document = ActionEditorDocument.Build(action);
            var snapshot = ActionTimelineOperationSnapshot.Capture(document, ActionTimelineOperationKind.Move,
                document.ContentEntries.Where(entry => entry.Source is GameplayItem || entry.Source is AnimationSegment)
                    .Select(entry => entry.EditorId).ToArray());
            int clamped = snapshot.ConstrainPointerDelta(-1000, out _);
            Assert.AreEqual(-10, clamped);
            var result = snapshot.Evaluate(ActionTimelineOperationInput.Delta(clamped));
            Assert.AreEqual(ActionTimelineOperationState.Allowed, result.State);
            Assert.AreEqual(0, result.Candidates[0].StartFrame);
            Assert.AreEqual(ActionTimelineOperationState.Rejected,
                snapshot.Evaluate(ActionTimelineOperationInput.Delta(-1000)).State);
        }
        finally
        {
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void GroupMove_StopsEarliestItemAtZeroAndPreservesRelativeTiming()
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        try
        {
            var lane = new GameplayLane();
            var range = new MotionPolicyItem();
            range.EditorSetTiming(10, 3);
            var point = new ImpulseItem();
            point.EditorSetFrame(30);
            var last = new MotionPolicyItem();
            last.EditorSetTiming(40, 2);
            lane.EditorItems.Add(range);
            lane.EditorItems.Add(point);
            lane.EditorItems.Add(last);
            action.Timeline.EditorGameplayLanes.Add(lane);
            ActionAuthoringIdentity.RepairInvalidIds(action);
            ActionEditorDocument document = ActionEditorDocument.Build(action);
            var snapshot = ActionTimelineOperationSnapshot.Capture(document, ActionTimelineOperationKind.Move,
                new[] { range.EditorId, point.EditorId, last.EditorId });
            int delta = snapshot.ConstrainPointerDelta(int.MinValue, out _);
            var result = snapshot.Evaluate(ActionTimelineOperationInput.Delta(delta));
            Assert.AreEqual(ActionTimelineOperationState.Allowed, result.State);
            Assert.AreEqual(0, result.ForSource(range).StartFrame);
            Assert.AreEqual(20, result.ForSource(point).StartFrame);
            Assert.AreEqual(30, result.ForSource(last).StartFrame);
            Assert.AreEqual(3, result.ForSource(range).DurationFrames);

            var edges = ActionTimelineInteractionMath.MovingSnapEdges(
                document.GameplayItems, ActionTimelineOperationKind.Move, document.ById[point.EditorId]);
            CollectionAssert.AreEquivalent(new[] { 10, 30, 42 }, edges,
                "Snap the grabbed Point or the group's outer edges, without an artificial edge at Frame 31.");
            CollectionAssert.AreEqual(new[] { 30 }, ActionTimelineInteractionMath.MovingSnapEdges(
                new[] { document.ById[point.EditorId] }, ActionTimelineOperationKind.Move, document.ById[point.EditorId]));
        }
        finally { Object.DestroyImmediate(action); }
    }

    [Test]
    public void ZeroBoundary_DoesNotTurnOverlappingMovesIntoLegalEdits()
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        try
        {
            var lane = new GameplayLane();
            var previous = new MotionPolicyItem();
            previous.EditorSetTiming(0, 3);
            var moved = new MotionPolicyItem();
            moved.EditorSetTiming(10, 3);
            lane.EditorItems.Add(previous);
            lane.EditorItems.Add(moved);
            action.Timeline.EditorGameplayLanes.Add(lane);
            ActionAuthoringIdentity.RepairInvalidIds(action);
            var snapshot = ActionTimelineOperationSnapshot.Capture(ActionEditorDocument.Build(action),
                ActionTimelineOperationKind.Move, new[] { moved.EditorId });
            int clamped = snapshot.ConstrainPointerDelta(-1000, out _);
            Assert.AreEqual(ActionTimelineOperationState.Rejected,
                snapshot.Evaluate(ActionTimelineOperationInput.Delta(clamped)).State);
            Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(-6d, new[] { 10, 13 }, new[] { 0, 3 }, 2d,
                delta => snapshot.Evaluate(ActionTimelineOperationInput.Delta(delta)).State != ActionTimelineOperationState.Rejected,
                out var snap));
            Assert.AreEqual(-7, snap.Delta);
            Assert.AreEqual(3, snap.TargetFrame, "Snap should align the moved Item after its neighbour, not overlap Frame 0.");
        }
        finally { Object.DestroyImmediate(action); }
    }

    [TestCase(2.25d, 2d)]
    [TestCase(2.75d, 3d)]
    [TestCase(4d, 4d)]
    public void DragPause_AlignsPlayheadToNearestFrameAndPreservesDurationEndpoint(double position, double expected)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        double previousPosition = context.PreviewPosition;
        try
        {
            var lane = new GameplayLane();
            var range = new MotionPolicyItem();
            range.EditorSetTiming(0, 4);
            lane.EditorItems.Add(range);
            action.Timeline.EditorGameplayLanes.Add(lane);
            context.SetAction(action);
            ActionEditorPlayback.Play();
            context.SetPreviewPosition(position);
            ActionEditorPlayback.PauseAtNearestFrame();
            Assert.IsFalse(ActionEditorPlayback.IsPlaying);
            Assert.AreEqual(expected, context.PreviewPosition);
        }
        finally
        {
            ActionEditorPlayback.Stop(false);
            context.SetAction(previousAction);
            context.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(action);
        }
    }
}
#endif
