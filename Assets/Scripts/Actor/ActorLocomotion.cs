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
    private readonly Dictionary<LocomotionAsset, LocomotionRuntime> _runtimes = new();
    private readonly LocomotionRunner _runner = new();
    private readonly LocomotionIntentBuffer _input = new();
    private LocomotionIntent _controlIntent = LocomotionIntent.Idle;
    private LocomotionRuntime _currentRuntime;
    private bool _hasControlIntent;
    private bool _controlFromContinuous;
    private bool _controlTickOpen;
    private bool _motionRequestBuilt;
    private LocomotionMotionRequest _builtMotionRequest;
    private bool _warnedInvalidCandidate;
    private bool _warnedInvalidList;
    private bool _warnedNoMatch;
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
    private const int DebugTraceTickCount = 120;
    private bool _debugTraceArmed;
    private int _debugAnimationTicksRemaining;
    private bool _debugTracePending;
    private LocomotionRuntimeAnimationContext _debugTraceContext;
    private LocomotionAnimationRequest _debugTraceRequest;
    private bool _debugTraceAccepted;

    [ContextMenu("Debug/Trace Next Turn or Release (120 Ticks)")]
    private void TraceNextAnimationTicks()
    {
        _debugTraceArmed = true;
        _debugAnimationTicksRemaining = 0;
        _debugTracePending = false;
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
            "[Locomotion Trace] Actor '{0}': armed; waiting for a moving reversal or input release.", name);
    }

    private void TraceAnimationTick(in LocomotionRuntimeAnimationContext context,
        in LocomotionAnimationRequest request, bool accepted)
    {
        if (_debugTraceArmed)
        {
            Vector3 source = Vector3.ProjectOnPlane(context.VelocityBeforeMotion, context.Motor.CharacterUp);
            Vector3 target = Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection, context.Motor.CharacterUp);
            if (source.sqrMagnitude < 0.25f
                || (context.HasMovingInput && Vector3.Dot(source.normalized, target.normalized) > -0.5f))
                return;
            _debugTraceArmed = false;
            _debugAnimationTicksRemaining = DebugTraceTickCount;
        }
        if (_debugAnimationTicksRemaining <= 0)
            return;
        _debugTraceContext = context;
        _debugTraceRequest = request;
        _debugTraceAccepted = accepted;
        _debugTracePending = true;
    }

    internal void TraceEvaluatedAnimationTick(ActorAnimation animation)
    {
        if (!_debugTracePending)
            return;
        _debugTracePending = false;
        _debugAnimationTicksRemaining--;
        var context = _debugTraceContext;
        var request = _debugTraceRequest;
        float idleWeight = 0f;
        if (request.IsMove && request.State != null && request.IdleClip != null)
            for (int i = 0; i < request.State.ChildCount; i++)
                if (request.State.GetChild(i).Clip == request.IdleClip)
                    idleWeight += request.State.GetChild(i).Weight;
        string state = _currentRuntime is LocomotionSetRuntime set ? set.AnimationState.ToString() : "Move";
        string message = $"[Locomotion Trace] tick={DebugTraceTickCount - _debugAnimationTicksRemaining}, frame={Time.frameCount}, "
            + $"Actor '{name}', Asset '{CurrentAsset.name}', owner={_animationOwner.Id}, action={context.ActionOwnerId}, "
            + $"input={context.Intent.WorldMoveDirection:F3}/{context.Intent.MoveStrength:F3}, "
            + $"before={context.VelocityBeforeMotion:F3}, after={context.ModelVelocity:F3}, "
            + $"state={state}, accepted={_debugTraceAccepted}, parameter={request.Parameter:F3}, "
            + $"idleSampleWeight={idleWeight:F3}, dt={context.DeltaTime:F4}; "
            + animation.DescribeEvaluatedLocomotionLayers();
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}", message);
        _debugTraceContext = default;
        _debugTraceRequest = default;
    }

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

        _hasControlIntent = _input.Capture(out _controlIntent, out _controlFromContinuous);
        _controlTickOpen = true;
        _motionRequestBuilt = false;
        _builtMotionRequest = default;
        _hasAnimationSnapshot = false;
        _animationUpdated = false;
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
        if (_controlTickOpen && _hasControlIntent)
            _input.Hold(_controlIntent, _controlFromContinuous);

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
        ConsumeControlTick();
        _controlFromContinuous = false;
        _motionRequestBuilt = false;
        _builtMotionRequest = default;
        _hasAnimationSnapshot = false;
        _animationUpdated = false;
    }

    internal void CancelSimulation()
    {
#if UNITY_EDITOR
        _debugTraceArmed = false;
        _debugTracePending = false;
        _debugAnimationTicksRemaining = 0;
        _debugTraceContext = default;
        _debugTraceRequest = default;
#endif
        ClearCurrentAsset();
        DisposeRuntimes();
        CancelControlTick();
        _runner.ClearIntent();
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
            || animation == null)
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
        bool accepted = animation.SubmitLocomotion(_animationOwner, request);
#if UNITY_EDITOR
        TraceAnimationTick(context, request, accepted);
#endif
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
        CurrentMovementConfig = CurrentAsset.MovementConfig;
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
