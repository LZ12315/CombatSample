using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCarryTest : MonoBehaviour
{
    PlayerAnimation Animation;
    [SerializeField] Transform objectGrabPos;

    private void Start()
    {
        Animation = GetComponentInChildren<PlayerAnimation>();
    }

    void OnTriggerStay(Collider other)
    {
        if (Input.GetKey(KeyCode.E) && other.CompareTag("Grabbable"))
        {
            other.transform.SetParent(objectGrabPos, true);
            other.transform.localPosition = Vector3.zero;
            other.GetComponent<Rigidbody>().isKinematic = true;
            Animation.Grab();
        }
    }
}
