using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;

public class EnemyController : MonoBehaviour
{
    Animator animator;
    PhysicsBody physicsCharacter;

    [SerializeField] private float moveSpeed = 5f;
    Transform player;

    void Start()
    {
        animator = GetComponent<Animator>();
        physicsCharacter = GetComponent<PhysicsBody>();

        player = GameObject.FindWithTag("Player").transform;
    }

    private void Update()
    {
        Chase(player);
    }

    void Chase(Transform target)
    {
        if (target == null) return;

        Vector3 chaseDir = (target.position - transform.position).normalized;

        Quaternion targetRotation = Quaternion.LookRotation(chaseDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * 500f);

        Vector3 motionStep = chaseDir * moveSpeed;
        Vector3 velocity = new Vector3(motionStep.x, physicsCharacter.Velocity.y, motionStep.z);
        physicsCharacter.SetVelocity(velocity);

        if (animator == null) return;
        animator.SetFloat("motionBlend", 1, 0.1f, Time.deltaTime);
    }

}
