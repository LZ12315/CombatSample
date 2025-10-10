using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFSM : StateMachine<EnemyController>
{
    [SerializeField] private EnemyFSMSetting FSMSetting;

    public bool IsInState(Utils.Enums.EnemyStates stateType)
    {
        if(!FSMSetting.AsDictionary.ContainsKey(stateType)) return false;

        return CurrentState == FSMSetting.AsDictionary[stateType];
    }

    public void ChangeState(Utils.Enums.EnemyStates stateType)
    {
        if (!FSMSetting.AsDictionary.ContainsKey(stateType)) return;

        ChangeState(FSMSetting.AsDictionary[stateType]);
    }

}
