using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] PlayerController controller;

    [SerializeField] Transform leftGrabPos;
    [SerializeField] Transform rightGrabPos;

    bool isGrabbing;

    private void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        if (animator != null)
        {
            animator.SetFloat("motionBlend", controller.MotionBlend, 0.1f, Time.deltaTime);
        }
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
