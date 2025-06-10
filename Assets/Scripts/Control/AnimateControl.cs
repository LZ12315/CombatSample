using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateControl : MonoBehaviour
{
    private Animator animator;

    [field:Header("注视设置")]
    [SerializeField] private Transform head = null;
    [SerializeField] private float lookAtCoolTime = 0.2f;
    [SerializeField] private float lookAtHeatTime = 0.2f;
    public Vector3 lookAtTargetPosition { get; set;}
    public bool looking { get; set; } = true;

    private Vector3 lookAtPosition;
    private float lookAtWeight = 0.0f;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (!head)
        {
            looking = false;
            return;
        }
        lookAtTargetPosition = head.position + transform.forward;
        lookAtPosition = lookAtTargetPosition;
    }

    //当该动画层启用IK Pass时 这个回调函数才会被触发
    private void OnAnimatorIK(int layerIndex)
    {
        LookIK();
    }

    void LookIK()
    {
        Vector3 modifiedTargetPos = lookAtTargetPosition;
        modifiedTargetPos.y = head.position.y;
        lookAtTargetPosition = modifiedTargetPos;
        float lookAtTargetWeight = looking ? 1.0f : 0.0f;

        Vector3 curDir = lookAtPosition - head.position;
        Vector3 futDir = lookAtTargetPosition - head.position;

        curDir = Vector3.RotateTowards(curDir, futDir, 6.28f * Time.deltaTime, float.PositiveInfinity);
        lookAtPosition = head.position + curDir;

        float blendTime = lookAtTargetWeight > lookAtWeight ? lookAtHeatTime : lookAtCoolTime;
        lookAtWeight = Mathf.MoveTowards(lookAtWeight, lookAtTargetWeight, Time.deltaTime / blendTime);
        animator.SetLookAtWeight(lookAtWeight, 0.2f, 0.5f, 0.7f, 0.5f);
        animator.SetLookAtPosition(lookAtPosition);
    }

}
