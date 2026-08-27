using System;
using Animancer;
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
    private bool _reportedMissingAnimancer;
    private bool _reportedMissingConfig;
    private bool _hasPreviousUpdateMode;
    private DirectorUpdateMode _previousUpdateMode;

    private AnimancerLayer _baseLayer;
    private AnimancerLayer _actionLayer;
    private AnimancerState _baseState;
    private AnimancerState _actionState;
    private string _baseStateKey;
    private string _actionStateKey;

    public bool HasActiveActionOwner => _actionOwnerActive;

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
    }

    internal void Bind(Actor owner)
    {
        if (owner != null)
            actor = owner;

        ResolveDependencies();
        ApplyManualUpdateMode();
        EnsureLayers();
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
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
    }

    internal bool SetLocomotionBase(LocomotionAnimationPose pose)
    {
        if (string.IsNullOrWhiteSpace(pose.AnimationKey))
            return false;

        ResolveDependencies();
        if (!TryResolveAnimationConfig(out AnimationConfig config))
            return false;

        if (!TryResolveTransition(config, pose.AnimationKey, "Locomotion Base", out TransitionAsset transitionAsset))
            return false;

        ApplyManualUpdateMode();
        EnsureLayers();
        if (_baseLayer == null)
            return false;

        bool shouldPlay = _baseState == null
                          || _baseLayer.CurrentState != _baseState
                          || !_baseState.IsPlaying
                          || !string.Equals(_baseStateKey, pose.AnimationKey, StringComparison.Ordinal);

        if (shouldPlay)
        {
            _baseState = _baseLayer.Play(transitionAsset.Transition);
            _baseStateKey = pose.AnimationKey;
        }

        if (_baseState == null)
            return false;

        ApplyMixerParameter(_baseState, pose);
        _baseState.Speed = 1f;
        _baseState.IsPlaying = true;
        return true;
    }

    internal bool SubmitActionPose(ActorAnimationActionOwner owner, ActionAnimationPose pose)
    {
        if (!IsActiveOwner(owner))
            return false;

        if (_hasActionPoseThisTick)
        {
            Debug.LogError(
                "[ActorAnimation] Multiple Action Pose submissions were received for one Actor in the same combat tick. " +
                "AnimationPoseClip overlap is an authoring error.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(pose.AnimationKey))
            return false;

        ResolveDependencies();
        if (!TryResolveAnimationConfig(out AnimationConfig config))
            return false;

        if (!TryResolveTransition(config, pose.AnimationKey, "Action Pose", out TransitionAsset transitionAsset))
            return false;

        ApplyManualUpdateMode();
        EnsureLayers();
        if (_actionLayer == null)
            return false;

        if (_actionState == null || !string.Equals(_actionStateKey, pose.AnimationKey, StringComparison.Ordinal))
        {
            _actionState = _actionLayer.Play(transitionAsset.Transition);
            _actionStateKey = pose.AnimationKey;
        }

        if (_actionState == null)
            return false;

        ApplyMixerParameter(_actionState, pose);
        _actionState.Speed = 0f;
        _actionState.Time = Mathf.Max(0f, pose.SampleTime);
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

    private bool TryResolveAnimationConfig(out AnimationConfig config)
    {
        config = actor != null ? actor.AnimationConfig : null;
        if (animancer == null)
        {
            if (!_reportedMissingAnimancer)
            {
                Debug.LogWarning("[ActorAnimation] Cannot resolve animation because AnimancerComponent is missing.", this);
                _reportedMissingAnimancer = true;
            }

            return false;
        }

        if (config == null)
        {
            if (!_reportedMissingConfig)
            {
                Debug.LogWarning("[ActorAnimation] Cannot resolve animation because Actor.AnimationConfig is missing.", this);
                _reportedMissingConfig = true;
            }

            return false;
        }

        return true;
    }

    private bool TryResolveTransition(
        AnimationConfig config,
        string animationKey,
        string role,
        out TransitionAsset transitionAsset)
    {
        transitionAsset = null;
        if (config == null)
            return false;

        if (config.TryGetTransition(animationKey, out transitionAsset)
            && transitionAsset != null
            && transitionAsset.Transition != null)
        {
            return true;
        }

        Debug.LogWarning($"[ActorAnimation] Cannot resolve {role} animation key '{animationKey}'.", this);
        return false;
    }

    private void ClearAnimationState()
    {
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
        _baseState = null;
        _baseStateKey = null;
        _baseLayer = null;
        _actionLayer = null;
    }

    private void ClearActionLayer()
    {
        if (_actionLayer != null)
        {
            _actionLayer.CancelFade();
            _actionLayer.Stop();
        }

        _actionState = null;
        _actionStateKey = null;
    }

    private void ApplyMixerParameter(AnimancerState state, ActionAnimationPose pose)
    {
        if (state is MixerState<Vector2> mixer2D && pose.HasVector2Parameter)
        {
            mixer2D.Parameter = pose.Vector2Parameter;
        }
        else if (state is MixerState<float> mixer1D && pose.HasFloatParameter)
        {
            mixer1D.Parameter = pose.FloatParameter;
        }
    }

    private void ApplyMixerParameter(AnimancerState state, LocomotionAnimationPose pose)
    {
        if (state is MixerState<Vector2> mixer2D && pose.HasVector2Parameter)
        {
            mixer2D.Parameter = pose.Vector2Parameter;
        }
        else if (state is MixerState<float> mixer1D && pose.HasFloatParameter)
        {
            mixer1D.Parameter = pose.FloatParameter;
        }
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

public readonly struct ActionAnimationPose
{
    public ActionAnimationPose(
        string animationKey,
        float sampleTime,
        bool hasVector2Parameter,
        Vector2 vector2Parameter,
        bool hasFloatParameter,
        float floatParameter)
    {
        AnimationKey = animationKey ?? string.Empty;
        SampleTime = sampleTime;
        HasVector2Parameter = hasVector2Parameter;
        Vector2Parameter = vector2Parameter;
        HasFloatParameter = hasFloatParameter;
        FloatParameter = floatParameter;
    }

    public string AnimationKey { get; }
    public float SampleTime { get; }
    public bool HasVector2Parameter { get; }
    public Vector2 Vector2Parameter { get; }
    public bool HasFloatParameter { get; }
    public float FloatParameter { get; }
}

public readonly struct LocomotionAnimationPose
{
    public LocomotionAnimationPose(
        string animationKey,
        bool hasVector2Parameter,
        Vector2 vector2Parameter,
        bool hasFloatParameter,
        float floatParameter)
    {
        AnimationKey = animationKey ?? string.Empty;
        HasVector2Parameter = hasVector2Parameter;
        Vector2Parameter = vector2Parameter;
        HasFloatParameter = hasFloatParameter;
        FloatParameter = floatParameter;
    }

    public string AnimationKey { get; }
    public bool HasVector2Parameter { get; }
    public Vector2 Vector2Parameter { get; }
    public bool HasFloatParameter { get; }
    public float FloatParameter { get; }
}
