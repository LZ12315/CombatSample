using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : State<EnemyController>
{
    [SerializeField] private float attackDistance = 1f;

    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);
        _owner.NavAgent.stoppingDistance = attackDistance;
    }

    public override void OnUpdate()
    {
        _owner.NavAgent.SetDestination(_owner.Target.position);

        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target <= attackDistance + 0.03f)
        {
            _owner.MeleeAttacker.TryAttack();
        }
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}
