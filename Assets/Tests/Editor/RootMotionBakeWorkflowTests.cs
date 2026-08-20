#if UNITY_EDITOR
using System.Linq;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RootMotionBakeWorkflowTests
{
    private const string TestRootPrefix = "Assets/__RootMotionBakeWorkflowTests_";
    private const string ProjectSampleConfigPath = "Assets/Create/Jaeger_AnimationConfig.asset";
    private static string _testRoot;

    [SetUp]
    public void SetUp()
    {
        _testRoot = TestRootPrefix + System.Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", _testRoot.Substring("Assets/".Length));
    }

    [TearDown]
    public void TearDown()
    {
        DestroyPreviewRoots();
        if (!string.IsNullOrEmpty(_testRoot) && AssetDatabase.IsValidFolder(_testRoot))
        {
            AssetDatabase.DeleteAsset(_testRoot);
            AssetDatabase.Refresh();
        }
        _testRoot = null;
    }

    [Test]
    public void Bake_StoresTrajectoryInsideConfigAndCreatesNoGeneratedAsset()
    {
        Fixture fixture = CreateFixture("Attack", 60);
        Assert.AreEqual(RootMotionEntryStatusCode.Missing, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0).Code);

        Assert.IsTrue(
            RootMotionBakeWorkflow.TryBakeEntry(fixture.Config, 0, out RootMotionBakeOperationResult result),
            result.Message);

        RootMotionTrajectory trajectory = fixture.Config.Entries[0].RootMotionTrajectory;
        Assert.NotNull(trajectory);
        Assert.AreSame(trajectory, result.Trajectory);
        Assert.AreSame(fixture.Clip, trajectory.SourceClip);
        Assert.AreEqual(60, trajectory.SampleRate);
        Assert.IsTrue(trajectory.ValidateData().IsValid);
        Assert.IsTrue(result.ValidationReport.IsValid, result.ValidationReport.Summary);
        Assert.IsFalse(AssetDatabase.IsValidFolder(_testRoot + "/Generated"));
        Assert.AreEqual(0, AssetDatabase.FindAssets("t:RootMotionTrajectory", new[] { _testRoot }).Length);

        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(fixture.Config), ImportAssetOptions.ForceUpdate);
        AnimationConfig reloaded = AssetDatabase.LoadAssetAtPath<AnimationConfig>(AssetDatabase.GetAssetPath(fixture.Config));
        Assert.NotNull(reloaded.Entries[0].RootMotionTrajectory);
        Assert.AreEqual(trajectory.SampleCount, reloaded.Entries[0].RootMotionTrajectory.SampleCount);
        Assert.IsTrue(reloaded.Entries[0].RootMotionTrajectory.ValidateData().IsValid);
        Assert.AreEqual(RootMotionEntryStatusCode.Ready, RootMotionBakeWorkflow.GetEntryStatus(reloaded, 0).Code);

        Editor editor = Editor.CreateEditor(reloaded);
        Assert.IsInstanceOf<AnimationConfigEditor>(editor);
        Object.DestroyImmediate(editor);
    }

    [Test]
    public void ProjectSampleConfig_ContainsValidEmbeddedTrajectory()
    {
        AnimationConfig config = AssetDatabase.LoadAssetAtPath<AnimationConfig>(ProjectSampleConfigPath);
        Assert.NotNull(config, ProjectSampleConfigPath);
        Assert.That(config.Entries.Count, Is.GreaterThan(0));
        Assert.NotNull(config.Entries[0].RootMotionTrajectory);
        Assert.AreEqual(144, config.Entries[0].RootMotionTrajectory.SampleCount);
        Assert.IsTrue(config.Entries[0].RootMotionTrajectory.ValidateData().IsValid);

        RootMotionEntryStatus status = RootMotionBakeWorkflow.GetEntryStatus(config, 0);
        Assert.That(
            status.Code,
            Is.EqualTo(RootMotionEntryStatusCode.Ready).Or.EqualTo(RootMotionEntryStatusCode.Stale),
            status.Message);
    }

    [Test]
    public void SettingsChange_MarksStaleAndRebakeReplacesEmbeddedData()
    {
        Fixture fixture = CreateFixture("Dash", 60);
        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult first), first.Message);

        RootMotionTrajectory original = first.Trajectory;
        string oldHash = original.DependencyHash;
        fixture.Config.EditorSetRootMotionBakeSettings(fixture.RigPrefab, 30);
        EditorUtility.SetDirty(fixture.Config);
        AssetDatabase.SaveAssetIfDirty(fixture.Config);

        RootMotionEntryStatus stale = RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0);
        Assert.AreEqual(RootMotionEntryStatusCode.Stale, stale.Code, stale.Message);
        Assert.AreNotEqual(oldHash, stale.ExpectedDependencyHash);

        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult rebake), rebake.Message);

        Assert.AreNotSame(original, rebake.Trajectory);
        Assert.AreEqual(30, rebake.Trajectory.SampleRate);
        Assert.AreNotEqual(oldHash, rebake.Trajectory.DependencyHash);
        Assert.AreEqual(RootMotionEntryStatusCode.Ready, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0).Code);
    }

    [Test]
    public void SourceClipAndReferenceRigChanges_MarkEmbeddedDataStale()
    {
        Fixture fixture = CreateFixture("Move", 60);
        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult bake), bake.Message);

        fixture.Clip.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.z",
            AnimationCurve.Linear(0f, 0f, 1.25f, 3f));
        EditorUtility.SetDirty(fixture.Clip);
        AssetDatabase.SaveAssetIfDirty(fixture.Clip);
        Assert.AreEqual(RootMotionEntryStatusCode.Stale, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0).Code);

        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult rebake), rebake.Message);
        GameObject replacementRig = CreateReferenceRig("Replacement");
        fixture.Config.EditorSetRootMotionBakeSettings(replacementRig, 60);
        EditorUtility.SetDirty(fixture.Config);
        AssetDatabase.SaveAssetIfDirty(fixture.Config);
        Assert.AreEqual(RootMotionEntryStatusCode.Stale, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0).Code);
    }

    [Test]
    public void FailedRebake_PreservesExistingEmbeddedTrajectory()
    {
        Fixture fixture = CreateFixture("Attack", 60);
        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult first), first.Message);

        RootMotionTrajectory original = first.Trajectory;
        string originalHash = original.DependencyHash;
        float[] originalTimes = original.SampleTimes.ToArray();

        AnimationClip legacyClip = CreateClip("LegacyFailure", 1f, 1f);
        TransitionAsset legacyTransition = CreateTransition("LegacyTransition", legacyClip);
        legacyClip.legacy = true;
        EditorUtility.SetDirty(legacyClip);
        fixture.Config.EditorSetEntries(new AnimationConfigEntry("Attack", legacyTransition, original));
        EditorUtility.SetDirty(fixture.Config);
        AssetDatabase.SaveAssets();

        Assert.IsFalse(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult failed));
        Assert.AreEqual(RootMotionBakeOperationResultCode.BakeFailed, failed.Code);
        Assert.AreSame(original, fixture.Config.Entries[0].RootMotionTrajectory);
        Assert.AreEqual(originalHash, original.DependencyHash);
        CollectionAssert.AreEqual(originalTimes, original.SampleTimes);
    }

    [Test]
    public void BakeAll_BakesSingleClipEntriesAndSkipsPoseOnlyMixer()
    {
        Fixture fixture = CreateFixture("First", 60);
        AnimationClip secondClip = CreateClip("SecondClip", 0.5f, 0.5f);
        TransitionAsset secondTransition = CreateTransition("SecondTransition", secondClip);
        TransitionAsset mixerTransition = CreateAsset<TransitionAsset>("PoseOnlyMixer.asset");
        mixerTransition.Transition = new ManualMixerTransition
        {
            Animations = new Object[] { fixture.Clip, secondClip },
        };
        EditorUtility.SetDirty(mixerTransition);
        fixture.Config.EditorSetEntries(
            new AnimationConfigEntry("First", fixture.Transition),
            new AnimationConfigEntry("Second", secondTransition),
            new AnimationConfigEntry("PoseOnly", mixerTransition));
        EditorUtility.SetDirty(fixture.Config);
        AssetDatabase.SaveAssets();

        RootMotionBakeBatchResult result = RootMotionBakeWorkflow.BakeAll(fixture.Config);

        Assert.IsTrue(result.IsSuccess, result.Summary);
        Assert.AreEqual(2, result.SuccessCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.AreEqual(0, result.FailureCount);
        Assert.NotNull(fixture.Config.Entries[0].RootMotionTrajectory);
        Assert.NotNull(fixture.Config.Entries[1].RootMotionTrajectory);
        Assert.IsNull(fixture.Config.Entries[2].RootMotionTrajectory);
        Assert.AreNotSame(
            fixture.Config.Entries[0].RootMotionTrajectory,
            fixture.Config.Entries[1].RootMotionTrajectory);
        Assert.AreEqual(RootMotionEntryStatusCode.PoseOnly, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 2).Code);
    }

    [Test]
    public void Clear_RemovesOnlyEmbeddedData()
    {
        Fixture fixture = CreateFixture("Owner", 60);
        Assert.IsTrue(RootMotionBakeWorkflow.TryBakeEntry(
            fixture.Config, 0, out RootMotionBakeOperationResult first), first.Message);
        Assert.NotNull(first.Trajectory);

        Assert.IsTrue(RootMotionBakeWorkflow.TryClearTrajectory(fixture.Config, 0, out string diagnostic), diagnostic);
        Assert.IsNull(fixture.Config.Entries[0].RootMotionTrajectory);
        Assert.AreEqual(RootMotionEntryStatusCode.Missing, RootMotionBakeWorkflow.GetEntryStatus(fixture.Config, 0).Code);
    }

    private static Fixture CreateFixture(string key, int sampleRate)
    {
        GameObject rigPrefab = CreateReferenceRig();
        AnimationClip clip = CreateClip("SourceClip", 1f, 2f);
        TransitionAsset transition = CreateTransition("Transition", clip);
        AnimationConfig config = CreateAsset<AnimationConfig>("AnimationConfig.asset");
        config.EditorSetRootMotionBakeSettings(rigPrefab, sampleRate);
        config.EditorSetEntries(new AnimationConfigEntry(key, transition));
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return new Fixture(config, rigPrefab, clip, transition);
    }

    private static GameObject CreateReferenceRig(string suffix = "")
    {
        var source = new GameObject("ReferenceRig");
        var rootBone = new GameObject("Root");
        rootBone.transform.SetParent(source.transform, false);
        Animator animator = source.AddComponent<Animator>();
        Avatar avatar = AvatarBuilder.BuildGenericAvatar(source, rootBone.name);
        Assert.NotNull(avatar);
        Assert.IsTrue(avatar.isValid);
        AssetDatabase.CreateAsset(avatar, _testRoot + "/ReferenceAvatar" + suffix + ".asset");
        animator.avatar = avatar;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, _testRoot + "/ReferenceRig" + suffix + ".prefab");
        Object.DestroyImmediate(source);
        Assert.NotNull(prefab);
        return prefab;
    }

    private static AnimationClip CreateClip(string name, float duration, float distance)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f, wrapMode = WrapMode.Once };
        AssetDatabase.CreateAsset(clip, _testRoot + "/" + name + ".anim");
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, duration, distance));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.x", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.y", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.z", AnimationCurve.Constant(0f, duration, 0f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.w", AnimationCurve.Constant(0f, duration, 1f));
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static TransitionAsset CreateTransition(string name, AnimationClip clip)
    {
        TransitionAsset transition = CreateAsset<TransitionAsset>(name + ".asset");
        transition.Transition = new ClipTransition { Clip = clip };
        EditorUtility.SetDirty(transition);
        AssetDatabase.SaveAssetIfDirty(transition);
        return transition;
    }

    private static T CreateAsset<T>(string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, _testRoot + "/" + fileName);
        return asset;
    }

    private static void DestroyPreviewRoots()
    {
        Object[] previews = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < previews.Length; i++)
        {
            if (previews[i] is GameObject gameObject
                && (gameObject.name == RootMotionBaker.PreviewRootName
                    || gameObject.name == RootMotionOracleEvaluator.PreviewRootName))
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }

    private readonly struct Fixture
    {
        public AnimationConfig Config { get; }
        public GameObject RigPrefab { get; }
        public AnimationClip Clip { get; }
        public TransitionAsset Transition { get; }

        public Fixture(AnimationConfig config, GameObject rigPrefab, AnimationClip clip, TransitionAsset transition)
        {
            Config = config;
            RigPrefab = rigPrefab;
            Clip = clip;
            Transition = transition;
        }
    }
}
#endif
