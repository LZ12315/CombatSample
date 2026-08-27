using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActorLocomotion : MonoBehaviour
{
    [SerializeField] private Actor actor;
    [SerializeField] private LocomotionModeAsset fallbackMode;
    [SerializeField] private List<LocomotionModeAsset> candidateModes = new();

    private readonly List<Tag> _acquiredTags = new();
    private readonly HashSet<LocomotionModeAsset> _candidateScanSet = new();
    private LocomotionIntent _controlIntent = LocomotionIntent.Idle;
    private bool _hasControlIntent;
    private bool _warnedInvalidFallback;
    private bool _warnedInvalidCandidate;
    private bool _warnedInvalidList;

    public LocomotionModeAsset CurrentMode { get; private set; }
    public LocomotionTuning CurrentTuning { get; private set; } = LocomotionTuning.Default;
    public LocomotionAnimationProfile CurrentAnimationProfile { get; private set; } =
        LocomotionAnimationProfile.Default;

    private void Awake()
    {
        ResolveActor();
    }

    private void OnDisable()
    {
        ClearCurrentMode(true);
        CancelControlTick();
    }

    private void OnDestroy()
    {
        ClearCurrentMode(true);
        CancelControlTick();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fallbackMode == null)
            Debug.LogWarning("[ActorLocomotion] Missing fallback LocomotionModeAsset.", this);

        _candidateScanSet.Clear();
        if (candidateModes == null)
            return;

        for (int i = 0; i < candidateModes.Count; i++)
        {
            LocomotionModeAsset mode = candidateModes[i];
            if (mode == null)
            {
                Debug.LogWarning("[ActorLocomotion] Candidate list contains a null entry.", this);
                continue;
            }

            if (mode == fallbackMode)
                Debug.LogWarning("[ActorLocomotion] Fallback mode must not be listed as a candidate.", this);

            if (!_candidateScanSet.Add(mode))
                Debug.LogWarning($"[ActorLocomotion] Duplicate candidate LocomotionModeAsset '{mode.name}'.", this);

            if (!mode.HasEntryConditions)
                Debug.LogWarning($"[ActorLocomotion] Candidate LocomotionModeAsset '{mode.name}' has no EntryConditions.", this);
        }
    }
#endif

    internal void Bind(Actor owner)
    {
        if (owner != null)
            actor = owner;

        ResolveActor();
    }

    internal void SelectModeForControlTick()
    {
        ResolveActor();
        if (actor == null)
            return;

        ActorMotor motor = actor.actorMotor;
        _controlIntent = motor != null && motor.HasPendingLocomotionIntent
            ? motor.PendingLocomotionIntent
            : LocomotionIntent.Idle;
        _hasControlIntent = true;

        LocomotionModeAsset selected = SelectMode(new LocomotionModeContext(actor, _controlIntent));
        if (selected != CurrentMode)
            ApplyMode(selected);
    }

    internal void CancelControlTick()
    {
        _hasControlIntent = false;
        _controlIntent = LocomotionIntent.Idle;
    }

    internal bool TryBuildLocomotionAnimationPose(out LocomotionAnimationPose pose)
    {
        pose = default;
        if (CurrentMode == null || !_hasControlIntent)
            return false;

        LocomotionAnimationProfile profile = CurrentAnimationProfile.Sanitize();
        if (string.IsNullOrWhiteSpace(profile.IdleKey) || string.IsNullOrWhiteSpace(profile.MoveKey))
            return false;

        float moveStrength = Mathf.Clamp01(_controlIntent.MoveStrength);
        Vector3 moveDirection = _controlIntent.WorldMoveDirection;
        moveDirection.y = 0f;
        bool isMoving = moveStrength > profile.MoveThreshold && moveDirection.sqrMagnitude > 0.0001f;

        bool hasVector2Parameter = profile.ParameterSource == LocomotionAnimationParameterSource.LocalDirection2D;
        bool hasFloatParameter = profile.ParameterSource == LocomotionAnimationParameterSource.MoveStrength1D;
        Vector2 vector2Parameter = Vector2.zero;
        float floatParameter = isMoving ? moveStrength : 0f;

        if (hasVector2Parameter && isMoving)
        {
            moveDirection.Normalize();
            Vector3 localDirection = actor.transform.InverseTransformDirection(moveDirection);
            vector2Parameter = new Vector2(localDirection.x, localDirection.z) * moveStrength;
            if (vector2Parameter.sqrMagnitude > 1f)
                vector2Parameter.Normalize();
        }

        pose = new LocomotionAnimationPose(
            isMoving ? profile.MoveKey : profile.IdleKey,
            hasVector2Parameter,
            vector2Parameter,
            hasFloatParameter,
            floatParameter);
        return true;
    }

    private LocomotionModeAsset SelectMode(in LocomotionModeContext context)
    {
        LocomotionModeAsset firstTop = null;
        LocomotionModeAsset currentCandidate = null;
        int topPriority = int.MinValue;
        _candidateScanSet.Clear();

        if (candidateModes != null)
        {
            for (int i = 0; i < candidateModes.Count; i++)
            {
                LocomotionModeAsset mode = candidateModes[i];
                if (!IsCandidateValidForSelection(mode, context))
                    continue;

                if (mode.Priority > topPriority)
                {
                    topPriority = mode.Priority;
                    firstTop = mode;
                    currentCandidate = mode == CurrentMode ? mode : null;
                }
                else if (mode.Priority == topPriority && mode == CurrentMode)
                {
                    currentCandidate = mode;
                }
            }
        }

        if (currentCandidate != null)
            return currentCandidate;

        if (firstTop != null)
            return firstTop;

        return IsFallbackValid() ? fallbackMode : null;
    }

    private bool IsCandidateValidForSelection(LocomotionModeAsset mode, in LocomotionModeContext context)
    {
        if (mode == null || mode == fallbackMode || !_candidateScanSet.Add(mode))
        {
            WarnInvalidListOnce();
            return false;
        }

        return mode.ValidateRuntime(false, this, ref _warnedInvalidCandidate)
               && mode.AreEntryConditionsMet(context);
    }

    private bool IsFallbackValid()
    {
        if (fallbackMode == null)
        {
            if (!_warnedInvalidFallback)
            {
                Debug.LogWarning("[ActorLocomotion] Missing fallback LocomotionModeAsset.", this);
                _warnedInvalidFallback = true;
            }

            return false;
        }

        return fallbackMode.ValidateRuntime(true, this, ref _warnedInvalidFallback);
    }

    private void ApplyMode(LocomotionModeAsset mode)
    {
        ReleaseAcquiredTags();
        CurrentMode = mode;

        if (CurrentMode == null)
        {
            CurrentTuning = LocomotionTuning.Default;
            CurrentAnimationProfile = LocomotionAnimationProfile.Default;
            actor?.actorMotor?.RestoreCompatibilityLocomotionTuning();
            return;
        }

        AcquireModeTags(CurrentMode);
        CurrentTuning = CurrentMode.Tuning;
        actor?.actorMotor?.ApplyLocomotionTuning(CurrentTuning);
        CurrentAnimationProfile = CurrentMode.AnimationProfile;
    }

    private void ClearCurrentMode(bool restoreTuning)
    {
        ReleaseAcquiredTags();
        CurrentMode = null;
        CurrentTuning = LocomotionTuning.Default;
        CurrentAnimationProfile = LocomotionAnimationProfile.Default;

        if (restoreTuning)
            actor?.actorMotor?.RestoreCompatibilityLocomotionTuning();
    }

    private void AcquireModeTags(LocomotionModeAsset mode)
    {
        IReadOnlyList<TagReference> tags = mode.SelfTags;
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
            "[ActorLocomotion] Candidate list contains null, duplicate, or fallback mode entries; invalid entries are ignored.",
            this);
        _warnedInvalidList = true;
    }
}
