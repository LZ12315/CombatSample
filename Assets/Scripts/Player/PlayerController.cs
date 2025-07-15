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
    CharacterBody physicsCharacter;
    Animator animator;
    Combater meleeAttacker;

    [Header("移动参数")]
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float runSpeed = 10f;

    [Header("输入控制")]
    private Queue<CommandInfo> inputs = new Queue<CommandInfo>();

    Vector2 inputDir = Vector3.zero;
    Vector3 moveDir = Vector3.zero;
    Vector3 faceDir = Vector3.zero;
    float currentSpeed = 0;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        physicsCharacter = GetComponent<CharacterBody>();
        meleeAttacker = GetComponent<Combater>();
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

    #region 移动相关

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

    void LocalMotion()
    {
        if (meleeAttacker.InAction)
        {
            physicsCharacter.SetVelocity(Vector3.zero);
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(faceDir);
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

    #endregion

    #region 攻击相关

    private void InvokeAttack(InputAction.CallbackContext context)
    {
        //CommandInfo newInfo = new CommandInfo(Time.time);
        //inputs.Enqueue(newInfo);

        meleeAttacker.TryAttack();
    }

    #endregion

    #region 其他

    private void OnEnable()
    {
        inputControl.Enable();
        inputControl.Player.Jump.started += Jump;
        inputControl.Player.Fire.started += InvokeAttack;
    }

    private void OnDisable()
    {
        inputControl.Disable();
        inputControl.Player.Jump.started -= Jump;
        inputControl.Player.Fire.started -= InvokeAttack;
    }

    public float MotionBlend => moveDir.magnitude * currentSpeed / runSpeed;

    # endregion

}
