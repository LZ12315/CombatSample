using UnityEngine;

public readonly struct LocomotionSelectionContext
{
    public LocomotionSelectionContext(Actor actor, LocomotionIntent intent, LocomotionMotionContext motion)
    {
        Actor = actor;
        Intent = intent;
        Motion = motion;
    }

    public Actor Actor { get; }
    public LocomotionIntent Intent { get; }
    public LocomotionMotionContext Motion { get; }
}
