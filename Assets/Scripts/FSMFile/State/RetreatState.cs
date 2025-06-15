using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/State/EnemyRetreat", fileName = "RetreatState")]
public class RetreatState : State<EnemyController>
{
    [SerializeField] private float backwardSpeed = 1.75f;
    Vector3 lastPosition = Vector3.zero;

    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);
        lastPosition = Vector3.zero;
        _owner.NavAgent.ResetPath();
    }

    public override void OnUpdate()
    {
        float distanceToTarget = Vector3.Distance(_owner.transform.position, _owner.Target.position);

        var vectorToTarget = _owner.Target.position - _owner.transform.position;

        _owner.NavAgent.Move(- vectorToTarget.normalized * backwardSpeed * Time.deltaTime);

        Vector3 velocity = ((_owner.NavAgent.nextPosition - lastPosition) / Time.deltaTime);
        _owner.LocalMotion(vectorToTarget, velocity.normalized, backwardSpeed);

        _owner.NavAgent.nextPosition = _owner.transform.position;
        lastPosition = _owner.NavAgent.nextPosition;
    }

    public override void OnStateExit()
    {
        _owner.NavAgent.ResetPath();
        _owner.LocalMotion(Vector3.zero);
        base.OnStateExit();
    }

}
