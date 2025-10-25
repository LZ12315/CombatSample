using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;

public class InputCheckHandler
{
    public List<InputCheckWrapper> inputChecks;
    public Enums.ActionPriority priority = Enums.ActionPriority.Normal;
    public float waitTime = 0;
    public bool useBuffer =false;

    public float waitCounter = 0;
    public int checkIndex = 0;

    public InputCheckHandler(float waitTime, List<InputCheckWrapper> inputChecks, Enums.ActionPriority priority, bool useBuffer)
    {
        this.waitTime = waitTime;
        this.inputChecks = inputChecks;
        this.priority = priority;
        this.useBuffer = useBuffer;
    }

    public void Update(float deltaTime)
    {
        if(waitCounter <= 0) return;

        waitCounter -= deltaTime;
        if(waitCounter <= 0)
        {
            checkIndex = 0;
            waitCounter = 0;
        }
    }

    public void Advance()
    {
        checkIndex++;
        waitCounter = waitTime;
    }

    public bool Matches(InputData inputData)
    {
        bool isMatch = inputChecks[checkIndex].CheckInputData(inputData);

        return isMatch;
    }

    public bool IsLast => checkIndex == inputChecks.Count - 1;

}

public static partial class Enums
{
    public enum ActionPriority
    {
        Normal,
        Special,
        Override
    }
}
