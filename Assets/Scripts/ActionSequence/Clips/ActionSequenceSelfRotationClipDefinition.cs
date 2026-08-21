using System;
using KinematicCharacterController;
using UnityEngine;

public enum SelfRotationSource
{
    RootRotation = 0,
    Target = 1,
    Direction = 2,
}

public enum SelfRotationMode
{
    Snap = 0,
    RotateBySpeed = 1,
}

public enum SelfRotationTargetSource
{
    CombatTarget = 0,
    ContextInstigator = 1,
    ContextTarget = 2,
}

public enum SelfRotationDirectionSource
{
    PresetLocal = 0,
    ContextDirection = 1,
}

[Serializable]
public sealed class ActionSequenceSelfRotationClipDefinition : ActionSequenceClipDefinition
{
    private const float DirectionEpsilonSqr = 0.000001f;

    [SerializeField] private SelfRotationSource source = SelfRotationSource.RootRotation;
    [SerializeField] private SelfRotationMode mode = SelfRotationMode.Snap;
    [SerializeField] private SelfRotationTargetSource targetSource = SelfRotationTargetSource.CombatTarget;
    [SerializeField] private SelfRotationDirectionSource directionSource = SelfRotationDirectionSource.PresetLocal;
    [SerializeField] private Vector3 presetLocalDirection = Vector3.forward;
    [SerializeField, Min(0f)] private float angularSpeedDegrees = 720f;

    [SerializeField] private string animationKey = string.Empty;
    [Min(0f)] public float startOffsetSeconds;
    public float playbackSpeed = 1f;

    public SelfRotationSource Source => source;
    public SelfRotationMode Mode => mode;
    public SelfRotationTargetSource TargetSource => targetSource;
    public SelfRotationDirectionSource DirectionSource => directionSource;
    public Vector3 PresetLocalDirection => presetLocalDirection;
    public float AngularSpeedDegrees => angularSpeedDegrees;
    public string AnimationKey => animationKey;

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Motion;

    public override ActionContextFieldMask RequiredContextFields
    {
        get
        {
            if (source == SelfRotationSource.Target)
            {
                return targetSource switch
                {
                    SelfRotationTargetSource.ContextInstigator => ActionContextFieldMask.Instigator,
                    SelfRotationTargetSource.ContextTarget => ActionContextFieldMask.Target,
                    _ => ActionContextFieldMask.None,
                };
            }

            if (source == SelfRotationSource.Direction
                && directionSource == SelfRotationDirectionSource.ContextDirection)
            {
                return ActionContextFieldMask.Direction;
            }

            return ActionContextFieldMask.None;
        }
    }

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    public bool HasValidPresetLocalDirection()
    {
        return IsFinite(presetLocalDirection)
            && presetLocalDirection.sqrMagnitude > DirectionEpsilonSqr;
    }

    public bool HasValidAngularSpeed()
    {
        return mode != SelfRotationMode.RotateBySpeed
            || (IsFinite(angularSpeedDegrees) && angularSpeedDegrees > 0f);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceSelfRotationClipDefinition _definition;
        private RootMotionTrajectory _trajectory;
        private MotionOwner _owner;
        private Actor _actor;
        private ActorMotor _motor;
        private Quaternion _rootRotationDesiredWorld = Quaternion.identity;
        private Vector3 _presetWorldDirection = Vector3.forward;
        private bool _warnedMissingTarget;

        public Runtime(ActionSequenceSelfRotationClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            _actor = context.Actor;
            _motor = _actor != null ? _actor.actorMotor : null;
            if (_motor == null)
                throw new InvalidOperationException("SelfRotationClip requires ActorMotor.");

            Quaternion currentRotation = GetSimulationRotation(_actor, _motor);
            _rootRotationDesiredWorld = currentRotation;
            _presetWorldDirection = currentRotation * _definition.presetLocalDirection;
            _warnedMissingTarget = false;

            if (_definition.source == SelfRotationSource.RootRotation
                && !ResolveTrajectory(_actor, out _trajectory))
            {
                throw new InvalidOperationException(
                    $"SelfRotationClip could not resolve RootMotionTrajectory for key '{_definition.AnimationKey}'.");
            }

            if (_definition.source == SelfRotationSource.Direction
                && _definition.directionSource == SelfRotationDirectionSource.PresetLocal
                && !_definition.HasValidPresetLocalDirection())
            {
                throw new InvalidOperationException("SelfRotationClip PresetLocal direction is invalid.");
            }

            if (!_definition.HasValidAngularSpeed())
                throw new InvalidOperationException("SelfRotationClip RotateBySpeed requires Angular Speed > 0.");

            if (!_motor.TryBeginSelfRotation(out _owner))
                throw new InvalidOperationException("SelfRotationClip could not acquire self-rotation owner.");
        }

        public override void OnTick(ActionSequenceContext context)
        {
            if (_motor == null || !_owner.IsValid)
                return;

            Quaternion currentRotation = GetSimulationRotation(_actor, _motor);
            Vector3 characterUp = GetCharacterUp(_motor, currentRotation);

            if (!TryResolveDesiredWorldDirection(context, currentRotation, characterUp, out Vector3 desiredWorldDirection))
            {
                SubmitYaw(Quaternion.identity);
                return;
            }

            if (!TryBuildLocalYawDelta(
                    currentRotation,
                    characterUp,
                    desiredWorldDirection,
                    context.FrameRate,
                    out Quaternion localYawDelta))
            {
                SubmitYaw(Quaternion.identity);
                return;
            }

            SubmitYaw(localYawDelta);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null && _owner.IsValid)
                _motor.EndSelfRotation(_owner);

            _owner = default;
            _trajectory = null;
            _actor = null;
            _motor = null;
        }

        private bool TryResolveDesiredWorldDirection(
            ActionSequenceContext context,
            Quaternion currentRotation,
            Vector3 characterUp,
            out Vector3 desiredWorldDirection)
        {
            desiredWorldDirection = Vector3.zero;
            switch (_definition.source)
            {
                case SelfRotationSource.RootRotation:
                    if (!AdvanceRootRotation(context))
                        return false;

                    desiredWorldDirection = _rootRotationDesiredWorld * Vector3.forward;
                    return ProjectPlanarDirection(desiredWorldDirection, characterUp, out desiredWorldDirection);

                case SelfRotationSource.Target:
                    return TryResolveTargetDirection(context, characterUp, out desiredWorldDirection);

                case SelfRotationSource.Direction:
                    return TryResolveConfiguredDirection(context, characterUp, out desiredWorldDirection);

                default:
                    desiredWorldDirection = currentRotation * Vector3.forward;
                    return ProjectPlanarDirection(desiredWorldDirection, characterUp, out desiredWorldDirection);
            }
        }

        private bool AdvanceRootRotation(ActionSequenceContext context)
        {
            if (_trajectory == null)
                return false;

            float startTime = ActionSequenceAnimationTimeUtility.GetFrameStartTime(
                context,
                _definition.StartFrame,
                _definition.startOffsetSeconds,
                _definition.playbackSpeed);
            float endTime = ActionSequenceAnimationTimeUtility.GetFrameEndTime(
                context,
                _definition.StartFrame,
                _definition.startOffsetSeconds,
                _definition.playbackSpeed);

            if (!_trajectory.TryExtract(startTime, endTime, out RootMotionTransform delta)
                || !RootMotionYawUtility.TryExtractLocalYaw(delta.Rotation, out Quaternion localYawDelta))
            {
                throw new InvalidOperationException(
                    $"SelfRotationClip failed to extract local yaw for key '{_definition.AnimationKey}'.");
            }

            _rootRotationDesiredWorld *= localYawDelta;
            return true;
        }

        private bool TryResolveTargetDirection(
            ActionSequenceContext context,
            Vector3 characterUp,
            out Vector3 desiredWorldDirection)
        {
            desiredWorldDirection = Vector3.zero;
            GameObject target = ResolveTargetObject(context);
            if (target == null)
            {
                WarnMissingTargetOnce("target is missing.");
                return false;
            }

            Vector3 origin = GetSimulationPosition(_actor != null ? _actor.gameObject : null);
            Vector3 targetPosition = GetSimulationPosition(target);
            Vector3 direction = targetPosition - origin;
            if (!ProjectPlanarDirection(direction, characterUp, out desiredWorldDirection))
            {
                WarnMissingTargetOnce("target direction is degenerate.");
                return false;
            }

            return true;
        }

        private GameObject ResolveTargetObject(ActionSequenceContext context)
        {
            return _definition.targetSource switch
            {
                SelfRotationTargetSource.CombatTarget => _actor != null && _actor.combater != null
                    ? _actor.combater.CombatTarget
                    : null,
                SelfRotationTargetSource.ContextInstigator => context.Context.Instigator,
                SelfRotationTargetSource.ContextTarget => context.Context.Target,
                _ => null,
            };
        }

        private bool TryResolveConfiguredDirection(
            ActionSequenceContext context,
            Vector3 characterUp,
            out Vector3 desiredWorldDirection)
        {
            Vector3 direction = _definition.directionSource == SelfRotationDirectionSource.ContextDirection
                ? context.Context.Direction
                : _presetWorldDirection;

            return ProjectPlanarDirection(direction, characterUp, out desiredWorldDirection);
        }

        private bool TryBuildLocalYawDelta(
            Quaternion currentRotation,
            Vector3 characterUp,
            Vector3 desiredWorldDirection,
            int frameRate,
            out Quaternion localYawDelta)
        {
            localYawDelta = Quaternion.identity;
            Vector3 currentForward = currentRotation * Vector3.forward;
            if (!ProjectPlanarDirection(currentForward, characterUp, out currentForward))
                return false;

            float signedYaw = Vector3.SignedAngle(currentForward, desiredWorldDirection, characterUp);
            if (_definition.mode == SelfRotationMode.RotateBySpeed)
            {
                float maxDelta = _definition.angularSpeedDegrees / Mathf.Max(1, frameRate);
                signedYaw = Mathf.Clamp(signedYaw, -maxDelta, maxDelta);
            }

            localYawDelta = Quaternion.AngleAxis(signedYaw, Vector3.up);
            return true;
        }

        private void SubmitYaw(Quaternion localYawDelta)
        {
            if (!_motor.SubmitSelfRotation(_owner, localYawDelta))
                throw new InvalidOperationException("SelfRotationClip self-rotation owner became invalid.");
        }

        private bool ResolveTrajectory(Actor actor, out RootMotionTrajectory trajectory)
        {
            trajectory = null;
            AnimationConfig config = actor != null ? actor.AnimationConfig : null;
            return config != null && config.TryGetTrajectory(_definition.AnimationKey, out trajectory);
        }

        private void WarnMissingTargetOnce(string reason)
        {
            if (_warnedMissingTarget)
                return;

            _warnedMissingTarget = true;
            Debug.LogWarning($"SelfRotationClip target source {_definition.targetSource} cannot rotate because {reason}", _motor);
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

    private static bool ProjectPlanarDirection(Vector3 direction, Vector3 up, out Vector3 planarDirection)
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
