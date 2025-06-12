using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface FSMUser<T> where T : class
{
    public T User { get;}
}
