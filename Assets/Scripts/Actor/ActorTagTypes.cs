public enum ActorTagContainerType
{
    Transient = 0,
    Persistent = 1,
}

public enum ActorTagMatchMode
{
    Fuzzy = 0,
    Exact = 1,
}

/// <summary>Timeline Cleanup：清理 Tag 容器的方式（与 TagCondition 的 Fuzzy 含义不同，见各模式说明）。</summary>
public enum ActionTagCleanupMode
{
    /// <summary>清空所选容器的全部 Tag。</summary>
    All = 0,
    /// <summary>对列表中每个 Tag 使用 Actor.RemoveTag（层级计数递减，与 ActionTagBehaviour 退出一致）。</summary>
    SpecifiedExact = 1,
    /// <summary>对列表中每个 Tag 使用 TagContainer.RemoveTagCompletely：移除该节点及容器内所有子代 Tag。</summary>
    SpecifiedFuzzy = 2,
}
