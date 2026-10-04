using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class ShiftProgressTests
{
    [Theory]
    [InlineData(false, false, 8u, "in-progress", false)]
    [InlineData(true, false, 8u, "ended", false)]
    [InlineData(true, true, 7u, "missed", false)]
    [InlineData(true, true, 8u, "success", true)]
    public void ChallengeRewardRequiresBothUsefulDischargeAndHorizonCompletion(
        bool terminal, bool completed, uint useful, string outcome, bool rewarded)
    {
        var progress = new ShiftProgress(true, 1001, 86400, terminal ? 86400 : 3600,
            terminal, completed, 8, useful, 3_600_000_000, 42, 12, 130);
        Assert.Equal(outcome, progress.Outcome);
        Assert.Equal(rewarded, progress.RewardEarned);
        Assert.Equal(100, progress.OperatingPoints);
        Assert.Equal(1, progress.ThermalEnergyMwh);
        Assert.Equal(650.0 / 2064, progress.ElectricalEnergyMwhEstimate, 12);
    }

    [Fact]
    public void AcceptedFuelMovesAccumulateActualDischargeAndRejectedMovesRetainResults()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001, challenge: true);
        Assert.Equal(86400, session.Snapshot.Shift.HorizonSeconds);
        Assert.Equal(ShiftProgress.ChallengeId, session.Snapshot.Shift.Id);
        var channel = session.Snapshot.Core.Channels.OrderByDescending(c => c.AverageBurnupMwDayPerKg).First();
        var expectedUseful = session.Snapshot.RefuellingPlans.Single(p => p.ShiftCount == 8 && p.DirectionId ==
            (channel.FlowDirection == FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a")).DischargedPositions.Count(position =>
            session.CoreState.GetBundle(channel.ChannelIndex, (uint)position).CurrentBurnupJPerKgHm /
                86_400_000_000.0 >= ShiftProgress.UsefulBurnupThresholdMwDayPerKg);
        var moved = session.RefuelChannel(channel.ChannelIndex, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.True(moved.Accepted, moved.DiagnosticMessage);
        Assert.Equal(8u, moved.Snapshot.Shift.FuelConsumed);
        Assert.Equal((uint)expectedUseful, moved.Snapshot.Shift.UsefulBundlesDischarged);
        Assert.Equal(moved.Snapshot.LastRefuellingScore!.DischargeReward, moved.Snapshot.Shift.DischargeReward);
        Assert.Equal(moved.Snapshot.LastRefuellingScore.FreshFuelCost, moved.Snapshot.Shift.FreshFuelCost);
        var rejected = session.RefuelChannel(999, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.False(rejected.Accepted);
        Assert.Equal(moved.Snapshot.Shift.FuelConsumed, rejected.Snapshot.Shift.FuelConsumed);
        Assert.Equal(moved.Snapshot.Shift.DischargeReward, rejected.Snapshot.Shift.DischargeReward);
        Assert.Equal(0, rejected.Snapshot.Shift.ThermalEnergyMwh);
    }

    [Fact]
    public void EnergyUsesOnlyAcceptedRunTimeAndStopsAtTheHorizon()
    {
        var session = PracticeGameSessionFactory.Create();
        Assert.True(session.Pause().Accepted);
        Assert.True(session.AdvanceWallMilliseconds(1000).Accepted);
        Assert.Equal(0, session.Snapshot.Shift.ThermalEnergyMwh);
        Assert.True(session.Resume().Accepted);
        var completed = session.AdvanceWallMilliseconds(60_000);
        Assert.True(completed.Accepted, completed.DiagnosticMessage);
        Assert.Equal(0, completed.Snapshot.Shift.RemainingSeconds);
        Assert.Equal(344, completed.Snapshot.Shift.ThermalEnergyMwh, 6);
        Assert.Equal(650.0 / 6, completed.Snapshot.Shift.ElectricalEnergyMwhEstimate, 6);
        Assert.Equal("success", completed.Snapshot.Shift.Outcome);
        Assert.InRange(completed.Snapshot.ScoreTotal, 0, 600.0 / 3600.0);
        Assert.Equal(completed.Snapshot.ScoreTotal, completed.Snapshot.Shift.OperatingPoints);
        Assert.False(session.AdvanceWallMilliseconds(100).Accepted);
        Assert.Equal(completed.Snapshot.Shift.ThermalEnergyMwh, session.Snapshot.Shift.ThermalEnergyMwh);
        Assert.Equal(0, PracticeGameSessionFactory.Create().Snapshot.Shift.ThermalEnergyMwh);
    }
}
