using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/Event", fileName = "EventTransition")]
public class EventTransition_Enemy : EventTransition<EnemyController>
{
    public override void OnStateEnter(EnemyController owner)
    {
        string eventString = owner.Info.ID + eventName;
        eventName = eventString;

        base.OnStateEnter(owner);
    }

}
