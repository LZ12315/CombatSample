#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RootMotionDependencyHash
{
    public static bool TryCompute(
        AnimationClip clip,
        AnimationConfig config,
        out string dependencyHash,
        out string diagnostic)
    {
        dependencyHash = string.Empty;
        diagnostic = string.Empty;

        if (clip == null)
        {
            diagnostic = "AnimationClip is missing.";
            return false;
        }

        if (config == null)
        {
            diagnostic = "AnimationConfig is missing.";
            return false;
        }

        RootMotionBakeSettingsValidationResult validation = config.ValidateRootMotionBakeSettings();
        if (!validation.IsValid)
        {
            diagnostic = JoinSettingsIssues(validation);
            return false;
        }

        config.TryGetRootMotionAnimator(out Animator animator);
        var material = new StringBuilder(512);
        material.Append("RootMotionBakerVersion=").Append(RootMotionBaker.CurrentBakerVersion).Append('\n');
        material.Append("SampleRate=").Append(config.RootMotionSampleRate).Append('\n');
        material.Append("PositionTolerance=")
            .Append(config.RootMotionPositionTolerance.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        material.Append("RotationTolerance=")
            .Append(config.RootMotionRotationToleranceDegrees.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        if (!AppendAssetIdentity(material, "Clip", clip, out diagnostic))
            return false;
        material.Append("ClipLength=").Append(clip.length.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        material.Append("ClipFrameRate=").Append(clip.frameRate.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        if (!AppendAssetIdentity(material, "ReferenceRig", config.RootMotionReferenceRigPrefab, out diagnostic)
            || !AppendAssetIdentity(material, "Avatar", animator != null ? animator.avatar : null, out diagnostic))
        {
            return false;
        }

        // Do not hash AnimationConfig itself: the generated trajectory is serialized
        // inside it, so doing so would make every successful bake immediately stale.
        dependencyHash = Hash128.Compute(material.ToString()).ToString();
        return true;
    }

    private static bool AppendAssetIdentity(
        StringBuilder material,
        string label,
        Object asset,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        material.Append(label).Append('=');
        if (asset == null)
        {
            diagnostic = $"{label} dependency is missing.";
            return false;
        }

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)
            || string.IsNullOrEmpty(guid))
        {
            diagnostic = $"{label} '{asset.name}' must be a persistent asset before baking.";
            return false;
        }

        material.Append(guid).Append(':').Append(localId);

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            diagnostic = $"{label} '{asset.name}' has no asset path.";
            return false;
        }

        material.Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
        material.Append('\n');
        return true;
    }

    private static string JoinSettingsIssues(RootMotionBakeSettingsValidationResult validation)
    {
        var message = new StringBuilder();
        for (int i = 0; i < validation.Issues.Count; i++)
        {
            if (message.Length > 0)
                message.Append(' ');
            message.Append(validation.Issues[i].Message);
        }
        return message.ToString();
    }
}
#endif
