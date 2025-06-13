using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CreateAssetMenu(menuName = "FSM/State/EnemyIdle")]
public class IdleState : State<EnemyController>
{
    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
    }

}
