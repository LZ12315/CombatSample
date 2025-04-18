using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] Animator animator;

    [SerializeField] Transform leftGrabPos;
    [SerializeField] Transform rightGrabPos;

    bool isGrabbing;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void Grab()
    {
        isGrabbing = true;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (isGrabbing)
        {
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightGrabPos.position);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrabPos.position);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        }
    }
}
