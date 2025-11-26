using UnityEngine;
using UnityEngine.Playables;

public class ActionPhaseAsset : PlayableAsset
{
    [Header("Phase…Ë÷√")]
    public Enums.ActionPhase actionPhase_Start = Enums.ActionPhase.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionPhaseClip>.Create(graph);
        ActionPhaseClip clip = playable.GetBehaviour();

        clip.actionPhase_Start = actionPhase_Start;

        return playable;
    }

}

public class ActionPhaseClip : ActionBehaviourBase
{
    public Enums.ActionPhase actionPhase_Start = Enums.ActionPhase.None;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);

        if (actionAsset != null)
            actionAsset.ActionAssetData.phase = actionPhase_Start;
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        if (actionAsset != null)
            actionAsset.ActionAssetData.phase = Enums.ActionPhase.Neutral;
    }
}