using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public readonly struct LocomotionRuntimeMotionContext
{
    public LocomotionRuntimeMotionContext(LocomotionRunner motion, LocomotionIntent intent,
        bool hasIntent, LocomotionMotionContext motor)
    {
        Motion = motion;
        Intent = intent;
        HasIntent = hasIntent;
        Motor = motor;
    }

    public LocomotionRunner Motion { get; }
    public LocomotionIntent Intent { get; }
    public bool HasIntent { get; }
    public LocomotionMotionContext Motor { get; }
}

/// <summary>Mutable per-Actor execution state created by an immutable LocomotionAsset.</summary>
public abstract class LocomotionRuntime : IDisposable
{
    private bool _disposed;
    protected LocomotionRuntime(LocomotionAsset asset)
    {
        Asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
    }

    public LocomotionAsset Asset { get; }
    public bool IsEntered { get; private set; }

    public void Enter(ActorLocomotion owner, Actor actor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LocomotionRuntime));
        if (IsEntered)
            return;

        IsEntered = true;
        OnEnter(owner, actor);
    }

    public void Exit(ActorLocomotion owner, Actor actor)
    {
        if (!IsEntered)
            return;

        OnExit(owner, actor);
        IsEntered = false;
    }

    public abstract LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context);

    public virtual LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context) => default;
    public virtual void ResetAnimation() { }
    public virtual void SuspendAnimationFeedback() { }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        OnDispose();
        IsEntered = false;
    }

    protected virtual void OnEnter(ActorLocomotion owner, Actor actor) { }
    protected virtual void OnExit(ActorLocomotion owner, Actor actor) { }
    protected virtual void OnDispose() { }

    protected LocomotionMotionRequest UpdateSharedMotion(in LocomotionRuntimeMotionContext context)
    {
        if (context.Motion == null || context.Motor.EffectiveDeltaTime <= 0f)
            return default;

        context.Motion.Prepare(context.Intent, context.HasIntent, context.Motor.EffectiveDeltaTime,
            Asset.MovementConfig);
        return new LocomotionMotionRequest(
            context.Motion.CachedVelocity,
            context.Motion.PendingRotation,
            true,
            true);
    }
}

/// <summary>
/// Common cached Move states, bound from immutable configuration for this Runtime's lifetime.
/// All graph attachment happens through ActorAnimation.
/// </summary>
public abstract class LocomotionAnimationRuntime : LocomotionRuntime
{
    private const float MoveParameterBlendSeconds = 0.1f;
    private readonly HashSet<string> _reportedIssues = new();
    private readonly List<string> _playbackIssues = new();
    private UnityEngine.Object _diagnosticContext;
    private ManualMixerState _move;
    private LocomotionMovePlayback _movePlayback;
    private bool _attemptedMove;
    private AnimationClip _idleClip;
    private float _maximumMoveThreshold;
    private float _moveParameter;
    private bool _hasMoveParameter;
    protected LocomotionAnimationRuntime(LocomotionAsset asset) : base(asset) { }

    protected override void OnEnter(ActorLocomotion owner, Actor actor)
    {
        _diagnosticContext = owner != null ? (UnityEngine.Object)owner : actor;
        ResetAnimation();
    }

    protected override void OnExit(ActorLocomotion owner, Actor actor) => ResetAnimation();

    public override void ResetAnimation()
    {
        _movePlayback?.Reset();
        _hasMoveParameter = false;
        if (_move == null)
            _attemptedMove = false;
    }
    public override void SuspendAnimationFeedback() => _movePlayback?.SuspendFeedback();

    public override LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context) =>
        UpdateSharedMotion(context);

    protected LocomotionAnimationRequest MoveRequest(in LocomotionRuntimeAnimationContext context,
        float blendDuration)
    {
        EnsureMove();
        Vector2 parameter;
        if (Asset.Move?.BlendType == LocomotionMoveBlendType.TwoDimensional)
        {
            Vector3 local = Quaternion.Inverse(context.Motor.CurrentWorldRotation) * context.PolicyVelocity;
            parameter = new Vector2(local.x, local.z);
        }
        else
        {
            bool vertical = Asset.Move?.OneDimensional?.Parameter == LocomotionMove1DParameter.VerticalSpeed;
            float speed = vertical
                ? context.VerticalSpeed : Vector3.ProjectOnPlane(context.PolicyVelocity, context.Motor.CharacterUp).magnitude;
            if (!vertical && _move != null)
            {
                // LinearMixer already clamps its weights outside this range. Smooth the visible
                // parameter, so out-of-range gameplay speed cannot delay an eventual return to Idle.
                float target = Mathf.Clamp(speed, 0f, _maximumMoveThreshold);
                _moveParameter = _hasMoveParameter
                    ? Mathf.MoveTowards(_moveParameter, target,
                        _maximumMoveThreshold * context.DeltaTime / MoveParameterBlendSeconds)
                    : target;
                _hasMoveParameter = true;
                speed = _moveParameter;
            }
            parameter = new Vector2(speed, 0f);
        }
        return new LocomotionAnimationRequest(_move, blendDuration, isMove: true,
            parameter: parameter, idleClip: _idleClip, movePlayback: _movePlayback,
            playbackContext: context);
    }

    protected void ReportIssue(string issue)
    {
        if (_reportedIssues.Add(issue))
            Debug.LogWarning($"[ActorLocomotion] Actor '{(_diagnosticContext != null ? _diagnosticContext.name : "unbound")}', Asset '{Asset.name}': {issue}", _diagnosticContext);
    }

    protected void ReportConfigurationError(string issue)
    {
        if (_reportedIssues.Add(issue))
            Debug.LogError($"[ActorLocomotion] Actor '{(_diagnosticContext != null ? _diagnosticContext.name : "unbound")}', Asset '{Asset.name}': {issue}", _diagnosticContext);
    }

    protected override void OnDispose()
    {
        LocomotionAnimationUtility.Destroy(_move);
        _move = null;
        _movePlayback = null;
        _attemptedMove = false;
    }

    private void EnsureMove()
    {
        if (LocomotionAnimationUtility.WasGraphDestroyed(_move))
        {
            _move = null;
            _movePlayback = null;
            _attemptedMove = false;
            _hasMoveParameter = false;
        }
        if (_attemptedMove)
            return;
        _attemptedMove = true;
        _idleClip = FindIdleClip();
        _playbackIssues.Clear();
        if (Asset.Move == null)
            _playbackIssues.Add("Move definition is missing.");
        else
            Asset.Move.CollectPlaybackIssues(_playbackIssues);
        if (_playbackIssues.Count > 0)
        {
            for (int i = 0; i < _playbackIssues.Count; i++)
                ReportConfigurationError(_playbackIssues[i]);
            return;
        }

        if (Asset.Move.BlendType == LocomotionMoveBlendType.OneDimensional)
        {
            var samples = new List<LocomotionMove1DSample>(Asset.Move.OneDimensional.Samples);
            samples.Sort((left, right) => left.Threshold.CompareTo(right.Threshold));
            var mixer = new LinearMixerState { ExtrapolateSpeed = false };
            _maximumMoveThreshold = samples[samples.Count - 1].Threshold;
            var animations = new AnimationAsset[samples.Count];
            var sync = new bool[samples.Count];
            var idle = new bool[samples.Count];
            var labels = new string[samples.Count];
            bool vertical = Asset.Move.OneDimensional.Parameter == LocomotionMove1DParameter.VerticalSpeed;
            for (int i = 0; i < samples.Count; i++)
            {
                ClipState child = mixer.Add(samples[i].Animation.Clip, samples[i].Threshold);
                if (!samples[i].Sync)
                    mixer.DontSynchronize(child);
                animations[i] = samples[i].Animation;
                sync[i] = samples[i].Sync;
                idle[i] = !vertical && Mathf.Abs(samples[i].Threshold) <= LocomotionDataValidation.ThresholdEpsilon;
                for (int authored = 0; authored < Asset.Move.OneDimensional.Samples.Count; authored++)
                    if (ReferenceEquals(samples[i], Asset.Move.OneDimensional.Samples[authored]))
                    { labels[i] = $"Move 1D[{authored}]"; break; }
            }
            _move = mixer;
            _movePlayback = new LocomotionMovePlayback(mixer, animations, sync, idle, vertical, ReportIssue, labels);
        }
        else
        {
            IReadOnlyList<LocomotionMove2DSample> samples = Asset.Move.TwoDimensional.Samples;
            var mixer = new DirectionalMixerState();
            var animations = new AnimationAsset[samples.Count];
            var sync = new bool[samples.Count];
            var idle = new bool[samples.Count];
            var labels = new string[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                ClipState child = mixer.Add(samples[i].Animation.Clip, samples[i].Threshold);
                if (!samples[i].Sync)
                    mixer.DontSynchronize(child);
                animations[i] = samples[i].Animation;
                sync[i] = samples[i].Sync;
                idle[i] = samples[i].Threshold.sqrMagnitude <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon;
                labels[i] = $"Move 2D[{i}]";
            }
            _move = mixer;
            _movePlayback = new LocomotionMovePlayback(mixer, animations, sync, idle, false, ReportIssue, labels);
        }
    }

    protected bool CheckClip(AnimationClip clip, string label)
    {
        if (LocomotionAnimationUtility.IsUsableClip(clip))
            return true;
        ReportConfigurationError($"{label} requires an AnimationAsset/Clip with a finite positive length.");
        return false;
    }

    private AnimationClip FindIdleClip()
    {
        if (Asset.Move?.BlendType == LocomotionMoveBlendType.OneDimensional
            && Asset.Move.OneDimensional?.Parameter == LocomotionMove1DParameter.HorizontalSpeed)
        {
            var samples = Asset.Move.OneDimensional.Samples;
            if (samples != null)
                for (int i = 0; i < samples.Count; i++)
                    if (samples[i] != null && Mathf.Abs(samples[i].Threshold) <= LocomotionDataValidation.ThresholdEpsilon
                        && LocomotionAnimationUtility.IsUsableClip(samples[i].Animation?.Clip))
                        return samples[i].Animation.Clip;
        }
        else if (Asset.Move?.BlendType == LocomotionMoveBlendType.TwoDimensional)
        {
            var samples = Asset.Move.TwoDimensional?.Samples;
            if (samples != null)
                for (int i = 0; i < samples.Count; i++)
                    if (samples[i] != null && samples[i].Threshold.sqrMagnitude <=
                        LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon
                        && LocomotionAnimationUtility.IsUsableClip(samples[i].Animation?.Clip))
                        return samples[i].Animation.Clip;
        }
        return null;
    }
}

public sealed class LocomotionMixerRuntime : LocomotionAnimationRuntime
{
    public LocomotionMixerRuntime(LocomotionMixerAsset asset) : base(asset) { }

    public override LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context) =>
        IsEntered && context.DeltaTime > 0f ? MoveRequest(context, 0.1f) : default;
}

public sealed class LocomotionSetRuntime : LocomotionAnimationRuntime
{
    private readonly LocomotionSetAsset _set;
    private readonly LocomotionSetStateMachine _machine = new();
    private readonly Dictionary<AnimationClip, ClipState> _clips = new();
    private TransitionSample[] _start = Array.Empty<TransitionSample>();
    private TransitionSample[] _stop = Array.Empty<TransitionSample>();
    private TransitionSample[] _pivot = Array.Empty<TransitionSample>();
    private bool _transitionsBound;
    private ClipState _transition;

    public LocomotionSetRuntime(LocomotionSetAsset asset) : base(asset) => _set = asset;
    public LocomotionSetState AnimationState => _machine.State;

    public override void ResetAnimation()
    {
        base.ResetAnimation();
        _machine.Reset();
        _transition = null;
    }

    public override LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context)
    {
        if (!IsEntered || !LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
            return default;

        if (LocomotionAnimationUtility.WasGraphDestroyed(_transition))
            ResetAnimation();
        LocomotionAnimationRequest move = MoveRequest(context, _set.TransitionBlendDuration);
        if (move.State == null)
        {
            _machine.Reset();
            _transition = null;
            return move;
        }

        BindTransitions();
        bool completed = _transition != null && _transition.TimeD >= _transition.Length;
        LocomotionSetState next = _machine.DecideNextState(context, completed);
        ClipState prepared = _transition;
        if (next != _machine.State)
        {
            prepared = next == LocomotionSetState.Move ? null : GetClipState(SelectTransition(next, context));
            if (prepared == null)
                next = LocomotionSetState.Move;
        }
        bool entered = _machine.CommitState(next);
        _transition = next == LocomotionSetState.Move ? null : prepared;
        if (next == LocomotionSetState.Move)
            return move;

        return new LocomotionAnimationRequest(_transition, _set.TransitionBlendDuration,
            entered, idleClip: move.IdleClip);
    }

    protected override void OnDispose()
    {
        foreach (ClipState state in _clips.Values)
            LocomotionAnimationUtility.Destroy(state);
        _clips.Clear();
        _start = _stop = _pivot = Array.Empty<TransitionSample>();
        _transitionsBound = false;
        _transition = null;
        base.OnDispose();
    }

    private ClipState GetClipState(AnimationClip clip)
    {
        if (clip == null)
            return null;
        if (_clips.TryGetValue(clip, out ClipState state)
            && !LocomotionAnimationUtility.WasGraphDestroyed(state))
            return state;
        LocomotionAnimationUtility.Destroy(state);
        state = new ClipState(clip);
        _clips[clip] = state;
        return state;
    }

    private void BindTransitions()
    {
        if (_transitionsBound)
            return;
        _transitionsBound = true;
        _start = new TransitionSample[_set.Start?.Count ?? 0];
        _stop = new TransitionSample[_set.Stop?.Count ?? 0];
        _pivot = new TransitionSample[_set.Pivot?.Count ?? 0];
        for (int i = 0; i < _start.Length; i++)
        {
            LocomotionStartEntry entry = _set.Start[i];
            _start[i] = new TransitionSample(entry?.Animation?.Clip, Vector2.up,
                entry?.TargetLocalDirection ?? Vector2.zero);
        }
        for (int i = 0; i < _stop.Length; i++)
        {
            LocomotionStopEntry entry = _set.Stop[i];
            _stop[i] = new TransitionSample(entry?.Animation?.Clip,
                entry?.SourceLocalDirection ?? Vector2.zero, Vector2.up);
        }
        for (int i = 0; i < _pivot.Length; i++)
        {
            LocomotionPivotEntry entry = _set.Pivot[i];
            _pivot[i] = new TransitionSample(entry?.Animation?.Clip,
                entry?.SourceLocalDirection ?? Vector2.zero, entry?.TargetLocalDirection ?? Vector2.zero);
        }
        _start = ValidateTransitions(_start, "Start");
        _stop = ValidateTransitions(_stop, "Stop");
        _pivot = ValidateTransitions(_pivot, "Pivot");
    }

    private TransitionSample[] ValidateTransitions(TransitionSample[] samples, string kind)
    {
        bool valid = true;
        for (int i = 0; i < samples.Length; i++)
        {
            string label = $"{kind}[{i}]";
            if (!LocomotionDataValidation.IsValidDirection(samples[i].Source)
                || !LocomotionDataValidation.IsValidDirection(samples[i].Target))
            {
                ReportConfigurationError($"{label} has invalid source/target directions.");
                valid = false;
            }
            if (!CheckClip(samples[i].Clip, label))
                valid = false;
        }
        return valid ? samples : Array.Empty<TransitionSample>();
    }

    private AnimationClip SelectTransition(LocomotionSetState next, in LocomotionRuntimeAnimationContext context)
    {
        Vector2 source = context.ToLocalDirection(context.VelocityBeforeMotion);
        Vector2 target = context.ToLocalDirection(context.Intent.WorldMoveDirection);
        AnimationClip best = null;
        float bestScore = float.PositiveInfinity;
        TransitionSample[] samples = next == LocomotionSetState.Start ? _start
            : next == LocomotionSetState.Stop ? _stop : _pivot;
        for (int i = 0; i < samples.Length; i++)
        {
            TransitionSample sample = samples[i];
            float score = next == LocomotionSetState.Start ? Vector2.Angle(target, sample.Target)
                : next == LocomotionSetState.Stop ? Vector2.Angle(source, sample.Source)
                : Vector2.Angle(source, sample.Source) + Vector2.Angle(target, sample.Target);
            if (score < bestScore)
            {
                bestScore = score;
                best = sample.Clip;
            }
        }
        return best;
    }

    private readonly struct TransitionSample
    {
        internal TransitionSample(AnimationClip clip, Vector2 source, Vector2 target)
        {
            Clip = clip;
            Source = source;
            Target = target;
        }

        internal AnimationClip Clip { get; }
        internal Vector2 Source { get; }
        internal Vector2 Target { get; }
    }
}
