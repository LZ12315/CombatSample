internal enum BattleOutcome
{
    InProgress,
    Defeat,
    Victory
}

/// <summary>One-on-one battle result. Player death wins ties, independent of hit callback order.</summary>
internal static class BattleOutcomeRules
{
    public static BattleOutcome Resolve(bool playerIsDead, bool enemyIsDead)
    {
        if (playerIsDead)
            return BattleOutcome.Defeat;
        return enemyIsDead ? BattleOutcome.Victory : BattleOutcome.InProgress;
    }
}
