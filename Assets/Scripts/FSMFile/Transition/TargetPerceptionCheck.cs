using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/TargetPerception",fileName ="TargetPerception") ]
public class TargetPerceptionCheck : Transition<EnemyController>
{
    [SerializeField] private string targetTypeName;
    [SerializeField] private LayerMask targetLayer;

    [Header("球体检测")]
    [SerializeField] private float overlapRadius = 3f; //球体半径

    [Header("视锥设置")]
    [SerializeField] private float viewRadius = 10f; // 视锥半径（等同于视距）
    [SerializeField] [Range(0, 180)] private float horizontalAngle = 60f; // 水平张角
    [SerializeField] [Range(0, 90)] private float verticalAngle = 30f; // 垂直张角

    public override bool ToTransition()
    {
        base.ToTransition();

        return TargetSence();
    }

    bool TargetSence()
    {
        Type targetType = Type.GetType(targetTypeName);
        if(targetType == null || _owner == null) return false;

        Collider[] hitColliders = Physics.OverlapSphere(_owner.transform.position, overlapRadius, targetLayer);
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
