using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputSequenceCondition : TransitionCondition
{
    [Header(" Ù–‘")]
    public float waitTime = 0.2f;
    public List<InputCheckWrapper> inputChecks;

    protected override void OnEnable()
    {

    }

    protected override bool OnCheck()
    {
        return true;
    }

    protected override void OnDisable()
    {

    }

}
