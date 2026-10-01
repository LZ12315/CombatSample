#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class LocomotionSetDecisionTests
{
    [Test]
    public void StartReleaseAndStopReinput_UseTheMotionBeforeIntegration()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
        Step(machine, Context(Vector3.zero, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Stop));
        Step(machine, Context(Vector3.forward, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.zero, Vector3.forward), false);
        Step(machine, Context(Vector3.right, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
    }

    [Test]
    public void StrongReversal_PlaysOnePivotUntilTheReversalConditionClears()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.forward), false);
        Step(machine, Context(Vector3.back, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Pivot));
        Step(machine, Context(Vector3.back, Vector3.forward), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.back, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.back, Vector3.back), false);
        Step(machine, Context(Vector3.forward, Vector3.back), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Pivot));
    }

    [Test]
    public void ZeroAnimationDelta_DoesNotConsumeAnInputEdgeOrCompleteAClip()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.zero, dt: 0f), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.forward, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
        Step(machine, Context(Vector3.zero, Vector3.forward, dt: 0f), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
        Step(machine, Context(Vector3.zero, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Stop));
    }

    [Test]
    public void ActionCoverReplacementAndRelease_DoNotQueueTransientAnimations()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.zero), false);
        Step(machine, Context(Vector3.back, Vector3.forward, action: 1), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.forward, Vector3.back, action: 2), false);
        Step(machine, Context(Vector3.right, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.right, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
    }

    [Test]
    public void StopCompletion_ReturnsToMoveWithoutRetriggeringStop()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.forward), false);
        Step(machine, Context(Vector3.zero, Vector3.forward), false);
        Step(machine, Context(Vector3.zero, Vector3.forward), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.zero, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.forward, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        machine.Reset();
        Step(machine, Context(Vector3.forward, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
    }

    [Test]
    public void UnavailableTransition_ConsumesItsEdgeWithoutEnteringOrQueuingIt()
    {
        var machine = new LocomotionSetStateMachine();
        Assert.That(machine.DecideNextState(Context(Vector3.forward, Vector3.zero), false),
            Is.EqualTo(LocomotionSetState.Start));
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        machine.CommitState(LocomotionSetState.Move);
        Assert.That(machine.DecideNextState(Context(Vector3.forward, Vector3.zero), false),
            Is.EqualTo(LocomotionSetState.Move));
    }

    private static void Step(LocomotionSetStateMachine machine,
        in LocomotionRuntimeAnimationContext context, bool completed) =>
        machine.CommitState(machine.DecideNextState(context, completed));

    internal static LocomotionRuntimeAnimationContext Context(Vector3 input, Vector3 before,
        Vector3? model = null, float dt = 1f / 60f, int action = 0,
        Quaternion? facing = null, float vertical = 0f, float scale = 1f)
    {
        var policy = new MotionStateSnapshot(scale, 1f, 1f, 1f, false, false, false, false);
        var motor = new LocomotionMotionContext(dt, true, Vector3.up,
            facing ?? Quaternion.identity, default, policy);
        return new LocomotionRuntimeAnimationContext(
            new LocomotionIntent { WorldMoveDirection = input, MoveStrength = input == Vector3.zero ? 0f : 1f },
            input != Vector3.zero, before, model ?? before, motor, vertical, action, dt);
    }
}

public sealed class LocomotionAnimationContractTests
{
    private readonly List<Object> _objects = new();
    private readonly List<LocomotionRuntime> _runtimes = new();
    private GameObject _root;
    private Transform _probe;
    private AnimancerComponent _animancer;
    private ActorAnimation _animation;
    private ActorAnimationLocomotionOwner _owner;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Locomotion Contract Actor");
        _probe = new GameObject("PoseProbe").transform;
        _probe.SetParent(_root.transform, false);
        Animator animator = _root.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animancer = _root.AddComponent<AnimancerComponent>();
        _animancer.Animator = animator;
        _animation = _root.AddComponent<ActorAnimation>();
        _owner = _animation.BeginLocomotionSession();
    }

    [TearDown]
    public void TearDown()
    {
        _animation.EndLocomotionSession(_owner);
        foreach (LocomotionRuntime runtime in _runtimes)
        {
            runtime.Exit(null, null);
            runtime.Dispose();
        }
        _runtimes.Clear();
        Object.DestroyImmediate(_root);
        for (int i = _objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveMixers_UseThresholdsPolicyLocalFacingAndSyncMembership(bool twoDimensional)
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset move = Clip("Move", 3f);
        string definition = twoDimensional
            ? $"{{\"blendType\":1,\"twoDimensional\":{{\"samples\":[{Sample2D(idle, 0, 0, false)},{Sample2D(move, 0, 2, true)}]}}}}"
            : $"{{\"blendType\":0,\"oneDimensional\":{{\"samples\":[{Sample1D(move, 2, true)},{Sample1D(idle, 0, false)}]}}}}";
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(definition));
        Quaternion facing = twoDimensional ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        Vector3 world = twoDimensional ? Vector3.right * 2f : Vector3.forward * 2f;
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(world, world, facing: facing, scale: 0.5f));
        Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
        _animation.Evaluate(0f);
        var mixer = (ManualMixerState)request.State;
        Assert.That(mixer.GetChild(0).Clip, Is.SameAs(idle.Clip));
        Assert.That(mixer.GetChild(0).Weight, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(mixer.GetChild(1).Weight, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(mixer.IsSynchronized(mixer.GetChild(0)), Is.False);
        Assert.That(mixer.IsSynchronized(mixer.GetChild(1)), Is.True);
        Assert.That(_probe.localPosition.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(runtime.UpdateAnimation(Context(world, world)).State, Is.SameAs(mixer));
    }

    [Test]
    public void NonLoopingMoveSample_RestartsOnReactivationButNotDuringContinuousInput()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset run = Clip("NonLooping Run", 3f, looping: false);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(idle, run)));
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        _animation.SubmitLocomotion(_owner, request);
        _animation.Evaluate(1.2f);
        var mixer = (ManualMixerState)request.State;
        AnimancerState runState = mixer.GetChild(1);
        Assert.That(runState.TimeD, Is.GreaterThanOrEqualTo(runState.Length));
        // A sample becomes inactive only after the Move parameter has finished blending to zero.
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero, dt: 0.1f)));
        _animation.Evaluate(0.1f);
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward)));
        Assert.That(runState.TimeD, Is.EqualTo(0d).Within(1e-5));
        _animation.Evaluate(0.1f);
        double before = runState.TimeD;
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward)));
        Assert.That(runState.TimeD, Is.EqualTo(before).Within(1e-5));
    }

    [Test]
    public void Set_SelectsClosestDirectionsCompletesClipsAndDiscardsAnExitedTransient()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset move = Clip("Move", 2f);
        AnimationAsset startForward = Clip("Start Forward", 3f, false);
        AnimationAsset startRight = Clip("Start Right", 4f, false);
        AnimationAsset stop = Clip("Stop Right", 5f, false);
        AnimationAsset pivot = Clip("Pivot Right To Left", 6f, false);
        string entries = $",\"start\":[{{\"animation\":{Ref(startForward)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}},"
            + $"{{\"animation\":{Ref(startRight)},\"targetLocalDirection\":{{\"x\":1,\"y\":0}}}}],"
            + $"\"stop\":[{{\"animation\":{Ref(stop)},\"sourceLocalDirection\":{{\"x\":1,\"y\":0}}}}],"
            + $"\"pivot\":[{{\"animation\":{Ref(pivot)},\"sourceLocalDirection\":{{\"x\":1,\"y\":0}},\"targetLocalDirection\":{{\"x\":-1,\"y\":0}}}}]";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move), entries));
        LocomotionAnimationRequest start = runtime.UpdateAnimation(Context(Vector3.right, Vector3.zero));
        Assert.That(start.State.Clip, Is.SameAs(startRight.Clip));
        _animation.SubmitLocomotion(_owner, start);
        _animation.Evaluate(1.1f);
        LocomotionAnimationRequest moving = runtime.UpdateAnimation(Context(Vector3.right, Vector3.right));
        Assert.That(moving.IsMove, Is.True);
        _animation.SubmitLocomotion(_owner, moving);
        _animation.Evaluate(0.2f);
        LocomotionAnimationRequest stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.right));
        Assert.That(stopping.State.Clip, Is.SameAs(stop.Clip));
        _animation.SubmitLocomotion(_owner, stopping);
        LocomotionAnimationRequest reversing = runtime.UpdateAnimation(Context(Vector3.left, Vector3.right));
        Assert.That(reversing.State.Clip, Is.SameAs(pivot.Clip));
        _animation.SubmitLocomotion(_owner, reversing);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(6f).Within(0.001f));
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Enter(null, null);
        _owner = _animation.BeginLocomotionSession();
        Assert.That(runtime.UpdateAnimation(Context(Vector3.right, Vector3.right)).IsMove, Is.True);
        runtime.Exit(null, null);
        runtime.Enter(null, null);
        Assert.That(runtime.UpdateAnimation(Context(Vector3.forward + Vector3.right, Vector3.zero)).State.Clip,
            Is.SameAs(startForward.Clip), "Equal direction scores retain authored order.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveOnlySet_ProducesContinuousPosesWhileStartingReversingAndBraking(bool twoDimensional)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        string definition = twoDimensional
            ? $"{{\"blendType\":1,\"twoDimensional\":{{\"samples\":[{Sample2D(idle, 0, 0, false)},"
                + $"{Sample2D(move, 0, 1, false)},{Sample2D(move, 0, -1, false)}]}}}}"
            : Move1D(idle, move);
        var asset = Asset<LocomotionSetAsset>(definition);
        var runtime = Runtime<LocomotionSetRuntime>(asset);
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        var intent = new LocomotionIntent
        {
            WorldMoveDirection = Vector3.back, MoveStrength = 1f, FacingDirection = Vector3.back
        };
        var motion = runtime.UpdateMotion(new LocomotionRuntimeMotionContext(runner, intent, true,
            Context(Vector3.back, Vector3.zero).Motor));
        Assert.That(motion.WorldPlanarVelocity.z, Is.LessThan(0f));
        Assert.That(Quaternion.Angle(Quaternion.identity, motion.TargetWorldRotation), Is.GreaterThan(0f));
        LogAssert.Expect(LogType.Warning, new Regex($"Move {(twoDimensional ? "2D" : "1D")}\\[1\\].*Retaining PlayRate 1"));
        if (twoDimensional)
            LogAssert.Expect(LogType.Warning, new Regex("Move 2D\\[2\\].*Retaining PlayRate 1"));

        var inputs = new[] { Vector3.forward, Vector3.back, Vector3.zero, Vector3.zero };
        var speeds = new[] { 1f, 0.5f, 0.25f, 0f };
        AnimancerState state = null;
        for (int i = 0; i < inputs.Length; i++)
        {
            var request = runtime.UpdateAnimation(Context(inputs[i], i == 0 ? Vector3.zero : Vector3.forward,
                model: Vector3.forward * speeds[i]));
            Assert.That(request.IsMove, Is.True);
            Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Move));
            Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
            _animation.Evaluate(0.2f);
            if (state != null) Assert.That(request.State, Is.SameAs(state));
            state = request.State;
            Assert.That(state.Speed, Is.EqualTo(1f));
            float visibleSpeed = twoDimensional ? request.Parameter.magnitude : request.Parameter.x;
            Assert.That(_probe.localPosition.x, Is.EqualTo(1f + 2f * visibleSpeed).Within(0.001f));
        }
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartOrPivotRelease_WithoutStopReturnsToMovingPose(bool usePivot)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        AnimationAsset transition = Clip("Transition", 7f, false);
        string entries = usePivot
            ? $",\"pivot\":[{{\"animation\":{Ref(transition)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}},\"targetLocalDirection\":{{\"x\":0,\"y\":-1}}}}]"
            : $",\"start\":[{{\"animation\":{Ref(transition)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move), entries));
        if (usePivot) runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        var request = runtime.UpdateAnimation(Context(usePivot ? Vector3.back : Vector3.forward,
            usePivot ? Vector3.forward : Vector3.zero));
        Assert.That(request.State.Clip, Is.SameAs(transition.Clip));
        _animation.SubmitLocomotion(_owner, request);
        _animation.Evaluate(0.1f);
        var released = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward, model: Vector3.forward * 0.5f,
            dt: 0.1f));
        Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Move));
        Assert.That(_animation.SubmitLocomotion(_owner, released), Is.True);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(2f).Within(0.001f));
    }

    [TestCase(5f, 60)]
    [TestCase(7f, 60)]
    [TestCase(7f, 120)]
    public void ContinuousReversal_BlendsThroughTheSpeedDipAndStopsPromptly(float maximumSpeed, int tickRate)
    {
        AnimationAsset idle = Clip("Idle", 1f), walk = Clip("Walk", 3f), run = Clip("Run", 5f);
        var asset = Asset<LocomotionSetAsset>(
            $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)},"
            + $"{{\"animation\":{Ref(walk)},\"threshold\":0.4,\"sync\":false}},{Sample1D(run, 1, false)}]}}}}");
        JsonUtility.FromJsonOverwrite($"{{\"movementConfig\":{{\"MaxSpeed\":{maximumSpeed},"
            + "\"Acceleration\":20,\"Deceleration\":32,\"RotateSpeed\":600,\"TurnResponseTime\":0.08}}", asset);
        var runtime = Runtime<LocomotionSetRuntime>(asset);
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        float dt = 1f / tickRate;
        var policy = new MotionStateSnapshot(1f, 1f, 1f, 1f, false, false, false, false);
        LogAssert.Expect(LogType.Warning, new Regex("Move 1D\\[1\\].*Retaining PlayRate 1"));
        LogAssert.Expect(LogType.Warning, new Regex("Move 1D\\[2\\].*Retaining PlayRate 1"));

        LocomotionAnimationRequest Tick(Vector3 direction)
        {
            Vector3 before = runner.CachedVelocity;
            var intent = new LocomotionIntent
            {
                WorldMoveDirection = direction, FacingDirection = direction,
                MoveStrength = direction == Vector3.zero ? 0f : 1f
            };
            var motor = new LocomotionMotionContext(dt, true, Vector3.up, runner.PendingRotation, default, policy);
            runtime.UpdateMotion(new LocomotionRuntimeMotionContext(runner, intent, true, motor));
            var context = new LocomotionRuntimeAnimationContext(intent, true, before, runner.CachedVelocity,
                motor, 0f, 0, dt);
            var request = runtime.UpdateAnimation(context);
            Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Move));
            Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
            _animation.Evaluate(dt);
            return request;
        }

        Tick(Vector3.zero);
        for (int i = 0; i < tickRate; i++) Tick(Vector3.forward);
        float lowestModelSpeed = maximumSpeed;
        float highestIdleWeight = 0f;
        for (int i = 0; i < tickRate; i++)
        {
            var request = Tick(Vector3.back);
            lowestModelSpeed = Mathf.Min(lowestModelSpeed, runner.CachedVelocity.magnitude);
            highestIdleWeight = Mathf.Max(highestIdleWeight, request.State.GetChild(0).Weight);
            Assert.That(_probe.localPosition.x, Is.GreaterThan(2.8f), "Check the pose after Graph Evaluate, not only the state enum.");
        }
        Assert.That(lowestModelSpeed, Is.LessThan(0.4f), "The gameplay reversal must still enter the Idle/Walk blend range.");
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(-maximumSpeed).Within(0.001f));
        Assert.That(highestIdleWeight, Is.LessThan(0.1f), "A brief reversal speed dip must not dominate the pose with Idle.");
        int stoppedTicks = 0;
        LocomotionAnimationRequest stopping = default;
        int requiredStoppedTicks = Mathf.CeilToInt(0.1f / dt) + 1;
        for (int i = 0; i < tickRate && stoppedTicks < requiredStoppedTicks; i++)
        {
            stopping = Tick(Vector3.zero);
            if (runner.CachedVelocity == Vector3.zero) stoppedTicks++;
        }
        Assert.That(stoppedTicks, Is.EqualTo(requiredStoppedTicks));
        Assert.That(stopping.Parameter.x, Is.Zero);
        Assert.That(stopping.State.GetChild(0).Weight, Is.EqualTo(1f).Within(0.001f));
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void HorizontalMoveBlend_FreezesOnZeroDeltaAndRebaselinesOnReentry()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(idle, move)));
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward * 7f));
        var slowing = runtime.UpdateAnimation(Context(Vector3.back, Vector3.zero));
        _animation.SubmitLocomotion(_owner, slowing);
        _animation.Evaluate(1f / 60f);
        float weight = slowing.State.GetChild(0).Weight;
        double time = slowing.State.GetChild(1).TimeD;
        for (int i = 0; i < 30; i++)
        {
            Assert.That(runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero, dt: 0f)).State, Is.Null);
            _animation.Evaluate(0f);
        }
        Assert.That(slowing.State.GetChild(0).Weight, Is.EqualTo(weight));
        Assert.That(slowing.State.GetChild(1).TimeD, Is.EqualTo(time));
        var resumed = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero));
        Assert.That(resumed.Parameter.x, Is.GreaterThan(0.6f), "Resume one tick without catching up paused time.");
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Enter(null, null);
        _owner = _animation.BeginLocomotionSession();
        var reentered = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero));
        Assert.That(reentered.Parameter.x, Is.Zero, "A new session cannot inherit the old parameter lag.");
        _animation.SubmitLocomotion(_owner, reentered);
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void StopWithoutTrajectory_CompletesIntoCurrentModelMove()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f), stop = Clip("Stop", 7f, false);
        var asset = Asset<LocomotionSetAsset>(Move1D(idle, move),
            $",\"stop\":[{{\"animation\":{Ref(stop)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]");
        var issues = new List<string>();
        asset.CollectAnimationCoverageIssues(issues);
        Assert.That(issues, Has.None.Contains("Stop"));
        var runtime = Runtime<LocomotionSetRuntime>(asset);
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.State.Clip, Is.SameAs(stop.Clip));
        _animation.SubmitLocomotion(_owner, stopping);
        _animation.Evaluate(1.1f);
        Assert.That(stopping.State.TimeD, Is.GreaterThanOrEqualTo(stop.Clip.length));
        var moving = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward, model: Vector3.forward * 0.25f,
            dt: 0.1f));
        Assert.That(moving.IsMove, Is.True);
        Assert.That(moving.Parameter.x, Is.EqualTo(0.25f));
        _animation.SubmitLocomotion(_owner, moving);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void StopReinput_WithoutPivotReturnsToMove()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        AnimationAsset start = Clip("Start", 5f, false), stop = Clip("Stop", 7f, false);
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move),
            $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}],"
            + $"\"stop\":[{{\"animation\":{Ref(stop)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]"));
        var starting = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        _animation.SubmitLocomotion(_owner, starting);
        _animation.Evaluate(1.1f);
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward)));
        _animation.Evaluate(0.2f);
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward)));
        _animation.Evaluate(0.1f);
        var reversing = runtime.UpdateAnimation(Context(Vector3.back, Vector3.forward, model: Vector3.forward * 0.5f,
            dt: 0.1f));
        Assert.That(reversing.IsMove, Is.True);
        Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Move));
        _animation.SubmitLocomotion(_owner, reversing);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(2f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidStartGroup_ReportsOnceAndDoesNotDisableValidStop(bool invalidDirection)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        AnimationAsset start = Clip("Start", 5f, false), stop = Clip("Stop", 7f, false);
        string damaged = invalidDirection
            ? $"{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":0}}}}"
            : "{\"targetLocalDirection\":{\"x\":0,\"y\":1}}";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move),
            $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}},"
            + damaged + "],"
            + $"\"stop\":[{{\"animation\":{Ref(stop)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]"));
        LogAssert.Expect(LogType.Warning, new Regex("Move 1D\\[1\\].*Retaining PlayRate 1"));
        LogAssert.Expect(LogType.Error, new Regex("Actor 'unbound'.*Asset 'Synthetic Locomotion'.*Start\\[1\\] "
            + (invalidDirection ? "has invalid" : "requires")));
        var moving = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(moving.IsMove, Is.True, "A damaged group must not silently select its remaining valid entry.");
        _animation.SubmitLocomotion(_owner, moving);
        _animation.Evaluate(0.2f);
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.State.Clip, Is.SameAs(stop.Clip));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GroundIdleAlone_IsNotACompleteMoveAndCannotEnterStart(bool twoDimensional)
    {
        AnimationAsset idle = Clip("Idle", 1f), start = Clip("Start", 7f, false);
        string definition = twoDimensional
            ? $"{{\"blendType\":1,\"twoDimensional\":{{\"samples\":[{Sample2D(idle, 0, 0, false)}]}}}}"
            : $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)}]}}}}";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(definition,
            $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]"));
        LogAssert.Expect(LogType.Error, new Regex("Move requires a non-zero movement sample"));
        var request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(request.State, Is.Null);
        Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Move));
        Assert.That(_animation.SubmitLocomotion(_owner, request), Is.False);
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ActionOverrideAndZeroDelta_PreserveTheCurrentBaseAndCrossfade()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset move = Clip("Move", 2f);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(idle, move)));
        _animation.SubmitLocomotion(_owner, runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero)));
        _animation.Evaluate(0.1f);
        var transition = new ClipState(Clip("Base Transition", 3f, false).Clip);
        try
        {
            _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(transition, 0.4f, true));
            _animation.Evaluate(0.1f);
            double time = transition.TimeD;
            float weight = transition.Weight;
            ActorAnimationActionOwner action = _animation.BeginActionOverride();
            _animation.BeginFixedAnimationTick();
            _animation.SubmitActionClipPose(action, Clip("Action", 9f).Clip, 0.25f);
            _animation.Evaluate(0f);
            Assert.That(transition.TimeD, Is.EqualTo(time).Within(1e-5));
            Assert.That(transition.Weight, Is.EqualTo(weight).Within(1e-5));
            Assert.That(_probe.localPosition.x, Is.EqualTo(9f).Within(0.001f));
            _animation.Evaluate(0.1f);
            Assert.That(transition.TimeD, Is.GreaterThan(time));
            _animation.EndActionOverride(action);
            _animation.Evaluate(0f);
            Assert.That(_animancer.Layers[1].Weight, Is.Zero);
            Assert.That(_animancer.Layers[0].CurrentState, Is.SameAs(transition));
            Assert.That(transition.TimeD, Is.GreaterThan(time));
        }
        finally
        {
            _animation.EndLocomotionSession(_owner);
            transition.Destroy();
        }
    }

    [Test]
    public void MissingMoveSamples_AreRejectedAsAWholeAndUseOnlyPoseProtection()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(
            $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)},{{\"threshold\":1}}]}}}}"));
        LogAssert.Expect(LogType.Error, new Regex("Move 1D\\[1\\] AnimationAsset/Clip is missing"));
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        Assert.That(request.State, Is.Null);
        Assert.That(_animation.SubmitLocomotion(_owner, request), Is.False);
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void StaleOwnersAndRuntimeDisposal_CannotClearOrChangeTheProtectedIdle()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset move = Clip("Move", 3f);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(idle, move)));
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        _animation.SubmitLocomotion(_owner, request);
        _animation.Evaluate(0.1f);
        ActorAnimationLocomotionOwner stale = _owner;
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Dispose();
        runtime.Dispose();
        _owner = _animation.BeginLocomotionSession();
        _animation.EndLocomotionSession(stale);
        Assert.That(_animation.IsLocomotionOwnerActive(_owner), Is.True);
        Assert.That(_animation.SubmitLocomotion(stale, request), Is.False);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        _animation.enabled = false;
        Assert.That(_animation.IsLocomotionOwnerActive(_owner), Is.False);
        _animation.enabled = true;
        _owner = _animation.BeginLocomotionSession();
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void AirWithoutIdle_RetainsAnIndependentFrozenBaseAfterRuntimeDisposal()
    {
        AnimationAsset falling = Clip("Fall", 1f);
        AnimationAsset rising = Clip("Rise", 3f);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(
            $"{{\"oneDimensional\":{{\"parameter\":1,\"samples\":[{Sample1D(falling, -1, false)},{Sample1D(rising, 1, false)}]}}}}"));
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero, vertical: 0f));
        _animation.SubmitLocomotion(_owner, request);
        _animation.Evaluate(0.2f);
        float pose = _probe.localPosition.x;
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Dispose();
        _animation.Evaluate(0.5f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(pose).Within(0.001f));
        Assert.That(_animancer.Layers[0].CurrentState, Is.Not.SameAs(request.State));
        Assert.That(_animancer.Layers[0].CurrentState.Speed, Is.Zero);
    }

    [Test]
    public void MotionAnimationPipeline_BindsAuthoredChangesAfterDisableEnable()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f), replacement = Clip("Replacement", 7f);
        var asset = Asset<LocomotionMixerAsset>($"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)},{Sample1D(move, 1, true)}]}}}}");
        _root.AddComponent<Actor>();
        var locomotion = _root.AddComponent<ActorLocomotion>();
        JsonUtility.FromJsonOverwrite($"{{\"locomotionAssets\":[{{\"instanceID\":{asset.GetInstanceID()}}}]}}", locomotion);
        void Tick()
        {
            locomotion.SetLocomotionIntent(new LocomotionIntent { WorldMoveDirection = Vector3.forward, MoveStrength = 1f });
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(new LocomotionMotionContext(0.1f, true, Vector3.up, Quaternion.identity,
                default, new MotionStateSnapshot(1f, 1f, 1f, 1f, false, false, false, false)));
            locomotion.UpdateAnimation(_animation, null, 0.1f);
            _animation.Evaluate(0.1f);
        }
        Tick();
        var original = (LinearMixerState)_animancer.Layers[0].CurrentState;
        locomotion.enabled = false;
        move.EditorSetClip(replacement.Clip);
        locomotion.enabled = true;
        _owner = _animation.BeginLocomotionSession();
        Tick();
        var corrected = (LinearMixerState)_animancer.Layers[0].CurrentState;
        Assert.That(corrected, Is.Not.SameAs(original));
        Assert.That(corrected.GetChild(1).Clip, Is.SameAs(replacement.Clip));
        Assert.That(_probe.localPosition.x, Is.EqualTo(7f).Within(0.001f));
        Assert.That(locomotion.DebugLocomotionVelocity.magnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void SetReentry_RestartsTheCachedTransitionAndRetainsTheProtectedPose()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 2f);
        AnimationAsset start = Clip("Start", 3f, false);
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move),
            $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]"));
        var old = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        _animation.SubmitLocomotion(_owner, old);
        _animation.Evaluate(0.1f);
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Enter(null, null);
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        _owner = _animation.BeginLocomotionSession();
        var current = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(current.State.Clip, Is.SameAs(start.Clip));
        Assert.That(current.State, Is.SameAs(old.State));
        _animation.SubmitLocomotion(_owner, current);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));
    }

    private static LocomotionRuntimeAnimationContext Context(Vector3 input, Vector3 before,
        Quaternion? facing = null, float vertical = 0f, float scale = 1f, Vector3? model = null,
        float dt = 1f / 60f) =>
        LocomotionSetDecisionTests.Context(input, before, model, dt, facing: facing, vertical: vertical, scale: scale);

    private AnimationAsset Clip(string name, float value, bool looping = true)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve("PoseProbe", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, value));
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = looping;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var asset = ScriptableObject.CreateInstance<AnimationAsset>();
        asset.EditorSetClip(clip);
        _objects.Add(clip);
        _objects.Add(asset);
        return asset;
    }

    private T Asset<T>(string move, string entries = "") where T : LocomotionAsset
    {
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = "Synthetic Locomotion";
        JsonUtility.FromJsonOverwrite($"{{\"move\":{move}{entries}}}", asset);
        _objects.Add(asset);
        return asset;
    }

    private T Runtime<T>(LocomotionAsset asset) where T : LocomotionRuntime
    {
        T runtime = (T)asset.CreateRuntime();
        runtime.Enter(null, null);
        _runtimes.Add(runtime);
        return runtime;
    }

    private static string Move1D(AnimationAsset idle, AnimationAsset move) =>
        $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)},{Sample1D(move, 1, false)}]}}}}";
    private static string Ref(AnimationAsset asset) => $"{{\"instanceID\":{asset.GetInstanceID()}}}";
    private static string Sample1D(AnimationAsset asset, int threshold, bool sync) =>
        $"{{\"animation\":{Ref(asset)},\"threshold\":{threshold},\"sync\":{(sync ? "true" : "false")}}}";
    private static string Sample2D(AnimationAsset asset, int x, int y, bool sync) =>
        $"{{\"animation\":{Ref(asset)},\"threshold\":{{\"x\":{x},\"y\":{y}}},\"sync\":{(sync ? "true" : "false")}}}";
}
#endif
