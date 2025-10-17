using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using CombatSample.Input;
using System;

public class InputCheckHandler
{
    public List<InputCheckWrapper> inputChecks;

    [HideInInspector] public int waitFrame = 0;
    [HideInInspector] public int waitCounter = 0;
    [HideInInspector] public int checkIndex = 0;

    public InputCheckHandler(int waitFrame, List<InputCheckWrapper> inputChecks)
    {
        this.inputChecks = inputChecks;
        this.waitFrame = waitFrame;
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

    public void Advance()
    {
        checkIndex++;
        waitCounter = waitFrame;
    }

    public bool Matches(InputData inputData)
    {
        bool isMatch = inputChecks[checkIndex].CheckInputData(inputData);

        return isMatch;
    }

    public bool IsLast => checkIndex == inputChecks.Count - 1;

}
