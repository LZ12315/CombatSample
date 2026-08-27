using System;
using KinematicCharacterController;
using UnityEngine;

public enum ActionSequenceBallisticVelocityOperation
{
    Add = 0,
    Set = 1,
}

[System.Serializable]
public sealed class ActionSequenceImpulseClipDefinition : ActionSequenceClipDefinition
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    public ImpulseConfig config = new ImpulseConfig();
    public bool useHorizontalImpulse = true;
    public bool useVerticalBallistic;
    public ActionSequenceBallisticVelocityOperation verticalOperation = ActionSequenceBallisticVelocityOperation.Add;
    public bool overrideGravityScale;
    [Min(0f)] public float gravityScale = 1f;

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Motion;

    public override ActionContextFieldMask RequiredContextFields =>
        UsesContextDirection ? ActionContextFieldMask.Direction : ActionContextFieldMask.None;

    public bool UsesContextDirection =>
        config != null
        && useHorizontalImpulse
        && config.directionMode == ImpulseDirectionMode.FromContext;

    public bool HasAnyContribution =>
        useHorizontalImpulse || useVerticalBallistic || overrideGravityScale;

    public bool HasFiniteValues()
    {
        return config != null
            && IsFinite(config.horizontalForce)
            && IsFinite(config.verticalForce)
            && (!overrideGravityScale || IsFiniteNonNegative(gravityScale));
    }

    public bool HasValidPresetLocalDirection()
    {
        return config == null
            || !useHorizontalImpulse
            || config.directionMode != ImpulseDirectionMode.LocalHorizontal
            || (IsFinite(config.localHorizontalDirection)
                && ProjectPlanar(config.localHorizontalDirection, Vector3.up, out _));
    }

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceImpulseClipDefinition _definition;
        private bool _applied;
        private bool _warnedMissingTarget;
        private ActorMotor _motor;
        private MotionOwner _gravityOwner;

        public Runtime(ActionSequenceImpulseClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            if (_applied)
                return;

            _applied = true;
            ApplyImpulse(context);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            ReleaseGravityOwner();
        }

        private void ApplyImpulse(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            _motor = actor != null ? actor.actorMotor : null;
            ImpulseConfig config = _definition.config;
            if (_motor == null)
                throw new InvalidOperationException("ImpulseClip requires ActorMotor.");
            if (config == null)
                throw new InvalidOperationException("ImpulseClip requires ImpulseConfig.");
            if (!_definition.HasAnyContribution)
                throw new InvalidOperationException("ImpulseClip requires at least one enabled contribution.");
            if (!_definition.HasFiniteValues())
                throw new InvalidOperationException("ImpulseClip values must be finite.");
            if (!_definition.HasValidPresetLocalDirection())
                throw new InvalidOperationException("ImpulseClip PresetLocal direction is invalid.");
            if (_definition.UsesContextDirection && !context.Context.HasDirection)
                throw new InvalidOperationException("ImpulseClip ContextDirection requires ActionContext.Direction.");

            if (_definition.overrideGravityScale)
                _gravityOwner = _motor.BeginGravityScale(_definition.gravityScale);

            Vector3 horizontalVelocity;
            float verticalVelocity;
            bool hasHorizontalVelocity = false;

            if (config.directionMode == ImpulseDirectionMode.ToCombatTarget3D)
            {
                if (!TryBuildCombatTarget3DVelocity(actor, config, out horizontalVelocity, out verticalVelocity))
                {
                    WarnMissingTargetOnce(actor);
                    Log(config, "combat-target-3d-missing", Vector3.zero, 0f);
                    return;
                }

                hasHorizontalVelocity = horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr;
            }
            else if (!_definition.useHorizontalImpulse)
            {
                ApplyVertical(actor, config.verticalForce);
                Log(config, "vertical-only", Vector3.zero, config.verticalForce);
                return;
            }
            else
            {
                Vector3 horizontalDirection = ResolvePlanarDirection(actor, context, config);
                horizontalVelocity = horizontalDirection * config.horizontalForce;
                verticalVelocity = config.verticalForce;
                hasHorizontalVelocity = true;
            }

            Apply(actor, hasHorizontalVelocity ? horizontalVelocity : Vector3.zero, verticalVelocity);
            Log(config, config.directionMode.ToString(), horizontalVelocity, verticalVelocity);
        }

        private void Apply(Actor actor, Vector3 horizontalVelocity, float verticalVelocity)
        {
            if (_definition.useHorizontalImpulse && horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr)
                actor.actorMotor.AddHorizontalImpulse(horizontalVelocity);

            ApplyVertical(actor, verticalVelocity);
        }

        private void ApplyVertical(Actor actor, float verticalVelocity)
        {
            if (!_definition.useVerticalBallistic || actor == null || actor.actorMotor == null)
                return;

            if (_definition.verticalOperation == ActionSequenceBallisticVelocityOperation.Set)
                actor.actorMotor.SetBallisticVerticalVelocity(verticalVelocity);
            else
                actor.actorMotor.AddBallisticVerticalVelocity(verticalVelocity);
        }

        private static Vector3 ResolvePlanarDirection(Actor actor, ActionSequenceContext context, ImpulseConfig config)
        {
            Vector3 direction = Vector3.zero;
            switch (config.directionMode)
            {
                case ImpulseDirectionMode.FromContext:
                    direction = context.Context.Direction;
                    break;
                case ImpulseDirectionMode.LocalHorizontal:
                    Vector3 local = new Vector3(config.localHorizontalDirection.x, 0f, config.localHorizontalDirection.z);
                    direction = GetSimulationRotation(actor) * local;
                    break;
            }

            Vector3 up = GetCharacterUp(actor, GetSimulationRotation(actor));
            if (!ProjectPlanar(direction, up, out Vector3 planarDirection))
                throw new InvalidOperationException(
                    $"ImpulseClip horizontal direction {config.directionMode} is invalid.");

            return planarDirection;
        }

        private static bool TryBuildCombatTarget3DVelocity(
            Actor actor,
            ImpulseConfig config,
            out Vector3 horizontalVelocity,
            out float verticalVelocity)
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;

            GameObject target = actor.combater != null ? actor.combater.CombatTarget : null;
            if (target == null)
                return false;

            Vector3 sourcePoint = ResolveActorAimPoint(actor);
            Vector3 targetPoint = ResolveTargetAimPoint(target);
            Vector3 toTarget = targetPoint - sourcePoint;

            if (toTarget.sqrMagnitude < DirectionEpsilonSqr)
                return false;

            Vector3 direction3D = toTarget.normalized;
            Quaternion simulationRotation = GetSimulationRotation(actor);
            Vector3 characterUp = GetCharacterUp(actor, simulationRotation).normalized;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction3D, characterUp);
            if (planarDirection.sqrMagnitude > DirectionEpsilonSqr)
                horizontalVelocity = planarDirection.normalized * Mathf.Abs(config.horizontalForce);

            verticalVelocity = Vector3.Dot(direction3D, characterUp) * Mathf.Abs(config.verticalForce);
            return true;
        }

        private static Vector3 ResolveActorAimPoint(Actor source)
        {
            if (source == null)
                return Vector3.zero;

            ActorMotor actorMotor = source.actorMotor;
            if (actorMotor != null && actorMotor.Capsule != null)
            {
                Vector3 currentCenter = actorMotor.Capsule.transform.TransformPoint(actorMotor.Capsule.center);
                KinematicCharacterMotor motor = actorMotor.Motor;
                if (motor != null && Application.isPlaying)
                {
                    Vector3 currentOffset = currentCenter - actorMotor.transform.position;
                    Vector3 localOffset = Quaternion.Inverse(actorMotor.transform.rotation) * currentOffset;
                    return motor.TransientPosition + motor.TransientRotation * localOffset;
                }

                return currentCenter;
            }

            return GetSimulationPosition(source.gameObject);
        }

        private static Vector3 ResolveTargetAimPoint(GameObject target)
        {
            if (target == null)
                return Vector3.zero;

            Actor targetActor = target.GetComponent<Actor>() ??
                                target.GetComponentInChildren<Actor>() ??
                                target.GetComponentInParent<Actor>();

            if (targetActor != null)
                return ResolveActorAimPoint(targetActor);

            Collider collider = target.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : GetSimulationPosition(target);
        }

        private void ReleaseGravityOwner()
        {
            if (_motor != null && _gravityOwner.IsValid)
                _motor.EndGravityScale(_gravityOwner);

            _gravityOwner = default;
            _motor = null;
        }

        private void WarnMissingTargetOnce(Actor actor)
        {
            if (_warnedMissingTarget)
                return;

            _warnedMissingTarget = true;
            Debug.LogWarning(
                "ImpulseClip ToCombatTarget3D skipped because the dynamic target is missing or degenerate.",
                actor);
        }

        private static void Log(ImpulseConfig config, string mode, Vector3 horizontalVelocity, float verticalVelocity)
        {
            if (config.debugLog)
                Debug.Log($"[ActionSequence Impulse] Start mode={mode}, horizontalVelocity={horizontalVelocity}, verticalVelocity={verticalVelocity}");
        }
    }

    private static Vector3 GetSimulationPosition(GameObject gameObject)
    {
        if (gameObject == null)
            return Vector3.zero;

        Actor actor = gameObject.GetComponent<Actor>();
        ActorMotor actorMotor = actor != null ? actor.actorMotor : gameObject.GetComponent<ActorMotor>();
        KinematicCharacterMotor motor = actorMotor != null ? actorMotor.Motor : null;
        return motor != null && Application.isPlaying ? motor.TransientPosition : gameObject.transform.position;
    }

    private static Quaternion GetSimulationRotation(Actor actor)
    {
        ActorMotor actorMotor = actor != null ? actor.actorMotor : null;
        KinematicCharacterMotor motor = actorMotor != null ? actorMotor.Motor : null;
        if (motor != null && Application.isPlaying)
            return motor.TransientRotation;

        if (actorMotor != null)
            return actorMotor.transform.rotation;

        return actor != null ? actor.transform.rotation : Quaternion.identity;
    }

    private static Vector3 GetCharacterUp(Actor actor, Quaternion currentRotation)
    {
        ActorMotor actorMotor = actor != null ? actor.actorMotor : null;
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

    private static bool IsFiniteNonNegative(float value)
    {
        return IsFinite(value) && value >= 0f;
    }
}
