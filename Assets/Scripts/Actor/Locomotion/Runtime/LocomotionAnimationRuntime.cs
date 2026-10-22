using Animancer;
using UnityEngine;

/// <summary>
/// Common cached Move states, bound from immutable configuration for this Runtime's lifetime.
/// All graph attachment happens through ActorAnimation.
/// </summary>
public abstract class LocomotionAnimationRuntime : LocomotionRuntime
{
    private readonly LocomotionBoundMove _definition;
    private readonly LocomotionTurnLean _lean = new();
    private ManualMixerState _move;
    private LocomotionMovePlayback _movePlayback;
    private LocomotionFootPhaseBinding[] _moveFootBindings;
    private bool _attemptedMove;

    protected LocomotionAnimationRuntime(LocomotionAsset asset)
        : this(asset, LocomotionBoundMove.Capture(asset, new LocomotionBindingBuilder())) { }

    private protected LocomotionAnimationRuntime(LocomotionAsset asset, LocomotionBoundMove definition) : base(asset)
    {
        _definition = definition;
    }

    protected override void OnEnter(ActorLocomotion owner, Actor actor) => ResetAnimation();
    protected override void OnExit(ActorLocomotion owner, Actor actor) => ResetAnimation();

    public override void ResetAnimation()
    {
        _movePlayback?.Reset();
        _lean.Reset();
        if (_move == null) _attemptedMove = false;
    }
    public override void SuspendAnimationFeedback()
    {
        _movePlayback?.SuspendFeedback();
        _lean.Suspend();
    }
    public override LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context) => UpdateSharedMotion(context);

    protected LocomotionAnimationRequest MoveRequest(in LocomotionRuntimeAnimationContext context, float blendDuration)
    {
        EnsureMove();
        // Update stateful sources once, then read both axes from the same tick sample.
        float lean = _definition.UsesLean ? _lean.Update(context, _definition.TurnLean) : 0f;
        var values = new LocomotionMoveParameters(context, lean);
        Vector2 parameter = new Vector2(values.Read(_definition.XParameter),
            _definition.BlendType == LocomotionMoveBlendType.TwoDimensional ? values.Read(_definition.YParameter) : 0f);
        return new LocomotionAnimationRequest(_move, blendDuration, isMove: true,
            parameter: parameter, idleClip: _definition.IdleClip, movePlayback: _movePlayback,
            playbackContext: context, footPhaseBindings: _moveFootBindings);
    }

    protected override void OnDispose()
    {
        LocomotionAnimationUtility.Destroy(_move);
        _move = null;
        _movePlayback = null;
        _moveFootBindings = null;
        _attemptedMove = false;
    }

    private void EnsureMove()
    {
        if (LocomotionAnimationUtility.WasGraphDestroyed(_move))
        {
            _move = null;
            _movePlayback = null;
            _moveFootBindings = null;
            _attemptedMove = false;
            _lean.Reset();
        }
        if (_attemptedMove) return;
        _attemptedMove = true;
        if (_definition.SampleCount == 0) return;
        ManualMixerState mixer = _definition.BlendType == LocomotionMoveBlendType.OneDimensional
            ? new LinearMixerState { ExtrapolateSpeed = false }
            : _definition.Algorithm == LocomotionMove2DAlgorithm.Cartesian
                ? new CartesianMixerState() : new DirectionalMixerState();
        var animations = new LocomotionBoundAnimation[_definition.SampleCount];
        var sync = new bool[_definition.SampleCount];
        var idle = new bool[_definition.SampleCount];
        _moveFootBindings = new LocomotionFootPhaseBinding[_definition.SampleCount];
        for (int i = 0; i < _definition.SampleCount; i++)
        {
            var sample = _definition.GetSample(i);
            ClipState child = mixer is LinearMixerState linear
                ? linear.Add(sample.Animation.Clip, sample.Threshold.x)
                : ((Vector2MixerState)mixer).Add(sample.Animation.Clip, sample.Threshold);
            if (!sample.Sync) mixer.DontSynchronize(child);
            animations[i] = sample.Animation;
            sync[i] = sample.Sync;
            idle[i] = sample.Idle;
            _moveFootBindings[i] = new LocomotionFootPhaseBinding(child, sample.Animation.FootPhase);
        }
        _move = mixer;
        _movePlayback = new LocomotionMovePlayback(mixer, animations, sync, idle, !_definition.MatchesHorizontalSpeed);
    }
}
