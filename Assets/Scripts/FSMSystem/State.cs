using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State
{
    GameObject pOwner;

    public virtual void OnEnter(GameObject owner)
    {
        pOwner = owner;
    }

    public virtual void OnUpdate()
    {

    }

    public virtual void OnExit()
    {

    }

}
