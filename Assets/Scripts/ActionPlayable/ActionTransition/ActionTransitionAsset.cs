using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CombatSample.Consts;

[Serializable]
public struct ActionCommandSetting
{
    public Enums.ActionPriority priority;
    public ActionTimelineAsset actionToPlay;
    public InputCheckSequence sequence;
}

public class ActionTransitionAsset : PlayableAsset
{
    [Header("Transition…Ë÷√")]
    public bool active = true;
    public Enums.ActTransType transitionType;

    [Header(" ‰»ÎºÏ≤È")]
    public List<ActionCommandSetting> actionCommandSettings = new ();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.active = active;
        clip.transitionType = transitionType;
        clip.actionCommandSettings = actionCommandSettings;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public bool active = true;
    public Enums.ActTransType transitionType;

    public List<ActionCommandSetting> actionCommandSettings;

    bool actionWaitToPlay = false;
    ActionTimelineAsset actionToPlay = null;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);
        if(!active || actor == null) return;

        foreach (var setting in actionCommandSettings)
            actor.logicInput.AddShortdatedHandler(setting.actionToPlay, setting.sequence, setting.priority);
        actor.logicInput.RegisterForTransitionEvent(this, PlayNextAction);
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);
        if (!active || actor == null) return;

        if(isNormal)
        {
            if (actionWaitToPlay && transitionType == Enums.ActTransType.TransitionEnd)
            {
                actionWaitToPlay = false;

                if (actionToPlay != null)
                    actor.actionPlayerDirector.PlayAction(actionToPlay);
            }
        }
    }

    private void PlayNextAction(ActionTimelineAsset actionToPlay)
    {
        if(!active) return;
        actionWaitToPlay = true;
        this.actionToPlay = actionToPlay;

        if (actionWaitToPlay && transitionType == Enums.ActTransType.Immediate)
        {
            actionWaitToPlay = false;

            if (actionToPlay != null)
                actor.actionPlayerDirector.PlayAction(actionToPlay);
        }
    }

    protected override void CleanUp()
    {
        base.CleanUp();

        actor.logicInput.ClearShortdatedCommand();
        actor.logicInput.UnregisterFromTransitionEvent(this);

        actionWaitToPlay = false;
        actionToPlay = null;
    }

}

public static partial class Enums
{
    public enum ActTransType
    {
        Immediate,
        TransitionEnd,
    }
}