using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Locomotion Mode", fileName = "LocomotionMode")]
public sealed class LocomotionModeAsset : ScriptableObject
{
    [SerializeField] private int priority;
    [SerializeReference, SubclassSelector] private List<LocomotionModeCondition> entryConditions = new();
    [SerializeField] private List<TagReference> selfTags = new();
    [SerializeField] private LocomotionTuning tuning = LocomotionTuning.Default;
    [SerializeField] private LocomotionAnimationProfile animationProfile = LocomotionAnimationProfile.Default;

    public int Priority => priority;
    public IReadOnlyList<LocomotionModeCondition> EntryConditions => entryConditions;
    public IReadOnlyList<TagReference> SelfTags => selfTags;
    public LocomotionTuning Tuning => LocomotionTuning.Sanitize(tuning);
    public LocomotionAnimationProfile AnimationProfile => animationProfile.Sanitize();
    public bool HasEntryConditions => entryConditions != null && entryConditions.Count > 0;

    public bool AreEntryConditionsMet(in LocomotionModeContext context)
    {
        if (!HasEntryConditions)
            return false;

        for (int i = 0; i < entryConditions.Count; i++)
        {
            LocomotionModeCondition condition = entryConditions[i];
            if (condition == null || !condition.Check(context))
                return false;
        }

        return true;
    }

    public bool ValidateRuntime(bool isFallback, UnityEngine.Object context, ref bool warned)
    {
        bool valid = true;
        if (!isFallback && (!HasEntryConditions || HasNullEntryCondition()))
            valid = false;

        LocomotionTuning sanitizedTuning = LocomotionTuning.Sanitize(tuning);
        if (!Mathf.Approximately(sanitizedTuning.MoveSpeed, tuning.MoveSpeed) ||
            !Mathf.Approximately(sanitizedTuning.AirControlFactor, tuning.AirControlFactor) ||
            !Mathf.Approximately(sanitizedTuning.RotateSpeed, tuning.RotateSpeed))
        {
            valid = false;
        }

        LocomotionAnimationProfile sanitizedProfile = animationProfile.Sanitize();
        if (string.IsNullOrWhiteSpace(sanitizedProfile.IdleKey) ||
            string.IsNullOrWhiteSpace(sanitizedProfile.MoveKey) ||
            !Mathf.Approximately(sanitizedProfile.MoveThreshold, animationProfile.MoveThreshold))
        {
            valid = false;
        }

        if (selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                if (selfTags[i] == null || selfTags[i].GetTag() == null)
                {
                    valid = false;
                    break;
                }
            }
        }

        if (!valid && !warned)
        {
            string role = isFallback ? "fallback" : "candidate";
            Debug.LogWarning($"[ActorLocomotion] Invalid {role} LocomotionModeAsset '{name}'.", context);
            warned = true;
        }

        return valid;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        tuning = LocomotionTuning.Sanitize(tuning);
        animationProfile = animationProfile.Sanitize();

        if (HasNullEntryCondition())
        {
            Debug.LogWarning(
                $"[ActorLocomotion] LocomotionModeAsset '{name}' contains a null EntryCondition and cannot be used as a candidate.",
                this);
        }
    }
#endif

    private bool HasNullEntryCondition()
    {
        if (entryConditions == null)
            return false;

        for (int i = 0; i < entryConditions.Count; i++)
        {
            if (entryConditions[i] == null)
                return true;
        }

        return false;
    }
}

[Serializable]
public struct LocomotionTuning
{
    public float MoveSpeed;
    public float AirControlFactor;
    public float RotateSpeed;

    public static LocomotionTuning Default => new LocomotionTuning
    {
        MoveSpeed = 5f,
        AirControlFactor = 0.4f,
        RotateSpeed = 600f,
    };

    public static LocomotionTuning Sanitize(LocomotionTuning value)
    {
        return new LocomotionTuning
        {
            MoveSpeed = SanitizeNonNegative(value.MoveSpeed),
            AirControlFactor = Mathf.Clamp01(SanitizeFinite(value.AirControlFactor, Default.AirControlFactor)),
            RotateSpeed = SanitizeNonNegative(value.RotateSpeed),
        };
    }

    private static float SanitizeNonNegative(float value)
    {
        return Mathf.Max(0f, SanitizeFinite(value, 0f));
    }

    private static float SanitizeFinite(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}

[Serializable]
public struct LocomotionAnimationProfile
{
    public string IdleKey;
    public string MoveKey;
    public float MoveThreshold;
    public LocomotionAnimationParameterSource ParameterSource;

    public static LocomotionAnimationProfile Default => new LocomotionAnimationProfile
    {
        IdleKey = string.Empty,
        MoveKey = string.Empty,
        MoveThreshold = 0.01f,
        ParameterSource = LocomotionAnimationParameterSource.None,
    };

    public LocomotionAnimationProfile Sanitize()
    {
        return new LocomotionAnimationProfile
        {
            IdleKey = IdleKey ?? string.Empty,
            MoveKey = MoveKey ?? string.Empty,
            MoveThreshold = float.IsNaN(MoveThreshold) || float.IsInfinity(MoveThreshold)
                ? 0f
                : Mathf.Max(0f, MoveThreshold),
            ParameterSource = ParameterSource,
        };
    }
}

public enum LocomotionAnimationParameterSource
{
    None = 0,
    MoveStrength1D = 1,
    LocalDirection2D = 2,
}
