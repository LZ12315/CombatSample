using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CombatMoveState : State<EnemyController>
{
    [SerializeField] private float distanceToStop = 2.5f;
    [SerializeField] private float adjustChaseDistance = 1f;
    [SerializeField] private Vector2 idleTimeRange = new Vector2(2,5);
    [SerializeField] private Vector2 circleTimeRange = new Vector2(3, 6);
    [SerializeField] private float circlingSpeed = 20f;

    Utils.Enums.AICombatStates combatState;
    float timer = 0;

    public override void OnEnter(EnemyController owner)
    {
        base.OnEnter(owner);

        _owner.NavMeshAgent.stoppingDistance = distanceToStop;
        owner.NavMeshAgent.angularSpeed = 120f;
        owner.NavMeshAgent.acceleration = 40f;
    }

    public override void OnUpdate()
    {
        float distance_Target = Vector3.Distance(_owner.Target.position, _owner.transform.position);

        if (distance_Target >= distanceToStop + adjustChaseDistance)
            ToChase();

        if(combatState == Utils.Enums.AICombatStates.Idle)
        {
            if(timer <= 0)
            {
                timer = 0;
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
            if (timer <= 0)
            {
                timer = 0;
                ToIdle();
                return;
            }

            transform.RotateAround(_owner.Target.transform.position, Vector3.up, circlingSpeed * circlingDir * Time.deltaTime);
        }

        if(timer >= 0)
            timer -= Time.deltaTime;

        float currentSpeed = _owner.NavMeshAgent.velocity.magnitude;
        _owner.animator.SetFloat("motionBlend", currentSpeed / _owner.NavMeshAgent.speed);
    }

    public override void OnExit()
    {
        base.OnExit();
    }

    void ToIdle()
    {
        combatState = Utils.Enums.AICombatStates.Idle;
        timer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        _owner.animator.SetBool("combatMode", true);
        _owner.animator.SetBool("circling", false);
    }

    private void ToChase()
    {
        combatState = Utils.Enums.AICombatStates.Chase;
        _owner.animator.SetBool("combatMode", false);
        _owner.animator.SetBool("circling", false);
    }

    int circlingDir = 1;
    void ToCircling()
    {
        combatState = Utils.Enums.AICombatStates.Circling;
        timer = Random.Range(circleTimeRange.x, circleTimeRange.y);
        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

        _owner.animator.SetBool("circling", true);
        _owner.animator.SetFloat("circlingDir", circlingDir);
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