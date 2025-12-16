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

    public static readonly ActionData Default = new ActionData
    {
        _normalizedTime = 0,
        _phase = Enums.ActionPhase.Neutral
    };
}


public static partial class Enums
{
    public enum ActionPhase
    {
        None,
        Neutral,
        Effect,
        Recovery
    }

    public enum ActionPriority
    {
        Normal,
        Special,
        Override
    }

    [Flags]
    public enum ActionMoveFlags
    {
        None = 0,
        CanMove = 1 << 0, // (1) ÔÊÐíÒ¡¸Ë¿ØÖÆÎ»ÒÆ
        CanRotate = 1 << 1, // (2) ÔÊÐíÒ¡¸Ë¿ØÖÆ³¯Ïò
        IgnoreGravity = 1 << 2, // (4) ºöÂÔÖØÁ¦ (ÖÍ¿Õ/¿ÕÖÐÁ¬ÕÐ)
    }

    public enum ActionType
    {
        Idle,           // ´ý»ú
        Locomotion,     // ÒÆ¶¯ (Run/Walk)
        GroundAttack,   // µØÃæ¹¥»÷
        AirAttack,      // ¿ÕÖÐ¹¥»÷
    }
}