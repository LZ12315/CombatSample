using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class EnemyController : MonoBehaviour
{
    public Animator Animator { get; private set; }
    public CharacterBody PhysicsCharacter { get; private set; }
    public Combater MeleeAttacker { get; private set; }
    public EnemyFSM StateMachine { get; private set;}
    public NavMeshAgent NavAgent { get; private set; }
    public EnemyInfo Info {  get; set; }
    public AnimateControl AnimateControl { get; private set; }

    [Header("移动设置")]
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float moveSpeed = 6f;

    [field : Header("行为设置")]
    [field:SerializeField] public Transform Target { get; set; }

    [Header("感知设置")]

    [Header("视锥参数")]
    [SerializeField] private float viewRadius = 10f; // 视锥半径（等同于视距）
    [SerializeField][Range(0, 180)] private float horizontalAngle = 60f; // 水平张角
    [SerializeField][Range(0, 90)] private float verticalAngle = 30f; // 垂直张角
    [SerializeField] private LayerMask targetMask;        // 检测目标层
    [SerializeField] private LayerMask obstacleMask;      // 阻挡层


    void Awake()
    {
        Animator = GetComponentInChildren<Animator>();
        PhysicsCharacter = GetComponent<CharacterBody>();
        NavAgent = GetComponent<NavMeshAgent>();
        MeleeAttacker = GetComponent<Combater>();
        StateMachine = GetComponent<EnemyFSM>();
        AnimateControl = GetComponentInChildren<AnimateControl>();

        IDInitialized();
    }

    private void Start()
    {
        NavAgent.updatePosition = false;
        NavAgent.updateRotation = false;
        NavAgent.angularSpeed = 120f;
        NavAgent.acceleration = 40f;
    }

    private void Update()
    {
        Animator.SetFloat("fowardSpeed", PhysicsCharacter.FowardSpeed, 0.2f, Time.deltaTime);
        Animator.SetFloat("strafeSpeed", PhysicsCharacter.StrafSpeed, 0.2f, Time.deltaTime);
    }

    #region 运动相关

    public void LocalMotion(Vector3 faceDir, Vector3? moveDir = null, float? speed = null)
    {
        if (MeleeAttacker.InAction)
        {
            PhysicsCharacter.SetVelocity(Vector3.zero);
            return;
        }

        LocalRotation(faceDir);

        Vector3 effectiveMoveDir = moveDir ?? faceDir;
        float effectiveSpeed = speed ?? moveSpeed;

        Vector3 motionStep = effectiveMoveDir * effectiveSpeed;
        Vector3 velocity = new Vector3(motionStep.x, PhysicsCharacter.Velocity.y, motionStep.z);
        PhysicsCharacter.SetVelocity(velocity);
    }


    public void LocalRotation(Vector3 faceDir)
    {
        if (faceDir.magnitude == 0) return;

        faceDir.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(faceDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    #endregion

    #region 感知相关

    public List<Transform> VisionConeCast()
    {
        // 步骤1：获取范围内所有可能目标
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            viewRadius,
            targetMask
        );

        List<Transform> validTargets = new List<Transform>();
        foreach (Collider hit in hits)
        {
            // 步骤2：三维位置校验
            Vector3 targetPos = hit.transform.position;
            if (!IsInCone(targetPos)) continue;

            // 步骤3：视线阻挡校验 
            if(!IsInSight(AnimateControl.Head.position, targetPos, hit.transform)) continue;

            validTargets.Add(hit.transform);
        }
        return validTargets;
    }

    public bool IsInSight(Vector3 startPos, Vector3 targetPos, Transform target, int sightLineNum = 1, float sightLineOffset = 0.1f)
    {
        //重点在于启用这个设置 否则LineCast会穿模
        Physics.SyncTransforms();

        bool isInSight = false;
        Vector3 sightPos = startPos;
        sightPos.y -= (sightLineNum - 1) * sightLineOffset;
        int num = sightLineNum;

        while (num > 0)
        {
            //如果射线检测到了障碍物且障碍物不为target,则target被阻挡
            //应使用Linecast 使用其他形状检测会导致穿模
            if (Physics.Linecast(sightPos, targetPos, out RaycastHit obstacleHit, obstacleMask))
                isInSight = (obstacleHit.transform == target);
            else
                isInSight = true;

            if(!isInSight)
            {
                //绘制阻挡示意
                Debug.DrawLine(transform.position, obstacleHit.point, Color.red, 2);
                Debug.DrawLine(obstacleHit.point, targetPos, Color.yellow, 2);
            }

            sightPos.y += sightLineOffset;
            num--;
        }

        return isInSight;
    }

    private bool IsInCone(Vector3 targetPos)
    {
        Vector3 toTargetDir = (targetPos - transform.position).normalized;
        float forwardDot = Vector3.Dot(AnimateControl.LookDirection, toTargetDir);

        // 计算水平投影点积
        float horizontalDot = Vector3.Dot(
            ProjectXZ(toTargetDir).normalized,
            ProjectXZ(AnimateControl.LookDirection).normalized
        );
        // horizontalDot ≈ cos(水平角)
        float minHorizontalDot = Mathf.Cos(horizontalAngle * 0.5f * Mathf.Deg2Rad);

        // 计算垂直投影点积
        float verticalDot = Vector3.Dot(
            toTargetDir.normalized,
            Vector3.up
        );
        // verticalDot ≈ sin(垂直角)
        float maxVerticalDot = Mathf.Sin(verticalAngle * 0.5f * Mathf.Deg2Rad);

        return horizontalDot >= minHorizontalDot
               && Mathf.Abs(verticalDot) <= maxVerticalDot;
    }

    // 扩展方法：三维向量投射到XZ平面
    Vector3 ProjectXZ(Vector3 v) => new Vector3(v.x, 0, v.z);

    void DrawAngleGizmo(float angle, Color color, bool isVertical = false)
    {
        if(AnimateControl == null) return;

        Gizmos.color = color;

        Vector3 leftDir = Quaternion.Euler(
            isVertical ? -angle / 2 : 0,
            !isVertical ? -angle / 2 : 0,
            0
        ) * AnimateControl.LookDirection * viewRadius;

        Vector3 rightDir = Quaternion.Euler(
            isVertical ? angle / 2 : 0,
            !isVertical ? angle / 2 : 0,
            0
        ) * AnimateControl.LookDirection * viewRadius;

        Gizmos.DrawLine(AnimateControl.Head.position, AnimateControl.Head.position + leftDir);
        Gizmos.DrawLine(AnimateControl.Head.position, AnimateControl.Head.position + rightDir);
    }

    #endregion

    #region 其他

    void IDInitialized()
    {
        string id = System.Guid.NewGuid().ToString();
        Info = new EnemyInfo(id, this);
    }

    void OnDrawGizmos()
    {
        // 水平角度线
        DrawAngleGizmo(horizontalAngle, Color.red);
        // 垂直角度线
        DrawAngleGizmo(verticalAngle, Color.green, true);

        // 绘制视距球体
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
    }

    #endregion

}

public static partial class Utils
{

    public static partial class Enums
    {
        public enum EnemyStates
        {
            None, Idle, CombatMove, Attack, Retreat
        }
    }
}
