using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class ActionTransitionBehavior : ActionBehaviourBase
{
    [Header("配置")]
    [SerializeField, Tooltip("下一个动作")]
    private ActionAsset targetAction;

    [SerializeField, Tooltip("检查模式")]
    private Enums.ActionTransitionMode transitionMode;

    [SerializeReference, SubclassSelector, Tooltip("Condition列表")]
    private List<TransitionCondition> conditions;

    private bool canTransition = false;

    protected override void OnClipStart(Playable playable)
    {
        if (conditions.Count == 0) return;

        foreach (var condition in conditions)
            condition.Enable(actor);
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (conditions.Count == 0)
        {
            canTransition = true;
            return;
        }

        foreach (var condition in conditions)
        {
            if (condition != null)
            {
                canTransition = canTransition && condition.Check();
                if (canTransition && transitionMode == Enums.ActionTransitionMode.AnyTrue)
                    return;
            }
        }

        if(canTransition)
        {

        }
            actor.actionPlayer.CurrentAction
    }

    protected override void OnClipFinish(bool isNormal)
    {
        if (conditions.Count == 0) return;

        foreach (var condition in conditions)
            condition.Disable();
    }

}
