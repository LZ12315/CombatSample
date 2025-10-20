using UnityEngine;
using UnityEngine.Playables;

public class ActionPhaseAsset : PlayableAsset
{
    [Header("Phase…Ë÷√")]
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.Neutral;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionPhaseClip>.Create(graph);
        ActionPhaseClip clip = playable.GetBehaviour();

        clip.actionPhase = actionPhase;

        return playable;
    }

}

public class ActionPhaseClip : ActionClipBase
{
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.Neutral;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);

        if (actionAsset != null)
            actionAsset.actionAssetData.phase = actionPhase;
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        if (actionAsset != null)
            actionAsset.actionAssetData.phase = Enums.ActionPhase.Neutral;
    }
}