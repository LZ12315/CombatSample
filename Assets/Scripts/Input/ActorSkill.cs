using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;

[CreateAssetMenu(menuName = "CombatSystem/Skill")]
public class ActorSkill : ScriptableObject
{
    public Enums.ActionPriority priority;
    public ActionAsset action;
}