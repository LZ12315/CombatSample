using UnityEngine;
using UnityEngine.Playables;

public class ActionPhaseAsset : PlayableAsset
{
    [Header("Phase…Ë÷√")]
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionPhaseClip>.Create(graph);
        ActionPhaseClip clip = playable.GetBehaviour();

        clip.actionPhase = actionPhase;

        return playable;
    }

}

public class ActionPhaseClip : ActionBehaviourBase
{
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.None;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);

        SetActionPhase(actionPhase);
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        SetActionPhase(Enums.ActionPhase.Neutral);
    }

    void SetActionPhase(Enums.ActionPhase phase)
    {
        if(actor.actionPlayerDirector.CurrentAction == null) return;

        double normalizedTime = actor.actionPlayerDirector.CurrentAction.RuntimeData.normalizedTime;
        actor.actionPlayerDirector.CurrentAction.UpdateRuntimeData(normalizedTime, phase);
    }

}