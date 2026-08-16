using System;
using KinematicCharacterController;
using UnityEngine;

[Serializable]
public sealed class ActionSequenceVelocityOverrideClipDefinition : ActionSequenceClipDefinition
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    public VelocityConfig config = new VelocityConfig();

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Motion;

    public override ActionContextFieldMask RequiredContextFields =>
        UsesContextDirection ? ActionContextFieldMask.Direction : ActionContextFieldMask.None;

    public bool UsesContextDirection =>
        config != null
        && config.useHorizontalVelocity
        && config.directionMode == MotionDirectionMode.FromContext;

    public bool HasAnyAxis => config != null && (config.useHorizontalVelocity || config.useVerticalVelocity);

    public bool HasValidPresetLocalDirection()
    {
        return config == null
            || !config.useHorizontalVelocity
            || config.directionMode != MotionDirectionMode.LocalHorizontal
            || (IsFinite(config.localHorizontalDirection)
                && ProjectPlanar(config.localHorizontalDirection, Vector3.up, out _));
    }

    public bool HasFiniteSpeeds()
    {
        return config != null
            && IsFinite(config.horizontalSpeed)
            && IsFinite(config.verticalSpeed);
    }

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceVelocityOverrideClipDefinition _definition;
        private Actor _actor;
        private ActorMotor _motor;
        private MotionOwner _horizontalOwner;
        private MotionOwner _verticalOwner;

        public Runtime(ActionSequenceVelocityOverrideClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            _actor = context.Actor;
            _motor = _actor != null ? _actor.actorMotor : null;
            VelocityConfig config = _definition.config;

            if (_motor == null)
                throw new InvalidOperationException("VelocityOverrideClip requires ActorMotor.");
            if (config == null || !config.useHorizontalVelocity && !config.useVerticalVelocity)
                throw new InvalidOperationException("VelocityOverrideClip requires at least one enabled axis.");
            if (!_definition.HasFiniteSpeeds())
                throw new InvalidOperationException("VelocityOverrideClip speed values must be finite.");
            if (!_definition.HasValidPresetLocalDirection())
                throw new InvalidOperationException("VelocityOverrideClip PresetLocal direction is invalid.");
            if (_definition.UsesContextDirection && !context.Context.HasDirection)
                throw new InvalidOperationException("VelocityOverrideClip ContextDirection requires ActionContext.Direction.");

            if (config.useHorizontalVelocity)
                _horizontalOwner = _motor.BeginHorizontalVelocity();
            if (config.useVerticalVelocity)
                _verticalOwner = _motor.BeginVerticalVelocity();
        }

        public override void OnTick(ActionSequenceContext context)
        {
            if (_motor == null)
                return;

            VelocityConfig config = _definition.config;
            float sample = GetCurveSample(context);

            if (_horizontalOwner.IsValid)
            {
                Vector3 direction = ResolveHorizontalDirection(context);
                float multiplier = config.horizontalCurve?.Evaluate(sample) ?? 1f;
                _motor.SetHorizontalVelocity(_horizontalOwner, direction * (config.horizontalSpeed * multiplier));
            }

            if (_verticalOwner.IsValid)
            {
                float multiplier = config.verticalCurve?.Evaluate(sample) ?? 1f;
                _motor.SetVerticalVelocity(_verticalOwner, config.verticalSpeed * multiplier);
            }

            if (config.debugLog)
            {
                Debug.Log(
                    $"[ActionSequence VelocityOverride] frame={context.Frame}, sample={sample:0.###}, hOwner={_horizontalOwner.IsValid}, vOwner={_verticalOwner.IsValid}",
                    _motor);
            }
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null)
            {
                if (_horizontalOwner.IsValid)
                    _motor.EndHorizontalVelocity(_horizontalOwner);
                if (_verticalOwner.IsValid)
                    _motor.EndVerticalVelocity(_verticalOwner);
            }

            _horizontalOwner = default;
            _verticalOwner = default;
            _actor = null;
            _motor = null;
        }

        private float GetCurveSample(ActionSequenceContext context)
        {
            int duration = _definition.DurationFrames;
            if (duration <= 1)
                return 0f;

            int localFrame = Mathf.Clamp(context.Frame - _definition.StartFrame, 0, duration - 1);
            return localFrame / (float)(duration - 1);
        }

        private Vector3 ResolveHorizontalDirection(ActionSequenceContext context)
        {
            VelocityConfig config = _definition.config;
            Vector3 direction = config.directionMode == MotionDirectionMode.FromContext
                ? context.Context.Direction
                : GetSimulationRotation(_actor, _motor) * new Vector3(
                    config.localHorizontalDirection.x,
                    0f,
                    config.localHorizontalDirection.z);

            Vector3 up = GetCharacterUp(_motor, GetSimulationRotation(_actor, _motor));
            if (!ProjectPlanar(direction, up, out Vector3 planarDirection))
            {
                throw new InvalidOperationException(
                    $"VelocityOverrideClip horizontal direction {config.directionMode} is invalid.");
            }

            return planarDirection;
        }
    }

    private static Quaternion GetSimulationRotation(Actor actor, ActorMotor actorMotor)
    {
        KinematicCharacterMotor motor = actorMotor != null ? actorMotor.Motor : null;
        if (motor != null && Application.isPlaying)
            return motor.TransientRotation;

        if (actorMotor != null)
            return actorMotor.transform.rotation;

        return actor != null ? actor.transform.rotation : Quaternion.identity;
    }

    private static Vector3 GetCharacterUp(ActorMotor actorMotor, Quaternion currentRotation)
    {
        KinematicCharacterMotor motor = actorMotor != null ? actorMotor.Motor : null;
        return motor != null && Application.isPlaying ? motor.CharacterUp : currentRotation * Vector3.up;
    }

    private static bool ProjectPlanar(Vector3 direction, Vector3 up, out Vector3 planarDirection)
    {
        planarDirection = Vector3.zero;
        if (!IsFinite(direction) || !IsFinite(up) || up.sqrMagnitude <= DirectionEpsilonSqr)
            return false;

        planarDirection = Vector3.ProjectOnPlane(direction, up.normalized);
        if (planarDirection.sqrMagnitude <= DirectionEpsilonSqr)
            return false;

        planarDirection.Normalize();
        return true;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
