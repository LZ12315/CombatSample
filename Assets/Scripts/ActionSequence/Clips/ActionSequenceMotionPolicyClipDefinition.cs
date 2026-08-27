using System;
using UnityEngine;

[Serializable]
public sealed class ActionSequenceMotionPolicyClipDefinition : ActionSequenceClipDefinition
{
    [SerializeField] private bool useLocomotionScale;
    [SerializeField, Range(0f, 1f)] private float locomotionScale = 1f;
    [SerializeField] private bool useAirLocomotionScale;
    [SerializeField, Range(0f, 1f)] private float airLocomotionScale = 1f;
    [SerializeField] private bool useGravityScale;
    [SerializeField, Min(0f)] private float gravityScale = 1f;

    public bool UseLocomotionScale => useLocomotionScale;
    public float LocomotionScale => locomotionScale;
    public bool UseAirLocomotionScale => useAirLocomotionScale;
    public float AirLocomotionScale => airLocomotionScale;
    public bool UseGravityScale => useGravityScale;
    public float GravityScale => gravityScale;

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Motion;

    public bool HasAnyPolicy =>
        useLocomotionScale || useAirLocomotionScale || useGravityScale;

    public bool HasValidValues()
    {
        return (!useLocomotionScale || IsFinite01(locomotionScale))
            && (!useAirLocomotionScale || IsFinite01(airLocomotionScale))
            && (!useGravityScale || IsFiniteNonNegative(gravityScale));
    }

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceMotionPolicyClipDefinition _definition;
        private ActorMotor _motor;
        private MotionOwner _locomotionOwner;
        private MotionOwner _airLocomotionOwner;
        private MotionOwner _gravityOwner;

        public Runtime(ActionSequenceMotionPolicyClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            _motor = context.Actor != null ? context.Actor.actorMotor : null;
            if (_motor == null)
                throw new InvalidOperationException("MotionPolicyClip requires ActorMotor.");
            if (!_definition.HasAnyPolicy)
                throw new InvalidOperationException("MotionPolicyClip requires at least one enabled policy.");
            if (!_definition.HasValidValues())
                throw new InvalidOperationException("MotionPolicyClip values are invalid.");

            if (_definition.useLocomotionScale)
                _locomotionOwner = _motor.BeginLocomotionScale(_definition.locomotionScale);
            if (_definition.useAirLocomotionScale)
                _airLocomotionOwner = _motor.BeginAirLocomotionScale(_definition.airLocomotionScale);
            if (_definition.useGravityScale)
                _gravityOwner = _motor.BeginGravityScale(_definition.gravityScale);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null)
            {
                if (_locomotionOwner.IsValid)
                    _motor.EndLocomotionScale(_locomotionOwner);
                if (_airLocomotionOwner.IsValid)
                    _motor.EndAirLocomotionScale(_airLocomotionOwner);
                if (_gravityOwner.IsValid)
                    _motor.EndGravityScale(_gravityOwner);
            }

            _locomotionOwner = default;
            _airLocomotionOwner = default;
            _gravityOwner = default;
            _motor = null;
        }
    }

    private static bool IsFinite01(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= 0f
            && value <= 1f;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= 0f;
    }
}
