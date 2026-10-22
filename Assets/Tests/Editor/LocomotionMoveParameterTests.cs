#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LocomotionMoveParameterTests
{
    private static LocomotionTurnLeanConfig Immediate
    {
        get
        {
            var config = LocomotionTurnLeanConfig.Default;
            config.SmoothSeconds = 0f;
            return config;
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(-1)]
    [TestCase(2)]
    public void Legacy2DData_ResolvesAxesWithoutRewritingSerializedData(int legacy)
    {
        var definition = JsonUtility.FromJson<LocomotionMove2DDefinition>("{\"parameter\":" + legacy + "}");
        string before = JsonUtility.ToJson(definition);
        var expectedX = legacy switch
        {
            0 => LocomotionMoveParameter.LocalVelocityX,
            1 => LocomotionMoveParameter.LocalInputX,
            _ => (LocomotionMoveParameter)(-1),
        };
        var expectedY = legacy switch
        {
            0 => LocomotionMoveParameter.LocalVelocityZ,
            1 => LocomotionMoveParameter.LocalInputZ,
            _ => (LocomotionMoveParameter)(-1),
        };
        Assert.That(definition.XParameter, Is.EqualTo(expectedX));
        Assert.That(definition.YParameter, Is.EqualTo(expectedY));
        Assert.That(definition.Algorithm, Is.EqualTo(LocomotionMove2DAlgorithm.Directional));
        Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
    }

    [Test]
    public void Lean_TurnsAreSymmetricAndStraightAccelerationIsNeutral()
    {
        var right = new LocomotionTurnLean();
        var left = new LocomotionTurnLean();
        Vector3 before = Vector3.forward * 5f;
        Assert.That(right.Update(Context(before, new Vector3(1f, 0f, 5f)), Immediate), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(left.Update(Context(before, new Vector3(-1f, 0f, 5f)), Immediate), Is.EqualTo(-right.Value));
        Assert.That(right.Update(Context(before, Vector3.forward * 6f), Immediate), Is.Zero);
        Assert.That(right.Update(Context(before, Vector3.forward * 4f), Immediate), Is.Zero);
    }

    [Test]
    public void Lean_StationaryLowSpeedAndReversalsAreNeutralAndLargeTurnsAreBounded()
    {
        var lean = new LocomotionTurnLean();
        Assert.That(lean.Update(Context(Vector3.zero, Vector3.right), Immediate), Is.Zero);
        Assert.That(lean.Update(Context(Vector3.forward * 0.1f, Vector3.right * 0.1f), Immediate), Is.Zero);
        Assert.That(lean.Update(Context(Vector3.forward * 5f, Vector3.back * 5f), Immediate), Is.Zero);
        Assert.That(lean.Update(Context(Vector3.forward * 5f, Vector3.right * 50f), Immediate), Is.EqualTo(1f));
    }

    [Test]
    public void Lean_UsesMotionTimeAndKeepsFrozenValue()
    {
        var lean = new LocomotionTurnLean();
        Vector3 before = Vector3.forward * 5f, after = new Vector3(1f, 0f, 5f);
        float first = lean.Update(Context(before, after, motionDt: 0.1f, animationDt: 0.001f), Immediate);
        Assert.That(first, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(lean.Update(Context(before, -after, motionDt: 0f), Immediate), Is.EqualTo(first));
        Assert.That(lean.Update(Context(before, after, motionDt: 0.2f), Immediate), Is.EqualTo(first * 0.5f).Within(0.0001f));
    }

    [Test]
    public void Lean_SmoothingIsTimeBasedAndResetDoesNotRetainOldTurn()
    {
        var config = Immediate;
        config.SmoothSeconds = 0.1f;
        var whole = new LocomotionTurnLean();
        var halves = new LocomotionTurnLean();
        whole.Update(Context(Vector3.forward * 5f, new Vector3(1f, 0f, 5f)), config);
        var halfContext = Context(Vector3.forward * 5f, new Vector3(0.5f, 0f, 5f), motionDt: 0.05f);
        halves.Update(halfContext, config);
        halves.Update(halfContext, config);
        Assert.That(halves.Value, Is.EqualTo(whole.Value).Within(0.0001f));
        whole.Reset();
        Assert.That(whole.Value, Is.Zero);
        whole.Update(Context(Vector3.forward * 5f, Vector3.forward * 5f), config);
        Assert.That(whole.Value, Is.Zero);
    }

    [TestCase("action")]
    [TestCase("velocity")]
    [TestCase("trajectory")]
    [TestCase("rootRotation")]
    [TestCase("scriptRotation")]
    [TestCase("scale")]
    [TestCase("air")]
    public void Lean_InterruptedMotionRebaselinesOnReturn(string interruption)
    {
        var lean = new LocomotionTurnLean();
        Vector3 before = Vector3.forward * 5f, after = new Vector3(1f, 0f, 5f);
        lean.Update(Context(before, after), Immediate);
        Assert.That(lean.Value, Is.GreaterThan(0f));
        var policy = new MotionStateSnapshot(interruption == "scale" ? 0f : 1f, 1f, 1f, 1f,
            interruption == "velocity", interruption == "trajectory", interruption == "rootRotation", interruption == "scriptRotation");
        Assert.That(lean.Update(Context(before, after, policy: policy, action: interruption == "action" ? 1 : 0,
            grounded: interruption != "air"), Immediate), Is.Zero);
        Assert.That(lean.Update(Context(before, after), Immediate), Is.Zero, "Do not replay a turn across an ownership gap.");
        Assert.That(lean.Update(Context(before, after), Immediate), Is.GreaterThan(0f));
    }

    [Test]
    public void Lean_InputReleaseReachesStationaryRowWithoutResidualTilt()
    {
        var lean = new LocomotionTurnLean();
        Vector3 before = Vector3.forward * 5f, after = new Vector3(1f, 0f, 5f);
        lean.Update(Context(before, after), Immediate);
        Assert.That(lean.Value, Is.GreaterThan(0f));
        var motor = new LocomotionMotionContext(0.1f, true, Vector3.up, Quaternion.identity, default,
            new MotionStateSnapshot(1f, 1f, 1f, 1f, false, false, false, false));
        var released = new LocomotionRuntimeAnimationContext(LocomotionIntent.Idle, true,
            after, after * 0.9f, motor, 0f, 0, 0.1f);
        Assert.That(lean.Update(released, LocomotionTurnLeanConfig.Default), Is.Zero);
    }

    [Test]
    public void Lean_InvalidSettingsAndNonFiniteMotionDoNotPoisonState()
    {
        var lean = new LocomotionTurnLean();
        var invalid = Immediate;
        invalid.FullLeanAcceleration = 0f;
        Assert.That(lean.Update(Context(Vector3.forward, Vector3.right), invalid), Is.Zero);
        lean.Reset();
        Assert.That(lean.Update(Context(Vector3.forward, new Vector3(float.NaN, 0f, 1f)), Immediate), Is.Zero);
        Assert.That(lean.Update(Context(Vector3.forward * 5f, new Vector3(1f, 0f, 5f)), Immediate), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void Lean_PositivePolicyChangesDoNotInterruptSameTickSteering()
    {
        var lean = new LocomotionTurnLean();
        Vector3 before = Vector3.forward * 5f, after = new Vector3(1f, 0f, 5f);
        for (int i = 0; i < 4; i++)
        {
            float scale = 1f - i * 0.2f;
            var policy = new MotionStateSnapshot(scale, 1f - i * 0.1f, 1f + i, 1f - i * 0.1f,
                false, false, false, false);
            Assert.That(lean.Update(Context(before, after, policy: policy), Immediate),
                Is.EqualTo(0.5f * scale).Within(0.0001f));
        }
    }

    [Test]
    public void Lean_RealRunnerSteeringIsSymmetricBoundedAndSettles()
    {
        float Simulate(float lateral)
        {
            const float dt = 1f / 60f;
            var runner = new LocomotionRunner();
            runner.Initialize(Quaternion.identity);
            var config = LocomotionMovementConfig.Default;
            var intent = new LocomotionIntent { WorldMoveDirection = Vector3.forward, MoveStrength = 1f };
            for (int i = 0; i < 60; i++) runner.Prepare(intent, true, dt, config);
            var lean = new LocomotionTurnLean();
            intent.WorldMoveDirection = new Vector3(lateral, 0f, 1f).normalized;
            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                Vector3 before = runner.CachedVelocity;
                runner.Prepare(intent, true, dt, config);
                float value = lean.Update(Context(before, runner.CachedVelocity, motionDt: dt), LocomotionTurnLeanConfig.Default);
                Assert.That(value, Is.InRange(-1f, 1f));
                if (Mathf.Abs(value) > Mathf.Abs(peak)) peak = value;
            }
            Assert.That(Mathf.Abs(lean.Value), Is.LessThan(0.01f), "A completed turn must settle without continued lean.");
            return peak;
        }

        float right = Simulate(0.5f), left = Simulate(-0.5f);
        Assert.That(right, Is.GreaterThan(0f).And.LessThan(1f), "A saturated target is not an instantly saturated output.");
        Assert.That(left, Is.EqualTo(-right).Within(0.0001f));
        Assert.That(Simulate(0f), Is.Zero);
    }

    private static LocomotionRuntimeAnimationContext Context(Vector3 before, Vector3 after,
        float motionDt = 0.1f, float animationDt = 0.1f, MotionStateSnapshot? policy = null, int action = 0, bool grounded = true)
    {
        var motor = new LocomotionMotionContext(motionDt, grounded, Vector3.up, Quaternion.identity, default,
            policy ?? new MotionStateSnapshot(1f, 1f, 1f, 1f, false, false, false, false));
        return new LocomotionRuntimeAnimationContext(
            new LocomotionIntent { WorldMoveDirection = after.normalized, MoveStrength = 1f }, true,
            before, after, motor, 0f, action, animationDt);
    }
}

// This native Editor contract protects serialized user data, not Inspector geometry.
public sealed class LocomotionMoveAuthoringTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void AxisEdit_PreservesEachOtherAxisAndPendingChanges_WithUndoRedo(bool editX)
    {
        var assets = new[]
        {
            ScriptableObject.CreateInstance<LocomotionMixerAsset>(),
            ScriptableObject.CreateInstance<LocomotionMixerAsset>(),
            ScriptableObject.CreateInstance<LocomotionMixerAsset>(),
        };
        try
        {
            JsonUtility.FromJsonOverwrite("{\"move\":{\"blendType\":1,\"twoDimensional\":{\"parameter\":0}}}", assets[0]);
            JsonUtility.FromJsonOverwrite("{\"move\":{\"blendType\":1,\"twoDimensional\":{\"parameter\":1}}}", assets[1]);
            JsonUtility.FromJsonOverwrite("{\"move\":{\"blendType\":1,\"twoDimensional\":{\"axesConfigured\":true,\"xParameter\":6,\"yParameter\":2}}}", assets[2]);
            var before = new string[assets.Length];
            var after = new string[assets.Length];
            var otherAxes = new LocomotionMoveParameter[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                before[i] = JsonUtility.ToJson(assets[i]);
                var definition = assets[i].Move.TwoDimensional;
                otherAxes[i] = editX ? definition.YParameter : definition.XParameter;
            }

            Undo.IncrementCurrentGroup();
            using (var multi = new SerializedObject(assets))
            {
                multi.FindProperty("move.twoDimensional.algorithm").intValue = (int)LocomotionMove2DAlgorithm.Cartesian;
                LocomotionMoveAuthoringGUI.SetAxis(multi.FindProperty("move.twoDimensional"), editX, LocomotionMoveParameter.TurnLean);
                multi.ApplyModifiedProperties(); // Match the enclosing Inspector's final Apply.
            }
            for (int i = 0; i < assets.Length; i++)
            {
                var definition = assets[i].Move.TwoDimensional;
                Assert.That(editX ? definition.XParameter : definition.YParameter, Is.EqualTo(LocomotionMoveParameter.TurnLean));
                Assert.That(editX ? definition.YParameter : definition.XParameter, Is.EqualTo(otherAxes[i]));
                Assert.That(definition.Algorithm, Is.EqualTo(LocomotionMove2DAlgorithm.Cartesian));
                after[i] = JsonUtility.ToJson(assets[i]);
            }
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            for (int i = 0; i < assets.Length; i++) Assert.That(JsonUtility.ToJson(assets[i]), Is.EqualTo(before[i]));
            Undo.PerformRedo();
            for (int i = 0; i < assets.Length; i++) Assert.That(JsonUtility.ToJson(assets[i]), Is.EqualTo(after[i]));
        }
        finally
        {
            foreach (var asset in assets)
            {
                Undo.ClearUndo(asset);
                Object.DestroyImmediate(asset);
            }
            Undo.IncrementCurrentGroup();
        }
    }
}
#endif
