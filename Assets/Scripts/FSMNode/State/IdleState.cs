using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CreateAssetMenu(menuName = "FSM/State/EnemyIdle")]
public class IdleState : State<EnemyController>
{
    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
