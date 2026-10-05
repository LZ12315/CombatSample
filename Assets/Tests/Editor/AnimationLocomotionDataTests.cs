#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AnimationLocomotionDataTests
{
    private readonly List<Object> _objects = new();
    [TearDown] public void Cleanup()
    { foreach (var obj in _objects) Object.DestroyImmediate(obj); _objects.Clear(); }

    [Test]
    public void StopCurve_UsesPlanarPathLengthAndExcludesStationaryTail()
    {
        var trajectory = Trajectory();
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.EqualTo(2f));
        Assert.That(AnimationStopDistanceCurve.TryCreate(trajectory, 2f, out var curve), Is.True);
        Assert.That(curve.MaximumDistance, Is.EqualTo(2f).Within(1e-5f));
        Assert.That(curve.TimeAtDistance(0.5f), Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(curve.TimeAtDistance(100f), Is.Zero);
        Assert.That(curve.TimeAtDistance(0f), Is.EqualTo(2f));
        Assert.That(curve.TimeAtDistance(-1f), Is.EqualTo(2f));
    }

    [Test]
    public void StopPointOverride_CutsTheCurveAtAnInterpolatedFrame()
    {
        Assert.That(AnimationStopDistanceCurve.TryCreate(Trajectory(), 1.5f, out var curve), Is.True);
        Assert.That(curve.StopTime, Is.EqualTo(1.5f));
        Assert.That(curve.MaximumDistance, Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(curve.TimeAtDistance(0f), Is.EqualTo(1.5f));
    }

    [Test]
    public void InPlaceTrajectory_HasNoUsableDistanceCurve()
    {
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(null, 60, 1f, 1, "test", new[] { 0f, 1f },
            new[] { Vector3.zero, Vector3.zero }, new[] { Quaternion.identity, Quaternion.identity });
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.Zero);
        Assert.That(AnimationStopDistanceCurve.TryCreate(trajectory, 1f, out _), Is.False);
    }

    [TestCase(1f)]
    [TestCase(0.001f)]
    public void AutomaticStop_IgnoresFastSmallTailMotionAndVerticalMovement(float interval)
    {
        var trajectory = SampledTrajectory(new[] { 0f, interval, interval * 2f, interval * 3f, interval * 4f },
            new[] { Vector3.zero, new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1f, 1.01f),
                new Vector3(0.008f, 2f, 0.99f), new Vector3(0f, 3f, 1f) });
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.EqualTo(interval * 2f));
    }

    [Test]
    public void AutomaticStop_RequiresEveryLaterPositionToRemainInsideTheFinalRegion()
    {
        var trajectory = SampledTrajectory(new[] { 0f, 1f, 2f, 3f, 4f },
            new[] { Vector3.zero, new Vector3(0f, 0f, 1.005f), new Vector3(0f, 0f, 1.1f),
                new Vector3(0f, 0f, 1.01f), Vector3.forward });
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.EqualTo(3f));
    }

    [Test]
    public void AutomaticStop_UsesACircularRegionInTheHorizontalPlane()
    {
        var trajectory = SampledTrajectory(new[] { 0f, 1f, 2f, 3f },
            new[] { Vector3.zero, new Vector3(1.016f, 0f, 1.016f), new Vector3(1.012f, 0f, 1.012f),
                new Vector3(1f, 0f, 1f) });
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.EqualTo(2f));
    }

    [TestCase(1f, 2f)]
    [TestCase(0.25f, 0f)]
    public void AutomaticStop_UsesTheExtentOfSlowDriftRatherThanASpeedThreshold(float scale, float expected)
    {
        var trajectory = SampledTrajectory(new[] { 0f, 1f, 2f, 3f, 4f },
            new[] { Vector3.zero, Vector3.forward * (0.01f * scale), Vector3.forward * (0.02f * scale),
                Vector3.forward * (0.03f * scale), Vector3.forward * (0.04f * scale) });
        Assert.That(AnimationStopDistanceCurve.FindAutomaticStopTime(trajectory), Is.EqualTo(expected));
    }

    [Test]
    public void StopClock_NeverRewindsAndHandsTheTailBackToTime()
    {
        AnimationStopDistanceCurve.TryCreate(Trajectory(), 2f, out var curve);
        var playback = new LocomotionStopDistancePlayback();
        playback.Begin(curve);
        Assert.That(playback.Sample(100f), Is.EqualTo(0f));
        Assert.That(playback.Sample(1f), Is.EqualTo(1f));
        Assert.That(playback.Sample(1.5f), Is.EqualTo(1f));
        Assert.That(playback.Sample(0f), Is.EqualTo(2f));
        Assert.That(playback.Sample(0f), Is.Null);
        playback.Begin(curve);
        Assert.That(playback.Sample(2f), Is.EqualTo(0f), "A new Stop has its own clock.");
        Assert.That(playback.Sample(null), Is.Null);
        Assert.That(playback.Sample(1f), Is.Null, "Unavailable prediction switches this Stop to time playback.");
        playback.Begin(null);
        Assert.That(playback.Sample(1f), Is.Null);
    }

    [Test]
    public void FootPhase_InterpolatesContactsAndWrapsTheLoopSeam()
    {
        var track = new AnimationFootPhaseTrack(new[] {
            new AnimationFootMarker(0f, AnimationFoot.Left),
            new AnimationFootMarker(0.5f, AnimationFoot.Right),
            new AnimationFootMarker(1f, AnimationFoot.Left) }, 1f, true);
        AssertPhase(track, 0.25f, 0.25f);
        AssertPhase(track, 0.75f, 0.75f);
        AssertPhase(track, 1.1f, 0.1f);
        AssertPhase(track, -0.1f, 0.9f);
        Assert.That(AnimationFootPhaseTrack.Difference(0.99f, 0.01f), Is.EqualTo(0.02f).Within(1e-5f));
    }

    [Test]
    public void FootPhase_MissingAndSingleContactHaveExplicitResults()
    {
        Assert.That(new AnimationFootPhaseTrack(null, 1f, false).TrySample(0f, out _), Is.False);
        var track = new AnimationFootPhaseTrack(new[] { new AnimationFootMarker(0.5f, AnimationFoot.Right) }, 1f, false);
        AssertPhase(track, 0f, 0.5f);
        AssertPhase(track, 1f, 0.5f);
    }

    [Test]
    public void FootBake_ExtractsOneCandidatePerObservableHeightValley()
    {
        var times = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
        var markers = AnimationFootContactBaker.Generate(times,
            new[] { 0.3f, 0f, 0.3f, 0.4f, 0.3f }, new[] { 0.3f, 0.4f, 0.3f, 0f, 0.3f });
        Assert.That(markers.Length, Is.EqualTo(2));
        Assert.That(markers[0].Foot, Is.EqualTo(AnimationFoot.Left));
        Assert.That(markers[0].Time, Is.EqualTo(0.25f));
        Assert.That(markers[1].Foot, Is.EqualTo(AnimationFoot.Right));
        Assert.That(markers[1].Time, Is.EqualTo(0.75f));
        Assert.That(AnimationFootContactBaker.Generate(times, null, new[] { 0f, 0f, 0f, 0f, 0f }), Is.Empty);
    }

    [Test]
    public void FootBake_DetectsStepsAtDifferentHeightsWithoutMarkingTheInitialStance()
    {
        var times = new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };
        var markers = AnimationFootContactBaker.Generate(times,
            new[] { 0.1f, 0.3f, 0.6f, 0.18f, 0.2f, 0.6f, 0.15f, 0.2f, 0.6f, 0.4f }, null);
        Assert.That(markers.Length, Is.EqualTo(2));
        Assert.That(markers[0].Time, Is.EqualTo(3f), "A higher landing must survive a lower initial stance.");
        Assert.That(markers[1].Time, Is.EqualTo(6f));
    }

    [Test]
    public void FootBake_SeparatesNewStepsFromSmallMotionWhilePlanted()
    {
        var times = new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f };
        var markers = AnimationFootContactBaker.Generate(times,
            new[] { 0.5f, 0.3f, 0.1f, 0.11f, 0.105f, 0.12f, 0.5f, 0.6f }, null);
        Assert.That(markers.Length, Is.EqualTo(1));
        Assert.That(markers[0].Time, Is.EqualTo(2f));
    }

    [Test]
    public void FootBake_RecognizesAPlantedTailButNotAnUnfinishedDescent()
    {
        var times = new[] { 0f, 1f, 2f, 3f, 3.0000002f };
        var planted = AnimationFootContactBaker.Generate(times, new[] { 0.5f, 0.3f, 0.1f, 0.1f, 0.1f }, null);
        Assert.That(planted.Length, Is.EqualTo(1));
        Assert.That(planted[0].Time, Is.EqualTo(2f));
        Assert.That(AnimationFootContactBaker.Generate(times, new[] { 0.5f, 0.4f, 0.3f, 0.2f, 0.2f }, null), Is.Empty);
    }

    [Test]
    public void FootBake_LoopSeamHasOneContactInsteadOfTwoBoundaryContacts()
    {
        var markers = AnimationFootContactBaker.Generate(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
            new[] { 0f, 0.4f, 1f, 0.4f, 0f }, null, looping: true);
        Assert.That(markers.Length, Is.EqualTo(1));
        Assert.That(markers[0].Time, Is.Zero);
    }

    [Test]
    public void Rebake_PreservesOverridesWhileResetAndClipReplacementUseAutomaticData()
    {
        var asset = Asset();
        asset.EditorSetLocomotionData(new AnimationLocomotionData(asset.Clip, "old", "|", 0.7f,
            new[] { new AnimationFootMarker(0.2f, AnimationFoot.Left) }));
        asset.EditorOverrideStopTime(true, 0.6f);
        asset.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0.3f, AnimationFoot.Right) });
        asset.EditorSetLocomotionData(new AnimationLocomotionData(asset.Clip, "new", "|", 0.8f,
            new[] { new AnimationFootMarker(0.4f, AnimationFoot.Left) }));
        Assert.That(asset.StopTime, Is.EqualTo(0.6f));
        Assert.That(asset.FootMarkers[0].Time, Is.EqualTo(0.3f));
        asset.EditorOverrideStopTime(false, 0.6f);
        asset.EditorOverrideFootMarkers(false, null);
        Assert.That(asset.StopTime, Is.EqualTo(0.8f));
        Assert.That(asset.FootMarkers[0].Time, Is.EqualTo(0.4f));
        asset.EditorOverrideStopTime(true, 0.6f);
        asset.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0.3f, AnimationFoot.Right) });
        asset.EditorSetClip(Asset().Clip);
        Assert.That(asset.HasStopTimeOverride, Is.False);
        Assert.That(asset.HasFootMarkerOverrides, Is.False);
        Assert.That(asset.LocomotionData, Is.Null);
    }

    [Test]
    public void StopPrediction_UsesTheBrakingModelAndPolicyScale()
    {
        Assert.That(LocomotionRunner.TryPredictStoppingDistance(5f, 32f, 1f, out float distance), Is.True);
        Assert.That(distance, Is.EqualTo(25f / 64f));
        Assert.That(LocomotionRunner.TryPredictStoppingDistance(5f, 32f, 0.5f, out distance), Is.True);
        Assert.That(distance, Is.EqualTo(25f / 128f));
        Assert.That(LocomotionRunner.TryPredictStoppingDistance(0f, 32f, 1f, out distance), Is.True);
        Assert.That(distance, Is.Zero);
        Assert.That(LocomotionRunner.TryPredictStoppingDistance(5f, 0f, 1f, out _), Is.False);
    }

    [Test]
    public void ExplicitPoseTime_FreezesOnlyTheDistanceSubmission()
    {
        var root = new GameObject("Distance Clock"); _objects.Add(root);
        var animator = root.AddComponent<Animator>();
        var animancer = root.AddComponent<AnimancerComponent>(); animancer.Animator = animator;
        var animation = root.AddComponent<ActorAnimation>();
        var owner = animation.BeginLocomotionSession();
        var state = new ClipState(Asset().Clip);
        try
        {
            Assert.That(animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(state, restart: true, sampleTime: 0.4f)), Is.True);
            animation.Evaluate(0.1f);
            Assert.That(state.TimeD, Is.EqualTo(0.4d).Within(1e-5));
            Assert.That(state.Speed, Is.Zero);
            animation.SubmitLocomotion(owner, new LocomotionAnimationRequest(state));
            animation.Evaluate(0.1f);
            Assert.That(state.TimeD, Is.EqualTo(0.5d).Within(0.005));
            Assert.That(state.Speed, Is.EqualTo(1f));
        }
        finally { animation.EndLocomotionSession(owner); state.Destroy(); }
    }

    private AnimationAsset Asset()
    {
        var clip = new AnimationClip();
        clip.SetCurve("Probe", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, 0f));
        var asset = ScriptableObject.CreateInstance<AnimationAsset>(); asset.EditorSetClip(clip);
        _objects.Add(asset); _objects.Add(clip); return asset;
    }
    private static void AssertPhase(AnimationFootPhaseTrack track, float time, float expected)
    { Assert.That(track.TrySample(time, out float phase), Is.True); Assert.That(phase, Is.EqualTo(expected).Within(1e-5f)); }
    private static RootMotionTrajectory Trajectory()
    {
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(null, 60, 3f, 1, "test", new[] { 0f, 1f, 2f, 3f },
            new[] { Vector3.zero, Vector3.forward, Vector3.forward + Vector3.right, Vector3.forward + Vector3.right },
            new[] { Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity });
        return trajectory;
    }
    private static RootMotionTrajectory SampledTrajectory(float[] times, Vector3[] positions)
    {
        var rotations = new Quaternion[times.Length];
        for (int i = 0; i < rotations.Length; i++) rotations[i] = Quaternion.identity;
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(null, 60, times[times.Length - 1], 1, "test", times, positions, rotations);
        return trajectory;
    }
}
#endif
