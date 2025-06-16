using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateControl : MonoBehaviour
{
    private Animator animator;

    [field: Header("注视设置")]
    [field: SerializeField] public Transform Head { get; private set; }
    [SerializeField] private float lookAtCoolTime = 0.2f;
    [SerializeField] private float lookAtHeatTime = 0.2f;

    public Vector3 LookAtTargetPosition { get; set; }
    public bool Looking { get; set; } = true;
    public Vector3 OriginLookDirection { get; set; }

    private Vector3 lookAtPosition;
    private float lookAtWeight = 0.0f;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (!Head)
        {
            Looking = false;
            return;
        }
        OriginLookDirection = Head.position + transform.forward;
        LookAtTargetPosition = OriginLookDirection;
        lookAtPosition = LookAtTargetPosition;
    }

    //当该动画层启用IK Pass时 这个回调函数才会被触发
    private void OnAnimatorIK(int layerIndex)
    {
        LookIK();
    }

    void LookIK()
    {
        Vector3 modifiedTargetPos = LookAtTargetPosition;
        modifiedTargetPos.y = Head.position.y;
        LookAtTargetPosition = modifiedTargetPos;
        float lookAtTargetWeight = Looking ? 1.0f : 0.0f;

        Vector3 curDir = lookAtPosition - Head.position;
        Vector3 futDir = LookAtTargetPosition - Head.position;

        curDir = Vector3.RotateTowards(curDir, futDir, 6.28f * Time.deltaTime, float.PositiveInfinity);
        lookAtPosition = Head.position + curDir;

        float blendTime = lookAtTargetWeight > lookAtWeight ? lookAtHeatTime : lookAtCoolTime;
        lookAtWeight = Mathf.MoveTowards(lookAtWeight, lookAtTargetWeight, Time.deltaTime / blendTime);
        animator.SetLookAtWeight(lookAtWeight, 0.2f, 0.5f, 0.7f, 0.5f);
        animator.SetLookAtPosition(lookAtPosition);
    }

}
