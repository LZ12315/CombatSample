#if UNITY_EDITOR
using UnityEngine.UIElements;

internal sealed class ActionSequenceIssueBadge : Label
{
    private string severityClass;

    public ActionSequenceIssueBadge()
    {
        AddToClassList("asv2-issue-badge");
        pickingMode = PickingMode.Ignore;
    }

    public void Refresh(ActionSequenceIssueState issueState)
    {
        ActionSequenceViewUtility.SetDisplay(this, issueState.HasIssues);
        if (!issueState.HasIssues)
            return;

        text = issueState.Count.ToString();
        tooltip = issueState.Tooltip;

        if (!string.IsNullOrEmpty(severityClass))
            RemoveFromClassList(severityClass);

        severityClass = issueState.HighestSeverity switch
        {
            ActionSequenceEditorValidationSeverity.Error => "asv2-issue-error",
            ActionSequenceEditorValidationSeverity.Warning => "asv2-issue-warning",
            _ => "asv2-issue-info",
        };
        AddToClassList(severityClass);
    }
}
#endif
