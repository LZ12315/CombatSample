using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    Transform player;
    Vector3 moveDir;

    void Start()
    {
        StartCoroutine(Act());
    }

    IEnumerator Act()
    {
        yield return new WaitForSeconds(1);
        animator.SetBool("Walk", true);
        canAct = true;

        moveDir = Vector3.forward;
        yield return new WaitForSeconds(2);

        moveDir = Vector3.right;
        yield return new WaitForSeconds(4.5f);

        moveDir = Vector3.forward;
        yield return new WaitForSeconds(2f);

        canAct = false;
    }

    private void Update()
    {
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * 500f);
    }

    bool canAct;
    private void FixedUpdate()
    {
        if (canAct)
            transform.position += moveDir * 1 * Time.fixedDeltaTime;
    }

}
