using Animancer;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// A clip-local pose correction, before layer blending. No Transform writes and no Animator Root Motion.
/// Its explicit clock keeps the extracted yaw sample and the evaluated clip pose at exactly the same time.
/// </summary>
internal sealed class LocomotionStartPoseState : ManualMixerState
{
    private readonly LocomotionStartYawCurve _yaw;
    private readonly float _authoredTurn;
    private Quaternion _offset = Quaternion.identity;
    private Quaternion _evaluatedOffset = Quaternion.identity;
    private double _evaluatedTime;
    private Quaternion _previousPoseRotation;
    private Vector3 _entryMove;
    private float _motorTurn;
    private bool _active;

    internal ClipState Pose { get; }
    internal float SampleTime { get; private set; }
    public override AnimationClip Clip => Pose.Clip;

    internal LocomotionStartPoseState(AnimationClip clip, LocomotionStartYawCurve yaw, float authoredTurn)
    {
        _yaw = yaw;
        _authoredTurn = authoredTurn;
        Pose = new ClipState(clip);
        Add(Pose);
        DontSynchronizeChildren();
        Pose.Weight = 1f;
        Speed = 0f;
    }

    internal void Begin(in LocomotionRuntimeAnimationContext context)
    {
        _active = true;
        SampleTime = _motorTurn = 0f;
        _previousPoseRotation = context.Motor.CurrentWorldRotation;
        _entryMove = Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection, context.Motor.CharacterUp);
    }

    internal void ResetSession() => _active = false;

    internal bool CanContinue(in LocomotionRuntimeAnimationContext context) =>
        LocomotionStartAlignment.IsTurnToMove(context)
        && Vector3.Angle(_entryMove, Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection,
            context.Motor.CharacterUp)) <= LocomotionStartAlignment.DirectionChangeLimit;

    internal void Advance(in LocomotionRuntimeAnimationContext context)
    {
        if (!_active || _yaw == null) return;
        SampleTime = Mathf.Min(_yaw.Duration, SampleTime + context.DeltaTime);
        Vector3 up = context.Motor.CharacterUp;
        Quaternion rotation = context.PoseWorldRotation;
        _motorTurn += Vector3.SignedAngle(
            Vector3.ProjectOnPlane(_previousPoseRotation * Vector3.forward, up),
            Vector3.ProjectOnPlane(rotation * Vector3.forward, up), up);
        _previousPoseRotation = rotation;
        _offset = Quaternion.Euler(0f, LocomotionStartAlignment.PoseYaw(
            _yaw.Sample(SampleTime), _authoredTurn, _motorTurn, SampleTime / _yaw.Duration), 0f);
        TimeD = SampleTime;
        Speed = 0f;
        if (Playable.IsValid()) SetJobData(new TurnPoseJob { Yaw = _offset });
        if (SampleTime >= _yaw.Duration) _active = false;
    }

    internal void RecordEvaluatedPose()
    {
        _evaluatedOffset = _offset;
        _evaluatedTime = LocomotionAnimationUtility.EvaluatedTime(Pose);
    }

    // Protection retains evaluated time AND correction, not a newer, not-yet-evaluated request.
    internal LocomotionStartPoseState CapturePose()
    {
        var snapshot = new LocomotionStartPoseState(Clip, null, 0f) { _offset = _evaluatedOffset };
        snapshot.TimeD = _evaluatedTime;
        return snapshot;
    }

    protected override void CreatePlayable(out Playable playable) =>
        CreatePlayable(out playable, new TurnPoseJob { Yaw = _offset }, processInputs: true);

    private struct TurnPoseJob : IAnimationJob
    {
        public Quaternion Yaw;
        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isHumanStream) return;
            var human = stream.AsHuman();
            human.bodyLocalPosition = Yaw * human.bodyLocalPosition;
            human.bodyLocalRotation = Yaw * human.bodyLocalRotation;
        }
    }
}
