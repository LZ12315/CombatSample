using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline Clip 资产：持有 ImpulseConfig，创建 ActionImpulseBehavior。
/// 放在受击 Action 的 Timeline 中，驱动角色的击飞/浮空/弹跳位移。
/// </summary>
[System.Serializable]
public class ActionImpulseClip : PlayableAsset
{
    [Header("Config")]
    public ImpulseConfig config = new ImpulseConfig();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionImpulseBehavior>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.config = config;
        return playable;
    }
}
