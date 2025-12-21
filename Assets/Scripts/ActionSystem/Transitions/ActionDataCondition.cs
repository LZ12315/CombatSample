using ParadoxNotion.Design;
using System;
using CombatSample.Utils;
using UnityEngine;

[Serializable]
public class ActionDataCondition : TransitionCondition
{
    [SerializeField]
    private Enums.ActionDataType actionDataType;
    [SerializeField]
    private Enums.ActionPhase requiredPhase = Enums.ActionPhase.Neutral;
    [SerializeField, SliderField(0, 1f)]
    private float requiredProgress = 0;

    protected override bool OnCheck()
    {
        bool isCorrect = false;
        ActionData actionData = actor.actionPlayer.CurrentAction.RuntimeData;

        foreach (var check in EnumUtils.GetFlags(actionDataType))
        {
            switch (check)
            {
                case Enums.ActionDataType.Phase:
                    isCorrect = EnumUtils.ContainsAny(actionData.phase, requiredPhase);
                    break;
                case Enums.ActionDataType.Progress:
                    isCorrect = (actionData.normalizedTime >= requiredProgress - 0.03f);
                    break;
            }
        }

        return isCorrect;
    }

    public override TransitionCondition Clone()
    {
        return new ActionDataCondition
        {
            actionDataType = actionDataType,
            requiredPhase = requiredPhase,
            requiredProgress = requiredProgress
        };
    }
}

public static partial class Enums
{
    [System.Flags]
    public enum ActionDataType
    {
        None = 0,
        Phase = 2,
        Progress = 4
    }
}