using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;

[Serializable]
public class ActionCommand
{
    public Enums.ActionPriority priority;
    public ActionTimelineAsset actionToPlay;
    public InputSequence sequence;

    [HideInInspector] public int waitTime = 0;
    [HideInInspector] public int waitCounter = 0;
    [HideInInspector] public int checkIndex = 0;

    public ActionCommand(ActionTimelineAsset actionToPlay, InputSequence sequence, Enums.ActionPriority priority)
    {
        this.actionToPlay = actionToPlay;
        this.sequence = sequence;
        this.priority = priority;
        waitTime = sequence.waitTime;
    }

    public void Update()
    {
        if(waitCounter <= 0) return;

        waitCounter--;
        if(waitCounter == 0)
        {
            checkIndex = 0;
            waitCounter = 0;
        }
    }

    public bool Matches(InputData inputData)
    {
        bool isMatch = sequence.dataChecks[checkIndex].CheckInputData(inputData);

        return isMatch;
    }

    public bool IsLast => checkIndex == sequence.dataChecks.Count - 1;

}

public partial class Enums
{
    public enum ActionPriority
    {
        Normal,
        Combo,
        Special
    }
}
