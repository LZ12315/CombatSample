#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public readonly struct ActionSequenceDisplayClip
{
    public ActionSequenceDisplayClip(ActionSequenceClipSnapshot snapshot, string renderKey, int safeStartFrame, int safeEndFrame)
    {
        Snapshot = snapshot;
        RenderKey = renderKey;
        SafeStartFrame = safeStartFrame;
        SafeEndFrame = safeEndFrame;
    }

    public ActionSequenceClipSnapshot Snapshot { get; }
    public string RenderKey { get; }
    public int SafeStartFrame { get; }
    public int SafeEndFrame { get; }
}

public sealed class ActionSequenceDisplayTrack
{
    private readonly List<ActionSequenceDisplayClip> clips = new List<ActionSequenceDisplayClip>();

    public ActionSequenceDisplayTrack(ActionSequenceTrackSnapshot snapshot, string renderKey)
    {
        Snapshot = snapshot;
        RenderKey = renderKey;
    }

    public ActionSequenceTrackSnapshot Snapshot { get; }
    public string RenderKey { get; }
    public IReadOnlyList<ActionSequenceDisplayClip> Clips => clips;

    internal void AddClip(ActionSequenceDisplayClip clip)
    {
        clips.Add(clip);
    }
}

public sealed class ActionSequenceEditorState : IDisposable
{
    public const float DefaultPixelsPerFrame = 8f;
    public const int MinimumViewEndFrame = 60;

    private readonly List<ActionSequenceDisplayTrack> displayTracks = new List<ActionSequenceDisplayTrack>();
    private ActionSequenceSerializedDocument document;

    public Object Target { get; private set; }
    public ActionSequenceSerializedDocument Document => document;
    public bool IsSupported => document != null && document.IsSupported;
    public IReadOnlyList<ActionSequenceDisplayTrack> DisplayTracks => displayTracks;
    public ActionSequenceTimelineTransform Transform { get; } = new ActionSequenceTimelineTransform();
    public float PixelsPerFrame { get; private set; } = DefaultPixelsPerFrame;
    public float HorizontalScroll { get; private set; }
    public float VerticalScroll { get; private set; }
    public float ViewportWidth { get; private set; } = 1f;
    public float ViewportHeight { get; private set; } = 1f;
    public float HeaderWidth { get; private set; } = 220f;
    public int ViewEndFrame { get; private set; } = MinimumViewEndFrame;
    public int Revision => document != null ? document.Revision : 0;
    public ActionSequenceEditorSelectionValue LocalSelection { get; private set; }
    public int CurrentFrame { get; private set; }
    public bool IsPlaying { get; private set; }
    public double LastPlaybackTimestamp { get; private set; }
    public ActionSequenceClipTimingPreview InteractionPreview { get; private set; }

    public void SetTarget(Object target)
    {
        if (ReferenceEquals(Target, target) && document != null)
            return;

        document?.Dispose();
        document = target != null ? ActionSequenceSerializedDocument.Open(target) : null;
        Target = target;
        StopPlayback(resetFrame: false);
        ClearInteractionPreview();
        ViewEndFrame = MinimumViewEndFrame;
        RebuildDisplayModel();
        UpdateTransform();
        ClampCurrentFrame();
        ValidateLocalSelection();
    }

    public bool Refresh()
    {
        if (document == null)
            return false;

        bool changed = document.Refresh();
        if (changed)
        {
            RebuildDisplayModel();
            UpdateTransform();
            ClampCurrentFrame();
            ValidateLocalSelection();
        }

        return changed;
    }

    public void ConfigureView(float pixelsPerFrame, float horizontalScroll, float verticalScroll, float headerWidth)
    {
        PixelsPerFrame = Mathf.Clamp(pixelsPerFrame, ActionSequenceTimelineTransform.MinPixelsPerFrame, ActionSequenceTimelineTransform.MaxPixelsPerFrame);
        HorizontalScroll = Mathf.Max(0f, horizontalScroll);
        VerticalScroll = Mathf.Max(0f, verticalScroll);
        HeaderWidth = Mathf.Clamp(headerWidth, 160f, 420f);
        UpdateTransform();
    }

    public void SetViewport(float width, float height)
    {
        ViewportWidth = Mathf.Max(1f, width);
        ViewportHeight = Mathf.Max(1f, height);
        UpdateTransform();
    }

    public void SetHorizontalScroll(float value)
    {
        Transform.SetScrollX(value);
        HorizontalScroll = Transform.ScrollX;
    }

    public void SetVerticalScroll(float value)
    {
        VerticalScroll = Mathf.Max(0f, value);
    }

    public void SetHeaderWidth(float value)
    {
        HeaderWidth = Mathf.Clamp(value, 160f, 420f);
    }

    public void SetPixelsPerFrame(float value)
    {
        PixelsPerFrame = Mathf.Clamp(value, ActionSequenceTimelineTransform.MinPixelsPerFrame, ActionSequenceTimelineTransform.MaxPixelsPerFrame);
        UpdateTransform();
        HorizontalScroll = Transform.ScrollX;
    }

    public void ZoomAt(float viewportX, float requestedPixelsPerFrame)
    {
        Transform.ZoomAtViewportX(viewportX, requestedPixelsPerFrame);
        PixelsPerFrame = Transform.PixelsPerFrame;
        HorizontalScroll = Transform.ScrollX;
    }

    public void Fit()
    {
        Transform.Fit(ViewEndFrame);
        PixelsPerFrame = Transform.PixelsPerFrame;
        HorizontalScroll = Transform.ScrollX;
    }

    public void FrameRange(int startFrame, int endFrame, float padding = ActionSequenceTimelineTransform.FitHorizontalPadding)
    {
        Transform.FrameRange(startFrame, endFrame, padding);
        ViewEndFrame = Transform.ViewEndFrame;
        PixelsPerFrame = Transform.PixelsPerFrame;
        HorizontalScroll = Transform.ScrollX;
    }

    public void SetCurrentFrame(int frame)
    {
        CurrentFrame = Mathf.Clamp(frame, 0, ViewEndFrame);
    }

    public bool CanPlay(out string reason)
    {
        reason = null;
        if (!IsSupported)
        {
            reason = "No supported ActionSequence target.";
            return false;
        }

        if (document.Sequence.FrameRate <= 0)
        {
            reason = "Frame rate must be greater than zero.";
            return false;
        }

        return true;
    }

    public bool TogglePlayback(double timestamp, out string reason)
    {
        if (IsPlaying)
        {
            StopPlayback(resetFrame: false);
            reason = null;
            return true;
        }

        if (!CanPlay(out reason))
            return false;

        int duration = Mathf.Max(1, CalculateSequenceDurationFrames());
        if (CurrentFrame >= duration)
            CurrentFrame = 0;

        IsPlaying = true;
        LastPlaybackTimestamp = timestamp;
        return true;
    }

    public void StopPlayback(bool resetFrame)
    {
        IsPlaying = false;
        LastPlaybackTimestamp = 0d;
        if (resetFrame)
            CurrentFrame = 0;
    }

    public bool AdvancePlayback(double timestamp)
    {
        if (!IsPlaying || !CanPlay(out _))
            return false;

        double delta = Math.Max(0d, timestamp - LastPlaybackTimestamp);
        int frameDelta = Mathf.FloorToInt((float)(delta * document.Sequence.FrameRate));
        if (frameDelta <= 0)
            return false;

        LastPlaybackTimestamp += frameDelta / (double)document.Sequence.FrameRate;
        int duration = Mathf.Max(1, CalculateSequenceDurationFrames());
        CurrentFrame = Mathf.Min(duration, CurrentFrame + frameDelta);
        if (CurrentFrame >= duration)
            IsPlaying = false;

        ClampCurrentFrame();
        return true;
    }

    public void BeginInteractionPreview(ActionSequenceClipSnapshot clip, ActionSequenceDisplayClip displayClip, ActionSequenceClipTimingEditMode mode)
    {
        if (clip == null || displayClip.Snapshot == null)
        {
            ClearInteractionPreview();
            return;
        }

        InteractionPreview = new ActionSequenceClipTimingPreview(
            clip.EditorId,
            displayClip.RenderKey,
            mode,
            displayClip.SafeStartFrame,
            displayClip.SafeEndFrame,
            displayClip.SafeStartFrame,
            displayClip.SafeEndFrame);
    }

    public void UpdateInteractionPreview(int startFrame, int endFrame)
    {
        if (!InteractionPreview.IsActive)
            return;

        InteractionPreview = InteractionPreview.WithTiming(startFrame, endFrame);
        if (document != null && document.IsSupported && document.Sequence.DurationMode == ActionSequenceDurationMode.AutoFromClips)
            EnsureWorkspaceEndFrame(endFrame + 8);
    }

    public void ClearInteractionPreview()
    {
        InteractionPreview = default;
    }

    public void EnsureWorkspaceEndFrame(int requiredFrame)
    {
        if (requiredFrame <= ViewEndFrame)
            return;

        ViewEndFrame = Mathf.Max(ViewEndFrame, requiredFrame);
        UpdateTransform();
    }

    public void SelectSequence()
    {
        SetLocalSelection(new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null));
    }

    public void SelectTrack(string trackId)
    {
        if (!TryResolveTrack(trackId, out ActionSequenceTrackSnapshot track))
        {
            SelectSequence();
            return;
        }

        SetLocalSelection(new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Track, track.EditorId, null));
    }

    public void SelectClip(string clipId)
    {
        if (!TryResolveClip(clipId, out ActionSequenceTrackSnapshot track, out ActionSequenceClipSnapshot clip))
        {
            SelectSequence();
            return;
        }

        if (track.Locked)
        {
            SelectTrack(track.EditorId);
            return;
        }

        SetLocalSelection(new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Clip, track.EditorId, clip.EditorId));
    }

    public void ApplySelectionSuggestion(ActionSequenceEditorSelectionSuggestion suggestion)
    {
        switch (suggestion.Kind)
        {
            case ActionSequenceEditorDocumentItemKind.Track:
                SelectTrack(suggestion.EditorId);
                break;
            case ActionSequenceEditorDocumentItemKind.Clip:
                SelectClip(suggestion.EditorId);
                break;
            default:
                SelectSequence();
                break;
        }
    }

    public void RestoreLocalSelection(ActionSequenceEditorSelection.SelectionKind kind, string trackId, string clipId)
    {
        SetLocalSelection(new ActionSequenceEditorSelectionValue(Target, kind, trackId, clipId), publish: false);
        ValidateLocalSelection();
    }

    public bool IsTrackSelected(ActionSequenceTrackSnapshot track)
    {
        return track != null
            && LocalSelection.Target == Target
            && LocalSelection.Kind == ActionSequenceEditorSelection.SelectionKind.Track
            && string.Equals(LocalSelection.TrackId, track.EditorId, StringComparison.Ordinal);
    }

    public bool IsClipSelected(ActionSequenceClipSnapshot clip)
    {
        return clip != null
            && LocalSelection.Target == Target
            && LocalSelection.Kind == ActionSequenceEditorSelection.SelectionKind.Clip
            && string.Equals(LocalSelection.ClipId, clip.EditorId, StringComparison.Ordinal);
    }

    public void PublishSelection()
    {
        PublishLocalSelection();
    }

    public int CalculateSequenceDurationFrames()
    {
        if (document == null || !document.IsSupported)
            return MinimumViewEndFrame;

        ActionSequenceSnapshot sequence = document.Sequence;
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
            return Mathf.Max(1, sequence.FixedDurationFrames);

        int maxEnd = 1;
        for (int i = 0; i < displayTracks.Count; i++)
        {
            IReadOnlyList<ActionSequenceDisplayClip> clips = displayTracks[i].Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                maxEnd = Mathf.Max(maxEnd, clips[clipIndex].SafeEndFrame);
        }

        for (int i = 0; i < document.LegacyClips.Count; i++)
            maxEnd = Mathf.Max(maxEnd, CalculateSafeEndFrame(document.LegacyClips[i]));

        return maxEnd;
    }

    public bool TryResolveTrackSnapshot(string trackId, out ActionSequenceTrackSnapshot track)
    {
        return TryResolveTrack(trackId, out track);
    }

    public bool TryResolveClipSnapshot(string clipId, out ActionSequenceTrackSnapshot track, out ActionSequenceClipSnapshot clip)
    {
        return TryResolveClip(clipId, out track, out clip);
    }

    public bool TryFindDisplayClip(string clipId, out ActionSequenceDisplayTrack displayTrack, out ActionSequenceDisplayClip displayClip)
    {
        displayTrack = null;
        displayClip = default;
        if (string.IsNullOrEmpty(clipId))
            return false;

        for (int trackIndex = 0; trackIndex < displayTracks.Count; trackIndex++)
        {
            ActionSequenceDisplayTrack track = displayTracks[trackIndex];
            IReadOnlyList<ActionSequenceDisplayClip> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                if (!string.Equals(clips[clipIndex].Snapshot.EditorId, clipId, StringComparison.Ordinal))
                    continue;

                displayTrack = track;
                displayClip = clips[clipIndex];
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        document?.Dispose();
        document = null;
    }

    private void SetLocalSelection(ActionSequenceEditorSelectionValue value, bool publish = true)
    {
        if (Target == null || !IsSupported)
            value = default;
        else if (value.Kind == ActionSequenceEditorSelection.SelectionKind.None)
            value = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null);

        LocalSelection = value;
        if (publish)
            PublishLocalSelection();
    }

    private void PublishLocalSelection()
    {
        switch (LocalSelection.Kind)
        {
            case ActionSequenceEditorSelection.SelectionKind.Track:
                ActionSequenceEditorSelection.SelectTrack(Target, LocalSelection.TrackId);
                break;
            case ActionSequenceEditorSelection.SelectionKind.Clip:
                ActionSequenceEditorSelection.SelectClip(Target, LocalSelection.ClipId);
                break;
            case ActionSequenceEditorSelection.SelectionKind.Sequence:
                ActionSequenceEditorSelection.SelectSequence(Target);
                break;
            default:
                if (Target == null)
                    ActionSequenceEditorSelection.Clear();
                else
                    ActionSequenceEditorSelection.ClearIfTarget(Target);
                break;
        }
    }

    private void ValidateLocalSelection()
    {
        if (Target == null || !IsSupported)
        {
            LocalSelection = default;
            return;
        }

        switch (LocalSelection.Kind)
        {
            case ActionSequenceEditorSelection.SelectionKind.Track:
                if (TryResolveTrack(LocalSelection.TrackId, out ActionSequenceTrackSnapshot track))
                    LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Track, track.EditorId, null);
                else
                    LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null);
                break;
            case ActionSequenceEditorSelection.SelectionKind.Clip:
                if (TryResolveClip(LocalSelection.ClipId, out ActionSequenceTrackSnapshot owner, out ActionSequenceClipSnapshot clip))
                {
                    LocalSelection = owner.Locked
                        ? new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Track, owner.EditorId, null)
                        : new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Clip, owner.EditorId, clip.EditorId);
                }
                else if (TryResolveTrack(LocalSelection.TrackId, out ActionSequenceTrackSnapshot fallbackTrack))
                {
                    LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Track, fallbackTrack.EditorId, null);
                }
                else
                {
                    LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null);
                }
                break;
            case ActionSequenceEditorSelection.SelectionKind.Sequence:
                LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null);
                break;
            default:
                LocalSelection = new ActionSequenceEditorSelectionValue(Target, ActionSequenceEditorSelection.SelectionKind.Sequence, null, null);
                break;
        }
    }

    private bool TryResolveTrack(string trackId, out ActionSequenceTrackSnapshot track)
    {
        track = null;
        if (document == null || !document.IsSupported || string.IsNullOrEmpty(trackId))
            return false;

        ActionSequenceEditorResolveStatus status = document.ResolveTrack(trackId, out int trackIndex);
        if (status != ActionSequenceEditorResolveStatus.Found || trackIndex < 0 || trackIndex >= document.Tracks.Count)
            return false;

        track = document.Tracks[trackIndex];
        return !track.IsNull && !track.MissingType;
    }

    private bool TryResolveClip(string clipId, out ActionSequenceTrackSnapshot track, out ActionSequenceClipSnapshot clip)
    {
        track = null;
        clip = null;
        if (document == null || !document.IsSupported || string.IsNullOrEmpty(clipId))
            return false;

        ActionSequenceEditorResolveStatus status = document.ResolveClip(clipId, out int trackIndex, out int clipIndex);
        if (status != ActionSequenceEditorResolveStatus.Found || trackIndex < 0 || trackIndex >= document.Tracks.Count)
            return false;

        ActionSequenceTrackSnapshot resolvedTrack = document.Tracks[trackIndex];
        if (clipIndex < 0 || clipIndex >= resolvedTrack.Clips.Count)
            return false;

        ActionSequenceClipSnapshot resolvedClip = resolvedTrack.Clips[clipIndex];
        if (resolvedClip.IsNull || resolvedClip.MissingType)
            return false;

        track = resolvedTrack;
        clip = resolvedClip;
        return true;
    }

    private void RebuildDisplayModel()
    {
        displayTracks.Clear();
        if (document == null || !document.IsSupported)
        {
            ViewEndFrame = MinimumViewEndFrame;
            return;
        }

        var tracks = new List<ActionSequenceTrackSnapshot>(document.Tracks);
        tracks.Sort(CompareDisplayTracks);

        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = tracks[i];
            var displayTrack = new ActionSequenceDisplayTrack(track, BuildTrackRenderKey(track, document));
            IReadOnlyList<ActionSequenceClipSnapshot> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                ActionSequenceClipSnapshot clip = clips[clipIndex];
                int safeStart = CalculateSafeStartFrame(clip);
                int safeEnd = CalculateSafeEndFrame(clip);
                displayTrack.AddClip(new ActionSequenceDisplayClip(clip, BuildClipRenderKey(clip, document), safeStart, safeEnd));
            }

            displayTracks.Add(displayTrack);
        }

        ViewEndFrame = Mathf.Max(ViewEndFrame, CalculateViewEndFrame());
    }

    private int CalculateViewEndFrame()
    {
        if (document == null || !document.IsSupported)
            return MinimumViewEndFrame;

        int sequenceDuration = CalculateSequenceDurationFrames();
        int maxClipEndWithPadding = 1;
        for (int trackIndex = 0; trackIndex < displayTracks.Count; trackIndex++)
        {
            IReadOnlyList<ActionSequenceDisplayClip> clips = displayTracks[trackIndex].Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                maxClipEndWithPadding = Mathf.Max(maxClipEndWithPadding, clips[clipIndex].SafeEndFrame + 8);
        }

        for (int i = 0; i < document.LegacyClips.Count; i++)
            maxClipEndWithPadding = Mathf.Max(maxClipEndWithPadding, CalculateSafeEndFrame(document.LegacyClips[i]) + 8);

        return Mathf.Max(MinimumViewEndFrame, sequenceDuration, maxClipEndWithPadding);
    }

    private void UpdateTransform()
    {
        Transform.Configure(PixelsPerFrame, HorizontalScroll, ViewportWidth, ViewEndFrame);
        PixelsPerFrame = Transform.PixelsPerFrame;
        HorizontalScroll = Transform.ScrollX;
    }

    private void ClampCurrentFrame()
    {
        CurrentFrame = Mathf.Clamp(CurrentFrame, 0, ViewEndFrame);
    }

    private static int CompareDisplayTracks(ActionSequenceTrackSnapshot left, ActionSequenceTrackSnapshot right)
    {
        int phase = left.Phase.CompareTo(right.Phase);
        if (phase != 0)
            return phase;

        return left.TrackIndex.CompareTo(right.TrackIndex);
    }

    private static int CalculateSafeStartFrame(ActionSequenceClipSnapshot clip)
    {
        return Mathf.Max(0, clip.StartFrame);
    }

    private static int CalculateSafeEndFrame(ActionSequenceClipSnapshot clip)
    {
        int start = CalculateSafeStartFrame(clip);
        return Mathf.Max(start + 1, clip.EndFrame);
    }

    private static string BuildTrackRenderKey(ActionSequenceTrackSnapshot track, ActionSequenceSerializedDocument document)
    {
        if (!string.IsNullOrEmpty(track.EditorId) && !document.HasDuplicateId(track.EditorId))
            return "track:" + track.EditorId;

        return $"track:fallback:{track.TrackIndex}:{track.ManagedReferenceId}";
    }

    private static string BuildClipRenderKey(ActionSequenceClipSnapshot clip, ActionSequenceSerializedDocument document)
    {
        if (!string.IsNullOrEmpty(clip.EditorId) && !document.HasDuplicateId(clip.EditorId))
            return "clip:" + clip.EditorId;

        return $"clip:fallback:{clip.TrackIndex}:{clip.ClipIndex}:{clip.ManagedReferenceId}";
    }
}

public enum ActionSequenceClipTimingEditMode
{
    None,
    Move,
    ResizeLeft,
    ResizeRight,
}

public readonly struct ActionSequenceClipTimingPreview
{
    public ActionSequenceClipTimingPreview(
        string clipId,
        string renderKey,
        ActionSequenceClipTimingEditMode mode,
        int originalStartFrame,
        int originalEndFrame,
        int startFrame,
        int endFrame)
    {
        ClipId = clipId;
        RenderKey = renderKey;
        Mode = mode;
        OriginalStartFrame = originalStartFrame;
        OriginalEndFrame = originalEndFrame;
        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public string ClipId { get; }
    public string RenderKey { get; }
    public ActionSequenceClipTimingEditMode Mode { get; }
    public int OriginalStartFrame { get; }
    public int OriginalEndFrame { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }
    public bool IsActive => !string.IsNullOrEmpty(ClipId) && Mode != ActionSequenceClipTimingEditMode.None;

    public ActionSequenceClipTimingPreview WithTiming(int startFrame, int endFrame)
    {
        return new ActionSequenceClipTimingPreview(ClipId, RenderKey, Mode, OriginalStartFrame, OriginalEndFrame, startFrame, endFrame);
    }
}
#endif
