using UnityEngine;

public sealed class SelfRotationBuffer
{
    private int _nextOwnerId = 1;

    private MotionOwner _owner;
    private Quaternion _pendingLocalYawDelta = Quaternion.identity;

    private MotionOwner _tickOwner;
    private Quaternion _tickLocalYawDelta = Quaternion.identity;

    public bool HasActiveOwner => _owner.IsValid;
    public bool HasTickOwner => _tickOwner.IsValid;
    public Quaternion TickLocalYawDelta => _tickLocalYawDelta;

    public bool TryBegin(out MotionOwner owner)
    {
        owner = default;
        if (_owner.IsValid)
            return false;

        _owner = NewOwner();
        _pendingLocalYawDelta = Quaternion.identity;
        owner = _owner;
        return true;
    }

    public bool Submit(MotionOwner owner, Quaternion localYawDelta)
    {
        if (!IsCurrent(owner, _owner))
            return false;

        _pendingLocalYawDelta = _pendingLocalYawDelta * localYawDelta;
        return true;
    }

    public bool End(MotionOwner owner)
    {
        if (!IsCurrent(owner, _owner))
            return false;

        _owner = default;
        _pendingLocalYawDelta = Quaternion.identity;
        _tickOwner = default;
        _tickLocalYawDelta = Quaternion.identity;
        return true;
    }

    public void BeginMotorTick()
    {
        _tickOwner = _owner;
        _tickLocalYawDelta = _pendingLocalYawDelta;
        _pendingLocalYawDelta = Quaternion.identity;
    }

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && owner.Id == current.Id;
    }
}
