using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CombatMoveState : State<EnemyController>
{
    [Header("待机状态")]
    [SerializeField] private Vector2 idleTimeRange = new Vector2(2,5);
    [Header("追逐状态")]
    [SerializeField] private float distanceToStop = 2.5f;
    [SerializeField] private float adjustChaseDistance = 1f;
    [Header("环绕状态")]
    [SerializeField] private Vector2 circleTimeRange = new Vector2(3, 6);
    [SerializeField] private float circlingSpeed = 20f;
    [Header("攻击切换")]
    [SerializeField] private float attackWaitTime = 3f;

    Utils.Enums.AICombatStates combatState;
    float stateTimer = 0;
    float attackWaitTimer = 0;

    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);

        if (EnemyManager.Instance != null)
            EnemyManager.Instance.AddEnemy(_owner.Info);
        EventCenter.Instance.AddEventListener(_owner.Info.ID + "Attack", ToAttack);

        _owner.NavMeshAgent.stoppingDistance = distanceToStop;
        _owner.NavMeshAgent.angularSpeed = 120f;
        _owner.NavMeshAgent.acceleration = 40f;
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
            if (distance_Target <= distanceToStop + 0.03f)
            {
                ToIdle();
                return;
            }

            _owner.NavMeshAgent.SetDestination(_owner.Target.position);
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

            _owner.NavMeshAgent.Move(rotatePos - vecToTarget); // 无条件移动，而SetDestination会在所处位置与目标位置较近时不运行
            _owner.transform.rotation = Quaternion.LookRotation(-rotatePos);

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

        _owner.Animator.SetBool("combatMode", true);
    }

    private void ToChase()
    {
        combatState = Utils.Enums.AICombatStates.Chase;
        _owner.Animator.SetBool("combatMode", true);
    }

    int circlingDir = 1;
    void ToCircling()
    {
        _owner.NavMeshAgent.ResetPath();

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