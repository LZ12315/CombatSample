#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionVfxContractTests
{
    private GameObject _prefab;
    [SetUp] public void SetUp() => _prefab = new GameObject("Temporary effect template");
    [TearDown] public void TearDown() => Object.DestroyImmediate(_prefab);

    [Test]
    public void Request_SnapshotsPrefabPlacementAndFollowMode()
    {
        EffectItemConfig config = Config();
        config.anchor = ActionEffectAnchor.TransformPath;
        config.transformPath = "Model/Weapon/Tip";
        config.localPosition = Vector3.up;
        config.localEulerAngles = Vector3.right * 45f;
        config.scale = 2f;
        config.followAnchor = true;
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out ActionEffectRequest request));
        config.prefab = null;
        config.transformPath = "Changed";
        config.localPosition = Vector3.zero;
        config.localEulerAngles = Vector3.zero;
        config.scale = 0f;
        config.followAnchor = false;
        Assert.AreSame(_prefab, request.Prefab);
        Assert.AreEqual("Model/Weapon/Tip", request.TransformPath);
        Assert.AreEqual(Vector3.up, request.LocalPosition);
        Assert.AreEqual(Vector3.right * 45f, request.LocalEulerAngles);
        Assert.AreEqual(2f, request.Scale);
        Assert.IsTrue(request.FollowAnchor);
    }

    [Test]
    public void WorldPose_UsesCurrentAnchorTransformAndRelativeDirection()
    {
        EffectItemConfig config = Config();
        config.localPosition = Vector3.forward;
        config.localEulerAngles = new Vector3(0f, 90f, 0f);
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out ActionEffectRequest request));
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        Matrix4x4 afterWorld = Matrix4x4.TRS(new Vector3(10f, 2f, 5f), rotation, Vector3.one * 2f);
        Pose pose = request.ResolveWorldPose(afterWorld, rotation);
        Assert.That(Vector3.Distance(new Vector3(12f, 2f, 5f), pose.position), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(Vector3.back, pose.rotation * Vector3.forward), Is.LessThan(0.0001f));
    }

    [Test]
    public void InvalidOrMissingConfiguration_DoesNotCreateAScope()
    {
        var buffer = new ActionEffectBuffer();
        Assert.IsNull(buffer.BeginEffect(null));
        Assert.IsNull(buffer.BeginEffect(new EffectItemConfig()));
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            EffectItemConfig config = Config();
            config.scale = invalid;
            Assert.IsNull(buffer.BeginEffect(config));
        }
        EffectItemConfig badPosition = Config();
        badPosition.localPosition = new Vector3(float.NaN, 0f, 0f);
        Assert.IsNull(buffer.BeginEffect(badPosition));
        EffectItemConfig badRotation = Config();
        badRotation.localEulerAngles = new Vector3(0f, float.PositiveInfinity, 0f);
        Assert.IsNull(buffer.BeginEffect(badRotation));
        EffectItemConfig badPath = Config();
        badPath.anchor = ActionEffectAnchor.TransformPath;
        badPath.transformPath = " ";
        Assert.IsNull(buffer.BeginEffect(badPath));
        EffectItemConfig badBone = Config();
        badBone.anchor = ActionEffectAnchor.HumanoidBone;
        badBone.bone = HumanBodyBones.LastBone;
        Assert.IsNull(buffer.BeginEffect(badBone));
    }

    [Test]
    public void MixedRanges_StartInOrderAndReleaseOnlyTheirOwnedPresentation()
    {
        var buffer = new ActionEffectBuffer();
        var events = new List<string>();
        var vfx = new Probe();
        var shake = new Probe();
        var vfxScope = buffer.BeginEffect(Config());
        var shakeScope = buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        buffer.UpdateWorld(Vector3.zero, request =>
        {
            Assert.AreSame(_prefab, request.Prefab);
            events.Add("vfx");
            return vfx;
        }, (_, __, ___) => { events.Add("shake"); return shake; });
        CollectionAssert.AreEqual(new[] { "vfx", "shake" }, events);
        vfxScope.Dispose();
        Assert.AreEqual(1, vfx.Disposals);
        Assert.AreEqual(0, shake.Disposals);
        shakeScope.Dispose();
        Assert.AreEqual(1, shake.Disposals);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void MutedVfxItemOrLane_DoesNotCreateAScope(bool muteItem, bool muteLane)
    {
        var buffer = new ActionEffectBuffer();
        var timeline = new ActionTimelineData();
        GameplayLane lane = AddLane(timeline);
        AddEffect(lane, 0, 2).EditorSetMuted(muteItem);
        lane.EditorSetMuted(muteLane);
        Scheduler(timeline, buffer).Begin();
        buffer.UpdateWorld(Vector3.zero, _ => { Assert.Fail("Muted VFX played."); return null; }, null);
    }

    [Test]
    public void VfxRange_StartsOnceAtArrivalAndDestroysAtExitRatherThanIndependentLifetime()
    {
        var buffer = new ActionEffectBuffer();
        var timeline = new ActionTimelineData();
        AddEffect(AddLane(timeline), 1, 2);
        var scheduler = Scheduler(timeline, buffer);
        var instance = new Probe();
        int starts = 0;
        scheduler.Begin();
        buffer.UpdateWorld(Vector3.zero, _ => { Assert.Fail("Future range started."); return null; }, null);
        scheduler.Finish();
        Assert.IsTrue(scheduler.Advance(1f));
        buffer.UpdateWorld(Vector3.zero, _ => { starts++; return instance; }, null);
        Assert.IsFalse(scheduler.Finish());
        Assert.AreEqual(0, instance.Disposals);
        Assert.IsTrue(scheduler.Advance(1f));
        buffer.UpdateWorld(Vector3.zero, _ => { starts++; return instance; }, null);
        Assert.AreEqual(1, starts);
        Assert.IsTrue(scheduler.Finish());
        Assert.AreEqual(1, instance.Disposals);
    }

    [Test]
    public void Anchors_ResolveRootAndExactWeaponPathWithoutMissingAnchorFallback()
    {
        GameObject root = new GameObject("Actor root");
        GameObject weapon = new GameObject("Weapon");
        weapon.transform.SetParent(root.transform);
        try
        {
            EffectItemConfig config = Config();
            Assert.IsTrue(ActionEffectRequest.TryCreate(config, out ActionEffectRequest request));
            Assert.IsTrue(ActionEffectAnchorResolver.TryResolve(request, root.transform, null, out Transform binding, out _));
            Assert.AreSame(root.transform, binding);
            config.anchor = ActionEffectAnchor.TransformPath;
            config.transformPath = "Weapon";
            Assert.IsTrue(ActionEffectRequest.TryCreate(config, out request));
            Assert.IsTrue(ActionEffectAnchorResolver.TryResolve(request, root.transform, null, out binding, out _));
            Assert.AreSame(weapon.transform, binding);
            config.transformPath = "Missing/Tip";
            Assert.IsTrue(ActionEffectRequest.TryCreate(config, out request));
            Assert.IsFalse(ActionEffectAnchorResolver.TryResolve(request, root.transform, null, out binding, out string error));
            Assert.IsNull(binding);
            Assert.IsNotEmpty(error);
            config.anchor = ActionEffectAnchor.HumanoidBone;
            Assert.IsTrue(ActionEffectRequest.TryCreate(config, out request));
            Assert.IsFalse(ActionEffectAnchorResolver.TryResolve(request, root.transform, null, out binding, out error));
            Assert.IsNull(binding);
            Assert.IsNotEmpty(error);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RangeTimingPlacementAndShakeDirection_RoundTripWithoutProjectAssets(bool follow)
    {
        var source = ScriptableObject.CreateInstance<ActionAsset>();
        var copy = ScriptableObject.CreateInstance<ActionAsset>();
        try
        {
            GameplayLane lane = AddLane(source.Timeline);
            EffectItem effect = AddEffect(lane, 7, 4);
            // Actual Prefab asset-reference persistence is a manual saved-asset check.
            effect.Config.prefab = null;
            effect.Config.anchor = ActionEffectAnchor.TransformPath;
            effect.Config.transformPath = "Model/Weapon";
            effect.Config.followAnchor = follow;
            effect.Config.localPosition = Vector3.up;
            effect.Config.localEulerAngles = Vector3.right * 45f;
            effect.Config.scale = 1.5f;
            effect.Config.orientation = ActionEffectOrientation.ScreenOriented;
            effect.Config.orientationUpdate = ActionEffectOrientationUpdate.OnEntry;
            effect.Config.anglePresetDegrees = 35f;
            effect.Config.simulationSpeed = 0.6f;
            effect.Config.occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;
            effect.EditorSetMuted(true);
            var shake = new ScreenShakeItem();
            shake.EditorSetTiming(12, 6);
            shake.Config.screenShake.intensity = 0.8f;
            shake.Config.direction = ActionScreenShakeDirection.Horizontal;
            shake.Config.waveform = AnimationCurve.Linear(0f, -1f, 1f, 1f);
            shake.Config.maxDistance = 30f;
            lane.EditorItems.Add(shake);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
            var restored = (EffectItem)copy.Timeline.GameplayLanes[0].Items[0];
            var restoredShake = (ScreenShakeItem)copy.Timeline.GameplayLanes[0].Items[1];
            Assert.AreEqual(7, restored.StartFrame);
            Assert.AreEqual(4, restored.DurationFrames);
            Assert.IsTrue(restored.Muted);
            Assert.AreEqual(ActionEffectAnchor.TransformPath, restored.Config.anchor);
            Assert.AreEqual("Model/Weapon", restored.Config.transformPath);
            Assert.AreEqual(follow, restored.Config.followAnchor);
            Assert.AreEqual(Vector3.up, restored.Config.localPosition);
            Assert.AreEqual(Vector3.right * 45f, restored.Config.localEulerAngles);
            Assert.AreEqual(1.5f, restored.Config.scale);
            Assert.AreEqual(ActionEffectOrientation.ScreenOriented, restored.Config.orientation);
            Assert.AreEqual(ActionEffectOrientationUpdate.OnEntry, restored.Config.orientationUpdate);
            Assert.AreEqual(35f, restored.Config.anglePresetDegrees);
            Assert.AreEqual(0.6f, restored.Config.simulationSpeed);
            Assert.AreEqual(HitVfxOcclusionMode.EnvironmentOnly, restored.Config.occlusionMode);
            Assert.AreEqual(12, restoredShake.StartFrame);
            Assert.AreEqual(6, restoredShake.DurationFrames);
            Assert.AreEqual(0.8f, restoredShake.Config.screenShake.intensity);
            Assert.AreEqual(ActionScreenShakeDirection.Horizontal, restoredShake.Config.direction);
            Assert.AreEqual(-1f, restoredShake.Config.waveform.Evaluate(0f));
            Assert.AreEqual(30f, restoredShake.Config.maxDistance);
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(copy); }
    }

    [TestCase(false, ActionEffectOrientationUpdate.MatchPositionBinding, false)]
    [TestCase(true, ActionEffectOrientationUpdate.MatchPositionBinding, true)]
    [TestCase(true, ActionEffectOrientationUpdate.OnEntry, false)]
    [TestCase(false, ActionEffectOrientationUpdate.Continuous, true)]
    public void PositionBindingAndOrientationUpdate_AreIndependent(bool follow, ActionEffectOrientationUpdate update, bool continuous)
    {
        var config = Config(); config.followAnchor = follow; config.orientationUpdate = update;
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out var request));
        Assert.AreEqual(follow, request.FollowAnchor);
        Assert.AreEqual(continuous, request.ContinuousOrientation);
        Assert.AreEqual(follow && continuous, request.ParentToAnchor);
        config.orientation = ActionEffectOrientation.ScreenOriented;
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out request));
        Assert.IsFalse(request.ParentToAnchor, "Screen/world facing must not inherit parent rotation implicitly.");
    }

    [Test]
    public void ScreenFacing_UsesExplicitCameraAndSampledAngle_NotGlobalCameraOrAnchorRotation()
    {
        var config = Config(); config.orientation = ActionEffectOrientation.ScreenOriented;
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out var request));
        Assert.IsFalse(request.TryResolveRotation(Quaternion.identity, Quaternion.identity, null, Vector2.zero, out _));
        Quaternion camera = Quaternion.Euler(20f, 80f, 10f);
        Assert.IsTrue(request.TryResolveRotation(Quaternion.Euler(90f, 0f, 0f), Quaternion.identity, camera, new Vector2(35f, 0f), out var rotation));
        Quaternion expected = ScreenOrientedImpactRotationResolver.Resolve(ScreenAngleMode.Preset, 35f, 0f, camera);
        Assert.That(Quaternion.Angle(expected, rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void WorldFacing_RollPreservesTheSelectedForwardAxis_AndSnapshotsPlayback()
    {
        var config = Config(); config.orientation = ActionEffectOrientation.WorldDirectional;
        config.worldDirection = ActionEffectWorldDirection.Custom;
        config.customWorldDirection = new Vector3(1f, 1f, 0.5f);
        config.simulationSpeed = 0.5f; config.occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;
        Assert.IsTrue(ActionEffectRequest.TryCreate(config, out var request));
        config.customWorldDirection = Vector3.back; config.simulationSpeed = 2f;
        Assert.IsTrue(request.TryResolveRotation(Quaternion.identity, Quaternion.identity, null, new Vector2(0f, 65f), out var rotation));
        Assert.That(Vector3.Distance(new Vector3(1f, 1f, 0.5f).normalized, rotation * Vector3.forward), Is.LessThan(0.0001f));
        Assert.AreEqual(0.5f, request.SimulationSpeed);
        Assert.AreEqual(HitVfxOcclusionMode.EnvironmentOnly, request.Occlusion);
    }

    [Test]
    public void LegacyPlaybackDefaults_DoNotRepairExplicitInvalidNewData()
    {
        var effect = JsonUtility.FromJson<EffectItemConfig>("{\"scale\":1,\"followAnchor\":true}");
        Assert.AreEqual(1f, effect.simulationSpeed);
        Assert.AreEqual(ActionEffectOrientationUpdate.MatchPositionBinding, effect.orientationUpdate);
        effect.prefab = _prefab; effect.simulationSpeed = 0f;
        effect = JsonUtility.FromJson<EffectItemConfig>(JsonUtility.ToJson(effect));
        Assert.AreEqual(0f, effect.simulationSpeed);
        var shake = JsonUtility.FromJson<ScreenShakeItemConfig>("{\"screenShake\":{\"enabled\":true,\"intensity\":0.7},\"_hasDirectionalSettings\":true,\"direction\":1,\"maxDistance\":30,\"frequency\":8}");
        Assert.IsTrue(ActionScreenShakeRequest.TryCreate(shake, out var request));
        Assert.AreEqual(0.7f, request.Intensity);
        Assert.AreEqual(30f, request.MaxDistance);
    }

    private sealed class Probe : IActionPresentation
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
    private EffectItemConfig Config() => new EffectItemConfig { prefab = _prefab };
    private EffectItem AddEffect(GameplayLane lane, int start, int duration)
    {
        var item = new EffectItem();
        item.EditorSetTiming(start, duration);
        item.Config.prefab = _prefab;
        lane.EditorItems.Add(item);
        return item;
    }
    private static GameplayLane AddLane(ActionTimelineData timeline)
    {
        var lane = new GameplayLane();
        timeline.EditorGameplayLanes.Add(lane);
        return lane;
    }
    private static ActionRuntimeScheduler Scheduler(ActionTimelineData timeline, ActionEffectBuffer buffer) =>
        new ActionRuntimeScheduler(timeline, new ActionRuntimeContext(null, ActionContext.None, buffer));
}
#endif
