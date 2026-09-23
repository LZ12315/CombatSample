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
    private bool _hasPreviousUpdateMode;
    private DirectorUpdateMode _previousUpdateMode;

    private AnimancerLayer _baseLayer;
    private AnimancerLayer _actionLayer;
    private AnimancerState _actionState;
    private AnimationClip _actionStateClip;

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
        _actionOwnerActive = false;
        _activeActionOwnerId = 0;
        _hasActionPoseThisTick = false;
        ClearActionLayer();
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
