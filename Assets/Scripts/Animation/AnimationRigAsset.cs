using UnityEngine;

/// <summary>
/// Editor authoring context shared by AnimationAssets that use the same skeleton.
/// Runtime animation playback never looks this asset up.
/// </summary>
[CreateAssetMenu(fileName = "AnimationRigAsset", menuName = "CombatSample/Animation/Animation Rig")]
public sealed class AnimationRigAsset : ScriptableObject
{
#if UNITY_EDITOR
    [SerializeField] private GameObject _bakeRigPrefab;
    [SerializeField] private GameObject _defaultPreviewPrefab;
    [SerializeField] private AnimationRigBakeSettings _bakeSettings = new AnimationRigBakeSettings();

    [Tooltip("Paths relative to the Animator. Empty paths use Humanoid foot bones.")]
    [SerializeField] private string _leftFootPath;
    [SerializeField] private string _rightFootPath;

    public GameObject BakeRigPrefab => _bakeRigPrefab;
    public GameObject DefaultPreviewPrefab => _defaultPreviewPrefab;
    public AnimationRigBakeSettings BakeSettings => _bakeSettings;

    /// <summary>Creates the transient combined settings expected by the existing proven bake pipeline.</summary>
    public RootMotionBakeSettings CreateEffectiveBakeSettings()
    {
        var settings = _bakeSettings?.CreateEffectiveSettings(_bakeRigPrefab);
        settings?.EditorSetFeet(_leftFootPath, _rightFootPath);
        return settings;
    }

    public void EditorSet(
        GameObject bakeRigPrefab,
        GameObject defaultPreviewPrefab,
        int sampleRate = 60,
        float positionTolerance = 0.001f,
        float rotationToleranceDegrees = 0.1f)
    {
        _bakeRigPrefab = bakeRigPrefab;
        _defaultPreviewPrefab = defaultPreviewPrefab;
        _bakeSettings ??= new AnimationRigBakeSettings();
        _bakeSettings.EditorSet(sampleRate, positionTolerance, rotationToleranceDegrees);
    }
#endif
}

#if UNITY_EDITOR
[System.Serializable]
public sealed class AnimationRigBakeSettings
{
    [SerializeField, Min(1)] private int _sampleRate = 60;
    [SerializeField, Min(0f)] private float _positionTolerance = 0.001f;
    [SerializeField, Min(0f)] private float _rotationToleranceDegrees = 0.1f;

    public int SampleRate => _sampleRate;
    public float PositionTolerance => _positionTolerance;
    public float RotationToleranceDegrees => _rotationToleranceDegrees;

    internal RootMotionBakeSettings CreateEffectiveSettings(GameObject bakeRigPrefab)
    {
        var settings = new RootMotionBakeSettings();
        settings.EditorSet(bakeRigPrefab, _sampleRate, _positionTolerance, _rotationToleranceDegrees);
        return settings;
    }

    public void EditorSet(
        int sampleRate,
        float positionTolerance = 0.001f,
        float rotationToleranceDegrees = 0.1f)
    {
        _sampleRate = sampleRate;
        _positionTolerance = positionTolerance;
        _rotationToleranceDegrees = rotationToleranceDegrees;
    }
}
#endif
