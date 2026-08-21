using Animancer;
using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceAnimationPoseClipDefinition : ActionSequenceClipDefinition
{
    [SerializeField] private string animationKey = string.Empty;
    public AnimancerParameterMode parameterMode = AnimancerParameterMode.None;
    public Vector2 fallbackVector2 = Vector2.zero;
    public float fallbackFloat;
    [Min(0f)] public float startOffsetSeconds;
    public float playbackSpeed = 1f;

    public string AnimationKey => animationKey;

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Animation;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceAnimationPoseClipDefinition _definition;
        private AnimancerState _state;
        private TransitionAsset _transitionAsset;
        private float _transitionDuration;

        public Runtime(ActionSequenceAnimationPoseClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            if (actor == null || actor.animancer == null)
                return;

            if (!ResolveTransition(actor, out _transitionAsset))
                return;

            _transitionDuration = (float)AnimancerTransitionUtility.GetDuration(_transitionAsset);
            _state = actor.animancer.Play(_transitionAsset.Transition);
            InitializeMixerParameter(context);

            if (_state != null)
            {
                _state.Speed = 0f;
                _state.IsPlaying = true;
                Sample(context);
            }
        }

        public override void OnTick(ActionSequenceContext context)
        {
            Sample(context);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_state != null)
                _state.IsPlaying = false;

            _state = null;
            _transitionAsset = null;
            _transitionDuration = 0f;
        }

        private bool ResolveTransition(Actor actor, out TransitionAsset transitionAsset)
        {
            transitionAsset = null;
            AnimationConfig config = actor != null ? actor.AnimationConfig : null;
            if (config == null)
                return false;

            return config.TryGetTransition(_definition.AnimationKey, out transitionAsset)
                   && transitionAsset != null
                   && transitionAsset.Transition != null;
        }

        private void Sample(ActionSequenceContext context)
        {
            if (_state == null || context.Actor == null || context.Actor.animancer == null)
                return;

            float sampleTime = ActionSequenceAnimationTimeUtility.GetPoseSampleTime(
                context,
                _definition.StartFrame,
                _definition.startOffsetSeconds,
                _definition.playbackSpeed);
            if (_transitionDuration > 0f)
                sampleTime = Mathf.Min(sampleTime, _transitionDuration);

            _state.Speed = 0f;
            _state.Time = sampleTime;
            context.Actor.animancer.Evaluate();
        }

        private void InitializeMixerParameter(ActionSequenceContext context)
        {
            if (_state is MixerState<Vector2> mixer2D)
            {
                switch (_definition.parameterMode)
                {
                    case AnimancerParameterMode.ContextDirection2D:
                        mixer2D.Parameter = TryGetContextDirection2D(context, out Vector2 dir2D)
                            ? dir2D
                            : _definition.fallbackVector2;
                        break;
                    case AnimancerParameterMode.SerializedFallback:
                        mixer2D.Parameter = _definition.fallbackVector2;
                        break;
                }
            }
            else if (_state is MixerState<float> mixer1D)
            {
                switch (_definition.parameterMode)
                {
                    case AnimancerParameterMode.ContextMagnitude:
                        mixer1D.Parameter = context.Context.HasMagnitude && Mathf.Abs(context.Context.Magnitude) > 0.001f
                            ? context.Context.Magnitude
                            : _definition.fallbackFloat;
                        break;
                    case AnimancerParameterMode.SerializedFallback:
                        mixer1D.Parameter = _definition.fallbackFloat;
                        break;
                }
            }
        }

        private bool TryGetContextDirection2D(ActionSequenceContext context, out Vector2 parameter)
        {
            parameter = _definition.fallbackVector2;
            Actor actor = context.Actor;
            if (actor == null)
                return false;

            Vector3 direction = context.Context.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 localDirection = actor.transform.InverseTransformDirection(direction.normalized);
            parameter = new Vector2(localDirection.x, localDirection.z);
            if (parameter.sqrMagnitude <= 0.0001f)
                return false;

            parameter.Normalize();
            return true;
        }
    }
}
