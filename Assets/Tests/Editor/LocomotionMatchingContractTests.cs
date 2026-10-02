#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LocomotionMatchingContractTests
{
    private readonly List<Object> _objects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void LoopingMove_UsesTheFullClipRootDisplacement()
    {
        AnimationAsset asset = Clip("Straight loop", 1f, 2f);
        Assert.That(asset.RootMotionData.TryExtract(0f, asset.Clip.length, out var cycle), Is.True);
        Assert.That(cycle.Position.z / asset.Clip.length, Is.EqualTo(2f).Within(1e-5));
    }

    [Test]
    public void VelocityFeedback_RejectsForeignMotionAndRemovesThePublishedTimeScaleOnce()
    {
        var filter = new LocomotionVelocityFeedback();
        var policy = Policy(0.5f);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy)), out _), Is.False);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy)), out float speed), Is.True);
        Assert.That(speed, Is.EqualTo(2f));
        for (int reason = 0; reason < 7; reason++)
            Assert.That(filter.TryGetSpeed(Context(policy, Result(policy, reason)), out _), Is.False);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy), action: 1), out _), Is.False);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy)), out _), Is.False);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy)), out _), Is.True);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy), dt: 0f), out _), Is.False);
        Assert.That(filter.TryGetSpeed(Context(policy, Result(policy)), out _), Is.False);
        var changed = Policy(1f);
        Assert.That(filter.TryGetSpeed(Context(changed, Result(policy)), out _), Is.False);
        filter.Reset();
        Assert.That(filter.TryGetSpeed(Context(changed, Result(changed)), out _), Is.False);
    }

    [Test]
    public void PlayRate_IsBoundedSmoothedAndFrozenAtZeroDelta()
    {
        float slow = 1f;
        for (int i = 0; i < 100; i++) slow = LocomotionPlayRateMatching.Update(slow, 0f, 2f, 0.1f);
        Assert.That(slow, Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(LocomotionPlayRateMatching.Update(slow, 20f, 2f, 0f), Is.EqualTo(slow));
        Assert.That(LocomotionPlayRateMatching.Update(slow, 2f, 0f, 0.1f), Is.EqualTo(1f));
        float fast = LocomotionPlayRateMatching.Update(1f, 20f, 2f, 0.1f);
        Assert.That(fast, Is.GreaterThan(1f).And.LessThan(1.5f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveReference_UsesWeightsAndBasicSyncFrequency(bool directional)
    {
        AnimationAsset idle = Clip("Idle", 1f, 0f);
        AnimationAsset walk = Clip("Walk", 1f, 1f);
        AnimationAsset run = Clip("Run", 0.5f, 2f);
        ManualMixerState mixer;
        if (directional)
        {
            var state = new DirectionalMixerState();
            state.Add(idle.Clip, Vector2.zero);
            state.Add(walk.Clip, Vector2.up);
            state.Add(run.Clip, Vector2.up * 2f);
            mixer = state;
        }
        else
        {
            var state = new LinearMixerState { ExtrapolateSpeed = false };
            state.Add(idle.Clip, 0f);
            state.Add(walk.Clip, 1f);
            state.Add(run.Clip, 2f);
            mixer = state;
        }
        mixer.DontSynchronize(mixer.GetChild(0));
        var playback = new LocomotionMovePlayback(mixer, new[] { idle, walk, run },
            new[] { false, true, true }, new[] { true, false, false }, false,
            message => Assert.Fail(message), null, QuerySyntheticTrajectory);
        var policy = Policy(1f);
        try
        {
            mixer.GetChild(0).Weight = 0f;
            mixer.GetChild(1).Weight = 0.5f;
            mixer.GetChild(2).Weight = 0.5f;
            playback.Prepare(Context(policy, Result(policy)));
            // Both synchronized cycles run at 0.5/1 + 0.5/0.5 = 1.5 cycles/s.
            Assert.That(playback.ReferenceSpeed, Is.EqualTo(2.25f).Within(1e-5));
            Assert.That(mixer.IsSynchronized(mixer.GetChild(0)), Is.False);
            Assert.That(mixer.IsSynchronized(mixer.GetChild(1)), Is.True);
            Assert.That(mixer.IsSynchronized(mixer.GetChild(2)), Is.True);

            mixer.GetChild(1).Weight = 0f;
            mixer.GetChild(2).Weight = 1f;
            playback.Prepare(Context(policy, Result(policy)));
            Assert.That(playback.ReferenceSpeed, Is.EqualTo(4f).Within(1e-5));
            Assert.That(playback.PlayRate, Is.LessThan(1f));

            float frozenRate = playback.PlayRate;
            playback.Prepare(Context(policy, Result(policy), dt: 0f));
            Assert.That(playback.PlayRate, Is.EqualTo(frozenRate));
            Assert.That(playback.ReferenceSpeed, Is.EqualTo(4f).Within(1e-5));
        }
        finally { mixer.Destroy(); }
    }

    [Test]
    public void UnsynchronizedSample_UsesItsOwnCycleRate()
    {
        AnimationAsset walk = Clip("Walk", 1f, 1f);
        AnimationAsset run = Clip("Run", 0.5f, 2f);
        var mixer = new LinearMixerState();
        mixer.Add(walk.Clip, 1f);
        mixer.Add(run.Clip, 2f);
        mixer.DontSynchronize(mixer.GetChild(1));
        var playback = new LocomotionMovePlayback(mixer, new[] { walk, run },
            new[] { true, false }, new[] { false, false }, false,
            message => Assert.Fail(message), null, QuerySyntheticTrajectory);
        try
        {
            mixer.GetChild(0).Weight = 0.5f;
            mixer.GetChild(1).Weight = 0.5f;
            playback.Prepare(Context(Policy(1f), default));
            Assert.That(playback.ReferenceSpeed, Is.EqualTo(2.5f).Within(1e-5));
            Assert.That(mixer.IsSynchronized(mixer.GetChild(1)), Is.False);
        }
        finally { mixer.Destroy(); }
    }

    [Test]
    public void MissingWeightedTrajectory_KeepsTheMixerPlayingAtNeutralRate()
    {
        AnimationAsset good = Clip("Good", 1f, 1f);
        AnimationAsset missing = Clip("Missing", 1f, 2f);
        var mixer = new LinearMixerState();
        mixer.Add(good.Clip, 1f);
        mixer.Add(missing.Clip, 2f);
        var issues = new List<string>();
        bool Query(AnimationAsset asset, out RootMotionTrajectory trajectory, out string reason)
        {
            trajectory = asset == missing ? null : asset.RootMotionData;
            reason = "Missing trajectory.";
            return trajectory != null;
        }
        var playback = new LocomotionMovePlayback(mixer, new[] { good, missing },
            new[] { true, true }, new[] { false, false }, false, issues.Add, null, Query);
        try
        {
            mixer.GetChild(0).Weight = 0.5f;
            mixer.GetChild(1).Weight = 0.5f;
            var policy = Policy(1f);
            playback.Prepare(Context(policy, Result(policy)));
            playback.Prepare(Context(policy, Result(policy)));
            Assert.That(playback.PlayRate, Is.EqualTo(1f));
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(mixer.ChildCount, Is.EqualTo(2));
        }
        finally { mixer.Destroy(); }
    }

    [Test]
    public void NonLoopingMove_KeepsBasicPlaybackWithoutInventingACycleSpeed()
    {
        AnimationAsset move = Clip("One-shot Move", 1f, 2f, looping: false);
        var mixer = new LinearMixerState();
        mixer.Add(move.Clip, 1f);
        var issues = new List<string>();
        var playback = new LocomotionMovePlayback(mixer, new[] { move },
            new[] { false }, new[] { false }, false, issues.Add, null, QuerySyntheticTrajectory);
        try
        {
            mixer.GetChild(0).Weight = 1f;
            var policy = Policy(1f);
            playback.Prepare(Context(policy, Result(policy)));
            playback.Prepare(Context(policy, Result(policy)));
            Assert.That(playback.PlayRate, Is.EqualTo(1f));
            Assert.That(issues.Count, Is.EqualTo(1));
        }
        finally { mixer.Destroy(); }
    }

    [Test]
    public void QualifiedWorldSpeed_ChangesTheEvaluatedMoveClipRate()
    {
        AnimationAsset move = Clip("Move", 1f, 2f);
        var mixer = new LinearMixerState { ExtrapolateSpeed = false };
        var child = mixer.Add(move.Clip, 1f);
        var playback = new LocomotionMovePlayback(mixer, new[] { move },
            new[] { true }, new[] { false }, false, message => Assert.Fail(message),
            null, QuerySyntheticTrajectory);
        var root = new GameObject("Matching Actor");
        _objects.Add(root);
        var animator = root.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var animancer = root.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        var animation = root.AddComponent<ActorAnimation>();
        var owner = animation.BeginLocomotionSession();
        var policy = Policy(1f);
        try
        {
            var request = new LocomotionAnimationRequest(mixer, isMove: true, parameter: Vector2.right,
                movePlayback: playback, playbackContext: Context(policy, Result(policy)));
            Assert.That(animation.SubmitLocomotion(owner, request), Is.True);
            animation.Evaluate(0.1f);
            double before = child.TimeD;
            Assert.That(animation.SubmitLocomotion(owner, request), Is.True);
            Assert.That(playback.ReferenceSpeed, Is.EqualTo(2f).Within(1e-5));
            Assert.That(playback.PlayRate, Is.GreaterThan(0.5f).And.LessThan(1f));
            animation.Evaluate(0.1f);
            Assert.That(child.TimeD - before, Is.EqualTo(0.1d * playback.PlayRate).Within(0.005d));
        }
        finally { animation.EndLocomotionSession(owner); mixer.Destroy(); }
    }

    [Test]
    public void TrajectoryQualification_RejectsChangedDependencies()
    {
        AnimationAsset move = Clip("Unqualified Move", 1f, 2f);
        Assert.That(move.RootMotionData.MatchesSource(move.Clip, 60, RootMotionTrajectory.CurrentBakerVersion, "synthetic"), Is.True);
        Assert.That(move.RootMotionData.MatchesSource(move.Clip, 60, RootMotionTrajectory.CurrentBakerVersion, "changed-rig"), Is.False);
        Assert.That(move.RootMotionData.MatchesSource(move.Clip, 30, RootMotionTrajectory.CurrentBakerVersion, "synthetic"), Is.False);
        Assert.That(move.TryGetLocomotionTrajectory(out _, out string reason), Is.False,
            "An in-memory clip has no confirmed bake rig.");
        Assert.That(reason, Is.Not.Empty);
    }

    private AnimationAsset Clip(string name, float duration, float distance, bool looping = true)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve("Probe", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, duration, 0f));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = looping;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var asset = ScriptableObject.CreateInstance<AnimationAsset>();
        asset.name = name;
        asset.EditorSetClip(clip);
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(clip, 60, duration, RootMotionTrajectory.CurrentBakerVersion, "synthetic",
            new[] { 0f, duration }, new[] { Vector3.zero, Vector3.forward * distance },
            new[] { Quaternion.identity, Quaternion.identity });
        asset.EditorSetRootMotionData(trajectory);
        _objects.Add(clip);
        _objects.Add(asset);
        return asset;
    }

    private static bool QuerySyntheticTrajectory(AnimationAsset asset, out RootMotionTrajectory trajectory, out string reason)
    {
        trajectory = asset.RootMotionData;
        reason = "Invalid synthetic trajectory.";
        return trajectory != null && trajectory.ValidateData().IsValid
            && trajectory.MatchesSource(asset.Clip, 60, RootMotionTrajectory.CurrentBakerVersion, "synthetic");
    }

    private static MotionStateSnapshot Policy(float scale) =>
        new(1f, 1f, 1f, scale, false, false, false, false);

    private static MotorMotionResult Result(MotionStateSnapshot policy, int reason = -1) => new(
        Vector3.forward, reason == 0 ? HorizontalMotionSource.TrajectoryRootMotion : reason == 1
            ? HorizontalMotionSource.HorizontalVelocityOwner : reason == 2
                ? HorizontalMotionSource.Unknown : HorizontalMotionSource.Locomotion,
        policy, true, true, reason == 3, reason == 4, reason == 5, reason == 6);

    private static LocomotionRuntimeAnimationContext Context(MotionStateSnapshot policy, MotorMotionResult previous,
        int action = 0, float dt = 0.1f) => new(LocomotionIntent.Idle, false,
            Vector3.forward, Vector3.forward,
            new LocomotionMotionContext(dt, true, Vector3.up, Quaternion.identity, previous, policy),
            0f, action, dt);
}
#endif
