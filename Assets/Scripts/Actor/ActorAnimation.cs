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
    private AnimancerState _protectionState;
    private readonly List<float> _previousChildWeights = new();
    private readonly Dictionary<AnimancerState, AnimationFootPhaseTrack> _locomotionFootTracks = new();
    private readonly List<AnimancerState> _invalidFootStates = new();

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
        if (_protectionState != null)
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
        _locomotionFootTracks.Clear();
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
        _locomotionFootTracks.Clear();
    }

    internal LocomotionFootPhaseReference GetLocomotionFootPhaseReference(ActorAnimationLocomotionOwner owner)
    {
        if (_baseLayer != null && !_baseLayer.Playable.IsValid())
            _locomotionFootTracks.Clear();
        if (!IsLocomotionOwnerActive(owner) || _actionOwnerActive) return default;

        _invalidFootStates.Clear();
        foreach (var binding in _locomotionFootTracks)
            if (!binding.Key.Playable.IsValid()) _invalidFootStates.Add(binding.Key);
        foreach (var state in _invalidFootStates) _locomotionFootTracks.Remove(state);
        _invalidFootStates.Clear();

        AnimancerState dominant = null;
        float contribution = LocomotionAnimationUtility.WeightEpsilon;
        FindDominantLocomotionLeaf(_baseLayer, _baseLayer.Weight, ref dominant, ref contribution);
        return dominant != null && _locomotionFootTracks.TryGetValue(dominant, out var track)
            && track != null && track.TrySample((float)LocomotionAnimationUtility.EvaluatedTime(dominant), out float phase)
            ? new LocomotionFootPhaseReference(phase) : default;
    }

    private static void FindDominantLocomotionLeaf(AnimancerNode node, float weight,
        ref AnimancerState dominant, ref float contribution)
    {
        if (!node.Playable.IsValid() || !LocomotionDataValidation.IsFinite(weight)
            || weight <= LocomotionAnimationUtility.WeightEpsilon) return;
        if (node.ChildCount == 0)
        {
            if (node is AnimancerState state && LocomotionAnimationUtility.IsUsableClip(state.Clip)
                && weight > contribution)
            { dominant = state; contribution = weight; }
            return;
        }
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child != null) FindDominantLocomotionLeaf(child, weight * child.Weight, ref dominant, ref contribution);
        }
    }

    internal bool SubmitLocomotion(ActorAnimationLocomotionOwner owner, in LocomotionAnimationRequest request)
    {
        if (!IsLocomotionOwnerActive(owner))
            return false;
        if (request.State == null || LocomotionAnimationUtility.WasGraphDestroyed(request.State)
            || !LocomotionDataValidation.IsFinite(request.BlendDuration) || request.BlendDuration < 0f
            || !LocomotionDataValidation.IsFinite(request.Parameter)
            || (request.SampleTime.HasValue && (!LocomotionDataValidation.IsFinite(request.SampleTime.Value)
                || request.SampleTime.Value < 0f))
            || (request.MovePlayback != null && !LocomotionDataValidation.IsFinite(request.PlaybackContext.DeltaTime)))
        {
            HoldLocomotionPose();
            return false;
        }

        AnimancerState state = request.State;
        if (request.MovePlayback != null && request.PlaybackContext.DeltaTime <= 0f)
        {
            request.MovePlayback?.SuspendFeedback();
            return true;
        }
        if (request.FootPhaseBindings != null)
            foreach (var binding in request.FootPhaseBindings)
                if (binding.State != null && !LocomotionAnimationUtility.WasGraphDestroyed(binding.State))
                    _locomotionFootTracks[binding.State] = binding.Track;
        if (state != _locomotionState || _baseLayer.CurrentState != state || request.Restart)
        {
            float fade = _baseLayer.CurrentState == null ? 0f : request.BlendDuration;
            _baseLayer.Play(state, fade);
        }
        state.Speed = 1f;
        if (request.Restart)
            state.TimeD = 0d;
        if (request.SampleTime.HasValue)
        {
            state.TimeD = request.SampleTime.Value;
            state.Speed = 0f;
        }
        if (request.IsMove)
        {
            if (request.MovePlayback != null)
                request.MovePlayback.PrepareMove(request.Parameter, request.PlaybackContext);
            else
                LocomotionMovePlayback.ApplyParameter(state, request.Parameter, _previousChildWeights);
        }
        _locomotionState = state;
        return true;
    }

    private void HoldLocomotionPose()
    {
        if (_baseLayer == null || !_baseLayer.Playable.IsValid())
            return;
        // Capture every contributing state, including both sides of an unfinished fade.
        // Reuse an already protected pose instead of nesting another snapshot around it.
        for (int i = 0; i < _baseLayer.ChildCount; i++)
        {
            var child = _baseLayer.GetChild(i);
            if (child != _protectionState && child.Playable.IsValid()
                && child.Weight > LocomotionAnimationUtility.WeightEpsilon)
            {
                CaptureBasePose(_baseLayer);
                break;
            }
        }
        if (_protectionState != null && !LocomotionAnimationUtility.WasGraphDestroyed(_protectionState))
        {
            _baseLayer.CancelFade();
            _baseLayer.Play(_protectionState);
            FreezePose(_protectionState);
            _locomotionState = null;
        }
    }

    private void CaptureBasePose(AnimancerNode source)
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

    private static AnimancerState CreatePoseSnapshot(AnimancerNode source)
    {
        if (source is AnimancerState state && LocomotionAnimationUtility.IsUsableClip(state.Clip))
            return new ClipState(state.Clip) { TimeD = LocomotionAnimationUtility.EvaluatedTime(state), Speed = 0f };
        if (source.ChildCount == 0)
            return null;
        var snapshot = new ManualMixerState { Speed = 0f };
        for (int i = 0; i < source.ChildCount; i++)
        {
            AnimancerState sourceChild = source.GetChild(i);
            if (!sourceChild.Playable.IsValid()
                || sourceChild.Weight <= LocomotionAnimationUtility.WeightEpsilon)
                continue;
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
        if (snapshot.ChildCount == 0)
        {
            snapshot.Destroy();
            return null;
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

#if UNITY_EDITOR
    internal string DescribeEvaluatedLocomotionLayers()
    {
        if (_baseLayer == null || !_baseLayer.Playable.IsValid())
            return "Layer0=unavailable";
        var text = new System.Text.StringBuilder();
        text.Append($"Layer0Weight={_baseLayer.Weight:F3}, Layer1Weight={_actionLayer?.Weight ?? 0f:F3}");
        for (int i = 0; i < _baseLayer.ChildCount; i++)
        {
            AnimancerState state = _baseLayer.GetChild(i);
            if (state.Weight <= LocomotionAnimationUtility.WeightEpsilon)
                continue;
            string label = state.Clip != null ? state.Clip.name : state.GetType().Name;
            text.Append($"; {label}[weight={state.Weight:F3}, time={state.TimeD:F3}, speed={state.Speed:F3}, playing={state.IsPlaying}]");
            for (int j = 0; j < state.ChildCount; j++)
            {
                AnimancerState child = state.GetChild(j);
                if (child.Weight > LocomotionAnimationUtility.WeightEpsilon)
                    text.Append($" {child.Clip?.name}[weight={child.Weight:F3}, time={child.TimeD:F3}/{child.Length:F3}, speed={child.Speed:F3}, loop={child.IsLooping}]");
            }
        }
        return text.ToString();
    }
#endif

    private bool IsActiveOwner(ActorAnimationActionOwner owner)
    {
        return owner.IsValid
               && _actionOwnerActive
               && owner.Id == _activeActionOwnerId;
    }

    private void ClearAnimationState()
    {
        LoseLocomotionSession();
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
        _baseLayer = null;
        _actionLayer = null;
        _locomotionFootTracks.Clear();
    }

    private void LoseLocomotionSession()
    {
        var owner = new ActorAnimationLocomotionOwner(_activeLocomotionOwnerId);
        EndLocomotionSession(owner);
        actor?.actorLocomotion?.OnAnimationSessionLost(this, owner);
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
            LoseLocomotionSession();
            _locomotionFootTracks.Clear();
            _activeLocomotionOwnerId = 0;
            _locomotionState = null;
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
