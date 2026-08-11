#if UNITY_EDITOR
using System;
using UnityEngine;

public static class RootMotionBakeValidator
{
    public static bool TryValidate(
        RootMotionBakeResult bakeResult,
        out RootMotionValidationReport report,
        out RootMotionValidationDiagnostic diagnostic)
    {
        report = null;
        diagnostic = default;

        if (bakeResult == null)
        {
            diagnostic = new RootMotionValidationDiagnostic(
                RootMotionValidationDiagnosticCode.MissingBakeResult,
                "RootMotionBakeResult is missing.");
            return false;
        }

        AnimationConfig config = bakeResult.AnimationConfig;
        if (config == null)
        {
            diagnostic = new RootMotionValidationDiagnostic(
                RootMotionValidationDiagnosticCode.MissingAnimationConfig,
                "Bake result has no AnimationConfig.");
            return false;
        }

        if (!RootMotionOracleEvaluator.TryEvaluate(
                bakeResult.SourceClip,
                config,
                bakeResult.SampleTimes,
                out RootMotionOracleResult oracle,
                out RootMotionOracleDiagnostic oracleDiagnostic))
        {
            diagnostic = new RootMotionValidationDiagnostic(
                RootMotionValidationDiagnosticCode.OracleEvaluationFailed,
                oracleDiagnostic.Message);
            return false;
        }

        if (oracle.SampleCount != bakeResult.SampleCount)
        {
            diagnostic = new RootMotionValidationDiagnostic(
                RootMotionValidationDiagnosticCode.SampleCountMismatch,
                $"Baker has {bakeResult.SampleCount} samples while Oracle has {oracle.SampleCount}.");
            return false;
        }

        float maximumPositionError = 0f;
        float maximumRotationError = 0f;
        float maximumPositionErrorTime = 0f;
        float maximumRotationErrorTime = 0f;
        int maximumPositionErrorIndex = 0;
        int maximumRotationErrorIndex = 0;
        RootMotionValidationFailure failures = RootMotionValidationFailure.None;

        for (int i = 0; i < bakeResult.SampleCount; i++)
        {
            float bakerTime = bakeResult.SampleTimes[i];
            float oracleTime = oracle.SampleTimes[i];
            if (!IsFinite(bakerTime) || !IsFinite(oracleTime))
            {
                diagnostic = new RootMotionValidationDiagnostic(
                    RootMotionValidationDiagnosticCode.NonFiniteSample,
                    $"Sample time at index {i} is not finite.");
                return false;
            }

            if (Mathf.Abs(bakerTime - oracleTime) > 1e-6f)
            {
                diagnostic = new RootMotionValidationDiagnostic(
                    RootMotionValidationDiagnosticCode.SampleTimeMismatch,
                    $"Sample time differs at index {i}: Baker={bakerTime:R}, Oracle={oracleTime:R}.");
                return false;
            }

            Vector3 bakerPosition = bakeResult.CumulativePositions[i];
            Vector3 oraclePosition = oracle.CumulativePositions[i];
            Quaternion bakerRotation = bakeResult.CumulativeRotations[i];
            Quaternion oracleRotation = oracle.CumulativeRotations[i];
            if (!IsFinite(bakerPosition)
                || !IsFinite(oraclePosition)
                || !IsFinite(bakerRotation)
                || !IsFinite(oracleRotation))
            {
                diagnostic = new RootMotionValidationDiagnostic(
                    RootMotionValidationDiagnosticCode.NonFiniteSample,
                    $"Root transform at sample {i} ({bakerTime:R}s) is not finite.");
                return false;
            }

            float positionError = Vector3.Distance(bakerPosition, oraclePosition);
            float rotationError = Quaternion.Angle(bakerRotation, oracleRotation);
            if (positionError > maximumPositionError)
            {
                maximumPositionError = positionError;
                maximumPositionErrorTime = bakerTime;
                maximumPositionErrorIndex = i;
            }

            if (rotationError > maximumRotationError)
            {
                maximumRotationError = rotationError;
                maximumRotationErrorTime = bakerTime;
                maximumRotationErrorIndex = i;
            }
        }

        float startPositionMagnitude = bakeResult.CumulativePositions[0].magnitude;
        float startRotationAngle = Quaternion.Angle(bakeResult.CumulativeRotations[0], Quaternion.identity);
        if (startPositionMagnitude > 1e-5f || startRotationAngle > 0.001f)
            failures |= RootMotionValidationFailure.NonIdentityStart;

        float finalTime = bakeResult.SampleTimes[bakeResult.SampleCount - 1];
        if (Mathf.Abs(finalTime - bakeResult.Duration) > 1e-5f
            || Mathf.Abs(finalTime - bakeResult.SourceClip.length) > 1e-5f)
        {
            failures |= RootMotionValidationFailure.DurationMismatch;
        }

        if (maximumPositionError > config.RootMotionPositionTolerance)
            failures |= RootMotionValidationFailure.PositionToleranceExceeded;
        if (maximumRotationError > config.RootMotionRotationToleranceDegrees)
            failures |= RootMotionValidationFailure.RotationToleranceExceeded;

        report = new RootMotionValidationReport(
            failures,
            bakeResult.SampleCount,
            config.RootMotionPositionTolerance,
            config.RootMotionRotationToleranceDegrees,
            maximumPositionError,
            maximumPositionErrorTime,
            maximumPositionErrorIndex,
            maximumRotationError,
            maximumRotationErrorTime,
            maximumRotationErrorIndex);
        diagnostic = RootMotionValidationDiagnostic.Success;
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }
}

[Flags]
public enum RootMotionValidationFailure
{
    None = 0,
    NonIdentityStart = 1 << 0,
    DurationMismatch = 1 << 1,
    PositionToleranceExceeded = 1 << 2,
    RotationToleranceExceeded = 1 << 3,
}

public sealed class RootMotionValidationReport
{
    public RootMotionValidationFailure Failures { get; }
    public bool IsValid => Failures == RootMotionValidationFailure.None;
    public int SamplesCompared { get; }
    public float PositionTolerance { get; }
    public float RotationToleranceDegrees { get; }
    public float MaximumPositionError { get; }
    public float MaximumPositionErrorTime { get; }
    public int MaximumPositionErrorIndex { get; }
    public float MaximumRotationErrorDegrees { get; }
    public float MaximumRotationErrorTime { get; }
    public int MaximumRotationErrorIndex { get; }

    public string Summary
    {
        get
        {
            if (IsValid)
            {
                return $"Validated {SamplesCompared} samples. Max position error {MaximumPositionError:R}m at {MaximumPositionErrorTime:R}s; "
                    + $"max rotation error {MaximumRotationErrorDegrees:R}° at {MaximumRotationErrorTime:R}s.";
            }

            return $"Validation failed ({Failures}). Max position error {MaximumPositionError:R}m at {MaximumPositionErrorTime:R}s "
                + $"(limit {PositionTolerance:R}m); max rotation error {MaximumRotationErrorDegrees:R}° at {MaximumRotationErrorTime:R}s "
                + $"(limit {RotationToleranceDegrees:R}°).";
        }
    }

    internal RootMotionValidationReport(
        RootMotionValidationFailure failures,
        int samplesCompared,
        float positionTolerance,
        float rotationToleranceDegrees,
        float maximumPositionError,
        float maximumPositionErrorTime,
        int maximumPositionErrorIndex,
        float maximumRotationErrorDegrees,
        float maximumRotationErrorTime,
        int maximumRotationErrorIndex)
    {
        Failures = failures;
        SamplesCompared = samplesCompared;
        PositionTolerance = positionTolerance;
        RotationToleranceDegrees = rotationToleranceDegrees;
        MaximumPositionError = maximumPositionError;
        MaximumPositionErrorTime = maximumPositionErrorTime;
        MaximumPositionErrorIndex = maximumPositionErrorIndex;
        MaximumRotationErrorDegrees = maximumRotationErrorDegrees;
        MaximumRotationErrorTime = maximumRotationErrorTime;
        MaximumRotationErrorIndex = maximumRotationErrorIndex;
    }
}

public enum RootMotionValidationDiagnosticCode
{
    None,
    MissingBakeResult,
    MissingAnimationConfig,
    OracleEvaluationFailed,
    SampleCountMismatch,
    SampleTimeMismatch,
    NonFiniteSample,
}

public readonly struct RootMotionValidationDiagnostic
{
    public static RootMotionValidationDiagnostic Success => new RootMotionValidationDiagnostic(RootMotionValidationDiagnosticCode.None, string.Empty);

    public RootMotionValidationDiagnosticCode Code { get; }
    public string Message { get; }

    public RootMotionValidationDiagnostic(RootMotionValidationDiagnosticCode code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}
#endif
