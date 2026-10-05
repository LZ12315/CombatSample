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
    public void GroundMovement_AcceleratesAndBrakesAtConfiguredRates()
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

    }

    [TestCase(45f)]
    [TestCase(90f)]
    [TestCase(135f)]
    [TestCase(180f)]
    public void DirectionChanges_AtFullInputDoNotDependOnStoppingDeceleration(float angle)
    {
        var slowBrake = LocomotionMovementConfig.Default;
        var fastBrake = slowBrake;
        slowBrake.Deceleration = 2f;
        fastBrake.Deceleration = 32f;
        var slow = new LocomotionRunner(); var fast = new LocomotionRunner();
        slow.Initialize(Quaternion.identity); fast.Initialize(Quaternion.identity);
        slow.Prepare(Intent(Vector3.forward), true, 0.25f, slowBrake);
        fast.Prepare(Intent(Vector3.forward), true, 0.25f, fastBrake);
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        for (int i = 0; i < 20; i++)
        {
            slow.Prepare(Intent(direction), true, 0.05f, slowBrake);
            fast.Prepare(Intent(direction), true, 0.05f, fastBrake);
            Assert.That(Vector3.Distance(slow.CachedVelocity, fast.CachedVelocity), Is.LessThan(1e-5f));
            Assert.That(slow.CachedVelocity.magnitude, Is.LessThanOrEqualTo(slowBrake.MaxSpeed + 1e-5f));
            Assert.That(slow.CachedVelocity.y, Is.Zero);
        }
        Assert.That(Vector3.Distance(slow.CachedVelocity, direction * slowBrake.MaxSpeed), Is.LessThan(1e-4f));
        slow.Prepare(LocomotionIntent.Idle, false, 0.1f, slowBrake);
        fast.Prepare(LocomotionIntent.Idle, false, 0.1f, fastBrake);
        Assert.That(slow.CachedVelocity.magnitude, Is.EqualTo(4.8f).Within(1e-4f));
        Assert.That(fast.CachedVelocity.magnitude, Is.EqualTo(1.8f).Within(1e-4f));
    }

    [Test]
    public void DirectionResponse_ControlsSteeringWithoutChangingReleaseBrakingOrFacing()
    {
        var assisted = LocomotionMovementConfig.Default;
        assisted.Deceleration = 12f;
        var unassisted = assisted; unassisted.DirectionResponse = 0f;
        var fast = new LocomotionRunner(); var slow = new LocomotionRunner();
        fast.Initialize(Quaternion.identity); slow.Initialize(Quaternion.identity);
        fast.Prepare(Intent(Vector3.forward), true, 0.25f, assisted);
        slow.Prepare(Intent(Vector3.forward), true, 0.25f, unassisted);
        fast.Prepare(LocomotionIntent.Idle, false, 0.1f, assisted);
        slow.Prepare(LocomotionIntent.Idle, false, 0.1f, unassisted);
        Assert.That(fast.CachedVelocity, Is.EqualTo(slow.CachedVelocity));
        Assert.That(fast.CachedVelocity.magnitude, Is.EqualTo(3.8f).Within(1e-4f));

        var right = Intent(Vector3.right); right.FacingDirection = Vector3.forward;
        fast.Prepare(right, true, 0.05f, assisted);
        slow.Prepare(right, true, 0.05f, unassisted);
        Assert.That(Vector3.Angle(fast.CachedVelocity, Vector3.right),
            Is.LessThan(Vector3.Angle(slow.CachedVelocity, Vector3.right)));
        Assert.That(Quaternion.Angle(fast.PendingRotation, Quaternion.identity), Is.LessThan(1e-4f));
    }

    [Test]
    public void NewInput_DuringBrakingSteersWithoutWaitingForTheOldVelocityToReachZero()
    {
        var config = LocomotionMovementConfig.Default; config.Deceleration = 12f;
        var runner = new LocomotionRunner(); runner.Initialize(Quaternion.identity);
        runner.Prepare(Intent(Vector3.forward), true, 0.25f, config);
        runner.Prepare(LocomotionIntent.Idle, false, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.GreaterThan(0f));
        runner.Prepare(Intent(Vector3.back), true, 0.05f, config);
        Assert.That(runner.CachedVelocity.z, Is.LessThan(0f));
        Assert.That(runner.CachedVelocity.x, Is.Zero);
        Assert.That(runner.CachedVelocity.y, Is.Zero);
    }

    [Test]
    public void PartialInput_ReducesStraightLineSpeedAtConfiguredDeceleration()
    {
        var config = LocomotionMovementConfig.Default; config.Deceleration = 12f;
        var runner = new LocomotionRunner(); runner.Initialize(Quaternion.identity);
        runner.Prepare(Intent(Vector3.forward), true, 0.25f, config);
        var partial = Intent(Vector3.forward); partial.MoveStrength = 0.5f;
        runner.Prepare(partial, true, 0.1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(3.8f).Within(1e-4f));
        runner.Prepare(partial, true, 1f, config);
        Assert.That(runner.CachedVelocity.z, Is.EqualTo(2.5f).Within(1e-4f));
    }

    [Test]
    public void MovementConfig_RejectsInvalidDirectionResponse()
    {
        foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            var config = LocomotionMovementConfig.Default; config.DirectionResponse = value;
            Assert.That(config.IsValid, Is.False);
            Assert.Throws<System.ArgumentException>(() => config.Sanitize());
        }
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
    public void ZeroMovementRates_PreserveVelocityAndFacingWithoutInvalidatingTheAsset()
    {
        var runner = new LocomotionRunner();
        runner.Initialize(Quaternion.identity);
        var config = LocomotionMovementConfig.Default;
        runner.Prepare(Intent(Vector3.forward), true, 0.2f, config);
        Vector3 velocity = runner.CachedVelocity;
        Quaternion facing = runner.PendingRotation;
        config.Acceleration = config.Deceleration = config.DirectionResponse = config.RotateSpeed = 0f;
        Assert.That(config.IsValid, Is.True);
        runner.Prepare(Intent(Vector3.back), true, 0.2f, config);
        Assert.That(runner.CachedVelocity, Is.EqualTo(velocity));
        Assert.That(runner.PendingRotation, Is.EqualTo(facing));
    }

    [Test]
    public void MoveSamples_DefaultToSynchronized()
    {
        Assert.That(new LocomotionMove1DSample().Sync, Is.True);
        Assert.That(new LocomotionMove2DSample().Sync, Is.True);
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
    public void ControlTick_LocksInputAndRepeatedBuildDoesNotIntegrateAgain()
    {
        var owner = new GameObject("Locomotion Tick Test");
        var asset = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
        try
        {
            owner.AddComponent<Actor>();
            var locomotion = owner.AddComponent<ActorLocomotion>();
            ConfigureAssets(locomotion, asset);
            locomotion.SetLocomotionIntent(Intent(Vector3.forward));
            locomotion.BeginControlTick();
            locomotion.SetLocomotionIntent(Intent(Vector3.right));
            Assert.That(locomotion.TryGetControlIntent(out var locked), Is.True);
            Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.forward));
            var first = locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            var repeated = locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            Assert.That(first.WorldPlanarVelocity.z, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(repeated.WorldPlanarVelocity, Is.EqualTo(first.WorldPlanarVelocity));
            Assert.That(locomotion.DebugLocomotionVelocity, Is.EqualTo(first.WorldPlanarVelocity));
            locomotion.BeginControlTick();
            Assert.That(locomotion.TryGetControlIntent(out locked), Is.True);
            Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.right));
            locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            Assert.That(locomotion.EffectiveIntent.WorldMoveDirection, Is.EqualTo(Vector3.right));
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(asset); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FrozenControlTick_RestoresOneShotUnlessNewInputOrClearReplacesIt(bool clear)
    {
        var owner = new GameObject("Locomotion Frozen Tick Test");
        try
        {
            owner.AddComponent<Actor>();
            var locomotion = owner.AddComponent<ActorLocomotion>();
            var frozen = new LocomotionMotionContext(0f, ActorGroundState.Grounded,
                Vector3.up, Quaternion.identity, default, default);
            locomotion.SetLocomotionIntent(Intent(Vector3.forward));
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(frozen);
            Assert.That(locomotion.DebugLocomotionVelocity, Is.EqualTo(Vector3.zero));
            locomotion.BeginControlTick();
            Assert.That(locomotion.TryGetControlIntent(out var locked), Is.True);
            Assert.That(locked.WorldMoveDirection, Is.EqualTo(Vector3.forward));
            if (clear) locomotion.ClearLocomotionIntent();
            else locomotion.SetLocomotionIntent(Intent(Vector3.right));
            locomotion.BuildMotionRequest(frozen);
            locomotion.BeginControlTick();
            Assert.That(locomotion.TryGetControlIntent(out locked), Is.EqualTo(!clear));
            Assert.That(locked.WorldMoveDirection, Is.EqualTo(clear ? Vector3.zero : Vector3.right));
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void UnlockedBuild_UsesNoInputAndCancellationClearsPendingInput()
    {
        var owner = new GameObject("Locomotion Unlocked Tick Test");
        var asset = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
        try
        {
            owner.AddComponent<Actor>();
            var locomotion = owner.AddComponent<ActorLocomotion>();
            ConfigureAssets(locomotion, asset);
            locomotion.SetLocomotionIntent(Intent(Vector3.forward));
            var request = locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded));
            Assert.That(request.WorldPlanarVelocity, Is.EqualTo(Vector3.zero));
            locomotion.BeginControlTick();
            Assert.That(locomotion.TryGetControlIntent(out _), Is.True);
            locomotion.SetLocomotionIntent(Intent(Vector3.right));
            locomotion.CancelControlTick();
            locomotion.BeginControlTick();
            Assert.That(locomotion.TryGetControlIntent(out _), Is.False);
            Assert.That(locomotion.BuildMotionRequest(Context(ActorGroundState.Grounded)).WorldPlanarVelocity,
                Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(asset); }
    }

    internal static void ConfigureAssets(ActorLocomotion locomotion, LocomotionAsset asset) =>
        JsonUtility.FromJsonOverwrite($"{{\"locomotionAssets\":[{{\"instanceID\":{asset.GetInstanceID()}}}]}}", locomotion);

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
    public int AnimationResetCount { get; private set; }
    public int AnimationUpdateCount { get; private set; }
    public LocomotionRuntimeAnimationContext LastAnimationContext { get; private set; }

    public override LocomotionRuntime CreateRuntime()
    {
        CreatedCount++;
        return new TrackingLocomotionRuntime(this);
    }

    internal void RecordEnter() => EnterCount++;
    internal void RecordExit() => ExitCount++;
    internal void RecordDispose() => DisposeCount++;
    internal void RecordAnimationReset() => AnimationResetCount++;
    internal void RecordAnimation(in LocomotionRuntimeAnimationContext context)
    {
        AnimationUpdateCount++;
        LastAnimationContext = context;
    }
}

public sealed class TrackingLocomotionRuntime : LocomotionRuntime
{
    private readonly TrackingLocomotionAsset _trackingAsset;

    public TrackingLocomotionRuntime(TrackingLocomotionAsset asset) : base(asset)
    {
        _trackingAsset = asset;
    }

    public override LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context) => UpdateSharedMotion(context);
    public override LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context)
    {
        _trackingAsset.RecordAnimation(context);
        return default;
    }
    public override void ResetAnimation() => _trackingAsset.RecordAnimationReset();

    protected override void OnEnter(ActorLocomotion owner, Actor actor) => _trackingAsset.RecordEnter();
    protected override void OnExit(ActorLocomotion owner, Actor actor)
    {
        _trackingAsset.RecordExit();
        ResetAnimation();
    }
    protected override void OnDispose() => _trackingAsset.RecordDispose();
}
#endif
