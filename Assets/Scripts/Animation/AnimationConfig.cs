using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationConfig", menuName = "Combat/Animation/Animation Config")]
public sealed class AnimationConfig : ScriptableObject
{
    [SerializeField] private List<AnimationConfigEntry> entries = new List<AnimationConfigEntry>();

#if UNITY_EDITOR
    [Header("Root Motion Bake Context")]
    [SerializeField] private GameObject rootMotionReferenceRigPrefab;
    [SerializeField, Min(1)] private int rootMotionSampleRate = 60;
    [SerializeField, Min(0f)] private float rootMotionPositionTolerance = 0.001f;
    [SerializeField, Min(0f)] private float rootMotionRotationToleranceDegrees = 0.1f;

    public GameObject RootMotionReferenceRigPrefab => rootMotionReferenceRigPrefab;
    public int RootMotionSampleRate => rootMotionSampleRate;
    public float RootMotionPositionTolerance => rootMotionPositionTolerance;
    public float RootMotionRotationToleranceDegrees => rootMotionRotationToleranceDegrees;
#endif

    [NonSerialized] private Dictionary<string, AnimationConfigEntry> _lookup;
    [NonSerialized] private bool _lookupBuilt;

    public IReadOnlyList<AnimationConfigEntry> Entries => entries ?? (IReadOnlyList<AnimationConfigEntry>)Array.Empty<AnimationConfigEntry>();

    public bool TryGetEntry(string key, out AnimationConfigEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        EnsureLookup();
        return _lookup.TryGetValue(key, out entry);
    }

    public bool TryGetTransition(string key, out TransitionAsset transition)
    {
        transition = null;
        if (!TryGetEntry(key, out AnimationConfigEntry entry) || entry.TransitionAsset == null)
            return false;

        transition = entry.TransitionAsset;
        return true;
    }

    public bool TryGetTrajectory(string key, out RootMotionTrajectory trajectory)
    {
        trajectory = null;
        if (!TryGetEntry(key, out AnimationConfigEntry entry) || entry.RootMotionTrajectory == null)
            return false;

        trajectory = entry.RootMotionTrajectory;
        return true;
    }

    public AnimationConfigValidationResult ValidateData()
    {
        var result = new AnimationConfigValidationResult();
        var firstIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        int count = entries != null ? entries.Count : 0;

#if UNITY_EDITOR
        RootMotionBakeSettingsValidationResult bakeSettings = ValidateRootMotionBakeSettings();
        for (int i = 0; i < bakeSettings.Issues.Count; i++)
        {
            result.Add(
                AnimationConfigValidationCode.InvalidRootMotionBakeSettings,
                -1,
                null,
                bakeSettings.Issues[i].Message);
        }
#endif

        for (int i = 0; i < count; i++)
        {
            AnimationConfigEntry entry = entries[i];
            if (entry == null)
            {
                result.Add(AnimationConfigValidationCode.NullEntry, i, null, "Entry is null.");
                continue;
            }

            string key = entry.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                result.Add(AnimationConfigValidationCode.EmptyKey, i, key, "Entry key is empty.");
            }
            else if (firstIndexByKey.TryGetValue(key, out int firstIndex))
            {
                result.Add(
                    AnimationConfigValidationCode.DuplicateKey,
                    i,
                    key,
                    $"Entry key duplicates index {firstIndex}.");
            }
            else
            {
                firstIndexByKey.Add(key, i);
            }

            if (entry.TransitionAsset == null)
                result.Add(AnimationConfigValidationCode.MissingTransition, i, key, "TransitionAsset is missing.");
        }

        return result;
    }

#if UNITY_EDITOR
    public RootMotionBakeSettingsValidationResult ValidateRootMotionBakeSettings()
    {
        var result = new RootMotionBakeSettingsValidationResult();

        if (rootMotionSampleRate <= 0)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidSampleRate, "Sample rate must be greater than zero.");

        if (!IsFinite(rootMotionPositionTolerance) || rootMotionPositionTolerance < 0f)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidPositionTolerance, "Position tolerance must be finite and non-negative.");

        if (!IsFinite(rootMotionRotationToleranceDegrees) || rootMotionRotationToleranceDegrees < 0f)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidRotationTolerance, "Rotation tolerance must be finite and non-negative.");

        if (rootMotionReferenceRigPrefab == null)
        {
            result.Add(RootMotionBakeSettingsValidationCode.MissingReferenceRig, "Reference Rig Prefab is missing.");
            return result;
        }

        Animator[] animators = rootMotionReferenceRigPrefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
        {
            result.Add(RootMotionBakeSettingsValidationCode.MissingAnimator, "Reference Rig must contain one Animator.");
            return result;
        }

        if (animators.Length != 1)
        {
            result.Add(
                RootMotionBakeSettingsValidationCode.MultipleAnimators,
                $"Reference Rig must contain exactly one Animator, but found {animators.Length}.");
            return result;
        }

        Animator animator = animators[0];
        if (animator.avatar == null || !animator.avatar.isValid)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidAvatar, "Reference Animator must use a valid Avatar.");

        Vector3 scale = animator.transform.lossyScale;
        if (!ApproximatelyOne(scale.x) || !ApproximatelyOne(scale.y) || !ApproximatelyOne(scale.z))
        {
            result.Add(
                RootMotionBakeSettingsValidationCode.NonUnitAnimatorScale,
                $"Reference Animator must have unit world scale, but found {scale}.");
        }

        MonoBehaviour[] behaviours = rootMotionReferenceRigPrefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            string typeName = behaviour != null ? behaviour.GetType().Name : "Missing Script";
            result.Add(
                RootMotionBakeSettingsValidationCode.ContainsMonoBehaviour,
                $"Reference Rig must be animation-only and cannot contain {typeName}.");
        }

        return result;
    }

    public bool TryGetRootMotionAnimator(out Animator animator)
    {
        animator = null;
        if (rootMotionReferenceRigPrefab == null)
            return false;

        Animator[] animators = rootMotionReferenceRigPrefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 1)
            return false;

        animator = animators[0];
        return true;
    }

    public void EditorSetRootMotionBakeSettings(
        GameObject referenceRigPrefab,
        int sampleRate,
        float positionTolerance = 0.001f,
        float rotationToleranceDegrees = 0.1f)
    {
        rootMotionReferenceRigPrefab = referenceRigPrefab;
        rootMotionSampleRate = sampleRate;
        rootMotionPositionTolerance = positionTolerance;
        rootMotionRotationToleranceDegrees = rotationToleranceDegrees;
    }

    public void EditorSetEntries(params AnimationConfigEntry[] newEntries)
    {
        entries = newEntries != null
            ? new List<AnimationConfigEntry>(newEntries)
            : new List<AnimationConfigEntry>();
        InvalidateLookup();
    }

    private static bool ApproximatelyOne(float value)
    {
        return Mathf.Abs(value - 1f) <= 1e-4f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
#endif

    private void OnEnable()
    {
        InvalidateLookup();
    }

    private void OnValidate()
    {
        InvalidateLookup();
    }

    private void EnsureLookup()
    {
        if (_lookupBuilt)
            return;

        _lookupBuilt = true;
        _lookup = new Dictionary<string, AnimationConfigEntry>(StringComparer.Ordinal);
        var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);
        int count = entries != null ? entries.Count : 0;

        for (int i = 0; i < count; i++)
        {
            AnimationConfigEntry entry = entries[i];
            string key = entry != null ? entry.Key : null;
            if (string.IsNullOrWhiteSpace(key) || ambiguousKeys.Contains(key))
                continue;

            if (_lookup.ContainsKey(key))
            {
                _lookup.Remove(key);
                ambiguousKeys.Add(key);
            }
            else
            {
                _lookup.Add(key, entry);
            }
        }
    }

    private void InvalidateLookup()
    {
        _lookupBuilt = false;
        _lookup = null;
    }
}

[Serializable]
public sealed class AnimationConfigEntry
{
    [SerializeField] private string key = string.Empty;
    [SerializeField] private TransitionAsset transitionAsset;
    [SerializeField, HideInInspector] private bool hasRootMotionTrajectory;
    [SerializeField, HideInInspector] private RootMotionTrajectory rootMotionTrajectory = new RootMotionTrajectory();

    public string Key => key;
    public TransitionAsset TransitionAsset => transitionAsset;
    public RootMotionTrajectory RootMotionTrajectory => hasRootMotionTrajectory ? rootMotionTrajectory : null;

    public AnimationConfigEntry()
    {
    }

#if UNITY_EDITOR
    public AnimationConfigEntry(string newKey, TransitionAsset newTransitionAsset, RootMotionTrajectory newRootMotionTrajectory = null)
    {
        key = newKey ?? string.Empty;
        transitionAsset = newTransitionAsset;
        EditorSetRootMotionTrajectory(newRootMotionTrajectory);
    }

    public void EditorSetRootMotionTrajectory(RootMotionTrajectory trajectory)
    {
        hasRootMotionTrajectory = trajectory != null;
        rootMotionTrajectory = trajectory ?? new RootMotionTrajectory();
    }
#endif
}

public enum AnimationConfigValidationCode
{
    InvalidRootMotionBakeSettings,
    NullEntry,
    EmptyKey,
    DuplicateKey,
    MissingTransition,
}

public readonly struct AnimationConfigValidationIssue
{
    public AnimationConfigValidationCode Code { get; }
    public int EntryIndex { get; }
    public string Key { get; }
    public string Message { get; }

    public AnimationConfigValidationIssue(AnimationConfigValidationCode code, int entryIndex, string key, string message)
    {
        Code = code;
        EntryIndex = entryIndex;
        Key = key;
        Message = message;
    }
}

public sealed class AnimationConfigValidationResult
{
    private readonly List<AnimationConfigValidationIssue> _issues = new List<AnimationConfigValidationIssue>();

    public bool IsValid => _issues.Count == 0;
    public IReadOnlyList<AnimationConfigValidationIssue> Issues => _issues;

    internal void Add(AnimationConfigValidationCode code, int entryIndex, string key, string message)
    {
        _issues.Add(new AnimationConfigValidationIssue(code, entryIndex, key, message));
    }
}

#if UNITY_EDITOR
public enum RootMotionBakeSettingsValidationCode
{
    MissingReferenceRig,
    InvalidSampleRate,
    InvalidPositionTolerance,
    InvalidRotationTolerance,
    MissingAnimator,
    MultipleAnimators,
    InvalidAvatar,
    NonUnitAnimatorScale,
    ContainsMonoBehaviour,
}

public readonly struct RootMotionBakeSettingsValidationIssue
{
    public RootMotionBakeSettingsValidationCode Code { get; }
    public string Message { get; }

    public RootMotionBakeSettingsValidationIssue(RootMotionBakeSettingsValidationCode code, string message)
    {
        Code = code;
        Message = message;
    }
}

public sealed class RootMotionBakeSettingsValidationResult
{
    private readonly List<RootMotionBakeSettingsValidationIssue> _issues = new List<RootMotionBakeSettingsValidationIssue>();

    public bool IsValid => _issues.Count == 0;
    public IReadOnlyList<RootMotionBakeSettingsValidationIssue> Issues => _issues;

    internal void Add(RootMotionBakeSettingsValidationCode code, string message)
    {
        _issues.Add(new RootMotionBakeSettingsValidationIssue(code, message));
    }
}
#endif
