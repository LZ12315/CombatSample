using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterBody : MonoBehaviour
{
    CharacterController characterController;
    GroundCheck groundCheck;

    [Header("Body设置")]
    [SerializeField] private bool bodyEnabled = true;
    
    [Header("重力相关")]
    [SerializeField] private float Mass = 1;
    [SerializeField] private float GravityScale = 1;
    [SerializeField] private static float gravityVelocity = 9.81f;

    [Header("速度属性")]
    [SerializeField] private Vector3 physicsVelocity;
    [SerializeField] private Vector3 frameVelocity;
    public Vector3 Velocity 
    { 
        get 
        {
            if (bodyEnabled) 
                return physicsVelocity;
            else 
                return frameVelocity;
        }
    }
    public float FowardSpeed
    {
        get
        {
            return Vector3.Dot(Velocity, transform.forward);
        }
    }
    public float StrafSpeed
    {
        get
        {
            float angle = Vector3.SignedAngle(Velocity, transform.forward, Vector3.up);
            return Mathf.Sin(angle * Mathf.Deg2Rad);
        }
    }

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

    Vector3 prePos = Vector3.zero;
    private void Update()
    {
        Vector3 deltaPos = transform.position - prePos;
        frameVelocity = deltaPos/Time.deltaTime;
        prePos = transform.position;
    }

    private void FixedUpdate()
    {
        if(!bodyEnabled) return;

        Gravity();
        characterController.Move(physicsVelocity * Time.fixedDeltaTime);
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        physicsVelocity = newVelocity;
    }

    public void AddForce(Vector3 forceDir, float force)
    {
        Vector3 originVelocity = physicsVelocity;
        Vector3 forceVelocity = Mathf.Sqrt(force/Mass) * forceDir;
        physicsVelocity = forceVelocity + originVelocity;
    }

    #region 重力控制

    void Gravity()
    {
        if (groundCheck == null || groundCheck.IsGround) return;
        
        float gravityForce = Mass * GravityScale * gravityVelocity;
        AddForce(Vector3.down, gravityForce * Time.fixedDeltaTime);
    }

    void OnFallDown()
    {
        Vector3 tmpVelocity = new Vector3(physicsVelocity.x, -0.5f, physicsVelocity.z);
        SetVelocity(tmpVelocity);
    }

    #endregion

}
