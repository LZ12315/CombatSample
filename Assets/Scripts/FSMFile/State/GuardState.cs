using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CreateAssetMenu(menuName = "FSM/State/Enemy/Guard",fileName ="GuardState")]
public class GuardState : State<EnemyController>
{
    private AnimateControl animControl;
    [SerializeField] private IdleActionState idleState;

    [Header("Idle参数")]
    [SerializeField] private float idleDuration;

    [Header("扫视参数")]
    [SerializeField] private float glanceMaxAngle = 60f;
    [SerializeField] private float glanceSpeed;

    [Header("注视参数")]
    [SerializeField] private float stareMaxAngle = 60f;
    [SerializeField] private int stareTimeOnce;
    [SerializeField] private float stareDuration;

    private Vector3 lookDirection;
    private IdleActionState IdleState 
    { 
        get => idleState; 
        set
        {
            EnterIdleMode(value);
            idleState = value;
        }
    }
    private float idleTimeCounter = 0;
    private Vector3 _currentLookDirection;
    private float glanceElapsedTime = 0;
    private Vector3 glanceTargetDir = Vector3.zero;
    private float stareTimeCounter = 0;
    private int stareCount = 0;

    public override void OnStateEnter(EnemyController owner)
    {
        base.OnStateEnter(owner);

        animControl = _owner.GetComponentInChildren<AnimateControl>();

        idleState = IdleActionState.None;
        idleTimeCounter = 0;
        glanceElapsedTime = 0;
        _currentLookDirection = Vector3.zero;
        glanceTargetDir = Vector3.zero;
        stareTimeCounter = 0;
        stareCount = 0;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if(animControl == null) return;

        if(IdleState == IdleActionState.None)
        {
            while(IdleState == IdleActionState.None)
                IdleState = Utils.GetRandomEnumValue<IdleActionState>();
        }
        else if(IdleState == IdleActionState.Idle)
        {
            idleTimeCounter += Time.deltaTime;
            if(idleTimeCounter >= idleDuration)
                IdleState = IdleActionState.None;
        }
        else if(IdleState == IdleActionState.Glance)
        {
            glanceElapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(glanceElapsedTime / (glanceMaxAngle / glanceSpeed));

            // 使用缓动曲线 + sphereical插值
            float easedProgress = Mathf.SmoothStep(0, 1, progress);
            _currentLookDirection = Vector3.Slerp(
                animControl.OriginLookDirection,
                glanceTargetDir,
                easedProgress
            );

            lookDirection = _currentLookDirection;

            // 使用角度差判断完成状态（避免浮点误差问题）
            if (Vector3.Angle(lookDirection, glanceTargetDir) < 0.1f)
                IdleState = IdleActionState.None;
        }
        else if(IdleState == IdleActionState.Stare)
        {
            if (stareCount == stareTimeOnce)
                IdleState = IdleActionState.None;

            stareTimeCounter += Time.deltaTime;
            if(stareTimeCounter >= stareDuration)
            {
                stareCount++;
                stareTimeCounter = 0;
                lookDirection = GenerateHorizontalGaze(animControl.OriginLookDirection, stareMaxAngle);
            }
        }

        animControl.LookAtTargetPosition = animControl.Head.position + lookDirection * 5f;
    }

    void EnterIdleMode(IdleActionState state)
    {
        switch (state) 
        {
            case IdleActionState.Idle:
                idleTimeCounter = 0;
                animControl.Looking = false;
                break;
            case IdleActionState.Glance:
                animControl.Looking = true;
                _currentLookDirection = Vector3.zero;
                glanceElapsedTime = 0;
                glanceTargetDir = GenerateHorizontalGaze(
                    animControl.OriginLookDirection,
                    glanceMaxAngle
                );
                break;
            case IdleActionState.Stare:
                stareCount = 0;
                stareTimeCounter = 0;
                animControl.Looking = true;
                lookDirection = GenerateHorizontalGaze(
                    animControl.OriginLookDirection, // 这里同理
                    stareMaxAngle
                );
                break;
        }
    }

    Vector3 GenerateHorizontalGaze(Vector3 baseDir, float maxAngle)
    {
        Vector3 horizontalDir = new Vector3(baseDir.x, 0f, baseDir.z);
        if (horizontalDir.magnitude < 0.0001f) // 处理全垂直方向
            horizontalDir = Vector3.forward;
        horizontalDir.Normalize();

        var t = Vector3.Cross(Vector3.forward, horizontalDir).y; // 用于角度规范

        // 步骤2：真正安全的随机角度生成
        int randomDir = UnityEngine.Random.Range(-1, 1);
        float randomAngle = UnityEngine.Random.Range(maxAngle/2, maxAngle) * Mathf.Sign(t) * Math.Sign(randomDir);

        // 步骤3：应用旋转并疾病分规范化
        Quaternion rotation = Quaternion.AngleAxis(randomAngle, Vector3.up);

        return rotation * horizontalDir;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
    }

    private enum IdleActionState
    {
        None,Idle ,Glance, Stare
    }

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
