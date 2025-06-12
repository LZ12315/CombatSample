using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Transition<T> : ScriptableObject
{
    protected T _owner;

    public virtual bool ToTransition(T owner)
    {
        _owner = owner;

        if(_owner == null) 
            return false;
        else 
            return true;
    }
}

