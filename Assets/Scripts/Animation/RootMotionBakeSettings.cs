#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only extraction settings used by AnimationAsset root-motion baking.
/// The type stays in the runtime assembly so the
/// serialized AnimationAsset can expose it to editor tooling without a reverse
/// reference to Assembly-CSharp-Editor.
/// </summary>
[System.Serializable]
public sealed class RootMotionBakeSettings
{
    // Retained for existing AnimationAsset inline bake settings. New assets use AnimationRigAsset.
    [SerializeField, HideInInspector] private GameObject _referenceRigPrefab;
    [SerializeField, Min(1)] private int _sampleRate = 60;
    [SerializeField, Min(0f)] private float _positionTolerance = 0.001f;
    [SerializeField, Min(0f)] private float _rotationToleranceDegrees = 0.1f;

    public GameObject ReferenceRigPrefab => _referenceRigPrefab;
    public int SampleRate => _sampleRate;
    public float PositionTolerance => _positionTolerance;
    public float RotationToleranceDegrees => _rotationToleranceDegrees;

    public RootMotionBakeSettingsValidationResult Validate()
    {
        var result = new RootMotionBakeSettingsValidationResult();
        if (_sampleRate <= 0)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidSampleRate, "Sample rate must be greater than zero.");
        if (!IsFinite(_positionTolerance) || _positionTolerance < 0f)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidPositionTolerance, "Position tolerance must be finite and non-negative.");
        if (!IsFinite(_rotationToleranceDegrees) || _rotationToleranceDegrees < 0f)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidRotationTolerance, "Rotation tolerance must be finite and non-negative.");

        if (_referenceRigPrefab == null)
        {
            result.Add(RootMotionBakeSettingsValidationCode.MissingReferenceRig, "Reference Rig Prefab is missing.");
            return result;
        }

        Animator[] animators = _referenceRigPrefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
        {
            result.Add(RootMotionBakeSettingsValidationCode.MissingAnimator, "Reference Rig must contain one Animator.");
            return result;
        }
        if (animators.Length != 1)
        {
            result.Add(RootMotionBakeSettingsValidationCode.MultipleAnimators, $"Reference Rig must contain exactly one Animator, but found {animators.Length}.");
            return result;
        }

        Animator animator = animators[0];
        if (animator.avatar == null || !animator.avatar.isValid)
            result.Add(RootMotionBakeSettingsValidationCode.InvalidAvatar, "Reference Animator must use a valid Avatar.");

        Vector3 scale = animator.transform.lossyScale;
        if (!ApproximatelyOne(scale.x) || !ApproximatelyOne(scale.y) || !ApproximatelyOne(scale.z))
            result.Add(RootMotionBakeSettingsValidationCode.NonUnitAnimatorScale, $"Reference Animator must have unit world scale, but found {scale}.");

        MonoBehaviour[] behaviours = _referenceRigPrefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            string typeName = behaviour != null ? behaviour.GetType().Name : "Missing Script";
            result.Add(RootMotionBakeSettingsValidationCode.ContainsMonoBehaviour, $"Reference Rig must be animation-only and cannot contain {typeName}.");
        }
        return result;
    }

    public bool TryGetAnimator(out Animator animator)
    {
        animator = null;
        if (_referenceRigPrefab == null)
            return false;

        Animator[] animators = _referenceRigPrefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 1)
            return false;

        animator = animators[0];
        return true;
    }

    public void EditorSet(
        GameObject referenceRigPrefab,
        int sampleRate,
        float positionTolerance = 0.001f,
        float rotationToleranceDegrees = 0.1f)
    {
        _referenceRigPrefab = referenceRigPrefab;
        _sampleRate = sampleRate;
        _positionTolerance = positionTolerance;
        _rotationToleranceDegrees = rotationToleranceDegrees;
    }

    private static bool ApproximatelyOne(float value) => Mathf.Abs(value - 1f) <= 1e-4f;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

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
    private readonly List<RootMotionBakeSettingsValidationIssue> _issues = new();

    public bool IsValid => _issues.Count == 0;
    public IReadOnlyList<RootMotionBakeSettingsValidationIssue> Issues => _issues;

    internal void Add(RootMotionBakeSettingsValidationCode code, string message)
    {
        _issues.Add(new RootMotionBakeSettingsValidationIssue(code, message));
    }
}
// Shared by authoring and locomotion binding; the dependency hash format stays unchanged.
internal static class RootMotionBakeSource
{
    internal static bool TryCompute(
        AnimationClip clip,
        RootMotionBakeSettings settings,
        out string dependencyHash,
        out string diagnostic)
    {
        dependencyHash = string.Empty;
        diagnostic = string.Empty;

        if (clip == null)
        {
            diagnostic = "AnimationClip is missing.";
            return false;
        }

        if (settings == null)
        {
            diagnostic = "Root Motion Bake Settings are missing.";
            return false;
        }

        RootMotionBakeSettingsValidationResult validation = settings.Validate();
        if (!validation.IsValid)
        {
            diagnostic = JoinSettingsIssues(validation);
            return false;
        }

        settings.TryGetAnimator(out Animator animator);
        var material = new StringBuilder(512);
        material.Append("RootMotionBakerVersion=").Append(RootMotionTrajectory.CurrentBakerVersion).Append('\n');
        material.Append("SampleRate=").Append(settings.SampleRate).Append('\n');
        material.Append("PositionTolerance=")
            .Append(settings.PositionTolerance.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        material.Append("RotationTolerance=")
            .Append(settings.RotationToleranceDegrees.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        if (!AppendAssetIdentity(material, "Clip", clip, out diagnostic))
            return false;
        material.Append("ClipLength=").Append(clip.length.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        material.Append("ClipFrameRate=").Append(clip.frameRate.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        if (!AppendAssetIdentity(material, "ReferenceRig", settings.ReferenceRigPrefab, out diagnostic)
            || !AppendAssetIdentity(material, "Avatar", animator != null ? animator.avatar : null, out diagnostic))
        {
            return false;
        }

        dependencyHash = Hash128.Compute(material.ToString()).ToString();
        return true;
    }

    private static bool AppendAssetIdentity(
        StringBuilder material,
        string label,
        Object asset,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        material.Append(label).Append('=');
        if (asset == null)
        {
            diagnostic = $"{label} dependency is missing.";
            return false;
        }

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)
            || string.IsNullOrEmpty(guid))
        {
            diagnostic = $"{label} '{asset.name}' must be a persistent asset before baking.";
            return false;
        }

        material.Append(guid).Append(':').Append(localId);

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            diagnostic = $"{label} '{asset.name}' has no asset path.";
            return false;
        }

        material.Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
        material.Append('\n');
        return true;
    }

    private static string JoinSettingsIssues(RootMotionBakeSettingsValidationResult validation)
    {
        var message = new StringBuilder();
        for (int i = 0; i < validation.Issues.Count; i++)
        {
            if (message.Length > 0)
                message.Append(' ');
            message.Append(validation.Issues[i].Message);
        }
        return message.ToString();
    }
}
#endif
