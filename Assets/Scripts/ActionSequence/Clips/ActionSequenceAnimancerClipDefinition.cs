using Animancer;
using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceAnimancerClipDefinition : ActionSequenceClipDefinition
{
    public TransitionAsset transitionAsset;
    public AnimancerParameterMode parameterMode = AnimancerParameterMode.None;
    public Vector2 fallbackVector2 = Vector2.zero;
    public float fallbackFloat;
    public float playbackSpeed = 1f;

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Animation;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceAnimancerClipDefinition _definition;
        private AnimancerState _state;

        public Runtime(ActionSequenceAnimancerClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            if (actor == null || actor.animancer == null)
                return;

            if (_definition.transitionAsset == null || _definition.transitionAsset.Transition == null)
                return;

            _state = actor.animancer.Play(_definition.transitionAsset.Transition);
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
        }

        private void Sample(ActionSequenceContext context)
        {
            if (_state == null || context.Actor == null || context.Actor.animancer == null)
                return;

            int localFrame = Mathf.Max(0, context.Frame - _definition.StartFrame);
            float speed = Mathf.Max(0f, _definition.playbackSpeed);
            _state.Speed = 0f;
            _state.Time = localFrame / (float)Mathf.Max(1, context.FrameRate) * speed;
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
                        mixer1D.Parameter = Mathf.Abs(context.EventContext.Magnitude) > 0.001f
                            ? context.EventContext.Magnitude
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

            Vector3 direction = context.EventContext.Direction;
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
