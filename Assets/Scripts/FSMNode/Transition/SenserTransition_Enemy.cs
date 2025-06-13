using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/TargetSenser")]
public class SenserTransition_Enemy : Transition<EnemyController>
{
    [SerializeField] private string targetTypeName;
    [SerializeField] private float senceRadius = 3f;
    [SerializeField] private LayerMask targetLayer;

    public override bool ToTransition()
    {
        base.ToTransition();

        return TargetSence();
    }

    bool TargetSence()
    {
        Type targetType = Type.GetType(targetTypeName);
        if(targetType == null || _owner == null) return false;

        Collider[] hitColliders = Physics.OverlapSphere(_owner.transform.position, senceRadius, targetLayer);
        foreach (var collider in hitColliders)
        {
            if (collider.GetComponent(targetType) != null)
            {
                _owner.Target = collider.transform;
                return true;
            }
        }

        return false;
    }

}
