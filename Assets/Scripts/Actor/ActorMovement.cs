using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorMovement : MonoBehaviour
{
    public Actor actor;
    public Animator animator;
    [SerializeField] private float rotateSpeed = 500f;

    Quaternion rotation = Quaternion.identity;
    Vector3 gravityVelocity = Vector3.zero;

    public void UpdateTurn(Vector3 direction)
    {
        //if(direction.sqrMagnitude < 0.01f) return;

        //Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        //rotation = Quaternion.RotateTowards(rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    private void Update()
    {
        transform.rotation = rotation;
    }

    private float _yPositionDeadZone = 0.5f; // 死区阈值（单位：米）
    private float _accumulatedYDelta; // 累积的Y轴变化量
    private Vector3 _lastPosition; // 上一帧位置
    private void OnAnimatorMove()
    {
        Vector3 deltaPos = animator.deltaPosition;
        Quaternion deltaRot = animator.deltaRotation;

        // 计算当前帧的Y轴变化
        float currentYDelta = deltaPos.y;

        // 死区处理：累积微小变化，直到超过阈值
        if (Mathf.Abs(currentYDelta) < _yPositionDeadZone)
        {
            // 累积微小变化
            _accumulatedYDelta += currentYDelta;

            // 检查累积值是否超过阈值
            if (Mathf.Abs(_accumulatedYDelta) >= _yPositionDeadZone)
            {
                // 应用累积的变化（保留符号）
                deltaPos.y = _accumulatedYDelta;
                _accumulatedYDelta = 0;
            }
            else
            {
                // 忽略此帧的Y轴变化
                deltaPos.y = 0;
            }
        }
        else
        {
            // 变化量超过阈值，直接应用
            _accumulatedYDelta = 0; // 重置累积值
        }

        // 应用旋转（修正为使用localRotation）
        transform.localRotation = deltaRot * transform.rotation;

        // 移动角色控制器
        actor.characterController.Move(deltaPos);

        // 记录位置用于调试
        _lastPosition = transform.position;
    }


    void PerformGravity()
    {
        if (actor.characterController.isGrounded)
            gravityVelocity = Vector3.zero;
        else
        {
            gravityVelocity += Physics.gravity * Time.deltaTime;
            actor.characterController.Move(gravityVelocity * Time.deltaTime);
        }
    }

    internal void ResetRotation()
    {
        transform.localRotation = Quaternion.identity;
    }
}
