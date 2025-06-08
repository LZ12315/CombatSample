using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class CombatMoveState : State<EnemyController>
{
    [Header("待机状态")]
    [SerializeField] private Vector2 idleTimeRange = new Vector2(2,5);
    [Header("追逐状态")]
    [SerializeField] private float distanceToIdle = 2.5f;
    [SerializeField] private float adjustChaseDistance = 1f; //防止角色在Chase与其他状态间反复切换导致不自然行为
    [Header("环绕状态")]
    [SerializeField] private Vector2 circleTimeRange = new Vector2(3, 6);
    [SerializeField] private float circlingSpeed = 20f;
    [Header("攻击切换")]
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

        _owner.NavAgent.updatePosition = false;
        _owner.NavAgent.updateRotation = false;
        _owner.NavAgent.angularSpeed = 120f;
        _owner.NavAgent.acceleration = 40f;
    }

    public override void OnUpdate()
    {
        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target >= distanceToIdle + adjustChaseDistance && combatState!= Utils.Enums.AICombatStates.Chase)
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

            var vecToTarget = _owner.Target.transform.position - _owner.transform.position;
            _owner.LocalRotation(vecToTarget);
        }
        else if(combatState == Utils.Enums.AICombatStates.Chase)
        {
            _owner.NavAgent.SetDestination(_owner.Target.position);
            Vector3 velocity =  _owner.NavAgent.desiredVelocity.normalized;
            float speed = _owner.NavAgent.speed;
            _owner.LocalMotion(velocity, null,speed);

            if (distance_Target <= distanceToIdle - 0.03f) // 同样作为状态切换的过渡值
            {
                ToIdle();
                return;
            }
        }

        if(stateTimer >= 0)
            stateTimer -= Time.deltaTime;

        if (attackWaitTimer < attackWaitTime)
            attackWaitTimer += Time.deltaTime;
        else if(EnemyManager.Instance != null)
            EnemyManager.Instance.EnemyTryAttack(_owner.Info);

        //每帧强制将NavAgent位置同步到角色位置 否则出现偏移
        _owner.NavAgent.nextPosition = _owner.transform.position;
    }

    //将NavAgent.Move的相关逻辑放到FixedUpdate中
    //更大的时间间隔有利于避障路线计算
    public override void OnFixedUpdate()
    {
        //强制更新NavAgent的同时更新前一步位置 用于之后Circling计算速度 这两步必须放在一起
        _owner.NavAgent.nextPosition = _owner.transform.position;
        lastPosition = _owner.NavAgent.nextPosition;

        if (combatState == Utils.Enums.AICombatStates.Circling)
        {
            if (stateTimer <= 0)
            {
                stateTimer = 0;
                ToIdle();
                return;
            }

            var vecToTarget = _owner.transform.position - _owner.Target.transform.position;
            var rotatePos = Quaternion.Euler(0, circlingSpeed * circlingDir * Time.fixedDeltaTime, 0) * vecToTarget;

            _owner.NavAgent.Move(rotatePos - vecToTarget);
            targetPosition = _owner.NavAgent.nextPosition;

            Vector3 velocity = ((_owner.NavAgent.nextPosition - lastPosition) / Time.deltaTime);
            _owner.LocalMotion(-rotatePos, velocity.normalized, _owner.NavAgent.speed);
        }
    }

    void ToIdle()
    {
        combatState = Utils.Enums.AICombatStates.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        _owner.NavAgent.ResetPath();
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
        _owner.LocalMotion(Vector3.zero);

        stateTimer = Random.Range(circleTimeRange.x, circleTimeRange.y);
        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

        combatState = Utils.Enums.AICombatStates.Circling;
        _owner.Animator.SetBool("combatMode", false);
    }

    void ToAttack()
    {
        _owner.ChangeState(Utils.Enums.EnemyStates.Attack);
    }

    public override void OnExit()
    {
        stateTimer = 0;
        _owner.NavAgent.ResetPath();
        _owner.LocalMotion(Vector3.zero);
        _owner.Animator.SetBool("combatMode", false);

        base.OnExit();
    }

    Vector3 targetPosition = Vector3.zero;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        float rayLength = 4f;

        if(_owner != null)
        {
            Vector3 direction = (targetPosition - _owner.transform.position).normalized;
            direction.y = 0;

            Gizmos.DrawRay(_owner.transform.position, direction * rayLength);

            Gizmos.DrawWireSphere(targetPosition, 0.5f);
        }
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