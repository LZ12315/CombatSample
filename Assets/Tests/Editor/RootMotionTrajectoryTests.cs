#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class RootMotionTrajectoryTests
{
    private readonly List<Object> _createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void TrySample_ClampsBoundsAndInterpolatesPositionAndRotation()
    {
        RootMotionTrajectory trajectory = CreateTrajectory(
            new[] { 0f, 1f },
            new[] { Vector3.zero, new Vector3(10f, 2f, -4f) },
            new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) });

        Assert.IsTrue(trajectory.TrySample(-10f, out RootMotionTransform before));
        AssertVector(Vector3.zero, before.Position);
        AssertQuaternion(Quaternion.identity, before.Rotation);

        Assert.IsTrue(trajectory.TrySample(0.5f, out RootMotionTransform middle));
        AssertVector(new Vector3(5f, 1f, -2f), middle.Position);
        AssertQuaternion(Quaternion.Euler(0f, 45f, 0f), middle.Rotation);

        Assert.IsTrue(trajectory.TrySample(10f, out RootMotionTransform after));
        AssertVector(new Vector3(10f, 2f, -4f), after.Position);
        AssertQuaternion(Quaternion.Euler(0f, 90f, 0f), after.Rotation);
    }

    [Test]
    public void TryExtract_UsesRotatedStartFrameInsteadOfWorldPositionDifference()
    {
        RootMotionTransform middle = new RootMotionTransform(
            new Vector3(3f, 0.5f, -2f),
            Quaternion.Euler(0f, 90f, 0f));
        RootMotionTransform expectedDelta = new RootMotionTransform(
            new Vector3(0.25f, 0f, 2f),
            Quaternion.Euler(0f, 35f, 0f));
        RootMotionTransform end = RootMotionTransform.Compose(middle, expectedDelta);
        RootMotionTrajectory trajectory = CreateTrajectory(
            new[] { 0f, 1f, 2f },
            new[] { Vector3.zero, middle.Position, end.Position },
            new[] { Quaternion.identity, middle.Rotation, end.Rotation });

        Assert.IsTrue(trajectory.TryExtract(1f, 2f, out RootMotionTransform actual));

        AssertVector(expectedDelta.Position, actual.Position);
        AssertQuaternion(expectedDelta.Rotation, actual.Rotation);
        Assert.Greater((end.Position - middle.Position - expectedDelta.Position).sqrMagnitude, 0.1f,
            "This fixture must detect an incorrect plain world-space subtraction.");
    }

    [Test]
    public void ComposeAndInverse_RoundTripRigidDeltas()
    {
        RootMotionTransform first = new RootMotionTransform(
            new Vector3(1f, -0.5f, 2f),
            Quaternion.Euler(10f, 70f, -5f));
        RootMotionTransform second = new RootMotionTransform(
            new Vector3(-0.25f, 1f, 3f),
            Quaternion.Euler(-15f, 25f, 20f));
        RootMotionTransform combined = RootMotionTransform.Compose(first, second);

        RootMotionTransform recoveredSecond = RootMotionTransform.Delta(first, combined);
        RootMotionTransform identity = RootMotionTransform.Compose(combined, RootMotionTransform.Inverse(combined));

        AssertVector(second.Position, recoveredSecond.Position);
        AssertQuaternion(second.Rotation, recoveredSecond.Rotation);
        AssertVector(Vector3.zero, identity.Position);
        AssertQuaternion(Quaternion.identity, identity.Rotation);
    }

    [Test]
    public void ReverseExtract_IsInverseOfForwardExtract()
    {
        RootMotionTransform middle = new RootMotionTransform(new Vector3(2f, 0f, 1f), Quaternion.Euler(0f, 45f, 0f));
        RootMotionTransform end = RootMotionTransform.Compose(
            middle,
            new RootMotionTransform(new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 60f, 0f)));
        RootMotionTrajectory trajectory = CreateTrajectory(
            new[] { 0f, 1f, 2f },
            new[] { Vector3.zero, middle.Position, end.Position },
            new[] { Quaternion.identity, middle.Rotation, end.Rotation });

        Assert.IsTrue(trajectory.TryExtract(1f, 2f, out RootMotionTransform forward));
        Assert.IsTrue(trajectory.TryExtract(2f, 1f, out RootMotionTransform reverse));
        RootMotionTransform identity = RootMotionTransform.Compose(forward, reverse);

        AssertVector(Vector3.zero, identity.Position);
        AssertQuaternion(Quaternion.identity, identity.Rotation);
    }

    [Test]
    public void EditorSetData_ClonesInputArrays()
    {
        float[] times = { 0f, 1f };
        Vector3[] positions = { Vector3.zero, Vector3.forward };
        Quaternion[] rotations = { Quaternion.identity, Quaternion.identity };
        RootMotionTrajectory trajectory = CreateTrajectory(times, positions, rotations);

        times[1] = 5f;
        positions[1] = Vector3.right * 100f;
        rotations[1] = Quaternion.Euler(0f, 180f, 0f);

        Assert.AreEqual(1f, trajectory.SampleTimes[1]);
        AssertVector(Vector3.forward, trajectory.CumulativePositions[1]);
        AssertQuaternion(Quaternion.identity, trajectory.CumulativeRotations[1]);
    }

    [Test]
    public void ValidateData_AcceptsCompleteTrajectory()
    {
        RootMotionTrajectory trajectory = CreateTrajectory(
            new[] { 0f, 0.5f, 1f },
            new[] { Vector3.zero, Vector3.forward, Vector3.forward * 2f },
            new[] { Quaternion.identity, Quaternion.Euler(0f, 20f, 0f), Quaternion.Euler(0f, 40f, 0f) });

        Assert.IsTrue(trajectory.ValidateData().IsValid);
    }

    [Test]
    public void ValidateData_ReportsMalformedMetadataAndSamples()
    {
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            null,
            0,
            3f,
            0,
            null,
            new[] { 0.25f, 0.25f },
            new[] { Vector3.one, new Vector3(float.NaN, 0f, 0f) },
            new[] { new Quaternion(0f, 0f, 0f, 0f), Quaternion.identity });

        RootMotionTrajectoryValidationResult result = trajectory.ValidateData();

        AssertCode(result, RootMotionTrajectoryValidationCode.MissingSourceClip);
        AssertCode(result, RootMotionTrajectoryValidationCode.InvalidSampleRate);
        AssertCode(result, RootMotionTrajectoryValidationCode.InvalidBakerVersion);
        AssertCode(result, RootMotionTrajectoryValidationCode.MissingDependencyHash);
        AssertCode(result, RootMotionTrajectoryValidationCode.FirstSampleTimeNotZero);
        AssertCode(result, RootMotionTrajectoryValidationCode.FirstSampleNotIdentity);
        AssertCode(result, RootMotionTrajectoryValidationCode.NonFiniteValue);
        AssertCode(result, RootMotionTrajectoryValidationCode.InvalidQuaternion);
        AssertCode(result, RootMotionTrajectoryValidationCode.NonIncreasingSampleTime);
        AssertCode(result, RootMotionTrajectoryValidationCode.DurationMismatch);
        Assert.IsFalse(trajectory.TrySample(0.25f, out _));
    }

    [Test]
    public void ValidateData_ReportsMismatchedAndInsufficientArrays()
    {
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            Create<AnimationClip>(),
            60,
            1f,
            1,
            "hash",
            new[] { 0f, 1f },
            new[] { Vector3.zero },
            new[] { Quaternion.identity });

        RootMotionTrajectoryValidationResult result = trajectory.ValidateData();

        AssertCode(result, RootMotionTrajectoryValidationCode.LengthMismatch);
        AssertCode(result, RootMotionTrajectoryValidationCode.InsufficientSamples);
        Assert.IsFalse(trajectory.TrySample(0f, out _));
    }

    private RootMotionTrajectory CreateTrajectory(float[] times, Vector3[] positions, Quaternion[] rotations)
    {
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            Create<AnimationClip>(),
            60,
            times[times.Length - 1],
            1,
            "test-hash",
            times,
            positions,
            rotations);
        return trajectory;
    }

    private T Create<T>() where T : Object
    {
        T value;
        if (typeof(ScriptableObject).IsAssignableFrom(typeof(T)))
            value = (T)(Object)ScriptableObject.CreateInstance(typeof(T));
        else
            value = (T)(Object)new AnimationClip();

        _createdObjects.Add(value);
        return value;
    }

    private static void AssertCode(RootMotionTrajectoryValidationResult result, RootMotionTrajectoryValidationCode code)
    {
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == code), code.ToString());
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.LessOrEqual((expected - actual).magnitude, 1e-4f, $"Expected {expected}, got {actual}.");
    }

    private static void AssertQuaternion(Quaternion expected, Quaternion actual)
    {
        Assert.LessOrEqual(Quaternion.Angle(expected, actual), 0.01f, $"Expected {expected.eulerAngles}, got {actual.eulerAngles}.");
    }
}
#endif
