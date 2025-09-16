using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class ActorSkillState : ScriptableObject
{
    public string skillName;
    public SkillCommand skillCommand;
}

public class SkillCommand
{
    public ActionTimelineAsset actionToPlay;
    public InputCommand command;
}