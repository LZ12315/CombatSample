using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Transition<T> : FSMNode<T,Transition<T>>
{
    public override void OnStateEnter(T owner)
    {
        _owner = owner;

        base.OnStateEnter(owner);
    }

    public virtual bool ToTransition()
    {
        if (_owner == null) 
            return false;
        else 
            return true;
    }

}

