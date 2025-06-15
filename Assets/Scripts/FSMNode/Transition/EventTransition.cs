using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "FSM/Transition/Normal/Event",fileName ="EventTransition")]
public class EventTransition<T> : Transition<T>
{
    [SerializeField] protected string eventName;
    private bool eventTriggered = false;

    public override void OnStateEnter(T owner)
    {
        base.OnStateEnter(owner);

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

    public override void OnStateExit()
    {
        base.OnStateExit();

        eventTriggered = false;
        EventCenter.Instance.RemoveEventListener("EventName", GetEventCallBack);
    }

}
