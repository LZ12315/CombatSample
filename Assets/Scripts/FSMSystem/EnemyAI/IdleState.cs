using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class IdleState : State<EnemyController>
{
    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);
    }

    public override void OnUpdate()
    {
        foreach (var target in _owner.detectTarget)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(dir, transform.forward);

            if(angle <= _owner.FOV/2)
            {
                _owner.chaseTarget = target.transform;
                _owner.ChangeState(Utils.EnemtState.Chase);
                break;
            }
        }
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
