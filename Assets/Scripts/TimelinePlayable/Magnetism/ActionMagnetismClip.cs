using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionMagnetismClip : PlayableAsset
{
    [Header("吸附模式")]
    [Tooltip("Instant: Clip开始时计算一次位移\nContinuous: Clip持续期间每帧执行吸附")]
    public MagnetismMode mode = MagnetismMode.Instant;

    [Header("目标")]
    [Tooltip("是否吸附向战斗目标 (ActorCombater.CombatTarget)")]
    public bool useCombatTarget = true;

    [Tooltip("备用目标 (useCombatTarget=false时使用)")]
    public Transform customTarget;

    [Header("吸附参数")]
    [Tooltip("最大吸附距离 (米). 超过此距离不吸附. 0=无限制")]
    public float maxDistance = 3f;

    [Tooltip("吸附速度 (米/秒). Instant模式为总位移, Continuous模式为每秒位移")]
    public float magnetSpeed = 8f;

    [Header("旋转")]
    [Tooltip("吸附时是否旋转朝向目标")]
    public bool rotateToTarget = true;

    [Tooltip("旋转速度 (度/秒). 0=瞬转")]
    public float rotateSpeed = 360f;

    [Header("Debug")]
    [Tooltip("输出吸附移动/旋转的调试日志，用于定位“距离生效但旋转不生效”的原因")]
    public bool debugLog = true;

    public enum MagnetismMode
    {
        Instant,   // Clip开始时一次性吸附
        Continuous // Clip持续期间持续吸附
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionMagnetismBehavior>.Create(graph);
        ActionMagnetismBehavior behaviour = playable.GetBehaviour();

        behaviour.mode = mode;
        behaviour.useCombatTarget = useCombatTarget;
        behaviour.customTarget = customTarget;
        behaviour.maxDistance = maxDistance;
        behaviour.magnetSpeed = magnetSpeed;
        behaviour.rotateToTarget = rotateToTarget;
        behaviour.rotateSpeed = rotateSpeed;
        behaviour.debugLog = debugLog;

        return playable;
    }
}
