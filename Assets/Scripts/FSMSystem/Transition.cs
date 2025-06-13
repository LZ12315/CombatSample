using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Transition<T> : FSMNode<T, Transition<T>>
{
    public override void OnInit()
    {
        base.OnInit();
    }

    public virtual bool ToTransition(T owner)
    {
        _owner = owner;

        if (owner == null) 
            return false;
        else 
            return true;
    }

    public override Transition<T> CreateRuntimeClone()
    {
        Transition<T> clone = Instantiate(this);
        clone.isClone = true;
        ActiveNode = clone;
        return clone;
    }

}

