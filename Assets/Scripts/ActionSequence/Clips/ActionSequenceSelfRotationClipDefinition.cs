using System;
using UnityEngine;

[Serializable]
public sealed class ActionSequenceSelfRotationClipDefinition : ActionSequenceClipDefinition
{
    [SerializeField] private string animationKey = string.Empty;
    [Min(0f)] public float startOffsetSeconds;
    public float playbackSpeed = 1f;

    public string AnimationKey => animationKey;

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Motion;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceSelfRotationClipDefinition _definition;
        private RootMotionTrajectory _trajectory;
        private MotionOwner _owner;
        private ActorMotor _motor;

        public Runtime(ActionSequenceSelfRotationClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            _motor = actor != null ? actor.actorMotor : null;
            if (_motor == null)
                throw new InvalidOperationException("SelfRotationClip requires ActorMotor.");

            if (!ResolveTrajectory(actor, out _trajectory))
                throw new InvalidOperationException(
                    $"SelfRotationClip could not resolve RootMotionTrajectory for key '{_definition.AnimationKey}'.");

            if (!_motor.TryBeginSelfRotation(out _owner))
                throw new InvalidOperationException("SelfRotationClip could not acquire self-rotation owner.");
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
                    $"SelfRotationClip failed to extract local yaw for key '{_definition.AnimationKey}'.");
            }

            if (!_motor.SubmitSelfRotation(_owner, localYawDelta))
                throw new InvalidOperationException("SelfRotationClip self-rotation owner became invalid.");
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null && _owner.IsValid)
                _motor.EndSelfRotation(_owner);

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
