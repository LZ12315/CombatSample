using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputStateCondition : TransitionCondition
{
    [Header(" Ù–‘")]
    public InputCheckWrapper inputCheck;

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

    public override TransitionCondition Clone()
    {
        throw new NotImplementedException();
    }
}