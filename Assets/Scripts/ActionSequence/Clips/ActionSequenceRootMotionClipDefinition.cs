using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceRootMotionClipDefinition : ActionSequenceClipDefinition
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
        private readonly ActionSequenceRootMotionClipDefinition _definition;
        private RootMotionTrajectory _trajectory;
        private MotionOwner _owner;
        private ActorMotor _motor;

        public Runtime(ActionSequenceRootMotionClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            _motor = actor != null ? actor.actorMotor : null;
            if (_motor == null || !ResolveTrajectory(actor, out _trajectory))
                return;

            _owner = _motor.BeginTrajectoryRootMotion();
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

            if (!_trajectory.TryExtract(startTime, endTime, out RootMotionTransform delta))
                return;

            Vector3 localDelta = delta.Position;
            localDelta.y = 0f;
            _motor.SubmitTrajectoryRootMotion(_owner, localDelta);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (_motor != null && _owner.IsValid)
                _motor.EndTrajectoryRootMotion(_owner);

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
