using System;
using System.Collections.Generic;
using UnityEngine;

public enum ActionSequenceFrameTransactionState
{
    Idle = 0,
    Begun = 1,
    PreWorldComplete = 2,
    PostWorldComplete = 3,
}

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
    private ActionSequenceContext _frameContext;
    private int _pendingFrame = -1;
    private bool _poseBaselineApplied;

    public ActionSequenceAsset Asset { get; private set; }
    public ActionSequenceData Data { get; private set; }
    public int CurrentFrame { get; private set; } = -1;
    public bool IsPlaying { get; private set; }
    public bool IsComplete { get; private set; }
    public ActionSequenceFrameTransactionState FrameTransactionState { get; private set; }
    public ActionSequenceRuntimeDiagnostics Diagnostics { get; } = new ActionSequenceRuntimeDiagnostics();

    public int DurationFrames => Data != null ? Data.DurationFrames : 0;
    public int FrameRate => Data != null ? Data.FrameRate : CombatSimulationTiming.FrameRate;
    public float NormalizedTime => DurationFrames > 0 ? Mathf.Clamp01((CurrentFrame + 1f) / DurationFrames) : 0f;
    public bool HasOpenFrame => FrameTransactionState != ActionSequenceFrameTransactionState.Idle;
    public int PendingFrame => HasOpenFrame ? _pendingFrame : -1;

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
        ResetFrameTransaction();
        CurrentFrame = -1;
        _poseBaselineApplied = false;
        IsPlaying = data != null;
        IsComplete = data == null;

        if (data == null)
            return;

        if (!CombatSimulationTiming.IsGameplayFrameRate(data.FrameRate))
        {
            Diagnostics.Add(new ActionSequenceRuntimeDiagnostic(
                ActionSequenceRuntimeDiagnosticCode.UnsupportedGameplayFrameRate,
                $"Gameplay ActionSequence frame rate must be {CombatSimulationTiming.FrameRate} Hz, but data uses {data.FrameRate} Hz."));
            IsPlaying = false;
            IsComplete = true;
            return;
        }

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

        EnsureNoOpenFrame(nameof(Tick));

        if (deltaSeconds <= 0f || speedScale <= 0f)
            return 0;

        _frameAccumulator += deltaSeconds * speedScale * FrameRate;
        int processedFrames = 0;

        while (_frameAccumulator >= 1f && !IsComplete)
        {
            _frameAccumulator -= 1f;
            StepFrame(context, CombatSimulationTiming.FixedDeltaTime, speedScale);
            processedFrames++;
        }

        return processedFrames;
    }

    public bool StepFrame(ActionSequenceContext context)
    {
        return StepFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f);
    }

    public bool ApplyPoseBaseline(ActionSequenceContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        EnsureNoOpenFrame(nameof(ApplyPoseBaseline));

        if (_poseBaselineApplied || !IsPlaying || IsComplete || Data == null || DurationFrames <= 0)
            return false;

        _poseBaselineApplied = true;

        context.Frame = 0;
        context.PoseFrame = 0f;
        context.FrameRate = FrameRate;
        context.DeltaTime = 0f;
        context.SpeedScale = 0f;
        context.IsPoseBaseline = true;
        context.IsPoseRefresh = false;

        try
        {
            EnterClipsStartingAt(
                0,
                context,
                ActionSequenceClipPhase.Animation,
                ActionSequenceClipPhase.Animation);
            TickActiveClips(
                context,
                ActionSequenceClipPhase.Animation,
                ActionSequenceClipPhase.Animation);
        }
        finally
        {
            context.IsPoseBaseline = false;
            context.IsPoseRefresh = false;
        }

        return true;
    }

    public bool RefreshPose(ActionSequenceContext context, float poseFrame)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        EnsureNoOpenFrame(nameof(RefreshPose));

        if (!IsPlaying || IsComplete || Data == null || DurationFrames <= 0)
            return false;

        poseFrame = Mathf.Max(0f, poseFrame);

        context.Frame = Mathf.Max(0, Mathf.FloorToInt(poseFrame));
        context.PoseFrame = poseFrame;
        context.FrameRate = FrameRate;
        context.DeltaTime = 0f;
        context.SpeedScale = 0f;
        context.IsPoseBaseline = false;
        context.IsPoseRefresh = true;

        try
        {
            TickActiveClips(
                context,
                ActionSequenceClipPhase.Animation,
                ActionSequenceClipPhase.Animation);
        }
        finally
        {
            context.IsPoseRefresh = false;
        }

        return true;
    }

    public bool BeginFrame(ActionSequenceContext context)
    {
        return BeginFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f);
    }

    public bool BeginFrame(ActionSequenceContext context, float deltaTime, float speedScale)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        EnsureNoOpenFrame(nameof(BeginFrame));

        if (!IsPlaying || IsComplete || Data == null)
            return false;

        int nextFrame = CurrentFrame + 1;
        if (nextFrame >= DurationFrames)
        {
            CompleteWithoutFrame(context);
            return false;
        }

        _frameContext = context;
        _pendingFrame = nextFrame;
        FrameTransactionState = ActionSequenceFrameTransactionState.Begun;

        context.Frame = nextFrame;
        context.PoseFrame = nextFrame + 1f;
        context.FrameRate = FrameRate;
        context.DeltaTime = deltaTime;
        context.SpeedScale = speedScale;
        context.IsPoseBaseline = false;
        context.IsPoseRefresh = false;

        EnterClipsStartingAt(nextFrame, context);
        return true;
    }

    public void ExecutePreWorld()
    {
        RequireFrameState(ActionSequenceFrameTransactionState.Begun, nameof(ExecutePreWorld));
        TickActiveClips(
            _frameContext,
            ActionSequenceClipPhase.State,
            ActionSequenceClipPhase.Motion);
        FrameTransactionState = ActionSequenceFrameTransactionState.PreWorldComplete;
    }

    public void ExecutePostWorld()
    {
        ExecutePostWorld(null);
    }

    internal void ExecutePostWorld(ICombatHitIntentSink hitIntentSink)
    {
        RequireFrameState(ActionSequenceFrameTransactionState.PreWorldComplete, nameof(ExecutePostWorld));
        _frameContext.HitIntentSink = hitIntentSink;
        try
        {
            TickActiveClips(
                _frameContext,
                ActionSequenceClipPhase.HitBox,
                ActionSequenceClipPhase.Cleanup);
            FrameTransactionState = ActionSequenceFrameTransactionState.PostWorldComplete;
        }
        finally
        {
            _frameContext.HitIntentSink = null;
        }
    }

    public void EndFrame()
    {
        RequireFrameState(ActionSequenceFrameTransactionState.PostWorldComplete, nameof(EndFrame));

        int completedFrame = _pendingFrame;
        ActionSequenceContext context = _frameContext;
        context.Frame = completedFrame + 1;
        ExitClipsEndingAt(completedFrame + 1, context, true);

        CurrentFrame = completedFrame;
        if (CurrentFrame >= DurationFrames - 1)
        {
            ExitAll(context, true);
            IsComplete = true;
            IsPlaying = false;
        }
        else
        {
            context.Frame = completedFrame;
        }

        ResetFrameTransaction();
    }

    public void Cancel(ActionSequenceContext context)
    {
        if (!IsPlaying || Data == null)
            return;

        ActionSequenceContext exitContext = _frameContext ?? context;
        ExitAll(exitContext, false);
        ResetFrameTransaction();
        IsPlaying = false;
        IsComplete = true;
    }

    private bool StepFrame(ActionSequenceContext context, float deltaTime, float speedScale)
    {
        if (!IsPlaying || IsComplete || Data == null)
            return false;

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        EnsureNoOpenFrame(nameof(StepFrame));

        if (!BeginFrame(context, deltaTime, speedScale))
            return false;

        ExecutePreWorld();
        ExecutePostWorld();
        EndFrame();
        return true;
    }

    private void EnterClipsStartingAt(int frame, ActionSequenceContext context)
    {
        EnterClipsStartingAt(
            frame,
            context,
            ActionSequenceClipPhase.State,
            ActionSequenceClipPhase.Cleanup);
    }

    private void EnterClipsStartingAt(
        int frame,
        ActionSequenceContext context,
        ActionSequenceClipPhase firstPhase,
        ActionSequenceClipPhase lastPhase)
    {
        for (int i = 0; i < _clips.Count; i++)
        {
            ClipRecord record = _clips[i];
            if (record.Active || record.StartFrame != frame)
                continue;
            if ((int)record.Phase < (int)firstPhase || (int)record.Phase > (int)lastPhase)
                continue;

            record.Active = true;
            _activeClips.Add(record);
            record.Runtime?.OnEnter(context);
        }

        _activeClips.Sort(CompareClipRecords);
    }

    private void TickActiveClips(
        ActionSequenceContext context,
        ActionSequenceClipPhase firstPhase,
        ActionSequenceClipPhase lastPhase)
    {
        for (int i = 0; i < _activeClips.Count; i++)
        {
            ClipRecord record = _activeClips[i];
            if (!record.Active
                || (int)record.Phase < (int)firstPhase
                || (int)record.Phase > (int)lastPhase)
                continue;

            if (context.Frame >= record.StartFrame && context.Frame < record.EndFrame)
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

    private void CompleteWithoutFrame(ActionSequenceContext context)
    {
        if (IsComplete)
            return;

        context.Frame = DurationFrames;
        ExitAll(context, true);
        IsComplete = true;
        IsPlaying = false;
    }

    private void EnsureNoOpenFrame(string operation)
    {
        if (!HasOpenFrame)
            return;

        throw new InvalidOperationException(
            $"{operation} cannot run while frame {_pendingFrame} is in state {FrameTransactionState}.");
    }

    private void RequireFrameState(ActionSequenceFrameTransactionState expectedState, string operation)
    {
        if (FrameTransactionState == expectedState)
            return;

        throw new InvalidOperationException(
            $"{operation} requires frame state {expectedState}, but the current state is {FrameTransactionState}.");
    }

    private void ResetFrameTransaction()
    {
        _frameContext = null;
        _pendingFrame = -1;
        FrameTransactionState = ActionSequenceFrameTransactionState.Idle;
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
