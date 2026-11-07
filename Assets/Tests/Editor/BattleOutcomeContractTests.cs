using NUnit.Framework;

public sealed class BattleOutcomeContractTests
{
    [TestCase(false, false, (int)BattleOutcome.InProgress)]
    [TestCase(false, true, (int)BattleOutcome.Victory)]
    [TestCase(true, false, (int)BattleOutcome.Defeat)]
    [TestCase(true, true, (int)BattleOutcome.Defeat)]
    public void OneOnOneResult_RequiresEnemyDeathAndPlayerSurvival(
        bool playerIsDead, bool enemyIsDead, int expected)
    {
        Assert.AreEqual((BattleOutcome)expected, BattleOutcomeRules.Resolve(playerIsDead, enemyIsDead));
    }
}
