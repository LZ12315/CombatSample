using System;
using UnityEngine;

/// <summary>
/// 永远通过的条件。用于 Idle 等兜底 ActionAsset，确保 CheckEntry 不会因条件列表为空而返回 false。
/// </summary>
[Serializable]
public class AlwaysTrueCondition : ActionCondition
{
    protected override bool OnCheck(Actor actor)
    {
        return true;
    }
}
