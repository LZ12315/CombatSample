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

        public Runtime(ActionSequenceAnimationPoseClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnTick(ActionSequenceContext context)
        {
            SubmitPose(context);
        }

        private void SubmitPose(ActionSequenceContext context)
        {
            if (context == null || context.Actor == null)
                return;

            float sampleTime = ActionSequenceAnimationTimeUtility.GetPoseSampleTime(
                context,
                _definition.StartFrame,
                _definition.startOffsetSeconds,
                _definition.playbackSpeed);

            BuildMixerParameter(
                context,
                out bool hasVector2Parameter,
                out Vector2 vector2Parameter,
                out bool hasFloatParameter,
                out float floatParameter);

            var pose = new ActionAnimationPose(
                _definition.AnimationKey,
                sampleTime,
                hasVector2Parameter,
                vector2Parameter,
                hasFloatParameter,
                floatParameter);

            ActorAnimation actorAnimation = context.Actor.actorAnimation != null
                ? context.Actor.actorAnimation
                : context.Actor.GetComponent<ActorAnimation>();
            actorAnimation?.SubmitActionPose(context.AnimationOwner, pose);
        }

        private void BuildMixerParameter(
            ActionSequenceContext context,
            out bool hasVector2Parameter,
            out Vector2 vector2Parameter,
            out bool hasFloatParameter,
            out float floatParameter)
        {
            hasVector2Parameter = false;
            vector2Parameter = _definition.fallbackVector2;
            hasFloatParameter = false;
            floatParameter = _definition.fallbackFloat;

            switch (_definition.parameterMode)
            {
                case AnimancerParameterMode.ContextDirection2D:
                    hasVector2Parameter = true;
                    vector2Parameter = TryGetContextDirection2D(context, out Vector2 dir2D)
                        ? dir2D
                        : _definition.fallbackVector2;
                    break;
                case AnimancerParameterMode.ContextMagnitude:
                    hasFloatParameter = true;
                    floatParameter = context.Context.HasMagnitude && Mathf.Abs(context.Context.Magnitude) > 0.001f
                        ? context.Context.Magnitude
                        : _definition.fallbackFloat;
                    break;
                case AnimancerParameterMode.SerializedFallback:
                    hasVector2Parameter = true;
                    vector2Parameter = _definition.fallbackVector2;
                    hasFloatParameter = true;
                    floatParameter = _definition.fallbackFloat;
                    break;
                case AnimancerParameterMode.VerticalVelocity:
                    hasFloatParameter = true;
                    floatParameter = TryGetActorVerticalVelocity(context, out float verticalVelocity)
                        ? verticalVelocity
                        : _definition.fallbackFloat;
                    break;
            }
        }

        private bool TryGetActorVerticalVelocity(ActionSequenceContext context, out float verticalVelocity)
        {
            verticalVelocity = _definition.fallbackFloat;
            ActorMotor motor = context.Actor != null ? context.Actor.actorMotor : null;
            if (motor == null)
                return false;

            verticalVelocity = motor.CurrentVerticalSpeed;
            return true;
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
