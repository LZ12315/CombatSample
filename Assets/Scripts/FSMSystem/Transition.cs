using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Transition : ScriptableObject
{
    GameObject _owner;

    public virtual bool ToTransition()
    {
        return false;
    }
}

