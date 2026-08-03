#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceValidatorTests
{
    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void Validate_ReportsIdentityIssuesWithoutMutation()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        var track = new ActionSequenceStateTrack();
        var clip = new ActionSequenceTagClipDefinition();
        track.EditorClips.Add(clip);
        asset.EditorTracks.Add(track);
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition());
        string duplicateId = System.Guid.NewGuid().ToString("N");
        track.EditorSetEditorId(duplicateId);
        clip.EditorSetEditorId("bad");
        asset.EditorClips[0].EditorSetEditorId(duplicateId);
        string before = EditorJsonUtility.ToJson(asset);

        ActionSequenceEditorValidationResult result = ActionSequenceValidator.Validate(asset);

        AssertIssue(result, ActionSequenceEditorValidationCode.MalformedEditorId);
        AssertIssue(result, ActionSequenceEditorValidationCode.DuplicateEditorId);
        Assert.AreEqual(before, EditorJsonUtility.ToJson(asset));
    }

    [Test]
    public void Validate_ReportsStructureTimingAndLegacyIssues()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        asset.EditorSetDurationMode(ActionSequenceDurationMode.FixedFrames);
        asset.EditorSetTiming(60, 3);

        var animationTrack = new ActionSequenceAnimationTrack();
        animationTrack.EditorClips.Add(new ActionSequenceHitBoxClipDefinition { startFrame = -1, endFrame = -1 });
        asset.EditorTracks.Add(animationTrack);
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 1, endFrame = 4 });
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        ActionSequenceEditorValidationResult result = ActionSequenceValidator.Validate(asset);

        AssertIssue(result, ActionSequenceEditorValidationCode.DisallowedClipType);
        AssertIssue(result, ActionSequenceEditorValidationCode.PhaseMismatch);
        AssertIssue(result, ActionSequenceEditorValidationCode.InvalidStartFrame);
        AssertIssue(result, ActionSequenceEditorValidationCode.InvalidEndFrame);
        AssertIssue(result, ActionSequenceEditorValidationCode.ClipExceedsFixedDuration);
        AssertIssue(result, ActionSequenceEditorValidationCode.LegacyClip);
        AssertIssue(result, ActionSequenceEditorValidationCode.TrackPhaseOrder);
    }

    [Test]
    public void Validate_ReportsMissingIdsAndRepairCommandIds()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorTracks.Add(new ActionSequenceStateTrack());

        ActionSequenceEditorValidationResult result = ActionSequenceValidator.Validate(asset);
        ActionSequenceEditorValidationIssue issue = result.Issues.First(i => i.Code == ActionSequenceEditorValidationCode.MissingEditorId);

        Assert.AreEqual(ActionSequenceValidator.RepairInvalidIdsCommandId, issue.RepairCommandId);
    }

    [Test]
    public void RepairCommandIds_AreAssignedOnlyForSupportedRepairs()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        asset.EditorTracks.Add(new ActionSequenceHitBoxTrack());
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition());
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        ActionSequenceEditorValidationResult result = ActionSequenceValidator.Validate(asset);

        Assert.AreEqual(ActionSequenceValidator.MigrateLegacyClipsCommandId, result.Issues.First(i => i.Code == ActionSequenceEditorValidationCode.LegacyClip).RepairCommandId);
        Assert.AreEqual(ActionSequenceValidator.RepairTrackPhaseOrderCommandId, result.Issues.First(i => i.Code == ActionSequenceEditorValidationCode.TrackPhaseOrder).RepairCommandId);
    }

    private static void AssertIssue(ActionSequenceEditorValidationResult result, ActionSequenceEditorValidationCode code)
    {
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == code), code.ToString());
    }
}
#endif
