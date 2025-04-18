using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.UI.Image;

public class EnemyController : MonoBehaviour
{
    Animator animator;
    PhysicsBody physicsCharacter;

    [Header("ÒÆ¶¯²ÎÊý")]
    [SerializeField] bool canMove = true;
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float moveSpeed = 6f;

    public Transform player { get; private set;}
    public StateMachine<EnemyController> stateMachine { get; private set;}
    private Dictionary<Utils.EnemtState, State<EnemyController>> stateDict;

    void Start()
    {
        animator = GetComponent<Animator>();
        physicsCharacter = GetComponent<PhysicsBody>();

        player = GameObject.FindWithTag("Player").transform;
        InitStateMachine();
    }

    void InitStateMachine()
    {
        stateMachine = new StateMachine<EnemyController>(this);
        stateDict = new Dictionary<Utils.EnemtState, State<EnemyController>>();

        stateDict.Add(Utils.EnemtState.Idle, GetComponent<IdleState>());
        stateMachine.ChangeState(stateDict[Utils.EnemtState.Idle]);
    }

    public void ChangeState(Utils.EnemtState stateKey)
    {
        if (stateDict.ContainsKey(stateKey))
            stateMachine.ChangeState(stateDict[stateKey]);
    }

    private void Update()
    {
        Vector3 faceDir = (player.position - transform.position).normalized;
        LocalMotion(faceDir);

        stateMachine.ExcuteState();
    }

    void LocalMotion(Vector3 faceDir)
    {
        Quaternion targetRotation = Quaternion.LookRotation(faceDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);

        Vector3 motionStep = faceDir * moveSpeed;
        Vector3 velocity = new Vector3(motionStep.x, physicsCharacter.Velocity.y, motionStep.z);
        physicsCharacter.SetVelocity(velocity);
    }

}

public static partial class Utils
{
    public enum EnemtState
    {
        None, Idle, Chase
    }
}
