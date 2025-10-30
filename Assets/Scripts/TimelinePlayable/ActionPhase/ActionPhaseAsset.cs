using UnityEngine;
using UnityEngine.Playables;

public class ActionPhaseAsset : PlayableAsset
{
    [Header("Phase…Ë÷√")]
    public Enums.ActionPhase actionPhase_Start = Enums.ActionPhase.Neutral;
    public Enums.ActionPhase actionPhase_End = Enums.ActionPhase.Neutral;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionPhaseClip>.Create(graph);
        ActionPhaseClip clip = playable.GetBehaviour();

        clip.actionPhase_Start = actionPhase_Start;
        clip.actionPhase_End = actionPhase_End;

        return playable;
    }

}

public class ActionPhaseClip : ActionBehaviourBase
{
    public Enums.ActionPhase actionPhase_Start = Enums.ActionPhase.Neutral;
    public Enums.ActionPhase actionPhase_End = Enums.ActionPhase.Neutral;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);

        if (actionAsset != null)
            actionAsset.actionAssetData.phase = actionPhase_Start;
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        if (actionAsset != null)
            actionAsset.actionAssetData.phase = Enums.ActionPhase.Neutral;
    }
}