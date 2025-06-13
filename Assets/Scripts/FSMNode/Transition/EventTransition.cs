using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventTransition<T> : Transition<T>
{
    [SerializeField] private string eventName;
    private bool eventTriggered = false;

    public override void OnInit()
    {
        base.OnInit();

        EventCenter.Instance.AddEventListener("EventName", GetEventCallBack);
    }

    public override bool ToTransition(T owner)
    {
        base.ToTransition(owner);

        return eventTriggered;
    }

    void GetEventCallBack()
    {
        eventTriggered = true;
    }

}
