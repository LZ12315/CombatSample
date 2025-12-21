using UnityEngine;
using UnityEngine.Playables;

public class ActionDataBehavior : ActionBehaviourBase
{
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.None;

    protected override void OnClipStart(Playable playable)
    {
        base.OnClipStart(playable);

        SetActionPhase(actionPhase);
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);

        SetActionPhase(Enums.ActionPhase.Neutral);
    }

    void SetActionPhase(Enums.ActionPhase phase)
    {
        if(actor.actionPlayer.CurrentAction == null) return;

        actor.actionPlayer.CurrentAction.UpdatePhase(phase);
    }

}