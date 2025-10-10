using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/TargetPerception",fileName ="TargetPerception") ]
public class TargetPerceptionCheck_Enemy : Transition<EnemyController>
{


    public override bool ToTransition()
    {
        base.ToTransition();

        if (_owner.Target == null)
            return false;

        return IsTargetPercepted(_owner.Target?.GetComponent<PlayerCombater>());
    }

    bool IsTargetPercepted(PlayerCombater target)
    {
        if(target == null) return false;

        if(target.IsAcquised) return true;

        return false;
    }

}
