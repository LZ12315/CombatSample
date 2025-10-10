using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State<T> : FSMNode<T, State<T>>
{

    public virtual void OnUpdate()
    {

    }

    public virtual void OnFixedUpdate()
    {

    }

}
