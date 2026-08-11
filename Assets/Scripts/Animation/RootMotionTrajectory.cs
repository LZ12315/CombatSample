using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baked cumulative root motion owned and serialized inline by an AnimationConfig entry.
/// </summary>
[Serializable]
public sealed class RootMotionTrajectory : ISerializationCallbackReceiver
{
    [Header("Source Metadata")]
    [SerializeField] private AnimationClip sourceClip;
    [SerializeField] private int sampleRate = 60;
    [SerializeField] private float duration;
    [SerializeField] private int bakerVersion;
    [SerializeField] private string dependencyHash = string.Empty;

    [Header("Cumulative Root Transform")]
    [SerializeField] private float[] sampleTimes = Array.Empty<float>();
    [SerializeField] private Vector3[] cumulativePositions = Array.Empty<Vector3>();
    [SerializeField] private Quaternion[] cumulativeRotations = Array.Empty<Quaternion>();

    [NonSerialized] private bool _sampleLayoutEvaluated;
    [NonSerialized] private bool _sampleLayoutValid;

    public AnimationClip SourceClip => sourceClip;
    public int SampleRate => sampleRate;
    public float Duration => duration;
    public int BakerVersion => bakerVersion;
    public string DependencyHash => dependencyHash;
    public int SampleCount => sampleTimes != null ? sampleTimes.Length : 0;
    public IReadOnlyList<float> SampleTimes => sampleTimes ?? Array.Empty<float>();
    public IReadOnlyList<Vector3> CumulativePositions => cumulativePositions ?? Array.Empty<Vector3>();
    public IReadOnlyList<Quaternion> CumulativeRotations => cumulativeRotations ?? Array.Empty<Quaternion>();

    public bool TrySample(float time, out RootMotionTransform sample)
    {
        sample = RootMotionTransform.Identity;
        if (!RootMotionTransform.IsFinite(time) || !EnsureSampleLayout())
            return false;

        int lastIndex = sampleTimes.Length - 1;
        if (time <= sampleTimes[0])
        {
            sample = GetSample(0);
            return true;
        }

        if (time >= sampleTimes[lastIndex])
        {
            sample = GetSample(lastIndex);
            return true;
        }

        int index = Array.BinarySearch(sampleTimes, time);
        if (index >= 0)
        {
            sample = GetSample(index);
            return true;
        }

        int upperIndex = ~index;
        int lowerIndex = upperIndex - 1;
        float lowerTime = sampleTimes[lowerIndex];
        float upperTime = sampleTimes[upperIndex];
        float interpolation = (time - lowerTime) / (upperTime - lowerTime);

        sample = new RootMotionTransform(
            Vector3.LerpUnclamped(cumulativePositions[lowerIndex], cumulativePositions[upperIndex], interpolation),
            Quaternion.SlerpUnclamped(cumulativeRotations[lowerIndex], cumulativeRotations[upperIndex], interpolation));
        return true;
    }

    public bool TryExtract(float startTime, float endTime, out RootMotionTransform delta)
    {
        delta = RootMotionTransform.Identity;
        if (!TrySample(startTime, out RootMotionTransform start)
            || !TrySample(endTime, out RootMotionTransform end))
        {
            return false;
        }

        delta = RootMotionTransform.Delta(start, end);
        return true;
    }

    public RootMotionTrajectoryValidationResult ValidateData()
    {
        var result = new RootMotionTrajectoryValidationResult();

        if (sourceClip == null)
            result.Add(RootMotionTrajectoryValidationCode.MissingSourceClip, -1, "Source AnimationClip is missing.");

        if (sampleRate <= 0)
            result.Add(RootMotionTrajectoryValidationCode.InvalidSampleRate, -1, "Sample rate must be greater than zero.");

        if (!RootMotionTransform.IsFinite(duration) || duration <= 0f)
            result.Add(RootMotionTrajectoryValidationCode.InvalidDuration, -1, "Duration must be finite and greater than zero.");

        if (bakerVersion <= 0)
            result.Add(RootMotionTrajectoryValidationCode.InvalidBakerVersion, -1, "Baker version must be greater than zero.");

        if (string.IsNullOrEmpty(dependencyHash))
            result.Add(RootMotionTrajectoryValidationCode.MissingDependencyHash, -1, "Dependency hash is missing.");

        int timeCount = sampleTimes != null ? sampleTimes.Length : 0;
        int positionCount = cumulativePositions != null ? cumulativePositions.Length : 0;
        int rotationCount = cumulativeRotations != null ? cumulativeRotations.Length : 0;
        if (timeCount != positionCount || timeCount != rotationCount)
        {
            result.Add(
                RootMotionTrajectoryValidationCode.LengthMismatch,
                -1,
                "Sample times, positions, and rotations must have matching lengths.");
        }

        int count = Mathf.Min(timeCount, Mathf.Min(positionCount, rotationCount));
        if (count < 2)
        {
            result.Add(RootMotionTrajectoryValidationCode.InsufficientSamples, -1, "A trajectory requires at least two samples.");
            return result;
        }

        if (!Mathf.Approximately(sampleTimes[0], 0f))
            result.Add(RootMotionTrajectoryValidationCode.FirstSampleTimeNotZero, 0, "The first sample time must be zero.");

        if (cumulativePositions[0].sqrMagnitude > 1e-10f
            || Quaternion.Angle(RootMotionTransform.NormalizeSafe(cumulativeRotations[0]), Quaternion.identity) > 0.001f)
        {
            result.Add(RootMotionTrajectoryValidationCode.FirstSampleNotIdentity, 0, "The first cumulative transform must be identity.");
        }

        for (int i = 0; i < count; i++)
        {
            if (!RootMotionTransform.IsFinite(sampleTimes[i]))
                result.Add(RootMotionTrajectoryValidationCode.NonFiniteValue, i, "Sample time is not finite.");

            if (!IsFinite(cumulativePositions[i]))
                result.Add(RootMotionTrajectoryValidationCode.NonFiniteValue, i, "Cumulative position is not finite.");

            if (!IsValidUnitQuaternion(cumulativeRotations[i]))
                result.Add(RootMotionTrajectoryValidationCode.InvalidQuaternion, i, "Cumulative rotation must be a finite unit quaternion.");

            if (i > 0 && !(sampleTimes[i] > sampleTimes[i - 1]))
                result.Add(RootMotionTrajectoryValidationCode.NonIncreasingSampleTime, i, "Sample times must be strictly increasing.");
        }

        if (RootMotionTransform.IsFinite(duration)
            && RootMotionTransform.IsFinite(sampleTimes[count - 1])
            && Mathf.Abs(sampleTimes[count - 1] - duration) > 1e-4f)
        {
            result.Add(RootMotionTrajectoryValidationCode.DurationMismatch, count - 1, "Duration must match the final sample time.");
        }

        return result;
    }

#if UNITY_EDITOR
    public void EditorSetData(
        AnimationClip newSourceClip,
        int newSampleRate,
        float newDuration,
        int newBakerVersion,
        string newDependencyHash,
        float[] newSampleTimes,
        Vector3[] newCumulativePositions,
        Quaternion[] newCumulativeRotations)
    {
        sourceClip = newSourceClip;
        sampleRate = newSampleRate;
        duration = newDuration;
        bakerVersion = newBakerVersion;
        dependencyHash = newDependencyHash ?? string.Empty;
        sampleTimes = CloneOrEmpty(newSampleTimes);
        cumulativePositions = CloneOrEmpty(newCumulativePositions);
        cumulativeRotations = CloneOrEmpty(newCumulativeRotations);
        InvalidateSampleLayout();
    }
#endif

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        InvalidateSampleLayout();
    }

    private RootMotionTransform GetSample(int index)
    {
        return new RootMotionTransform(cumulativePositions[index], cumulativeRotations[index]);
    }

    private bool EnsureSampleLayout()
    {
        if (_sampleLayoutEvaluated)
            return _sampleLayoutValid;

        _sampleLayoutEvaluated = true;
        _sampleLayoutValid = EvaluateSampleLayout();
        return _sampleLayoutValid;
    }

    private bool EvaluateSampleLayout()
    {
        if (sampleTimes == null || cumulativePositions == null || cumulativeRotations == null)
            return false;

        int count = sampleTimes.Length;
        if (count == 0 || cumulativePositions.Length != count || cumulativeRotations.Length != count)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!RootMotionTransform.IsFinite(sampleTimes[i])
                || !IsFinite(cumulativePositions[i])
                || !IsValidUnitQuaternion(cumulativeRotations[i]))
            {
                return false;
            }

            if (i > 0 && !(sampleTimes[i] > sampleTimes[i - 1]))
                return false;
        }

        return true;
    }

    private void InvalidateSampleLayout()
    {
        _sampleLayoutEvaluated = false;
        _sampleLayoutValid = false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return RootMotionTransform.IsFinite(value.x)
            && RootMotionTransform.IsFinite(value.y)
            && RootMotionTransform.IsFinite(value.z);
    }

    private static bool IsValidUnitQuaternion(Quaternion value)
    {
        if (!RootMotionTransform.IsFinite(value.x)
            || !RootMotionTransform.IsFinite(value.y)
            || !RootMotionTransform.IsFinite(value.z)
            || !RootMotionTransform.IsFinite(value.w))
        {
            return false;
        }

        float sqrMagnitude = value.x * value.x
            + value.y * value.y
            + value.z * value.z
            + value.w * value.w;
        return sqrMagnitude > 1e-12f && Mathf.Abs(sqrMagnitude - 1f) <= 1e-3f;
    }

#if UNITY_EDITOR
    private static T[] CloneOrEmpty<T>(T[] source)
    {
        return source != null ? (T[])source.Clone() : Array.Empty<T>();
    }
#endif
}

public enum RootMotionTrajectoryValidationCode
{
    MissingSourceClip,
    InvalidSampleRate,
    InvalidDuration,
    InvalidBakerVersion,
    MissingDependencyHash,
    LengthMismatch,
    InsufficientSamples,
    FirstSampleTimeNotZero,
    FirstSampleNotIdentity,
    NonFiniteValue,
    InvalidQuaternion,
    NonIncreasingSampleTime,
    DurationMismatch,
}

public readonly struct RootMotionTrajectoryValidationIssue
{
    public RootMotionTrajectoryValidationCode Code { get; }
    public int SampleIndex { get; }
    public string Message { get; }

    public RootMotionTrajectoryValidationIssue(RootMotionTrajectoryValidationCode code, int sampleIndex, string message)
    {
        Code = code;
        SampleIndex = sampleIndex;
        Message = message;
    }
}

public sealed class RootMotionTrajectoryValidationResult
{
    private readonly List<RootMotionTrajectoryValidationIssue> _issues = new List<RootMotionTrajectoryValidationIssue>();

    public bool IsValid => _issues.Count == 0;
    public IReadOnlyList<RootMotionTrajectoryValidationIssue> Issues => _issues;

    internal void Add(RootMotionTrajectoryValidationCode code, int sampleIndex, string message)
    {
        _issues.Add(new RootMotionTrajectoryValidationIssue(code, sampleIndex, message));
    }
}
