using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] PlayerInputControl inputControl;
    [SerializeField] Rigidbody rb;
    CameraController cameraControl;
    PlayerAnimation Animation;

    [SerializeField] Transform objectGrabPos;
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
        Animation = GetComponentInChildren<PlayerAnimation>();
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

    void OnTriggerStay(Collider other)
    {
        if (Input.GetKey(KeyCode.E) && other.CompareTag("Grabbable"))
        {
            other.transform.SetParent(objectGrabPos, true);
            other.transform.localPosition = Vector3.zero;
            other.GetComponent<Rigidbody>().isKinematic = true;
            Animation.Grab();
        }
    }

    public float Movement => moveDir.magnitude * moveSpeed;

}
