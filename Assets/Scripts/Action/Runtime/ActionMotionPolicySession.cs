using System;
using System.Collections.Generic;

/// <summary>
/// One execution's immutable policy snapshot. Windows replace defaults per field before external
/// Motor constraints are composed. Sampling action frames also keeps policy active at fractional speed/pause.
/// </summary>
internal sealed class ActionMotionPolicySession : IDisposable
{
    private readonly MotionPolicyValues _defaults;
    private readonly List<Window> _windows = new();
    private readonly ActorMotor _motor;
    private readonly bool _needsOwner;
    private MotionOwner _owner;
    private bool _disposed;

    internal ActionMotionPolicySession(ActionAsset asset, ActorMotor motor)
    {
        _motor = motor;
        _defaults = asset.MotionPolicy?.Capture() ?? MotionPolicyValues.Neutral;
        var lanes = asset.Timeline?.GameplayLanes;
        for (int l = 0; lanes != null && l < lanes.Count; l++)
        {
            var lane = lanes[l];
            if (lane == null || lane.Muted) continue;
            var items = lane.Items;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is not MotionPolicyItem item || item.Muted || item.StartFrame < 0
                    || item.DurationFrames <= 0 || item.EndFrameExclusiveLong > int.MaxValue) continue;
                var policy = new ActionMotionPolicyOverride(item.Config);
                if (!policy.HasAny) continue; // All-Inherit is a valid, side-effect-free authoring state.
                _windows.Add(new Window(item.StartFrame, (int)item.EndFrameExclusiveLong, policy));
            }
        }
        // Canonical product order: lane order must not affect floating-point gravity resolution.
        _windows.Sort((a, b) => a.Policy.Values.GravityScale.CompareTo(b.Policy.Values.GravityScale));
        _needsOwner = !_defaults.IsNeutral || _windows.Count > 0;
        if (_needsOwner && motor == null)
            throw new InvalidOperationException("Action Motion Policy requires ActorMotor.");
    }

    internal MotionPolicyValues Resolve(int frame)
    {
        float movement = 1f, air = 1f, turnLimit = 0f;
        double gravityLog = 0d;
        bool brake = false, lockedTurn = false, hasMove = false, hasAir = false, hasGravity = false, hasTurn = false;
        foreach (var window in _windows)
        {
            if (frame < window.Start || frame >= window.End) continue;
            var p = window.Policy;
            var v = p.Values;
            if (p.Movement)
            {
                hasMove = true;
                movement = Math.Min(movement, v.LocomotionScale);
                brake |= v.BrakeLocomotion;
            }
            if (p.Turning)
            {
                hasTurn = true;
                lockedTurn |= v.LockLocomotionRotation;
                turnLimit = MotionPolicyValues.CombineTurnLimit(turnLimit, v.TurnSpeedLimit);
            }
            if (p.Air) { hasAir = true; air = Math.Min(air, v.AirLocomotionScale); }
            if (p.Gravity) { hasGravity = true; gravityLog += window.GravityLog; }
        }
        // Log space avoids intermediate overflow; a zero factor remains zero even beside huge factors.
        float gravity = hasGravity ? (float)Math.Exp(gravityLog) : 1f;
        if (!ActionAuthoringMath.IsFinite(gravity))
            throw new InvalidOperationException("Overlapping gravity policy windows overflowed.");
        return new MotionPolicyValues(hasMove ? movement : _defaults.LocomotionScale,
            hasAir ? air : _defaults.AirLocomotionScale, hasGravity ? gravity : _defaults.GravityScale,
            hasMove ? brake : _defaults.BrakeLocomotion, hasTurn ? lockedTurn : _defaults.LockLocomotionRotation,
            hasTurn ? turnLimit : _defaults.TurnSpeedLimit);
    }

    internal void ApplyFrame(int frame)
    {
        if (_disposed || !_needsOwner) return;
        var values = Resolve(frame);
        if (!_owner.IsValid) _owner = _motor.MotionPolicy.BeginActionPolicy(values);
        else if (!_motor.MotionPolicy.UpdateActionPolicy(_owner, values))
            throw new InvalidOperationException("Action Motion Policy owner became invalid.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_owner.IsValid && _motor != null) _motor.MotionPolicy.EndActionPolicy(_owner);
        _owner = default;
    }

    private readonly struct Window
    {
        internal readonly int Start, End;
        internal readonly ActionMotionPolicyOverride Policy;
        internal readonly double GravityLog;
        internal Window(int start, int end, ActionMotionPolicyOverride policy)
        {
            Start = start; End = end; Policy = policy;
            GravityLog = policy.Gravity ? Math.Log(policy.Values.GravityScale) : 0d;
        }
    }
}
