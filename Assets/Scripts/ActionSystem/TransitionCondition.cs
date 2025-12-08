using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class TransitionCondition
{
    protected Actor actor;
    bool enabled = false;

    protected virtual void OnEnable() { }

    protected virtual bool OnCheck() { return true; }

    protected virtual void OnDisable() { }

    public abstract TransitionCondition Clone();

    #region 公开方法

    public void Enable(Actor actor)
    {
        if(actor == null) return;

        this.actor = actor;
        enabled = true;
        OnEnable();
    }

    public bool Check()
    {
        if(!enabled) return false;

        return OnCheck();
    }

    public void Disable()
    {
        enabled = false;
        OnDisable();
        actor = null;
    }

    #endregion

}
