using System;
using UnityEngine;

/// <summary>
/// 检查角色当前的地面状态，用于 ActionAsset 的 EntryCondition。
/// 数据源 = ActorMotor.GroundState（唯一权威），不依赖独立的 GroundCheck 组件。
///
/// 使用 Flags 枚举支持多选：比如同时勾选 Grounded + JustLanded 表达"在地面上（含刚落地那一帧）"。
/// 与基类 invertResult 配合可表达"不在地面"等反向语义。
/// </summary>
[Serializable]
public class GroundStateCondition : ActionCondition
{
    [Tooltip("勾选所有视为通过的地面状态。可多选。\n" +
             "Grounded = 稳定贴地\n" +
             "JustLanded = 刚落地那一帧\n" +
             "Airborne = 在空中\n" +
             "JustLeftGround = 刚离地那一帧")]
    public GroundStateMask acceptedStates = GroundStateMask.Grounded | GroundStateMask.JustLanded;

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || actor.actorMotor == null)
            return false;

        var mask = ToMask(actor.actorMotor.GroundState);
        return (acceptedStates & mask) != 0;
    }

    private static GroundStateMask ToMask(ActorGroundState state)
    {
        switch (state)
        {
            case ActorGroundState.Grounded:        return GroundStateMask.Grounded;
            case ActorGroundState.JustLanded:      return GroundStateMask.JustLanded;
            case ActorGroundState.Airborne:        return GroundStateMask.Airborne;
            case ActorGroundState.JustLeftGround:  return GroundStateMask.JustLeftGround;
            default:                                        return 0;
        }
    }

    [Flags]
    public enum GroundStateMask
    {
        None            = 0,
        Grounded        = 1 << 0,
        JustLanded      = 1 << 1,
        Airborne        = 1 << 2,
        JustLeftGround  = 1 << 3,
    }
}
