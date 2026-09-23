using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSample/Actor/Locomotion Mode", fileName = "LocomotionMode")]
public sealed class LocomotionModeAsset : ScriptableObject
{
    [SerializeField] private int priority;
    [SerializeReference, SubclassSelector] private List<LocomotionModeCondition> entryConditions = new();
    [SerializeField] private List<TagReference> selfTags = new();
    [SerializeField] private LocomotionTuning tuning = LocomotionTuning.Default;

    public int Priority => priority;
    public IReadOnlyList<LocomotionModeCondition> EntryConditions => entryConditions;
    public IReadOnlyList<TagReference> SelfTags => selfTags;
    public LocomotionTuning Tuning => LocomotionTuning.Sanitize(tuning);
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
