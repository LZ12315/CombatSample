using UnityEngine;

internal enum LocomotionTickStage
{
    Unprepared,
    ControlLocked,
    MotionPrepared,
    AnimationSubmitted,
}

/// <summary>The current tick only. Pending input and long-lived Runtime state live elsewhere.</summary>
internal struct LocomotionTickState
{
    internal LocomotionTickStage Stage { get; private set; }
    internal LocomotionIntent ControlIntent { get; private set; }
    internal bool HasControlIntent { get; private set; }
    internal bool FromContinuous { get; private set; }
    internal LocomotionMotionRequest MotionRequest { get; private set; }
    internal LocomotionMotionSnapshot? AnimationSnapshot { get; private set; }
    internal bool IsControlLocked => Stage == LocomotionTickStage.ControlLocked;
    internal bool IsMotionPrepared => Stage >= LocomotionTickStage.MotionPrepared;
    internal bool IsAnimationSubmitted => Stage == LocomotionTickStage.AnimationSubmitted;

    internal void Begin(in LocomotionIntent intent, bool hasIntent, bool continuous)
    {
        this = default;
        Stage = LocomotionTickStage.ControlLocked;
        ControlIntent = intent;
        HasControlIntent = hasIntent;
        FromContinuous = continuous;
    }

    internal void CompleteMotion(in LocomotionMotionRequest request, LocomotionMotionSnapshot? snapshot)
    {
        ControlIntent = LocomotionIntent.Idle;
        HasControlIntent = false;
        FromContinuous = false;
        MotionRequest = request;
        AnimationSnapshot = snapshot;
        Stage = LocomotionTickStage.MotionPrepared;
    }

    internal void MarkAnimationSubmitted() => Stage = LocomotionTickStage.AnimationSubmitted;
}

internal readonly struct LocomotionMotionSnapshot
{
    internal readonly LocomotionIntent Intent;
    internal readonly bool HasIntent;
    internal readonly Vector3 VelocityBeforeMotion;
    internal readonly Vector3 ModelVelocity;
    internal readonly LocomotionMotionContext Motor;

    internal LocomotionMotionSnapshot(LocomotionIntent intent, bool hasIntent, Vector3 before,
        Vector3 after, LocomotionMotionContext motor)
    {
        Intent = intent;
        HasIntent = hasIntent;
        VelocityBeforeMotion = before;
        ModelVelocity = after;
        Motor = motor;
    }
}
