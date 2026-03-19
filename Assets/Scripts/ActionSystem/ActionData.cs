using UnityEngine;
using System;

[Serializable]
public struct ActionData
{
    [SerializeField, Range(0, 1)] private double _normalizedTime;

    public double normalizedTime
    {
        get => _normalizedTime;
        set => _normalizedTime = Math.Clamp(value, 0, 1);
    }

    public static readonly ActionData Default = new ActionData
    {
        _normalizedTime = 0
    };
}


public static partial class Enums
{
    public enum ActionPriority
    {
        Normal,
        Special,
        Override
    }

    [Flags]
    public enum CleanupTarget
    {
        None = 0,
        Input = 1 << 0,
        Tags = 1 << 1,
        All = Input | Tags
    }
}
