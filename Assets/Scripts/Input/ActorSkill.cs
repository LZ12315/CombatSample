using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class ActorSkill : ScriptableObject
{
    public string skillName;
    public SkillCommand skillCommand;
}