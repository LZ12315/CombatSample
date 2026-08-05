#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

internal sealed class ActionSequenceValidationPanel
{
    private readonly VisualElement panel;
    private readonly Label titleLabel;
    private readonly ScrollView list;
    private readonly Button closeButton;
    private bool visible;

    public ActionSequenceValidationPanel(VisualElement root)
    {
        panel = root.Q<VisualElement>("validation-panel") ?? new VisualElement { name = "validation-panel" };
        panel.AddToClassList("asv2-validation-panel");
        if (panel.parent == null)
            root.Add(panel);

        var header = panel.Q<VisualElement>("validation-panel-header") ?? new VisualElement { name = "validation-panel-header" };
        header.AddToClassList("asv2-validation-panel-header");
        if (header.parent == null)
            panel.Add(header);

        titleLabel = header.Q<Label>("validation-panel-title") ?? new Label { name = "validation-panel-title" };
        titleLabel.AddToClassList("asv2-validation-panel-title");
        if (titleLabel.parent == null)
            header.Add(titleLabel);

        closeButton = header.Q<Button>("validation-panel-close") ?? new Button { name = "validation-panel-close", text = "x" };
        closeButton.AddToClassList("asv2-toolbar-icon-button");
        if (closeButton.parent == null)
            header.Add(closeButton);

        list = panel.Q<ScrollView>("validation-panel-list") ?? new ScrollView { name = "validation-panel-list" };
        list.AddToClassList("asv2-validation-panel-list");
        if (list.parent == null)
            panel.Add(list);

        closeButton.clicked += Hide;
        Hide();
    }

    public event Action<ActionSequenceEditorValidationIssue> LocateRequested;
    public event Action<string> RepairRequested;

    public bool Visible => visible;

    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }

    public void Hide()
    {
        visible = false;
        ActionSequenceViewUtility.SetDisplay(panel, false);
    }

    public void Show()
    {
        visible = true;
        ActionSequenceViewUtility.SetDisplay(panel, true);
    }

    public void Refresh(ActionSequenceValidationPresentation presentation)
    {
        list.Clear();

        IReadOnlyList<ActionSequenceEditorValidationIssue> issues = presentation?.Validation.Issues;
        int count = issues != null ? issues.Count : 0;
        titleLabel.text = count == 0 ? "No Issues" : $"Issues ({count})";

        if (count == 0)
        {
            list.Add(new Label("Valid") { name = "validation-empty-label" });
            return;
        }

        for (int i = 0; i < issues.Count; i++)
        {
            ActionSequenceEditorValidationIssue issue = issues[i];
            var row = new ActionSequenceValidationIssueRow(issue);
            row.LocateRequested += LocateRequested;
            row.RepairRequested += RepairRequested;
            list.Add(row);
        }
    }
}

internal sealed class ActionSequenceValidationIssueRow : VisualElement
{
    private readonly ActionSequenceEditorValidationIssue issue;

    public ActionSequenceValidationIssueRow(ActionSequenceEditorValidationIssue issue)
    {
        this.issue = issue;
        AddToClassList("asv2-validation-issue-row");
        AddToClassList(issue.Severity switch
        {
            ActionSequenceEditorValidationSeverity.Error => "asv2-validation-issue-error",
            ActionSequenceEditorValidationSeverity.Warning => "asv2-validation-issue-warning",
            _ => "asv2-validation-issue-info",
        });

        var label = new Label(BuildText(issue));
        label.AddToClassList("asv2-validation-issue-label");
        label.tooltip = issue.Message;
        Add(label);

        if (!string.IsNullOrEmpty(issue.RepairCommandId))
        {
            var repair = new Button { text = "Repair" };
            repair.AddToClassList("asv2-validation-issue-repair");
            repair.clicked += () => RepairRequested?.Invoke(issue.RepairCommandId);
            Add(repair);
        }

        RegisterCallback<PointerDownEvent>(OnPointerDown);
    }

    public event Action<ActionSequenceEditorValidationIssue> LocateRequested;
    public event Action<string> RepairRequested;

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
            return;

        LocateRequested?.Invoke(issue);
        evt.StopPropagation();
    }

    private static string BuildText(ActionSequenceEditorValidationIssue issue)
    {
        return $"{issue.Severity} - {issue.Code} - {BuildLocation(issue)}";
    }

    private static string BuildLocation(ActionSequenceEditorValidationIssue issue)
    {
        return issue.ItemKind switch
        {
            ActionSequenceEditorDocumentItemKind.Track => $"Track {issue.TrackIndex}",
            ActionSequenceEditorDocumentItemKind.Clip => $"Track {issue.TrackIndex}, Clip {issue.ClipIndex}",
            ActionSequenceEditorDocumentItemKind.LegacyClip => $"Legacy Clip {issue.LegacyClipIndex}",
            _ => "Sequence",
        };
    }
}
#endif
