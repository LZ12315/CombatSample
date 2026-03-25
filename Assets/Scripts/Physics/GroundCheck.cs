using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GroundCheck : MonoBehaviour
{
    [SerializeField] private Vector3 checkOffset;
    [SerializeField] private float checkRadius;
    [SerializeField] private LayerMask checkLayer;
    [SerializeField] private bool isGround = true;

    [HideInInspector] public UnityEvent OnTakeOff;
    [HideInInspector] public UnityEvent OnFallDown;

    private void Update()
    {
        bool isGround_pre = Physics.CheckSphere(transform.TransformPoint(checkOffset), checkRadius, checkLayer);
        if(!isGround && isGround_pre)
            OnFallDown.Invoke();
        if(isGround && !isGround_pre)
            OnTakeOff.Invoke();

        isGround = isGround_pre;
    }

    public bool IsGround => isGround;   

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(checkOffset), checkRadius);    
    }

}
