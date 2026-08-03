#if UNITY_EDITOR
using System.Text;
using UnityEngine.UIElements;

internal sealed class ActionSequenceStatusBar
{
    private readonly Label targetLabel;
    private readonly Label validationLabel;
    private readonly Button repairButton;
    private string transientMessage;

    public ActionSequenceStatusBar(VisualElement root)
    {
        targetLabel = root.Q<Label>("status-target-label") ?? new Label();
        validationLabel = root.Q<Label>("status-validation-label") ?? new Label();
        repairButton = root.Q<Button>("repair-ids-button") ?? new Button();
        repairButton.text = "Repair IDs";
        repairButton.clicked += () => RepairInvalidIdsRequested?.Invoke();
    }

    public event System.Action RepairInvalidIdsRequested;

    public void Refresh(ActionSequenceEditorState state, ActionSequenceEditorValidationResult validation, ActionSequenceEditorIdentityValidationResult identity)
    {
        if (state == null || state.Target == null)
        {
            targetLabel.text = "No target";
            validationLabel.text = string.Empty;
            repairButton.SetEnabled(false);
            return;
        }

        if (!state.IsSupported)
        {
            targetLabel.text = "Unsupported target";
            validationLabel.text = "Use a Sequence ActionAsset or ActionSequenceAsset.";
            repairButton.SetEnabled(false);
            return;
        }

        targetLabel.text = $"{state.DisplayTracks.Count} tracks, {CountClips(state)} clips, {state.Document.LegacyClips.Count} legacy";
        validationLabel.text = string.IsNullOrEmpty(transientMessage) ? BuildValidationText(validation, identity) : transientMessage;
        repairButton.SetEnabled(identity != null && identity.HasRepairableInvalidIds);
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

    private static string BuildValidationText(ActionSequenceEditorValidationResult validation, ActionSequenceEditorIdentityValidationResult identity)
    {
        int identityIssues = identity != null ? identity.Issues.Count : 0;
        int validationIssues = validation != null ? validation.Issues.Count : 0;
        if (identityIssues == 0 && validationIssues == 0)
            return "Valid";

        var builder = new StringBuilder();
        if (identityIssues > 0)
            builder.Append(identityIssues).Append(" identity issue(s)");
        if (validationIssues > 0)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(validationIssues).Append(" validation issue(s)");
            if (validation.HasErrors)
                builder.Append(" with errors");
        }

        return builder.ToString();
    }
}
#endif
