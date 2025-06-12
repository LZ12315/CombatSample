using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.UI.GridLayoutGroup;
using static UnityEngine.UI.Image;

public class EnemyController : MonoBehaviour
{
    public Animator Animator { get; private set; }
    public CharacterBody PhysicsCharacter { get; private set; }
    public MeleeAttacker MeleeAttacker { get; private set; }
    public NavMeshAgent NavAgent { get; private set; }
    public EnemyInfo Info {  get; set; }

    [Header("移动参数")]
    [SerializeField] float rotateSpeed = 500f;
    [SerializeField] float moveSpeed = 6f;

    [field : Header("寻路参数")]
    [field: SerializeField] public float FOV { get; private set; } = 180f;
    public Transform Target { get; set; }
    public List<MeleeAttacker> detectTarget { get; set;} = new List<MeleeAttacker>();

    [Header("状态控制")]
    private Dictionary<Utils.Enums.EnemyStates, State<EnemyController>> stateDict;
    public StateMachine<EnemyController> stateMachine { get; private set;}

    void Start()
    {
        Animator = GetComponentInChildren<Animator>();
        PhysicsCharacter = GetComponent<CharacterBody>();
        NavAgent = GetComponent<NavMeshAgent>();
        MeleeAttacker = GetComponent<MeleeAttacker>();

        IDInitialized();
        InitStateMachine();
    }

    private void Update()
    {
        //stateMachine.StateUpdate();

        Animator.SetFloat("fowardSpeed", PhysicsCharacter.FowardSpeed, 0.2f, Time.deltaTime);
        Animator.SetFloat("strafeSpeed", PhysicsCharacter.StrafSpeed, 0.2f, Time.deltaTime);
    }

    private void FixedUpdate()
    {
        //stateMachine.StateFixedUpdate();
    }

    #region 位移相关

    public void LocalMotion(Vector3 faceDir, Vector3? moveDir = null, float? speed = null)
    {
        if (MeleeAttacker.InAction)
        {
            PhysicsCharacter.SetVelocity(Vector3.zero);
            return;
        }

        LocalRotation(faceDir);

        Vector3 effectiveMoveDir = moveDir ?? faceDir;
        float effectiveSpeed = speed ?? moveSpeed;

        Vector3 motionStep = effectiveMoveDir * effectiveSpeed;
        Vector3 velocity = new Vector3(motionStep.x, PhysicsCharacter.Velocity.y, motionStep.z);
        PhysicsCharacter.SetVelocity(velocity);
    }


    public void LocalRotation(Vector3 faceDir)
    {
        if (faceDir.magnitude == 0) return;

        faceDir.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(faceDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    #endregion

    #region 状态相关

    void InitStateMachine()
    {
        stateDict = new Dictionary<Utils.Enums.EnemyStates, State<EnemyController>>();

        stateDict.Add(Utils.Enums.EnemyStates.Idle, GetComponent<IdleState>());
        stateDict.Add(Utils.Enums.EnemyStates.CombatMove, GetComponent<CombatMoveState>());
        stateDict.Add(Utils.Enums.EnemyStates.Attack, GetComponent<AttackState>());
        stateDict.Add(Utils.Enums.EnemyStates.Retreat, GetComponent<RetreatState>());
        stateMachine.ChangeState(stateDict[Utils.Enums.EnemyStates.Idle]);
    }

    public void ChangeState(Utils.Enums.EnemyStates stateKey)
    {
        if (stateDict.ContainsKey(stateKey))
            stateMachine.ChangeState(stateDict[stateKey]);
    }

    public bool IsInState(Utils.Enums.EnemyStates state)
    {
        if (stateMachine.CurrentState == stateDict[state])
            return true;
        return false;
    }

    #endregion

    #region 其他

    void IDInitialized()
    {
        string id = System.Guid.NewGuid().ToString();
        Info = new EnemyInfo(id, this);
    }

    #endregion

}

public static partial class Utils
{

    public static partial class Enums
    {
        public enum EnemyStates
        {
            None, Idle, CombatMove, Attack, Retreat
        }
    }
}
