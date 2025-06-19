using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/IDEvent", fileName = "EventTransition")]
public class EventTrans_Enemy : Transition<EnemyController>
{
    [SerializeField] protected string eventName;
    [SerializeField] private bool eventTriggered = false;

    private string eventString;

    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);

        eventString = _owner.Info.ID + eventName;

        EventCenter.Instance.AddEventListener(eventString, GetEventCallBack);
    }

    public override bool ToTransition()
    {
        base.ToTransition();

        return eventTriggered;
    }

    protected virtual void GetEventCallBack()
    {
        eventTriggered = true;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        eventTriggered = false;
        EventCenter.Instance.RemoveEventListener(eventString, GetEventCallBack);
        eventString = null;
    }

}
