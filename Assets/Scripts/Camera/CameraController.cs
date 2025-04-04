using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    PlayerInputControl inputControl;
    [SerializeField] Transform followTarget;

    [SerializeField] float rotationSpeedX = 2f;
    [SerializeField] float rotationSpeedY = 1f;
    [SerializeField] float followDistance;

    [SerializeField] Vector2 frameOffset;
    [SerializeField] float minVerticalAngle = -45f;
    [SerializeField] float maxVerticalAngle = 45f;

    [SerializeField] bool invertX;
    [SerializeField] bool invertY;

    float rotationY = 0;
    float rotationX = 0;
    float invertXVal;
    float invertYVal;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        invertXVal = (invertX ? -1 : 1);
        invertYVal = (invertY ? -1 : 1);
    }

    private void Update()
    {
        Vector2 lookInput = inputControl.Player.Look.ReadValue<Vector2>();

        rotationX += lookInput.y * rotationSpeedY * invertXVal * Time.deltaTime;
        rotationY += lookInput.x * rotationSpeedX * invertYVal * Time.deltaTime;
        rotationX = Mathf.Clamp(rotationX, minVerticalAngle, maxVerticalAngle);

        Quaternion targetRotation = Quaternion.Euler(rotationX, rotationY, 0);

        Vector3 focusPosition = followTarget.position + (Vector3)frameOffset;
        transform.position = focusPosition - targetRotation * new Vector3(0,0,followDistance);
        transform.rotation = targetRotation;
    }

    #region ÆäËû

    public Quaternion RotationVertical => Quaternion.Euler(0, rotationY, 0);

    private void OnEnable()
    {
        inputControl.Enable();
    }

    private void OnDisable()
    {
        inputControl.Disable();
    }

    #endregion

}
