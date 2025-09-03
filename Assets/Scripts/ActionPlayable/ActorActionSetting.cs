using Animancer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/ActorActionSetting", fileName = "ActionSetting")]
public class ActorActionSetting : ScriptableObject
{
    public ActionTimelineAsset idle;
    public ActionTimelineAsset fight_Idle;
}
