#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LocomotionMatchingContractTests
{
    private readonly List<Object> _objects = new List<Object>();
    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void CycleMapping_RoundTripsAcrossTheClipSeamAndRejectsInvalidAnchors()
    {
        var cycle = new LocomotionCycleMapping(new FootPhaseAnchor(0.2f, 0f),
            new FootPhaseAnchor(0.6f, 0.5f), new FootPhaseAnchor(1.2f, 1f));
        Assert.That(cycle.Validate(1f, out _), Is.True);
        foreach (double phase in new[] { -0.2, 0.0, 0.5, 0.9, 1.0, 2.3 })
            Assert.That(cycle.TimeToPhase(cycle.PhaseToTime(phase)), Is.EqualTo(phase).Within(1e-5));
        var invalid = new LocomotionCycleMapping(new FootPhaseAnchor(0f, 0f),
            new FootPhaseAnchor(0.5f, 0.5f), new FootPhaseAnchor(0.5f, 1f));
        Assert.That(invalid.Validate(1f, out _), Is.False);
    }

    [Test]
    public void LoopingTrajectory_ComposesAcrossTheSeamWithoutChangingClampedQueries()
    {
        AnimationAsset asset = Clip("Straight loop", 1f, 2f);
        Assert.That(asset.RootMotionData.TryExtractLooping(0.9, 1.2, out var delta), Is.True);
        Assert.That(delta.Position.z, Is.EqualTo(0.6f).Within(1e-5));
        Assert.That(asset.RootMotionData.TryExtract(0.9f, 1.2f, out var clamped), Is.True);
        Assert.That(clamped.Position.z, Is.EqualTo(0.2f).Within(1e-5));
        Assert.That(asset.RootMotionData.TryExtractLooping(1.2, 0.9, out _), Is.False);
        var turning = new RootMotionTrajectory();
        turning.EditorSetData(asset.Clip, 60, 1f, 1, "synthetic", new[] { 0f, 1f },
            new[] { Vector3.zero, Vector3.forward }, new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) });
        Assert.That(turning.TryExtractLooping(0d, 2d, out var twoCycles), Is.True);
        Assert.That(Vector3.Distance(twoCycles.Position, Vector3.forward + Vector3.right), Is.LessThan(1e-5));
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
        filter.Reset(); // Asset/session change.
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

    [Test]
    public void Metadata_ReanalysisPreservesCorrectionsAndRequiresConfirmationForChangedSources()
    {
        AnimationAsset asset = Clip("Manual cycle", 1f, 1f);
        var data = asset.LocomotionData;
        var manual = Cycle(1f, 0.2f);
        data.EditorSetCycle(manual);
        data.EditorApplyAnalysis(asset.Clip, "changed-source", new LocomotionAnalyzedMotion { Cycle = Cycle(1f, 0f) });
        Assert.That(data.TryGetCycle(asset.Clip, out _, out _), Is.False);
        data.EditorConfirmSource(asset.Clip, "changed-source");
        Assert.That(data.TryGetCycle(asset.Clip, out var preserved, out _), Is.True);
        Assert.That(preserved, Is.SameAs(manual));
        data.EditorSetRole(LocomotionAnimationRole.Transition);
        data.EditorApplyAnalysis(asset.Clip, "transition", new LocomotionAnalyzedMotion { HasExitPhaseCandidate = true, ExitPhaseCandidate = 0.75f });
        Assert.That(data.TryGetExitPhase(asset.Clip, out _), Is.False);
        data.EditorSetExitPhase(0.75f);
        Assert.That(data.TryGetExitPhase(asset.Clip, out float phase), Is.True);
        Assert.That(phase, Is.EqualTo(0.75f));
    }

    [Test]
    public void ContactAnalysis_ProducesAlternatingCandidatesAndReportsAmbiguousCycles()
    {
        var times = new List<float>();
        var left = new List<Vector3>();
        var right = new List<Vector3>();
        for (int i = 0; i <= 100; i++)
        {
            float t = i / 100f;
            times.Add(t);
            left.Add(Vector3.up * (t >= 0.1f && t <= 0.3f ? 0f : 0.3f));
            right.Add(Vector3.up * (t >= 0.6f && t <= 0.8f ? 0f : 0.3f));
        }
        Assert.That(LocomotionFootAnalyzer.TryAnalyze(times, left, right, 1f, new AnimationRigBakeSettings(),
            true, LocomotionAnimationRole.MoveCycle, out var result, out _), Is.True);
        Assert.That(result.LeftContacts.Length, Is.EqualTo(1));
        Assert.That(result.RightContacts.Length, Is.EqualTo(1));
        Assert.That(result.Cycle.Validate(1f, out _), Is.True);
        Assert.That(LocomotionFootAnalyzer.TryAnalyze(times, left, left, 1f, new AnimationRigBakeSettings(),
            true, LocomotionAnimationRole.MoveCycle, out _, out _), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SemanticMixers_BlendReferenceSpeedAndRejoinTheSameFootPhase(bool directional)
    {
        AnimationAsset idle = Clip("Idle", 1f, 0f);
        AnimationAsset walk = Clip("Walk", 1f, 1f);
        AnimationAsset run = Clip("Run", 0.5f, 1f);
        ManualMixerState mixer;
        if (directional)
        {
            var state = new LocomotionDirectionalMixerState();
            state.Add(idle.Clip, Vector2.zero); state.Add(walk.Clip, Vector2.up); state.Add(run.Clip, Vector2.up * 2f);
            mixer = state;
        }
        else
        {
            var state = new LocomotionLinearMixerState { ExtrapolateSpeed = false };
            state.Add(idle.Clip, 0f); state.Add(walk.Clip, 1f); state.Add(run.Clip, 2f);
            mixer = state;
        }
        mixer.DontSynchronize(mixer.GetChild(0));
        var playback = new LocomotionMovePlayback(mixer, new[] { idle, walk, run },
            new[] { false, true, true }, new[] { true, false, false }, false, message => Assert.Fail(message));
        if (mixer is LocomotionLinearMixerState linear) linear.Playback = playback;
        if (mixer is LocomotionDirectionalMixerState two) two.Playback = playback;
        ActorAnimation animation = AnimationHost();
        var owner = animation.BeginLocomotionSession();
        var policy = Policy(1f);
        try
        {
            for (int i = 0; i < 16; i++)
            {
                var context = Context(policy, Result(policy), dt: 0.1f);
                var request = new LocomotionAnimationRequest(mixer, isMove: true, parameter: Vector2.up * 1.5f,
                    idleClip: idle.Clip, movePlayback: playback, playbackContext: context);
                // Linear uses x; Directional uses (right, forward).
                if (!directional) request = new LocomotionAnimationRequest(mixer, isMove: true, parameter: new Vector2(1.5f, 0f),
                    idleClip: idle.Clip, movePlayback: playback, playbackContext: context);
                animation.SubmitLocomotion(owner, request);
                double before = playback.Phase;
                animation.Evaluate(0f);
                Assert.That(playback.Phase, Is.EqualTo(before));
                animation.Evaluate(0.1f);
                Assert.That(walk.LocomotionData.TryGetCycle(walk.Clip, out var walkCycle, out _), Is.True);
                Assert.That(run.LocomotionData.TryGetCycle(run.Clip, out var runCycle, out _), Is.True);
                Assert.That(walkCycle.TimeToPhase(mixer.GetChild(1).TimeD), Is.EqualTo(playback.Phase).Within(0.001));
                Assert.That(runCycle.TimeToPhase(mixer.GetChild(2).TimeD), Is.EqualTo(playback.Phase).Within(0.001));
                Assert.That(playback.ReferenceSpeed, Is.EqualTo(1.5f).Within(0.001f));
            }
            Assert.That(mixer.IsSynchronized(mixer.GetChild(0)), Is.False);
            Assert.That(mixer.IsSynchronized(mixer.GetChild(1)), Is.True);
            var contextNow = Context(policy, Result(policy));
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(mixer, isMove: true,
                parameter: directional ? Vector2.up : Vector2.right, idleClip: idle.Clip, movePlayback: playback, playbackContext: contextNow));
            animation.Evaluate(0.1f);
            double beforeReentry = playback.Phase;
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(mixer, isMove: true,
                parameter: directional ? Vector2.up * 2f : Vector2.right * 2f, idleClip: idle.Clip, movePlayback: playback, playbackContext: contextNow));
            animation.Evaluate(0.1f);
            run.LocomotionData.TryGetCycle(run.Clip, out var activeCycle, out _);
            Assert.That(activeCycle.TimeToPhase(mixer.GetChild(2).TimeD), Is.EqualTo(playback.Phase).Within(0.001));
            Assert.That(playback.Phase, Is.GreaterThan(beforeReentry));
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(mixer, isMove: true,
                parameter: directional ? Vector2.up * 2f : Vector2.right * 2f, idleClip: idle.Clip, movePlayback: playback, playbackContext: contextNow, entryPhase: 0.75f));
            animation.Evaluate(0.1f);
            Assert.That(playback.Phase, Is.GreaterThan(0.75d));
            double frozenPhase = playback.Phase;
            float frozenRate = playback.PlayRate;
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(mixer, isMove: true, parameter: Vector2.zero,
                idleClip: idle.Clip, movePlayback: playback, playbackContext: Context(policy, Result(policy), dt: 0f)));
            animation.Evaluate(0f);
            Assert.That(playback.Phase, Is.EqualTo(frozenPhase));
            Assert.That(playback.PlayRate, Is.EqualTo(frozenRate));
            Assert.That(mixer.GetChild(2).Weight, Is.EqualTo(1f).Within(0.001f));
        }
        finally { animation.EndLocomotionSession(owner); mixer.Destroy(); }
    }

    [Test]
    public void IncompletePhaseGroup_PreservesEverySampleAndBasicSyncMembership()
    {
        AnimationAsset a = Clip("Valid", 1f, 1f), b = Clip("Missing", 1f, 1f);
        b.EditorSetLocomotionData(new LocomotionAnimationData());
        var mixer = new LocomotionLinearMixerState { ExtrapolateSpeed = false };
        mixer.Add(a.Clip, 1f); mixer.Add(b.Clip, 2f);
        var issues = new List<string>();
        var playback = new LocomotionMovePlayback(mixer, new[] { a, b }, new[] { true, true }, new[] { false, false }, false, issues.Add);
        mixer.Playback = playback;
        ActorAnimation animation = AnimationHost();
        var owner = animation.BeginLocomotionSession();
        try
        {
            var policy = Policy(1f);
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(mixer, isMove: true,
                parameter: new Vector2(1.5f, 0f), idleClip: a.Clip, movePlayback: playback, playbackContext: Context(policy, Result(policy))));
            Assert.That(playback.SemanticSyncActive, Is.False);
            Assert.That(mixer.ChildCount, Is.EqualTo(2));
            Assert.That(mixer.IsSynchronized(mixer.GetChild(0)), Is.True);
            Assert.That(mixer.IsSynchronized(mixer.GetChild(1)), Is.True);
            Assert.That(issues.Count, Is.GreaterThan(0));
            Assert.That(playback.PlayRate, Is.EqualTo(1f));
        }
        finally { animation.EndLocomotionSession(owner); mixer.Destroy(); }
    }

    [Test]
    public void SetCompletion_SeedsMoveOnlyForAnUninterruptedConfirmedTransition()
    {
        AnimationAsset idle = Clip("Idle", 1f, 0f), move = Clip("Move", 1f, 1f);
        AnimationAsset start = Clip("Start", 0.2f, 0.2f);
        start.LocomotionData.EditorSetRole(LocomotionAnimationRole.Transition);
        start.LocomotionData.EditorConfirmSource(start.Clip, LocomotionDataSourceHash.Compute(start));
        start.LocomotionData.EditorSetExitPhase(0.75f);
        var asset = ScriptableObject.CreateInstance<LocomotionSetAsset>(); _objects.Add(asset);
        string idleRef = "{\"instanceID\":" + idle.GetInstanceID() + "}";
        string moveRef = "{\"instanceID\":" + move.GetInstanceID() + "}";
        string startRef = "{\"instanceID\":" + start.GetInstanceID() + "}";
        JsonUtility.FromJsonOverwrite("{\"move\":{\"oneDimensional\":{\"samples\":[{\"animation\":" + idleRef
            + ",\"threshold\":0,\"sync\":false},{\"animation\":" + moveRef + ",\"threshold\":1,\"sync\":true}]}},"
            + "\"start\":[{\"animation\":" + startRef + ",\"targetLocalDirection\":{\"x\":0,\"y\":1}}]}", asset);
        var runtime = (LocomotionSetRuntime)asset.CreateRuntime(); runtime.Enter(null, null);
        ActorAnimation animation = AnimationHost(); var owner = animation.BeginLocomotionSession();
        try
        {
            var motor = new LocomotionMotionContext(0.1f, true, Vector3.up, Quaternion.identity, default, Policy(1f));
            var intent = new LocomotionIntent { WorldMoveDirection = Vector3.forward, MoveStrength = 1f };
            var context = new LocomotionRuntimeAnimationContext(intent, true, Vector3.zero, Vector3.forward, motor, 0f, 0, 0.1f);
            animation.SubmitLocomotion(owner, runtime.UpdateAnimation(context)); animation.Evaluate(0.3f);
            var completed = runtime.UpdateAnimation(context);
            Assert.That(completed.IsMove, Is.True);
            Assert.That(completed.EntryPhase, Is.EqualTo(0.75f));
            animation.SubmitLocomotion(owner, completed); animation.Evaluate(0.1f);
            Assert.That(completed.MovePlayback.Phase, Is.GreaterThan(0.75d));
            runtime.ResetAnimation();
            animation.SubmitLocomotion(owner, runtime.UpdateAnimation(context)); animation.Evaluate(0.3f);
            var covered = new LocomotionRuntimeAnimationContext(intent, true, Vector3.zero, Vector3.forward, motor, 0f, 42, 0.1f);
            Assert.That(runtime.UpdateAnimation(covered).EntryPhase, Is.Null);
        }
        finally { animation.EndLocomotionSession(owner); runtime.Exit(null, null); runtime.Dispose(); }
    }

    private ActorAnimation AnimationHost()
    {
        var root = new GameObject("Synthetic Matching Actor");
        _objects.Add(root);
        var probe = new GameObject("Probe"); probe.transform.SetParent(root.transform, false);
        var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var animancer = root.AddComponent<AnimancerComponent>(); animancer.Animator = animator;
        return root.AddComponent<ActorAnimation>();
    }

    private AnimationAsset Clip(string name, float duration, float distance)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve("Probe", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, duration, 0f));
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var asset = ScriptableObject.CreateInstance<AnimationAsset>(); asset.name = name; asset.EditorSetClip(clip);
        asset.LocomotionData.EditorSetRole(LocomotionAnimationRole.MoveCycle);
        asset.LocomotionData.EditorConfirmSource(clip, LocomotionDataSourceHash.Compute(asset));
        asset.LocomotionData.EditorSetCycle(Cycle(duration, duration * 0.2f));
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(clip, 60, duration, 1, "synthetic", new[] { 0f, duration },
            new[] { Vector3.zero, Vector3.forward * distance }, new[] { Quaternion.identity, Quaternion.identity });
        asset.EditorSetRootMotionData(trajectory);
        _objects.Add(clip); _objects.Add(asset);
        return asset;
    }

    private static LocomotionCycleMapping Cycle(float duration, float start) => new LocomotionCycleMapping(
        new FootPhaseAnchor(start, 0f), new FootPhaseAnchor(start + duration * 0.5f, 0.5f), new FootPhaseAnchor(start + duration, 1f));
    private static MotionStateSnapshot Policy(float scale) => new MotionStateSnapshot(1f, 1f, 1f, scale, false, false, false, false);
    private static MotorMotionResult Result(MotionStateSnapshot policy, int reason = -1) => new MotorMotionResult(
        Vector3.forward, reason == 0 ? HorizontalMotionSource.TrajectoryRootMotion : reason == 1
            ? HorizontalMotionSource.HorizontalVelocityOwner : reason == 2 ? HorizontalMotionSource.Unknown : HorizontalMotionSource.Locomotion,
        policy, true, true, reason == 3, reason == 4, reason == 5, reason == 6);
    private static LocomotionRuntimeAnimationContext Context(MotionStateSnapshot policy, MotorMotionResult previous,
        int action = 0, float dt = 0.1f) => new LocomotionRuntimeAnimationContext(LocomotionIntent.Idle,
            false, Vector3.forward, Vector3.forward, new LocomotionMotionContext(dt, true, Vector3.up,
                Quaternion.identity, previous, policy), 0f, action, dt);
}
#endif
