using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State<T> : FSMNode<T, State<T>>
{

    public override void OnStateEnter(T owner)
    {
        base.OnStateEnter(owner);
    }

    public virtual void OnUpdate()
    {

    }

    public virtual void OnFixedUpdate()
    {

    }

    public override void OnStateExit()
    {
        base .OnStateExit();
    }

}
