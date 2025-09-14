using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;

struct CommandInfo
{
    public float inputTime;

    public CommandInfo(float inputTime)
    {
        this.inputTime = inputTime;
    }
}

public class Combater : MonoBehaviour
{
    PlayerInputControl inputControl;
    Animator animator;

    [Header("战斗设置")]
    [SerializeField] protected GameObject swordObject;
    protected Dictionary<Enums.AttackHitBox, Collider> HitBoxColliders = new Dictionary<Enums.AttackHitBox, Collider>();

    [SerializeField] protected List<AttackData> attacks = new List<AttackData>();
    [field : SerializeField] public bool InAction { get; private set; }
    public Enums.AttackStates AttackState {  get; private set; }

    protected void Awake()
    {
        inputControl = new PlayerInputControl();
        animator = GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        GetAttackComponent();
    }

    bool doCombo;
    int comboCount;
    public void TryAttack()
    { 
        if (attacks.Count <= 0) return;

        if (!InAction)
            StartCoroutine(Attack());
        else if (AttackState == Enums.AttackStates.Impact || AttackState == Enums.AttackStates.ColdDown)
            doCombo = true;
    }

    protected IEnumerator Attack()
    {
        InAction = true;
        AttackState = Enums.AttackStates.WindUp;

        AttackData attack = attacks[comboCount];
        animator.CrossFade(attack.AnimName, 0.2f);
        yield return null;

        float timer = 0;
        var animState = animator.GetNextAnimatorStateInfo(1);
        while(timer < animState.length)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / animState.length;
            switch (AttackState)
            {
                case Enums.AttackStates.WindUp:
                    if (normalizedTime >= attack.ImpactStartTime)
                    {
                        AttackState = Enums.AttackStates.Impact;
                        EnableHitBox(attack);
                    }
                    break;
                case Enums.AttackStates.Impact:
                    if (normalizedTime >= attack.ImpactEndTime)
                    {
                        AttackState = Enums.AttackStates.ColdDown;
                        DisableAllHitBoxes();
                    }
                    break;
                case Enums.AttackStates.ColdDown:
                    {
                        if(doCombo)
                        {
                            doCombo = false;
                            comboCount = (comboCount + 1) % attacks.Count;

                            StartCoroutine(Attack());
                            yield break;
                        }
                    }
                    break;

            }

            yield return null;
        }

        AttackState = Enums.AttackStates.Idle;
        comboCount = 0;
        InAction = false;
    }


    protected void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "HitBox")
            StartCoroutine(GetAttack());
    }


    protected IEnumerator GetAttack()
    {
        InAction = true;
        animator.CrossFade("Injured", 0.2f);
        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);
        yield return new WaitForSeconds(animState.length * 0.2f);

        InAction = false;
    }



    #region 碰撞体相关

    void EnableHitBox(AttackData attackData)
    {
        if(HitBoxColliders.ContainsKey(attackData.hitBoxType))
        {
            if(HitBoxColliders[attackData.hitBoxType] != null)
                HitBoxColliders[attackData.hitBoxType].enabled = true;
        }
    }

    void DisableAllHitBoxes()
    {
        foreach (var hitbox in HitBoxColliders)
        {
            if(hitbox.Value != null)
                hitbox.Value.enabled = false;
        }
    }

    void GetAttackComponent()
    {
        if (swordObject != null)
            AddPartCollider(Enums.AttackHitBox.Sword, swordObject.GetComponent<BoxCollider>());
        if (animator != null)
        {
            AddPartCollider(Enums.AttackHitBox.LeftHand, animator.GetBoneTransform(HumanBodyBones.LeftHand)?.GetComponent<Collider>());
            AddPartCollider(Enums.AttackHitBox.RightHand, animator.GetBoneTransform(HumanBodyBones.RightHand)?.GetComponent<Collider>());
            AddPartCollider(Enums.AttackHitBox.LeftFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.GetComponent<Collider>());
            AddPartCollider(Enums.AttackHitBox.RightFoot, animator.GetBoneTransform(HumanBodyBones.RightFoot)?.GetComponent<Collider>());
        }
        DisableAllHitBoxes();
    }

    void AddPartCollider(Enums.AttackHitBox part, Collider collider)
    {
        if(HitBoxColliders.ContainsKey(part))
            HitBoxColliders[part] = collider;
        else
            HitBoxColliders.Add(part, collider);
    }

    #endregion

}

public static partial class Enums
{
    public enum AttackStates
    {
        Idle, WindUp, Impact, ColdDown
    }
}
