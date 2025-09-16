using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class Skill : ScriptableObject
{
    public ActionTimelineAsset actionToPlay;
    public InputCommand command;
}
