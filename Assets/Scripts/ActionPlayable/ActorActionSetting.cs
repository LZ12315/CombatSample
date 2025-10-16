using Animancer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/ActorActionSetting", fileName = "ActionSetting")]
public class ActorActionSetting : ScriptableObject
{
    public ActionAsset idle;
    public ActionAsset fight_Idle;
    public List<ActorSkill> specialSkills;
}
