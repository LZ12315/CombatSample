#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionPreviewArchitectureTests
{
    [Test]
    public void EditorContext_CoalescesBindingChangesAndDropsCallbacksFromOldSession()
    {
        ActionAsset action = CreateActionAsset();
        ActionAsset replacement = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        int bindingNotifications = 0;
        ActionEditorChange observed = default;
        void OnChanged(ActionEditorChange change)
        {
            if (change.Origin != ActionEditorChangeOrigin.Binding)
                return;
            bindingNotifications++;
            observed = change;
        }

        ActionEditorContext.Changed += OnChanged;
        try
        {
            context.SetAction(action);
            int version = context.DocumentVersion;
            lane.EditorSetName("Binding Name");
            lane.EditorSetMuted(true);
            context.QueueBindingChange(action, ActionEditorChangeFlags.Content);
            context.QueueBindingChange(action, ActionEditorChangeFlags.Presentation);
            context.FlushQueuedBindingChange();

            Assert.AreEqual(1, bindingNotifications);
            Assert.AreEqual(version + 1, context.DocumentVersion);
            Assert.AreEqual(
                ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation,
                observed.Flags & (ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation));

            context.QueueBindingChange(action, ActionEditorChangeFlags.Content);
            context.SetAction(replacement);
            context.FlushQueuedBindingChange();
            Assert.AreEqual(1, bindingNotifications, "A delayed callback from the previous Action session must be discarded.");
            Assert.AreSame(replacement, context.CurrentAction);
        }
        finally
        {
            ActionEditorContext.Changed -= OnChanged;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(replacement);
        }
    }

    [Test]
    public void EditorContext_ExternalNameChangeIsPresentationOnlyAndDoesNotStopPlayback()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin != ActionEditorChangeOrigin.ObjectChange) return;
            observed = change;
            notifications++;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            ActionEditorPlayback.Play();
            lane.EditorSetName("Renamed Externally");
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(ActionEditorChangeFlags.Presentation, observed.Flags);
            Assert.IsTrue(ActionEditorPlayback.IsPlaying);
            Assert.IsFalse(ActionPreviewPanel.RequiresDataInvalidation(observed.Flags));
            Assert.IsFalse(ActionPreviewPanel.RequiresRepaint(observed.Flags));
            Assert.AreEqual("Renamed Externally", context.Document.Lanes[0].LaneName);
        }
        finally
        {
            ActionEditorPlayback.Stop(false);
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorContext_ExternalMuteChangeIsContentAndPresentationWithoutStructuralInvalidation()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin == ActionEditorChangeOrigin.ObjectChange) observed = change;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            lane.EditorSetMuted(true);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);

            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Content);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Presentation);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Preview);
            Assert.AreEqual(ActionEditorChangeFlags.None,
                observed.Flags & (ActionEditorChangeFlags.Structure |
                                  ActionEditorChangeFlags.Timing |
                                  ActionEditorChangeFlags.PreviewResources));
            Assert.IsTrue(ActionPreviewPanel.RequiresDataInvalidation(observed.Flags));
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorContext_ExternalRootMotionConfigurationAndBakeChangesInvalidateVisualDataOnly()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out RootMotionItem root);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin != ActionEditorChangeOrigin.ObjectChange) return;
            observed = change;
            notifications++;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            root.Config.playRate = 2f;
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(1, notifications);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Content);
            Assert.AreEqual(ActionEditorChangeFlags.None,
                observed.Flags & (ActionEditorChangeFlags.Structure |
                                  ActionEditorChangeFlags.Timing |
                                  ActionEditorChangeFlags.PreviewResources));

            var changedTrajectory = new RootMotionTrajectory();
            changedTrajectory.EditorSetData(clip, ActionTimelineData.FrameRate, 1f, 2, "changed-bake",
                new[] { 0f, 1f },
                new[] { Vector3.zero, new Vector3(2f, 0f, 0f) },
                new[] { Quaternion.identity, Quaternion.identity });
            animation.EditorSetRootMotionData(changedTrajectory);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(2, notifications);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Content);
            Assert.AreEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.PreviewResources);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void EditorContext_RigPreviewPrefabChangeInvalidatesResourcesWithoutRebuildingDocument()
    {
        ActionAsset action = CreateActionAsset();
        AnimationAsset animation = ScriptableObject.CreateInstance<AnimationAsset>();
        AnimationRigAsset rig = ScriptableObject.CreateInstance<AnimationRigAsset>();
        var clip = new AnimationClip();
        var firstPrefab = new GameObject("First Preview");
        var secondPrefab = new GameObject("Second Preview");
        animation.EditorSetClip(clip);
        rig.EditorSet(null, firstPrefab);
        animation.EditorSetAnimationRigAsset(rig);
        var segment = new AnimationSegment();
        segment.EditorSetData(0, animation, 0f, 1f, 1f);
        action.Timeline.EditorAnimationSegments.Add(segment);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin == ActionEditorChangeOrigin.ObjectChange) observed = change;
        }

        try
        {
            context.SetAction(action);
            int documentVersion = context.DocumentVersion;
            ActionEditorContext.Changed += Observe;
            rig.EditorSet(null, secondPrefab);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);

            Assert.AreEqual(ActionEditorChangeFlags.PreviewResources, observed.Flags);
            Assert.AreEqual(documentVersion, context.DocumentVersion);
            Assert.AreSame(secondPrefab, ActionPreviewCharacterResolver.Resolve(action, null));
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(firstPrefab);
            Object.DestroyImmediate(secondPrefab);
        }
    }

    [Test]
    public void EditorCommand_AbsorbsPendingBindingFlagsAndPreventsDuplicateExternalNotification()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Action != action) return;
            observed = change;
            notifications++;
        }

        try
        {
            context.SetAction(action);
            lane.EditorSetName("Pending Name");
            context.QueueBindingChange(action, ActionEditorChangeFlags.Presentation);
            ActionEditorContext.Changed += Observe;
            Assert.IsTrue(ActionEditorCommands.SetMuted(action, lane.EditorId, true));

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(ActionEditorChangeOrigin.Command, observed.Origin);
            Assert.AreNotEqual(0, observed.Flags & ActionEditorChangeFlags.Content);
            Assert.AreNotEqual(0, observed.Flags & ActionEditorChangeFlags.Presentation);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual("Pending Name", context.Document.Lanes[0].LaneName);
            Assert.IsTrue(context.Document.Lanes[0].Muted);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorBindingCallbackAfterCommandDoesNotRepublishHandledState()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Action == action) notifications++;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            Assert.IsTrue(ActionEditorCommands.RenameLane(action, lane.EditorId, "Handled Name"));
            int version = context.DocumentVersion;
            Assert.AreEqual(1, notifications);

            // UI Toolkit may deliver its property tracking callback after another path has already
            // published the same serialized write.
            context.QueueBindingChange(action, ActionEditorChangeFlags.Presentation);
            context.FlushQueuedBindingChange();

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(version, context.DocumentVersion);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorCommandPublishesUnreportedExternalChangeBeforeAdvancingObservedState()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out _);
        GameplayLane lane = action.Timeline.EditorGameplayLanes[0];
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Action != action) return;
            observed = change;
            notifications++;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            var trajectory = new RootMotionTrajectory();
            trajectory.EditorSetData(clip, 60, 1f, 1, "unreported-bake",
                new[] { 0f, 1f }, new[] { Vector3.zero, Vector3.right * 120f },
                new[] { Quaternion.identity, Quaternion.identity });
            animation.EditorSetRootMotionData(trajectory);
            // Rename declares Presentation only. Content must come from the pending bake change.
            int version = context.DocumentVersion;
            Assert.IsTrue(ActionEditorCommands.RenameLane(action, lane.EditorId, "Renamed Lane"));

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(version + 1, context.DocumentVersion);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Presentation);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Content);
            Assert.IsTrue(ActionPreviewPanel.RequiresDataInvalidation(observed.Flags));
            Assert.AreEqual("Renamed Lane", context.Document.Lanes[0].LaneName);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(version + 1, context.DocumentVersion);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void PreviewCharacterChangePublishesPendingDataTogetherAndDoesNotRepublish(int pendingChange)
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var character = new GameObject("Temporary Preview Character");
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        GameObject previousCharacter = context.PreviewCharacter;
        int notifications = 0;
        ActionEditorChange observed = default;
        bool documentWasCurrentAtNotification = false;
        void Observe(ActionEditorChange change)
        {
            if (change.Action != action) return;
            notifications++;
            observed = change;
            documentWasCurrentAtNotification = context.Document.Lanes[0].LaneName == lane.Name;
        }

        try
        {
            context.SetAction(action);
            int version = context.DocumentVersion;
            if (pendingChange != 0) lane.EditorSetName("Pending Name");
            if (pendingChange == 1)
                context.QueueBindingChange(action, ActionEditorChangeFlags.Presentation);
            ActionEditorContext.Changed += Observe;

            context.SetPreviewCharacter(character);

            Assert.AreEqual(1, notifications);
            Assert.IsTrue(documentWasCurrentAtNotification);
            Assert.AreSame(character, context.PreviewCharacter);
            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.PreviewCharacter);
            Assert.AreEqual(pendingChange != 0,
                (observed.Flags & ActionEditorChangeFlags.Presentation) != 0);
            Assert.AreEqual(version + (pendingChange != 0 ? 1 : 0), context.DocumentVersion);

            context.FlushQueuedBindingChange();
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            // A tracking callback arriving after the character change is also redundant.
            context.QueueBindingChange(action, ActionEditorChangeFlags.Presentation);
            context.FlushQueuedBindingChange();
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(version + (pendingChange != 0 ? 1 : 0), context.DocumentVersion);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            context.SetPreviewCharacter(previousCharacter);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(character);
        }
    }

    [Test]
    public void ExternalUndoStopsPlaybackOnlyWhenObservedDataChanged()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        int notifications = 0;
        ActionEditorChange observed = default;
        void Observe(ActionEditorChange change)
        {
            if (change.Action != action) return;
            notifications++;
            observed = change;
        }

        try
        {
            context.SetAction(action);
            ActionEditorPlayback.Play();
            ActionEditorContext.Changed += Observe;
            int version = context.DocumentVersion;
            context.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            Assert.IsTrue(ActionEditorPlayback.IsPlaying);
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(version, context.DocumentVersion);

            lane.EditorSetName("Name After Undo");
            context.RefreshExternal(ActionEditorChangeOrigin.UndoRedo);
            Assert.IsFalse(ActionEditorPlayback.IsPlaying);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(ActionEditorChangeFlags.Presentation | ActionEditorChangeFlags.Playback,
                observed.Flags);
            Assert.AreEqual(version + 1, context.DocumentVersion);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            ActionEditorPlayback.Stop(false);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorContext_RigBakeSettingsChangeRefreshesValidationWithoutRebuildingPreviewResources()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out _);
        AnimationRigAsset rig = ScriptableObject.CreateInstance<AnimationRigAsset>();
        var bakeRig = new GameObject("Bake Rig");
        bakeRig.AddComponent<Animator>();
        rig.EditorSet(bakeRig, null, 60, 0.001f, 0.1f);
        animation.EditorSetAnimationRigAsset(rig);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        ActionEditorChange observed = default;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin == ActionEditorChangeOrigin.ObjectChange) observed = change;
        }

        try
        {
            context.SetAction(action);
            int version = context.DocumentVersion;
            ActionEditorContext.Changed += Observe;
            rig.EditorSet(bakeRig, null, 30, 0.002f, 0.2f);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);

            Assert.AreNotEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.Content);
            Assert.AreEqual(ActionEditorChangeFlags.None,
                observed.Flags & ActionEditorChangeFlags.PreviewResources);
            Assert.AreEqual(version + 1, context.DocumentVersion);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(bakeRig);
        }
    }

    [Test]
    public void EditorContext_ObservesEveryTrajectorySampleAndToleratesMismatchedArrays()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out _);
        var initial = new RootMotionTrajectory();
        initial.EditorSetData(clip, 60, 1f, 1, "same-metadata",
            new[] { 0f, 0.5f, 1f },
            new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f) },
            new[] { Quaternion.identity, Quaternion.identity, Quaternion.identity });
        animation.EditorSetRootMotionData(initial);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            if (change.Origin == ActionEditorChangeOrigin.ObjectChange) notifications++;
        }

        try
        {
            context.SetAction(action);
            ActionEditorContext.Changed += Observe;
            var middleChanged = new RootMotionTrajectory();
            middleChanged.EditorSetData(clip, 60, 1f, 1, "same-metadata",
                new[] { 0f, 0.5f, 1f },
                new[] { Vector3.zero, new Vector3(9f, 0f, 0f), new Vector3(2f, 0f, 0f) },
                new[] { Quaternion.identity, Quaternion.identity, Quaternion.identity });
            animation.EditorSetRootMotionData(middleChanged);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(1, notifications, "A changed middle sample must invalidate visual data.");

            var malformed = new RootMotionTrajectory();
            malformed.EditorSetData(clip, 60, 1f, 1, "same-metadata",
                new[] { 0f, 1f },
                new[] { Vector3.zero },
                new[] { Quaternion.identity, Quaternion.identity });
            animation.EditorSetRootMotionData(malformed);
            Assert.DoesNotThrow(() => context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange));
            Assert.AreEqual(2, notifications);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void TimelineOperationSnapshot_CapturedAfterBindingFlushUsesPublishedCurrentData()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var selected = new MotionPolicyItem();
        selected.EditorSetTiming(0, 2);
        var other = new MotionPolicyItem();
        other.EditorSetTiming(5, 2);
        lane.EditorItems.Add(selected);
        lane.EditorItems.Add(other);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        try
        {
            context.SetAction(action);
            int version = context.DocumentVersion;
            other.EditorSetTiming(3, 2);
            context.QueueBindingChange(action, ActionEditorChangeFlags.Timing);
            context.FlushQueuedBindingChange();

            Assert.AreEqual(version + 1, context.DocumentVersion);
            Assert.AreEqual(3, context.Document.ById[other.EditorId].StartFrame);
            ActionTimelineOperationSnapshot snapshot = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.Move, new[] { selected.EditorId });
            Assert.IsTrue(snapshot.MatchesCurrentSource(out string message), message);
            Assert.AreEqual(ActionTimelineOperationState.Allowed,
                snapshot.Evaluate(ActionTimelineOperationInput.Delta(1)).State);
        }
        finally
        {
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void TimelinePointerResize_StopsAtAdjacentItems()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var previous = new MotionPolicyItem();
        previous.EditorSetTiming(0, 3);
        var selected = new MotionPolicyItem();
        selected.EditorSetTiming(5, 5);
        var next = new MotionPolicyItem();
        next.EditorSetTiming(12, 2);
        lane.EditorItems.Add(previous);
        lane.EditorItems.Add(selected);
        lane.EditorItems.Add(next);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        try
        {
            context.SetAction(action);
            ActionTimelineOperationSnapshot left = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.ResizeLeft, new[] { selected.EditorId });
            Assert.AreEqual(-2, left.ConstrainPointerDelta(-100, out _));
            ActionTimelineOperationResult leftResult = left.Evaluate(ActionTimelineOperationInput.Delta(-2));
            Assert.AreEqual(ActionTimelineOperationState.Allowed, leftResult.State);
            Assert.AreEqual(3, leftResult.Candidates[0].StartFrame);

            ActionTimelineOperationSnapshot right = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.ResizeRight, new[] { selected.EditorId });
            Assert.AreEqual(2, right.ConstrainPointerDelta(100, out _));
            ActionTimelineOperationResult rightResult = right.Evaluate(ActionTimelineOperationInput.Delta(2));
            Assert.AreEqual(ActionTimelineOperationState.Allowed, rightResult.State);
            Assert.AreEqual(7, rightResult.Candidates[0].DurationFrames);
        }
        finally
        {
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void PreviewInitialFraming_FitsBoundsForCameraAspectAndRotation()
    {
        var bounds = new Bounds(new Vector3(3f, 2f, -4f), new Vector3(6f, 3f, 2f));
        Quaternion rotation = Quaternion.Euler(18f, 37f, 0f);
        const float fieldOfView = 30f;
        const float aspect = 16f / 9f;
        const float fill = 0.42f;

        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            bounds, rotation, fieldOfView, aspect, fill, out Vector3 target, out float distance));
        Assert.AreEqual(bounds.center, target);

        Vector3 extents = bounds.extents;
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;
        float halfWidth = Vector3.Dot(new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)), extents);
        float halfHeight = Vector3.Dot(new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)), extents);
        float halfDepth = Vector3.Dot(new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)), extents);
        float usableDistance = distance - halfDepth;
        float verticalTangent = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        Assert.LessOrEqual(halfHeight / (usableDistance * verticalTangent), fill + 0.0001f);
        Assert.LessOrEqual(halfWidth / (usableDistance * verticalTangent * aspect), fill + 0.0001f);
    }

    [Test]
    public void PreviewInitialFraming_RespondsToSizeAspectAndViewDirection()
    {
        var small = new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f));
        var large = new Bounds(Vector3.zero, new Vector3(2f, 4f, 2f));
        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            small, Quaternion.identity, 30f, 16f / 9f, 0.42f, out _, out float smallDistance));
        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            large, Quaternion.identity, 30f, 16f / 9f, 0.42f, out _, out float largeDistance));
        Assert.Greater(largeDistance, smallDistance);

        var wide = new Bounds(Vector3.zero, new Vector3(6f, 2f, 1f));
        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            wide, Quaternion.identity, 30f, 2f, 0.42f, out _, out float landscapeDistance));
        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            wide, Quaternion.identity, 30f, 0.5f, 0.42f, out _, out float portraitDistance));
        Assert.Greater(portraitDistance, landscapeDistance);

        Assert.IsTrue(ActionPreviewCameraUtility.TryCalculateInitialFraming(
            wide, Quaternion.Euler(0f, 90f, 0f), 30f, 2f, 0.42f, out _, out float sideDistance));
        Assert.Greater(Mathf.Abs(landscapeDistance - sideDistance), 0.0001f);
    }

    [Test]
    public void PreviewSupersampling_UsesTwoTimesScaleAndHonorsBudgets()
    {
        Assert.AreEqual(2f, ActionPreviewCameraUtility.CalculateSupersampleScale(
            new Vector2(800f, 450f), 1f, 8192, 16000000L, 2f), 0.0001f);

        Vector2 largeSize = new Vector2(4000f, 3000f);
        float limitedScale = ActionPreviewCameraUtility.CalculateSupersampleScale(
            largeSize, 1f, 4096, 16000000L, 2f);
        Assert.LessOrEqual(largeSize.x * limitedScale, 4096f + 0.01f);
        Assert.LessOrEqual(largeSize.y * limitedScale, 4096f + 0.01f);
        Assert.LessOrEqual(largeSize.x * largeSize.y * limitedScale * limitedScale, 16000000f + 1f);
    }

    [Test]
    public void AnimationCreation_ChecksOverlapWithoutMutationAndAcceptsAdjacentSegments()
    {
        ActionAsset action = CreateActionAsset();
        AnimationAsset animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        animation.EditorSetClip(clip);
        ActionAsset previousAction = ActionEditorContext.Shared.CurrentAction;
        double previousPosition = ActionEditorContext.Shared.PreviewPosition;
        try
        {
            ActionEditorContext.Shared.SetAction(action);
            Assert.IsTrue(ActionEditorCommands.CanAddAnimationSegment(action, animation, 0, out string message), message);
            Assert.IsEmpty(action.Timeline.AnimationSegments, "Hover validation must not add Timeline content.");
            Assert.IsTrue(ActionEditorCommands.AddAnimationSegment(action, animation, 0, out message), message);
            Assert.AreSame(animation, action.Timeline.AnimationSegments[0].AnimationAsset);
            Assert.AreEqual(60, action.Timeline.AnimationSegments[0].DerivedDurationFrames);

            Assert.IsFalse(ActionEditorCommands.CanAddAnimationSegment(action, animation, 59, out message));
            StringAssert.Contains("overlap", message);
            Assert.IsFalse(ActionEditorCommands.AddAnimationSegment(action, animation, 59, out _));
            Assert.AreEqual(1, action.Timeline.AnimationSegments.Count);
            Assert.IsTrue(ActionEditorCommands.CanAddAnimationSegment(action, animation, 60, out message), message);
            Assert.IsTrue(ActionEditorCommands.AddAnimationSegment(action, animation, 60, out message), message);
            Assert.AreEqual(2, action.Timeline.AnimationSegments.Count);
        }
        finally
        {
            Undo.ClearUndo(action);
            ActionEditorContext.Shared.SetAction(previousAction);
            ActionEditorContext.Shared.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void GameplayLaneOccupancy_UsesHalfOpenIntervalsAndPointsOccupyOneFrame()
    {
        var lane = new GameplayLane();
        var range = new MotionPolicyItem();
        range.EditorSetTiming(10, 5);
        lane.EditorItems.Add(range);

        Assert.IsTrue(ActionGameplayLaneOccupancy.WouldOverlap(lane, null, 12, 13));
        Assert.IsFalse(ActionGameplayLaneOccupancy.WouldOverlap(lane, null, 15, 16));
        Assert.IsFalse(ActionGameplayLaneOccupancy.WouldOverlap(lane, null, 5, 10));
    }

    [Test]
    public void EditorCommands_RejectSameLaneCollisionAndAllowAdjacentOrCrossLaneContent()
    {
        ActionAsset asset = CreateActionAsset();
        var firstLane = new GameplayLane();
        var secondLane = new GameplayLane();
        asset.Timeline.EditorGameplayLanes.Add(firstLane);
        asset.Timeline.EditorGameplayLanes.Add(secondLane);
        ActionAuthoringIdentity.RepairInvalidIds(asset);

        GameplayItem first = ActionEditorCommands.AddItem(
            asset, firstLane.EditorId, typeof(MotionPolicyItem), 3, out string firstMessage);
        Assert.NotNull(first, firstMessage);
        Assert.IsNull(ActionEditorCommands.AddItem(
            asset, firstLane.EditorId, typeof(ImpulseItem), 3, out string collisionMessage));
        StringAssert.Contains("overlap", collisionMessage);
        Assert.NotNull(ActionEditorCommands.AddItem(
            asset, firstLane.EditorId, typeof(ImpulseItem), 4, out string adjacentMessage), adjacentMessage);
        Assert.NotNull(ActionEditorCommands.AddItem(
            asset, secondLane.EditorId, typeof(ImpulseItem), 3, out string crossLaneMessage), crossLaneMessage);
        Object.DestroyImmediate(asset);
    }

    [Test]
    public void SharedAnimationResolver_UsesSegmentTimeAndPreviousEndHold()
    {
        ActionAsset action = CreateActionAsset();
        AnimationAsset animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var clip = new AnimationClip();
        animation.EditorSetClip(clip);
        var segment = new AnimationSegment();
        segment.EditorSetData(6, animation, 0.25f, 0.75f, 1f);
        var records = new[] { new ActionRuntimeAnimationRecord(segment) };

        Assert.IsFalse(ActionAnimationSampleResolver.TryResolve(records, 5.999, out _));
        Assert.IsTrue(ActionAnimationSampleResolver.TryResolve(records, 12d, out ActionAnimationSample active));
        Assert.AreEqual(ActionAnimationSampleKind.Segment, active.Kind);
        Assert.AreEqual(0.35f, active.SourceTime, 0.0001f);
        Assert.IsTrue(ActionAnimationSampleResolver.TryResolve(records, 100d, out ActionAnimationSample held));
        Assert.AreEqual(ActionAnimationSampleKind.Hold, held.Kind);
        Assert.AreEqual(0.75f, held.SourceTime, 0.0001f);

        Object.DestroyImmediate(clip);
        Object.DestroyImmediate(animation);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void EditorContext_DerivesCurrentFrameFromContinuousPreviewPosition()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var point = new ImpulseItem();
        point.EditorSetFrame(5);
        lane.EditorItems.Add(point);
        action.Timeline.EditorGameplayLanes.Add(lane);
        try
        {
            ActionEditorContext.Shared.SetAction(action);
            ActionEditorContext.Shared.SetPreviewPosition(2.75d);
            Assert.AreEqual(2.75d, ActionEditorContext.Shared.PreviewPosition, 1e-9d);
            Assert.AreEqual(2, ActionEditorContext.Shared.CurrentFrame);
            ActionEditorContext.Shared.SetPreviewPosition(action.Timeline.DurationFrames);
            Assert.AreEqual(5, ActionEditorContext.Shared.CurrentFrame,
                "The inclusive preview endpoint still displays the last authored Frame.");
        }
        finally
        {
            ActionEditorContext.Shared.SetAction(null);
            Object.DestroyImmediate(action);
        }
    }

    [TestCase(0d, 0)]
    [TestCase(2.75d, 2)]
    [TestCase(2.75d, 1)]
    public void ManualFrameSeek_StopsPlaybackAndPublishesOneCoherentPausedFrame(double position, int requestedFrame)
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip, out _);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        double previousPosition = context.PreviewPosition;
        int notifications = 0;
        ActionEditorChange observed = default;
        void Observe(ActionEditorChange change)
        {
            notifications++;
            observed = change;
            Assert.IsFalse(ActionEditorPlayback.IsPlaying);
            Assert.AreEqual(requestedFrame, context.CurrentFrame);
            Assert.AreEqual((double)requestedFrame, context.PreviewPosition);
        }

        try
        {
            context.SetAction(action);
            context.SetPreviewPosition(position);
            ActionEditorPlayback.Play();
            ActionEditorContext.Changed += Observe;
            context.SetFrame(requestedFrame);
            Assert.AreEqual(1, notifications);
            Assert.AreNotEqual(ActionEditorChangeFlags.None, observed.Flags & ActionEditorChangeFlags.Playback);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            ActionEditorPlayback.Stop(false);
            context.SetAction(previousAction);
            context.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [TestCase(2.75d, 5, 2.75d, 2)]
    [TestCase(5d, 5, 5d, 4)]
    [TestCase(4.75d, 3, 3d, 2)]
    public void DocumentValidation_PreservesContinuousTimeAndClampsOnlyToNewDuration(
        double position, int duration, double expectedPosition, int expectedFrame)
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var range = new MotionPolicyItem();
        range.EditorSetTiming(0, 5);
        lane.EditorItems.Add(range);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        double previousPosition = context.PreviewPosition;
        try
        {
            context.SetAction(action);
            context.SetPreviewPosition(position);
            range.EditorSetTiming(0, duration);
            context.ApplyAssetChange(action, ActionEditorChangeFlags.Timing);
            Assert.AreEqual(expectedPosition, context.PreviewPosition);
            Assert.AreEqual(expectedFrame, context.CurrentFrame);
        }
        finally
        {
            context.SetAction(previousAction);
            context.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorContext_PublishesOneCoherentStateAfterDeletingSelectedContent()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var range = new MotionPolicyItem();
        range.EditorSetTiming(0, 5);
        var selectedPoint = new ImpulseItem();
        selectedPoint.EditorSetFrame(10);
        lane.EditorItems.Add(range);
        lane.EditorItems.Add(selectedPoint);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        double previousPosition = context.PreviewPosition;
        int observedUpdates = 0;
        bool observedCoherentState = false;
        void Observe(ActionEditorChange change)
        {
            if ((change.Flags & ActionEditorChangeFlags.Structure) == 0) return;
            observedUpdates++;
            observedCoherentState = !context.Document.ById.ContainsKey(selectedPoint.EditorId) &&
                                    context.PrimarySelection.Kind == ActionSelectionKind.Action &&
                                    context.PreviewPosition == 5d && context.CurrentFrame == 4;
        }

        try
        {
            context.SetAction(action);
            context.Select(ActionSelectionKind.GameplayItem, selectedPoint.EditorId, false, false);
            context.SetPreviewPosition(10.5d);
            ActionEditorContext.Changed += Observe;
            Assert.IsTrue(ActionEditorCommands.DeleteSelection(
                action, new[] { selectedPoint.EditorId }, true, out string message), message);
            Assert.AreEqual(1, observedUpdates);
            Assert.IsTrue(observedCoherentState, "Observers must never see the rebuilt Document with stale selection or time.");
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            context.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void EditorContext_TimeSelectionAndNonCurrentChangesDoNotRebuildCurrentDocument()
    {
        ActionAsset current = CreateActionAsset();
        ActionAsset other = CreateActionAsset();
        var lane = new GameplayLane();
        var point = new ImpulseItem();
        point.EditorSetFrame(3);
        lane.EditorItems.Add(point);
        current.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(current);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        double previousPosition = context.PreviewPosition;
        try
        {
            context.SetAction(current);
            ActionEditorDocument document = context.Document;
            int version = context.DocumentVersion;
            context.SetPreviewPosition(1.5d);
            context.Select(ActionSelectionKind.GameplayItem, point.EditorId, false, false);
            context.ApplyAssetChange(other, ActionEditorChangeFlags.Content);
            other.name = "Unrelated External Change";
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreSame(current, context.CurrentAction);
            Assert.AreSame(document, context.Document);
            Assert.AreEqual(version, context.DocumentVersion);
        }
        finally
        {
            context.SetAction(previousAction);
            context.SetPreviewPosition(previousPosition);
            Object.DestroyImmediate(current);
            Object.DestroyImmediate(other);
        }
    }

    [TestCase(32, false)]
    [TestCase(33, true)]
    public void AnimationTiming_CandidateAndCommittedAssetAgreeAtFloatBoundary(int nextStart, bool allowed)
    {
        var action = CreateActionAsset();
        var animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        animation.EditorSetClip(clip);
        var edited = new AnimationSegment();
        edited.EditorSetData(0, animation, 0f, 0.2f, 1f);
        var next = new AnimationSegment();
        next.EditorSetData(nextStart, animation, 0f, 0.2f, 1f);
        action.Timeline.EditorAnimationSegments.Add(edited);
        action.Timeline.EditorAnimationSegments.Add(next);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        try
        {
            context.SetAction(action);
            var snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                ActionTimelineOperationKind.SetAnimationTiming, new[] { edited.EditorId });
            var input = ActionTimelineOperationInput.Absolute(0, 0, animation, 0f, 32f / 60f, 1f);
            var candidate = snapshot.Evaluate(input);
            Assert.AreEqual(allowed ? ActionTimelineOperationState.Allowed : ActionTimelineOperationState.Rejected,
                candidate.State);
            Assert.AreEqual(allowed, ActionEditorCommands.CommitTimelineOperation(snapshot, input, out string message), message);
            if (allowed)
            {
                Assert.AreEqual(candidate.Candidates[0].DurationFrames, edited.DerivedDurationFrames);
                Assert.AreEqual(33, edited.DerivedDurationFrames);
                Assert.AreEqual(next.StartFrame, edited.EndFrameExclusiveLong);
            }
            else
                Assert.AreEqual(0.2f, edited.SourceEndTime);
        }
        finally
        {
            Undo.ClearUndo(action);
            context.SetAction(previous);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void TimelineOperation_PreservesNullSlotsAndRejectsTheirStructuralChanges()
    {
        var action = CreateActionAsset();
        var lane = new GameplayLane();
        var range = new MotionPolicyItem();
        range.EditorSetTiming(0, 2);
        lane.EditorItems.Add(null);
        lane.EditorItems.Add(range);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        try
        {
            context.SetAction(action);
            var snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                ActionTimelineOperationKind.Move, new[] { range.EditorId });
            Assert.IsTrue(ActionEditorCommands.CommitTimelineOperation(snapshot,
                ActionTimelineOperationInput.Delta(1), out string message), message);
            Assert.IsNull(lane.Items[0]);
            Assert.AreEqual(1, range.StartFrame);
            snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                ActionTimelineOperationKind.Move, new[] { range.EditorId });
            lane.EditorItems.Reverse(); // Same count; the original null slot is no longer in place.
            Assert.IsFalse(ActionEditorCommands.CommitTimelineOperation(snapshot,
                ActionTimelineOperationInput.Delta(1), out _));
            Assert.AreEqual(1, range.StartFrame);
        }
        finally
        {
            Undo.ClearUndo(action);
            context.SetAction(previous);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void DeleteNullEntry_PublishesFinalSelectionOnceAndDoesNotSelectInAnotherSession()
    {
        var action = CreateActionAsset();
        var other = CreateActionAsset();
        var lane = new GameplayLane();
        var otherLane = new GameplayLane();
        lane.EditorItems.Add(null);
        otherLane.EditorItems.Add(null);
        action.Timeline.EditorGameplayLanes.Add(lane);
        other.Timeline.EditorGameplayLanes.Add(otherLane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            notifications++;
            Assert.AreEqual(1, context.Document.Lanes.Count);
            Assert.IsEmpty(context.Document.GameplayItems);
            Assert.AreEqual(ActionSelectionKind.Action, context.PrimarySelection.Kind);
            Assert.IsEmpty(context.SelectedIds);
        }
        try
        {
            context.SetAction(action);
            context.Select(ActionSelectionKind.GameplayLane, lane.EditorId, false, false);
            ActionEditorContext.Changed += Observe;
            Assert.IsTrue(ActionEditorCommands.DeleteNullEntry(other, ActionSelectionKind.GameplayItem, 0, 0, out string otherMessage), otherMessage);
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(lane.EditorId, context.PrimarySelection.EditorId);
            Assert.IsTrue(ActionEditorCommands.DeleteNullEntry(action, ActionSelectionKind.GameplayItem, 0, 0, out string message), message);
            Assert.AreEqual(1, notifications);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            Undo.ClearUndo(action);
            Undo.ClearUndo(other);
            context.SetAction(previous);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(other);
        }
    }

    [Test]
    public void DestroyedCurrentAction_ClearsSessionAndStopsPlaybackInOneNotification()
    {
        var action = CreateActionAsset();
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            notifications++;
            Assert.IsTrue(ReferenceEquals(null, context.CurrentAction));
            Assert.AreEqual(ActionEditorReadiness.NoAction, context.Document.Readiness);
            Assert.IsEmpty(context.Document.ContentEntries);
            Assert.IsEmpty(context.SelectedIds);
            Assert.AreEqual(0d, context.PreviewPosition);
            Assert.IsFalse(ActionEditorPlayback.IsPlaying);
            Assert.IsTrue((change.Flags & ActionEditorChangeFlags.PreviewResources) != 0);
        }
        try
        {
            context.SetAction(action);
            ActionEditorCommands.AddLane(action);
            context.SetPreviewPosition(0.5d);
            ActionEditorPlayback.Play();
            ActionEditorContext.Changed += Observe;
            Object.DestroyImmediate(action);
            context.RefreshExternal(ActionEditorChangeOrigin.Project);
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            Assert.AreEqual(1, notifications);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previous);
            if (action != null) Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void AnimationAssetRename_IsPresentationOnlyAndKeepsPlayback()
    {
        var action = CreateActionAsset();
        var animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var segment = new AnimationSegment();
        segment.EditorSetData(0, animation, 0f, 1f, 1f);
        action.Timeline.EditorAnimationSegments.Add(segment);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        int notifications = 0;
        void Observe(ActionEditorChange change)
        {
            notifications++;
            Assert.AreEqual(ActionEditorChangeFlags.Presentation, change.Flags);
            Assert.AreEqual("Renamed Animation", context.Document.AnimationSegments[0].DisplayName);
            Assert.IsFalse(ActionPreviewPanel.RequiresDataInvalidation(change.Flags));
        }
        try
        {
            context.SetAction(action);
            ActionEditorPlayback.Play();
            ActionEditorContext.Changed += Observe;
            animation.name = "Renamed Animation";
            context.RefreshExternal(ActionEditorChangeOrigin.ObjectChange);
            context.RefreshExternal(ActionEditorChangeOrigin.Project);
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(ActionEditorPlayback.IsPlaying);
        }
        finally
        {
            ActionEditorContext.Changed -= Observe;
            context.SetAction(previous);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
        }
    }

    [Test]
    public void AnimationTimingDraft_RetainsValuesAcrossLaneMuteButExpiresOnSourceOrTargetChange()
    {
        var action = CreateActionAsset();
        var lane = new GameplayLane();
        var animation = ScriptableObject.CreateInstance<AnimationAsset>();
        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        animation.EditorSetClip(clip);
        var segment = new AnimationSegment();
        segment.EditorSetData(0, animation, 0f, 1f, 1f);
        action.Timeline.EditorAnimationSegments.Add(segment);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        var context = ActionEditorContext.Shared;
        ActionAsset previous = context.CurrentAction;
        try
        {
            context.SetAction(action);
            var selection = new ActionSelectionValue(ActionSelectionKind.AnimationSegment, segment.EditorId);
            var draft = new ActionDetailsWindow.TimingDraft
            {
                Kind = selection.Kind, EditorId = selection.EditorId, Source = segment, StartFrame = 5,
                AnimationAsset = animation, SourceStartTime = 0.1f, SourceEndTime = 0.8f, PlayRate = 2f,
                Snapshot = ActionTimelineOperationSnapshot.Capture(context.Document,
                    ActionTimelineOperationKind.SetAnimationTiming, new[] { segment.EditorId }),
            };
            Assert.IsTrue(ActionEditorCommands.SetMuted(action, lane.EditorId, true));
            Assert.IsTrue(draft.CanRetain(action, selection));
            Assert.AreEqual(5, draft.StartFrame);
            Assert.AreEqual(0.1f, draft.SourceStartTime);
            Assert.AreEqual(0.8f, draft.SourceEndTime);
            Assert.IsFalse(draft.CanRetain(null, selection));
            Assert.IsFalse(draft.CanRetain(action, new ActionSelectionValue(ActionSelectionKind.Action, string.Empty)));
            segment.EditorSetData(1, animation, 0f, 1f, 1f);
            Assert.IsFalse(draft.CanRetain(action, selection));
        }
        finally
        {
            Undo.ClearUndo(action);
            context.SetAction(previous);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void TimelineOperation_RejectsWhenUnselectedLaneItemChangedAfterSnapshot()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var selected = new MotionPolicyItem();
        selected.EditorSetTiming(0, 2);
        var other = new MotionPolicyItem();
        other.EditorSetTiming(5, 2);
        lane.EditorItems.Add(selected);
        lane.EditorItems.Add(other);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        try
        {
            context.SetAction(action);
            ActionTimelineOperationSnapshot snapshot = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.Move, new[] { selected.EditorId });
            Assert.AreEqual(ActionTimelineOperationState.Allowed,
                snapshot.Evaluate(ActionTimelineOperationInput.Delta(1)).State);

            other.EditorSetTiming(2, 2);
            Assert.IsFalse(ActionEditorCommands.CommitTimelineOperation(
                snapshot, ActionTimelineOperationInput.Delta(1), out string message));
            StringAssert.Contains("changed", message);
            Assert.AreEqual(0, selected.StartFrame);
        }
        finally
        {
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void TimelineOperation_AllowsUnrelatedNameChangeAndReevaluatesInputAtCommit()
    {
        ActionAsset action = CreateActionAsset();
        var lane = new GameplayLane();
        var selected = new MotionPolicyItem();
        selected.EditorSetTiming(0, 2);
        var other = new MotionPolicyItem();
        other.EditorSetTiming(5, 2);
        lane.EditorItems.Add(selected);
        lane.EditorItems.Add(other);
        action.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(action);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        try
        {
            context.SetAction(action);
            ActionTimelineOperationSnapshot snapshot = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.Move, new[] { selected.EditorId });
            lane.EditorSetName("Renamed while dragging");
            Assert.IsTrue(ActionEditorCommands.CommitTimelineOperation(
                snapshot, ActionTimelineOperationInput.Delta(1), out string message), message);
            Assert.AreEqual(1, selected.StartFrame);
        }
        finally
        {
            Undo.ClearUndo(action);
            context.SetAction(previousAction);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void TimelineOperation_CannotCommitAfterCurrentActionChanges()
    {
        ActionAsset source = CreateActionAsset();
        ActionAsset replacement = CreateActionAsset();
        var lane = new GameplayLane();
        var range = new MotionPolicyItem();
        range.EditorSetTiming(0, 2);
        lane.EditorItems.Add(range);
        source.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(source);
        ActionEditorContext context = ActionEditorContext.Shared;
        ActionAsset previousAction = context.CurrentAction;
        try
        {
            context.SetAction(source);
            ActionTimelineOperationSnapshot snapshot = ActionTimelineOperationSnapshot.Capture(
                context.Document, ActionTimelineOperationKind.Move, new[] { range.EditorId });
            context.SetAction(replacement);
            Assert.IsFalse(ActionEditorCommands.CommitTimelineOperation(
                snapshot, ActionTimelineOperationInput.Delta(1), out string message));
            StringAssert.Contains("current Timeline Action", message);
            Assert.AreEqual(0, range.StartFrame);
        }
        finally
        {
            context.SetAction(previousAction);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(replacement);
        }
    }

    [Test]
    public void PreviewSpatialEvaluator_IsIndependentOfEvaluationOrderAndHonorsInvalidation()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out RootMotionItem root);
        var evaluator = new ActionPreviewSpatialEvaluator();
        var fresh = new ActionPreviewSpatialEvaluator();
        try
        {
            evaluator.Evaluate(action, 1.25d);
            Vector3 sequential = evaluator.Evaluate(action, 3.5d);
            Vector3 direct = fresh.Evaluate(action, 3.5d);
            Assert.AreEqual(direct.x, sequential.x, 0.0001f);
            Assert.AreEqual(new ActionPreviewSpatialEvaluator().Evaluate(action, 0.5d).x,
                evaluator.Evaluate(action, 0.5d).x, 0.0001f);

            root.EditorSetMuted(true);
            evaluator.Invalidate();
            Assert.AreEqual(Vector3.zero, evaluator.Evaluate(action, 3.5d));
        }
        finally
        {
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PreviewSpatialEvaluator_IntegerFramesUsePostTickPositionAndDurationShowsFinalPosition()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out _);
        try
        {
            var evaluator = new ActionPreviewSpatialEvaluator();
            Assert.AreEqual(1f, evaluator.Evaluate(action, 0d).x, 0.0001f);
            Assert.AreEqual(2f, evaluator.Evaluate(action, 1d).x, 0.0001f);
            Assert.AreEqual(2.5f, evaluator.Evaluate(action, 1.5d).x, 0.0001f);
            Assert.AreEqual(4f, evaluator.Evaluate(action, 3d).x, 0.0001f);
            Assert.AreEqual(4f, evaluator.Evaluate(action, action.Timeline.DurationFrames).x, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PreviewHitBoxPosition_MatchesAccumulatedRuntimeHitStageMotion()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset animation, out AnimationClip clip,
            out RootMotionItem root);
        var binding = new GameObject("HitBox Binding");
        try
        {
            var evaluator = new ActionPreviewSpatialEvaluator();
            var hitBox = new ActionHitBoxConfig { center = new Vector3(0.25f, 0f, 0f) };
            Vector3 runtimeHitPosition = Vector3.zero;
            for (int frame = 0; frame < 4; frame++)
            {
                ActionItemRuntimeUtility.GetSourceWindow(root.Config.sourceStartTime, root.Config.playRate,
                    frame, out float start, out float end);
                Assert.IsTrue(animation.RootMotionData.TryExtract(start, end, out RootMotionTransform delta));
                Vector3 localDelta = delta.Position;
                localDelta.y = 0f;
                runtimeHitPosition += localDelta;

                binding.transform.position = evaluator.Evaluate(action, frame);
                Assert.IsTrue(ActionHitBoxGeometry.TryBuild(binding.transform, hitBox,
                    out ActionHitBoxWorldShape previewShape, out string failure), failure);
                Assert.AreEqual(runtimeHitPosition.x + hitBox.center.x, previewShape.Center.x, 0.0001f);
            }
        }
        finally
        {
            Object.DestroyImmediate(binding);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(animation);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PreviewSpatialEvaluator_UsesTranslationDomainOwnershipAcrossLanes()
    {
        ActionAsset action = CreateRootMotionAction(1f, out AnimationAsset firstAnimation, out AnimationClip firstClip,
            out _);
        ActionAsset secondAction = CreateRootMotionAction(2f, out AnimationAsset secondAnimation,
            out AnimationClip secondClip, out RootMotionItem secondRoot);
        action.Timeline.EditorGameplayLanes.Add(secondAction.Timeline.EditorGameplayLanes[0]);
        try
        {
            Vector3 position = new ActionPreviewSpatialEvaluator().Evaluate(action, 1d);
            Assert.AreEqual(4f, position.x, 0.0001f,
                "The active TranslationDomain owner wins; overlapping lanes are not summed ad hoc.");
            Assert.IsNotNull(secondRoot);
        }
        finally
        {
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(secondAction);
            Object.DestroyImmediate(firstAnimation);
            Object.DestroyImmediate(secondAnimation);
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }
    }

    private static ActionAsset CreateActionAsset()
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        return action;
    }

    private static ActionAsset CreateRootMotionAction(float unitsPerFrame, out AnimationAsset animation,
        out AnimationClip clip, out RootMotionItem root)
    {
        ActionAsset action = CreateActionAsset();
        animation = ScriptableObject.CreateInstance<AnimationAsset>();
        clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, 1f, unitsPerFrame * ActionTimelineData.FrameRate));
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(clip, ActionTimelineData.FrameRate, 1f, 1, "test",
            new[] { 0f, 1f },
            new[] { Vector3.zero, new Vector3(unitsPerFrame * ActionTimelineData.FrameRate, 0f, 0f) },
            new[] { Quaternion.identity, Quaternion.identity });
        animation.EditorSetClip(clip);
        animation.EditorSetRootMotionData(trajectory);
        root = new RootMotionItem();
        root.EditorSetTiming(0, 4);
        root.Config.animationAsset = animation;
        root.Config.sourceStartTime = 0f;
        root.Config.playRate = 1f;
        var lane = new GameplayLane();
        lane.EditorItems.Add(root);
        action.Timeline.EditorGameplayLanes.Add(lane);
        return action;
    }

    [Test]
    public void TimelineSnap_UsesScreenDistanceAndStableTieBreaking()
    {
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(
            4, new[] { 10 }, new[] { 18 }, 2d, _ => true, out ActionTimelineSnapResult zoomedOut));
        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(
            6, new[] { 10 }, new[] { 18 }, 4d, _ => true, out ActionTimelineSnapResult zoomedIn));
        Assert.AreEqual(8, zoomedOut.Delta);
        Assert.AreEqual(8, zoomedIn.Delta);
        Assert.AreEqual(18, zoomedOut.TargetFrame);
        Assert.AreEqual(18, zoomedIn.TargetFrame);

        Assert.IsTrue(ActionTimelineInteractionMath.TrySnap(
            0, new[] { 10, 20 }, new[] { 9, 21 }, 1d, _ => true, out ActionTimelineSnapResult tie));
        Assert.AreEqual(-1, tie.Delta, "Equal-distance candidates prefer the left moving edge.");
    }

    [Test]
    public void TimelineAutoPan_IsTimeBasedAndBounded()
    {
        Assert.AreEqual(300f, ActionTimelineInteractionMath.AutoPanSpeed(50f, 600f), 0.001f);
        Assert.AreEqual(-600f, ActionTimelineInteractionMath.AutoPanSpeed(-200f, 600f), 0.001f);
        float tenSmallSteps = ActionTimelineInteractionMath.AutoPanSpeed(50f, 600f) * 0.1f * 10f;
        float oneLargeStep = ActionTimelineInteractionMath.AutoPanSpeed(50f, 600f) * 1f;
        Assert.AreEqual(oneLargeStep, tenSmallSteps, 0.001f);
    }

    [Test]
    public void FailedBake_DoesNotApplyDraftClipRigOrReplaceExistingTrajectory()
    {
        AnimationAsset asset = ScriptableObject.CreateInstance<AnimationAsset>();
        AnimationRigAsset originalRig = ScriptableObject.CreateInstance<AnimationRigAsset>();
        AnimationRigAsset invalidRig = ScriptableObject.CreateInstance<AnimationRigAsset>();
        var originalClip = new AnimationClip();
        var draftClip = new AnimationClip();
        var originalTrajectory = new RootMotionTrajectory();
        asset.EditorSetClip(originalClip);
        asset.EditorSetAnimationRigAsset(originalRig);
        asset.EditorSetRootMotionData(originalTrajectory);
        try
        {
            Assert.IsFalse(AnimationAssetBakeWorkflow.TryBake(asset, draftClip, invalidRig, out var result));
            Assert.IsFalse(result.Success);
            Assert.AreSame(originalClip, asset.Clip);
            Assert.AreSame(originalRig, asset.AnimationRigAsset);
            Assert.AreSame(originalTrajectory, asset.RootMotionData);
        }
        finally
        {
            Undo.ClearUndo(asset);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(originalRig);
            Object.DestroyImmediate(invalidRig);
            Object.DestroyImmediate(originalClip);
            Object.DestroyImmediate(draftClip);
        }
    }
}
#endif
