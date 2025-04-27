using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : State<EnemyController>
{
    [SerializeField] private float attackDistance = 1f;

    public override void OnEnter(EnemyController owner)
    {
        Debug.Log("Entered Attack State");
        base.OnEnter(owner);
    }

    public override void OnUpdate()
    {
        _owner.NavMeshAgent.SetDestination(_owner.Target.position);

        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target <= attackDistance)
            _owner.MeleeAttacker.TryAttack();
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
