using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Transition/Enemy/IsSolo", fileName = "IsSoloTransition")]
public class IsSoloTransition_Enemy : Transition<EnemyController>
{

    public override bool ToTransition()
    {
        return IsSolo();
    }

    bool IsSolo()
    {
        if (EnemyManager.Instance == null)
            return true;

        if (EnemyManager.Instance.enemiesInCombat.Count > 1) 
            return false;
        
        if(_owner != EnemyManager.Instance.enemiesInCombat[0].controller)
            return false;

        return true;
    }

}
