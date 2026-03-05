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

    protected override void OnClipStop(bool isNormal)
    {
        base.OnClipStop(isNormal);

        SetActionPhase(Enums.ActionPhase.Neutral);
    }

    void SetActionPhase(Enums.ActionPhase phase)
    {
        if(actor.actionPlayer.CurrentAction == null) return;

        actor.actionPlayer.CurrentAction.UpdatePhase(phase);
    }

}