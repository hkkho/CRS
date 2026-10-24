using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class MultiDayObjectiveTests
{
    [Theory]
    [InlineData(ShiftObjective.ThreeDayId, 3, 0u, "Steady hands badge")]
    [InlineData(ShiftObjective.StewardshipId, 7, 48u, "Fuel steward badge")]
    public void AuthoredGoalsUseTheExistingFiniteStockAndRequireTheFullHorizon(string id, int days, uint useful, string reward)
    {
        var goal = ShiftObjective.ForId(id);
        Assert.Equal(days, goal.Days);
        var pending = Progress(id, false, false, useful);
        Assert.Equal("in-progress", pending.Outcome);
        Assert.False(pending.RewardEarned);
        Assert.Equal(128u, pending.FuelBudget);
        Assert.Equal(useful, pending.UsefulBundlesRequired);
        var finished = Progress(id, true, true, useful);
        Assert.Equal("success", finished.Outcome);
        Assert.True(finished.RewardEarned);
        Assert.Equal(reward, finished.Reward);
        Assert.Equal("ended", Progress(id, true, false, useful).Outcome);
        Assert.False(Progress(id, true, true, useful, false).RewardEarned);
    }

    [Theory]
    [InlineData(47u, "missed", false)]
    [InlineData(48u, "success", true)]
    public void StewardshipNeedsActualUsefulDischarges(uint useful, string outcome, bool earned)
    {
        var result = Progress(ShiftObjective.StewardshipId, true, true, useful);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(earned, result.RewardEarned);
    }

    [Fact]
    public void ThreeDayRunDoesNotEndAfterItsFirstDay()
    {
        var game = PracticeGameSessionFactory.CreateBrowserPlaytest(1001, ShiftObjective.ThreeDayId);
        var initial = game.Snapshot;
        Assert.Equal(3 * 86400, initial.Shift.HorizonSeconds);
        Assert.Equal(128u, initial.FreshBundlesAvailable);
        Assert.True(initial.IsPaused);
        Assert.Equal("standard-challenge", initial.Provenance.Kind);
        var day = game.CommitDay(0, new uint[] { 210 });
        Assert.True(day.Accepted, day.DiagnosticMessage);
        Assert.Equal(1u, day.Snapshot.CompletedDays);
        Assert.Equal("in-progress", day.Snapshot.Shift.Outcome);
        Assert.Equal(2 * 86400, day.Snapshot.Shift.RemainingSeconds);
        Assert.Equal(120u, day.Snapshot.FreshBundlesAvailable);
    }

    private static ShiftProgress Progress(string id, bool terminal, bool completed, uint useful, bool standard = true)
    {
        var goal = ShiftObjective.ForId(id);
        return new ShiftProgress(true, 1001, goal.Days * 86400, completed ? goal.Days * 86400 : 86400,
            terminal, completed, 64, useful, 0, 0, 0, 100, standard, false, id);
    }
}
