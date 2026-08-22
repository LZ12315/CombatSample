using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects;
using UnityEngine;

public class Actor : MonoBehaviour
{
    private ActorSimulationRuntime _simulationRuntime;

    #region === 组件引用 ===

    public ActorMotor actorMotor;
    public ActionStateManager actionManager;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCombater combater;

    [Header("Animation")]
    [SerializeField] private AnimationConfig animationConfig;
    public AnimationConfig AnimationConfig => animationConfig;
    internal ActorHitBoxRuntime HitBoxes => _simulationRuntime?.HitBoxes;

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
        actionPlayer = actionPlayer != null ? actionPlayer : GetComponent<ActionPlayer>();
        actionManager = actionManager != null ? actionManager : GetComponent<ActionStateManager>();
        EnsureSimulationRuntime();

        if (cameraTarget == null)
            cameraTarget = transform.Find("CameraPivot");

        persistentTags = gameObject.GetTagContainer();
        transientTags = new TagContainer();
    }

    private void OnEnable()
    {
        EnsureSimulationRuntime();
        CombatSimulationDriver.RegisterActor(_simulationRuntime);
    }

    private void OnDisable()
    {
        if (_simulationRuntime != null)
        {
            _simulationRuntime.CancelAction();
            CombatSimulationDriver.UnregisterActor(_simulationRuntime);
        }

        // Actor can be disabled independently from ActionPlayer. Do not leave a
        // fixed Sequence session alive after its simulation entry is removed.
        if (actionPlayer != null && actionPlayer.isActiveAndEnabled)
            actionPlayer.StopAction();
    }

    private void OnDestroy()
    {
        if (_simulationRuntime != null)
            CombatSimulationDriver.UnregisterActor(_simulationRuntime);
    }

    private void EnsureSimulationRuntime()
    {
        if (_simulationRuntime == null)
            _simulationRuntime = new ActorSimulationRuntime(this);
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
