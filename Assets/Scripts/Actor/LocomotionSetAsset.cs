using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSample/Locomotion/Set", fileName = "LocomotionSet")]
public sealed class LocomotionSetAsset : LocomotionAsset
{
    [SerializeField, Min(0f)] private float transitionBlendDuration = 0.1f;
    [SerializeField] private List<LocomotionStartEntry> start = new();
    [SerializeField] private List<LocomotionStopEntry> stop = new();
    [SerializeField] private List<LocomotionPivotEntry> pivot = new();

    public float TransitionBlendDuration => transitionBlendDuration;
    public IReadOnlyList<LocomotionStartEntry> Start => start;
    public IReadOnlyList<LocomotionStopEntry> Stop => stop;
    public IReadOnlyList<LocomotionPivotEntry> Pivot => pivot;

    public override LocomotionRuntime CreateRuntime() => new LocomotionSetRuntime(this);

    protected override bool ValidateSpecificRuntimeConfig() =>
        LocomotionDataValidation.IsFinite(transitionBlendDuration) && transitionBlendDuration >= 0f;

    protected override void CollectSpecificAnimationCoverageIssues(List<string> issues)
    {
        CheckStart(start, issues);
        CheckStop(stop, issues);
        CheckPivot(pivot, issues);
    }

    private static void CheckStart(List<LocomotionStartEntry> entries, List<string> issues)
    {
        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            LocomotionStartEntry entry = entries[i];
            if (entry == null || !LocomotionDataValidation.IsValidDirection(entry.TargetLocalDirection))
                issues.Add($"Start[{i}] has an invalid target direction.");
            LocomotionDataValidation.CheckAnimation(entry?.Animation, $"Start[{i}]", issues, false);
        }
    }

    private static void CheckStop(List<LocomotionStopEntry> entries, List<string> issues)
    {
        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            LocomotionStopEntry entry = entries[i];
            if (entry == null || !LocomotionDataValidation.IsValidDirection(entry.SourceLocalDirection))
                issues.Add($"Stop[{i}] has an invalid source direction.");
            LocomotionDataValidation.CheckAnimation(entry?.Animation, $"Stop[{i}]", issues, false);
        }
    }

    private static void CheckPivot(List<LocomotionPivotEntry> entries, List<string> issues)
    {
        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            LocomotionPivotEntry entry = entries[i];
            if (entry == null || !LocomotionDataValidation.IsValidDirection(entry.SourceLocalDirection)
                || !LocomotionDataValidation.IsValidDirection(entry.TargetLocalDirection))
            {
                issues.Add($"Pivot[{i}] has invalid source/target directions.");
            }

            LocomotionDataValidation.CheckAnimation(entry?.Animation, $"Pivot[{i}]", issues, false);
        }
    }
}

[Serializable]
public sealed class LocomotionStartEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 targetLocalDirection = Vector2.up;

    public AnimationAsset Animation => animation;
    public Vector2 TargetLocalDirection => targetLocalDirection;
}

[Serializable]
public sealed class LocomotionStopEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 sourceLocalDirection = Vector2.up;

    public AnimationAsset Animation => animation;
    public Vector2 SourceLocalDirection => sourceLocalDirection;
}

[Serializable]
public sealed class LocomotionPivotEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 sourceLocalDirection = Vector2.up;
    [SerializeField] private Vector2 targetLocalDirection = Vector2.down;

    public AnimationAsset Animation => animation;
    public Vector2 SourceLocalDirection => sourceLocalDirection;
    public Vector2 TargetLocalDirection => targetLocalDirection;
    public float SignedAngle => Vector2.SignedAngle(sourceLocalDirection, targetLocalDirection);
}
