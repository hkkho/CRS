using System;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class DischargeBurnupHistoryTests
{
    [Fact]
    public void DischargePeaksUseActualRemovedFuelAndSurviveRejectedCommands()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        Assert.Null(session.Snapshot.LastRefuellingScore);
        Assert.Equal(PracticeScoring.PolicyId, session.Snapshot.ScorePolicyId);
        Assert.Null(session.Snapshot.LastDischargedMaximumBurnupMwDayPerKg);
        Assert.Null(session.Snapshot.MaximumDischargedBurnupMwDayPerKg);
        var expected = session.CoreState.TryRefuel(189, GameRefuellingDirectionV1.TowardEndA,
            8, "NAT-U-SYNTHETIC", 0.0);
        Assert.True(expected.IsValid);
        double peak = expected.Value.DischargedBundles.Max(bundle =>
            bundle.CurrentBurnupJPerKgHm / GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
        var accepted = session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.True(accepted.Accepted, accepted.DiagnosticMessage);
        var score = accepted.Snapshot.LastRefuellingScore!;
        Assert.Equal(0.0, score.FreshFuelCost);
        Assert.Equal(expected.Value.DischargedBundles.Sum(bundle => PracticeScoring.DischargeBundlePoints(
            bundle.CurrentBurnupJPerKgHm / GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram)), score.NetPoints);
        Assert.Equal(score.NetPoints, accepted.Snapshot.ScoreTotal);
        Assert.Equal(peak, accepted.Snapshot.LastDischargedMaximumBurnupMwDayPerKg);
        Assert.Equal(peak, accepted.Snapshot.MaximumDischargedBurnupMwDayPerKg);

        var rejected = session.RefuelChannel(189, "toward-end-a", 8, "UNSUPPORTED");
        Assert.False(rejected.Accepted);
        Assert.Same(score, rejected.Snapshot.LastRefuellingScore);
        Assert.Equal(peak, rejected.Snapshot.LastDischargedMaximumBurnupMwDayPerKg);
        Assert.Equal(peak, rejected.Snapshot.MaximumDischargedBurnupMwDayPerKg);

        var second = session.RefuelChannel(189, "toward-end-a", 8, "NAT-U-SYNTHETIC");
        Assert.True(second.Accepted, second.DiagnosticMessage);
        Assert.Equal(0.0, second.Snapshot.LastRefuellingScore!.NetPoints);
        Assert.Equal(0.0, second.Snapshot.LastRefuellingScore.FreshFuelCost);
        Assert.Null(PracticeGameSessionFactory.CreateBrowserPlaytest().Snapshot.LastRefuellingScore);
        Assert.Equal(Math.Max(peak, second.Snapshot.LastDischargedMaximumBurnupMwDayPerKg!.Value),
            second.Snapshot.MaximumDischargedBurnupMwDayPerKg);
        Assert.Null(PracticeGameSessionFactory.CreateBrowserPlaytest().Snapshot.MaximumDischargedBurnupMwDayPerKg);
    }
}
