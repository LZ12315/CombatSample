#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RootMotionBakeValidatorTests
{
    private const string HumanoidRigPath = "Assets/Resources/Models/Kiana/Avatar_Kiana_C2_Model.FBX";
    private const string HumanoidClipPath = "Assets/Resources/Animations/Kiana/Battle/Clip/Avatar_Kiana_C2_Ani_Attack_1_fix.FBX";
    private const string GenericRigPath = "Assets/Resources/Models/Kiana/Avatar_Kiana_C2_Model_RootBone.fbx";
    private const string GenericClipPath = "Assets/Resources/Animations/Kiana/RootBone/Kiana_Attack1.fbx";

    private readonly List<Object> _createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
        DestroyPreviewRoots(RootMotionBaker.PreviewRootName);
        DestroyPreviewRoots(RootMotionOracleEvaluator.PreviewRootName);
    }

    [Test]
    public void Validator_MatchesIndependentOracleForTranslationRootYAndFastRotation()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 60, 0.001f, 0.1f);
        AnimationClip clip = CreateRootMotionClip(
            duration: 0.2f,
            finalPosition: new Vector3(1.5f, 0.75f, 2.25f),
            finalYaw: 170f);

        RootMotionBakeResult bake = Bake(clip, config);
        Assert.IsTrue(RootMotionBakeValidator.TryValidate(bake, out RootMotionValidationReport report, out RootMotionValidationDiagnostic diagnostic), diagnostic.Message);

        Assert.IsTrue(report.IsValid, report.Summary);
        Assert.AreEqual(bake.SampleCount, report.SamplesCompared);
        Assert.LessOrEqual(report.MaximumPositionError, config.RootMotionPositionTolerance);
        Assert.LessOrEqual(report.MaximumRotationErrorDegrees, config.RootMotionRotationToleranceDegrees);
        AssertNoPreviewRoots();
    }

    [Test]
    public void Validator_MatchesIndependentOracleForInPlaceClip()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 30, 0.001f, 0.1f);
        AnimationClip clip = CreateRootMotionClip(0.3f, Vector3.zero, 0f);

        RootMotionBakeResult bake = Bake(clip, config);
        Assert.IsTrue(RootMotionBakeValidator.TryValidate(bake, out RootMotionValidationReport report, out RootMotionValidationDiagnostic diagnostic), diagnostic.Message);

        Assert.IsTrue(report.IsValid, report.Summary);
        Assert.LessOrEqual(report.MaximumPositionError, 1e-6f);
        Assert.LessOrEqual(report.MaximumRotationErrorDegrees, 0.001f);
    }

    [Test]
    public void Validator_ReportsMaximumErrorIndexTimeAndExceededChannels()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 10, 0.001f, 0.1f);
        AnimationClip clip = CreateRootMotionClip(0.5f, Vector3.forward, 45f);
        RootMotionBakeResult original = Bake(clip, config);
        var times = original.SampleTimes.ToList();
        var positions = original.CumulativePositions.ToList();
        var rotations = original.CumulativeRotations.ToList();
        int tamperedIndex = 3;
        positions[tamperedIndex] += Vector3.right * 0.2f;
        rotations[tamperedIndex] = rotations[tamperedIndex] * Quaternion.Euler(0f, 15f, 0f);
        var tampered = new RootMotionBakeResult(
            original.SourceClip,
            original.AnimationConfig,
            original.BakerVersion,
            original.SampleRate,
            original.Duration,
            times,
            positions,
            rotations);

        Assert.IsTrue(RootMotionBakeValidator.TryValidate(tampered, out RootMotionValidationReport report, out RootMotionValidationDiagnostic diagnostic), diagnostic.Message);

        Assert.IsFalse(report.IsValid);
        Assert.IsTrue((report.Failures & RootMotionValidationFailure.PositionToleranceExceeded) != 0);
        Assert.IsTrue((report.Failures & RootMotionValidationFailure.RotationToleranceExceeded) != 0);
        Assert.AreEqual(tamperedIndex, report.MaximumPositionErrorIndex);
        Assert.AreEqual(tamperedIndex, report.MaximumRotationErrorIndex);
        Assert.AreEqual(times[tamperedIndex], report.MaximumPositionErrorTime, 1e-6f);
        Assert.AreEqual(times[tamperedIndex], report.MaximumRotationErrorTime, 1e-6f);
        StringAssert.Contains("Validation failed", report.Summary);
    }

    [Test]
    public void Validator_ReportsNonIdentityStartSample()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 10, 0.001f, 0.1f);
        AnimationClip clip = CreateRootMotionClip(0.5f, Vector3.forward, 45f);
        RootMotionBakeResult original = Bake(clip, config);
        var positions = original.CumulativePositions.ToList();
        var rotations = original.CumulativeRotations.ToList();
        positions[0] = new Vector3(0.01f, 0f, 0f);
        rotations[0] = Quaternion.Euler(0f, 1f, 0f);
        var malformed = new RootMotionBakeResult(
            original.SourceClip,
            original.AnimationConfig,
            original.BakerVersion,
            original.SampleRate,
            original.Duration,
            original.SampleTimes.ToList(),
            positions,
            rotations);

        Assert.IsTrue(RootMotionBakeValidator.TryValidate(malformed, out RootMotionValidationReport report, out RootMotionValidationDiagnostic diagnostic), diagnostic.Message);

        Assert.IsFalse(report.IsValid);
        Assert.IsTrue((report.Failures & RootMotionValidationFailure.NonIdentityStart) != 0);
        StringAssert.Contains(nameof(RootMotionValidationFailure.NonIdentityStart), report.Summary);
    }

    [Test]
    public void Validator_RejectsBakeResultWhoseDurationGridIsInvalid()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 10, 0.001f, 0.1f);
        AnimationClip clip = CreateRootMotionClip(0.5f, Vector3.forward, 0f);
        RootMotionBakeResult original = Bake(clip, config);
        var times = original.SampleTimes.ToList();
        times[times.Count - 1] -= 0.01f;
        var malformed = new RootMotionBakeResult(
            original.SourceClip,
            original.AnimationConfig,
            original.BakerVersion,
            original.SampleRate,
            original.Duration,
            times,
            original.CumulativePositions.ToList(),
            original.CumulativeRotations.ToList());

        Assert.IsFalse(RootMotionBakeValidator.TryValidate(malformed, out _, out RootMotionValidationDiagnostic diagnostic));
        Assert.AreEqual(RootMotionValidationDiagnosticCode.OracleEvaluationFailed, diagnostic.Code);
        StringAssert.Contains("final Oracle sample time", diagnostic.Message);
    }

    [TestCase(HumanoidRigPath, HumanoidClipPath, true)]
    [TestCase(GenericRigPath, GenericClipPath, false)]
    public void Validator_MatchesRepositoryAvatarUnderCurrentImporterSemantics(
        string rigPath,
        string clipPath,
        bool expectNonZeroMotion)
    {
        GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
        AnimationClip clip = LoadPrimaryClip(clipPath);
        Assert.NotNull(rig, rigPath);
        Assert.NotNull(clip, clipPath);

        AnimationConfig config = CreateConfig(rig, 60, 0.001f, 0.1f);
        RootMotionBakeResult bake = Bake(clip, config);
        Assert.IsTrue(RootMotionBakeValidator.TryValidate(bake, out RootMotionValidationReport report, out RootMotionValidationDiagnostic diagnostic), diagnostic.Message);

        Assert.IsTrue(report.IsValid, report.Summary);
        Assert.LessOrEqual(report.MaximumPositionError, config.RootMotionPositionTolerance);
        Assert.LessOrEqual(report.MaximumRotationErrorDegrees, config.RootMotionRotationToleranceDegrees);
        float maximumMotion = bake.CumulativePositions.Max(position => position.magnitude);
        if (expectNonZeroMotion)
        {
            Assert.Greater(maximumMotion, 0.001f);
        }
        else
        {
            var importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
            Assert.NotNull(importer);
            Assert.IsTrue(string.IsNullOrEmpty(importer.motionNodeName));
            Assert.LessOrEqual(maximumMotion, 1e-5f);
        }

        AssertNoPreviewRoots();
    }

    [Test]
    public void AnimationConfig_RejectsInvalidOracleTolerances()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 60, -1f, float.NaN);

        RootMotionBakeSettingsValidationResult result = config.ValidateRootMotionBakeSettings();

        Assert.IsTrue(result.Issues.Any(issue => issue.Code == RootMotionBakeSettingsValidationCode.InvalidPositionTolerance));
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == RootMotionBakeSettingsValidationCode.InvalidRotationTolerance));
    }

    private RootMotionBakeResult Bake(AnimationClip clip, AnimationConfig config)
    {
        Assert.IsTrue(RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult result, out RootMotionBakeDiagnostic diagnostic), diagnostic.Message);
        return result;
    }

    private GameObject CreateGenericReferenceRig()
    {
        GameObject rig = Track(new GameObject("OracleSyntheticRig"));
        GameObject rootBone = new GameObject("Root");
        rootBone.transform.SetParent(rig.transform, false);
        Animator animator = rig.AddComponent<Animator>();
        Avatar avatar = Track(AvatarBuilder.BuildGenericAvatar(rig, rootBone.name));
        Assert.NotNull(avatar);
        Assert.IsTrue(avatar.isValid);
        animator.avatar = avatar;
        return rig;
    }

    private AnimationConfig CreateConfig(
        GameObject rig,
        int sampleRate,
        float positionTolerance,
        float rotationTolerance)
    {
        AnimationConfig config = Track(ScriptableObject.CreateInstance<AnimationConfig>());
        config.EditorSetRootMotionBakeSettings(rig, sampleRate, positionTolerance, rotationTolerance);
        return config;
    }

    private AnimationClip CreateRootMotionClip(float duration, Vector3 finalPosition, float finalYaw)
    {
        AnimationClip clip = Track(new AnimationClip { name = "OracleSyntheticRootMotion", frameRate = 60f, wrapMode = WrapMode.Once });
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, duration, finalPosition.x));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.y", AnimationCurve.Linear(0f, 0f, duration, finalPosition.y));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, duration, finalPosition.z));

        float halfRadians = finalYaw * Mathf.Deg2Rad * 0.5f;
        float finalY = Mathf.Sin(halfRadians);
        float finalW = Mathf.Cos(halfRadians);
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.y", AnimationCurve.Linear(0f, 0f, duration, finalY));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.z", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.w", AnimationCurve.Linear(0f, 1f, duration, finalW));
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    private T Track<T>(T value) where T : Object
    {
        _createdObjects.Add(value);
        return value;
    }

    private static AnimationClip LoadPrimaryClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
    }

    private static void AssertNoPreviewRoots()
    {
        Assert.IsFalse(Resources.FindObjectsOfTypeAll<GameObject>().Any(gameObject => gameObject.name == RootMotionBaker.PreviewRootName));
        Assert.IsFalse(Resources.FindObjectsOfTypeAll<GameObject>().Any(gameObject => gameObject.name == RootMotionOracleEvaluator.PreviewRootName));
    }

    private static void DestroyPreviewRoots(string previewName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].name == previewName)
                Object.DestroyImmediate(objects[i]);
        }
    }
}
#endif
