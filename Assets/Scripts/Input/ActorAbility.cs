using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class ActorAbility : ScriptableObject
{
    public string abilityName;
    public ActionTimelineAsset action;
    public InputSequence inputSequence;
}