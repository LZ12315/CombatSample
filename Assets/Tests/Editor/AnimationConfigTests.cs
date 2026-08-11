#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Animancer;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimationConfigTests
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
    }

    [Test]
    public void Lookup_IsCaseSensitiveAndResolvesTransitionAndOptionalTrajectory()
    {
        AnimationConfig config = Create<AnimationConfig>();
        TransitionAsset upperTransition = Create<TransitionAsset>();
        TransitionAsset lowerTransition = Create<TransitionAsset>();
        RootMotionTrajectory trajectory = new RootMotionTrajectory();
        config.EditorSetRootMotionBakeSettings(CreateReferenceRig(), 60);
        var upper = new AnimationConfigEntry("Run", upperTransition, trajectory);
        var lower = new AnimationConfigEntry("run", lowerTransition);
        config.EditorSetEntries(upper, lower);

        Assert.IsTrue(config.TryGetEntry("Run", out AnimationConfigEntry resolvedUpper));
        Assert.AreSame(upper, resolvedUpper);
        Assert.IsTrue(config.TryGetTransition("run", out TransitionAsset resolvedTransition));
        Assert.AreSame(lowerTransition, resolvedTransition);
        Assert.IsTrue(config.TryGetTrajectory("Run", out RootMotionTrajectory resolvedTrajectory));
        Assert.AreSame(trajectory, resolvedTrajectory);
        Assert.IsFalse(config.TryGetTrajectory("run", out _), "A pose-only entry may omit trajectory data.");
        Assert.IsFalse(config.TryGetEntry("RUN", out _));
        Assert.IsTrue(config.ValidateData().IsValid);
    }

    [Test]
    public void DuplicateKey_IsAmbiguousAndCannotResolveEitherEntry()
    {
        AnimationConfig config = Create<AnimationConfig>();
        var first = new AnimationConfigEntry("Attack", Create<TransitionAsset>());
        var second = new AnimationConfigEntry("Attack", Create<TransitionAsset>());
        config.EditorSetEntries(first, second);

        Assert.IsFalse(config.TryGetEntry("Attack", out _));
        AnimationConfigValidationResult validation = config.ValidateData();
        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.Issues.Any(issue => issue.Code == AnimationConfigValidationCode.DuplicateKey));
    }

    [Test]
    public void Validation_ReportsNullEmptyAndMissingTransitionWithoutMutatingData()
    {
        AnimationConfig config = Create<AnimationConfig>();
        var emptyKey = new AnimationConfigEntry("   ", Create<TransitionAsset>());
        var missingTransition = new AnimationConfigEntry("Idle", null);
        config.EditorSetEntries(null, emptyKey, missingTransition);

        AnimationConfigValidationResult validation = config.ValidateData();

        Assert.IsTrue(validation.Issues.Any(issue => issue.Code == AnimationConfigValidationCode.NullEntry));
        Assert.IsTrue(validation.Issues.Any(issue => issue.Code == AnimationConfigValidationCode.EmptyKey));
        Assert.IsTrue(validation.Issues.Any(issue => issue.Code == AnimationConfigValidationCode.MissingTransition));
        Assert.AreEqual(3, config.Entries.Count);
    }

    [Test]
    public void EditorSetEntries_InvalidatesPreviouslyBuiltLookup()
    {
        AnimationConfig config = Create<AnimationConfig>();
        var first = new AnimationConfigEntry("First", Create<TransitionAsset>());
        config.EditorSetEntries(first);
        Assert.IsTrue(config.TryGetEntry("First", out _));

        var second = new AnimationConfigEntry("Second", Create<TransitionAsset>());
        config.EditorSetEntries(second);

        Assert.IsFalse(config.TryGetEntry("First", out _));
        Assert.IsTrue(config.TryGetEntry("Second", out AnimationConfigEntry resolved));
        Assert.AreSame(second, resolved);
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        _createdObjects.Add(value);
        return value;
    }

    private GameObject CreateReferenceRig()
    {
        GameObject rig = new GameObject("AnimationConfigTestRig");
        GameObject root = new GameObject("Root");
        root.transform.SetParent(rig.transform, false);
        Animator animator = rig.AddComponent<Animator>();
        Avatar avatar = AvatarBuilder.BuildGenericAvatar(rig, root.name);
        animator.avatar = avatar;
        _createdObjects.Add(rig);
        _createdObjects.Add(avatar);
        return rig;
    }
}
#endif
