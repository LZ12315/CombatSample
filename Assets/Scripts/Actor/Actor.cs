using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects;
using UnityEngine;

public class Actor : MonoBehaviour
{
    #region === 组件引用 ===

    public ActorMotor actorMotor;
    public ActionStateManager actionManager;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCombater combater;

    [Header("Camera")]
    [Tooltip("相机观察该 Actor 时使用的目标点。玩家通常指向 CameraPivot；敌人可指向胸口/锁定点。未配置时回退到 Actor Transform。")]
    [SerializeField] private Transform cameraTarget;
    public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;

    #endregion

    #region === 标签容器 ===

    public TagContainer persistentTags;
    public TagContainer transientTags;

    // Backward-compatible alias while the project migrates to dual containers.
    public TagContainer tagContainer => persistentTags;

    #endregion

    private void Awake()
    {
        actorMotor = actorMotor != null ? actorMotor : GetComponent<ActorMotor>();

        if (cameraTarget == null)
            cameraTarget = transform.Find("CameraPivot");

        persistentTags = gameObject.GetTagContainer();
        transientTags = new TagContainer();
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