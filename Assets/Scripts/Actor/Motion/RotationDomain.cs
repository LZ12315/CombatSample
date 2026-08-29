using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActorMotor rotation authority. Runtime arbitration is fixed:
/// Scripted Rotation > Root Rotation > Locomotion Rotation.
/// </summary>
public sealed class RotationDomain
{
    private int _nextOwnerId = 1;
    private readonly List<RotationOwnerState> _rootRotationOwners = new();
    private readonly List<RotationOwnerState> _scriptedRotationOwners = new();

    private MotionOwner _tickRootRotationOwner;
    private Quaternion _tickRootLocalYawDelta = Quaternion.identity;
    private MotionOwner _tickScriptedRotationOwner;
    private Quaternion _tickScriptedLocalYawDelta = Quaternion.identity;

    public Quaternion RequestedRotation { get; private set; } = Quaternion.identity;
    public bool HasRootRotationTick => _tickRootRotationOwner.IsValid;
    public Quaternion RootRotationLocalYawDelta => _tickRootLocalYawDelta;
    public bool HasScriptedRotationTick => _tickScriptedRotationOwner.IsValid;
    public Quaternion ScriptedRotationLocalYawDelta => _tickScriptedLocalYawDelta;
    public int DebugRootRotationOwnerCount => _rootRotationOwners.Count;
    public int DebugScriptedRotationOwnerCount => _scriptedRotationOwners.Count;

    public bool BeginRootRotation(out MotionOwner owner)
    {
        owner = NewOwner();
        _rootRotationOwners.Add(new RotationOwnerState(owner, Quaternion.identity));
        return true;
    }

    public bool SubmitRootRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return Submit(_rootRotationOwners, owner, localYawDelta);
    }

    public bool EndRootRotation(MotionOwner owner)
    {
        return End(_rootRotationOwners, owner, ref _tickRootRotationOwner, ref _tickRootLocalYawDelta);
    }

    public bool BeginScriptedRotation(out MotionOwner owner)
    {
        owner = NewOwner();
        _scriptedRotationOwners.Add(new RotationOwnerState(owner, Quaternion.identity));
        return true;
    }

    public bool SubmitScriptedRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return Submit(_scriptedRotationOwners, owner, localYawDelta);
    }

    public bool EndScriptedRotation(MotionOwner owner)
    {
        return End(_scriptedRotationOwners, owner, ref _tickScriptedRotationOwner, ref _tickScriptedLocalYawDelta);
    }

    public void BeginMotionTick()
    {
        Snapshot(_rootRotationOwners, out _tickRootRotationOwner, out _tickRootLocalYawDelta);
        Snapshot(_scriptedRotationOwners, out _tickScriptedRotationOwner, out _tickScriptedLocalYawDelta);
    }

    public void Prepare(Quaternion tickStartRotation, Quaternion locomotionRotation)
    {
        if (HasScriptedRotationTick)
        {
            RequestedRotation = tickStartRotation * _tickScriptedLocalYawDelta;
            return;
        }

        if (HasRootRotationTick)
        {
            RequestedRotation = tickStartRotation * _tickRootLocalYawDelta;
            return;
        }

        RequestedRotation = locomotionRotation;
    }

    private bool Submit(List<RotationOwnerState> owners, MotionOwner owner, Quaternion localYawDelta)
    {
        int index = FindOwnerIndex(owners, owner);
        if (index < 0)
            return false;

        RotationOwnerState state = owners[index];
        owners[index] = new RotationOwnerState(owner, state.PendingLocalYawDelta * localYawDelta);
        return true;
    }

    private bool End(
        List<RotationOwnerState> owners,
        MotionOwner owner,
        ref MotionOwner tickOwner,
        ref Quaternion tickLocalYawDelta)
    {
        int index = FindOwnerIndex(owners, owner);
        if (index < 0)
            return false;

        owners.RemoveAt(index);
        if (IsCurrent(owner, tickOwner))
        {
            tickOwner = default;
            tickLocalYawDelta = Quaternion.identity;
        }

        return true;
    }

    private static void Snapshot(
        List<RotationOwnerState> owners,
        out MotionOwner tickOwner,
        out Quaternion tickLocalYawDelta)
    {
        if (owners.Count > 0)
        {
            RotationOwnerState top = owners[owners.Count - 1];
            tickOwner = top.Owner;
            tickLocalYawDelta = top.PendingLocalYawDelta;
        }
        else
        {
            tickOwner = default;
            tickLocalYawDelta = Quaternion.identity;
        }

        for (int i = 0; i < owners.Count; i++)
            owners[i] = new RotationOwnerState(owners[i].Owner, Quaternion.identity);
    }

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private static int FindOwnerIndex(List<RotationOwnerState> owners, MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = owners.Count - 1; i >= 0; i--)
        {
            if (owners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && current.IsValid && owner.Id == current.Id;
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
