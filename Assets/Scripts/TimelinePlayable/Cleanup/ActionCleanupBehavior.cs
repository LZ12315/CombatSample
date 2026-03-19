using UnityEngine;
using UnityEngine.Playables;

public class ActionCleanupBehavior : ActionBehaviourBase
{
    public Enums.CleanupTarget cleanupOnStart = Enums.CleanupTarget.None;
    public Enums.CleanupTarget cleanupOnEndNormal = Enums.CleanupTarget.None;
    public Enums.CleanupTarget cleanupOnEndInterrupt = Enums.CleanupTarget.None;

    protected override void OnClipStart(Playable playable)
    {
        ExecuteCleanup(cleanupOnStart);
    }

    protected override void OnClipStop(bool isNormal)
    {
        // isNormal = true 表示 Clip 正常播完
        // isNormal = false 表示被强制中断
        var target = isNormal ? cleanupOnEndNormal : cleanupOnEndInterrupt;
        ExecuteCleanup(target);
    }

    private void ExecuteCleanup(Enums.CleanupTarget target)
    {
        if (target == Enums.CleanupTarget.None) return;

        if ((target & Enums.CleanupTarget.Input) != 0)
            actor?.logicInput?.ClearBuffer();

        if ((target & Enums.CleanupTarget.Tags) != 0)
            actor?.tagContainer?.ClearTags();
    }
}
