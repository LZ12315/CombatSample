using System;
using DeiveEx.TagTree;
using UnityEngine;

public readonly struct LocomotionModeContext
{
    public LocomotionModeContext(Actor actor, LocomotionIntent intent)
    {
        Actor = actor;
        Intent = intent;
    }

    public Actor Actor { get; }
    public LocomotionIntent Intent { get; }
}

[Serializable]
public abstract class LocomotionModeCondition
{
    [SerializeField] private bool invertResult;

    public bool Check(in LocomotionModeContext context)
    {
        if (context.Actor == null)
            return false;

        bool result = OnCheck(context);
        return invertResult ? !result : result;
    }

    protected abstract bool OnCheck(in LocomotionModeContext context);
}

public enum LocomotionIntentConditionMode
{
    Idle = 0,
    Moving = 1,
}

[Serializable]
public sealed class LocomotionIntentModeCondition : LocomotionModeCondition
{
    [SerializeField] private LocomotionIntentConditionMode mode = LocomotionIntentConditionMode.Moving;
    [SerializeField] private float threshold = 0.01f;

    protected override bool OnCheck(in LocomotionModeContext context)
    {
        float effectiveThreshold = Mathf.Max(0f, threshold);
        bool moving = context.Intent.MoveStrength > effectiveThreshold &&
                      ProjectPlanar(context.Intent.WorldMoveDirection).sqrMagnitude > 0.0001f;

        return mode == LocomotionIntentConditionMode.Moving ? moving : !moving;
    }

    private static Vector3 ProjectPlanar(Vector3 direction)
    {
        direction.y = 0f;
        return direction;
    }
}

[Flags]
public enum LocomotionGroundStateMask
{
    None = 0,
    Grounded = 1 << 0,
    JustLanded = 1 << 1,
    Airborne = 1 << 2,
    JustLeftGround = 1 << 3,
}

[Serializable]
public sealed class LocomotionGroundStateCondition : LocomotionModeCondition
{
    [SerializeField] private LocomotionGroundStateMask acceptedStates =
        LocomotionGroundStateMask.Grounded | LocomotionGroundStateMask.JustLanded;

    protected override bool OnCheck(in LocomotionModeContext context)
    {
        if (context.Actor.actorMotor == null)
            return false;

        return (acceptedStates & ToMask(context.Actor.actorMotor.GroundState)) != 0;
    }

    private static LocomotionGroundStateMask ToMask(ActorGroundState state)
    {
        switch (state)
        {
            case ActorGroundState.Grounded:
                return LocomotionGroundStateMask.Grounded;
            case ActorGroundState.JustLanded:
                return LocomotionGroundStateMask.JustLanded;
            case ActorGroundState.Airborne:
                return LocomotionGroundStateMask.Airborne;
            case ActorGroundState.JustLeftGround:
                return LocomotionGroundStateMask.JustLeftGround;
            default:
                return LocomotionGroundStateMask.None;
        }
    }
}

[Serializable]
public sealed class LocomotionActorTagCondition : LocomotionModeCondition
{
    [SerializeField] private TagReference requiredTag;
    [SerializeField] private ActorTagContainerType targetContainer = ActorTagContainerType.Transient;
    [SerializeField] private ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

    protected override bool OnCheck(in LocomotionModeContext context)
    {
        if (requiredTag == null)
            return false;

        Tag tag = requiredTag.GetTag();
        return tag != null && context.Actor.HasTag(tag, targetContainer, matchMode);
    }
}
