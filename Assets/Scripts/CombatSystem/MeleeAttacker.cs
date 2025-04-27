using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AttackStates { None, WindUp, Impact, ColdDown}

public class MeleeAttacker : MonoBehaviour
{
    PlayerInputControl inputControl;
    Animator animator;

    [Header("武器设置")]
    [SerializeField] GameObject swordGamobject;
    BoxCollider swordCollider;
    Collider leftHandCollider, rightHandCollider, leftFootCollider, rightFootCollider; 

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

    AttackStates attackState;
    bool doCombo;
    [SerializeField] int comboCount;
    public void TryAttack()
    {
        if (attacks.Count <= 0) return;

        if (!InAction)
            StartCoroutine(Attack());
        else if (attackState == AttackStates.Impact || attackState == AttackStates.ColdDown)
            doCombo = true;
    }

    private void InvokeAttack(InputAction.CallbackContext context)
    {
        if (attacks.Count <= 0) return;

        if (!InAction)
            StartCoroutine(Attack());
        else if(attackState == AttackStates.Impact || attackState == AttackStates.ColdDown)
            doCombo = true;
    }

    IEnumerator Attack()
    {
        InAction = true;
        attackState = AttackStates.WindUp;

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
                case AttackStates.WindUp:
                    if (normalizedTime >= attack.ImpactStartTime)
                    {
                        attackState = AttackStates.Impact;
                        EnableHitBox(attack);
                    }
                    break;
                case AttackStates.Impact:
                    if (normalizedTime >= attack.ImpactEndTime)
                    {
                        attackState = AttackStates.ColdDown;
                        DisableAllHitBoxes();
                    }
                    break;
                case AttackStates.ColdDown:
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

        attackState = AttackStates.None;
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

    #region 判定相关

    void GetAttackComponent()
    {
        if (swordGamobject != null)
        {
            swordCollider = swordGamobject.GetComponent<BoxCollider>();
        }
        if (animator != null)
        {
            leftHandCollider = animator.GetBoneTransform(HumanBodyBones.LeftHand)?.GetComponent<Collider>();
            rightHandCollider = animator.GetBoneTransform(HumanBodyBones.RightHand)?.GetComponent<Collider>();
            leftFootCollider = animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.GetComponent<Collider>();
            rightFootCollider = animator.GetBoneTransform(HumanBodyBones.RightFoot)?.GetComponent<Collider>();
        }
        DisableAllHitBoxes();
    }

    void EnableHitBox(AttackData attackData)
    {
        switch (attackData.hitBoxType)
        {
            case Utils.AttackHitBox.LeftHand:
                if (leftHandCollider != null)
                    leftHandCollider.enabled = true;
                break;
            case Utils.AttackHitBox.RightHand:
                if (rightHandCollider != null)
                    rightHandCollider.enabled = true;
                break;
            case Utils.AttackHitBox.LeftFoot:
                if (leftFootCollider != null)
                    leftFootCollider.enabled = true;
                break;
            case Utils.AttackHitBox.RightFoot:
                if (rightFootCollider != null)
                    rightFootCollider.enabled = true;
                break;
            case Utils.AttackHitBox.Sword:
                if (swordCollider != null)
                    swordCollider.enabled = true;
                break;
        }
    }

    void DisableAllHitBoxes()
    {
        if (swordCollider != null) swordCollider.enabled = false;
        if (leftHandCollider != null) leftHandCollider.enabled = false;
        if (rightHandCollider != null) rightHandCollider.enabled = false;
        if (leftFootCollider != null) leftFootCollider.enabled = false;
        if (rightFootCollider != null) rightFootCollider.enabled = false;
    }

    #endregion

}
