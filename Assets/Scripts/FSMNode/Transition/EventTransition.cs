using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventTransition<T> : Transition<T>
{
    [SerializeField] private string eventName;
    private bool eventTriggered = false;

    public override void OnStateEnter(T _owner)
    {
        base.OnStateEnter(_owner);

        EventCenter.Instance.AddEventListener("EventName", GetEventCallBack);
    }

    public override bool ToTransition()
    {
        base.ToTransition();

        return eventTriggered;
    }

    void GetEventCallBack()
    {
        eventTriggered = true;
    }

}
