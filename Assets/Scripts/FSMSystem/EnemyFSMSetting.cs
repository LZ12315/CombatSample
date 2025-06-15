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
        public Utils.Enums.EnemyStates stateType;
        public State<EnemyController> state;
    }

    [field:SerializeField] public List<SettingPairs> settings {  get; private set; }
    private Dictionary<Utils.Enums.EnemyStates, State<EnemyController>> settingDic;

    public Dictionary<Utils.Enums.EnemyStates, State<EnemyController>> AsDictionary
    {
        get
        {
            if (settingDic == null)
            {
                settingDic = new Dictionary<Utils.Enums.EnemyStates, State<EnemyController>>();
                foreach (var setting in settings)
                    settingDic[setting.stateType] = setting.state;
            }
            return settingDic;
        }
    }
}
