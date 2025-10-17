using UnityEngine;
using UnityEngine.Playables;

public class ActionTransitionAsset : PlayableAsset
{
    [Header("∆¨∂Œ…Ë÷√")]
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.Neutral;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.actionPhase = actionPhase;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.Neutral;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);

        actionAsset.ActionAssetData.phase = actionPhase;
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        actionAsset.ActionAssetData.phase = Enums.ActionPhase.None;
    }
}

public static partial class Enums
{
    public enum ActionPhase
    {
        None = 0,
        Neutral = 2,
        Startup = 4,
        Recovery = 8,
        Effect = 16,
        Charging = 32,
        OverCharge = 64
    }
}