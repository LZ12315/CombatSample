#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceTrackHeaderRow : VisualElement
{
    private readonly Button collapseButton;
    private readonly Label nameLabel;
    private readonly Label typeLabel;
    private readonly ActionSequenceIssueBadge issueBadge;
    private readonly Button addClipButton;
    private readonly Toggle muteToggle;
    private readonly Toggle lockToggle;
    private string phaseClass;
    private bool suppressCallbacks;

    public ActionSequenceTrackHeaderRow()
    {
        AddToClassList("asv2-track-header-row");

        collapseButton = new Button();
        collapseButton.AddToClassList("asv2-track-icon-button");
        collapseButton.tooltip = "Collapse Track";
        Add(collapseButton);

        var labelContainer = new VisualElement();
        labelContainer.AddToClassList("asv2-track-labels");
        Add(labelContainer);

        nameLabel = new Label();
        nameLabel.AddToClassList("asv2-track-name");
        labelContainer.Add(nameLabel);

        typeLabel = new Label();
        typeLabel.AddToClassList("asv2-track-type");
        labelContainer.Add(typeLabel);

        issueBadge = new ActionSequenceIssueBadge();
        Add(issueBadge);

        addClipButton = new Button { text = "+" };
        addClipButton.AddToClassList("asv2-track-icon-button");
        addClipButton.tooltip = "Add Clip";
        Add(addClipButton);

        muteToggle = new Toggle();
        muteToggle.AddToClassList("asv2-track-small-toggle");
        muteToggle.tooltip = "Mute Track";
        Add(muteToggle);

        lockToggle = new Toggle();
        lockToggle.AddToClassList("asv2-track-small-toggle");
        lockToggle.tooltip = "Lock Track";
        Add(lockToggle);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        collapseButton.clicked += () =>
        {
            if (Snapshot != null)
                CollapseChanged?.Invoke(Snapshot, !Snapshot.Collapsed);
        };
        addClipButton.clicked += () =>
        {
            if (Snapshot != null)
                AddClipRequested?.Invoke(Snapshot);
        };
        muteToggle.RegisterValueChangedCallback(evt =>
        {
            if (!suppressCallbacks && Snapshot != null)
                MuteChanged?.Invoke(Snapshot, evt.newValue);
        });
        lockToggle.RegisterValueChangedCallback(evt =>
        {
            if (!suppressCallbacks && Snapshot != null)
                LockChanged?.Invoke(Snapshot, evt.newValue);
        });
    }

    public string RenderKey { get; private set; }
    public ActionSequenceTrackSnapshot Snapshot { get; private set; }

    public event Action<ActionSequenceTrackSnapshot> Selected;
    public event Action<ActionSequenceTrackSnapshot> AddClipRequested;
    public event Action<ActionSequenceTrackSnapshot, bool> MuteChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> LockChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> CollapseChanged;
    public event Action<ActionSequenceTrackSnapshot, Vector2> ContextRequested;

    public void Bind(ActionSequenceDisplayTrack track, bool selected, ActionSequenceValidationPresentation validation)
    {
        ActionSequenceTrackSnapshot snapshot = track.Snapshot;
        Snapshot = snapshot;
        RenderKey = track.RenderKey;
        string displayName = ActionSequenceViewUtility.GetTrackDisplayName(snapshot);
        string typeName = ActionSequenceViewUtility.GetTrackTypeDisplayName(snapshot);
        nameLabel.text = displayName;
        typeLabel.text = typeName;
        nameLabel.tooltip = displayName;
        typeLabel.tooltip = string.IsNullOrEmpty(ActionSequenceViewUtility.GetFullTypeName(snapshot.Type))
            ? typeName
            : ActionSequenceViewUtility.GetFullTypeName(snapshot.Type);
        issueBadge.Refresh(validation != null ? validation.GetTrackIssueState(snapshot) : default);

        SetClass("asv2-track-muted", snapshot.Muted);
        SetClass("asv2-track-locked", snapshot.Locked);
        SetClass("asv2-track-collapsed", snapshot.Collapsed);
        SetClass("asv2-track-invalid", snapshot.IsNull || snapshot.MissingType);
        SetClass("asv2-selected", selected);

        suppressCallbacks = true;
        collapseButton.text = snapshot.Collapsed ? ">" : "v";
        muteToggle.SetValueWithoutNotify(snapshot.Muted);
        lockToggle.SetValueWithoutNotify(snapshot.Locked);
        suppressCallbacks = false;

        bool editable = !snapshot.Locked && !snapshot.IsNull && !snapshot.MissingType;
        collapseButton.SetEnabled(editable);
        addClipButton.SetEnabled(editable);
        muteToggle.SetEnabled(editable);
        lockToggle.SetEnabled(!snapshot.IsNull && !snapshot.MissingType);

        if (!string.IsNullOrEmpty(phaseClass))
            RemoveFromClassList(phaseClass);
        phaseClass = ActionSequenceViewUtility.GetPhaseClass(snapshot.Phase);
        AddToClassList(phaseClass);

        style.height = ActionSequenceViewUtility.GetTrackHeight(snapshot);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (Snapshot == null || IsControl(evt.target as VisualElement))
            return;

        if (evt.button == 0)
        {
            Selected?.Invoke(Snapshot);
            evt.StopPropagation();
        }
        else if (evt.button == 1)
        {
            Selected?.Invoke(Snapshot);
            ContextRequested?.Invoke(Snapshot, evt.position);
            evt.StopPropagation();
        }
    }

    private static bool IsControl(VisualElement element)
    {
        while (element != null)
        {
            if (element is Button || element is Toggle)
                return true;
            element = element.parent;
        }

        return false;
    }

    private void SetClass(string className, bool enabled)
    {
        if (enabled)
            AddToClassList(className);
        else
            RemoveFromClassList(className);
    }
}
#endif
