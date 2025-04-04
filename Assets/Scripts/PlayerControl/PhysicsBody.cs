using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsBody : MonoBehaviour
{
    CharacterController characterController;
    GroundCheck groundCheck;

    [Header("重力相关")]
    [SerializeField] private float Mass = 1;
    [SerializeField] private float GravityScale = 1;
    private static float gravityVelocity = 9.81f;

    private Vector3 velocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = GetComponent<GroundCheck>();
    }

    private void Start()
    {
        if(groundCheck != null)
            groundCheck.OnFallDown.AddListener(OnFallDown);
    }

    private void FixedUpdate()
    {
        Gravity();
        characterController.Move(velocity * Time.fixedDeltaTime);
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }

    public void AddForce(Vector3 forceDir, float force)
    {
        Vector3 originVelocity = velocity;
        Vector3 forceVelocity = Mathf.Sqrt(force/Mass) * forceDir;
        velocity = forceVelocity + originVelocity;
    }

    void Gravity()
    {
        if (groundCheck == null || groundCheck.IsGround) return;
        
        float gravityForce = Mass * GravityScale * gravityVelocity;
        AddForce(Vector3.down, gravityForce * Time.fixedDeltaTime);
    }

    void OnFallDown()
    {
        Vector3 tmpVelocity = new Vector3(velocity.x, -0.5f, velocity.z);
        SetVelocity(tmpVelocity);
    }

    public Vector3 Velocity => velocity;

}
