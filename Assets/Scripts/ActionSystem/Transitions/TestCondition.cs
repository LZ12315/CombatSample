using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TestCondition : TransitionCondition
{
    [Header("ÊôÐÔ")]
    [SerializeField]
    private int counter = 0;

    protected override void OnEnable()
    {

    }

    protected override bool OnCheck()
    {
        counter++;
        return counter > 3000;
    }

    protected override void OnDisable()
    {

    }

    public override TransitionCondition Clone()
    {
        return new TestCondition
        {
            counter = this.counter
        };
    }
}
