using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : State<EnemyController>
{
    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);

        _owner.NavMeshAgent.stoppingDistance = 2f;
        owner.NavMeshAgent.angularSpeed = 720f;
    }

    public override void OnUpdate()
    {
        _owner.NavMeshAgent.SetDestination(_owner.chaseTarget.position);

        float currentSpeed = _owner.NavMeshAgent.velocity.magnitude;
        _owner.animator.SetFloat("motionBlend", currentSpeed / _owner.NavMeshAgent.speed);
    }

    public override void OnExit()
    {
        base.OnExit();
    }
}
