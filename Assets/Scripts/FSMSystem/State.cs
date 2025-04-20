using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State<T>:MonoBehaviour
{
    protected T _owner;

    public virtual void OnEnter(T owner)
    {
        _owner = owner;
    }

    public virtual void OnUpdate()
    {

    }

    public virtual void OnExit()
    {

    }

}
