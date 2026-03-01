using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputSequenceCondition : TransitionCondition
{
    [Header("????")]
    [SerializeField]
    private float waitTime = 0.4f;
    [SerializeReference, SubclassSelector]
    private List<InputCheckBase> inputChecks = new List<InputCheckBase>();

    private float waitCounter = 0;
    private int checkIndex = 0;

    protected override void OnEnable()
    {
        if(actor.logicInput != null)
            actor.logicInput.RegisterForInputEvent(this, GetInput);

        waitCounter = 0;
        checkIndex = 0;
    }

    protected override bool OnCheck()
    {
        if(waitCounter > 0)
        {
            waitCounter -= Time.deltaTime;
            if(waitCounter <= 0)
            {
                checkIndex = 0;
                waitCounter = 0;
            }
        }

        return checkIndex == inputChecks.Count;
    }

    protected override void OnDisable()
    {
        if (actor.logicInput != null)
            actor.logicInput.UnregisterFromInputEvent(this);
    }

    void GetInput(InputData input)
    {
        if(checkIndex == inputChecks.Count) return;

        if (inputChecks[checkIndex].CheckInput(input))
        {
            checkIndex++;
            waitCounter = waitTime;
        }
    }

    public override TransitionCondition Clone()
    {
        return new InputSequenceCondition
        {
            waitTime = this.waitTime,
            inputChecks = this.inputChecks
        };
    }
}
