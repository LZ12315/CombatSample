using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ActorLocomotion : MonoBehaviour
{
    [SerializeField] private Actor actor;
    [SerializeField, FormerlySerializedAs("candidateModes")]
    private List<LocomotionAsset> locomotionAssets = new();
    [SerializeField, HideInInspector, FormerlySerializedAs("fallbackMode")]
    private LocomotionAsset legacyFallbackMode;

    private readonly List<Tag> _acquiredTags = new();
    private readonly HashSet<LocomotionAsset> _candidateScanSet = new();
    private readonly HashSet<LocomotionAsset> _warnedIncompleteAnimation = new();
    private readonly Dictionary<LocomotionAsset, LocomotionRuntime> _runtimes = new();
    private readonly List<string> _animationCoverageIssues = new();
    private readonly LocomotionRunner _runner = new();
    private LocomotionIntent _pendingIntent = LocomotionIntent.Idle;
    private LocomotionIntent _controlIntent = LocomotionIntent.Idle;
    private LocomotionRuntime _currentRuntime;
    private bool _hasPendingIntent;
    private bool _hasControlIntent;
    private bool _controlTickOpen;
    private bool _motionRequestBuilt;
    private LocomotionMotionRequest _builtMotionRequest;
    private bool _warnedInvalidCandidate;
    private bool _warnedInvalidList;
    private bool _warnedNoMatch;
    private bool _warnedInvalidRuntime;
    private ActorAnimation _animation;
    private ActorAnimationLocomotionOwner _animationOwner;
    private LocomotionIntent _animationIntent;
    private bool _animationHasIntent;
    private Vector3 _velocityBeforeMotion;
    private LocomotionMotionContext _animationMotorContext;
    private bool _hasAnimationSnapshot;
    private bool _animationUpdated;

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        MigrateLegacyFallback();
        _candidateScanSet.Clear();
        if (locomotionAssets == null || locomotionAssets.Count == 0)
        {
            Debug.LogWarning("[ActorLocomotion] Locomotion asset list is empty.", this);
            return;
        }

        for (int i = 0; i < locomotionAssets.Count; i++)
        {
            LocomotionAsset asset = locomotionAssets[i];
            if (asset == null)
            {
                Debug.LogWarning("[ActorLocomotion] Locomotion asset list contains a null entry.", this);
                continue;
            }

            if (!_candidateScanSet.Add(asset))
                Debug.LogWarning($"[ActorLocomotion] Duplicate LocomotionAsset '{asset.name}'.", this);
        }
    }
#endif

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

        _pendingIntent = intent;
        _hasPendingIntent = true;
    }

    public void ClearLocomotionIntent()
    {
        _pendingIntent = LocomotionIntent.Idle;
        _controlIntent = LocomotionIntent.Idle;
        _hasPendingIntent = false;
        _hasControlIntent = false;
        _controlTickOpen = false;
        _motionRequestBuilt = false;
        _builtMotionRequest = default;
        _runner.ClearIntent();
        _hasAnimationSnapshot = false;
    }

    public bool TryGetControlIntent(out LocomotionIntent intent)
    {
        intent = _controlTickOpen && _hasControlIntent ? _controlIntent : LocomotionIntent.Idle;
        return _controlTickOpen && _hasControlIntent;
    }

    internal void SyncRotation(Quaternion rotation) => _runner.SyncRotation(rotation);

    /// <summary>Locks the one Intent snapshot shared by Action, selection and Motion for this tick.</summary>
    public void BeginControlTick()
    {
        ResolveActor();
        if (actor == null)
            return;

        _controlIntent = _hasPendingIntent ? _pendingIntent : LocomotionIntent.Idle;
        _hasControlIntent = _hasPendingIntent;
        _controlTickOpen = true;
        _motionRequestBuilt = false;
        _builtMotionRequest = default;
        _hasAnimationSnapshot = false;
        _animationUpdated = false;
        _pendingIntent = LocomotionIntent.Idle;
        _hasPendingIntent = false;
    }

    public LocomotionMotionRequest BuildMotionRequest(in LocomotionMotionContext context)
    {
        if (_motionRequestBuilt)
            return _builtMotionRequest;

        if (context.EffectiveDeltaTime <= 0f)
        {
            HoldControlTick();
            return default;
        }

        ResolveActor();
        LocomotionIntent intent = _controlTickOpen ? _controlIntent : LocomotionIntent.Idle;
        bool hasIntent = _controlTickOpen && _hasControlIntent;
        LocomotionAsset selected = actor != null
            ? SelectAsset(new LocomotionSelectionContext(actor, intent, context))
            : null;
        if (selected != CurrentAsset)
            ApplyAsset(selected);

        if (_currentRuntime != null)
        {
            _animationIntent = intent;
            _animationHasIntent = hasIntent;
            _velocityBeforeMotion = _runner.CachedVelocity;
            _animationMotorContext = context;
            _hasAnimationSnapshot = true;
            var runtimeContext = new LocomotionRuntimeMotionContext(_runner, intent, hasIntent, context);
            _builtMotionRequest = _currentRuntime.UpdateMotion(runtimeContext);
        }
        else
        {
            _runner.ClearIntent();
            _builtMotionRequest = default;
        }

        ConsumeControlTick();
        _motionRequestBuilt = true;
        return _builtMotionRequest;
    }

    internal void HoldControlTick()
    {
        if (_controlTickOpen && _hasControlIntent && !_hasPendingIntent)
        {
            _pendingIntent = _controlIntent;
            _hasPendingIntent = true;
        }

        _controlTickOpen = false;
        _motionRequestBuilt = false;
        _builtMotionRequest = default;
        _hasControlIntent = false;
        _controlIntent = LocomotionIntent.Idle;
        _hasAnimationSnapshot = false;
    }

    internal void CancelControlTick()
    {
        ClearLocomotionIntent();
    }

    internal void CancelSimulation()
    {
        ClearCurrentAsset();
        DisposeRuntimes();
        CancelControlTick();
    }

    internal void ResetAnimationSession()
    {
        _animation?.EndLocomotionSession(_animationOwner);
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
        if (_animationUpdated || !_hasAnimationSnapshot || _currentRuntime == null
            || !LocomotionDataValidation.IsFinite(deltaTime) || deltaTime <= 0f || animation == null)
            return;
        _animationUpdated = true;
        if (_animation != animation || !animation.IsLocomotionOwnerActive(_animationOwner))
        {
            ResetAnimationSession();
            _animation = animation;
            _animationOwner = animation.BeginLocomotionSession();
        }
        if (!_animationOwner.IsValid)
            return;
        float timeScale = _animationMotorContext.MotionState.MovementTimeScale;
        float verticalSpeed = motor != null && timeScale > 0f
            ? Vector3.Dot(motor.RequestedVelocity, _animationMotorContext.CharacterUp) / timeScale : 0f;
        var context = new LocomotionRuntimeAnimationContext(_animationIntent, _animationHasIntent,
            _velocityBeforeMotion, _runner.CachedVelocity, _animationMotorContext,
            verticalSpeed, animation.ActiveActionOwnerId, deltaTime);
        LocomotionAnimationRequest request = _currentRuntime.UpdateAnimation(context);
        animation.SubmitLocomotion(_animationOwner, request);
    }

    private void ConsumeControlTick()
    {
        _controlTickOpen = false;
        _hasControlIntent = false;
        _controlIntent = LocomotionIntent.Idle;
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

                if (asset.Priority > topPriority)
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

        LocomotionAsset selected = currentCandidate != null ? currentCandidate : firstTop;
        if (selected == null && !_warnedNoMatch)
        {
            Debug.LogWarning("[ActorLocomotion] No valid LocomotionAsset matched this Motion tick.", this);
            _warnedNoMatch = true;
        }
        else if (selected != null)
        {
            _warnedNoMatch = false;
        }

        return selected;
    }

    private bool IsCandidateValidForSelection(LocomotionAsset asset, in LocomotionSelectionContext context)
    {
        if (asset == null || !_candidateScanSet.Add(asset))
        {
            WarnInvalidListOnce();
            return false;
        }

        return asset.ValidateRuntime(this, ref _warnedInvalidCandidate)
               && asset.AreEntryConditionsMet(context);
    }

    private void ApplyAsset(LocomotionAsset asset)
    {
        ResetAnimationSession();
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
        if (_currentRuntime == null)
        {
            CurrentAsset = null;
            CurrentMovementConfig = LocomotionMovementConfig.Default;
            return;
        }

        CurrentMovementConfig = CurrentAsset.MovementConfig;
        AcquireAssetTags(CurrentAsset);
        _currentRuntime.Enter(this, actor);
        ReportAnimationCoverageOnce(CurrentAsset);
    }

    private LocomotionRuntime GetOrCreateRuntime(LocomotionAsset asset)
    {
        if (_runtimes.TryGetValue(asset, out LocomotionRuntime runtime))
            return runtime;

        runtime = asset.CreateRuntime();
        if (runtime == null || runtime.Asset != asset)
        {
            if (!_warnedInvalidRuntime)
            {
                Debug.LogWarning($"[ActorLocomotion] LocomotionAsset '{asset.name}' created an invalid Runtime.", this);
                _warnedInvalidRuntime = true;
            }
            runtime?.Dispose();
            return null;
        }

        _runtimes.Add(asset, runtime);
        return runtime;
    }

    private void ReportAnimationCoverageOnce(LocomotionAsset asset)
    {
        if (!Application.isPlaying || !_warnedIncompleteAnimation.Add(asset))
            return;

        _animationCoverageIssues.Clear();
        asset.CollectAnimationCoverageIssues(_animationCoverageIssues);
        if (_animationCoverageIssues.Count > 0)
        {
            Debug.LogWarning(
                $"[ActorLocomotion] '{asset.name}' animation coverage incomplete: "
                + string.Join("; ", _animationCoverageIssues), this);
        }
    }

    private void ClearCurrentAsset()
    {
        ResetAnimationSession();
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

    private void WarnInvalidListOnce()
    {
        if (_warnedInvalidList)
            return;

        Debug.LogWarning(
            "[ActorLocomotion] Locomotion asset list contains null or duplicate entries; invalid entries are ignored.",
            this);
        _warnedInvalidList = true;
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
