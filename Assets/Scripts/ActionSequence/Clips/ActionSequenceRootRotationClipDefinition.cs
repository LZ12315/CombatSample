using System;
using UnityEngine;

[Serializable]
public sealed class ActionSequenceRootRotationClipDefinition : ActionSequenceClipDefinition
{
    [SerializeField] private string animationKey = string.Empty;
    [Min(0f)] public float startOffsetSeconds;
    public float playbackSpeed = 1f;

    public string AnimationKey => animationKey;

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.Motion;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceRootRotationClipDefinition _definition;
        private RootMotionTrajectory _trajectory;
        private MotionOwner _owner;
        private ActorMotor _motor;

        public Runtime(ActionSequenceRootRotationClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            _motor = actor != null ? actor.actorMotor : null;
            if (_motor == null)
                throw new InvalidOperationException("RootRotationClip requires ActorMotor.");
            if (!ResolveTrajectory(actor, out _trajectory))
                throw new InvalidOperationException(
                    $"RootRotationClip could not resolve RootMotionTrajectory for key '{_definition.AnimationKey}'.");

            if (!_motor.BeginRootRotation(out _owner))
                throw new InvalidOperationException("RootRotationClip could not acquire root rotation owner.");
        }

        public override void OnTick(ActionSequenceContext context)
        {
            if (_motor == null || !_owner.IsValid || _trajectory == null)
                return;

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
                    $"RootRotationClip failed to extract local yaw for key '{_definition.AnimationKey}'.");
            }

            if (!_motor.SubmitRootRotation(_owner, localYawDelta))
                throw new InvalidOperationException("RootRotationClip rotation owner became invalid.");
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null && _owner.IsValid)
                _motor.EndRootRotation(_owner);

            _owner = default;
            _trajectory = null;
            _motor = null;
        }

        private bool ResolveTrajectory(Actor actor, out RootMotionTrajectory trajectory)
        {
            trajectory = null;
            AnimationConfig config = actor != null ? actor.AnimationConfig : null;
            return config != null && config.TryGetTrajectory(_definition.AnimationKey, out trajectory);
        }
    }
}
