using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CombatMoveState : State<EnemyController>
{
    [Header("´ý»ú×´Ì¬")]
    [SerializeField] private Vector2 idleTimeRange = new Vector2(2,5);
    [Header("×·Öð×´Ì¬")]
    [SerializeField] private float distanceToStop = 2.5f;
    [SerializeField] private float adjustChaseDistance = 1f;
    [Header("»·ÈÆ×´Ì¬")]
    [SerializeField] private Vector2 circleTimeRange = new Vector2(3, 6);
    [SerializeField] private float circlingSpeed = 20f;
    [Header("¹¥»÷ÇÐ»»")]
    [SerializeField] private float attackWaitTime = 3f;
    private float attackWaitTimer = 0;

    Utils.Enums.AICombatStates combatState;
    float stateTimer = 0;

    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);

        if (EnemyManager.Instance != null)
            EnemyManager.Instance.AddEnemy(_owner.Info);
        EventCenter.Instance.AddEventListener(_owner.Info.ID + "Attack", ToAttack);

        _owner.NavAgent.stoppingDistance = distanceToStop;
        _owner.NavAgent.updatePosition = false;
        _owner.NavAgent.updateRotation = false;
        _owner.NavAgent.angularSpeed = 120f;
        _owner.NavAgent.acceleration = 40f;
    }

    public override void OnUpdate()
    {
        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target >= distanceToStop + adjustChaseDistance)
            ToChase();

        if(combatState == Utils.Enums.AICombatStates.Idle)
        {
            if (stateTimer <= 0)
            {
                stateTimer = 0;
                if (Random.Range(0, 2) == 0)
                    ToIdle();
                else
                    ToCircling();
            }
        }
        else if(combatState == Utils.Enums.AICombatStates.Chase)
        {
            _owner.NavAgent.SetDestination(_owner.Target.position);
            Vector3 velocity =  _owner.NavAgent.desiredVelocity.normalized;
            float speed = _owner.NavAgent.speed;
            _owner.LocalMotion(velocity, speed);

            if (distance_Target <= distanceToStop - 0.03f)
            {
                ToIdle();
                return;
            }
        }
        else if(combatState == Utils.Enums.AICombatStates.Circling)
        {
            if (stateTimer <= 0)
            {
                stateTimer = 0;
                ToIdle();
                return;
            }
            var vecToTarget = _owner.transform.position - _owner.Target.transform.position;
            var rotatePos = Quaternion.Euler(0, circlingSpeed * circlingDir * Time.deltaTime, 0) * vecToTarget;

            _owner.NavAgent.Move(rotatePos - vecToTarget);

            Vector3 velocity = ((_owner.NavAgent.nextPosition - lastPosition) / Time.deltaTime);
            _owner.LocalMotion(velocity.normalized, _owner.NavAgent.speed);
            _owner.transform.rotation = Quaternion.LookRotation(-rotatePos);
            _owner.NavAgent.nextPosition = _owner.transform.position;
            lastPosition = _owner.NavAgent.nextPosition;
        }

        if(stateTimer >= 0)
            stateTimer -= Time.deltaTime;

        if (attackWaitTimer < attackWaitTime)
            attackWaitTimer += Time.deltaTime;
        else
            EventCenter.Instance.EventTrigger<EnemyInfo>("EnemyTryAttack", _owner.Info);
    }

    public override void OnExit()
    {
        stateTimer = 0;

        if (EnemyManager.Instance != null)
            EnemyManager.Instance.RemoveEnemy(_owner.Info);

        base.OnExit();
    }

    void ToIdle()
    {
        combatState = Utils.Enums.AICombatStates.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        _owner.LocalMotion(Vector3.zero);

        _owner.Animator.SetBool("combatMode", true);
    }

    private void ToChase()
    {
        combatState = Utils.Enums.AICombatStates.Chase;
        _owner.Animator.SetBool("combatMode", true);
    }

    int circlingDir = 1;
    Vector3 lastPosition = Vector3.zero;
    void ToCircling()
    {
        _owner.NavAgent.ResetPath();

        float circlingDuration = Random.Range(circleTimeRange.x, circleTimeRange.y);
        stateTimer = circlingDuration;
        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

        combatState = Utils.Enums.AICombatStates.Circling;
        _owner.Animator.SetBool("combatMode", false);
    }

    void ToAttack()
    {
        _owner.ChangeState(Utils.Enums.EnemyStates.Attack);
    }

}

public static partial class Utils
{
    public static partial class Enums
    {
        public enum AICombatStates
        {
            None, Idle, Chase, Circling
        }
    }
}