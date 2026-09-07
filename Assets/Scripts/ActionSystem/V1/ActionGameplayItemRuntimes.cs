using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using KinematicCharacterController;
using UnityEngine;

/// <summary>V1-only preflight. Legacy ActionAsset context checks remain unchanged.</summary>
internal static class ActionRuntimeItemRequirements
{
    internal static void Validate(ActionTimelineData timeline, ActionContext context)
    {
        ActionContextFieldMask required = ActionContextFieldMask.None;
        IReadOnlyList<GameplayLane> lanes = timeline != null ? timeline.GameplayLanes : null;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null || lane.Muted)
                continue;

            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                if (item == null || item.Muted)
                    continue;

                required |= GetRequiredFields(item);
            }
        }

        ActionContextFieldMask missing = required & ~context.Fields;
        if (missing != ActionContextFieldMask.None)
            throw new InvalidOperationException($"Action V1 runtime requires ActionContext fields: {missing}.");
    }

    private static ActionContextFieldMask GetRequiredFields(GameplayItem item)
    {
        switch (item)
        {
            case ImpulseItem impulse when impulse.Config != null
                && impulse.Config.impulse != null
                && impulse.Config.useHorizontalImpulse
                && impulse.Config.impulse.directionMode == ImpulseDirectionMode.FromContext:
                return ActionContextFieldMask.Direction;
            case VelocityOverrideItem velocity when velocity.Config != null
                && velocity.Config.velocity != null
                && velocity.Config.velocity.useHorizontalVelocity
                && velocity.Config.velocity.directionMode == MotionDirectionMode.FromContext:
                return ActionContextFieldMask.Direction;
            case SelfRotationItem rotation when rotation.Config != null:
                if (rotation.Config.source == ActionSelfRotationSource.Target)
                {
                    return rotation.Config.targetSource switch
                    {
                        ActionSelfRotationTargetSource.ContextInstigator => ActionContextFieldMask.Instigator,
                        ActionSelfRotationTargetSource.ContextTarget => ActionContextFieldMask.Target,
                        _ => ActionContextFieldMask.None,
                    };
                }
                return rotation.Config.source == ActionSelfRotationSource.Direction
                    && rotation.Config.directionSource == ActionSelfRotationDirectionSource.ContextDirection
                    ? ActionContextFieldMask.Direction
                    : ActionContextFieldMask.None;
            default:
                return ActionContextFieldMask.None;
        }
    }
}

internal sealed class ActionTagItemRuntime : IActionRangeRuntime
{
    private readonly TagItemConfig _config;
    private Tag _tag;

    public ActionTagItemRuntime(TagItemConfig config) => _config = config;

    public void Enter(ActionRuntimeContext context)
    {
        _tag = _config != null && _config.tag != null ? _config.tag.GetTag() : null;
        if (context.Actor != null && _tag != null)
            context.Actor.AddTag(_tag, _config.targetContainer);
    }

    public void Tick(ActionRuntimeContext context, int localFrame) { }
    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release(context);
    public void Abort(ActionRuntimeContext context) => Release(context);

    private void Release(ActionRuntimeContext context)
    {
        if (context.Actor != null && _tag != null)
            context.Actor.RemoveTag(_tag, _config.targetContainer);
        _tag = null;
    }
}

internal sealed class ActionMotionPolicyItemRuntime : IActionRangeRuntime
{
    private readonly MotionPolicyItemConfig _config;
    private ActorMotor _motor;
    private MotionOwner _locomotionOwner;
    private MotionOwner _airLocomotionOwner;
    private MotionOwner _gravityOwner;

    public ActionMotionPolicyItemRuntime(MotionPolicyItemConfig config) => _config = config;

    public void Enter(ActionRuntimeContext context)
    {
        _motor = context.Motor ?? throw new InvalidOperationException("MotionPolicyItem requires ActorMotor.");
        if (_config == null || (!_config.useLocomotionScale && !_config.useAirLocomotionScale && !_config.useGravityScale))
            throw new InvalidOperationException("MotionPolicyItem requires at least one policy.");

        if (_config.useLocomotionScale)
            _locomotionOwner = _motor.BeginLocomotionScale(_config.locomotionScale);
        if (_config.useAirLocomotionScale)
            _airLocomotionOwner = _motor.BeginAirLocomotionScale(_config.airLocomotionScale);
        if (_config.useGravityScale)
            _gravityOwner = _motor.BeginGravityScale(_config.gravityScale);
    }

    public void Tick(ActionRuntimeContext context, int localFrame) { }
    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release();
    public void Abort(ActionRuntimeContext context) => Release();

    private void Release()
    {
        if (_motor != null)
        {
            if (_locomotionOwner.IsValid) _motor.EndLocomotionScale(_locomotionOwner);
            if (_airLocomotionOwner.IsValid) _motor.EndAirLocomotionScale(_airLocomotionOwner);
            if (_gravityOwner.IsValid) _motor.EndGravityScale(_gravityOwner);
        }
        _locomotionOwner = default;
        _airLocomotionOwner = default;
        _gravityOwner = default;
        _motor = null;
    }
}

internal sealed class ActionHitBoxItemRuntime : IActionRangeRuntime
{
    private readonly HitBoxItemConfig _config;
    private readonly string _stableId;
    private HitBoxHandle _handle;
    private ActorHitBoxRuntime _hitBoxes;
    private bool _reportedMissingReceiver;

    public ActionHitBoxItemRuntime(HitBoxItemConfig config, string stableId)
    {
        _config = config;
        _stableId = stableId ?? string.Empty;
    }

    public void Enter(ActionRuntimeContext context)
    {
        _hitBoxes = context.HitBoxes;
        if (_hitBoxes == null)
        {
            if (!_reportedMissingReceiver)
            {
                _reportedMissingReceiver = true;
                Debug.LogWarning("[Action V1 HitBox] No ActorHitBoxRuntime is available; hit query is skipped.", context.Actor);
            }
            return;
        }

        _handle = _hitBoxes.Activate(
            _stableId,
            _config != null ? _config.boneReference : default,
            _config != null ? _config.hitboxConfig : null,
            _config != null ? _config.dataConfig : null,
            _config != null ? _config.effects : null);
    }

    public void Tick(ActionRuntimeContext context, int localFrame) { }
    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release();
    public void Abort(ActionRuntimeContext context) => Release();

    private void Release()
    {
        if (_hitBoxes != null)
            _hitBoxes.Deactivate(_handle);
        _handle = default;
        _hitBoxes = null;
    }
}

internal sealed class ActionVelocityOverrideItemRuntime : IActionRangeRuntime
{
    private readonly VelocityOverrideItemConfig _config;
    private readonly int _durationFrames;
    private ActorMotor _motor;
    private Actor _actor;
    private MotionOwner _horizontalOwner;
    private MotionOwner _verticalOwner;

    public ActionVelocityOverrideItemRuntime(VelocityOverrideItemConfig config, int durationFrames)
    {
        _config = config;
        _durationFrames = Math.Max(1, durationFrames);
    }

    public void Enter(ActionRuntimeContext context)
    {
        _actor = context.Actor;
        _motor = context.Motor ?? throw new InvalidOperationException("VelocityOverrideItem requires ActorMotor.");
        VelocityConfig velocity = _config != null ? _config.velocity : null;
        if (velocity == null || (!velocity.useHorizontalVelocity && !velocity.useVerticalVelocity))
            throw new InvalidOperationException("VelocityOverrideItem requires at least one velocity axis.");
        if (velocity.useHorizontalVelocity)
            _horizontalOwner = _motor.BeginHorizontalVelocity();
        if (velocity.useVerticalVelocity)
            _verticalOwner = _motor.BeginVerticalVelocity();
    }

    public void Tick(ActionRuntimeContext context, int localFrame)
    {
        VelocityConfig velocity = _config.velocity;
        float sample = _durationFrames <= 1 ? 0f : Mathf.Clamp01(localFrame / (float)(_durationFrames - 1));
        if (_horizontalOwner.IsValid)
        {
            float multiplier = velocity.horizontalCurve != null ? velocity.horizontalCurve.Evaluate(sample) : 1f;
            _motor.SetHorizontalVelocity(_horizontalOwner,
                ActionItemRuntimeUtility.ResolvePlanarDirection(_actor, _motor, context.ActionContext, velocity.directionMode, velocity.localHorizontalDirection)
                * (velocity.horizontalSpeed * multiplier));
        }
        if (_verticalOwner.IsValid)
        {
            float multiplier = velocity.verticalCurve != null ? velocity.verticalCurve.Evaluate(sample) : 1f;
            _motor.SetVerticalVelocity(_verticalOwner, velocity.verticalSpeed * multiplier);
        }
    }

    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release();
    public void Abort(ActionRuntimeContext context) => Release();

    private void Release()
    {
        if (_motor != null)
        {
            if (_horizontalOwner.IsValid) _motor.EndHorizontalVelocity(_horizontalOwner);
            if (_verticalOwner.IsValid) _motor.EndVerticalVelocity(_verticalOwner);
        }
        _horizontalOwner = default;
        _verticalOwner = default;
        _actor = null;
        _motor = null;
    }
}

internal sealed class ActionRootMotionItemRuntime : IActionRangeRuntime
{
    private readonly RootMotionItemConfig _config;
    private readonly int _durationFrames;
    private ActorMotor _motor;
    private RootMotionTrajectory _trajectory;
    private MotionOwner _owner;

    public ActionRootMotionItemRuntime(RootMotionItemConfig config, int durationFrames)
    {
        _config = config;
        _durationFrames = Math.Max(1, durationFrames);
    }

    public void Enter(ActionRuntimeContext context)
    {
        _motor = context.Motor ?? throw new InvalidOperationException("RootMotionItem requires ActorMotor.");
        _trajectory = _config != null && _config.animationAsset != null ? _config.animationAsset.RootMotionData : null;
        if (_trajectory == null || !_trajectory.ValidateData().IsValid)
            throw new InvalidOperationException("RootMotionItem requires valid baked AnimationAsset.RootMotionData.");
        _owner = _motor.BeginTrajectoryRootMotion();
    }

    public void Tick(ActionRuntimeContext context, int localFrame)
    {
        if (!_owner.IsValid || _trajectory == null)
            return;

        ActionItemRuntimeUtility.GetSourceWindow(_config.sourceStartTime, _config.playRate, localFrame, out float start, out float end);
        if (_trajectory.TryExtract(start, end, out RootMotionTransform delta))
        {
            Vector3 localDelta = delta.Position;
            localDelta.y = 0f;
            _motor.SubmitTrajectoryRootMotion(_owner, localDelta);
        }
    }

    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release();
    public void Abort(ActionRuntimeContext context) => Release();

    private void Release()
    {
        if (_motor != null && _owner.IsValid)
            _motor.EndTrajectoryRootMotion(_owner);
        _owner = default;
        _trajectory = null;
        _motor = null;
    }
}

internal sealed class ActionSelfRotationItemRuntime : IActionRangeRuntime
{
    private readonly SelfRotationItemConfig _config;
    private readonly int _durationFrames;
    private Actor _actor;
    private ActorMotor _motor;
    private RootMotionTrajectory _trajectory;
    private MotionOwner _owner;
    private Vector3 _presetWorldDirection;
    private bool _usesRootRotation;
    private bool _warnedMissingTarget;

    public ActionSelfRotationItemRuntime(SelfRotationItemConfig config, int durationFrames)
    {
        _config = config;
        _durationFrames = Math.Max(1, durationFrames);
    }

    public void Enter(ActionRuntimeContext context)
    {
        _actor = context.Actor;
        _motor = context.Motor ?? throw new InvalidOperationException("SelfRotationItem requires ActorMotor.");
        if (_config == null)
            throw new InvalidOperationException("SelfRotationItem requires config.");

        Quaternion currentRotation = ActionItemRuntimeUtility.GetSimulationRotation(_actor, _motor);
        _presetWorldDirection = currentRotation * _config.presetLocalDirection;
        _usesRootRotation = _config.source == ActionSelfRotationSource.RootMotion;
        if (_usesRootRotation)
        {
            _trajectory = _config.animationAsset != null ? _config.animationAsset.RootMotionData : null;
            if (_trajectory == null || !_trajectory.ValidateData().IsValid)
                throw new InvalidOperationException("RootMotion SelfRotationItem requires valid baked AnimationAsset.RootMotionData.");
            if (!_motor.BeginRootRotation(out _owner))
                throw new InvalidOperationException("SelfRotationItem could not acquire root rotation owner.");
        }
        else if (!_motor.BeginScriptedRotation(out _owner))
        {
            throw new InvalidOperationException("SelfRotationItem could not acquire scripted rotation owner.");
        }
    }

    public void Tick(ActionRuntimeContext context, int localFrame)
    {
        if (!_owner.IsValid)
            return;

        Quaternion localYaw = _usesRootRotation
            ? ExtractRootYaw(localFrame)
            : BuildDesiredYaw(context.ActionContext);
        bool submitted = _usesRootRotation
            ? _motor.SubmitRootRotation(_owner, localYaw)
            : _motor.SubmitScriptedRotation(_owner, localYaw);
        if (!submitted)
            throw new InvalidOperationException("SelfRotationItem rotation owner became invalid.");
    }

    public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => Release();
    public void Abort(ActionRuntimeContext context) => Release();

    private Quaternion ExtractRootYaw(int localFrame)
    {
        ActionItemRuntimeUtility.GetSourceWindow(_config.sourceStartTime, _config.playRate, localFrame, out float start, out float end);
        if (_trajectory == null || !_trajectory.TryExtract(start, end, out RootMotionTransform delta)
            || !RootMotionYawUtility.TryExtractLocalYaw(delta.Rotation, out Quaternion yaw))
            return Quaternion.identity;

        return _config.mode == ActionSelfRotationMode.RotateBySpeed
            ? ActionItemRuntimeUtility.ClampLocalYaw(yaw, _config.angularSpeedDegrees)
            : yaw;
    }

    private Quaternion BuildDesiredYaw(ActionContext context)
    {
        Quaternion currentRotation = ActionItemRuntimeUtility.GetSimulationRotation(_actor, _motor);
        Vector3 up = ActionItemRuntimeUtility.GetCharacterUp(_motor, currentRotation);
        if (!TryGetDesiredDirection(context, up, out Vector3 desired))
            return Quaternion.identity;
        return ActionItemRuntimeUtility.BuildLocalYaw(currentRotation, up, desired, _config.mode, _config.angularSpeedDegrees);
    }

    private bool TryGetDesiredDirection(ActionContext context, Vector3 up, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (_config.source == ActionSelfRotationSource.Direction)
        {
            Vector3 raw = _config.directionSource == ActionSelfRotationDirectionSource.ContextDirection
                ? context.Direction
                : _presetWorldDirection;
            return ActionItemRuntimeUtility.TryProjectPlanar(raw, up, out direction);
        }

        GameObject target = _config.targetSource switch
        {
            ActionSelfRotationTargetSource.CombatTarget => _actor != null && _actor.combater != null ? _actor.combater.CombatTarget : null,
            ActionSelfRotationTargetSource.ContextInstigator => context.Instigator,
            ActionSelfRotationTargetSource.ContextTarget => context.Target,
            _ => null,
        };
        if (target == null)
        {
            if (!_warnedMissingTarget)
            {
                _warnedMissingTarget = true;
                Debug.LogWarning("[Action V1 SelfRotation] Target is missing; rotation is skipped.", _motor);
            }
            return false;
        }
        return ActionItemRuntimeUtility.TryProjectPlanar(
            ActionItemRuntimeUtility.GetSimulationPosition(target) - ActionItemRuntimeUtility.GetSimulationPosition(_actor.gameObject),
            up,
            out direction);
    }

    private void Release()
    {
        if (_motor != null && _owner.IsValid)
        {
            if (_usesRootRotation) _motor.EndRootRotation(_owner);
            else _motor.EndScriptedRotation(_owner);
        }
        _owner = default;
        _trajectory = null;
        _actor = null;
        _motor = null;
        _usesRootRotation = false;
    }
}

internal sealed class ActionImpulseItemRuntime : IActionPointRuntime
{
    private const float DirectionEpsilonSqr = 0.000001f;
    private readonly ImpulseItemConfig _config;
    private bool _warnedMissingTarget;

    public ActionImpulseItemRuntime(ImpulseItemConfig config) => _config = config;

    public void Execute(ActionRuntimeContext context)
    {
        ActorMotor motor = context.Motor ?? throw new InvalidOperationException("ImpulseItem requires ActorMotor.");
        ImpulseConfig impulse = _config != null ? _config.impulse : null;
        if (impulse == null || (!_config.useHorizontalImpulse && !_config.useVerticalBallistic))
            throw new InvalidOperationException("ImpulseItem requires an impulse contribution.");
        if (_config.overrideGravityScale)
            throw new InvalidOperationException("V1 ImpulseItem cannot override gravity; use MotionPolicyItem.");

        Vector3 horizontal = Vector3.zero;
        float vertical = impulse.verticalForce;
        bool hasHorizontal = false;
        if (impulse.directionMode == ImpulseDirectionMode.ToCombatTarget3D)
        {
            if (!TryBuildCombatTargetVelocity(context.Actor, motor, impulse, out horizontal, out vertical))
            {
                if (!_warnedMissingTarget)
                {
                    _warnedMissingTarget = true;
                    Debug.LogWarning("[Action V1 Impulse] Combat target is missing; impulse is skipped.", context.Actor);
                }
                return;
            }
            hasHorizontal = horizontal.sqrMagnitude > DirectionEpsilonSqr;
        }
        else if (_config.useHorizontalImpulse)
        {
            Vector3 direction = ActionItemRuntimeUtility.ResolvePlanarDirection(
                context.Actor, motor, context.ActionContext,
                impulse.directionMode == ImpulseDirectionMode.FromContext ? MotionDirectionMode.FromContext : MotionDirectionMode.LocalHorizontal,
                impulse.localHorizontalDirection);
            horizontal = direction * impulse.horizontalForce;
            hasHorizontal = true;
        }

        if (_config.useHorizontalImpulse && hasHorizontal)
            motor.AddHorizontalImpulse(horizontal);
        if (_config.useVerticalBallistic)
        {
            if (_config.verticalOperation == ActionBallisticVelocityOperation.Set)
                motor.SetBallisticVerticalVelocity(vertical);
            else
                motor.AddBallisticVerticalVelocity(vertical);
        }
    }

    private static bool TryBuildCombatTargetVelocity(Actor actor, ActorMotor motor, ImpulseConfig config, out Vector3 horizontal, out float vertical)
    {
        horizontal = Vector3.zero;
        vertical = 0f;
        GameObject target = actor != null && actor.combater != null ? actor.combater.CombatTarget : null;
        if (target == null)
            return false;

        Vector3 direction = ActionItemRuntimeUtility.GetSimulationPosition(target) - ActionItemRuntimeUtility.GetSimulationPosition(actor.gameObject);
        if (direction.sqrMagnitude <= DirectionEpsilonSqr)
            return false;
        direction.Normalize();
        Quaternion rotation = ActionItemRuntimeUtility.GetSimulationRotation(actor, motor);
        Vector3 up = ActionItemRuntimeUtility.GetCharacterUp(motor, rotation);
        Vector3 planar = Vector3.ProjectOnPlane(direction, up);
        if (planar.sqrMagnitude > DirectionEpsilonSqr)
            horizontal = planar.normalized * Mathf.Abs(config.horizontalForce);
        vertical = Vector3.Dot(direction, up.normalized) * Mathf.Abs(config.verticalForce);
        return true;
    }
}

internal static class ActionItemRuntimeUtility
{
    private const float EpsilonSqr = 0.000001f;

    internal static void GetSourceWindow(float sourceStartTime, float playRate, int localFrame, out float start, out float end)
    {
        start = sourceStartTime + localFrame / (float)ActionTimelineData.FrameRate * playRate;
        end = sourceStartTime + (localFrame + 1) / (float)ActionTimelineData.FrameRate * playRate;
    }

    internal static Quaternion GetSimulationRotation(Actor actor, ActorMotor motor)
    {
        KinematicCharacterMotor kcc = motor != null ? motor.Motor : null;
        if (kcc != null && Application.isPlaying)
            return kcc.TransientRotation;
        if (motor != null)
            return motor.transform.rotation;
        return actor != null ? actor.transform.rotation : Quaternion.identity;
    }

    internal static Vector3 GetSimulationPosition(GameObject gameObject)
    {
        if (gameObject == null)
            return Vector3.zero;
        Actor actor = gameObject.GetComponent<Actor>();
        ActorMotor motor = actor != null ? actor.actorMotor : gameObject.GetComponent<ActorMotor>();
        KinematicCharacterMotor kcc = motor != null ? motor.Motor : null;
        return kcc != null && Application.isPlaying ? kcc.TransientPosition : gameObject.transform.position;
    }

    internal static Vector3 GetCharacterUp(ActorMotor motor, Quaternion rotation)
    {
        KinematicCharacterMotor kcc = motor != null ? motor.Motor : null;
        return kcc != null && Application.isPlaying ? kcc.CharacterUp : rotation * Vector3.up;
    }

    internal static Vector3 ResolvePlanarDirection(Actor actor, ActorMotor motor, ActionContext context, MotionDirectionMode mode, Vector3 localDirection)
    {
        Vector3 raw = mode == MotionDirectionMode.FromContext
            ? context.Direction
            : GetSimulationRotation(actor, motor) * new Vector3(localDirection.x, 0f, localDirection.z);
        if (!TryProjectPlanar(raw, GetCharacterUp(motor, GetSimulationRotation(actor, motor)), out Vector3 planar))
            throw new InvalidOperationException("Action V1 motion direction is invalid.");
        return planar;
    }

    internal static bool TryProjectPlanar(Vector3 direction, Vector3 up, out Vector3 planar)
    {
        planar = Vector3.zero;
        if (!IsFinite(direction) || !IsFinite(up) || up.sqrMagnitude <= EpsilonSqr)
            return false;
        planar = Vector3.ProjectOnPlane(direction, up.normalized);
        if (planar.sqrMagnitude <= EpsilonSqr)
            return false;
        planar.Normalize();
        return true;
    }

    internal static Quaternion ClampLocalYaw(Quaternion localYaw, float degreesPerSecond)
    {
        float signedYaw = Mathf.DeltaAngle(0f, localYaw.eulerAngles.y);
        signedYaw = Mathf.Clamp(signedYaw, -degreesPerSecond / ActionTimelineData.FrameRate, degreesPerSecond / ActionTimelineData.FrameRate);
        return Quaternion.AngleAxis(signedYaw, Vector3.up);
    }

    internal static Quaternion BuildLocalYaw(Quaternion current, Vector3 up, Vector3 desired, ActionSelfRotationMode mode, float degreesPerSecond)
    {
        if (!TryProjectPlanar(current * Vector3.forward, up, out Vector3 forward))
            return Quaternion.identity;
        float signedYaw = Vector3.SignedAngle(forward, desired, up);
        if (mode == ActionSelfRotationMode.RotateBySpeed)
            signedYaw = Mathf.Clamp(signedYaw, -degreesPerSecond / ActionTimelineData.FrameRate, degreesPerSecond / ActionTimelineData.FrameRate);
        return Quaternion.AngleAxis(signedYaw, Vector3.up);
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
        && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
