#if UNITY_EDITOR
using UnityEngine.UIElements;

internal sealed class ActionSequenceStatusBar
{
    private readonly Label targetLabel;
    private readonly Label validationLabel;
    private readonly Button issuesButton;
    private readonly Button repairButton;
    private string transientMessage;

    public ActionSequenceStatusBar(VisualElement root)
    {
        targetLabel = root.Q<Label>("status-target-label") ?? new Label();
        validationLabel = root.Q<Label>("status-validation-label") ?? new Label();
        issuesButton = root.Q<Button>("issues-button") ?? new Button();
        repairButton = root.Q<Button>("repair-ids-button") ?? new Button();
        issuesButton.text = "Issues";
        issuesButton.clicked += () => IssuesRequested?.Invoke();
        repairButton.text = "Repair IDs";
        repairButton.clicked += () => RepairInvalidIdsRequested?.Invoke();
    }

    public event System.Action IssuesRequested;
    public event System.Action RepairInvalidIdsRequested;

    public void Refresh(ActionSequenceEditorState state, ActionSequenceValidationPresentation validation)
    {
        if (state == null || state.Target == null)
        {
            targetLabel.text = "No target";
            validationLabel.text = string.Empty;
            issuesButton.SetEnabled(false);
            repairButton.SetEnabled(false);
            return;
        }

        if (!state.IsSupported)
        {
            targetLabel.text = "Unsupported target";
            validationLabel.text = "Use a Sequence ActionAsset or ActionSequenceAsset.";
            issuesButton.SetEnabled(false);
            repairButton.SetEnabled(false);
            return;
        }

        targetLabel.text = $"{state.DisplayTracks.Count} tracks, {CountClips(state)} clips, {state.Document.LegacyClips.Count} legacy";
        validationLabel.text = string.IsNullOrEmpty(transientMessage) ? BuildValidationText(validation) : transientMessage;
        issuesButton.SetEnabled(validation != null && validation.HasIssues);
        repairButton.SetEnabled(validation != null && validation.HasRepairCommand(ActionSequenceValidator.RepairInvalidIdsCommandId));
    }

    public void SetTransientMessage(string message)
    {
        transientMessage = message;
        validationLabel.text = message ?? string.Empty;
    }

    private static int CountClips(ActionSequenceEditorState state)
    {
        int count = 0;
        for (int i = 0; i < state.DisplayTracks.Count; i++)
            count += state.DisplayTracks[i].Clips.Count;
        return count;
    }

    private static string BuildValidationText(ActionSequenceValidationPresentation validation)
    {
        return validation != null ? validation.BuildSummaryText() : "Valid";
    }
}
#endif
