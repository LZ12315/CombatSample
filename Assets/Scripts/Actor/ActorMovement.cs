using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorMovement : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 500f;
    Quaternion rotation = Quaternion.identity;

    public void UpdateTurn(Vector3 direction)
    {
        if(direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        rotation = Quaternion.RotateTowards(rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    private void Update()
    {
        transform.rotation = rotation;
    }

}
