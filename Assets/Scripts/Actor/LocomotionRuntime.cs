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
    private readonly HashSet<string> _reportedIssues = new();
    private readonly List<string> _playbackIssues = new();
    private UnityEngine.Object _diagnosticContext;
    private ManualMixerState _move;
    private LocomotionMovePlayback _movePlayback;
    private bool _attemptedMove;
    private AnimationClip _idleClip;
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
        if (_move == null)
            _attemptedMove = false;
    }
    public override void SuspendAnimationFeedback() => _movePlayback?.SuspendFeedback();

    public override LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context) =>
        UpdateSharedMotion(context);

    protected LocomotionAnimationRequest MoveRequest(in LocomotionRuntimeAnimationContext context,
        float blendDuration, bool zeroParameter = false)
    {
        EnsureMove();
        Vector2 parameter;
        if (zeroParameter)
            parameter = Vector2.zero;
        else if (Asset.Move?.BlendType == LocomotionMoveBlendType.TwoDimensional)
        {
            Vector3 local = Quaternion.Inverse(context.Motor.CurrentWorldRotation) * context.PolicyVelocity;
            parameter = new Vector2(local.x, local.z);
        }
        else
        {
            float speed = Asset.Move?.OneDimensional?.Parameter == LocomotionMove1DParameter.VerticalSpeed
                ? context.VerticalSpeed : Vector3.ProjectOnPlane(context.PolicyVelocity, context.Motor.CharacterUp).magnitude;
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
                ReportIssue(_playbackIssues[i]);
            return;
        }

        if (Asset.Move.BlendType == LocomotionMoveBlendType.OneDimensional)
        {
            var samples = new List<LocomotionMove1DSample>(Asset.Move.OneDimensional.Samples);
            samples.Sort((left, right) => left.Threshold.CompareTo(right.Threshold));
            var mixer = new LinearMixerState { ExtrapolateSpeed = false };
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
        ReportIssue($"{label} requires an AnimationAsset/Clip with a finite positive length.");
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
    private readonly Dictionary<AnimationAsset, ClipState> _clips = new();
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
        if (!IsEntered || context.DeltaTime <= 0f)
            return default;

        if (LocomotionAnimationUtility.WasGraphDestroyed(_transition))
            ResetAnimation();
        bool completed = _transition != null && _transition.TimeD >= _transition.Length;
        bool entered = _machine.Step(context, completed);
        // Build Move even on the first Start tick, so the zero sample can protect an invalid transition.
        LocomotionAnimationRequest move = MoveRequest(context, _set.TransitionBlendDuration,
            _machine.UseZeroMoveParameter);
        if (_machine.State == LocomotionSetState.Move)
        {
            _transition = null;
            return move;
        }

        if (entered)
            _transition = GetClipState(SelectTransition(context));
        return new LocomotionAnimationRequest(_transition, _set.TransitionBlendDuration,
            entered, idleClip: move.IdleClip);
    }

    protected override void OnDispose()
    {
        foreach (ClipState state in _clips.Values)
            LocomotionAnimationUtility.Destroy(state);
        _clips.Clear();
        _transition = null;
        base.OnDispose();
    }

    private ClipState GetClipState(AnimationAsset animation)
    {
        if (animation == null)
            return null;
        if (_clips.TryGetValue(animation, out ClipState state)
            && !LocomotionAnimationUtility.WasGraphDestroyed(state))
            return state;
        LocomotionAnimationUtility.Destroy(state);
        state = new ClipState(animation.Clip);
        _clips[animation] = state;
        return state;
    }

    private AnimationAsset SelectTransition(in LocomotionRuntimeAnimationContext context)
    {
        Vector2 source = context.ToLocalDirection(context.VelocityBeforeMotion);
        Vector2 target = context.ToLocalDirection(context.Intent.WorldMoveDirection);
        AnimationAsset best = null;
        float bestScore = float.PositiveInfinity;
        bool valid = true;
        int count = _machine.State == LocomotionSetState.Start ? _set.Start?.Count ?? 0
            : _machine.State == LocomotionSetState.Stop ? _set.Stop?.Count ?? 0 : _set.Pivot?.Count ?? 0;
        if (count == 0)
        {
            ReportIssue($"{_machine.State} samples are missing.");
            return null;
        }
        for (int i = 0; i < count; i++)
        {
            AnimationAsset animation;
            Vector2 entrySource = Vector2.up;
            Vector2 entryTarget = Vector2.up;
            if (_machine.State == LocomotionSetState.Start)
            {
                animation = _set.Start[i]?.Animation;
                entryTarget = _set.Start[i]?.TargetLocalDirection ?? Vector2.zero;
            }
            else if (_machine.State == LocomotionSetState.Stop)
            {
                animation = _set.Stop[i]?.Animation;
                entrySource = _set.Stop[i]?.SourceLocalDirection ?? Vector2.zero;
            }
            else
            {
                animation = _set.Pivot[i]?.Animation;
                entrySource = _set.Pivot[i]?.SourceLocalDirection ?? Vector2.zero;
                entryTarget = _set.Pivot[i]?.TargetLocalDirection ?? Vector2.zero;
            }
            string label = $"{_machine.State}[{i}]";
            if (!LocomotionDataValidation.IsValidDirection(entrySource)
                || !LocomotionDataValidation.IsValidDirection(entryTarget))
            {
                ReportIssue($"{label} has invalid source/target directions.");
                valid = false;
            }
            if (!CheckClip(animation?.Clip, label))
                valid = false;
            float score = _machine.State == LocomotionSetState.Start ? Vector2.Angle(target, entryTarget)
                : _machine.State == LocomotionSetState.Stop ? Vector2.Angle(source, entrySource)
                : Vector2.Angle(source, entrySource) + Vector2.Angle(target, entryTarget);
            if (score < bestScore)
            {
                bestScore = score;
                best = animation;
            }
        }
        // Never build a second, reduced set by silently excluding bad entries.
        return valid ? best : null;
    }
}
