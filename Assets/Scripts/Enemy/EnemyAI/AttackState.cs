using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : State<EnemyController>
{
    [SerializeField] private float attackDistance = 1f;
    private bool isAttack = false;

    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);
        _owner.NavAgent.stoppingDistance = attackDistance;
    }

    public override void OnUpdate()
    {
        if (isAttack) return;

        _owner.NavAgent.SetDestination(_owner.Target.position);
        Vector3 velocity = _owner.NavAgent.desiredVelocity.normalized;
        float speed = _owner.NavAgent.speed;
        _owner.LocalMotion(velocity, null, speed);

        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target <= attackDistance + 0.03f)
        {
            StartCoroutine(Attack());
            _owner.LocalMotion(Vector3.zero);
        }
    }

    IEnumerator Attack()
    {
        isAttack = true;
        _owner.MeleeAttacker.TryAttack();

        yield return new WaitUntil(() => _owner.MeleeAttacker.AttackState == Utils.Enums.AttackStates.Idle);

        isAttack = false;
        _owner.ChangeState(Utils.Enums.EnemyStates.Retreat);
    }

    public override void OnExit()
    {
        base.OnExit();
        _owner.NavAgent.ResetPath();
        _owner.LocalMotion(Vector3.zero);
    }

}
