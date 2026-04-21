using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Actor : MonoBehaviour
{
    #region === 组件引用 ===

    public CharacterController characterController;
    public ActorLogicInput logicInput;
    public ActionStateManager actionManager;
    public ActorMovement movement;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;

    #endregion

    #region === 标签容器 ===

    public TagContainer persistentTags;
    public TagContainer transientTags;

    // Backward-compatible alias while the project migrates to dual containers.
    public TagContainer tagContainer => persistentTags;

    #endregion

    #region === 运动能力状态 ===
    // 此 region 存放"与角色运动能力相关的运行时状态计数/标记"。
    // 原则：数据留在 Actor 层（便于多处读取与条件系统访问），行为/触发由 Movement 的事件驱动。

    [Header("Jump Ability")]
    [SerializeField, Tooltip("最大跳跃次数。2 = 支持二段跳。")]
    private int _maxJumpCount = 2;

    /// <summary>已消耗的跳跃次数。落地时自动重置为 0。</summary>
    public int jumpCount { get; private set; }

    /// <summary>最大跳跃次数（面板配置）。</summary>
    public int maxJumpCount => _maxJumpCount;

    /// <summary>是否还能跳（供条件系统查询）。</summary>
    public bool CanJump() => jumpCount < _maxJumpCount;

    /// <summary>消耗一次跳跃（由 JumpAction 触发）。</summary>
    public void ConsumeJump() => jumpCount++;

    /// <summary>落地回调：重置跳跃计数。</summary>
    private void HandleLanded()
    {
        jumpCount = 0;
    }

    // 未来加 dash / airCombo 等同类能力状态时，也放在这个 region。

    #endregion

    private void Awake()
    {
        persistentTags = gameObject.GetTagContainer();
        transientTags = new TagContainer();
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.OnLanded += HandleLanded;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.OnLanded -= HandleLanded;
    }

    public TagContainer GetTagContainer(ActorTagContainerType containerType)
    {
        return containerType == ActorTagContainerType.Persistent ? persistentTags : transientTags;
    }

    public void AddTag(Tag tag, ActorTagContainerType containerType)
    {
        if (tag == null) return;
        GetTagContainer(containerType)?.AddTag(tag);
    }

    public bool RemoveTag(Tag tag, ActorTagContainerType containerType)
    {
        if (tag == null) return false;
        return GetTagContainer(containerType)?.RemoveTag(tag) ?? false;
    }

    public bool HasTag(Tag tag, ActorTagContainerType containerType, ActorTagMatchMode matchMode)
    {
        if (tag == null) return false;

        var container = GetTagContainer(containerType);
        if (container == null) return false;

        if (matchMode == ActorTagMatchMode.Fuzzy)
            return container.HasTag(tag);

        return container.HasTag(tag) && container.IsLeafTagInContainer(tag);
    }

    public void ClearTransientTags()
    {
        transientTags?.ClearTags();
    }
}
