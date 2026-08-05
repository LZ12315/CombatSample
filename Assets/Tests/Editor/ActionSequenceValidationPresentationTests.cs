#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceValidationPresentationTests
{
    [TearDown]
    public void TearDown()
    {
        UnityEditor.Undo.ClearAll();
    }

    [Test]
    public void Presentation_UsesValidatorAsSingleIssueSource()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        var track = new ActionSequenceAnimationTrack();
        track.EditorClips.Add(new ActionSequenceHitBoxClipDefinition { startFrame = -1, endFrame = 0 });
        asset.EditorTracks.Add(track);
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);
        ActionSequenceEditorValidationResult validation = ActionSequenceValidator.Validate(document);
        ActionSequenceValidationPresentation presentation = ActionSequenceValidationPresentation.Create(validation);

        Assert.AreEqual(validation.Issues.Count, presentation.ErrorCount + presentation.WarningCount + presentation.InfoCount);
        Assert.IsTrue(presentation.HasIssues);
        Assert.IsTrue(presentation.HasErrors);
    }

    [Test]
    public void Presentation_AggregatesClipIssuesToOwningTrack()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        var track = new ActionSequenceAnimationTrack();
        var clip = new ActionSequenceHitBoxClipDefinition { startFrame = -1, endFrame = 0 };
        track.EditorClips.Add(clip);
        asset.EditorTracks.Add(track);
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);
        ActionSequenceValidationPresentation presentation = ActionSequenceValidationPresentation.Create(ActionSequenceValidator.Validate(document));

        ActionSequenceIssueState trackIssues = presentation.GetTrackIssueState(document.Tracks[0]);
        ActionSequenceIssueState clipIssues = presentation.GetClipIssueState(document.Tracks[0].Clips[0]);

        Assert.IsTrue(trackIssues.HasIssues);
        Assert.IsTrue(clipIssues.HasIssues);
        Assert.GreaterOrEqual(trackIssues.Count, clipIssues.Count);
    }

    [Test]
    public void Presentation_GroupsSupportedRepairCommands()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition());

        ActionSequenceValidationPresentation presentation = ActionSequenceValidationPresentation.Create(ActionSequenceValidator.Validate(asset));

        Assert.IsTrue(presentation.HasRepairCommand(ActionSequenceValidator.RepairInvalidIdsCommandId));
        Assert.IsTrue(presentation.HasRepairCommand(ActionSequenceValidator.MigrateLegacyClipsCommandId));
        Assert.IsTrue(presentation.Validation.Issues.Any(issue => issue.RepairCommandId == ActionSequenceValidator.RepairInvalidIdsCommandId));
    }
}
#endif
