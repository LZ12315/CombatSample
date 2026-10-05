using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed partial class ActorLocomotion : MonoBehaviour
{
    [SerializeField] private Actor actor;
    [SerializeField, FormerlySerializedAs("candidateModes")]
    private List<LocomotionAsset> locomotionAssets = new();
    [SerializeField, HideInInspector, FormerlySerializedAs("fallbackMode")]
    private LocomotionAsset legacyFallbackMode;

    private readonly List<Tag> _acquiredTags = new();
    private readonly HashSet<LocomotionAsset> _candidateScanSet = new();
    private readonly Dictionary<LocomotionAsset, LocomotionRuntime> _runtimes = new();
    private readonly LocomotionRunner _runner = new();
    private readonly LocomotionIntentBuffer _input = new();
    private LocomotionTickState _tick;
    private LocomotionRuntime _currentRuntime;
    private ActorAnimation _animation;
    private ActorAnimationLocomotionOwner _animationOwner;

    public LocomotionAsset CurrentAsset { get; private set; }
    public LocomotionMovementConfig CurrentMovementConfig { get; private set; } = LocomotionMovementConfig.Default;
    public LocomotionIntent EffectiveIntent => _runner.EffectiveIntent;
    public Vector3 DebugLocomotionVelocity => _runner.CachedVelocity;
    public float DebugLocomotionTargetYaw => _runner.TargetRotationYaw;

    private void Awake()
    {
        MigrateLegacyFallback();
        ResolveActor();
        _runner.Initialize(transform.rotation);
    }

    private void OnEnable()
    {
        _runner.SyncRotation(transform.rotation);
    }

    private void OnDisable()
    {
        CancelSimulation();
    }

    private void OnDestroy()
    {
        CancelSimulation();
    }

    internal void Bind(Actor owner)
    {
        if (owner != null)
            actor = owner;

        ResolveActor();
    }

    public void SetLocomotionIntent(in LocomotionIntent intent)
    {
        if (!isActiveAndEnabled)
            return;

        _input.Submit(intent);
    }

    /// <summary>Releases pending input. The locked tick remains unchanged; model velocity brakes through Motion.</summary>
    public void ClearLocomotionIntent() => _input.Clear();

    // Input lifetime is independent of the producer's update clock. One controller per Actor.
    internal void SetContinuousLocomotionIntent(in LocomotionIntent intent)
    {
        if (!isActiveAndEnabled)
            return;
        _input.Submit(intent, continuous: true);
    }

    internal void ReleaseContinuousLocomotionIntent() => _input.ReleaseContinuous();

    public bool TryGetControlIntent(out LocomotionIntent intent)
    {
        intent = _tick.IsControlLocked && _tick.HasControlIntent ? _tick.ControlIntent : LocomotionIntent.Idle;
        return _tick.IsControlLocked && _tick.HasControlIntent;
    }

    internal void SyncRotation(Quaternion rotation) => _runner.SyncRotation(rotation);

    /// <summary>Locks the one Intent snapshot shared by Action, selection and Motion for this tick.</summary>
    public void BeginControlTick()
    {
        ResolveActor();
        if (actor == null)
            return;

        bool hasIntent = _input.Capture(out var intent, out bool continuous);
        _tick.Begin(intent, hasIntent, continuous);
    }

    public LocomotionMotionRequest BuildMotionRequest(in LocomotionMotionContext context)
    {
        if (_tick.IsMotionPrepared)
            return _tick.MotionRequest;

        if (context.EffectiveDeltaTime <= 0f)
        {
            HoldControlTick();
            return default;
        }

        ResolveActor();
        LocomotionIntent intent = _tick.IsControlLocked ? _tick.ControlIntent : LocomotionIntent.Idle;
        bool hasIntent = _tick.IsControlLocked && _tick.HasControlIntent;
        LocomotionAsset selected = actor != null
            ? SelectAsset(new LocomotionSelectionContext(actor, intent, context))
            : null;
        if (selected != CurrentAsset)
            ApplyAsset(selected);

        LocomotionMotionRequest request = default;
        LocomotionMotionSnapshot? snapshot = null;
        if (_currentRuntime != null)
        {
            Vector3 before = _runner.CachedVelocity;
            var runtimeContext = new LocomotionRuntimeMotionContext(_runner, intent, hasIntent, context);
            request = _currentRuntime.UpdateMotion(runtimeContext);
            snapshot = new LocomotionMotionSnapshot(intent, hasIntent, before, _runner.CachedVelocity, context);
        }
        else
        {
            _runner.ClearIntent();
        }

        _tick.CompleteMotion(request, snapshot);
        return _tick.MotionRequest;
    }

    internal void HoldControlTick()
    {
        if (_tick.IsControlLocked && _tick.HasControlIntent)
            _input.Hold(_tick.ControlIntent, _tick.FromContinuous);
        _tick = default;
    }

    internal void CancelControlTick()
    {
        ClearLocomotionIntent();
        _tick = default;
    }

    internal void CancelSimulation()
    {
#if UNITY_EDITOR
        ClearDebugTrace();
#endif
        ClearCurrentAsset();
        DisposeRuntimes();
        CancelControlTick();
        _runner.ClearIntent();
    }

    internal void ResetAnimationSession()
    {
        EndAnimationSession();
        _currentRuntime?.ResetAnimation();
    }

    private void EndAnimationSession()
    {
        _animation?.EndLocomotionSession(_animationOwner);
        _animationOwner = default;
        _animation = null;
    }

    internal void OnAnimationSessionLost(ActorAnimation source, ActorAnimationLocomotionOwner owner)
    {
        if (_animation != source || !owner.IsValid || owner.Id != _animationOwner.Id)
            return;
        // The playback owner has already ended this session. Do not call it back.
        _animationOwner = default;
        _animation = null;
        _currentRuntime?.ResetAnimation();
    }

    internal void UpdateAnimation(ActorAnimation animation, ActorMotor motor, float deltaTime)
    {
        if (!LocomotionDataValidation.IsFinite(deltaTime) || deltaTime <= 0f)
        {
            _currentRuntime?.SuspendAnimationFeedback();
            return;
        }
        if (_tick.IsAnimationSubmitted || !_tick.AnimationSnapshot.HasValue || _currentRuntime == null
            || animation == null)
            return;
        _tick.MarkAnimationSubmitted();
        if (_animation != animation || !animation.IsLocomotionOwnerActive(_animationOwner))
        {
            if (_animationOwner.IsValid)
                ResetAnimationSession();
            _animation = animation;
            _animationOwner = animation.BeginLocomotionSession();
        }
        if (!_animationOwner.IsValid)
            return;
        var snapshot = _tick.AnimationSnapshot.Value;
        float timeScale = snapshot.Motor.MotionState.MovementTimeScale;
        float verticalSpeed = motor != null && timeScale > 0f
            ? Vector3.Dot(motor.RequestedVelocity, snapshot.Motor.CharacterUp) / timeScale : 0f;
        var context = new LocomotionRuntimeAnimationContext(snapshot.Intent, snapshot.HasIntent,
            snapshot.VelocityBeforeMotion, snapshot.ModelVelocity, snapshot.Motor,
            verticalSpeed, animation.ActiveActionOwnerId, deltaTime,
            animation.GetLocomotionFootPhaseReference(_animationOwner));
        LocomotionAnimationRequest request = _currentRuntime.UpdateAnimation(context);
        bool accepted = animation.SubmitLocomotion(_animationOwner, request);
#if UNITY_EDITOR
        TraceAnimationTick(context, request, accepted);
#endif
    }

    private LocomotionAsset SelectAsset(in LocomotionSelectionContext context)
    {
        LocomotionAsset firstTop = null;
        LocomotionAsset currentCandidate = null;
        int topPriority = int.MinValue;
        _candidateScanSet.Clear();

        if (locomotionAssets != null)
        {
            for (int i = 0; i < locomotionAssets.Count; i++)
            {
                LocomotionAsset asset = locomotionAssets[i];
                if (!IsCandidateValidForSelection(asset, context))
                    continue;

                if (firstTop == null || asset.Priority > topPriority)
                {
                    topPriority = asset.Priority;
                    firstTop = asset;
                    currentCandidate = asset == CurrentAsset ? asset : null;
                }
                else if (asset.Priority == topPriority && asset == CurrentAsset)
                {
                    currentCandidate = asset;
                }
            }
        }

        return currentCandidate != null ? currentCandidate : firstTop;
    }

    private bool IsCandidateValidForSelection(LocomotionAsset asset, in LocomotionSelectionContext context) =>
        asset != null && _candidateScanSet.Add(asset)
        && (_runtimes.TryGetValue(asset, out var runtime) ? runtime.HasValidRuntimeConfig : asset.HasValidRuntimeConfig)
        && asset.AreEntryConditionsMet(context);

    private void ApplyAsset(LocomotionAsset asset)
    {
        EndAnimationSession();
        _currentRuntime?.Exit(this, actor);
        _currentRuntime = null;
        ReleaseAcquiredTags();
        CurrentAsset = asset;

        if (CurrentAsset == null)
        {
            CurrentMovementConfig = LocomotionMovementConfig.Default;
            return;
        }

        _currentRuntime = GetOrCreateRuntime(CurrentAsset);
        CurrentMovementConfig = _currentRuntime.MovementConfig;
        AcquireAssetTags(CurrentAsset);
        _currentRuntime.Enter(this, actor);
    }

    private LocomotionRuntime GetOrCreateRuntime(LocomotionAsset asset)
    {
        if (_runtimes.TryGetValue(asset, out LocomotionRuntime runtime))
            return runtime;

        runtime = asset.CreateRuntime();
        if (runtime == null || runtime.Asset != asset)
        {
            runtime?.Dispose();
            throw new System.InvalidOperationException(
                $"[ActorLocomotion] Actor '{name}', Asset '{asset.name}': CreateRuntime must return a Runtime owned by this Asset.");
        }

        _runtimes.Add(asset, runtime);
        return runtime;
    }

    private void ClearCurrentAsset()
    {
        EndAnimationSession();
        _currentRuntime?.Exit(this, actor);
        _currentRuntime = null;
        ReleaseAcquiredTags();
        CurrentAsset = null;
        CurrentMovementConfig = LocomotionMovementConfig.Default;
    }

    private void DisposeRuntimes()
    {
        foreach (KeyValuePair<LocomotionAsset, LocomotionRuntime> pair in _runtimes)
            pair.Value?.Dispose();
        _runtimes.Clear();
    }

    private void AcquireAssetTags(LocomotionAsset asset)
    {
        IReadOnlyList<TagReference> tags = asset.SelfTags;
        if (tags == null || actor == null)
            return;

        for (int i = 0; i < tags.Count; i++)
        {
            Tag tag = tags[i] != null ? tags[i].GetTag() : null;
            if (tag == null || _acquiredTags.Contains(tag))
                continue;

            actor.AddTag(tag, ActorTagContainerType.Transient);
            _acquiredTags.Add(tag);
        }
    }

    private void ReleaseAcquiredTags()
    {
        if (actor != null)
        {
            for (int i = _acquiredTags.Count - 1; i >= 0; i--)
                actor.RemoveTag(_acquiredTags[i], ActorTagContainerType.Transient);
        }

        _acquiredTags.Clear();
    }

    private void ResolveActor()
    {
        if (actor == null)
            actor = GetComponent<Actor>();

        if (actor != null)
            actor.actorLocomotion = this;
    }

    private void MigrateLegacyFallback()
    {
        if (legacyFallbackMode == null)
            return;

        locomotionAssets ??= new List<LocomotionAsset>();
        if (!locomotionAssets.Contains(legacyFallbackMode))
            locomotionAssets.Insert(0, legacyFallbackMode);
        legacyFallbackMode = null;
    }
}
