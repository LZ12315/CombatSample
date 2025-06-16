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
            float duration = glanceMaxAngle/glanceSpeed;
            lookDirection = Vector3.Lerp(animControl.OriginLookDirection, glanceTargetDir, glanceElapsedTime / duration);

            if(lookDirection == glanceTargetDir)
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
                glanceTargetDir = GenerateHorizontalGaze(_owner.transform.forward, glanceMaxAngle);
                break;
            case IdleActionState.Stare:
                stareCount = 0;
                stareTimeCounter = 0;
                animControl.Looking = true;
                lookDirection = GenerateHorizontalGaze(_owner.transform.forward, stareMaxAngle);
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
        float randomAngle = UnityEngine.Random.Range(-maxAngle, maxAngle) * Mathf.Sign(t);

        // 步骤3：应用旋转并疾病分规范化
        Quaternion rotation = Quaternion.AngleAxis(randomAngle, Vector3.up);
        Debug.Log(rotation * horizontalDir);
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
