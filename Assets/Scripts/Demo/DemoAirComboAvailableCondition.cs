using System;

/// <summary>Demo-only requirement for AirCombo_1; never consumes the allowance during evaluation.</summary>
[Serializable]
[AddTypeMenu("Demo/Air Combo Available")]
public sealed class DemoAirComboAvailableCondition : ActionCondition
{
    // Missing/disabled/unconfigured trackers must fail even when inversion is enabled.
    protected override bool CanEvaluate(Actor actor) =>
        actor.TryGetComponent<DemoAirComboUsage>(out var usage) && usage.IsReady;

    protected override bool OnCheck(Actor actor) =>
        actor.TryGetComponent<DemoAirComboUsage>(out var usage) && usage.CanStartAirCombo;
}
