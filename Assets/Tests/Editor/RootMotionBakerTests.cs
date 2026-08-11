#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RootMotionBakerTests
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
        DestroyLeakedPreviewRoots();
    }

    [Test]
    public void AnimationConfig_OwnsEditorOnlyRootMotionBakeContext()
    {
        AnimationConfig config = CreateScriptableObject<AnimationConfig>();
        Assert.IsFalse(config.ValidateRootMotionBakeSettings().IsValid);

        GameObject rig = CreateGenericReferenceRig(out _);
        config.EditorSetRootMotionBakeSettings(rig, 60);

        Assert.AreSame(rig, config.RootMotionReferenceRigPrefab);
        Assert.AreEqual(60, config.RootMotionSampleRate);
        Assert.IsTrue(config.ValidateRootMotionBakeSettings().IsValid);
    }

    [Test]
    public void BakeContext_RequiresCleanSingleAnimatorRigAndValidAvatar()
    {
        AnimationConfig config = CreateScriptableObject<AnimationConfig>();
        config.EditorSetRootMotionBakeSettings(null, 0);
        RootMotionBakeSettingsValidationResult missing = config.ValidateRootMotionBakeSettings();
        AssertSettingsCode(missing, RootMotionBakeSettingsValidationCode.MissingReferenceRig);
        AssertSettingsCode(missing, RootMotionBakeSettingsValidationCode.InvalidSampleRate);

        GameObject rig = CreateGenericReferenceRig(out _);
        config.EditorSetRootMotionBakeSettings(rig, 60);
        Assert.IsTrue(config.ValidateRootMotionBakeSettings().IsValid);

        rig.AddComponent<RootMotionBakerTestBehaviour>();
        AssertSettingsCode(config.ValidateRootMotionBakeSettings(), RootMotionBakeSettingsValidationCode.ContainsMonoBehaviour);
    }

    [Test]
    public void TransitionResolver_RequiresExactlyOneUniqueAnimationClip()
    {
        AnimationClip first = CreateClip("First");
        AnimationClip second = CreateClip("Second");
        TransitionAsset asset = CreateScriptableObject<TransitionAsset>();

        asset.Transition = new ClipTransition { Clip = first };
        Assert.IsTrue(RootMotionTransitionClipResolver.TryResolve(asset, out AnimationClip resolved, out RootMotionClipResolutionDiagnostic success));
        Assert.AreSame(first, resolved);
        Assert.AreEqual(RootMotionClipResolutionCode.None, success.Code);

        var duplicateMixer = new ManualMixerTransition();
        duplicateMixer.Animations = new Object[] { first, first };
        asset.Transition = duplicateMixer;
        Assert.IsTrue(RootMotionTransitionClipResolver.TryResolve(asset, out resolved, out _), "Repeated references to the same clip are not ambiguous.");
        Assert.AreSame(first, resolved);

        var multipleMixer = new ManualMixerTransition();
        multipleMixer.Animations = new Object[] { first, second };
        asset.Transition = multipleMixer;
        Assert.IsFalse(RootMotionTransitionClipResolver.TryResolve(asset, out _, out RootMotionClipResolutionDiagnostic multiple));
        Assert.AreEqual(RootMotionClipResolutionCode.MultipleAnimationClips, multiple.Code);
    }

    [Test]
    public void TransitionResolver_ReportsMissingAssetTransitionAndClip()
    {
        Assert.IsFalse(RootMotionTransitionClipResolver.TryResolve(null, out _, out RootMotionClipResolutionDiagnostic missingAsset));
        Assert.AreEqual(RootMotionClipResolutionCode.MissingTransitionAsset, missingAsset.Code);

        TransitionAsset asset = CreateScriptableObject<TransitionAsset>();
        asset.Transition = null;
        Assert.IsFalse(RootMotionTransitionClipResolver.TryResolve(asset, out _, out RootMotionClipResolutionDiagnostic missingTransition));
        Assert.AreEqual(RootMotionClipResolutionCode.MissingTransition, missingTransition.Code);

        asset.Transition = new ClipTransition();
        Assert.IsFalse(RootMotionTransitionClipResolver.TryResolve(asset, out _, out RootMotionClipResolutionDiagnostic missingClip));
        Assert.AreEqual(RootMotionClipResolutionCode.NoAnimationClip, missingClip.Code);
    }

    [Test]
    public void Baker_ContinuouslyEvaluatesSyntheticGenericRootMotionAndCleansPreview()
    {
        GameObject rig = CreateGenericReferenceRig(out _);
        AnimationConfig config = CreateConfig(rig, 10);
        AnimationClip clip = CreateSyntheticRootMotionClip();

        Assert.IsTrue(RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult result, out RootMotionBakeDiagnostic diagnostic), diagnostic.Message);

        Assert.AreEqual(11, result.SampleCount);
        Assert.AreEqual(0f, result.SampleTimes[0]);
        Assert.AreEqual(clip.length, result.SampleTimes[result.SampleCount - 1], 1e-6f);
        AssertVector(Vector3.zero, result.CumulativePositions[0]);
        AssertQuaternion(Quaternion.identity, result.CumulativeRotations[0]);
        AssertVector(new Vector3(0f, 0f, 2f), result.CumulativePositions[result.SampleCount - 1], 0.02f);
        AssertQuaternion(Quaternion.Euler(0f, 90f, 0f), result.CumulativeRotations[result.SampleCount - 1], 0.5f);
        AssertNoPreviewRoot();
    }

    [Test]
    public void Baker_UsesExactPartialFinalStep()
    {
        GameObject rig = CreateGenericReferenceRig(out _);
        AnimationConfig config = CreateConfig(rig, 4);
        AnimationClip clip = CreateSyntheticRootMotionClip(0.6f, 1.2f, 30f);

        Assert.IsTrue(RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult result, out RootMotionBakeDiagnostic diagnostic), diagnostic.Message);

        CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.5f, 0.6f }, result.SampleTimes.ToArray());
        Assert.AreEqual(clip.length, result.Duration, 1e-6f);
        AssertNoPreviewRoot();
    }

    [Test]
    public void Baker_EvaluationFailureStillCleansPreviewRoot()
    {
        GameObject rig = CreateGenericReferenceRig(out _);
        AnimationConfig config = CreateConfig(rig, 60);
        AnimationClip legacyClip = CreateSyntheticRootMotionClip();
        legacyClip.legacy = true;

        Assert.IsFalse(RootMotionBaker.TryBake(legacyClip, config, out _, out RootMotionBakeDiagnostic diagnostic));
        Assert.AreEqual(RootMotionBakeDiagnosticCode.EvaluationException, diagnostic.Code);
        AssertNoPreviewRoot();
    }

    [TestCase(HumanoidRigPath, HumanoidClipPath, true)]
    [TestCase(GenericRigPath, GenericClipPath, false)]
    public void Baker_EvaluatesRepositoryReferenceAvatar(
        string rigPath,
        string clipPath,
        bool expectHumanoid)
    {
        GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
        AnimationClip clip = LoadPrimaryClip(clipPath);
        Assert.NotNull(rig, rigPath);
        Assert.NotNull(clip, clipPath);

        Animator animator = rig.GetComponentInChildren<Animator>(true);
        Assert.NotNull(animator);
        Assert.NotNull(animator.avatar);
        Assert.AreEqual(expectHumanoid, animator.avatar.isHuman);

        AnimationConfig config = CreateConfig(rig, 60);
        RootMotionBakeSettingsValidationResult settingsValidation = config.ValidateRootMotionBakeSettings();
        Assert.IsTrue(settingsValidation.IsValid, JoinSettingsIssues(settingsValidation));

        Assert.IsTrue(RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult result, out RootMotionBakeDiagnostic diagnostic), diagnostic.Message);

        Assert.Greater(result.SampleCount, 1);
        Assert.AreEqual(0f, result.SampleTimes[0]);
        Assert.AreEqual(clip.length, result.SampleTimes[result.SampleCount - 1], 1e-5f);
        AssertVector(Vector3.zero, result.CumulativePositions[0]);
        AssertQuaternion(Quaternion.identity, result.CumulativeRotations[0]);
        float maximumTranslation = result.CumulativePositions.Max(position => position.magnitude);
        if (expectHumanoid)
        {
            Assert.Greater(maximumTranslation, 0.001f,
                $"'{clip.name}' produced no observable Humanoid root translation through the Manual Graph path.");
        }
        else
        {
            var importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
            Assert.NotNull(importer);
            Assert.IsTrue(string.IsNullOrEmpty(importer.motionNodeName),
                "This repository Generic fixture currently expects Unity to import no Root Motion Node.");
        }
        Assert.IsTrue(result.CumulativeRotations.All(IsFiniteUnitQuaternion));
        AssertNoPreviewRoot();
    }

    private GameObject CreateGenericReferenceRig(out Avatar avatar)
    {
        GameObject rig = Track(new GameObject("SyntheticReferenceRig"));
        GameObject rootBone = new GameObject("Root");
        rootBone.transform.SetParent(rig.transform, false);
        Animator animator = rig.AddComponent<Animator>();
        avatar = Track(AvatarBuilder.BuildGenericAvatar(rig, rootBone.name));
        Assert.NotNull(avatar);
        Assert.IsTrue(avatar.isValid);
        animator.avatar = avatar;
        return rig;
    }

    private AnimationConfig CreateConfig(GameObject rig, int sampleRate)
    {
        AnimationConfig config = CreateScriptableObject<AnimationConfig>();
        config.EditorSetRootMotionBakeSettings(rig, sampleRate);
        return config;
    }

    private AnimationClip CreateSyntheticRootMotionClip(float duration = 1f, float distance = 2f, float yaw = 90f)
    {
        AnimationClip clip = CreateClip("SyntheticRootMotion");
        clip.frameRate = 60f;
        clip.wrapMode = WrapMode.Once;

        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, duration, distance));

        float halfRadians = yaw * Mathf.Deg2Rad * 0.5f;
        float finalY = Mathf.Sin(halfRadians);
        float finalW = Mathf.Cos(halfRadians);
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.y", AnimationCurve.Linear(0f, 0f, duration, finalY));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.z", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.w", AnimationCurve.Linear(0f, 1f, duration, finalW));
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    private AnimationClip CreateClip(string clipName)
    {
        AnimationClip clip = Track(new AnimationClip { name = clipName });
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 0.1f, 0f));
        return clip;
    }

    private T CreateScriptableObject<T>() where T : ScriptableObject
    {
        return Track(ScriptableObject.CreateInstance<T>());
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

    private static void AssertSettingsCode(RootMotionBakeSettingsValidationResult result, RootMotionBakeSettingsValidationCode code)
    {
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == code), code.ToString());
    }

    private static string JoinSettingsIssues(RootMotionBakeSettingsValidationResult result)
    {
        return string.Join(" ", result.Issues.Select(issue => issue.Message));
    }

    private static bool IsFiniteUnitQuaternion(Quaternion value)
    {
        float sqrMagnitude = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
        return !float.IsNaN(sqrMagnitude) && !float.IsInfinity(sqrMagnitude) && Mathf.Abs(sqrMagnitude - 1f) <= 1e-3f;
    }

    private static void AssertNoPreviewRoot()
    {
        Assert.IsFalse(Resources.FindObjectsOfTypeAll<GameObject>().Any(gameObject => gameObject.name == RootMotionBaker.PreviewRootName));
    }

    private static void DestroyLeakedPreviewRoots()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].name == RootMotionBaker.PreviewRootName)
                Object.DestroyImmediate(objects[i]);
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

public sealed class RootMotionBakerTestBehaviour : MonoBehaviour
{
}
#endif
