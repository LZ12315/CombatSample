#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public enum AnimationAssetBakeStatusCode
{
    Ready,
    Missing,
    Stale,
    Invalid,
}

public readonly struct AnimationAssetBakeStatus
{
    public AnimationAssetBakeStatusCode Code { get; }
    public string Message { get; }

    public AnimationAssetBakeStatus(AnimationAssetBakeStatusCode code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}

public readonly struct AnimationAssetBakeOperationResult
{
    public bool Success { get; }
    public string Message { get; }
    public RootMotionValidationReport ValidationReport { get; }

    public AnimationAssetBakeOperationResult(bool success, string message, RootMotionValidationReport validationReport = null)
    {
        Success = success;
        Message = message ?? string.Empty;
        ValidationReport = validationReport;
    }
}

public static class AnimationAssetBakeWorkflow
{
    public static AnimationAssetBakeStatus GetStatus(AnimationAsset asset)
    {
        if (asset == null)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, "AnimationAsset is missing.");
        if (asset.Clip == null)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, "AnimationClip is missing.");
        if (!IsFinite(asset.Clip.length) || asset.Clip.length <= 0f)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, "AnimationClip duration must be finite and greater than zero.");

        RootMotionBakeSettings settings = asset.RootMotionBakeSettings;
        RootMotionBakeSettingsValidationResult settingsValidation = settings != null ? settings.Validate() : null;
        if (settingsValidation == null || !settingsValidation.IsValid)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, JoinSettingsIssues(settingsValidation));

        if (!RootMotionDependencyHash.TryCompute(asset.Clip, settings, out string expectedHash, out string hashError))
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, hashError);

        RootMotionTrajectory trajectory = asset.RootMotionData;
        if (trajectory == null)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Missing, "No baked Root Motion data. Use Bake.");

        RootMotionTrajectoryValidationResult dataValidation = trajectory.ValidateData();
        if (!dataValidation.IsValid)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, "Root Motion data is invalid: " + JoinTrajectoryIssues(dataValidation));

        if (trajectory.SourceClip != asset.Clip
            || trajectory.SampleRate != settings.SampleRate
            || trajectory.BakerVersion != RootMotionBaker.CurrentBakerVersion
            || Mathf.Abs(trajectory.Duration - asset.Clip.length) > 1e-5f
            || !string.Equals(trajectory.DependencyHash, expectedHash, StringComparison.Ordinal))
        {
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Stale, "Root Motion dependencies changed. Use Rebuild.");
        }

        return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Ready, $"Root Motion data is current ({trajectory.SampleCount} samples, {trajectory.Duration:R}s).");
    }

    public static bool TryBake(AnimationAsset asset, out AnimationAssetBakeOperationResult result)
    {
        result = default;
        if (asset == null || asset.Clip == null)
        {
            result = new AnimationAssetBakeOperationResult(false, "AnimationAsset and AnimationClip are required.");
            return false;
        }

        RootMotionBakeSettings settings = asset.RootMotionBakeSettings;
        if (!RootMotionDependencyHash.TryCompute(asset.Clip, settings, out string dependencyHash, out string hashError))
        {
            result = new AnimationAssetBakeOperationResult(false, hashError);
            return false;
        }

        if (!RootMotionBaker.TryBake(asset.Clip, settings, out RootMotionBakeResult bake, out RootMotionBakeDiagnostic bakeDiagnostic))
        {
            result = new AnimationAssetBakeOperationResult(false, bakeDiagnostic.Message);
            return false;
        }

        if (!RootMotionBakeValidator.TryValidate(bake, out RootMotionValidationReport validation, out RootMotionValidationDiagnostic validationDiagnostic))
        {
            result = new AnimationAssetBakeOperationResult(false, validationDiagnostic.Message);
            return false;
        }
        if (!validation.IsValid)
        {
            result = new AnimationAssetBakeOperationResult(false, validation.Summary, validation);
            return false;
        }

        bake.CopySamples(out float[] times, out Vector3[] positions, out Quaternion[] rotations);
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            bake.SourceClip,
            bake.SampleRate,
            bake.Duration,
            bake.BakerVersion,
            dependencyHash,
            times,
            positions,
            rotations);

        RootMotionTrajectoryValidationResult trajectoryValidation = trajectory.ValidateData();
        if (!trajectoryValidation.IsValid)
        {
            result = new AnimationAssetBakeOperationResult(false, "Baked Root Motion data is invalid: " + JoinTrajectoryIssues(trajectoryValidation), validation);
            return false;
        }

        Undo.RecordObject(asset, $"Bake Root Motion '{asset.name}'");
        asset.EditorSetRootMotionData(trajectory);
        EditorUtility.SetDirty(asset);
        if (AssetDatabase.Contains(asset))
            AssetDatabase.SaveAssetIfDirty(asset);

        result = new AnimationAssetBakeOperationResult(true, validation.Summary, validation);
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static string JoinSettingsIssues(RootMotionBakeSettingsValidationResult validation)
    {
        if (validation == null)
            return "Root Motion Bake Settings are missing.";
        string message = string.Empty;
        for (int i = 0; i < validation.Issues.Count; i++)
            message += (i == 0 ? string.Empty : " ") + validation.Issues[i].Message;
        return message;
    }

    private static string JoinTrajectoryIssues(RootMotionTrajectoryValidationResult validation)
    {
        string message = string.Empty;
        for (int i = 0; i < validation.Issues.Count; i++)
            message += (i == 0 ? string.Empty : " ") + validation.Issues[i].Message;
        return message;
    }
}
#endif
