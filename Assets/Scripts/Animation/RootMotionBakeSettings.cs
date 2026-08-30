#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Editor-only extraction settings shared by legacy AnimationConfig and the
/// Action V1 AnimationAsset. The type stays in the runtime assembly so the
/// serialized AnimationAsset can expose it to editor tooling without a reverse
/// reference to Assembly-CSharp-Editor.
/// </summary>
[System.Serializable]
public sealed class RootMotionBakeSettings
{
    [SerializeField] private GameObject _referenceRigPrefab;
    [SerializeField, Min(1)] private int _sampleRate = 60;
    [SerializeField, Min(0f)] private float _positionTolerance = 0.001f;
    [SerializeField, Min(0f)] private float _rotationToleranceDegrees = 0.1f;

    public GameObject ReferenceRigPrefab => _referenceRigPrefab;
    public int SampleRate => _sampleRate;
    public float PositionTolerance => _positionTolerance;
    public float RotationToleranceDegrees => _rotationToleranceDegrees;

    public static RootMotionBakeSettings FromLegacy(AnimationConfig config)
    {
        if (config == null)
            return null;

        return new RootMotionBakeSettings
        {
            _referenceRigPrefab = config.RootMotionReferenceRigPrefab,
            _sampleRate = config.RootMotionSampleRate,
            _positionTolerance = config.RootMotionPositionTolerance,
            _rotationToleranceDegrees = config.RootMotionRotationToleranceDegrees,
        };
    }

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
#endif
