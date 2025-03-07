using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] float moveSpeed;

    private void FixedUpdate()
    {
        transform.position += Vector3.forward * moveSpeed * Time.fixedDeltaTime;
    }


}
