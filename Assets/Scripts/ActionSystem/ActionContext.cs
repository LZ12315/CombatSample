using System;
using UnityEngine;

[Flags]
public enum ActionContextFieldMask
{
    None = 0,
    Direction = 1 << 0,
    Point = 1 << 1,
    Magnitude = 1 << 2,
    Instigator = 1 << 3,
    Target = 1 << 4,
}

/// <summary>
/// Immutable startup snapshot for one Action play.
/// The data is neutral: each Action, Timeline clip, or Sequence clip decides how to interpret it.
/// </summary>
public readonly struct ActionContext
{
    private const float DirectionEpsilonSqr = 0.0001f;

    private readonly GameObject _instigator;
    private readonly GameObject _target;
    private readonly Vector3 _point;
    private readonly Vector3 _direction;
    private readonly float _magnitude;
    private readonly ActionContextFieldMask _fields;

    public ActionContext(GameObject instigator, GameObject target)
        : this(instigator, target, Vector3.zero, Vector3.zero, 0f, ActionContextFieldMask.None)
    {
    }

    private ActionContext(
        GameObject instigator,
        GameObject target,
        Vector3 point,
        Vector3 direction,
        float magnitude,
        ActionContextFieldMask fields)
    {
        _instigator = instigator;
        _target = target;
        _point = point;
        _direction = direction;
        _magnitude = magnitude;
        _fields = fields;
    }

    public static ActionContext None => default;

    public static ActionContext ForSelf(Actor actor)
    {
        GameObject owner = actor != null ? actor.gameObject : null;
        return owner != null ? new ActionContext(owner, owner) : default;
    }

    public static ActionContext ForSelf(GameObject owner)
    {
        return owner != null ? new ActionContext(owner, owner) : default;
    }

    public static ActionContext ForParticipants(GameObject instigator, GameObject target)
    {
        return new ActionContext(instigator, target);
    }

    public GameObject Instigator => _instigator;
    public GameObject Target => _target;
    public Vector3 Point => HasPoint ? _point : Vector3.zero;
    public Vector3 Direction => HasDirection ? _direction : Vector3.zero;
    public float Magnitude => HasMagnitude ? _magnitude : 0f;
    public ActionContextFieldMask Fields => _fields | ReferenceFields;

    public bool HasInstigator => _instigator != null;
    public bool HasTarget => _target != null;
    public bool HasDirection => (_fields & ActionContextFieldMask.Direction) != 0;
    public bool HasPoint => (_fields & ActionContextFieldMask.Point) != 0;
    public bool HasMagnitude => (_fields & ActionContextFieldMask.Magnitude) != 0;

    public bool IsValid =>
        _instigator != null ||
        _target != null ||
        _fields != ActionContextFieldMask.None;

    private ActionContextFieldMask ReferenceFields
    {
        get
        {
            ActionContextFieldMask fields = ActionContextFieldMask.None;
            if (_instigator != null)
                fields |= ActionContextFieldMask.Instigator;
            if (_target != null)
                fields |= ActionContextFieldMask.Target;
            return fields;
        }
    }

    public ActionContext WithDirection(Vector3 worldDirection)
    {
        if (!IsFinite(worldDirection))
            throw new ArgumentException("ActionContext direction must be finite.", nameof(worldDirection));

        if (worldDirection.sqrMagnitude <= DirectionEpsilonSqr)
            throw new ArgumentException("ActionContext direction cannot be zero.", nameof(worldDirection));

        return new ActionContext(
            _instigator,
            _target,
            _point,
            worldDirection.normalized,
            _magnitude,
            _fields | ActionContextFieldMask.Direction);
    }

    public ActionContext WithPoint(Vector3 worldPoint)
    {
        if (!IsFinite(worldPoint))
            throw new ArgumentException("ActionContext point must be finite.", nameof(worldPoint));

        return new ActionContext(
            _instigator,
            _target,
            worldPoint,
            _direction,
            _magnitude,
            _fields | ActionContextFieldMask.Point);
    }

    public ActionContext WithMagnitude(float magnitude)
    {
        if (!IsFinite(magnitude))
            throw new ArgumentException("ActionContext magnitude must be finite.", nameof(magnitude));

        return new ActionContext(
            _instigator,
            _target,
            _point,
            _direction,
            magnitude,
            _fields | ActionContextFieldMask.Magnitude);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
