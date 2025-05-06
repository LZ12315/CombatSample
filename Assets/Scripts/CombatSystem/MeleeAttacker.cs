using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MeleeAttacker : MonoBehaviour
{
    PlayerInputControl inputControl;
    Animator animator;

    [Header("武器设置")]
    [SerializeField] private GameObject swordGamobject;
    private Dictionary<Utils.Enums.AttackHitBox, Collider> HitBoxColliders = new Dictionary<Utils.Enums.AttackHitBox, Collider>();

    [Header("攻击设置")]
    [SerializeField] private List<AttackData> attacks = new List<AttackData>();
    [field : SerializeField] public bool InAction { get; private set; }

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        GetAttackComponent();
    }

    Utils.Enums.AttackStates attackState;
    bool doCombo;
    int comboCount;
    public void TryAttack()
    {
        if (attacks.Count <= 0) return;

        if (!InAction)
            StartCoroutine(Attack());
        else if (attackState == Utils.Enums.AttackStates.Impact || attackState == Utils.Enums.AttackStates.ColdDown)
            doCombo = true;
    }

    IEnumerator Attack()
    {
        InAction = true;
        attackState = Utils.Enums.AttackStates.WindUp;

        AttackData attack = attacks[comboCount];
        animator.CrossFade(attack.AnimName, 0.2f);
        yield return null;

        float timer = 0;
        var animState = animator.GetNextAnimatorStateInfo(1);
        while(timer < animState.length)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / animState.length;
            switch (attackState)
            {
                case Utils.Enums.AttackStates.WindUp:
                    if (normalizedTime >= attack.ImpactStartTime)
                    {
                        attackState = Utils.Enums.AttackStates.Impact;
                        EnableHitBox(attack);
                    }
                    break;
                case Utils.Enums.AttackStates.Impact:
                    if (normalizedTime >= attack.ImpactEndTime)
                    {
                        attackState = Utils.Enums.AttackStates.ColdDown;
                        DisableAllHitBoxes();
                    }
                    break;
                case Utils.Enums.AttackStates.ColdDown:
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

        attackState = Utils.Enums.AttackStates.None;
        comboCount = 0;
        InAction = false;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "HitBox" && !InAction)
            StartCoroutine(GetAttack());
    }


    IEnumerator GetAttack()
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
        if (swordGamobject != null)
            AddPartCollider(Utils.Enums.AttackHitBox.Sword, swordGamobject.GetComponent<BoxCollider>());
        if (animator != null)
        {
            AddPartCollider(Utils.Enums.AttackHitBox.LeftHand, animator.GetBoneTransform(HumanBodyBones.LeftHand)?.GetComponent<Collider>());
            AddPartCollider(Utils.Enums.AttackHitBox.RightHand, animator.GetBoneTransform(HumanBodyBones.RightHand)?.GetComponent<Collider>());
            AddPartCollider(Utils.Enums.AttackHitBox.LeftFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.GetComponent<Collider>());
            AddPartCollider(Utils.Enums.AttackHitBox.RightFoot, animator.GetBoneTransform(HumanBodyBones.RightFoot)?.GetComponent<Collider>());
        }
        DisableAllHitBoxes();
    }

    void AddPartCollider(Utils.Enums.AttackHitBox part, Collider collider)
    {
        if(HitBoxColliders.ContainsKey(part))
            HitBoxColliders[part] = collider;
        else
            HitBoxColliders.Add(part, collider);
    }

    #endregion

}

public static partial class Utils
{
    public static partial class Enums
    {
        public enum AttackStates 
        { 
            None, WindUp, Impact, ColdDown 
        }
    }
}
