using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class ActorAbility : ScriptableObject
{
    public string abilityName;
    public Enums.ActionPriority priority;
    public ActionTimelineAsset action;
    public InputSequence inputSequence;
}