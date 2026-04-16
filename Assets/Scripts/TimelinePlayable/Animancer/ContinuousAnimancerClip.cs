using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Animancer;

/// <summary>
/// 每帧持续注入参数的数据源枚举。
/// </summary>
public enum ContinuousParameterSource
{
    /// <summary>从 LocomotionIntent 计算（主用于 Locomotion 动作）。</summary>
    LocomotionIntent = 0,
    /// <summary>从角色实际物理速度计算（备用）。</summary>
    CharacterVelocity = 1,
}

/// <summary>
/// 播放 Animancer 动画并每帧持续注入 Mixer 参数的 Timeline Clip。
/// 适用于 Locomotion 等需要持续响应外部数据驱动动画混合的场景。
/// </summary>
[System.Serializable]
public class ContinuousAnimancerClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("拖入 ClipTransition / MixerTransition2D / LinearMixerTransition 等 TransitionAsset。")]
    public TransitionAsset transitionAsset;

    [Tooltip("每帧参数注入的数据源。")]
    public ContinuousParameterSource parameterSource = ContinuousParameterSource.LocomotionIntent;

    public ClipCaps clipCaps => ClipCaps.SpeedMultiplier;

    public override double duration
    {
        get
        {
            double resolvedDuration = AnimancerTransitionUtility.GetDuration(transitionAsset);
            return resolvedDuration > 0d ? resolvedDuration : base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ContinuousAnimancerBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.transitionAsset = this.transitionAsset;
        behaviour.parameterSource = this.parameterSource;

        return playable;
    }
}
