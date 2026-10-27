using System;
using UnityEngine;

/// <summary>
/// 永远通过的条件。可配合 invertResult 显式关闭某个条件集。
/// </summary>
[Serializable]
public class AlwaysTrueCondition : ActionCondition
{
    protected override bool OnCheck(Actor actor)
    {
        return true;
    }
}
