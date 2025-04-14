using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AttackState { None, WindUp, Impact, ColdDown}

public class MeleeAttacker : MonoBehaviour
{
    PlayerInputControl inputControl;
    Animator animator;

    [Header("武器设置")]
    [SerializeField] GameObject swordGamobject;
    Collider swordCollider;

    [Header("攻击设置")]
    [SerializeField] private List<AttackData> attacks;
    [field : SerializeField] public bool InAction { get; private set; }
    AttackState attackState;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if(swordGamobject != null)
        {
            swordCollider = swordGamobject.GetComponent<Collider>();
            swordCollider.enabled = false;
        }
    }

    private void InvokeAttack(InputAction.CallbackContext context)
    {
        EventCenter.Instance.EventTrigger("SwordAttack");
        if(!InAction)
            StartCoroutine(Attack());
    }


    IEnumerator Attack()
    {
        InAction = true;
        attackState = AttackState.WindUp;


        AttackData attack = attacks[0];
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
                case AttackState.WindUp:
                    if (normalizedTime >= attack.ImpactStartTime)
                    {
                        attackState = AttackState.Impact;
                        swordCollider.enabled = true;
                    }
                    break;
                case AttackState.Impact:
                    if (normalizedTime >= attack.ImpactEndTime)
                    {
                        attackState = AttackState.ColdDown;
                        swordCollider.enabled = false;
                    }
                    break;
                case AttackState.ColdDown:
                    break;

            }

            yield return null;
        }

        attackState = AttackState.None;
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
        animator.CrossFade("SwordInjured", 0.2f);
        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);
        yield return new WaitForSeconds(animState.length);

        InAction = false;
    }


    #region 其他

    private void OnEnable()
    {
        inputControl.Enable();
        inputControl.Player.Fire.started += InvokeAttack;
    }

    private void OnDisable()
    {
        inputControl.Disable();
        inputControl.Player.Fire.started -= InvokeAttack;
    }

    #endregion

}
