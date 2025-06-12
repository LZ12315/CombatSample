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

    public override void OnUpdate()
    {
        foreach (var target in _owner.detectTarget)
        {
            Vector3 dir = (target.transform.position - _owner.transform.position).normalized;
            float angle = Vector3.Angle(dir, _owner.transform.forward);

            if(angle <= _owner.FOV/2)
            {
                _owner.Target = target.transform;
                _owner.ChangeState(Utils.Enums.EnemyStates.CombatMove);
                break;
            }
        }
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
