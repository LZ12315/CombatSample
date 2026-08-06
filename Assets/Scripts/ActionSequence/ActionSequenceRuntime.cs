using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActionSequenceRuntime
{
    private sealed class ClipRecord
    {
        public ActionSequenceClipDefinition Definition;
        public ActionSequenceClipRuntime Runtime;
        public ActionSequenceClipPhase Phase;
        public int TrackIndex;
        public int ClipIndex;
        public int StartFrame;
        public int EndFrame;
        public bool Active;
    }

    private readonly List<ClipRecord> _clips = new List<ClipRecord>();
    private readonly List<ClipRecord> _activeClips = new List<ClipRecord>();
    private float _frameAccumulator;

    public ActionSequenceAsset Asset { get; private set; }
    public ActionSequenceData Data { get; private set; }
    public int CurrentFrame { get; private set; } = -1;
    public bool IsPlaying { get; private set; }
    public bool IsComplete { get; private set; }
    public ActionSequenceRuntimeDiagnostics Diagnostics { get; } = new ActionSequenceRuntimeDiagnostics();

    public int DurationFrames => Data != null ? Data.DurationFrames : 0;
    public int FrameRate => Data != null ? Data.FrameRate : 60;
    public float NormalizedTime => DurationFrames > 0 ? Mathf.Clamp01((CurrentFrame + 1f) / DurationFrames) : 0f;

    public ActionSequenceRuntime(ActionSequenceAsset asset)
    {
        Initialize(asset);
    }

    public ActionSequenceRuntime(ActionSequenceData data)
    {
        Initialize(data);
    }

    public void Initialize(ActionSequenceAsset asset)
    {
        Initialize(asset != null ? asset.Data : null);
        Asset = asset;
    }

    public void Initialize(ActionSequenceData data)
    {
        Asset = null;
        Data = data;
        _clips.Clear();
        _activeClips.Clear();
        Diagnostics.Clear();
        _frameAccumulator = 0f;
        CurrentFrame = -1;
        IsPlaying = data != null;
        IsComplete = data == null;

        if (data == null)
            return;

        IReadOnlyList<ActionSequenceTrackDefinition> tracks = data.Tracks;
        if (tracks != null)
        {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                ActionSequenceTrackDefinition track = tracks[trackIndex];
                if (track == null)
                {
                    Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                        ActionSequenceRuntimeDiagnosticCode.NullTrack,
                        "Track is null and was skipped.",
                        trackIndex));
                    continue;
                }

                if (track.muted)
                    continue;

                IReadOnlyList<ActionSequenceClipDefinition> definitions = track.Clips;
                if (definitions == null)
                    continue;

                for (int clipIndex = 0; clipIndex < definitions.Count; clipIndex++)
                    AddClipRecord(data, track, definitions[clipIndex], trackIndex, clipIndex);
            }
        }

        AddLegacyClipRecords(data);
        _clips.Sort(CompareClipRecords);
    }

    public int Tick(ActionSequenceContext context, float deltaSeconds, float speedScale = 1f)
    {
        if (!IsPlaying || IsComplete || Data == null)
            return 0;

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (deltaSeconds <= 0f || speedScale <= 0f)
            return 0;

        _frameAccumulator += deltaSeconds * speedScale * FrameRate;
        int processedFrames = 0;

        while (_frameAccumulator >= 1f && !IsComplete)
        {
            _frameAccumulator -= 1f;
            StepFrame(context, 1f / FrameRate, speedScale);
            processedFrames++;
        }

        return processedFrames;
    }

    public bool StepFrame(ActionSequenceContext context)
    {
        return StepFrame(context, 1f / FrameRate, 1f);
    }

    public void Cancel(ActionSequenceContext context)
    {
        if (!IsPlaying || Data == null)
            return;

        ExitAll(context, false);
        IsPlaying = false;
        IsComplete = true;
    }

    private bool StepFrame(ActionSequenceContext context, float deltaTime, float speedScale)
    {
        if (!IsPlaying || IsComplete || Data == null)
            return false;

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        int nextFrame = CurrentFrame + 1;
        if (nextFrame >= DurationFrames)
        {
            Complete(context);
            return false;
        }

        context.Frame = nextFrame;
        context.FrameRate = FrameRate;
        context.DeltaTime = deltaTime;
        context.SpeedScale = speedScale;

        ExitClipsEndingAt(nextFrame, context, true);
        EnterClipsStartingAt(nextFrame, context);
        TickActiveClips(context);

        CurrentFrame = nextFrame;

        if (CurrentFrame >= DurationFrames - 1)
        {
            context.Frame = DurationFrames;
            Complete(context);
        }

        return true;
    }

    private void EnterClipsStartingAt(int frame, ActionSequenceContext context)
    {
        for (int i = 0; i < _clips.Count; i++)
        {
            ClipRecord record = _clips[i];
            if (record.Active || record.StartFrame != frame)
                continue;

            record.Active = true;
            _activeClips.Add(record);
            record.Runtime?.OnEnter(context);
        }
    }

    private void TickActiveClips(ActionSequenceContext context)
    {
        for (int i = 0; i < _activeClips.Count; i++)
        {
            ClipRecord record = _activeClips[i];
            if (record.Active && context.Frame >= record.StartFrame && context.Frame < record.EndFrame)
                record.Runtime?.OnTick(context);
        }
    }

    private void ExitClipsEndingAt(int frame, ActionSequenceContext context, bool completed)
    {
        for (int i = _activeClips.Count - 1; i >= 0; i--)
        {
            ClipRecord record = _activeClips[i];
            if (!record.Active || record.EndFrame > frame)
                continue;

            record.Active = false;
            _activeClips.RemoveAt(i);
            record.Runtime?.OnExit(context, completed);
        }
    }

    private void Complete(ActionSequenceContext context)
    {
        if (IsComplete)
            return;

        ExitAll(context, true);
        IsComplete = true;
        IsPlaying = false;
    }

    private void ExitAll(ActionSequenceContext context, bool completed)
    {
        for (int i = _activeClips.Count - 1; i >= 0; i--)
        {
            ClipRecord record = _activeClips[i];
            if (!record.Active)
                continue;

            record.Active = false;
            record.Runtime?.OnExit(context, completed);
        }

        _activeClips.Clear();
    }

    private static int CompareClipRecords(ClipRecord a, ClipRecord b)
    {
        int phaseCompare = a.Phase.CompareTo(b.Phase);
        if (phaseCompare != 0)
            return phaseCompare;

        int trackCompare = a.TrackIndex.CompareTo(b.TrackIndex);
        if (trackCompare != 0)
            return trackCompare;

        int startCompare = a.StartFrame.CompareTo(b.StartFrame);
        if (startCompare != 0)
            return startCompare;

        return a.ClipIndex.CompareTo(b.ClipIndex);
    }

    private void AddClipRecord(
        ActionSequenceData data,
        ActionSequenceTrackDefinition track,
        ActionSequenceClipDefinition definition,
        int trackIndex,
        int clipIndex)
    {
        if (definition == null)
        {
            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.NullClip,
                "Clip is null and was skipped.",
                trackIndex,
                clipIndex));
            return;
        }

        if (definition.Phase != track.Phase)
        {
            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.PhaseMismatch,
                $"Clip phase {definition.Phase} does not match track phase {track.Phase}.",
                trackIndex,
                clipIndex));
            return;
        }

        if (!track.AllowsClipType(definition.GetType()))
        {
            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.DisallowedClipType,
                $"{definition.GetType().Name} is not allowed on {track.GetType().Name}.",
                trackIndex,
                clipIndex));
            return;
        }

        if (!TryGetRuntimeInterval(data, definition, trackIndex, clipIndex, false, Diagnostics, out int startFrame, out int endFrame))
            return;

        ActionSequenceClipRuntime runtime = definition.CreateRuntime();
        if (runtime == null)
        {
            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.NullClipRuntime,
                $"{definition.GetType().Name} returned a null runtime and was skipped.",
                trackIndex,
                clipIndex));
            return;
        }

        _clips.Add(new ClipRecord
        {
            Definition = definition,
            Runtime = runtime,
            Phase = track.Phase,
            TrackIndex = trackIndex,
            ClipIndex = clipIndex,
            StartFrame = startFrame,
            EndFrame = endFrame,
        });
    }

    private void AddLegacyClipRecords(ActionSequenceData data)
    {
        IReadOnlyList<ActionSequenceClipDefinition> legacyClips = data.LegacyClips;
        if (legacyClips == null)
            return;

        for (int i = 0; i < legacyClips.Count; i++)
        {
            ActionSequenceClipDefinition definition = legacyClips[i];
            if (definition == null)
            {
                Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                    ActionSequenceRuntimeDiagnosticCode.NullClip,
                    "Legacy clip is null and was skipped.",
                    -1,
                    i,
                    true));
                continue;
            }

            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.LegacyClipProjection,
                "Legacy flat clip was projected into runtime without migrating asset data.",
                -1,
                i,
                true));

            if (!TryGetRuntimeInterval(data, definition, -1, i, true, Diagnostics, out int startFrame, out int endFrame))
                continue;

            ActionSequenceClipRuntime runtime = definition.CreateRuntime();
            if (runtime == null)
            {
                Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                    ActionSequenceRuntimeDiagnosticCode.NullClipRuntime,
                    $"{definition.GetType().Name} returned a null runtime and was skipped.",
                    -1,
                    i,
                    true));
                continue;
            }

            _clips.Add(new ClipRecord
            {
                Definition = definition,
                Runtime = runtime,
                Phase = definition.Phase,
                TrackIndex = int.MaxValue,
                ClipIndex = i,
                StartFrame = startFrame,
                EndFrame = endFrame,
            });
        }
    }

    private static bool TryGetRuntimeInterval(
        ActionSequenceData data,
        ActionSequenceClipDefinition definition,
        int trackIndex,
        int clipIndex,
        bool isLegacy,
        ActionSequenceRuntimeDiagnostics diagnostics,
        out int startFrame,
        out int endFrame)
    {
        startFrame = Mathf.Max(0, definition.startFrame);
        endFrame = Mathf.Max(startFrame + 1, definition.endFrame);

        if (startFrame != definition.startFrame || endFrame != definition.endFrame)
        {
            diagnostics?.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.TimingAdjusted,
                $"Timing [{definition.startFrame}, {definition.endFrame}) was projected as [{startFrame}, {endFrame}) for runtime.",
                trackIndex,
                clipIndex,
                isLegacy));
        }

        if (data.DurationMode == ActionSequenceDurationMode.FixedFrames)
        {
            int duration = data.DurationFrames;
            if (duration <= 0)
                return false;

            if (startFrame >= duration)
            {
                diagnostics?.Add(new ActionSequenceRuntimeDiagnostic(
                    ActionSequenceRuntimeDiagnosticCode.FixedDurationClipSkipped,
                    $"Clip starts at {startFrame}, outside fixed duration {duration}.",
                    trackIndex,
                    clipIndex,
                    isLegacy));
                return false;
            }

            int unclampedEndFrame = endFrame;
            endFrame = Mathf.Min(endFrame, duration);
            if (endFrame != unclampedEndFrame)
            {
                diagnostics?.Add(new ActionSequenceRuntimeDiagnostic(
                    ActionSequenceRuntimeDiagnosticCode.FixedDurationClipTruncated,
                    $"Clip end {unclampedEndFrame} was truncated to fixed duration {duration}.",
                    trackIndex,
                    clipIndex,
                    isLegacy));
            }

            if (endFrame <= startFrame)
                return false;
        }

        return true;
    }
}
