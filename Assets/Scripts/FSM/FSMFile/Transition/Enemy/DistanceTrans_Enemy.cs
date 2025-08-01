using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/TargetDistance", fileName = "TargetDistanceTransition")]
public class DistanceTrans_Enemy : Transition<EnemyController>
{
    [SerializeField] private float value;
    [SerializeField] private Enums.ConditionFunc function;

    public override bool ToTransition()
    {
        base.ToTransition();

        return CheckDistance(_owner.Target);
    }

    bool CheckDistance(Transform target)
    {
        if(target == null) return false;

        float distance = Vector3.Distance(_owner.transform.position, target.position);
        switch(function)
        {
            case Enums.ConditionFunc.More:
                return distance > value;
            case Enums.ConditionFunc.Less:
                return distance < value;
            case Enums.ConditionFunc.Equal:
                return distance == value;
            case Enums.ConditionFunc.MoreOrEqual:
                return distance >= value;
            case Enums.ConditionFunc.LessOrEqual:
                return distance <= value;   
        }

        return false;
    }
}

public static partial class Enums
{
    public enum ConditionFunc
    {
        More, Less, Equal, MoreOrEqual, LessOrEqual
    }
}