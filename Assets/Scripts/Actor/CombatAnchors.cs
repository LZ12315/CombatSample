using UnityEngine;

/// <summary>
/// 战斗用锚点 id + 表项（与 Actor 内嵌列表配合使用，不单独挂组件）。
/// </summary>
public enum CombatAnchorId
{
    WeaponBladeMid,
    RightHand,
    LeftHand,
}

[System.Serializable]
public struct CombatAnchorEntry
{
    public CombatAnchorId id;
    public Transform transform;
}
