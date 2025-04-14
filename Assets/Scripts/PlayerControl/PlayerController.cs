using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerController : MonoBehaviour
{
    PlayerInputControl inputControl;
    CameraController cameraControl;
    PhysicsBody physicsCharacter;
    Animator animator;
    MeleeAttacker meleeAttacker;

    [Header("移动参数")]
    [SerializeField] bool canMove = true;
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float runSpeed = 10f;


    Vector2 inputDir = Vector3.zero;
    Vector3 moveDir = Vector3.zero;
    Vector3 faceDir = Vector3.zero;
    float currentSpeed = 0;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        physicsCharacter = GetComponent<PhysicsBody>();
        meleeAttacker = GetComponent<MeleeAttacker>();
        animator = GetComponentInChildren<Animator>();
        cameraControl = Camera.main.GetComponent<CameraController>();
    }

    private void Update()
    {
        GetDir();
        GetSpeed();
        LocalMotion();
        MotionAnim();
    }

    void GetDir()
    {
        inputDir = inputControl.Player.Move.ReadValue<Vector2>();
        moveDir = cameraControl.RotationVertical * new Vector3(inputDir.x, 0, inputDir.y);

        if (Mathf.Clamp01(inputDir.magnitude) > 0)
            faceDir = moveDir;
    }

    void GetSpeed()
    {
        currentSpeed = walkSpeed;
        if(Input.GetKey(KeyCode.LeftShift))
            currentSpeed = runSpeed;
    }

    Quaternion targetRotation;
    void LocalMotion()
    {
        if (meleeAttacker.InAction)
        {
            physicsCharacter.SetVelocity(Vector3.zero);
            return;
        }

        targetRotation = Quaternion.LookRotation(faceDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);

        Vector3 motionStep = moveDir * currentSpeed;
        Vector3 velocity = new Vector3(motionStep.x, physicsCharacter.Velocity.y, motionStep.z);
        physicsCharacter.SetVelocity(velocity);
    }

    void MotionAnim()
    {
        if (animator == null) return;
        animator.SetFloat("motionBlend", MotionBlend, 0.1f, Time.deltaTime);
    }

    void Jump(InputAction.CallbackContext context)
    {
        physicsCharacter.AddForce(Vector3.up, 100f);
    }


    # region 其他

    private void OnEnable()
    {
        inputControl.Enable();
        inputControl.Player.Jump.started += Jump;
    }

    private void OnDisable()
    {
        inputControl.Disable();
        inputControl.Player.Jump.started -= Jump;
    }

    public float MotionBlend => moveDir.magnitude * currentSpeed / runSpeed;

    # endregion

}
