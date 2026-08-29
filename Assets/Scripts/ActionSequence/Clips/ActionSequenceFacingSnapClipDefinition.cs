using System;
using KinematicCharacterController;
using UnityEngine;

public enum ActionSequenceFacingSnapSource
{
    ContextDirection = 0,
    CombatTargetThenContextDirection = 1,
    LocomotionIntentThenContextDirection = 2,
}

[Serializable]
public sealed class ActionSequenceFacingSnapClipDefinition : ActionSequenceClipDefinition
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    [SerializeField] private ActionSequenceFacingSnapSource source =
        ActionSequenceFacingSnapSource.ContextDirection;

    public ActionSequenceFacingSnapSource Source => source;
    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Motion;

    public override ActionContextFieldMask RequiredContextFields =>
        source == ActionSequenceFacingSnapSource.ContextDirection
            ? ActionContextFieldMask.Direction
            : ActionContextFieldMask.None;

    public bool IsOneFrame => EndFrame == StartFrame + 1;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceFacingSnapClipDefinition _definition;
        private ActorMotor _motor;
        private MotionOwner _owner;

        public Runtime(ActionSequenceFacingSnapClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            _motor = actor != null ? actor.actorMotor : null;
            if (_motor == null)
                throw new InvalidOperationException("FacingSnapClip requires ActorMotor.");
            if (!_definition.IsOneFrame)
                throw new InvalidOperationException("FacingSnapClip must be exactly one frame long.");

            if (!_motor.BeginScriptedRotation(out _owner))
                throw new InvalidOperationException("FacingSnapClip could not acquire scripted rotation owner.");
        }

        public override void OnTick(ActionSequenceContext context)
        {
            if (_motor == null || !_owner.IsValid)
                return;

            if (!TryResolveWorldDirection(context, out Vector3 direction))
            {
                _motor.SubmitScriptedRotation(_owner, Quaternion.identity);
                return;
            }

            Quaternion currentRotation = GetSimulationRotation(context.Actor, _motor);
            Vector3 characterUp = GetCharacterUp(_motor, currentRotation);
            Vector3 currentForward = currentRotation * Vector3.forward;
            if (!ProjectPlanar(currentForward, characterUp, out currentForward)
                || !ProjectPlanar(direction, characterUp, out direction))
            {
                _motor.SubmitScriptedRotation(_owner, Quaternion.identity);
                return;
            }

            float signedYaw = Vector3.SignedAngle(currentForward, direction, characterUp);
            _motor.SubmitScriptedRotation(_owner, Quaternion.AngleAxis(signedYaw, Vector3.up));
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null && _owner.IsValid)
                _motor.EndScriptedRotation(_owner);

            _owner = default;
            _motor = null;
        }

        private bool TryResolveWorldDirection(ActionSequenceContext context, out Vector3 direction)
        {
            direction = Vector3.zero;
            Actor actor = context.Actor;

            if (_definition.source == ActionSequenceFacingSnapSource.CombatTargetThenContextDirection)
            {
                GameObject target = actor != null && actor.combater != null
                    ? actor.combater.CombatTarget
                    : null;

                if (target != null)
                {
                    direction = GetSimulationPosition(target) - GetSimulationPosition(actor != null ? actor.gameObject : null);
                    direction.y = 0f;
                    if (direction.sqrMagnitude > DirectionEpsilonSqr)
                        return true;
                }
            }

            direction = context.Context.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude > DirectionEpsilonSqr)
                return true;

            ActorMotor motor = actor != null ? actor.actorMotor : null;
            if (motor != null && motor.HasPendingLocomotionIntent)
                direction = motor.PendingLocomotionIntent.WorldMoveDirection;
            else if (motor != null)
                direction = motor.LocomotionIntent.WorldMoveDirection;

            direction.y = 0f;
            return direction.sqrMagnitude > DirectionEpsilonSqr;
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
