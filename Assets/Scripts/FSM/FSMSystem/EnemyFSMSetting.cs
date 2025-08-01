using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Setting/EnemySetting",fileName = "EnemySetting")]
public class EnemyFSMSetting : ScriptableObject
{
    [Serializable]
    public struct SettingPairs
    {
        public Enums.EnemyStates stateType;
        public State<EnemyController> state;
    }

    [field:SerializeField] public List<SettingPairs> settings {  get; private set; }
    private Dictionary<Enums.EnemyStates, State<EnemyController>> settingDic;

    public Dictionary<Enums.EnemyStates, State<EnemyController>> AsDictionary
    {
        get
        {
            if (settingDic == null)
            {
                settingDic = new Dictionary<Enums.EnemyStates, State<EnemyController>>();
                foreach (var setting in settings)
                    settingDic[setting.stateType] = setting.state;
            }
            return settingDic;
        }
    }
}
