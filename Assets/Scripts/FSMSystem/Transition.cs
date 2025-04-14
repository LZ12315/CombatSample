using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transition
{
    GameObject pOwner;

    public virtual bool ToTransition()
    {
        return false;
    }
}

