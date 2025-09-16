using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionCommand : MonoBehaviour
{
    public ActionTimelineAsset actionToPlay;
    public InputSequence command;

    private double waitCounter = 0;
    private int checkIndex = 0;

    public ActionCommand(ActionTimelineAsset actionToPlay, InputSequence command)
    {
        this.actionToPlay = actionToPlay;
        this.command = command;
        checkIndex = 0;
        waitCounter = 0;
    }

    public void CommandUpdate(double deltaTime)
    {
        waitCounter += deltaTime;
    }

    public void GetInputData(InputData inputData)
    {

    }

    public bool IsCommandComplished()
    {
        return checkIndex == command.dataChecks.Count;
    }

}
