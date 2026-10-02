#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionContractTests
{
    [Test]
    public void IntentBuffer_HoldsContinuousInputAndReplacesItWithoutRestoringOldInput()
    {
        var input = new LocomotionIntentBuffer();
        input.Submit(Intent(Vector3.forward), continuous: true);
        Assert.That(input.Capture(out var locked, out _), Is.True);
        input.Submit(Intent(Vector3.right), continuous: true);
        Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.forward));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(input.Capture(out locked, out _), Is.True);
            Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.right));
        }
        input.Submit(Intent(Vector3.back));
        Assert.That(input.Capture(out locked, out _), Is.True);
        Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.back));
        Assert.That(input.Capture(out _, out _), Is.False, "A one-shot replaces held input; it does not suspend it.");
        input.Submit(Intent(Vector3.forward), continuous: true);
        input.Capture(out locked, out bool continuous);
        input.ReleaseContinuous();
        input.Hold(locked, continuous);
        Assert.That(input.Capture(out _, out _), Is.False, "Pausing must not resurrect a released continuous input.");
    }

    [Test]
    public void IntentBuffer_HoldsFrozenOneShotAndKeepsNewerSubmissions()
    {
        var input = new LocomotionIntentBuffer();
        input.Submit(Intent(Vector3.forward));
        input.Capture(out var locked, out bool continuous);
        input.Hold(locked, continuous);
        Assert.That(input.Capture(out locked, out _), Is.True);
        Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.forward));
        input.Submit(Intent(Vector3.right));
        input.Hold(locked, continuous);
        input.Capture(out locked, out _);
        Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.right));
        Assert.That(input.Capture(out _, out _), Is.False);
        input.Submit(Intent(Vector3.forward), continuous: true);
        input.Clear();
        Assert.That(input.Capture(out _, out _), Is.False);
        input.Submit(Intent(Vector3.forward));
        input.Capture(out locked, out continuous);
        input.Clear();
        input.Hold(locked, continuous);
        Assert.That(input.Capture(out _, out _), Is.False, "Freezing must not undo an explicit release.");
        var invalid = Intent(Vector3.forward); invalid.MoveStrength = float.NaN;
        Assert.Throws<System.ArgumentException>(() => input.Submit(invalid));
        Assert.That(input.Capture(out _, out _), Is.False);
    }

    [Test]
    public void GroundMovement_AcceleratesBrakesAndReversesThroughZero()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        LocomotionMovementConfig config = LocomotionMovementConfig.Default;
        LocomotionIntent forward = Intent(Vector3.forward);

        runner.Prepare(forward, true, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(2f).Within(0.001f));

        runner.Prepare(forward, true, 0.2f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(5f).Within(0.001f));

        runner.Prepare(LocomotionIntent.Idle, false, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(1.8f).Within(0.001f));

        runner.Prepare(Intent(Vector3.back), true, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.LessThan(0f));
        Assert.That(runner.CachedVelocity.z, Is.GreaterThan(-2f));
    }

    [Test]
    public void AirAssetConfig_UsesItsOwnSpeedAndRates()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        LocomotionMovementConfig config = LocomotionMovementConfig.Default;
        config.MaxSpeed = 2f;
        config.Acceleration = 6f;
        config.Deceleration = 3f;

        runner.Prepare(Intent(Vector3.forward), true, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(0.6f).Within(0.001f));
        runner.Prepare(Intent(Vector3.forward), true, 1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(2f).Within(0.001f));
        runner.Prepare(LocomotionIntent.Idle, false, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(1.7f).Within(0.001f));
    }

    [Test]
    public void Facing_UsesOnlyExplicitFacingDirection()
    {
        LocomotionMovementConfig config = LocomotionMovementConfig.Default;
        var explicitFacing = new LocomotionRunner();
        var preserveFacing = new LocomotionRunner();
        explicitFacing.Initialize(Quaternion.identity);
        preserveFacing.Initialize(Quaternion.Euler(0f, 25f, 0f));
        LocomotionIntent intent = Intent(Vector3.forward);
        intent.FacingDirection = Vector3.right;

        explicitFacing.Prepare(intent, true, 0.1f, config);
        intent.FacingDirection = Vector3.zero;
        preserveFacing.Prepare(intent, true, 0.1f, config);

        Assert.That(Quaternion.Angle(Quaternion.identity, explicitFacing.PendingRotation),
            Is.EqualTo(60f).Within(0.01f));
        Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 25f, 0f), preserveFacing.PendingRotation),
            Is.LessThan(0.001f));
    }

    [Test]
    public void ZeroFacing_StopsAnUnfinishedTurnAtTheCurrentFacing()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        LocomotionIntent intent = Intent(Vector3.forward);
        intent.FacingDirection = Vector3.right;
        runner.Prepare(intent, true, 0.05f, LocomotionMovementConfig.Default);
        Quaternion facingBeforeRelease = runner.PendingRotation;

        intent.FacingDirection = Vector3.zero;
        runner.Prepare(intent, true, 0.1f, LocomotionMovementConfig.Default);

        Assert.That(Quaternion.Angle(facingBeforeRelease, runner.PendingRotation), Is.LessThan(0.001f));
    }

    [Test]
    public void TurnResponse_SmoothsSmallTurnsBelowTheSpeedCap()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        LocomotionIntent intent = Intent(Quaternion.Euler(0f, 10f, 0f) * Vector3.forward);
        intent.FacingDirection = intent.WorldMoveDirection;

        runner.Prepare(intent, true, 0.1f, LocomotionMovementConfig.Default);

        float turned = Quaternion.Angle(Quaternion.identity, runner.PendingRotation);
        Assert.That(turned, Is.GreaterThan(7f).And.LessThan(8f));
    }

    [Test]
    public void ZeroDeltaTime_DoesNotConsumeIntentOrIntegrateVelocity()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        runner.Prepare(Intent(Vector3.forward), true, 0f, LocomotionMovementConfig.Default);

        Assert.That(runner.CachedVelocity, Is.EqualTo(Vector3.zero));
        Assert.That(runner.EffectiveIntent.MoveStrength, Is.Zero);
    }

    [Test]
    public void InvalidMovementConfig_IsRejectedWithoutReplacingValuesWithDefaults()
    {
        LocomotionMovementConfig config = LocomotionMovementConfig.Default;
        config.Deceleration = 0f;
        config.MaxSpeed = float.NaN;
        Assert.That(config.IsValid, Is.False);
        Assert.Throws<System.ArgumentException>(() => config.Sanitize());
        var runner = new LocomotionRunner();
        Assert.Throws<System.ArgumentException>(() => runner.Prepare(Intent(Vector3.forward), true, 0.1f, config));
    }

    [Test]
    public void MissingAnimationCoverage_IsReportedWithoutInvalidatingMovement()
    {
        LocomotionSetAsset asset = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        try
        {
            var issues = new List<string>();
            asset.CollectAnimationCoverageIssues(issues);
            Assert.That(asset.HasValidMovementConfig, Is.True);
            Assert.That(issues, Has.Some.Contains("Move 1D samples are missing"));
            Assert.That(issues, Has.None.Contains("Start"));
            Assert.That(issues, Has.None.Contains("Stop"));
            Assert.That(issues, Has.None.Contains("Pivot"));
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void MoveSamples_DefaultToSynchronized()
    {
        Assert.That(new LocomotionMove1DSample().Sync, Is.True);
        Assert.That(new LocomotionMove2DSample().Sync, Is.True);
    }

    [Test]
    public void MoveDefinition_ReportsInvalidAndDuplicateThresholds()
    {
        const string json = "{\"blendType\":0,\"oneDimensional\":{"
            + "\"parameter\":0,\"samples\":[{\"threshold\":0},{\"threshold\":0},{\"threshold\":-1}]},"
            + "\"twoDimensional\":{\"samples\":[]}}";
        LocomotionMoveDefinition definition = JsonUtility.FromJson<LocomotionMoveDefinition>(json);
        var issues = new List<string>();

        definition.CollectCoverageIssues(issues);

        Assert.That(issues, Has.Some.Contains("duplicates threshold"));
        Assert.That(issues, Has.Some.Contains("invalid threshold"));
    }

    [Test]
    public void SetDefinition_ReportsInvalidStartStopAndPivotDirections()
    {
        LocomotionSetAsset asset = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        try
        {
            const string json = "{"
                + "\"start\":[{\"targetLocalDirection\":{\"x\":0,\"y\":0}}],"
                + "\"stop\":[{\"sourceLocalDirection\":{\"x\":0,\"y\":0}}],"
                + "\"pivot\":[{\"sourceLocalDirection\":{\"x\":0,\"y\":1},"
                + "\"targetLocalDirection\":{\"x\":0,\"y\":0}}]}";
            JsonUtility.FromJsonOverwrite(json, asset);
            var issues = new List<string>();

            asset.CollectAnimationCoverageIssues(issues);

            Assert.That(issues, Has.Some.Contains("Start[0] has an invalid target direction"));
            Assert.That(issues, Has.Some.Contains("Stop[0] has an invalid source direction"));
            Assert.That(issues, Has.Some.Contains("Pivot[0] has invalid source/target directions"));
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void ConcreteAssets_CreateTheirOwnRuntimeTypes()
    {
        LocomotionSetAsset set = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        LocomotionMixerAsset mixer = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
        try
        {
            using (LocomotionRuntime setRuntime = set.CreateRuntime())
                Assert.That(setRuntime, Is.TypeOf<LocomotionSetRuntime>());
            using (LocomotionRuntime mixerRuntime = mixer.CreateRuntime())
                Assert.That(mixerRuntime, Is.TypeOf<LocomotionMixerRuntime>());
        }
        finally
        {
            Object.DestroyImmediate(set);
            Object.DestroyImmediate(mixer);
        }
    }

    [Test]
    public void AssetSelection_UsesMotionContextGroundState()
    {
        var owner = new GameObject("Locomotion Asset Selection Test");
        LocomotionSetAsset ground = null;
        LocomotionMixerAsset air = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;

            ground = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            air = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
            SetPrivateField(ground, "entryConditions", new List<LocomotionModeCondition>
            {
                JsonUtility.FromJson<LocomotionGroundStateCondition>("{\"acceptedStates\":3}")
            });
            SetPrivateField(air, "entryConditions", new List<LocomotionModeCondition>
            {
                JsonUtility.FromJson<LocomotionGroundStateCondition>("{\"acceptedStates\":12}")
            });
            SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { ground, air });

            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(new LocomotionMotionContext(0.02f,
                ActorGroundState.Grounded, Vector3.up, Quaternion.identity, default, default));
            Assert.That(locomotion.CurrentAsset, Is.SameAs(ground));

            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(new LocomotionMotionContext(0.02f,
                ActorGroundState.JustLeftGround, Vector3.up, Quaternion.identity, default, default));
            Assert.That(locomotion.CurrentAsset, Is.SameAs(air));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (ground != null)
                Object.DestroyImmediate(ground);
            if (air != null)
                Object.DestroyImmediate(air);
        }
    }

    [Test]
    public void EqualPrioritySelection_KeepsCurrentAssetWhenAuthoredOrderChanges()
    {
        var owner = new GameObject("Locomotion Stable Selection Test");
        LocomotionSetAsset first = null;
        LocomotionSetAsset second = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;
            first = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            second = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            var assets = new List<LocomotionAsset> { first, second };
            SetPrivateField(locomotion, "locomotionAssets", assets);

            LocomotionMotionContext context = new LocomotionMotionContext(0.02f,
                ActorGroundState.Grounded, Vector3.up, Quaternion.identity, default, default);
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(context);
            Assert.That(locomotion.CurrentAsset, Is.SameAs(first));

            assets.Clear();
            assets.Add(second);
            assets.Add(first);
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(context);
            Assert.That(locomotion.CurrentAsset, Is.SameAs(first));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (first != null)
                Object.DestroyImmediate(first);
            if (second != null)
                Object.DestroyImmediate(second);
        }
    }

    [TestCase(1, 10)]
    [TestCase(int.MinValue, int.MinValue + 1)]
    [TestCase(int.MinValue, int.MinValue)]
    public void AssetSelection_UsesPriorityAndAuthoredOrderAcrossTheFullIntegerRange(int lowPriority, int highPriority)
    {
        var owner = new GameObject("Locomotion Priority Selection Test");
        LocomotionSetAsset low = null;
        LocomotionSetAsset high = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;
            low = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            high = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            JsonUtility.FromJsonOverwrite($"{{\"priority\":{lowPriority}}}", low);
            JsonUtility.FromJsonOverwrite($"{{\"priority\":{highPriority}}}", high);
            SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { low, high });

            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));

            Assert.That(locomotion.CurrentAsset, Is.SameAs(highPriority > lowPriority ? high : low));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (low != null)
                Object.DestroyImmediate(low);
            if (high != null)
                Object.DestroyImmediate(high);
        }
    }

    [Test]
    public void AssetSwitch_PreservesSharedIntegratedVelocity()
    {
        var owner = new GameObject("Locomotion Shared Velocity Test");
        LocomotionSetAsset ground = null;
        LocomotionMixerAsset air = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;
            ground = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            air = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
            SetPrivateField(ground, "entryConditions", new List<LocomotionModeCondition>
            {
                GroundCondition(LocomotionGroundStateMask.Grounded)
            });
            SetPrivateField(air, "entryConditions", new List<LocomotionModeCondition>
            {
                GroundCondition(LocomotionGroundStateMask.Airborne)
            });
            SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { ground, air });

            locomotion.SetLocomotionIntent(Intent(Vector3.forward));
            locomotion.BeginControlTick();
            LocomotionMotionRequest groundRequest = locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            Assert.That(groundRequest.WorldPlanarVelocity.z, Is.EqualTo(2f).Within(0.001f));

            locomotion.SetLocomotionIntent(Intent(Vector3.forward));
            locomotion.BeginControlTick();
            LocomotionMotionRequest airRequest = locomotion.BuildMotionRequest(Context(ActorGroundState.Airborne));
            Assert.That(locomotion.CurrentAsset, Is.SameAs(air));
            Assert.That(airRequest.WorldPlanarVelocity.z, Is.EqualTo(4f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (ground != null)
                Object.DestroyImmediate(ground);
            if (air != null)
                Object.DestroyImmediate(air);
        }
    }

    [Test]
    public void NoMatchingCandidate_ExitsCurrentAssetAndStopsContributing()
    {
        var owner = new GameObject("Locomotion No Match Test");
        LocomotionSetAsset ground = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;
            ground = ScriptableObject.CreateInstance<LocomotionSetAsset>();
            SetPrivateField(ground, "entryConditions", new List<LocomotionModeCondition>
            {
                GroundCondition(LocomotionGroundStateMask.Grounded)
            });
            SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { ground });

            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            Assert.That(locomotion.CurrentAsset, Is.SameAs(ground));

            locomotion.BeginControlTick();
            LocomotionMotionRequest request = locomotion.BuildMotionRequest(Context(ActorGroundState.Airborne));
            Assert.That(locomotion.CurrentAsset, Is.Null);
            Assert.That(request.HasVelocity, Is.False);
            Assert.That(request.HasRotation, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (ground != null)
                Object.DestroyImmediate(ground);
        }
    }

    [Test]
    public void Runtime_IsCachedWhileAssetRemainsSelected()
    {
        var owner = new GameObject("Locomotion Runtime Lifecycle Test");
        TrackingLocomotionAsset asset = null;
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
            actor.actorLocomotion = locomotion;
            asset = ScriptableObject.CreateInstance<TrackingLocomotionAsset>();
            SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { asset });

            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));

            Assert.That(asset.CreatedCount, Is.EqualTo(1));
            Assert.That(asset.EnterCount, Is.EqualTo(1));
            Assert.That(asset.ExitCount, Is.Zero);
            Assert.That(asset.DisposeCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (asset != null)
                Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void RuntimeLifecycle_EnterAndExitAreIdempotentAndDisposeReleasesState()
    {
        TrackingLocomotionAsset asset = ScriptableObject.CreateInstance<TrackingLocomotionAsset>();
        try
        {
            LocomotionRuntime runtime = asset.CreateRuntime();
            runtime.Enter(null, null);
            runtime.Enter(null, null);
            runtime.Exit(null, null);
            runtime.Exit(null, null);
            runtime.Dispose();

            Assert.That(asset.CreatedCount, Is.EqualTo(1));
            Assert.That(asset.EnterCount, Is.EqualTo(1));
            Assert.That(asset.ExitCount, Is.EqualTo(1));
            Assert.That(asset.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.IsEntered, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    private static LocomotionIntent Intent(Vector3 direction) => new LocomotionIntent
    {
        WorldMoveDirection = direction,
        MoveStrength = 1f,
    };

    private static LocomotionMotionContext Context(ActorGroundState state) =>
        new LocomotionMotionContext(0.1f, state, Vector3.up, Quaternion.identity, default, default);

    private static LocomotionGroundStateCondition GroundCondition(LocomotionGroundStateMask states) =>
        JsonUtility.FromJson<LocomotionGroundStateCondition>($"{{\"acceptedStates\":{(int)states}}}");

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                continue;

            field.SetValue(target, value);
            return;
        }

        Assert.Fail($"Field '{fieldName}' was not found on {target.GetType().Name}.");
    }
}

public sealed class TrackingLocomotionAsset : LocomotionAsset
{
    public int CreatedCount { get; private set; }
    public int EnterCount { get; private set; }
    public int ExitCount { get; private set; }
    public int DisposeCount { get; private set; }

    public override LocomotionRuntime CreateRuntime()
    {
        CreatedCount++;
        return new TrackingLocomotionRuntime(this);
    }

    internal void RecordEnter() => EnterCount++;
    internal void RecordExit() => ExitCount++;
    internal void RecordDispose() => DisposeCount++;
}

public sealed class TrackingLocomotionRuntime : LocomotionRuntime
{
    private readonly TrackingLocomotionAsset _trackingAsset;

    public TrackingLocomotionRuntime(TrackingLocomotionAsset asset) : base(asset)
    {
        _trackingAsset = asset;
    }

    public override LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context) => default;

    protected override void OnEnter(ActorLocomotion owner, Actor actor) => _trackingAsset.RecordEnter();
    protected override void OnExit(ActorLocomotion owner, Actor actor) => _trackingAsset.RecordExit();
    protected override void OnDispose() => _trackingAsset.RecordDispose();
}
#endif
