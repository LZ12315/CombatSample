using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : State<EnemyController>
{
    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);
        Debug.Log("Entered Idle State");
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
