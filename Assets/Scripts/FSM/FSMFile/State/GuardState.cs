using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;


[CreateAssetMenu(menuName = "FSM/State/Enemy/Guard",fileName ="GuardState")]
public class GuardState : State<EnemyController>
{
    private EnemyAnimateControl animControl;
    [SerializeField] private IdleActionState idleState;

    [Header("感知参数")]
    [SerializeField] private float AcquisteSpeed = 10f; // Acq增加速度
    private Vector3 lookDirection;
    private IdleActionState IdleState 
    { 
        get => idleState; 
        set
        {
            EnterIdleAction(value);
            idleState = value;
        }
    }

    [Header("Idle参数")]
    [SerializeField] private float idleDuration;

    [Header("扫视参数")]
    [SerializeField] private float glanceMaxAngle = 60f;
    [SerializeField] private float glanceSpeed;

    [Header("注视参数")]
    [SerializeField] private float stareMaxAngle = 60f;
    [SerializeField] private int stareTimeOnce;
    [SerializeField] private float stareDuration;


    private float idleTimeCounter = 0;
    private float glanceElapsedTime = 0;
    private Vector3 glanceTargetDir = Vector3.zero;
    private float stareTimeCounter = 0;
    private int stareCount = 0;

    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);

        animControl = _owner.GetComponentInChildren<EnemyAnimateControl>();

        //等到副本机制实现后可以删除
        idleState = IdleActionState.None;
        idleTimeCounter = 0;
        glanceElapsedTime = 0;
        glanceTargetDir = Vector3.zero;
        stareTimeCounter = 0;
        stareCount = 0;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        IdleAction();

        foreach (var target in _owner.VisionConeCast())
        {
            PlayerCombater player = target.GetComponent<PlayerCombater>();
            if (player == null) continue;

            _owner.Target = target;
            player.Acquisition += AcquisteSpeed * Time.deltaTime;
        }
    }

    public override void OnStateExit()
    {
        animControl.IsLooking = false;
        base.OnStateExit();
    }

    #region Guard行为

    void IdleAction()
    {

        if (animControl == null) return;

        if (IdleState == IdleActionState.None)
        {
            while (IdleState == IdleActionState.None)
                IdleState = Utils.GetRandomEnumValue<IdleActionState>();
        }
        else if (IdleState == IdleActionState.Idle)
        {
            idleTimeCounter += Time.deltaTime;
            if (idleTimeCounter >= idleDuration)
                IdleState = IdleActionState.None;
        }
        else if (IdleState == IdleActionState.Glance)
        {
            glanceElapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(glanceElapsedTime / (glanceMaxAngle / glanceSpeed));

            // 使用缓动曲线 + sphereical插值
            float easedProgress = Mathf.SmoothStep(0, 1, progress);
            lookDirection = Vector3.Slerp(
                animControl.LookDirection,
                glanceTargetDir,
                easedProgress
            );

            // 使用角度差判断完成状态（避免浮点误差问题）
            if (Vector3.Angle(lookDirection, glanceTargetDir) < 0.1f)
                IdleState = IdleActionState.None;
        }
        else if (IdleState == IdleActionState.Stare)
        {
            if (stareCount == stareTimeOnce)
            {
                stareCount = 0; 
                IdleState = IdleActionState.None;
            }

            stareTimeCounter += Time.deltaTime;
            if (stareTimeCounter >= stareDuration)
            {
                stareCount++;
                stareTimeCounter = 0;
                lookDirection = RandomHorizontalGaze(animControl.transform.forward, stareMaxAngle);
            }
        }

        animControl.LookDirection = lookDirection;
    }

    void EnterIdleAction(IdleActionState state)
    {
        switch (state) 
        {
            case IdleActionState.Idle:
                idleTimeCounter = 0;
                animControl.LookDirection = animControl.Head.forward;
                animControl.IsLooking = false;
                break;
            case IdleActionState.Glance:
                animControl.IsLooking = true;
                glanceElapsedTime = 0;
                glanceTargetDir = RandomHorizontalGaze(
                    animControl.transform.forward,
                    glanceMaxAngle
                );
                break;
            case IdleActionState.Stare:
                stareCount = 0;
                stareTimeCounter = 0;
                animControl.IsLooking = true;
                lookDirection = RandomHorizontalGaze(
                    animControl.transform.forward,
                    stareMaxAngle
                );
                break;
        }
    }

    Vector3 RandomHorizontalGaze(Vector3 baseDir, float maxAngle)
    {
        Vector3 horizontalDir = new Vector3(baseDir.x, 0f, baseDir.z);
        if (horizontalDir.magnitude < 0.0001f) // 处理全垂直方向
            horizontalDir = Vector3.forward;
        horizontalDir.Normalize();

        int randomDir = UnityEngine.Random.Range(0, 2) * 2 - 1; // 生成随机方向
        var t = Vector3.Cross(Vector3.forward, horizontalDir).y; // 用于角度规范

        // 步骤2：真正安全的随机角度生成
        float randomAngle = UnityEngine.Random.Range(maxAngle/2, maxAngle) * Mathf.Sign(t) * Mathf.Sign(randomDir);

        // 步骤3：应用旋转并疾病分规范化
        Quaternion rotation = Quaternion.AngleAxis(randomAngle, Vector3.up);

        return rotation * horizontalDir;
    }

    private enum IdleActionState
    {
        None,Idle ,Glance, Stare
    }

    #endregion

}


public partial class Utils
{
    public static T GetRandomEnumValue<T>() where T : Enum
    {
        Array enumValues = Enum.GetValues(typeof(T));

        int randomIndex = UnityEngine.Random.Range(0, enumValues.Length);

        return (T)enumValues.GetValue(randomIndex);
    }
}
