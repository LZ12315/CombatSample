using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/IDEvent", fileName = "EventTransition")]
public class EventTransition_Enemy : EventTransition<EnemyController>
{
    private string originName;

    public override void OnStateEnter(EnemyController owner)
    {
        originName = eventName;
        string eventString = owner.Info.ID + eventName;
        eventName = eventString;

        base.OnStateEnter(owner);
    }

    protected override void GetEventCallBack()
    {
        base.GetEventCallBack();
    }

    public override void OnStateExit()
    {
        eventName = originName;
        base.OnStateExit();
    }

}
