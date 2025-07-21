using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFSM : StateMachine<EnemyController>
{
    [SerializeField] private EnemyFSMSetting setting;

    public bool IsInState(Utils.Enums.EnemyStates stateType)
    {
        if(!setting.AsDictionary.ContainsKey(stateType)) return false;

        return CurrentState == setting.AsDictionary[stateType];
    }

    public void ChangeState(Utils.Enums.EnemyStates stateType)
    {
        if (!setting.AsDictionary.ContainsKey(stateType)) return;

        ChangeState(setting.AsDictionary[stateType]);
    }

}
