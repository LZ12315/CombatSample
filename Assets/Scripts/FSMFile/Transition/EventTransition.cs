using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "FSM/Transition/Normal/NormalEvent",fileName ="EventTransition")]
public class EventTransition<T> : Transition<T>
{
    [SerializeField] protected string eventName;
    [SerializeField] private bool eventTriggered = false;

    public override void OnStateEnter(T owner)
    {
        base.OnStateEnter(owner);

        EventCenter.Instance.AddEventListener(eventName, GetEventCallBack);
    }

    public override bool ToTransition()
    {
        base.ToTransition();

        return eventTriggered;
    }

    protected virtual void GetEventCallBack()
    {
        Debug.Log(eventName);
        eventTriggered = true;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        eventTriggered = false;
        EventCenter.Instance.RemoveEventListener(eventName, GetEventCallBack);
    }

}
