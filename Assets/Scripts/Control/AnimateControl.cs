using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Controls;

public class AnimateControl : MonoBehaviour
{
    private Animator animator;

    [field: Header("注视设置")]
    [field: SerializeField] public Transform Head { get; private set; }
    [SerializeField] private bool horizontalOnly = true;
    [SerializeField] private float lookAtCoolTime = 0.8f;
    [SerializeField] private float lookAtHeatTime = 0.5f;

    public bool IsLooking {  get; set; }
    public Vector3 LookDirection
    {
        get
        {
            if (Head == null || lookAtPosition == null) return Vector3.forward;
            return (lookAtPosition - Head.transform.position).normalized;
        }
        set
        {
            Vector3 modifiedDir = value;
            modifiedDir.y = 0;
            LookAtTargetPosition = Head.position + modifiedDir * 5f;
        }
    }
    public Vector3 LookAtTargetPosition { get; set; } = Vector3.forward;

    private Vector3 lookAtPosition = Vector3.forward;
    private float lookAtWeight = 0.0f;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (!Head)
        {
            IsLooking = false;
            return;
        }
        LookAtTargetPosition = Head.position + transform.forward;
        lookAtPosition = LookAtTargetPosition;
    }

    //当该动画层启用IK Pass时 这个回调函数才会被触发
    private void OnAnimatorIK(int layerIndex)
    {
        if(!IsLooking)
            LookDirection = Head.forward;

        LookIK();
    }

    void LookIK()
    {
        Vector3 modifiedTargetPos = LookAtTargetPosition;
        modifiedTargetPos.y = Head.position.y;
        LookAtTargetPosition = modifiedTargetPos;
        float lookAtTargetWeight = IsLooking ? 1.0f : 0.0f;

        Vector3 curDir = lookAtPosition - Head.position;
        Vector3 futDir = LookAtTargetPosition - Head.position;

        curDir = Vector3.RotateTowards(
            curDir,
            futDir,
            1.57f * Time.deltaTime,
            float.PositiveInfinity
        );
        lookAtPosition = Head.position + curDir;

        float blendTime = lookAtTargetWeight > lookAtWeight ? lookAtHeatTime : lookAtCoolTime;
        lookAtWeight = Mathf.MoveTowards(lookAtWeight, lookAtTargetWeight, Time.deltaTime / blendTime);
        animator.SetLookAtWeight(
            lookAtWeight,
            bodyWeight: 0.0f,  // 完全禁用身体转动
            headWeight: 1.0f,   // 满分头部权重
            eyesWeight: 0.0f,
            clampWeight: 0.6f  // 限制颈部旋转幅度
        );
        animator.SetLookAtPosition(lookAtPosition);
    }

}
