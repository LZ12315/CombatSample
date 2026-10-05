using System;
using UnityEngine;

/// <summary>
/// One controller's latest submission. A new submission replaces the old one;
/// there is no input arbitration or suspended intent to restore.
/// </summary>
internal sealed class LocomotionIntentBuffer
{
    private LocomotionIntent _intent;
    private bool _hasIntent;
    private bool _continuous;
    private bool _canHoldOneShot;

    internal void Submit(in LocomotionIntent intent, bool continuous = false)
    {
        if (!LocomotionDataValidation.IsFinite(intent.WorldMoveDirection)
            || !LocomotionDataValidation.IsFinite(intent.FacingDirection)
            || !LocomotionDataValidation.IsFinite(intent.MoveStrength)
            || intent.MoveStrength < 0f || intent.MoveStrength > 1f)
            throw new ArgumentException("Locomotion intent requires finite directions and MoveStrength in [0, 1].", nameof(intent));
        _intent = intent;
        _hasIntent = true;
        _continuous = continuous;
        _canHoldOneShot = false;
    }

    internal bool Capture(out LocomotionIntent intent, out bool continuous)
    {
        intent = _hasIntent ? _intent : LocomotionIntent.Idle;
        continuous = _continuous;
        bool hasIntent = _hasIntent;
        if (!continuous) Clear();
        _canHoldOneShot = hasIntent && !continuous;
        return hasIntent;
    }

    internal void Hold(in LocomotionIntent intent, bool continuous)
    {
        // Only consumption may be undone by freezing. A later submission or release wins.
        if (!continuous && _canHoldOneShot && !_hasIntent) Submit(intent);
    }

    internal void ReleaseContinuous()
    {
        if (_continuous) Clear();
    }

    internal void Clear()
    {
        _intent = LocomotionIntent.Idle;
        _hasIntent = false;
        _continuous = false;
        _canHoldOneShot = false;
    }
}
