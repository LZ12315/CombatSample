using System.Collections.Generic;
using UnityEngine;
using Animancer;

/// <summary>
/// [已废弃] Locomotion 的所有职责已被 ActorMotor（意图层 + 内部换算）和 Locomotion ActionAsset 接管。
/// 保留此文件仅为避免 Prefab 序列化字段丢失，后续手动清理组件引用后可删除。
/// </summary>
[System.Obsolete("Locomotion 已统一为 ActionAsset，请使用 ActorMotor.SetLocomotionIntent() 代替。")]
public class ActorLocomotion : MonoBehaviour
{
    // ── 保留序列化字段，避免 Prefab 引用丢失 ──
    [Header("References")]
    [SerializeField] private Actor _actor;

    [Header("Settings")]
    [SerializeField] private float _baseMoveSpeed = 5f;

    [SerializeReference, SubclassSelector]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeField] private LocomotionModeAsset _defaultMode;
    [SerializeField] private List<LocomotionModeAsset> _specialModes = new List<LocomotionModeAsset>();
}
