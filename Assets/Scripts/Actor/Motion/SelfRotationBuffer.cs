using UnityEngine;
using System.Collections.Generic;

public sealed class SelfRotationBuffer
{
    private int _nextOwnerId = 1;

    private readonly List<RotationOwnerState> _owners = new();

    private MotionOwner _tickOwner;
    private Quaternion _tickLocalYawDelta = Quaternion.identity;

    public bool HasActiveOwner => _owners.Count > 0;
    public bool HasTickOwner => _tickOwner.IsValid;
    public Quaternion TickLocalYawDelta => _tickLocalYawDelta;
    public int DebugOwnerCount => _owners.Count;

    public bool TryBegin(out MotionOwner owner)
    {
        owner = NewOwner();
        _owners.Add(new RotationOwnerState(owner, Quaternion.identity));
        return true;
    }

    public bool Submit(MotionOwner owner, Quaternion localYawDelta)
    {
        int index = FindOwnerIndex(owner);
        if (index < 0)
            return false;

        RotationOwnerState state = _owners[index];
        _owners[index] = new RotationOwnerState(owner, state.PendingLocalYawDelta * localYawDelta);
        return true;
    }

    public bool End(MotionOwner owner)
    {
        int index = FindOwnerIndex(owner);
        if (index < 0)
            return false;

        _owners.RemoveAt(index);
        if (IsCurrent(owner, _tickOwner))
        {
            _tickOwner = default;
            _tickLocalYawDelta = Quaternion.identity;
        }

        return true;
    }

    public void BeginMotorTick()
    {
        if (_owners.Count > 0)
        {
            RotationOwnerState top = _owners[_owners.Count - 1];
            _tickOwner = top.Owner;
            _tickLocalYawDelta = top.PendingLocalYawDelta;
        }
        else
        {
            _tickOwner = default;
            _tickLocalYawDelta = Quaternion.identity;
        }

        for (int i = 0; i < _owners.Count; i++)
            _owners[i] = new RotationOwnerState(_owners[i].Owner, Quaternion.identity);
    }

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private int FindOwnerIndex(MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = _owners.Count - 1; i >= 0; i--)
        {
            if (_owners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && owner.Id == current.Id;
    }

    private readonly struct RotationOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly Quaternion PendingLocalYawDelta;

        public RotationOwnerState(MotionOwner owner, Quaternion pendingLocalYawDelta)
        {
            Owner = owner;
            PendingLocalYawDelta = pendingLocalYawDelta;
        }
    }
}
