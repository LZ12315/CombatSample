using UnityEngine;
using System;

[Serializable]
public struct ActionData
{
    [SerializeField, Range(0, 1)] private double _normalizedTime;
    [SerializeField] private Enums.ActionPhase _phase;

    public double normalizedTime
    {
        get => _normalizedTime;
        set => _normalizedTime = Math.Clamp(value, 0, 1);
    }

    public Enums.ActionPhase phase
    {
        get => _phase;
        set
        {
            if (!Enum.IsDefined(typeof(Enums.ActionPhase), value))
                throw new ArgumentException($"Invalid ActionPhase: {value}");
            _phase = value;
        }
    }

    public bool IsInPhase(Enums.ActionPhase phaseToCheck)
    {
        return (_phase & phaseToCheck) != 0;
    }

    public static readonly ActionData Default = new ActionData
    {
        _normalizedTime = 0,
        _phase = Enums.ActionPhase.Neutral
    };
}

[Serializable]
public class ActionAttribute
{
    [SerializeField] private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;
    [SerializeField, Min(0)] private int _weight = 0;

    public Enums.ActionPriority priority
    {
        get => _priority;
        set
        {
            if (!Enum.IsDefined(typeof(Enums.ActionPriority), value))
                throw new ArgumentException($"Invalid ActionPriority: {value}");
            _priority = value;
        }
    }

    public int weight
    {
        get => _weight;
        set => _weight = Math.Max(0, value);
    }

}

public static partial class Enums
{
    [System.Flags]
    public enum ActionPhase
    {
        None = 0,
        Neutral = 2,
        Startup = 4,
        Charging = 8,
        FullPower = 16,
        OverCharge = 32,
        Effect = 64,
        Recovery = 128
    }

    public enum ActionPriority
    {
        Normal,
        Special,
        Override
    }
}