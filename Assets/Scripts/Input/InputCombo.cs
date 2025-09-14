using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/Skill", fileName = "Skill")]
public class ComboAction : ScriptableObject
{
    public List<InputCommand> commands = new List<InputCommand>();
    public ActionTimelineAsset actionTimelineAsset;
}
