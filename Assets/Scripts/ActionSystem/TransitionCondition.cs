using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
abstract public class TransitionCondition
{
    protected Actor actor;

    protected virtual void OnEnable() { }

    protected virtual bool OnCheck() { return true; }

    protected virtual void OnDisable() { }

    public abstract TransitionCondition Clone();

    #region 公开方法

    public void Enable(Actor actor)
    {
        this.actor = actor;
        OnEnable();
    }

    public bool Check()
    {
        return OnCheck();
    }

    public void Disable()
    {
        actor = null;
        OnDisable();
    }

    #endregion

}
