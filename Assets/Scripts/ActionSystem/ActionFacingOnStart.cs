/// <summary>
/// Action 开始时的朝向行为。
/// </summary>
public enum ActionFacingOnStart
{
    /// <summary>不转向（受击、下劈等）</summary>
    None,
    /// <summary>朝摇杆方向（冲刺、翻滚）</summary>
    SnapToInput,
    /// <summary>有目标朝目标，否则朝摇杆（大多数攻击）</summary>
    SnapToInputOrTarget,
}
