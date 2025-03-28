using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] PlayerInputControl inputControl;
    [SerializeField] Rigidbody rb;
    CameraController cameraControl;


    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float moveSpeed = 0.1f;

    Vector2 inputDir;
    Vector3 moveDir;
    Quaternion targetRotation;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        rb = GetComponent<Rigidbody>();
        cameraControl = Camera.main.GetComponent<CameraController>();
    }

    private void Update()
    {
        inputDir = inputControl.Player.Move.ReadValue<Vector2>();
        moveDir = cameraControl.RotationVertical * new Vector3(inputDir.x, 0 ,inputDir.y);
        targetRotation = Quaternion.LookRotation(moveDir);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    private void FixedUpdate()
    {
        transform.position += moveDir * moveSpeed * Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        inputControl.Enable();
    }

    private void OnDisable()
    {
        inputControl.Disable();
    }

    public float Movement => moveDir.magnitude * moveSpeed;

}
