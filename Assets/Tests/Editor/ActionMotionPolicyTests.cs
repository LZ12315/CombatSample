#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ActionMotionPolicyTests
{
    private readonly List<Object> _objects = new();
    private readonly List<ActionRuntime> _runtimes = new();
    private readonly List<ActionMotionPolicySession> _policies = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var runtime in _runtimes) runtime.Abort();
        foreach (var policy in _policies) policy.Dispose();
        for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
    }

    private ActionAsset Asset(int duration = 1)
    {
        var asset = ScriptableObject.CreateInstance<ActionAsset>();
        asset.MotionPolicy.movement = ActionMovementControl.Allow;
        asset.MotionPolicy.turning = ActionTurnControl.Normal; // Neutral fixture; production defaults are tested below.
        _objects.Add(asset);
        if (duration > 1)
        {
            var tail = Window(asset, duration - 1, 1);
            tail.EditorSetMuted(true); // Reserve duration without introducing another active policy.
        }
        return asset;
    }

    private Actor Actor()
    {
        var root = new GameObject("Action Motion Policy Contract");
        _objects.Add(root);
        var actor = root.AddComponent<Actor>();
        actor.actorMotor = root.AddComponent<ActorMotor>();
        return actor;
    }

    private static MotionPolicyItem Window(ActionAsset asset, int start, int duration)
    {
        var lane = new GameplayLane();
        asset.Timeline.EditorGameplayLanes.Add(lane);
        var item = new MotionPolicyItem();
        item.EditorSetTiming(start, duration);
        lane.EditorItems.Add(item);
        return item;
    }

    private ActionMotionPolicySession Policy(ActionAsset asset, ActorMotor motor)
    {
        var policy = new ActionMotionPolicySession(asset, motor);
        _policies.Add(policy);
        return policy;
    }

    private ActionRuntime Runtime(ActionAsset asset, Actor actor)
    {
        var runtime = new ActionRuntime(asset, actor, ActionContext.None);
        _runtimes.Add(runtime);
        return runtime;
    }

    [Test]
    public void CompositeConstraints_KeepExternalRestrictionsAndReleaseOnlyTheirOwnScope()
    {
        var state = new MotionPolicyState();
        var externalMove = state.BeginLocomotionScale(0.3f);
        var externalGravity = state.BeginGravityScale(0.5f);
        var old = state.BeginActionPolicy(new MotionPolicyValues(0f, 1f, 0f, lockLocomotionRotation: true));
        var current = state.BeginActionPolicy(new MotionPolicyValues(0.8f, 0.5f, 2f,
            brakeLocomotion: true, turnSpeedLimit: 90f));
        Assert.That(state.LocomotionScale, Is.Zero);
        Assert.That(state.GravityScale, Is.Zero);
        Assert.That(state.LockLocomotionRotation, Is.True);
        Assert.That(state.EndActionPolicy(old), Is.True);
        Assert.That(state.EndActionPolicy(old), Is.False);
        Assert.That(state.LocomotionScale, Is.EqualTo(0.3f));
        Assert.That(state.AirLocomotionScale, Is.EqualTo(0.5f));
        Assert.That(state.GravityScale, Is.EqualTo(1f));
        Assert.That(state.BrakeLocomotion, Is.True);
        Assert.That(state.LockLocomotionRotation, Is.False);
        Assert.That(state.LocomotionTurnSpeedLimit, Is.EqualTo(90f));
        state.EndActionPolicy(current);
        Assert.That(state.LocomotionScale, Is.EqualTo(0.3f));
        Assert.That(state.GravityScale, Is.EqualTo(0.5f));
        state.EndLocomotionScale(externalMove); state.EndGravityScale(externalGravity);
        Assert.That(state.LocomotionScale, Is.EqualTo(1f));
        Assert.That(state.LocomotionTurnSpeedLimit, Is.Zero);
    }

    [Test]
    public void PolicyData_ValidatesActiveFieldsAndPreservesLegacyInterpretation()
    {
        var defaults = new ActionMotionPolicy();
        Assert.That(defaults.Capture().LockLocomotionRotation, Is.True);
        Assert.That(defaults.Capture().LocomotionScale, Is.Zero);
        defaults.movement = ActionMovementControl.Allow;
        defaults.turning = ActionTurnControl.Normal;
        Assert.That(defaults.Capture().IsNeutral, Is.True);
        var legacy = new MotionPolicyItemConfig { useLocomotionScale = true, locomotionScale = 0f };
        var read = new ActionMotionPolicyOverride(legacy);
        Assert.That(read.Movement, Is.True);
        Assert.That(read.Values.LocomotionScale, Is.Zero);
        Assert.That(legacy.useMovementControl, Is.False);
        defaults.turning = ActionTurnControl.Limited;
        defaults.maxTurnSpeed = float.NaN;
        Assert.Throws<InvalidOperationException>(() => defaults.Capture());
        defaults.turning = ActionTurnControl.Locked; // Inactive, retained speed is not used.
        Assert.That(defaults.Capture().LockLocomotionRotation, Is.True);
        defaults.gravityScale = -1f;
        Assert.Throws<InvalidOperationException>(() => defaults.Capture());
    }

    [Test]
    public void LegacyDefaultsAndWindows_AreReadOnlyAndKeepTheirMultipliers()
    {
        var asset = Asset();
        var item = Window(asset, 0, 2);
        JsonUtility.FromJsonOverwrite("{\"useLocomotionScale\":true,\"locomotionScale\":0.4,\"useAirLocomotionScale\":true,\"airLocomotionScale\":0.5,\"useGravityScale\":true,\"gravityScale\":2}", item.Config);
        string before = JsonUtility.ToJson(asset);
        var values = Policy(asset, Actor().actorMotor).Resolve(0);
        Assert.That(values.LocomotionScale, Is.EqualTo(0.4f));
        Assert.That(values.AirLocomotionScale, Is.EqualTo(0.5f));
        Assert.That(values.GravityScale, Is.EqualTo(2f));
        Assert.That(values.BrakeLocomotion || values.LockLocomotionRotation, Is.False);
        Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(before));
        var neutral = Asset(); Window(neutral, 0, 1); // An all-Inherit window is not a malformed policy.
        Assert.That(Policy(neutral, null).Resolve(0).IsNeutral, Is.True,
            "A neutral Action, including an all-Inherit window, must still run without a Motor.");
        var runtime = Runtime(neutral, null); runtime.Begin();
        Assert.That(runtime.Finish(), Is.True);
    }

    [Test]
    public void Windows_ReplaceOnlyTheirFieldsUseHalfOpenIntervalsAndSnapshotOnBegin()
    {
        var asset = Asset(5);
        asset.MotionPolicy.movement = ActionMovementControl.Locked;
        asset.MotionPolicy.turning = ActionTurnControl.Locked;
        asset.MotionPolicy.gravityScale = 0.25f;
        var move = Window(asset, 1, 2);
        move.Config.useMovementControl = true;
        move.Config.movement = ActionMovementControl.Allow;
        move.Config.locomotionScale = 0.5f;
        var turn = Window(asset, 2, 1);
        turn.Config.useTurnControl = true;
        turn.Config.turning = ActionTurnControl.Normal;
        var policy = Policy(asset, Actor().actorMotor);
        asset.MotionPolicy.movement = ActionMovementControl.Allow;
        move.Config.locomotionScale = 1f;
        move.EditorSetTiming(8, 1);
        Assert.That(policy.Resolve(0).LocomotionScale, Is.Zero);
        Assert.That(policy.Resolve(1).LocomotionScale, Is.EqualTo(0.5f));
        Assert.That(policy.Resolve(1).LockLocomotionRotation, Is.True);
        Assert.That(policy.Resolve(2).LockLocomotionRotation, Is.False);
        Assert.That(policy.Resolve(2).GravityScale, Is.EqualTo(0.25f));
        Assert.That(policy.Resolve(3).LocomotionScale, Is.Zero);
        Assert.That(policy.Resolve(3).LockLocomotionRotation, Is.True);
    }

    [Test]
    public void OverlappingWindows_AreOrderIndependentAndDoNotMultiplyTheActionDefaultAgain()
    {
        var asset = Asset();
        asset.MotionPolicy.locomotionScale = 0.1f;
        asset.MotionPolicy.gravityScale = 0.25f;
        asset.MotionPolicy.airLocomotionScale = 0.1f;
        asset.MotionPolicy.turning = ActionTurnControl.Locked;
        var first = Window(asset, 0, 3);
        first.Config.useMovementControl = first.Config.useGravityScale = first.Config.useAirLocomotionScale = first.Config.useTurnControl = true;
        first.Config.locomotionScale = first.Config.airLocomotionScale = 0.7f;
        first.Config.turning = ActionTurnControl.Limited; first.Config.maxTurnSpeed = 180f;
        first.Config.gravityScale = 0.5f;
        var second = Window(asset, 1, 3);
        second.Config.useMovementControl = second.Config.useGravityScale = second.Config.useAirLocomotionScale = second.Config.useTurnControl = true;
        second.Config.movement = ActionMovementControl.Brake;
        second.Config.airLocomotionScale = 0.4f;
        second.Config.turning = ActionTurnControl.Limited; second.Config.maxTurnSpeed = 90f;
        second.Config.gravityScale = 2f;
        var locked = Window(asset, 2, 1);
        locked.Config.useMovementControl = locked.Config.useTurnControl = true;
        locked.Config.movement = ActionMovementControl.Locked;
        locked.Config.turning = ActionTurnControl.Locked;
        foreach (float gravity in new[] { float.MaxValue, float.MaxValue, 0f })
        {
            var factor = Window(asset, 5, 1);
            factor.Config.useGravityScale = true; factor.Config.gravityScale = gravity;
        }
        var motor = Actor().actorMotor;
        var forward = Policy(asset, motor);
        asset.Timeline.EditorGameplayLanes.Reverse();
        var reverse = Policy(asset, motor);
        for (int frame = 0; frame < 6; frame++)
        {
            var a = forward.Resolve(frame); var b = reverse.Resolve(frame);
            Assert.That(b.LocomotionScale, Is.EqualTo(a.LocomotionScale));
            Assert.That(b.BrakeLocomotion, Is.EqualTo(a.BrakeLocomotion));
            Assert.That(b.GravityScale, Is.EqualTo(a.GravityScale));
            Assert.That(b.AirLocomotionScale, Is.EqualTo(a.AirLocomotionScale));
            Assert.That(b.LockLocomotionRotation, Is.EqualTo(a.LockLocomotionRotation));
            Assert.That(b.TurnSpeedLimit, Is.EqualTo(a.TurnSpeedLimit));
        }
        Assert.That(forward.Resolve(0).TurnSpeedLimit, Is.EqualTo(180f));
        Assert.That(forward.Resolve(1).TurnSpeedLimit, Is.EqualTo(90f));
        Assert.That(forward.Resolve(1).LockLocomotionRotation, Is.False);
        Assert.That(forward.Resolve(2).LockLocomotionRotation, Is.True);
        Assert.That(forward.Resolve(3).LockLocomotionRotation, Is.False);
        Assert.That(forward.Resolve(4).LockLocomotionRotation, Is.True);
        Assert.That(forward.Resolve(1).AirLocomotionScale, Is.EqualTo(0.4f));
        Assert.That(forward.Resolve(4).AirLocomotionScale, Is.EqualTo(0.1f));
        Assert.That(forward.Resolve(1).LocomotionScale, Is.EqualTo(0.7f));
        Assert.That(forward.Resolve(1).BrakeLocomotion, Is.True);
        Assert.That(forward.Resolve(1).GravityScale, Is.EqualTo(1f));
        Assert.That(forward.Resolve(2).LocomotionScale, Is.Zero);
        Assert.That(forward.Resolve(3).GravityScale, Is.EqualTo(2f));
        Assert.That(forward.Resolve(4).GravityScale, Is.EqualTo(0.25f));
        Assert.That(forward.Resolve(5).GravityScale, Is.Zero,
            "A zero factor must not depend on lane order or intermediate overflow.");
    }

    [Test]
    public void ExternalRestrictionsAndOldSessionCleanup_CannotBeReleasedByANewWindow()
    {
        var motor = Actor().actorMotor;
        var external = motor.BeginLocomotionScale(0.3f);
        var oldAsset = Asset(); oldAsset.MotionPolicy.movement = ActionMovementControl.Locked;
        var old = Policy(oldAsset, motor); old.ApplyFrame(0);
        var asset = Asset(); asset.MotionPolicy.movement = ActionMovementControl.Locked;
        var item = Window(asset, 0, 1);
        item.Config.useMovementControl = true; item.Config.locomotionScale = 0.8f;
        var current = Policy(asset, motor); current.ApplyFrame(0);
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.Zero);
        old.Dispose(); old.Dispose();
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.EqualTo(0.3f));
        motor.EndLocomotionScale(external);
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.EqualTo(0.8f));
        current.Dispose();
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.EqualTo(1f));
        Assert.That(motor.MotionPolicy.DebugLocomotionScaleOwnerCount, Is.Zero);
    }

    [Test]
    public void Policy_PersistsThroughFractionalFramesAndPauseUntilTheActualWindowBoundary()
    {
        var asset = Asset(3); var actor = Actor();
        asset.MotionPolicy.movement = ActionMovementControl.Locked;
        var item = Window(asset, 0, 1);
        item.Config.useMovementControl = true;
        item.Config.locomotionScale = 0.5f;
        var runtime = Runtime(asset, actor);
        runtime.SetSpeed(0.5f); runtime.Begin();
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.EqualTo(0.5f));
        Assert.That(runtime.Finish(), Is.False);
        Assert.That(runtime.Advance(), Is.False);
        runtime.Pause();
        Assert.That(runtime.Advance(), Is.False);
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.EqualTo(0.5f));
        runtime.Resume();
        Assert.That(runtime.Advance(), Is.True);
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.Zero);
    }

    [TestCase("complete")]
    [TestCase("interrupt")]
    [TestCase("abort")]
    [TestCase("exception")]
    public void Runtime_HoldsTheLastMotionFrameAndReleasesEveryTerminationPath(string ending)
    {
        var asset = Asset(ending == "complete" ? 1 : 3); var actor = Actor();
        asset.MotionPolicy.movement = ActionMovementControl.Locked;
        if (ending == "exception")
        {
            var lane = new GameplayLane(); asset.Timeline.EditorGameplayLanes.Add(lane);
            lane.EditorItems.Add(new ThrowPoint());
        }
        var runtime = Runtime(asset, actor); runtime.Begin();
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.Zero);
        Assert.That(runtime.Interrupt(), Is.False, "An open world frame cannot be normally interrupted.");
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.Zero);
        if (ending == "abort") runtime.Abort();
        else
        {
            runtime.Finish();
            if (ending == "interrupt") Assert.That(runtime.Interrupt(), Is.True);
            if (ending == "exception") Assert.Throws<InvalidOperationException>(() => runtime.Advance());
        }
        Assert.That(actor.actorMotor.MotionPolicy.DebugLocomotionScaleOwnerCount, Is.Zero);
        Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.EqualTo(1f));
    }

    [Test]
    public void LoopRestart_ReacquiresBeforeTheNextMotionAndDisposeDoesNotLeak()
    {
        var asset = Asset(); var actor = Actor();
        asset.MotionPolicy.movement = ActionMovementControl.Locked;
        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), actor, ActionContext.None);
        session.Completed += value => value.Restart();
        try
        {
            session.Start();
            Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.Zero);
            session.FinishFrame();
            Assert.That(actor.actorMotor.MotionPolicy.DebugLocomotionScaleOwnerCount, Is.Zero);
            Assert.That(session.TryPlayFrame(1f / 60f), Is.True);
            Assert.That(actor.actorMotor.MotionPolicy.LocomotionScale, Is.Zero);
            session.Stop(ActionPlaybackStopMode.Explicit);
            Assert.That(actor.actorMotor.MotionPolicy.DebugLocomotionScaleOwnerCount, Is.Zero);
        }
        finally { session.Dispose(); }
    }

    [TestCase(float.NaN)]
    [TestCase(-1f)]
    public void InvalidFutureWindows_AreRejectedBeforeAnyPolicyIsAcquired(float gravity)
    {
        var asset = Asset(); var actor = Actor();
        var item = Window(asset, 2, 1);
        item.Config.useGravityScale = true; item.Config.gravityScale = gravity;
        var runtime = Runtime(asset, actor);
        Assert.Throws<InvalidOperationException>(() => runtime.Begin());
        Assert.That(runtime.State, Is.EqualTo(ActionRuntimeState.Aborted));
        Assert.That(actor.actorMotor.MotionPolicy.DebugGravityScaleOwnerCount, Is.Zero);
    }

    private static LocomotionIntent Input => new()
    { WorldMoveDirection = Vector3.forward, FacingDirection = Vector3.right, MoveStrength = 1f };

    private static LocomotionMotionContext Context(float scale = 1f, bool brake = false, bool turnLock = false,
        float turnLimit = 0f, Quaternion? rotation = null) => new(0.1f, true, Vector3.up,
            rotation ?? Quaternion.identity, default,
            new MotionStateSnapshot(scale, 1f, 1f, 1f, false, false, false, false, brake, turnLock, turnLimit));

    [Test]
    public void Brake_IsContinuousWhileLockClearsHiddenVelocityAndRawInputSurvives()
    {
        var runner = new LocomotionRunner(); runner.Initialize(Quaternion.identity);
        var config = LocomotionMovementConfig.Default;
        config.Acceleration = 10f; config.Deceleration = 2f;
        runner.Prepare(Input, true, Context(0.5f), config);
        runner.Prepare(Input, true, Context(0.5f), config);
        float before = runner.CachedVelocity.magnitude * 0.5f;
        runner.Prepare(Input, true, Context(brake: true), config);
        Assert.That(runner.CachedVelocity.magnitude, Is.EqualTo(before - config.Deceleration * 0.1f).Within(0.001f));
        Assert.That(runner.CachedVelocity.magnitude, Is.GreaterThan(0f), "Brake is deceleration, not an immediate lock.");
        Assert.That(runner.EffectiveIntent.MoveStrength, Is.Zero);
        for (int i = 0; i < 10; i++) runner.Prepare(Input, true, Context(0f), config);
        Assert.That(runner.CachedVelocity, Is.EqualTo(Vector3.zero));
        runner.Prepare(Input, true, Context(), config);
        Assert.That(runner.CachedVelocity.magnitude, Is.EqualTo(config.Acceleration * 0.1f).Within(0.001f));
        var raw = Input;
        var animation = new LocomotionRuntimeAnimationContext(raw, true, Vector3.forward, Vector3.zero,
            Context(0f), 0f, 0, 0.1f);
        Assert.That(raw.MoveStrength, Is.EqualTo(1f));
        Assert.That(animation.HasMovingInput, Is.False);
        Assert.That(animation.Intent.FacingDirection, Is.EqualTo(raw.FacingDirection));
        Assert.That(new LocomotionMoveParameters(animation, 0f).Read(LocomotionMoveParameter.InputStrength), Is.Zero);
    }

    [Test]
    public void LocomotionPipeline_EnforcesTheMotionSnapshotWithoutDeletingControlIntent()
    {
        var asset = ScriptableObject.CreateInstance<LocomotionMixerAsset>(); _objects.Add(asset);
        var actor = Actor();
        var locomotion = actor.gameObject.AddComponent<ActorLocomotion>(); actor.actorLocomotion = locomotion;
        LocomotionContractTests.ConfigureAssets(locomotion, asset);
        locomotion.SetLocomotionIntent(Input); locomotion.BeginControlTick();
        var request = locomotion.BuildMotionRequest(Context(0f, turnLock: true));
        Assert.That(request.WorldPlanarVelocity, Is.EqualTo(Vector3.zero));
        Assert.That(Quaternion.Angle(request.TargetWorldRotation, Quaternion.identity), Is.LessThan(0.001f));
        Assert.That(locomotion.TryGetControlIntent(out var raw), Is.True);
        Assert.That(raw.MoveStrength, Is.EqualTo(1f));
        Assert.That(raw.WorldMoveDirection, Is.EqualTo(Vector3.forward));
        Assert.That(locomotion.EffectiveIntent.MoveStrength, Is.Zero);
    }

    [Test]
    public void LockedTurning_DoesNotAccumulateAndLimitedTurningStartsFromTheBody()
    {
        var runner = new LocomotionRunner(); runner.Initialize(Quaternion.identity);
        var config = LocomotionMovementConfig.Default; config.TurnResponseTime = 0f;
        var body = Quaternion.Euler(0f, -45f, 0f);
        for (int i = 0; i < 20; i++) runner.Prepare(Input, true, Context(turnLock: true, rotation: body), config);
        Assert.That(Quaternion.Angle(runner.PendingRotation, body), Is.LessThan(0.001f));
        runner.Prepare(Input, true, Context(turnLimit: 45f, rotation: body), config);
        Assert.That(Quaternion.Angle(runner.PendingRotation, body), Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(LocomotionVelocityFeedback.SamePolicy(Context().MotionState, Context(brake: true).MotionState), Is.False);
        Assert.That(LocomotionVelocityFeedback.SamePolicy(Context().MotionState, Context(turnLimit: 45f).MotionState), Is.False);
    }

    [Test]
    public void Motor_ConstraintsAffectOnlyLocomotionAndKeepTheExistingSourcePriority()
    {
        var actor = Actor(); var motor = actor.actorMotor;
        actor.gameObject.SendMessage("Awake"); // Standard startup is explicit in EditMode; no private-field injection.
        var asset = Asset();
        asset.MotionPolicy.movement = ActionMovementControl.Locked;
        asset.MotionPolicy.turning = ActionTurnControl.Locked;
        var runtime = Runtime(asset, actor); runtime.Begin();
        var request = new LocomotionMotionRequest(Vector3.forward * 5f, Quaternion.Euler(0f, 90f, 0f), true, true);
        void Compose() { motor.BeginMotion(0.1f); motor.ComposeMotion(request); }
        Compose();
        Assert.That(Vector3.ProjectOnPlane(motor.RequestedVelocity, Vector3.up).magnitude, Is.Zero);
        Assert.That(Quaternion.Angle(motor.RequestedRotation, Quaternion.identity), Is.LessThan(0.001f));
        motor.CancelPreparedMotion();
        var root = motor.BeginTrajectoryRootMotion();
        motor.BeginRootRotation(out var rootTurn);
        motor.SubmitTrajectoryRootMotion(root, Vector3.forward);
        motor.SubmitRootRotation(rootTurn, Quaternion.Euler(0f, 20f, 0f));
        Compose();
        Assert.That(motor.RequestedVelocity.z, Is.EqualTo(10f).Within(0.001f));
        Assert.That(Quaternion.Angle(motor.RequestedRotation, Quaternion.Euler(0f, 20f, 0f)), Is.LessThan(0.001f));
        motor.CancelPreparedMotion();
        var velocity = motor.BeginHorizontalVelocity(); motor.SetHorizontalVelocity(velocity, Vector3.right * 3f);
        motor.BeginScriptedRotation(out var scripted);
        motor.SubmitScriptedRotation(scripted, Quaternion.Euler(0f, -7f, 0f));
        motor.SubmitTrajectoryRootMotion(root, Vector3.forward);
        Compose();
        Assert.That(motor.RequestedVelocity.x, Is.EqualTo(3f).Within(0.001f));
        Assert.That(motor.RequestedVelocity.z, Is.Zero.Within(0.001f));
        Assert.That(Quaternion.Angle(motor.RequestedRotation, Quaternion.Euler(0f, -7f, 0f)), Is.LessThan(0.001f));
        motor.CancelPreparedMotion();
        motor.EndHorizontalVelocity(velocity); motor.EndScriptedRotation(scripted);
        motor.EndTrajectoryRootMotion(root); motor.EndRootRotation(rootTurn);
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.Zero);
        Assert.That(runtime.Finish(), Is.True);
        Assert.That(motor.MotionPolicy.LocomotionScale, Is.EqualTo(1f));
    }

    [Serializable]
    private sealed class ThrowPoint : PointGameplayItem
    {
        internal ThrowPoint() { EditorSetFrame(1); }
        protected internal override IActionPointRuntime CreateRuntime() => new ThrowRuntime();
    }
    private sealed class ThrowRuntime : IActionPointRuntime
    {
        public void Execute(ActionRuntimeContext context) => throw new InvalidOperationException("Policy cleanup probe.");
    }
}
#endif
