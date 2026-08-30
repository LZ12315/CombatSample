using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

public enum ActionRuntimeState
{
    Created,
    Running,
    Completed,
    Interrupted,
    Aborted,
}

public enum ActionRuntimeTerminationResult
{
    Completed,
    Interrupted,
    Aborted,
}

public enum ActionRangeExitReason
{
    Completed,
    Interrupted,
}

/// <summary>One-shot timed work created lazily when its action frame opens.</summary>
public interface IActionPointRuntime
{
    void Execute(ActionRuntimeContext context);
}

/// <summary>Scoped timed work. Abort is intentionally distinct from normal Exit.</summary>
public interface IActionRangeRuntime
{
    void Enter(ActionRuntimeContext context);
    void Tick(ActionRuntimeContext context, int localFrame);
    void Exit(ActionRuntimeContext context, ActionRangeExitReason reason);
    void Abort(ActionRuntimeContext context);
}

/// <summary>
/// Narrow receiver boundary for V1 item runtimes. It does not expose timeline
/// data, Scheduler, ActionPlayer, ActionStateManager, or other item runtimes.
/// </summary>
public readonly struct ActionRuntimeContext
{
    internal ActionRuntimeContext(Actor actor, ActionContext actionContext)
    {
        Actor = actor;
        ActionContext = actionContext;
        Motor = actor != null ? actor.actorMotor : null;
    }

    public Actor Actor { get; }
    public ActionContext ActionContext { get; }
    public ActorMotor Motor { get; }
}

/// <summary>Immutable timing data retained by a scheduler for one execution.</summary>
public readonly struct ActionRuntimeAnimationRecord
{
    internal ActionRuntimeAnimationRecord(AnimationSegment segment)
    {
        StartFrame = segment.StartFrame;
        EndFrameExclusive = segment.EndFrameExclusiveLong >= int.MaxValue
            ? int.MaxValue
            : (int)segment.EndFrameExclusiveLong;
        AnimationAsset = segment.AnimationAsset;
        SourceStartTime = segment.SourceStartTime;
        SourceEndTime = segment.SourceEndTime;
        PlayRate = segment.PlayRate;
    }

    public int StartFrame { get; }
    public int EndFrameExclusive { get; }
    public AnimationAsset AnimationAsset { get; }
    public float SourceStartTime { get; }
    public float SourceEndTime { get; }
    public float PlayRate { get; }
}

/// <summary>
/// Action time and timed-content lifecycle authority. It has no arbitration,
/// composition, animation-player, or Gameplay-domain knowledge.
/// </summary>
public sealed class ActionRuntimeScheduler
{
    private readonly ActionRuntimeContext _context;
    private readonly Dictionary<int, List<PointGameplayItem>> _pointsByFrame = new Dictionary<int, List<PointGameplayItem>>();
    private readonly Dictionary<int, List<RangeRecord>> _rangesByStartFrame = new Dictionary<int, List<RangeRecord>>();
    private readonly List<ActiveRange> _activeRanges = new List<ActiveRange>();
    private readonly List<ActionRuntimeAnimationRecord> _animationRecords = new List<ActionRuntimeAnimationRecord>();

    private bool _started;
    private bool _hasOpenFrame;
    private bool _isCompleted;
    private bool _isTerminated;
    private int _currentFrame = -1;
    private double _continuousPosition;

    internal ActionRuntimeScheduler(ActionTimelineData timeline, ActionRuntimeContext context)
    {
        _context = context;
        DurationFrames = timeline != null ? timeline.DurationFrames : 1;
        CaptureSnapshot(timeline);
    }

    public int DurationFrames { get; }
    public int CurrentFrame => _currentFrame;
    public double ContinuousPosition => _continuousPosition;
    public bool HasOpenFrame => _hasOpenFrame;
    public bool IsCompleted => _isCompleted;
    public IReadOnlyList<ActionRuntimeAnimationRecord> AnimationRecords => _animationRecords;

    internal void Begin()
    {
        if (_started)
            throw new InvalidOperationException("ActionRuntimeScheduler can only begin once.");

        _started = true;
        OpenFrame(0);
    }

    /// <summary>Advances continuous action time and opens at most one new frame.</summary>
    internal bool Advance(float speed)
    {
        if (!_started || _hasOpenFrame || _isCompleted || _isTerminated)
            return false;
        if (!IsFinite(speed) || speed < 0f || speed > 1f)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "ActionRuntime speed must be within [0, 1].");
        if (speed <= 0f)
            return false;

        _continuousPosition += speed;
        int nextFrame = (int)Math.Floor(_continuousPosition + 1e-9d);
        if (nextFrame <= _currentFrame || nextFrame >= DurationFrames)
            return false;

        OpenFrame(nextFrame);
        return true;
    }

    internal bool Finish()
    {
        if (!_hasOpenFrame || _isTerminated)
            return false;

        int endExclusive = _currentFrame + 1;
        for (int index = 0; index < _activeRanges.Count;)
        {
            ActiveRange active = _activeRanges[index];
            if (active.Record.EndFrameExclusive != endExclusive)
            {
                index++;
                continue;
            }

            active.Runtime.Exit(_context, ActionRangeExitReason.Completed);
            _activeRanges.RemoveAt(index);
        }

        _hasOpenFrame = false;
        if (_currentFrame == DurationFrames - 1)
            _isCompleted = true;

        return _isCompleted;
    }

    /// <summary>Normal interruption is legal only outside an open frame.</summary>
    internal bool Interrupt()
    {
        if (_hasOpenFrame || _isCompleted || _isTerminated)
            return false;

        ExitAllRanges(ActionRangeExitReason.Interrupted);
        _isTerminated = true;
        return true;
    }

    /// <summary>Best-effort emergency cleanup. It never converts into Exit.</summary>
    internal void Abort()
    {
        if (_isTerminated || _isCompleted)
            return;

        for (int i = 0; i < _activeRanges.Count; i++)
        {
            try
            {
                _activeRanges[i].Runtime.Abort(_context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        _activeRanges.Clear();
        _hasOpenFrame = false;
        _isTerminated = true;
    }

    private void CaptureSnapshot(ActionTimelineData timeline)
    {
        if (timeline == null)
            return;

        IReadOnlyList<AnimationSegment> segments = timeline.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            if (segment != null)
                _animationRecords.Add(new ActionRuntimeAnimationRecord(segment));
        }

        IReadOnlyList<GameplayLane> lanes = timeline.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null)
                continue;

            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                if (item == null || lane.Muted || item.Muted)
                    continue;

                if (item is PointGameplayItem point && point.Frame >= 0)
                {
                    AddPoint(point.Frame, point);
                }
                else if (item is RangeGameplayItem range
                    && range.StartFrame >= 0
                    && range.DurationFrames > 0
                    && range.EndFrameExclusiveLong <= int.MaxValue)
                {
                    AddRange(new RangeRecord(range, range.StartFrame, (int)range.EndFrameExclusiveLong));
                }
            }
        }
    }

    private void OpenFrame(int frame)
    {
        if (_hasOpenFrame || frame < 0 || frame >= DurationFrames)
            throw new InvalidOperationException("ActionRuntimeScheduler cannot open the requested frame.");

        _currentFrame = frame;
        _hasOpenFrame = true;

        if (_pointsByFrame.TryGetValue(frame, out List<PointGameplayItem> points))
        {
            for (int i = 0; i < points.Count; i++)
            {
                IActionPointRuntime runtime = points[i].CreateRuntime();
                runtime?.Execute(_context);
            }
        }

        if (_rangesByStartFrame.TryGetValue(frame, out List<RangeRecord> starts))
        {
            for (int i = 0; i < starts.Count; i++)
            {
                RangeRecord record = starts[i];
                IActionRangeRuntime runtime = record.Item.CreateRuntime();
                if (runtime == null)
                    continue;

                runtime.Enter(_context);
                _activeRanges.Add(new ActiveRange(record, runtime));
            }
        }

        for (int i = 0; i < _activeRanges.Count; i++)
        {
            ActiveRange active = _activeRanges[i];
            active.Runtime.Tick(_context, frame - active.Record.StartFrame);
        }
    }

    private void AddPoint(int frame, PointGameplayItem item)
    {
        if (!_pointsByFrame.TryGetValue(frame, out List<PointGameplayItem> points))
        {
            points = new List<PointGameplayItem>();
            _pointsByFrame.Add(frame, points);
        }
        points.Add(item);
    }

    private void AddRange(RangeRecord record)
    {
        if (!_rangesByStartFrame.TryGetValue(record.StartFrame, out List<RangeRecord> ranges))
        {
            ranges = new List<RangeRecord>();
            _rangesByStartFrame.Add(record.StartFrame, ranges);
        }
        ranges.Add(record);
    }

    private void ExitAllRanges(ActionRangeExitReason reason)
    {
        for (int i = 0; i < _activeRanges.Count; i++)
            _activeRanges[i].Runtime.Exit(_context, reason);
        _activeRanges.Clear();
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private readonly struct RangeRecord
    {
        public RangeRecord(RangeGameplayItem item, int startFrame, int endFrameExclusive)
        {
            Item = item;
            StartFrame = startFrame;
            EndFrameExclusive = endFrameExclusive;
        }

        public RangeGameplayItem Item { get; }
        public int StartFrame { get; }
        public int EndFrameExclusive { get; }
    }

    private readonly struct ActiveRange
    {
        public ActiveRange(RangeRecord record, IActionRangeRuntime runtime)
        {
            Record = record;
            Runtime = runtime;
        }

        public RangeRecord Record { get; }
        public IActionRangeRuntime Runtime { get; }
    }
}

/// <summary>
/// One V1 Action execution. This Stage 2 type is intentionally not yet bound
/// to the existing ActionPlayer/Legacy playback chain.
/// </summary>
public sealed class ActionRuntime
{
    private readonly List<Tag> _acquiredSelfTags = new List<Tag>();
    private readonly Actor _actor;
    private readonly ActionContext _actionContext;
    private bool _paused;
    private float _speed = 1f;

    public ActionRuntime(ActionAsset actionAsset, Actor actor, ActionContext actionContext)
    {
        ActionAsset = actionAsset ?? throw new ArgumentNullException(nameof(actionAsset));
        _actor = actor;
        _actionContext = actionContext;
        State = ActionRuntimeState.Created;
    }

    public ActionAsset ActionAsset { get; }
    public ActionAsset Asset => ActionAsset;
    public ActionContext ActionContext => _actionContext;
    public ActionRuntimeState State { get; private set; }
    public ActionRuntimeTerminationResult? TerminationResult { get; private set; }
    internal ActionRuntimeScheduler Scheduler { get; private set; }
    public int CurrentFrame => Scheduler != null ? Scheduler.CurrentFrame : -1;
    public int DurationFrames => Scheduler != null ? Scheduler.DurationFrames : 0;
    public double ContinuousPosition => Scheduler != null ? Scheduler.ContinuousPosition : 0d;
    public IReadOnlyList<ActionRuntimeAnimationRecord> AnimationRecords => Scheduler != null
        ? Scheduler.AnimationRecords
        : Array.Empty<ActionRuntimeAnimationRecord>();
    public bool IsPlaying => State == ActionRuntimeState.Running;
    public bool HasOpenFrame => Scheduler != null && Scheduler.HasOpenFrame;
    public bool IsPaused => _paused;
    public float Speed => _speed;

    public void Begin()
    {
        if (State != ActionRuntimeState.Created)
            throw new InvalidOperationException("ActionRuntime can only begin from Created.");

        try
        {
            AcquireSelfTags();
            var context = new ActionRuntimeContext(_actor, _actionContext);
            Scheduler = new ActionRuntimeScheduler(ActionAsset.Timeline, context);
            State = ActionRuntimeState.Running;
            Scheduler.Begin();
        }
        catch
        {
            Abort();
            throw;
        }
    }

    public bool Advance()
    {
        if (!IsPlaying || _paused)
            return false;

        try
        {
            return Scheduler.Advance(_speed);
        }
        catch
        {
            Abort();
            throw;
        }
    }

    public bool Finish()
    {
        if (!IsPlaying)
            return false;

        try
        {
            bool completed = Scheduler.Finish();
            if (completed)
                Complete();
            return completed;
        }
        catch
        {
            Abort();
            throw;
        }
    }

    public bool Interrupt()
    {
        if (!IsPlaying || !Scheduler.Interrupt())
            return false;

        State = ActionRuntimeState.Interrupted;
        TerminationResult = ActionRuntimeTerminationResult.Interrupted;
        ReleaseSelfTags();
        return true;
    }

    public void Abort()
    {
        if (State == ActionRuntimeState.Completed
            || State == ActionRuntimeState.Interrupted
            || State == ActionRuntimeState.Aborted)
            return;

        try
        {
            Scheduler?.Abort();
        }
        finally
        {
            State = ActionRuntimeState.Aborted;
            TerminationResult = ActionRuntimeTerminationResult.Aborted;
            ReleaseSelfTags();
        }
    }

    public void Pause()
    {
        if (IsPlaying)
            _paused = true;
    }

    public void Resume()
    {
        if (IsPlaying)
            _paused = false;
    }

    public void SetSpeed(float speed)
    {
        if (!IsFinite(speed) || speed < 0f || speed > 1f)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "ActionRuntime speed must be within [0, 1].");
        _speed = speed;
    }

    private void Complete()
    {
        State = ActionRuntimeState.Completed;
        TerminationResult = ActionRuntimeTerminationResult.Completed;
        ReleaseSelfTags();
    }

    private void AcquireSelfTags()
    {
        IReadOnlyList<TagReference> selfTags = ActionAsset.SelfTags;
        for (int i = 0; _actor != null && selfTags != null && i < selfTags.Count; i++)
        {
            Tag tag = selfTags[i] != null ? selfTags[i].GetTag() : null;
            if (tag == null)
                continue;

            _actor.AddTag(tag, ActorTagContainerType.Transient);
            _acquiredSelfTags.Add(tag);
        }
    }

    private void ReleaseSelfTags()
    {
        for (int i = _acquiredSelfTags.Count - 1; _actor != null && i >= 0; i--)
            _actor.RemoveTag(_acquiredSelfTags[i], ActorTagContainerType.Transient);
        _acquiredSelfTags.Clear();
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
