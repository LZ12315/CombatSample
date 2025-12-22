using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;

public class InputCheckHandler
{
    public List<InputCheckBase> inputChecks;
    public Enums.ActionPriority priority = Enums.ActionPriority.Normal;
    public float waitTime = 0;
    public bool useBuffer =false;

    public float waitCounter = 0;
    public int checkIndex = 0;

    public InputCheckHandler(float waitTime, List<InputCheckBase> inputChecks, Enums.ActionPriority priority)
    {
        this.waitTime = waitTime;
        this.inputChecks = inputChecks;
        this.priority = priority;
    }

    public bool Matches(InputData inputData)
    {
        if(checkIndex >= inputChecks.Count) return true;

        bool isMatch = inputChecks[checkIndex].CheckInput(inputData);
        return isMatch;
    }

    public bool IsLast => checkIndex >= inputChecks.Count;

}
