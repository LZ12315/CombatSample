using UnityEngine;

public readonly struct LocomotionRuntimeMotionContext
{
    public LocomotionRuntimeMotionContext(LocomotionRunner motion, LocomotionIntent intent,
        bool hasIntent, LocomotionMotionContext motor)
    {
        Motion = motion;
        Intent = intent;
        HasIntent = hasIntent;
        Motor = motor;
    }

    public LocomotionRunner Motion { get; }
    public LocomotionIntent Intent { get; }
    public bool HasIntent { get; }
    public LocomotionMotionContext Motor { get; }
}
