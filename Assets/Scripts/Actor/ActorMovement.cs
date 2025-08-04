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

    public void UpdateTurn(Vector3 direction)
    {
        if(direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        rotation = Quaternion.RotateTowards(rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    private void Update()
    {
        transform.rotation = rotation;
    }

    private void OnAnimatorMove()
    {
        animator.applyRootMotion = true;
        var deltaPos = animator.deltaPosition;
        var deltaRot = animator.deltaRotation;
        //为何这里要使用LocalRotation
        transform.localRotation = deltaRot * transform.rotation;
        actor.characterController.Move(deltaPos);
    }

    internal void ResetRotation()
    {
        transform.localRotation = Quaternion.identity;
    }
}
