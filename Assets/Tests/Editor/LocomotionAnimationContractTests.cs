#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    public void StartReversal_InterruptsEvenWhenStartCompletesAndConsumesTheEdge()
    {
        var machine = new LocomotionSetStateMachine();
        Step(machine, Context(Vector3.forward, Vector3.zero), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
        Step(machine, Context(Vector3.back, Vector3.forward, dt: 0f), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Start));
        Step(machine, Context(Vector3.back, Vector3.forward), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Pivot));
        Step(machine, Context(Vector3.back, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Pivot));
        Step(machine, Context(Vector3.back, Vector3.forward), true);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
        Step(machine, Context(Vector3.back, Vector3.forward), false);
        Assert.That(machine.State, Is.EqualTo(LocomotionSetState.Move));
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

    [TestCase(0.19f, LocomotionSetState.Start)]
    [TestCase(0.2f, LocomotionSetState.Start)]
    [TestCase(0.21f, LocomotionSetState.Move)]
    public void ConfiguredStationarySpeed_ControlsStartAndRelease(float speed, LocomotionSetState expected)
    {
        var config = LocomotionTransitionDecisionConfig.Default;
        config.StationarySpeed = 0.2f;
        var machine = new LocomotionSetStateMachine(config);
        Step(machine, Context(Vector3.forward, Vector3.forward * speed), false);
        Assert.That(machine.State, Is.EqualTo(expected));
        Step(machine, Context(Vector3.zero, Vector3.forward * speed), false);
        Assert.That(machine.State, Is.EqualTo(speed > 0.2f ? LocomotionSetState.Stop : LocomotionSetState.Move));
    }

    [TestCase(0.99f, 100f, false)]
    [TestCase(1f, 89f, false)]
    [TestCase(1f, 90f, true)]
    [TestCase(1.01f, 91f, true)]
    public void ConfiguredPivotThresholds_UseSpeedAndAngleBoundaries(float speed, float angle, bool pivots)
    {
        var config = LocomotionTransitionDecisionConfig.Default;
        config.PivotMinimumSpeed = 1f;
        config.PivotMinimumAngleDegrees = 90f;
        var machine = new LocomotionSetStateMachine(config);
        Step(machine, Context(Vector3.forward, Vector3.forward * speed), false);
        Vector3 input = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
        Step(machine, Context(input, Vector3.forward * speed), false);
        Assert.That(machine.State, Is.EqualTo(pivots ? LocomotionSetState.Pivot : LocomotionSetState.Move));
    }

    [Test]
    public void DecisionConfiguration_DefaultsRemainCompatibleWithExistingAssets()
    {
        var asset = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"transitionBlendDuration\":0.2}", asset);
            Assert.That(asset.TransitionDecisionConfig.StationarySpeed, Is.EqualTo(0.1f));
            Assert.That(asset.TransitionDecisionConfig.PivotMinimumSpeed, Is.EqualTo(0.5f));
            Assert.That(asset.TransitionDecisionConfig.PivotMinimumAngleDegrees, Is.EqualTo(120f));
            JsonUtility.FromJsonOverwrite("{\"transitionDecisionConfig\":{\"PivotMinimumAngleDegrees\":181}}", asset);
            Assert.That(asset.HasValidRuntimeConfig, Is.False);
        }
        finally { Object.DestroyImmediate(asset); }
    }

    [Test]
    public void DecisionConfiguration_RejectsNonFiniteOrOutOfRangeThresholds()
    {
        foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            var config = LocomotionTransitionDecisionConfig.Default; config.StationarySpeed = bad;
            Assert.That(config.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => new LocomotionSetStateMachine(config));
            config = LocomotionTransitionDecisionConfig.Default; config.PivotMinimumSpeed = bad;
            Assert.That(config.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => new LocomotionSetStateMachine(config));
        }
        foreach (float bad in new[] { -1f, 181f, float.NaN, float.PositiveInfinity })
        {
            var config = LocomotionTransitionDecisionConfig.Default; config.PivotMinimumAngleDegrees = bad;
            Assert.That(config.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => new LocomotionSetStateMachine(config));
        }
        var limits = new LocomotionTransitionDecisionConfig { PivotMinimumAngleDegrees = 180f };
        Assert.That(limits.IsValid, Is.True);
    }

    private static void Step(LocomotionSetStateMachine machine,
        in LocomotionRuntimeAnimationContext context, bool completed) =>
        machine.CommitState(machine.DecideNextState(context, completed));

    internal static LocomotionRuntimeAnimationContext Context(Vector3 input, Vector3 before,
        Vector3? model = null, float dt = 1f / 60f, int action = 0,
        Quaternion? facing = null, float vertical = 0f, float scale = 1f, float strength = 1f,
        LocomotionFootPhaseReference sourceFootPhase = default)
    {
        var policy = new MotionStateSnapshot(scale, 1f, 1f, 1f, false, false, false, false);
        var motor = new LocomotionMotionContext(dt, true, Vector3.up,
            facing ?? Quaternion.identity, default, policy);
        return new LocomotionRuntimeAnimationContext(
            new LocomotionIntent { WorldMoveDirection = input, MoveStrength = input == Vector3.zero ? 0f : strength },
            input != Vector3.zero, before, model ?? before, motor, vertical, action, dt, sourceFootPhase);
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
    private bool _dynamicAnimationWarning;

    [SetUp]
    public void SetUp()
    {
        _dynamicAnimationWarning = OptionalWarning.DynamicAnimation.IsEnabled();
        OptionalWarning.DynamicAnimation.Disable(); // These fixtures intentionally create Editor-only clips.
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
        OptionalWarning.DynamicAnimation.SetEnabled(_dynamicAnimationWarning);
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
    public void InputStrength_UsesOnlyIntentThroughReversalAndRelease()
    {
        AnimationAsset idle = Clip("Idle", 1f), run = Clip("Run", 3f);
        var asset = Asset<LocomotionSetAsset>(
            "{\"oneDimensional\":{\"parameter\":2,\"samples\":["
            + Sample1D(idle, 0, false) + "," + Sample1D(run, 1, false) + "]}}");
        var runtime = Runtime<LocomotionSetRuntime>(asset);

        var starting = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero, model: Vector3.zero));
        Assert.That(starting.Parameter.x, Is.EqualTo(1f));
        Assert.That(_animation.SubmitLocomotion(_owner, starting), Is.True);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));

        var reversing = runtime.UpdateAnimation(Context(Vector3.back, Vector3.forward * 5f,
            model: Vector3.zero));
        Assert.That(reversing.Parameter.x, Is.EqualTo(1f));
        Assert.That(_animation.SubmitLocomotion(_owner, reversing), Is.True);
        _animation.Evaluate(0.1f);
        Assert.That(reversing.State.GetChild(0).Weight, Is.Zero);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));

        var slowing = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.back * 5f,
            model: Vector3.back * 2.5f));
        Assert.That(slowing.Parameter.x, Is.Zero);
        Assert.That(_animation.SubmitLocomotion(_owner, slowing), Is.True);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        var stopped = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero, model: Vector3.zero));
        Assert.That(stopped.Parameter.x, Is.Zero);
        foreach (float strength in new[] { 0f, 0.005f, 0.4f })
        {
            var partial = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.back * 5f,
                model: Vector3.back * 2.5f, scale: 2f, strength: strength));
            Assert.That(partial.Parameter.x, Is.EqualTo(strength).Within(0.00001f),
                "Move input parameters must not inherit the transition detector's input threshold or residual speed.");
        }
    }

    [Test]
    public void LocalInput_UsesFacingRelativeIntentInsteadOfVelocity()
    {
        AnimationAsset idle = Clip("Idle", 1f), forward = Clip("Forward", 3f), back = Clip("Back", 5f);
        var asset = Asset<LocomotionMixerAsset>(
            "{\"blendType\":1,\"twoDimensional\":{\"parameter\":1,\"samples\":["
            + Sample2D(idle, 0, 0, false) + "," + Sample2D(forward, 0, 1, false)
            + "," + Sample2D(back, 0, -1, false) + "]}}");
        var runtime = Runtime<LocomotionMixerRuntime>(asset);
        Quaternion facing = Quaternion.Euler(0f, 90f, 0f);

        var right = runtime.UpdateAnimation(Context(Vector3.right, Vector3.zero,
            facing: facing, model: Vector3.zero));
        Assert.That(right.Parameter.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(right.Parameter.y, Is.EqualTo(1f).Within(0.001f));
        Assert.That(_animation.SubmitLocomotion(_owner, right), Is.True);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));

        var left = runtime.UpdateAnimation(Context(Vector3.left, Vector3.right * 5f,
            facing: facing, model: Vector3.zero));
        Assert.That(left.Parameter.y, Is.EqualTo(-1f).Within(0.001f));
        Assert.That(_animation.SubmitLocomotion(_owner, left), Is.True);
        _animation.Evaluate(0.1f);
        // The contract here is the local input parameter; directional weights belong to Animancer.

        var released = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.right * 5f,
            facing: facing, model: Vector3.right * 2.5f));
        Assert.That(released.Parameter, Is.EqualTo(Vector2.zero));
        foreach (float strength in new[] { 0f, 0.005f, 0.4f })
        {
            var partial = runtime.UpdateAnimation(Context(Vector3.right, Vector3.left * 5f,
                facing: facing, model: Vector3.left * 2.5f, scale: 2f, strength: strength));
            Assert.That(partial.Parameter.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(partial.Parameter.y, Is.EqualTo(strength).Within(0.00001f));
        }
    }

    [Test]
    public void InputModes_PlayAuthoredThresholdsOutsideTheUnitRange()
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        var oneDimensional = Asset<LocomotionMixerAsset>(
            "{\"oneDimensional\":{\"parameter\":2,\"samples\":["
            + Sample1D(idle, 0, false) + "," + Sample1D(move, 2, false) + "]}}");
        var twoDimensional = Asset<LocomotionMixerAsset>(
            "{\"blendType\":1,\"twoDimensional\":{\"parameter\":1,\"samples\":["
            + Sample2D(idle, 0, 0, false) + "," + Sample2D(move, 0, 2, false) + "]}}");
        foreach (var asset in new[] { oneDimensional, twoDimensional })
        {
            var runtime = Runtime<LocomotionMixerRuntime>(asset);
            var request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
            Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
            _animation.Evaluate(0.2f);
            Assert.That(_probe.localPosition.x, Is.EqualTo(2f).Within(0.001f));
        }
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void NonLoopingMoveSample_RestartsOnReactivationButNotDuringContinuousInput()
    {
        AnimationAsset idle = Clip("Idle", 1f);
        AnimationAsset run = Clip("NonLooping Run", 3f, looping: false);
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(idle, run)));
        LocomotionAnimationRequest request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        _animation.SubmitLocomotion(_owner, request);
        _animation.Evaluate(0f); // Flush initial playable creation before measuring elapsed time.
        _animation.Evaluate(1.2f);
        var mixer = (ManualMixerState)request.State;
        AnimancerState runState = mixer.GetChild(1);
        Assert.That(runState.RawTime, Is.GreaterThanOrEqualTo(runState.Length));
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
        _animation.Evaluate(0f); // Flush initial playable creation before measuring elapsed time.
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
    public void StartReversal_UsesPivotWhenAvailableOtherwiseReturnsToMove(bool hasPivot)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f);
        AnimationAsset start = Clip("Start", 7f, false), pivot = Clip("Pivot", 9f, false);
        string entries = $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]";
        if (hasPivot)
            entries += $",\"pivot\":[{{\"animation\":{Ref(pivot)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}},\"targetLocalDirection\":{{\"x\":0,\"y\":-1}}}}]";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move), entries));
        var starting = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(starting.State.Clip, Is.SameAs(start.Clip));
        Assert.That(_animation.SubmitLocomotion(_owner, starting), Is.True);
        _animation.Evaluate(0.1f);

        Assert.That(runtime.UpdateAnimation(Context(Vector3.back, Vector3.forward, dt: 0f)).State, Is.Null);
        Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Start));
        var reversing = runtime.UpdateAnimation(Context(Vector3.back, Vector3.forward));
        Assert.That(runtime.AnimationState, Is.EqualTo(hasPivot ? LocomotionSetState.Pivot : LocomotionSetState.Move));
        Assert.That(reversing.IsMove, Is.EqualTo(!hasPivot));
        if (hasPivot) Assert.That(reversing.State.Clip, Is.SameAs(pivot.Clip));
        Assert.That(_animation.SubmitLocomotion(_owner, reversing), Is.True);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(hasPivot ? 9f : 3f).Within(0.001f));

        var continuing = runtime.UpdateAnimation(Context(Vector3.back, Vector3.forward));
        Assert.That(runtime.AnimationState, Is.EqualTo(hasPivot ? LocomotionSetState.Pivot : LocomotionSetState.Move));
        Assert.That(continuing.State, Is.SameAs(reversing.State));
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

    [TestCase(1)]
    [TestCase(10)]
    public void HorizontalSpeed_TracksPolicyVelocityWithoutSampleDependentFiltering(int maximumThreshold)
    {
        AnimationAsset idle = Clip("Idle", 1f), run = Clip("Run", 3f);
        var asset = Asset<LocomotionMixerAsset>(
            $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)},"
            + $"{Sample1D(run, maximumThreshold, false)}]}}}}");
        var runtime = Runtime<LocomotionMixerRuntime>(asset);

        foreach (float modelSpeed in new[] { 0f, 7f, 0.25f, 0f })
        {
            var request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.back * 5f,
                model: Vector3.back * modelSpeed, scale: 0.5f));
            float expectedSpeed = modelSpeed * 0.5f;
            Assert.That(request.Parameter.x, Is.EqualTo(expectedSpeed).Within(0.00001f),
                "Sample thresholds determine blend weights, not the speed parameter or its response time.");
            Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
            _animation.Evaluate(1f / 60f);
            float expectedRunWeight = Mathf.Clamp01(expectedSpeed / maximumThreshold);
            Assert.That(request.State.GetChild(1).Weight, Is.EqualTo(expectedRunWeight).Within(0.001f));
            Assert.That(_probe.localPosition.x, Is.EqualTo(1f + 2f * expectedRunWeight).Within(0.001f));
        }
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void HorizontalSpeed_FreezesOnZeroDeltaAndReadsCurrentVelocityOnResumeAndReentry()
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
        var resumed = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward * 0.25f));
        Assert.That(resumed.Parameter.x, Is.EqualTo(0.25f), "Resume directly from the current model velocity.");
        _animation.EndLocomotionSession(_owner);
        runtime.Exit(null, null);
        runtime.Enter(null, null);
        _owner = _animation.BeginLocomotionSession();
        var reentered = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.zero));
        Assert.That(reentered.Parameter.x, Is.Zero, "A new session reads its current model velocity.");
        _animation.SubmitLocomotion(_owner, reentered);
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StopSelection_UsesCurrentFootPhaseAmongEqualDirections(bool releasingStart)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f), start = Clip("Start", 4f, false);
        AnimationAsset left = Clip("Left Stop", 5f, false), right = Clip("Right Stop", 6f, false);
        move.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0f, AnimationFoot.Right) });
        start.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0f, AnimationFoot.Right) });
        left.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0f, AnimationFoot.Left) });
        right.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0f, AnimationFoot.Right) });
        string entries = $",\"stop\":[{{\"animation\":{Ref(left)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}},"
            + $"{{\"animation\":{Ref(right)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]";
        if (releasingStart) entries += $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, move), entries));
        var playing = runtime.UpdateAnimation(Context(Vector3.forward, releasingStart ? Vector3.zero : Vector3.forward));
        _animation.SubmitLocomotion(_owner, playing);
        _animation.Evaluate(0.1f);
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.State.Clip, Is.SameAs(right.Clip));
    }

    [TestCase(LocomotionStopPlaybackMode.Time)]
    [TestCase(LocomotionStopPlaybackMode.Distance)]
    public void StopWithoutTrajectory_CompletesIntoCurrentModelMove(LocomotionStopPlaybackMode mode)
    {
        AnimationAsset idle = Clip("Idle", 1f), move = Clip("Move", 3f), stop = Clip("Stop", 7f, false);
        var asset = Asset<LocomotionSetAsset>(Move1D(idle, move),
            $",\"stop\":[{{\"animation\":{Ref(stop)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]");
        JsonUtility.FromJsonOverwrite($"{{\"stopPlaybackMode\":{(int)mode}}}", asset);
        var runtime = Runtime<LocomotionSetRuntime>(asset);
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.SampleTime, Is.Null);
        Assert.That(stopping.State.Clip, Is.SameAs(stop.Clip));
        _animation.SubmitLocomotion(_owner, stopping);
        _animation.Evaluate(0f); // Flush initial playable creation before measuring elapsed time.
        _animation.Evaluate(1.1f);
        Assert.That(stopping.State.RawTime, Is.GreaterThanOrEqualTo(stop.Clip.length));
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
        _animation.Evaluate(0f); // Flush initial playable creation before measuring elapsed time.
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
    public void IncompleteStartGroup_KeepsUsableClipsAndValidStop(bool invalidDirection)
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
        var moving = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(moving.State.Clip, Is.SameAs(start.Clip));
        _animation.SubmitLocomotion(_owner, moving);
        _animation.Evaluate(0.2f);
        runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.State.Clip, Is.SameAs(stop.Clip));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GroundIdleAlone_PlaysAndAllowsStart(bool twoDimensional)
    {
        AnimationAsset idle = Clip("Idle", 1f), start = Clip("Start", 7f, false);
        string definition = twoDimensional
            ? $"{{\"blendType\":1,\"twoDimensional\":{{\"samples\":[{Sample2D(idle, 0, 0, false)}]}}}}"
            : $"{{\"oneDimensional\":{{\"samples\":[{Sample1D(idle, 0, false)}]}}}}";
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(definition,
            $",\"start\":[{{\"animation\":{Ref(start)},\"targetLocalDirection\":{{\"x\":0,\"y\":1}}}}]"));
        var request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.zero));
        Assert.That(request.State.Clip, Is.SameAs(start.Clip));
        Assert.That(runtime.AnimationState, Is.EqualTo(LocomotionSetState.Start));
        Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
        _animation.Evaluate(0f); // Flush initial playable creation before measuring elapsed time.
        _animation.Evaluate(1.1f);
        var move = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        Assert.That(move.State.ChildCount, Is.EqualTo(1));
        Assert.That(_animation.SubmitLocomotion(_owner, move), Is.True);
        _animation.Evaluate(0.2f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(1f).Within(0.001f));
        LogAssert.NoUnexpectedReceived();
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

    [TestCase(false)]
    [TestCase(true)]
    public void IncompleteMove_BindsUsableSamplesWithoutRequiringIdle(bool twoDimensional)
    {
        AnimationAsset move = Clip("Move", 3f), duplicate = Clip("Duplicate", 7f);
        string definition = twoDimensional
            ? $"{{\"blendType\":1,\"twoDimensional\":{{\"samples\":[{{\"threshold\":{{\"x\":0,\"y\":0}}}},"
                + $"{Sample2D(move, 0, 1, false)},{Sample2D(duplicate, 0, 1, false)}]}}}}"
            : $"{{\"oneDimensional\":{{\"samples\":[{{\"threshold\":0}},"
                + $"{Sample1D(move, 1, false)},{Sample1D(duplicate, 1, false)}]}}}}";
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(definition));
        var request = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        Assert.That(request.State.ChildCount, Is.EqualTo(1));
        Assert.That(request.State.GetChild(0).Clip, Is.SameAs(move.Clip));
        Assert.That(_animation.SubmitLocomotion(_owner, request), Is.True);
        _animation.Evaluate(0.1f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void StaleOwnersAndRuntimeDisposal_CannotClearOrChangeTheProtectedPose()
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
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));
        _animation.enabled = false;
        Assert.That(_animation.IsLocomotionOwnerActive(_owner), Is.False);
        _animation.enabled = true;
        _owner = _animation.BeginLocomotionSession();
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void AirAfterGroundIdle_ProtectsItsOwnLastPose()
    {
        var ground = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(
            Move1D(Clip("Ground Idle", 1f), Clip("Ground Run", 3f))));
        _animation.SubmitLocomotion(_owner, ground.UpdateAnimation(Context(Vector3.zero, Vector3.zero)));
        _animation.Evaluate(0.1f);
        _animation.EndLocomotionSession(_owner); ground.Exit(null, null); ground.Dispose();
        _owner = _animation.BeginLocomotionSession();
        var air = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(
            $"{{\"oneDimensional\":{{\"parameter\":1,\"samples\":[{Sample1D(Clip("Air", 5f), 1, false)}]}}}}"));
        _animation.SubmitLocomotion(_owner, air.UpdateAnimation(Context(Vector3.zero, Vector3.zero, vertical: 1f)));
        _animation.Evaluate(0.2f);
        _animation.EndLocomotionSession(_owner); air.Exit(null, null); air.Dispose();
        _animation.Evaluate(0.5f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(5f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PoseProtection_FreezesTheEvaluatedCrossfadeAndKeepsActionSeparate(bool invalidRequest)
    {
        var run = Clip("Changing Run", 2f);
        run.Clip.SetCurve("PoseProbe", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 1f, 1f, 5f));
        var runtime = Runtime<LocomotionMixerRuntime>(Asset<LocomotionMixerAsset>(Move1D(Clip("Idle", 0f), run)));
        var move = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward));
        _animation.SubmitLocomotion(_owner, move); _animation.Evaluate(0.1f);
        double observedTime = move.State.GetChild(1).TimeD;
        var transition = new ClipState(Clip("Pivot", 7f, false).Clip);
        try
        {
            _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(transition, 0.5f, true));
            _animation.Evaluate(0.1f);
            Assert.That(move.State.Weight, Is.GreaterThan(0f));
            Assert.That(transition.Weight, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(move.State.GetChild(1).RawTime, Is.GreaterThan(observedTime));
            float evaluatedPose = _probe.localPosition.x;
            var action = _animation.BeginActionOverride();
            _animation.SubmitActionClipPose(action, Clip("Action", 9f).Clip, 0.25f);
            if (invalidRequest)
                Assert.That(_animation.SubmitLocomotion(_owner, default), Is.False);
            else
                _animation.EndLocomotionSession(_owner);
            runtime.Exit(null, null); runtime.Dispose(); transition.Destroy();
            _animation.Evaluate(0.3f);
            Assert.That(_probe.localPosition.x, Is.EqualTo(9f).Within(0.001f));
            _animation.EndActionOverride(action); _animation.Evaluate(0f);
            Assert.That(_probe.localPosition.x, Is.EqualTo(evaluatedPose).Within(0.001f));
            _animation.Evaluate(0.3f);
            Assert.That(_probe.localPosition.x, Is.EqualTo(evaluatedPose).Within(0.001f));
        }
        finally { LocomotionAnimationUtility.Destroy(transition); }
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
    public void MotionAnimationPipeline_RebindsAuthoredChangesAfterSimulationCancel()
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
        var originalClip = move.Clip;
        move.EditorSetClip(replacement.Clip);
        var changedConfig = asset.MovementConfig;
        changedConfig.MaxSpeed = 8f;
        JsonUtility.FromJsonOverwrite("{\"movementConfig\":" + JsonUtility.ToJson(changedConfig) + "}", asset);
        Tick();
        Assert.That(original.GetChild(1).Clip, Is.SameAs(originalClip));
        Assert.That(locomotion.CurrentMovementConfig.MaxSpeed, Is.EqualTo(5f));
        locomotion.CancelSimulation();
        _owner = _animation.BeginLocomotionSession();
        Tick();
        var corrected = (LinearMixerState)_animancer.Layers[0].CurrentState;
        Assert.That(corrected, Is.Not.SameAs(original));
        Assert.That(corrected.GetChild(1).Clip, Is.SameAs(replacement.Clip));
        Assert.That(locomotion.CurrentMovementConfig.MaxSpeed, Is.EqualTo(8f));
        Assert.That(_probe.localPosition.x, Is.EqualTo(7f).Within(0.001f));
        Assert.That(locomotion.DebugLocomotionVelocity.magnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void MotionAnimationPipeline_SessionLossResetsOnceAndRejectsStaleNotifications()
    {
        _root.AddComponent<Actor>();
        var locomotion = _root.AddComponent<ActorLocomotion>();
        var asset = ScriptableObject.CreateInstance<TrackingLocomotionAsset>();
        _objects.Add(asset);
        LocomotionContractTests.ConfigureAssets(locomotion, asset);
        void Tick()
        {
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(new LocomotionMotionContext(0.1f, true, Vector3.up,
                Quaternion.identity, default, default));
            locomotion.UpdateAnimation(_animation, null, 0.1f);
        }
        Tick();
        Assert.That(asset.EnterCount, Is.EqualTo(1));
        Assert.That(asset.AnimationResetCount, Is.Zero, "The first playback session must not reset an entered Runtime.");
        locomotion.OnAnimationSessionLost(_animation, _owner);
        locomotion.OnAnimationSessionLost(null, _owner);
        Assert.That(asset.AnimationResetCount, Is.Zero, "Old owners and other components cannot reset the current session.");
        _animation.enabled = false;
        Assert.That(asset.AnimationResetCount, Is.EqualTo(1));
        Tick();
        Assert.That(asset.AnimationResetCount, Is.EqualTo(1));
        _animation.enabled = true;
        Tick();
        Assert.That(asset.AnimationResetCount, Is.EqualTo(1));
        _animancer.Graph.Destroy();
        _animation.Evaluate(0f); // Graph replacement independently notifies the motion owner.
        Assert.That(asset.AnimationResetCount, Is.EqualTo(2));
        Tick();
        Assert.That(asset.AnimationResetCount, Is.EqualTo(2));
        JsonUtility.FromJsonOverwrite("{\"locomotionAssets\":[]}", locomotion);
        Tick();
        Assert.That(asset.ExitCount, Is.EqualTo(1));
        Assert.That(asset.AnimationResetCount, Is.EqualTo(3), "Mode exit resets through Runtime.Exit once.");
        locomotion.CancelSimulation();
        Assert.That(asset.AnimationResetCount, Is.EqualTo(3));
        Assert.That(asset.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void MotionAnimationPipeline_UsesLockedMotionSnapshotAndSubmitsOnceAfterZeroDelta()
    {
        _root.AddComponent<Actor>();
        var locomotion = _root.AddComponent<ActorLocomotion>();
        var asset = ScriptableObject.CreateInstance<TrackingLocomotionAsset>();
        _objects.Add(asset);
        LocomotionContractTests.ConfigureAssets(locomotion, asset);
        var motor = new LocomotionMotionContext(0.1f, true, Vector3.up, Quaternion.identity, default, default);
        void Build()
        {
            locomotion.SetLocomotionIntent(new LocomotionIntent { WorldMoveDirection = Vector3.forward, MoveStrength = 1f });
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(motor);
        }
        Build();
        locomotion.UpdateAnimation(_animation, null, 0f);
        Assert.That(asset.AnimationUpdateCount, Is.Zero);
        locomotion.SetLocomotionIntent(new LocomotionIntent { WorldMoveDirection = Vector3.right, MoveStrength = 1f });
        locomotion.BuildMotionRequest(new LocomotionMotionContext(1f, false, Vector3.up,
            Quaternion.Euler(0f, 90f, 0f), default, default));
        locomotion.UpdateAnimation(_animation, null, 0.1f);
        locomotion.UpdateAnimation(_animation, null, 0.1f);
        Assert.That(asset.AnimationUpdateCount, Is.EqualTo(1));
        Assert.That(asset.LastAnimationContext.VelocityBeforeMotion, Is.EqualTo(Vector3.zero));
        Assert.That(asset.LastAnimationContext.ModelVelocity.z, Is.EqualTo(2f).Within(1e-5f));
        Assert.That(asset.LastAnimationContext.Intent.WorldMoveDirection, Is.EqualTo(Vector3.forward));
        Assert.That(asset.LastAnimationContext.Motor.CurrentWorldRotation, Is.EqualTo(Quaternion.identity));
        Build();
        locomotion.UpdateAnimation(_animation, null, 0.1f);
        Assert.That(asset.AnimationUpdateCount, Is.EqualTo(2));
        Assert.That(asset.LastAnimationContext.VelocityBeforeMotion.z, Is.EqualTo(2f).Within(1e-5f));
        Assert.That(asset.LastAnimationContext.ModelVelocity.z, Is.EqualTo(4f).Within(1e-5f));
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

    [TestCase(false)]
    [TestCase(true)]
    public void StopDuringTransitionBlend_UsesTheDominantEvaluatedPose(bool returningFromStart)
    {
        AnimationAsset idle = Clip("Idle", 1f), run = FootClip("Run", 2f, AnimationFoot.Left);
        AnimationAsset transition = FootClip("Transition", 4f, AnimationFoot.Right, false);
        AnimationAsset left = FootClip("Left Stop", 5f, AnimationFoot.Left, false);
        AnimationAsset right = FootClip("Right Stop", 6f, AnimationFoot.Right, false);
        string entries = StopEntries(left, right) + (returningFromStart
            ? $",\"start\":[{{\"animation\":{Ref(transition)}}}]"
            : $",\"pivot\":[{{\"animation\":{Ref(transition)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}},\"targetLocalDirection\":{{\"x\":0,\"y\":-1}}}}]");
        var runtime = Runtime<LocomotionSetRuntime>(Asset<LocomotionSetAsset>(Move1D(idle, run), entries));
        var old = runtime.UpdateAnimation(Context(Vector3.forward, returningFromStart ? Vector3.zero : Vector3.forward));
        _animation.SubmitLocomotion(_owner, old);
        _animation.Evaluate(returningFromStart ? 1.1f : 0.1f);
        var incoming = runtime.UpdateAnimation(Context(returningFromStart ? Vector3.forward : Vector3.back, Vector3.forward));
        _animation.SubmitLocomotion(_owner, incoming);
        _animation.Evaluate(0f);
        old.State.CancelFade(); incoming.State.CancelFade();
        old.State.Weight = 0.8f; incoming.State.Weight = 0.2f;
        _animation.Evaluate(0f);
        Assert.That(_probe.localPosition.x, Is.EqualTo(returningFromStart ? 3.6f : 2.4f).Within(0.001f));
        var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
        Assert.That(stopping.State.Clip, Is.SameAs(returningFromStart ? right.Clip : left.Clip));
    }

    [Test]
    public void DominantFootPhase_MultipliesNestedMixerAndLayerWeights()
    {
        var left = FootClip("Left", 1f, AnimationFoot.Left);
        var right = FootClip("Right", 2f, AnimationFoot.Right);
        var rival = FootClip("Rival", 3f, AnimationFoot.Left);
        var nested = new ManualMixerState();
        var a = nested.Add(left.Clip); var b = nested.Add(right.Clip);
        nested.DontSynchronizeChildren(); a.Weight = 0.9f; b.Weight = 0.1f;
        var mixer = new ManualMixerState(); mixer.Add(nested);
        var c = mixer.Add(right.Clip); mixer.DontSynchronizeChildren();
        nested.Weight = 0.5f; c.Weight = 0.5f;
        var bindings = new[] { Binding(a, left), Binding(b, right), Binding(c, right) };
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(mixer, footPhaseBindings: bindings));
        var rivalState = new ClipState(rival.Clip);
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(rivalState, 1f,
            footPhaseBindings: new[] { Binding(rivalState, rival) }));
        _animation.Evaluate(0f);
        mixer.CancelFade(); rivalState.CancelFade();
        mixer.Weight = 0.8f; rivalState.Weight = 0.2f; _animancer.Layers[0].Weight = 0.5f;
        _animation.Evaluate(0f);
        var phase = _animation.GetLocomotionFootPhaseReference(_owner);
        Assert.That(phase.IsKnown, Is.True);
        Assert.That(phase.Phase, Is.EqualTo(0.5f), "Right contributes .5*.8*.5=.2; nested Left contributes .5*.8*.5*.9=.18.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DominantFootPhase_MissingMetadataAndTiesHaveDeterministicResults(bool equalWeights)
    {
        var idle = Clip("Idle", 1f);
        var run = FootClip("Run", 2f, AnimationFoot.Right);
        var mixer = new ManualMixerState(); var a = mixer.Add(idle.Clip); var b = mixer.Add(run.Clip);
        mixer.DontSynchronizeChildren(); a.Weight = equalWeights ? 0.5f : 0.8f; b.Weight = 1f - a.Weight;
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(mixer,
            footPhaseBindings: new[] { Binding(a, idle), Binding(b, run) }));
        _animation.Evaluate(0f);
        Assert.That(_animation.GetLocomotionFootPhaseReference(_owner).IsKnown, Is.False);
        var leftTrack = new AnimationFootPhaseTrack(new[] { new AnimationFootMarker(0f, AnimationFoot.Left) }, 1f, true);
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(mixer,
            footPhaseBindings: new[] { new LocomotionFootPhaseBinding(a, leftTrack), Binding(b, run) }));
        var known = _animation.GetLocomotionFootPhaseReference(_owner);
        Assert.That(known.IsKnown, Is.True);
        Assert.That(known.Phase, Is.Zero, "Equal contributions retain the first graph leaf.");
    }

    [Test]
    public void FootPhaseBindings_EndSessionAndGraphDestructionReturnUnknown()
    {
        var run = FootClip("Run", 2f, AnimationFoot.Right);
        var state = new ClipState(run.Clip);
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(state,
            footPhaseBindings: new[] { Binding(state, run) }));
        _animation.Evaluate(0f);
        Assert.That(_animation.GetLocomotionFootPhaseReference(_owner).IsKnown, Is.True);
        var previous = _owner;
        _animation.EndLocomotionSession(_owner);
        _owner = _animation.BeginLocomotionSession();
        Assert.That(_animation.GetLocomotionFootPhaseReference(previous).IsKnown, Is.False);
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(state));
        _animation.Evaluate(0f);
        Assert.That(_animation.GetLocomotionFootPhaseReference(_owner).IsKnown, Is.False);
        _animation.SubmitLocomotion(_owner, new LocomotionAnimationRequest(state,
            footPhaseBindings: new[] { Binding(state, run) }));
        _animancer.Graph.Destroy();
        Assert.That(_animation.GetLocomotionFootPhaseReference(_owner).IsKnown, Is.False);
    }

    [Test]
    public void RuntimeSnapshot_AssetEditsReentryAndGraphRebuildKeepBoundExecutionData()
    {
        var idle = Clip("Idle", 1f); var run = FootClip("Run", 2f, AnimationFoot.Left);
        var stop = FootClip("Stop", 5f, AnimationFoot.Left, false); var replacement = Clip("Replacement", 7f);
        string definition = Move1D(idle, run).Replace("\"oneDimensional\":{", "\"oneDimensional\":{\"parameter\":2,");
        var set = Asset<LocomotionSetAsset>(definition, StopEntries(stop, stop)
            + ",\"movementConfig\":{\"MaxSpeed\":1,\"Acceleration\":100,\"Deceleration\":10}");
        var runtime = Runtime<LocomotionSetRuntime>(set);
        var originalRunClip = run.Clip; var originalStopClip = stop.Clip;
        run.EditorSetClip(replacement.Clip); stop.EditorSetClip(replacement.Clip);
        JsonUtility.FromJsonOverwrite("{\"movementConfig\":{\"MaxSpeed\":3},\"transitionBlendDuration\":0.5,\"stop\":[],\"transitionDecisionConfig\":{\"StationarySpeed\":0.8},\"move\":{\"oneDimensional\":{\"parameter\":0,\"samples\":[]}}}", set);
        void Verify()
        {
            runtime.ResetAnimation();
            var move = runtime.UpdateAnimation(Context(Vector3.forward, Vector3.forward * 0.3f, strength: 0.7f));
            Assert.That(move.IsMove, Is.True);
            Assert.That(move.Parameter.x, Is.EqualTo(0.7f));
            Assert.That(move.BlendDuration, Is.EqualTo(0.1f));
            Assert.That(move.State.GetChild(1).Clip, Is.SameAs(originalRunClip));
            _animation.SubmitLocomotion(_owner, move); _animation.Evaluate(0f);
            var source = _animation.GetLocomotionFootPhaseReference(_owner);
            Assert.That(source.IsKnown, Is.True);
            Assert.That(source.Phase, Is.Zero);
            var stopping = runtime.UpdateAnimation(Context(Vector3.zero, Vector3.forward));
            Assert.That(stopping.State.Clip, Is.SameAs(originalStopClip));
            Assert.That(runtime.MovementConfig.MaxSpeed, Is.EqualTo(1f));
            var runner = new LocomotionRunner(); runner.Initialize(Quaternion.identity);
            runtime.UpdateMotion(new LocomotionRuntimeMotionContext(runner,
                new LocomotionIntent { WorldMoveDirection=Vector3.forward, MoveStrength=1f }, true,
                new LocomotionMotionContext(0.1f,true,Vector3.up,Quaternion.identity,default,default)));
            Assert.That(runner.CachedVelocity.magnitude, Is.EqualTo(1f).Within(0.001f));
        }
        Verify();
        runtime.Exit(null,null); runtime.Enter(null,null); Verify();
        _animancer.Graph.Destroy(); _owner = _animation.BeginLocomotionSession(); Verify();
        var fresh = Runtime<LocomotionSetRuntime>(set);
        Assert.That(fresh.MovementConfig.MaxSpeed, Is.EqualTo(3f));
        Assert.That(fresh.UpdateAnimation(Context(Vector3.forward,Vector3.forward)).State, Is.Null);
    }

    private AnimationAsset FootClip(string name, float pose, AnimationFoot foot, bool looping = true)
    {
        var asset = Clip(name, pose, looping);
        asset.EditorOverrideFootMarkers(true, new[] { new AnimationFootMarker(0f, foot) });
        return asset;
    }

    private static LocomotionFootPhaseBinding Binding(AnimancerState state, AnimationAsset asset) =>
        new LocomotionFootPhaseBinding(state, asset.CreateFootPhaseTrack());

    private static string StopEntries(AnimationAsset left, AnimationAsset right) =>
        $",\"stop\":[{{\"animation\":{Ref(left)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}},"
            + $"{{\"animation\":{Ref(right)},\"sourceLocalDirection\":{{\"x\":0,\"y\":1}}}}]";

    private LocomotionRuntimeAnimationContext Context(Vector3 input, Vector3 before,
        Quaternion? facing = null, float vertical = 0f, float scale = 1f, Vector3? model = null,
        float dt = 1f / 60f, float strength = 1f) =>
        LocomotionSetDecisionTests.Context(input, before, model, dt, facing: facing, vertical: vertical, scale: scale, strength: strength,
            sourceFootPhase: _animation.GetLocomotionFootPhaseReference(_owner));

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
