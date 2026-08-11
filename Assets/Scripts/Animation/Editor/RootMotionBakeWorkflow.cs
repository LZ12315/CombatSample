#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum RootMotionEntryStatusCode
{
    Ready,
    Missing,
    Stale,
    PoseOnly,
    Invalid,
}

public readonly struct RootMotionEntryStatus
{
    public RootMotionEntryStatusCode Code { get; }
    public string Message { get; }
    public AnimationClip SourceClip { get; }
    public string ExpectedDependencyHash { get; }

    public RootMotionEntryStatus(
        RootMotionEntryStatusCode code,
        string message,
        AnimationClip sourceClip = null,
        string expectedDependencyHash = null)
    {
        Code = code;
        Message = message ?? string.Empty;
        SourceClip = sourceClip;
        ExpectedDependencyHash = expectedDependencyHash ?? string.Empty;
    }
}

public enum RootMotionBakeOperationResultCode
{
    Success,
    InvalidConfig,
    InvalidEntry,
    UnsupportedTransition,
    DependencyHashFailed,
    BakeFailed,
    ValidationFailed,
    ConfigWriteFailed,
}

public readonly struct RootMotionBakeOperationResult
{
    public bool Success => Code == RootMotionBakeOperationResultCode.Success;
    public RootMotionBakeOperationResultCode Code { get; }
    public string Message { get; }
    public RootMotionTrajectory Trajectory { get; }
    public RootMotionValidationReport ValidationReport { get; }

    public RootMotionBakeOperationResult(
        RootMotionBakeOperationResultCode code,
        string message,
        RootMotionTrajectory trajectory = null,
        RootMotionValidationReport validationReport = null)
    {
        Code = code;
        Message = message ?? string.Empty;
        Trajectory = trajectory;
        ValidationReport = validationReport;
    }
}

public sealed class RootMotionBakeBatchResult
{
    private readonly List<RootMotionBakeOperationResult> _entries = new List<RootMotionBakeOperationResult>();

    public IReadOnlyList<RootMotionBakeOperationResult> Entries => _entries;
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }
    public int SkippedCount { get; private set; }
    public bool IsSuccess => FailureCount == 0;

    public string Summary
    {
        get
        {
            string summary = $"Bake All finished: {SuccessCount} succeeded, {SkippedCount} skipped, {FailureCount} failed.";
            if (FailureCount == 0)
                return summary;

            IEnumerable<string> failures = _entries
                .Where(entry => !entry.Success)
                .Select(entry => $"{entry.Code}: {entry.Message}");
            return summary + " " + string.Join(" ", failures);
        }
    }

    internal void Add(RootMotionBakeOperationResult result, bool skipped)
    {
        _entries.Add(result);
        if (skipped)
            SkippedCount++;
        else if (result.Success)
            SuccessCount++;
        else
            FailureCount++;
    }
}

/// <summary>
/// Editor authoring workflow which bakes directly into an AnimationConfig entry.
/// No generated UnityEngine.Object or external asset is created.
/// </summary>
public static class RootMotionBakeWorkflow
{
    public static RootMotionEntryStatus GetEntryStatus(AnimationConfig config, int entryIndex)
    {
        if (!TryGetEntry(config, entryIndex, out AnimationConfigEntry entry, out string entryError))
            return new RootMotionEntryStatus(RootMotionEntryStatusCode.Invalid, entryError);

        if (!TryValidateConfig(config, out string configError))
            return new RootMotionEntryStatus(RootMotionEntryStatusCode.Invalid, configError);

        if (HasDuplicateKey(config, entry.Key))
            return new RootMotionEntryStatus(RootMotionEntryStatusCode.Invalid, $"Entry key '{entry.Key}' is duplicated.");

        if (!RootMotionTransitionClipResolver.TryResolve(
                entry.TransitionAsset,
                out AnimationClip clip,
                out RootMotionClipResolutionDiagnostic resolution))
        {
            if (resolution.Code == RootMotionClipResolutionCode.MultipleAnimationClips
                && entry.RootMotionTrajectory == null)
            {
                return new RootMotionEntryStatus(
                    RootMotionEntryStatusCode.PoseOnly,
                    "Transition resolves to multiple clips and has no single trajectory to bake.");
            }

            RootMotionEntryStatusCode code = entry.RootMotionTrajectory == null
                ? RootMotionEntryStatusCode.Invalid
                : RootMotionEntryStatusCode.Stale;
            return new RootMotionEntryStatus(code, resolution.Message);
        }

        if (!RootMotionDependencyHash.TryCompute(clip, config, out string expectedHash, out string hashError))
            return new RootMotionEntryStatus(RootMotionEntryStatusCode.Invalid, hashError, clip);

        RootMotionTrajectory trajectory = entry.RootMotionTrajectory;
        if (trajectory == null)
        {
            return new RootMotionEntryStatus(
                RootMotionEntryStatusCode.Missing,
                "No embedded Root Motion data. Use Bake.",
                clip,
                expectedHash);
        }

        RootMotionTrajectoryValidationResult dataValidation = trajectory.ValidateData();
        if (!dataValidation.IsValid)
        {
            return new RootMotionEntryStatus(
                RootMotionEntryStatusCode.Invalid,
                "Embedded trajectory data is invalid: " + JoinTrajectoryIssues(dataValidation),
                clip,
                expectedHash);
        }

        if (trajectory.SourceClip != clip
            || trajectory.SampleRate != config.RootMotionSampleRate
            || trajectory.BakerVersion != RootMotionBaker.CurrentBakerVersion
            || Mathf.Abs(trajectory.Duration - clip.length) > 1e-5f
            || !string.Equals(trajectory.DependencyHash, expectedHash, StringComparison.Ordinal))
        {
            return new RootMotionEntryStatus(
                RootMotionEntryStatusCode.Stale,
                "Root Motion dependencies changed. Use Rebake.",
                clip,
                expectedHash);
        }

        return new RootMotionEntryStatus(
            RootMotionEntryStatusCode.Ready,
            $"Embedded trajectory is current ({trajectory.SampleCount} samples, {trajectory.Duration:R}s).",
            clip,
            expectedHash);
    }

    public static bool TryBakeEntry(
        AnimationConfig config,
        int entryIndex,
        out RootMotionBakeOperationResult operationResult)
    {
        operationResult = default;
        if (!TryGetEntry(config, entryIndex, out AnimationConfigEntry entry, out string entryError))
        {
            operationResult = Failure(RootMotionBakeOperationResultCode.InvalidEntry, entryError);
            return false;
        }

        if (!TryValidateConfig(config, out string configError))
        {
            operationResult = Failure(RootMotionBakeOperationResultCode.InvalidConfig, configError);
            return false;
        }

        if (HasDuplicateKey(config, entry.Key))
        {
            operationResult = Failure(
                RootMotionBakeOperationResultCode.InvalidEntry,
                $"Entry key '{entry.Key}' is duplicated; baking requires a unique key.");
            return false;
        }

        RootMotionTrajectory existing = entry.RootMotionTrajectory;
        if (!RootMotionTransitionClipResolver.TryResolve(
                entry.TransitionAsset,
                out AnimationClip clip,
                out RootMotionClipResolutionDiagnostic resolution))
        {
            operationResult = Failure(RootMotionBakeOperationResultCode.UnsupportedTransition, resolution.Message, existing);
            return false;
        }

        if (!RootMotionDependencyHash.TryCompute(clip, config, out string dependencyHash, out string hashError))
        {
            operationResult = Failure(RootMotionBakeOperationResultCode.DependencyHashFailed, hashError, existing);
            return false;
        }

        if (!RootMotionBaker.TryBake(clip, config, out RootMotionBakeResult bake, out RootMotionBakeDiagnostic bakeDiagnostic))
        {
            operationResult = Failure(RootMotionBakeOperationResultCode.BakeFailed, bakeDiagnostic.Message, existing);
            return false;
        }

        if (!RootMotionBakeValidator.TryValidate(
                bake,
                out RootMotionValidationReport validationReport,
                out RootMotionValidationDiagnostic validationDiagnostic))
        {
            operationResult = Failure(
                RootMotionBakeOperationResultCode.ValidationFailed,
                validationDiagnostic.Message,
                existing);
            return false;
        }

        if (!validationReport.IsValid)
        {
            operationResult = new RootMotionBakeOperationResult(
                RootMotionBakeOperationResultCode.ValidationFailed,
                validationReport.Summary,
                existing,
                validationReport);
            return false;
        }

        bake.CopySamples(out float[] times, out Vector3[] positions, out Quaternion[] rotations);
        var replacement = new RootMotionTrajectory();
        replacement.EditorSetData(
            bake.SourceClip,
            bake.SampleRate,
            bake.Duration,
            bake.BakerVersion,
            dependencyHash,
            times,
            positions,
            rotations);

        RootMotionTrajectoryValidationResult replacementValidation = replacement.ValidateData();
        if (!replacementValidation.IsValid)
        {
            operationResult = Failure(
                RootMotionBakeOperationResultCode.ValidationFailed,
                "Baked trajectory data is invalid: " + JoinTrajectoryIssues(replacementValidation),
                existing);
            return false;
        }

        try
        {
            Undo.RecordObject(config, $"Bake Root Motion '{entry.Key}'");
            entry.EditorSetRootMotionTrajectory(replacement);
            EditorUtility.SetDirty(config);
            if (AssetDatabase.Contains(config))
                AssetDatabase.SaveAssetIfDirty(config);

            operationResult = new RootMotionBakeOperationResult(
                RootMotionBakeOperationResultCode.Success,
                $"Baked '{entry.Key}' into AnimationConfig. {validationReport.Summary}",
                replacement,
                validationReport);
            return true;
        }
        catch (Exception exception)
        {
            entry.EditorSetRootMotionTrajectory(existing);
            EditorUtility.SetDirty(config);
            operationResult = Failure(
                RootMotionBakeOperationResultCode.ConfigWriteFailed,
                $"Failed to store Root Motion for '{entry.Key}': {exception.GetType().Name}: {exception.Message}",
                existing);
            return false;
        }
    }

    public static RootMotionBakeBatchResult BakeAll(AnimationConfig config)
    {
        var result = new RootMotionBakeBatchResult();
        int count = config != null ? config.Entries.Count : 0;
        for (int i = 0; i < count; i++)
        {
            RootMotionEntryStatus status = GetEntryStatus(config, i);
            if (status.Code == RootMotionEntryStatusCode.PoseOnly)
            {
                string skippedKey = config.Entries[i] != null ? config.Entries[i].Key : string.Empty;
                result.Add(
                    Failure(
                        RootMotionBakeOperationResultCode.UnsupportedTransition,
                        $"Entry {i} '{skippedKey}': {status.Message}"),
                    skipped: true);
                continue;
            }

            TryBakeEntry(config, i, out RootMotionBakeOperationResult entryResult);
            string key = config.Entries[i] != null ? config.Entries[i].Key : string.Empty;
            result.Add(
                new RootMotionBakeOperationResult(
                    entryResult.Code,
                    $"Entry {i} '{key}': {entryResult.Message}",
                    entryResult.Trajectory,
                    entryResult.ValidationReport),
                skipped: false);
        }

        return result;
    }

    public static bool TryClearTrajectory(AnimationConfig config, int entryIndex, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (!TryGetEntry(config, entryIndex, out AnimationConfigEntry entry, out diagnostic))
            return false;

        if (entry.RootMotionTrajectory == null)
        {
            diagnostic = $"Entry '{entry.Key}' has no embedded Root Motion data.";
            return false;
        }

        Undo.RecordObject(config, $"Clear Root Motion '{entry.Key}'");
        entry.EditorSetRootMotionTrajectory(null);
        EditorUtility.SetDirty(config);
        if (AssetDatabase.Contains(config))
            AssetDatabase.SaveAssetIfDirty(config);
        diagnostic = $"Cleared embedded Root Motion data from '{entry.Key}'.";
        return true;
    }

    private static bool TryGetEntry(
        AnimationConfig config,
        int entryIndex,
        out AnimationConfigEntry entry,
        out string diagnostic)
    {
        entry = null;
        diagnostic = string.Empty;
        if (config == null)
        {
            diagnostic = "AnimationConfig is missing.";
            return false;
        }

        if (entryIndex < 0 || entryIndex >= config.Entries.Count)
        {
            diagnostic = $"Entry index {entryIndex} is out of range.";
            return false;
        }

        entry = config.Entries[entryIndex];
        if (entry == null)
        {
            diagnostic = $"Entry {entryIndex} is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Key))
        {
            diagnostic = $"Entry {entryIndex} has an empty key.";
            return false;
        }

        if (entry.TransitionAsset == null)
        {
            diagnostic = $"Entry '{entry.Key}' has no TransitionAsset.";
            return false;
        }

        return true;
    }

    private static bool TryValidateConfig(AnimationConfig config, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (config == null)
        {
            diagnostic = "AnimationConfig is missing.";
            return false;
        }

        RootMotionBakeSettingsValidationResult validation = config.ValidateRootMotionBakeSettings();
        if (!validation.IsValid)
        {
            diagnostic = "Root Motion Bake Context is invalid: "
                + string.Join(" ", validation.Issues.Select(issue => issue.Message));
            return false;
        }

        return true;
    }

    private static bool HasDuplicateKey(AnimationConfig config, string key)
    {
        int matches = 0;
        for (int i = 0; i < config.Entries.Count; i++)
        {
            if (config.Entries[i] != null
                && string.Equals(config.Entries[i].Key, key, StringComparison.Ordinal))
            {
                matches++;
            }
        }
        return matches > 1;
    }

    private static string JoinTrajectoryIssues(RootMotionTrajectoryValidationResult validation)
    {
        return string.Join(" ", validation.Issues.Select(issue => issue.Message));
    }

    private static RootMotionBakeOperationResult Failure(
        RootMotionBakeOperationResultCode code,
        string message,
        RootMotionTrajectory trajectory = null)
    {
        return new RootMotionBakeOperationResult(code, message, trajectory);
    }
}
#endif
