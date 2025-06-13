using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State<T> : FSMNode<T,State<T>>
{

    public virtual void OnEnter(T owner)
    {
        _owner = owner;
    }

    public virtual void OnUpdate()
    {

    }

    public virtual void OnFixedUpdate()
    {

    }

    public virtual void OnExit()
    {

    }

    public override State<T> CreateRuntimeClone()
    {
        State<T> clone = Instantiate(this);
        clone.isClone = true;
        ActiveNode = clone;
        return clone;
    }

}
