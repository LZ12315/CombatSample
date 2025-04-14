using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/AttackData")]
public class AttackData : ScriptableObject
{
    [field: SerializeField]
    public string AnimName { get; private set;}

    [field : SerializeField]
    public float ImpactStartTime { get; private set;}

    [field: SerializeField]
    public float ImpactEndTime { get; private set;}
}
