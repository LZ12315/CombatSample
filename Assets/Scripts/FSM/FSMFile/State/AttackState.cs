using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/State/EnemyAttack",fileName = "AttackState")]
public class AttackState : State<EnemyController>
{
    [SerializeField] private float attackDistance = 1.8f;
    private bool isAttack = false;
    float stoppingDistance = 0;

    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);
        isAttack = false;

        stoppingDistance = _owner.NavAgent.stoppingDistance;
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
            _owner.StartCoroutine(Attack());
            _owner.LocalMotion(Vector3.zero);
        }
    }

    IEnumerator Attack()
    {
        isAttack = true;
        _owner.MeleeAttacker.TryAttack();

        yield return new WaitUntil(() => _owner.MeleeAttacker.AttackState == Enums.AttackStates.Idle);

        isAttack = false;
        _owner.StateMachine.NextState();
    }

    public override void OnStateExit()
    {
        _owner.NavAgent.ResetPath();
        _owner.LocalMotion(Vector3.zero);
        isAttack = false;
        _owner.NavAgent.stoppingDistance = stoppingDistance;
        base.OnStateExit();
    }

}
