using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSystem/AttackData")]
public class AttackData : ScriptableObject
{
    [field: SerializeField]
    public string AnimName { get; private set;}

    [SerializeField]
    public Consts.AttackHitBox hitBoxType;

    [field : SerializeField]
    public float ImpactStartTime { get; private set;}

    [field: SerializeField]
    public float ImpactEndTime { get; private set;}
}

public static partial class Consts
{
    public enum AttackHitBox
    {
        None, Sword, LeftHand, LeftFoot,RightHand, RightFoot
    }
}
