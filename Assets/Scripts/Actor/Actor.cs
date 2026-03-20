using System.Collections.Generic;
using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects; // 必须引入这个命名空间，才能使用 GetTagContainer() 扩展方法
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Actor : MonoBehaviour
{
    public CharacterController characterController;
    public ActorLogicInput logicInput;
    public ActionStateManager actionManager;
    public ActorLocomotion locomotion;
    public ActorMovement movement;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;
    public TagContainer tagContainer; //插件原生的黑板容器

    [Header("Combat Anchors")]
    [SerializeField] private List<CombatAnchorEntry> _combatAnchors = new List<CombatAnchorEntry>();

    private Dictionary<CombatAnchorId, Transform> _combatAnchorCache;

    private void Awake()
    {
        // 为这个 GameObject 绑定或获取一个黑板实例
        tagContainer = this.gameObject.GetTagContainer();
        BuildCombatAnchorCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            BuildCombatAnchorCache();
    }
#endif

    private void BuildCombatAnchorCache()
    {
        _combatAnchorCache = new Dictionary<CombatAnchorId, Transform>();
        if (_combatAnchors == null) return;

        var seen = new HashSet<CombatAnchorId>();
        foreach (var entry in _combatAnchors)
        {
            if (seen.Contains(entry.id))
            {
                Debug.LogWarning($"[Actor] Duplicate CombatAnchorId '{entry.id}' on {name}, ignoring duplicate entry.");
                continue;
            }

            seen.Add(entry.id);
            if (entry.transform != null)
                _combatAnchorCache[entry.id] = entry.transform;
        }
    }

    public bool TryGetCombatAnchor(CombatAnchorId id, out Transform t)
    {
        t = null;
        if (_combatAnchorCache == null)
            BuildCombatAnchorCache();

        if (_combatAnchorCache == null)
            return false;

        if (!_combatAnchorCache.TryGetValue(id, out t) || t == null)
        {
            t = null;
            return false;
        }

        return true;
    }
}