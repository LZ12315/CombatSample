using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline Clip Asset —— 持续型速度位移 Clip。
/// 
/// 与 ActionImpulseClip 的核心区别：
///   - ImpulseClip：OnClipStart 一次性注入冲量，之后由 drag / gravity 自然衰减
///   - VelocityClip：OnClipUpdate 每帧覆盖声明轴的速度，Clip 结束释放 owner
/// 
/// 选哪一个？
///   - 需要"撞击感 / 惯性尾巴"（如击退、闪避、冲刺结尾滑行）→ ImpulseClip
///   - 需要"Clip 期间完全由数值接管位移 / 曲线塑形"（如吹飞抛物线、浮空、滑行）→ VelocityClip
/// 
/// Config 由自定义 Inspector (ActionVelocityClipInspector) 平铺绘制。
/// </summary>
[System.Serializable]
public class ActionVelocityClip : PlayableAsset
{
    public VelocityConfig config = new VelocityConfig();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionVelocityBehavior>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.config = config;
        return playable;
    }
}
