using UnityEngine;

/// <summary>Only value data and clip identity survive binding; graph reconstruction never reads the authoring asset.</summary>
internal readonly struct LocomotionBoundAnimation
{
    public readonly AnimationClip Clip;
    public readonly AnimationFootPhaseTrack FootPhase;
    public readonly AnimationStopDistanceCurve StopCurve;
    public readonly float Duration;
    public readonly bool HasCycleTrajectory;
    public readonly Vector3 CycleDisplacement;

    internal LocomotionBoundAnimation(AnimationAsset animation, LocomotionMovePlayback.TrajectoryQuery query = null)
    {
        Clip = animation?.Clip;
        Duration = Clip != null ? Clip.length : 0f;
        RootMotionTrajectory trajectory = null;
        bool qualified = animation != null && animation.TryGetLocomotionTrajectory(out trajectory, out _);
        if (!qualified) trajectory = null;
        FootPhase = animation?.CreateFootPhaseTrack(trajectory);
        StopCurve = animation != null && animation.TryCreateStopCurve(trajectory, out var curve) ? curve : null;
        HasCycleTrajectory = false;
        CycleDisplacement = Vector3.zero;
        if (Clip == null || !Clip.isLooping) return;
        bool available = query != null ? query(animation, out trajectory, out _) : qualified;
        if (available && trajectory.TryExtract(0f, Duration, out var cycle))
        {
            HasCycleTrajectory = true;
            CycleDisplacement = cycle.Position;
        }
    }
}
