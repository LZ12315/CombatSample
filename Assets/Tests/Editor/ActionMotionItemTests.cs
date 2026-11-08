#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionMotionItemTests
{
    private readonly List<Object> _objects = new List<Object>();
    private readonly List<IActionRangeRuntime> _runtimes = new List<IActionRangeRuntime>();

    [TearDown]
    public void TearDown()
    {
        foreach (IActionRangeRuntime runtime in _runtimes)
            runtime.Abort(default);
        _runtimes.Clear();
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void LegacyRootMotionConfig_DefaultsToOriginalDistance()
    {
        var restored = JsonUtility.FromJson<RootMotionItemConfig>("{\"playRate\":1}");
        Assert.That(restored.distanceScale, Is.EqualTo(1f));
    }

    [TestCase(0f)]
    [TestCase(2.5f)]
    public void RootMotionDistanceScale_ExplicitValuesSurviveSerialization(float scale)
    {
        var config = new RootMotionItemConfig { distanceScale = scale };
        var restored = JsonUtility.FromJson<RootMotionItemConfig>(JsonUtility.ToJson(config));
        Assert.That(restored.distanceScale, Is.EqualTo(scale));
    }

    [TestCase(0f, 0f)]
    [TestCase(0.5f, 0.5f)]
    [TestCase(3f, 3f)]
    [TestCase(-1f, 0f)]
    public void RootMotionDistanceScale_MatchesRuntimeAndPreviewWithoutChangingTimingOrBake(float scale, float expected)
    {
        Actor actor = CreateActor();
        var clip = Track(new AnimationClip());
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 60f));
        var animation = Track(ScriptableObject.CreateInstance<AnimationAsset>());
        animation.EditorSetClip(clip);
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(clip, 60, 1f, 1, "motion-scale-test",
            new[] { 0f, 1f }, new[] { Vector3.zero, new Vector3(60f, 30f, 0f) },
            new[] { Quaternion.identity, Quaternion.identity });
        animation.EditorSetRootMotionData(trajectory);
        var item = new RootMotionItem();
        item.EditorSetTiming(0, 4);
        item.Config.animationAsset = animation;
        item.Config.distanceScale = scale;
        var action = Track(ScriptableObject.CreateInstance<ActionAsset>());
        var lane = new GameplayLane();
        lane.EditorItems.Add(item);
        action.Timeline.EditorGameplayLanes.Add(lane);

        IActionRangeRuntime runtime = item.CreateRuntime(item.DurationFrames);
        _runtimes.Add(runtime);
        var context = new ActionRuntimeContext(actor, ActionContext.None);
        runtime.Enter(context);
        runtime.Tick(context, 0);
        actor.actorMotor.Translation.BeginMotionTick();
        Vector3 delta = actor.actorMotor.Translation.TrajectoryRootMotionLocalPosition;
        Assert.That(delta.x, Is.EqualTo(expected).Within(1e-5f));
        Assert.That(delta.y, Is.Zero);
        Assert.That(new ActionPreviewSpatialEvaluator().Evaluate(action, 0d).x, Is.EqualTo(expected).Within(1e-5f));
        Assert.That(item.DurationFrames, Is.EqualTo(4));
        Assert.That(item.Config.playRate, Is.EqualTo(1f));
        Assert.That(trajectory.TryExtract(0f, 1f / 60f, out RootMotionTransform original), Is.True);
        Assert.That(original.Position.x, Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void MoveToTarget_TracksTheSelectedObjectWithoutRotatingOrControllingHeight()
    {
        Actor actor = CreateActor();
        GameObject target = Target(new Vector3(10f, 100f, 0f));
        StartMove(actor, target, 3f, 1f);
        Assert.That(Compose(actor).x, Is.EqualTo(3f).Within(1e-5f));
        target.transform.position = new Vector3(0f, -100f, 10f);
        Vector3 velocity = Compose(actor);
        Assert.That(velocity.z, Is.EqualTo(3f).Within(1e-5f));
        Assert.That(velocity.y, Is.Zero);
        Assert.That(actor.transform.rotation, Is.EqualTo(Quaternion.identity));
        Assert.That(actor.actorMotor.Translation.HasVerticalVelocityOwner, Is.False);
    }

    [TestCase(0.25f)]
    [TestCase(1f)]
    [TestCase(4f)]
    public void MoveToTarget_CapsStepUsingActualMovementTime_AndOnlyCompletesAfterObservedArrival(float timeScale)
    {
        Actor actor = CreateActor();
        GameObject target = Target(new Vector3(1.01f, 0f, 0f));
        StartMove(actor, target, 100f, 1f);
        Vector3 velocity = Compose(actor, timeScale);
        Assert.That(velocity.x * CombatSimulationTiming.FixedDeltaTime, Is.EqualTo(0.01f).Within(1e-5f));
        // A blocked/collision-rejected step did not reach the target: continue trying next Motor tick.
        Assert.That(Compose(actor, timeScale).x, Is.EqualTo(velocity.x).Within(1e-5f));
        actor.transform.position = new Vector3(0.01f, 0f, 0f);
        Assert.That(Compose(actor, timeScale), Is.EqualTo(Vector3.zero));
        target.transform.position = Vector3.right * 20f;
        Assert.That(Compose(actor, timeScale), Is.EqualTo(Vector3.zero), "Once arrived, never chase again in this window.");
        Assert.That(actor.actorMotor.Translation.HasHorizontalVelocityOwner, Is.True);
    }

    [Test]
    public void MoveToTarget_ReevaluatesOnMotorTicksWithoutANewGameplayFrame_AndHonorsFreeze()
    {
        Actor actor = CreateActor();
        GameObject target = Target(Vector3.right * 10f);
        StartMove(actor, target, 3f, 1f);
        Assert.That(Compose(actor, 0f), Is.EqualTo(Vector3.zero));
        Assert.That(Compose(actor, 0.5f).x, Is.EqualTo(1.5f).Within(1e-5f));
        target.transform.position = Vector3.forward * 10f;
        Assert.That(Compose(actor, 0.5f).z, Is.EqualTo(1.5f).Within(1e-5f));
    }

    [Test]
    public void MoveToTarget_ArrivalUsesHorizontalRootDistance_EvenWhenSpeedIsZero()
    {
        Actor actor = CreateActor();
        GameObject target = Target(new Vector3(0.5f, 100f, 0f));
        StartMove(actor, target, 0f, 1f);
        Assert.That(Compose(actor), Is.EqualTo(Vector3.zero));
        target.transform.position = Vector3.right * 20f;
        Assert.That(Compose(actor), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void MoveToTarget_UsesNormalizedSpeedCurve_AndNeverMovesAwayForNegativeValues()
    {
        Actor actor = CreateActor();
        var item = new MoveToTargetItem();
        item.Config.targetSource = ActionMoveToTargetSource.ContextInstigator;
        item.Config.speed = 4f;
        item.Config.speedCurve = AnimationCurve.Linear(0f, 0.5f, 1f, -1f);
        var context = new ActionRuntimeContext(actor, new ActionContext(Target(Vector3.right * 20f), null));
        IActionRangeRuntime runtime = item.CreateRuntime(3);
        _runtimes.Add(runtime);
        runtime.Enter(context);
        runtime.Tick(context, 0);
        Assert.That(Compose(actor).x, Is.EqualTo(2f).Within(1e-5f));
        runtime.Tick(context, 2);
        Assert.That(Compose(actor), Is.EqualTo(Vector3.zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveToTarget_MissingOrDestroyedTarget_HoldsZeroUntilWindowCleanup(bool destroyTarget)
    {
        Actor actor = CreateActor();
        GameObject target = destroyTarget ? Target(Vector3.right * 10f) : null;
        IActionRangeRuntime runtime = StartMove(actor, target, 5f, 1f);
        if (target != null) Object.DestroyImmediate(target);
        Assert.That(Compose(actor), Is.EqualTo(Vector3.zero));
        Assert.That(actor.actorMotor.Translation.HasHorizontalVelocityOwner, Is.True);
        runtime.Exit(default, ActionRangeExitReason.Completed);
        Assert.That(actor.actorMotor.Translation.HasHorizontalVelocityOwner, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveToTarget_OverridesRootMotion_AndReleasesOnlyItsOwnerOnExitOrAbort(bool abort)
    {
        Actor actor = CreateActor();
        TranslationDomain translation = actor.actorMotor.Translation;
        MotionOwner previous = translation.BeginHorizontalVelocity();
        translation.SetHorizontalVelocity(previous, Vector3.forward * 2f);
        MotionOwner root = translation.BeginTrajectoryRootMotion();
        translation.SubmitTrajectoryRootMotion(root, Vector3.right * 100f);
        IActionRangeRuntime runtime = StartMove(actor, Target(Vector3.right * 10f), 3f, 1f);
        translation.BeginMotionTick();
        Vector3 velocity = translation.ComposeHorizontalMotion(Vector3.zero, false,
            Quaternion.identity, 1f, CombatSimulationTiming.FixedDeltaTime, out var source, out _);
        Assert.That(source, Is.EqualTo(HorizontalMotionSource.HorizontalVelocityOwner));
        Assert.That(velocity.x, Is.EqualTo(3f).Within(1e-5f));
        Assert.That(velocity.z, Is.Zero);
        if (abort) runtime.Abort(default);
        else runtime.Exit(default, ActionRangeExitReason.Interrupted);
        Assert.That(Compose(actor).z, Is.EqualTo(2f).Within(1e-5f));
        Assert.That(translation.DebugHorizontalVelocityOwnerCount, Is.EqualTo(1));
        Assert.That(translation.HasTrajectoryRootMotionOwner, Is.True);
    }

    private Actor CreateActor()
    {
        var go = Track(new GameObject("Motion contract actor"));
        Actor actor = go.AddComponent<Actor>();
        actor.actorMotor = go.AddComponent<ActorMotor>();
        return actor;
    }

    private GameObject Target(Vector3 position)
    {
        var target = Track(new GameObject("Motion contract target"));
        target.transform.position = position;
        return target;
    }

    private IActionRangeRuntime StartMove(Actor actor, GameObject target, float speed, float stopDistance)
    {
        var item = new MoveToTargetItem();
        item.Config.targetSource = ActionMoveToTargetSource.ContextTarget;
        item.Config.speed = speed;
        item.Config.stopDistance = stopDistance;
        var context = new ActionRuntimeContext(actor, new ActionContext(null, target));
        IActionRangeRuntime runtime = item.CreateRuntime(60);
        _runtimes.Add(runtime);
        runtime.Enter(context);
        runtime.Tick(context, 0);
        return runtime;
    }

    private static Vector3 Compose(Actor actor, float timeScale = 1f)
    {
        Assert.That(actor.actorMotor.Translation.TryComposeHorizontalVelocityOwner(timeScale,
            out Vector3 velocity, CombatSimulationTiming.FixedDeltaTime), Is.True);
        return velocity;
    }

    private T Track<T>(T value) where T : Object
    {
        _objects.Add(value);
        return value;
    }
}
#endif
