using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowEjector : MonoBehaviour
{
    [SerializeField] GameObject arrow;
    [SerializeField] Transform ejectHole;
    [SerializeField] float ejectForce = 10f;

    private void Start()
    {
        Eject();
    }

    void Eject()
    {
        if (arrow == null)
            return;

        GameObject newArrow = Instantiate(arrow,ejectHole.position,transform.rotation);
        Rigidbody rb = newArrow?.GetComponent<Rigidbody>();
        rb.AddForce(transform.forward * ejectForce, ForceMode.Impulse);
    }
}
