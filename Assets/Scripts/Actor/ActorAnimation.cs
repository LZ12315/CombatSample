using Animancer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class ActorAnimation : MonoBehaviour
{
    private const int LocomotionBaseLayerIndex = 0;
    private const int ActionOverrideLayerIndex = 1;

    [SerializeField] private Actor actor;
    [SerializeField] private AnimancerComponent animancer;

    private int _nextActionOwnerId;
    private int _activeActionOwnerId;
    private bool _actionOwnerActive;
    private bool _hasActionPoseThisTick;
    private bool _hasPreviousUpdateMode;
    private DirectorUpdateMode _previousUpdateMode;

    private AnimancerLayer _baseLayer;
    private AnimancerLayer _actionLayer;
    private AnimancerState _actionState;
    private AnimationClip _actionStateClip;
    private int _nextLocomotionOwnerId;
    private int _activeLocomotionOwnerId;
    private AnimancerState _locomotionState;
    private AnimancerState _lastMoveState;
    private AnimancerState _protectionState;
    private AnimationClip _idleClip;
    private readonly List<float> _previousChildWeights = new();
    private bool _warnedMissingBasePose;

    public bool HasActiveActionOwner => _actionOwnerActive;
    internal int ActiveActionOwnerId => _actionOwnerActive ? _activeActionOwnerId : 0;

    private void Awake()
    {
        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
    }

    private void OnDisable()
    {
        ClearAnimationState();
        RestoreUpdateMode();
    }

    private void OnDestroy()
    {
        ClearAnimationState();
        RestoreUpdateMode();
        LocomotionAnimationUtility.Destroy(_protectionState);
        _protectionState = null;
    }

    internal void Bind(Actor owner)
    {
        if (owner != null)
            actor = owner;

        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
        if (_idleClip != null || _protectionState != null)
            HoldLocomotionPose();
    }

    internal ActorAnimationLocomotionOwner BeginLocomotionSession()
    {
        if (!isActiveAndEnabled)
            return default;
        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
        if (_baseLayer == null)
            return default;
        if (_activeLocomotionOwnerId != 0)
            HoldLocomotionPose();
        _activeLocomotionOwnerId = ++_nextLocomotionOwnerId;
        return new ActorAnimationLocomotionOwner(_activeLocomotionOwnerId);
    }

    internal bool IsLocomotionOwnerActive(ActorAnimationLocomotionOwner owner) =>
        isActiveAndEnabled && owner.IsValid && owner.Id == _activeLocomotionOwnerId
        && _baseLayer != null && _baseLayer.Playable.IsValid();

    internal void EndLocomotionSession(ActorAnimationLocomotionOwner owner)
    {
        if (!owner.IsValid || owner.Id != _activeLocomotionOwnerId)
            return;
        HoldLocomotionPose();
        _activeLocomotionOwnerId = 0;
        _locomotionState = null;
        _lastMoveState = null;
    }

    internal bool SubmitLocomotion(ActorAnimationLocomotionOwner owner, in LocomotionAnimationRequest request)
    {
        if (!IsLocomotionOwnerActive(owner))
            return false;
        if (LocomotionAnimationUtility.IsUsableClip(request.IdleClip))
            _idleClip = request.IdleClip;
        if (request.State == null || LocomotionAnimationUtility.WasGraphDestroyed(request.State)
            || !LocomotionDataValidation.IsFinite(request.BlendDuration) || request.BlendDuration < 0f
            || !LocomotionDataValidation.IsFinite(request.Parameter)
            || (request.EntryPhase.HasValue && (!LocomotionDataValidation.IsFinite(request.EntryPhase.Value)
                || request.EntryPhase.Value < 0f || request.EntryPhase.Value > 1f))
            || (request.MovePlayback != null && !LocomotionDataValidation.IsFinite(request.PlaybackContext.DeltaTime)))
        {
            HoldLocomotionPose();
            return false;
        }

        AnimancerState state = request.State;
        if (request.MovePlayback != null && request.PlaybackContext.DeltaTime <= 0f)
        {
            request.MovePlayback.SuspendFeedback();
            return true;
        }
        if (state != _locomotionState || _baseLayer.CurrentState != state || request.Restart)
        {
            if (!request.IsMove && _idleClip == null && _lastMoveState != null
                && _lastMoveState.IsPlaying && _lastMoveState.Weight > LocomotionAnimationUtility.WeightEpsilon)
                CaptureBasePose(_lastMoveState);
            float fade = _baseLayer.CurrentState == null ? 0f : request.BlendDuration;
            _baseLayer.Play(state, fade);
        }
        state.Speed = 1f;
        if (request.Restart)
            state.TimeD = 0d;
        if (request.IsMove)
        {
            ApplyMixerParameter(state, request.Parameter);
            request.MovePlayback?.Prepare(request.PlaybackContext, request.EntryPhase);
            // Rebind the hook even when a cached state re-enters with the same parameter.
            if (request.MovePlayback != null && state is ManualMixerState mixer)
                state.Graph.RequirePreUpdate(mixer);
            _lastMoveState = state;
        }
        _locomotionState = state;
        _warnedMissingBasePose = false;
        return true;
    }

    private void ApplyMixerParameter(AnimancerState state, Vector2 parameter)
    {
        _previousChildWeights.Clear();
        for (int i = 0; i < state.ChildCount; i++)
            _previousChildWeights.Add(state.GetChild(i).Weight);
        if (state is LinearMixerState linear)
        {
            linear.Parameter = parameter.x;
            linear.RecalculateWeights();
        }
        else if (state is DirectionalMixerState directional)
        {
            directional.Parameter = parameter;
            directional.RecalculateWeights();
        }
        for (int i = 0; i < state.ChildCount; i++)
        {
            AnimancerState child = state.GetChild(i);
            if (!child.IsLooping && _previousChildWeights[i] <= LocomotionAnimationUtility.WeightEpsilon
                && child.Weight > LocomotionAnimationUtility.WeightEpsilon)
                child.TimeD = 0d;
        }
    }

    private void HoldLocomotionPose()
    {
        if (_baseLayer == null || !_baseLayer.Playable.IsValid())
            return;
        if (LocomotionAnimationUtility.IsUsableClip(_idleClip))
        {
            if (LocomotionAnimationUtility.WasGraphDestroyed(_protectionState)
                || _protectionState?.Clip != _idleClip)
            {
                AnimancerState previous = _protectionState;
                _protectionState = new ClipState(_idleClip);
                _baseLayer.Play(_protectionState);
                LocomotionAnimationUtility.Destroy(previous);
            }
        }
        else if (_lastMoveState != null && _lastMoveState.IsPlaying
            && _lastMoveState.Weight > LocomotionAnimationUtility.WeightEpsilon)
        {
            CaptureBasePose(_lastMoveState);
        }
        else if (_protectionState == null || LocomotionAnimationUtility.WasGraphDestroyed(_protectionState))
        {
            CaptureBasePose(_lastMoveState ?? _locomotionState ?? _baseLayer.CurrentState);
        }
        if (_protectionState != null && !LocomotionAnimationUtility.WasGraphDestroyed(_protectionState))
        {
            _baseLayer.CancelFade();
            _baseLayer.Play(_protectionState);
            FreezePose(_protectionState);
            _locomotionState = null;
        }
        else if (!_warnedMissingBasePose)
        {
            Debug.LogWarning("[ActorLocomotion] Cannot establish a valid Layer 0 base pose: " +
                "no valid Idle sample or previously played base state is available.", this);
            _warnedMissingBasePose = true;
        }
    }

    private void CaptureBasePose(AnimancerState source)
    {
        if (source == null || !source.Playable.IsValid())
            return;
        // Copy only pose data, not fades, events, synchronization or references to Runtime-owned states.
        AnimancerState snapshot = CreatePoseSnapshot(source);
        if (snapshot == null)
            return;
        LocomotionAnimationUtility.Destroy(_protectionState);
        _protectionState = snapshot;
        FreezePose(snapshot);
    }

    private static AnimancerState CreatePoseSnapshot(AnimancerState source)
    {
        if (LocomotionAnimationUtility.IsUsableClip(source.Clip))
            return new ClipState(source.Clip) { TimeD = source.TimeD, Speed = 0f };
        if (source.ChildCount == 0)
            return null;
        var snapshot = new ManualMixerState { Speed = 0f };
        for (int i = 0; i < source.ChildCount; i++)
        {
            AnimancerState sourceChild = source.GetChild(i);
            AnimancerState child = CreatePoseSnapshot(sourceChild);
            if (child == null)
            {
                snapshot.Destroy();
                return null;
            }
            snapshot.Add(child);
            snapshot.DontSynchronize(child);
            child.Weight = sourceChild.Weight;
        }
        return snapshot;
    }

    private static void FreezePose(AnimancerState state)
    {
        state.CancelFade();
        state.Speed = 0f;
        if (state is ManualMixerState mixer)
            mixer.DontSynchronizeChildren();
        for (int i = 0; i < state.ChildCount; i++)
            FreezePose(state.GetChild(i));
    }

    internal ActorAnimationActionOwner BeginActionOverride()
    {
        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();

        _actionOwnerActive = true;
        _activeActionOwnerId = ++_nextActionOwnerId;
        _hasActionPoseThisTick = false;
        return new ActorAnimationActionOwner(_activeActionOwnerId);
    }

    internal void EndActionOverride(ActorAnimationActionOwner owner)
    {
        if (!IsActiveOwner(owner))
            return;

        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
    }

    internal void BeginFixedAnimationTick()
    {
        _hasActionPoseThisTick = false;
    }

    internal void CancelFixedAnimationTick()
    {
        EndLocomotionSession(new ActorAnimationLocomotionOwner(_activeLocomotionOwnerId));
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
    }

    /// <summary>Submits an ActionRuntime pose directly from its AnimationClip.</summary>
    internal bool SubmitActionClipPose(ActorAnimationActionOwner owner, AnimationClip clip, float sampleTime)
    {
        if (!IsActiveOwner(owner) || clip == null || float.IsNaN(sampleTime) || float.IsInfinity(sampleTime))
            return false;

        if (_hasActionPoseThisTick)
        {
            Debug.LogError(
                "[ActorAnimation] Multiple Action Pose submissions were received for one Actor in the same combat tick. " +
                "Animation pose overlap is an authoring error.",
                this);
            return false;
        }

        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
        if (_actionLayer == null)
            return false;

        if (_actionState == null || _actionStateClip != clip)
        {
            _actionState = _actionLayer.Play(clip);
            _actionStateClip = clip;
        }

        if (_actionState == null)
            return false;

        _actionState.Speed = 0f;
        _actionState.Time = Mathf.Clamp(sampleTime, 0f, clip.length);
        _actionState.IsPlaying = true;
        _hasActionPoseThisTick = true;
        return true;
    }

    internal void Evaluate(float deltaSeconds)
    {
        ResolveDependencies();
        if (animancer == null)
            return;

        ApplyManualUpdateMode();
        EnsureLayers();

        if (!_actionOwnerActive || !_hasActionPoseThisTick)
            ClearActionLayer();

        animancer.Evaluate(Mathf.Max(0f, deltaSeconds));
    }

    private bool IsActiveOwner(ActorAnimationActionOwner owner)
    {
        return owner.IsValid
               && _actionOwnerActive
               && owner.Id == _activeActionOwnerId;
    }

    private void ClearAnimationState()
    {
        EndLocomotionSession(new ActorAnimationLocomotionOwner(_activeLocomotionOwnerId));
        actor?.actorLocomotion?.ResetAnimationSession();
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
        _baseLayer = null;
        _actionLayer = null;
    }

    private void ClearActionLayer()
    {
        if (_actionLayer != null && _actionLayer.Playable.IsValid())
        {
            _actionLayer.CancelFade();
            _actionLayer.Stop();
        }

        _actionState = null;
        _actionStateClip = null;
    }

    private void ResolveDependencies()
    {
        if (actor == null)
            actor = GetComponent<Actor>();

        if (animancer == null)
        {
            animancer = actor != null && actor.animancer != null
                ? actor.animancer
                : GetComponentInChildren<AnimancerComponent>();
        }
    }

    private void EnsureLayers()
    {
        if (animancer == null)
            return;

        animancer.Layers.SetMinCount(ActionOverrideLayerIndex + 1);
        if (_baseLayer != null && _baseLayer != animancer.Layers[LocomotionBaseLayerIndex])
        {
            _activeLocomotionOwnerId = 0;
            _locomotionState = null;
            _lastMoveState = null;
            _protectionState = null;
            _actionState = null;
            _actionStateClip = null;
        }
        _baseLayer = animancer.Layers[LocomotionBaseLayerIndex];
        _actionLayer = animancer.Layers[ActionOverrideLayerIndex];
        if (_baseLayer != null && _baseLayer.Weight <= 0f)
            _baseLayer.StartFade(1f, 0f);
        if (_actionLayer != null && _actionState == null && _actionLayer.Weight > 0f)
            _actionLayer.Stop();
    }

    private void ApplyManualUpdateMode()
    {
        if (animancer == null)
            return;

        if (!_hasPreviousUpdateMode)
        {
            _previousUpdateMode = animancer.Graph.UpdateMode;
            _hasPreviousUpdateMode = true;
        }

        if (animancer.Graph.UpdateMode != DirectorUpdateMode.Manual)
            animancer.Graph.UpdateMode = DirectorUpdateMode.Manual;
    }

    private void RestoreUpdateMode()
    {
        if (animancer == null || !_hasPreviousUpdateMode)
            return;

        if (animancer.Graph.IsValidOrDispose())
            animancer.Graph.UpdateMode = _previousUpdateMode;

        _hasPreviousUpdateMode = false;
    }
}

public readonly struct ActorAnimationActionOwner
{
    internal ActorAnimationActionOwner(int id)
    {
        Id = id;
    }

    internal int Id { get; }
    public bool IsValid => Id != 0;
}
