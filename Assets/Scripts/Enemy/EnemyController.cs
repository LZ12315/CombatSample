using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.UI.Image;

public class EnemyController : MonoBehaviour
{
    public Animator animator { get; set;}
    PhysicsBody physicsCharacter;

    [Header("移动参数")]
    [SerializeField] bool canMove = true;
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float moveSpeed = 6f;

    [field : Header("寻路参数")]
    [field: SerializeField] public float FOV { get; private set; } = 180f;
    public Transform chaseTarget { get; set; }
    public List<MeleeAttacker> detectTarget { get; set;} = new List<MeleeAttacker>();
    public NavMeshAgent NavMeshAgent { get; set; }

    [Header("状态控制")]
    private Dictionary<Utils.EnemtState, State<EnemyController>> stateDict;
    public StateMachine<EnemyController> stateMachine { get; private set;}

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        physicsCharacter = GetComponent<PhysicsBody>();
        NavMeshAgent = GetComponent<NavMeshAgent>();

        InitStateMachine();
    }

    private void Update()
    {
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

    #region 状态相关

    void InitStateMachine()
    {
        stateMachine = new StateMachine<EnemyController>(this);
        stateDict = new Dictionary<Utils.EnemtState, State<EnemyController>>();

        stateDict.Add(Utils.EnemtState.Idle, GetComponent<IdleState>());
        stateDict.Add(Utils.EnemtState.Chase, GetComponent<ChaseState>());
        stateMachine.ChangeState(stateDict[Utils.EnemtState.Idle]);
    }

    public void ChangeState(Utils.EnemtState stateKey)
    {
        if (stateDict.ContainsKey(stateKey))
            stateMachine.ChangeState(stateDict[stateKey]);
    }

    #endregion

}

public static partial class Utils
{
    public enum EnemtState
    {
        None, Idle, Chase
    }
}
