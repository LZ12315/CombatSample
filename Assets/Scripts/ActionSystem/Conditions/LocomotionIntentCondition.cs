using System;
using UnityEngine;

/// <summary>
/// 检查角色是否有移动输入意图。
/// 用于 Locomotion ActionAsset 的 EntryCondition。
/// </summary>
[Serializable]
public class LocomotionIntentCondition : ActionCondition
{
    [Tooltip("移动强度阈值，大于此值视为有移动输入")]
    public float threshold = 0.01f;

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || actor.actorMotor == null)
            return false;

        return actor.actorMotor.LocomotionIntent.MoveStrength > threshold;
    }
}
