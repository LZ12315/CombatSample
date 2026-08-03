#if UNITY_EDITOR
using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal sealed class ActionSequenceToolbar
{
    private readonly ObjectField targetField;
    private readonly Button addTrackButton;
    private readonly Button playPauseButton;
    private readonly Button stopButton;
    private readonly IntegerField currentFrameField;
    private readonly Label fpsLabel;
    private readonly Label durationLabel;
    private readonly Slider zoomSlider;
    private readonly Button fitButton;
    private bool suppressCallbacks;

    public ActionSequenceToolbar(VisualElement root)
    {
        targetField = root.Q<ObjectField>("target-field") ?? new ObjectField("Target");
        addTrackButton = root.Q<Button>("add-track-button") ?? new Button();
        playPauseButton = root.Q<Button>("play-pause-button") ?? new Button();
        stopButton = root.Q<Button>("stop-button") ?? new Button();
        currentFrameField = root.Q<IntegerField>("current-frame-field") ?? new IntegerField();
        fpsLabel = root.Q<Label>("fps-label") ?? new Label();
        durationLabel = root.Q<Label>("duration-label") ?? new Label();
        zoomSlider = root.Q<Slider>("zoom-slider") ?? new Slider(ActionSequenceTimelineTransform.MinPixelsPerFrame, ActionSequenceTimelineTransform.MaxPixelsPerFrame);
        fitButton = root.Q<Button>("fit-button") ?? new Button();

        if (addTrackButton.parent == null)
            root.Add(addTrackButton);
        if (playPauseButton.parent == null)
            root.Add(playPauseButton);
        if (stopButton.parent == null)
            root.Add(stopButton);
        if (currentFrameField.parent == null)
            root.Add(currentFrameField);

        targetField.objectType = typeof(Object);
        targetField.allowSceneObjects = false;
        zoomSlider.lowValue = ActionSequenceTimelineTransform.MinPixelsPerFrame;
        zoomSlider.highValue = ActionSequenceTimelineTransform.MaxPixelsPerFrame;
        addTrackButton.text = "+ Track";
        addTrackButton.tooltip = "Add Track";
        playPauseButton.text = ">";
        playPauseButton.tooltip = "Play/Pause";
        stopButton.text = "[]";
        stopButton.tooltip = "Stop";
        currentFrameField.tooltip = "Current Frame";
        currentFrameField.isDelayed = false;
        fitButton.text = "Fit";
        fpsLabel.tooltip = "Frame Rate";
        durationLabel.tooltip = "Sequence Duration";

        targetField.RegisterValueChangedCallback(evt =>
        {
            if (!suppressCallbacks)
                TargetChanged?.Invoke(evt.newValue);
        });

        zoomSlider.RegisterValueChangedCallback(evt =>
        {
            if (!suppressCallbacks)
                ZoomChanged?.Invoke(evt.newValue);
        });

        fitButton.clicked += () => FitRequested?.Invoke();
        addTrackButton.clicked += () => AddTrackRequested?.Invoke(addTrackButton);
        playPauseButton.clicked += () => PlayPauseRequested?.Invoke();
        stopButton.clicked += () => StopRequested?.Invoke();
        currentFrameField.RegisterValueChangedCallback(evt =>
        {
            if (!suppressCallbacks)
                CurrentFrameChanged?.Invoke(evt.newValue);
        });
    }

    public event Action<Object> TargetChanged;
    public event Action<VisualElement> AddTrackRequested;
    public event Action PlayPauseRequested;
    public event Action StopRequested;
    public event Action<int> CurrentFrameChanged;
    public event Action<float> ZoomChanged;
    public event Action FitRequested;

    public void SetTarget(Object target)
    {
        suppressCallbacks = true;
        targetField.SetValueWithoutNotify(target);
        suppressCallbacks = false;
    }

    public void Refresh(ActionSequenceEditorState state)
    {
        suppressCallbacks = true;
        zoomSlider.SetValueWithoutNotify(state != null ? state.PixelsPerFrame : ActionSequenceEditorState.DefaultPixelsPerFrame);
        currentFrameField.SetValueWithoutNotify(state != null ? state.CurrentFrame : 0);
        suppressCallbacks = false;

        if (state == null || !state.IsSupported)
        {
            fpsLabel.text = "-- FPS";
            durationLabel.text = "--";
            playPauseButton.text = ">";
            addTrackButton.SetEnabled(false);
            playPauseButton.SetEnabled(false);
            stopButton.SetEnabled(false);
            currentFrameField.SetEnabled(false);
            zoomSlider.SetEnabled(false);
            fitButton.SetEnabled(false);
            return;
        }

        ActionSequenceSnapshot sequence = state.Document.Sequence;
        fpsLabel.text = $"{sequence.FrameRate} FPS";
        fpsLabel.tooltip = $"Frame Rate: {sequence.FrameRate}";
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
        {
            durationLabel.text = $"Fixed {sequence.FixedDurationFrames}";
            durationLabel.tooltip = $"Fixed Duration: {sequence.FixedDurationFrames} frames";
        }
        else
        {
            int duration = state.CalculateSequenceDurationFrames();
            durationLabel.text = $"Auto {duration}";
            durationLabel.tooltip = $"Auto Duration: {duration} frames";
        }

        addTrackButton.SetEnabled(true);
        playPauseButton.SetEnabled(state.CanPlay(out _));
        playPauseButton.text = state.IsPlaying ? "||" : ">";
        stopButton.SetEnabled(true);
        currentFrameField.SetEnabled(true);
        zoomSlider.SetEnabled(true);
        fitButton.SetEnabled(true);
    }
}
#endif
