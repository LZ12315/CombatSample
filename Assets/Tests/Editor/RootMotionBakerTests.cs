#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RootMotionBakerTests
{
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
        DestroyPreviewRoots();
    }

    [Test]
    public void BakerAndValidator_MatchSyntheticGenericRootMotion()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 10);
        AnimationClip clip = CreateRootMotionClip(1f, 2f, 90f);

        Assert.IsTrue(
            RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult bake, out RootMotionBakeDiagnostic diagnostic),
            diagnostic.Message);

        Assert.AreEqual(11, bake.SampleCount);
        Assert.AreEqual(0f, bake.SampleTimes[0]);
        Assert.AreEqual(clip.length, bake.SampleTimes[bake.SampleCount - 1], 1e-6f);
        AssertVector(Vector3.zero, bake.CumulativePositions[0]);
        AssertQuaternion(Quaternion.identity, bake.CumulativeRotations[0]);
        AssertVector(Vector3.forward * 2f, bake.CumulativePositions[bake.SampleCount - 1], 0.02f);
        AssertQuaternion(Quaternion.Euler(0f, 90f, 0f), bake.CumulativeRotations[bake.SampleCount - 1], 0.5f);

        Assert.IsTrue(
            RootMotionBakeValidator.TryValidate(
                bake,
                out RootMotionValidationReport report,
                out RootMotionValidationDiagnostic validationDiagnostic),
            validationDiagnostic.Message);
        Assert.IsTrue(report.IsValid, report.Summary);
        AssertNoPreviewRoots();
    }

    [Test]
    public void Baker_UsesExactPartialFinalStep()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 4);
        AnimationClip clip = CreateRootMotionClip(0.6f, 1.2f, 30f);

        Assert.IsTrue(
            RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult bake, out RootMotionBakeDiagnostic diagnostic),
            diagnostic.Message);

        CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.5f, 0.6f }, bake.SampleTimes.ToArray());
        Assert.AreEqual(clip.length, bake.Duration, 1e-6f);
        AssertNoPreviewRoots();
    }

    [Test]
    public void Validator_RejectsTamperedSyntheticBake()
    {
        GameObject rig = CreateGenericReferenceRig();
        AnimationConfig config = CreateConfig(rig, 10);
        AnimationClip clip = CreateRootMotionClip(0.5f, 1f, 45f);

        Assert.IsTrue(
            RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult original, out RootMotionBakeDiagnostic diagnostic),
            diagnostic.Message);

        var positions = original.CumulativePositions.ToList();
        positions[2] += Vector3.right * 0.2f;
        var tampered = new RootMotionBakeResult(
            original.SourceClip,
            original.AnimationConfig,
            original.BakerVersion,
            original.SampleRate,
            original.Duration,
            original.SampleTimes.ToList(),
            positions,
            original.CumulativeRotations.ToList());

        Assert.IsTrue(
            RootMotionBakeValidator.TryValidate(
                tampered,
                out RootMotionValidationReport report,
                out RootMotionValidationDiagnostic validationDiagnostic),
            validationDiagnostic.Message);
        Assert.IsFalse(report.IsValid);
        Assert.Greater(report.MaximumPositionError, config.RootMotionPositionTolerance);
        AssertNoPreviewRoots();
    }

    private GameObject CreateGenericReferenceRig()
    {
        GameObject rig = Track(new GameObject("SyntheticRootMotionRig"));
        GameObject rootBone = new GameObject("Root");
        rootBone.transform.SetParent(rig.transform, false);
        Animator animator = rig.AddComponent<Animator>();
        Avatar avatar = Track(AvatarBuilder.BuildGenericAvatar(rig, rootBone.name));
        Assert.NotNull(avatar);
        Assert.IsTrue(avatar.isValid);
        animator.avatar = avatar;
        return rig;
    }

    private AnimationConfig CreateConfig(GameObject rig, int sampleRate)
    {
        AnimationConfig config = Track(ScriptableObject.CreateInstance<AnimationConfig>());
        config.EditorSetRootMotionBakeSettings(rig, sampleRate, 0.001f, 0.1f);
        return config;
    }

    private AnimationClip CreateRootMotionClip(float duration, float distance, float yaw)
    {
        AnimationClip clip = Track(new AnimationClip
        {
            name = "SyntheticRootMotion",
            frameRate = 60f,
            wrapMode = WrapMode.Once,
        });

        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, duration, distance));

        float halfRadians = yaw * Mathf.Deg2Rad * 0.5f;
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.y", AnimationCurve.Linear(0f, 0f, duration, Mathf.Sin(halfRadians)));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.z", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.w", AnimationCurve.Linear(0f, 1f, duration, Mathf.Cos(halfRadians)));
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    private T Track<T>(T value) where T : Object
    {
        _createdObjects.Add(value);
        return value;
    }

    private static void AssertNoPreviewRoots()
    {
        Assert.IsFalse(Resources.FindObjectsOfTypeAll<GameObject>().Any(go =>
            go.name == RootMotionBaker.PreviewRootName ||
            go.name == RootMotionOracleEvaluator.PreviewRootName));
    }

    private static void DestroyPreviewRoots()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null &&
                (objects[i].name == RootMotionBaker.PreviewRootName ||
                 objects[i].name == RootMotionOracleEvaluator.PreviewRootName))
            {
                Object.DestroyImmediate(objects[i]);
            }
        }
    }

    private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
    {
        Assert.LessOrEqual((expected - actual).magnitude, tolerance, $"Expected {expected}, got {actual}.");
    }

    private static void AssertQuaternion(Quaternion expected, Quaternion actual, float tolerance = 0.01f)
    {
        Assert.LessOrEqual(Quaternion.Angle(expected, actual), tolerance, $"Expected {expected.eulerAngles}, got {actual.eulerAngles}.");
    }
}
#endif
