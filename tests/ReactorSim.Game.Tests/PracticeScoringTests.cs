using System;
using System.Linq;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeScoringTests
{
    private static readonly double[] ZeroReference = { 0.0, 8.0 };
    private static readonly double[] ShortReference = { 8.0 };
    [Fact]
    public void ScoreMeasuresChannelDeviationEvenWhenTotalPowerAndTiltMatch()
    {
        double[] reference = { 2.0, 8.0 };
        double[] exact = { 2.0, 8.0 }, redistributed = { 2.4, 7.6 };
        Assert.Equal(0, PracticeScoring.RmsRipple(exact, reference));
        Assert.Equal(Math.Sqrt((0.2 * 0.2 + 0.05 * 0.05) / 2), PracticeScoring.RmsRipple(redistributed, reference), 12);
        Assert.Equal(24, PracticeScoring.OperatingPoints(86400, 0));
        Assert.Equal(0.8, PracticeScoring.OperatingPoints(3600, 0.05), 12);
        Assert.Equal(0.5, PracticeScoring.OperatingPoints(3600, 0.10), 12);
        Assert.Equal(0.2, PracticeScoring.OperatingPoints(3600, 0.20), 12);
        Assert.True(PracticeScoring.OperatingPoints(3600, 0.30) < PracticeScoring.OperatingPoints(3600, 0.20));
        Assert.Equal(PracticeScoring.OperatingPoints(3600, 0.08), PracticeScoring.OperatingPoints(1800, 0.08) * 2, 12);
        Assert.Equal(0.2, PracticeScoring.RmsRipple(exact, reference, 0.8), 12);
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeScoring.OperatingPoints(double.NaN, 0));
        Assert.Throws<ArgumentException>(() => PracticeScoring.RmsRipple(exact, ZeroReference));
        Assert.Throws<ArgumentException>(() => PracticeScoring.RmsRipple(exact, ShortReference));
    }
    [Fact]
    public void RefuellingNeverAwardsInstantPointsOrLetsBurnupHideRipple()
    {
        foreach (double burnup in new[] { 0.0, 6.0, 20.0 })
        {
            var breakdown = PracticeScoring.DescribeDischarge(Enumerable.Repeat(burnup, 8));
            Assert.Equal(PracticeScoring.PolicyId, breakdown.PolicyId);
            Assert.Equal(0, breakdown.NetPoints);
            Assert.Equal(0, breakdown.DischargeReward);
            Assert.Equal(0, breakdown.FreshFuelCost);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeScoring.DischargeBundlePoints(double.NaN));
    }
    [Fact]
    public void ReferenceIsFixedAcrossSeedsRefuellingAndTimeAndSnapshotsMatchScoring()
    {
        var session = PracticeGameSessionFactory.Create();
        var initial = session.Snapshot;
        var other = PracticeGameSessionFactory.Create(1002).Snapshot;
        Assert.Equal(initial.Ripple.ReferenceChannelPowerWatts, other.Ripple.ReferenceChannelPowerWatts);
        Assert.Equal(2_064_000_000, initial.Ripple.ReferenceChannelPowerWatts.Sum(), 3);
        Assert.All(initial.Ripple.ReferenceChannelPowerWatts, p => Assert.True(p > 0));
        Assert.True(initial.Ripple.ReferenceChannelPowerWatts.Max() > initial.Ripple.ReferenceChannelPowerWatts.Min());
        Assert.All(initial.Ripple.ReferenceChannelPowerWatts, p => Assert.InRange(p, 0, 7_300_000));
        var moved = session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.True(moved.Accepted, moved.DiagnosticMessage);
        Assert.Equal(0, moved.Snapshot.ScoreTotal);
        Assert.Equal(initial.Ripple.ReferenceChannelPowerWatts, moved.Snapshot.Ripple.ReferenceChannelPowerWatts);
        // Integrate each retained-shape interval; the rate now refreshes every
        // three simulated minutes rather than staying fixed for this run.
        double expected = 0;
        GameSessionCommandResult advanced = moved;
        foreach (ulong milliseconds in new ulong[] { 18_000, 18_000, 18_000, 6_000 })
        {
            double beforeTime = session.Snapshot.SimulationTimeSeconds;
            double rate = session.Snapshot.Ripple.PointsPerHour;
            advanced = session.AdvanceWallMilliseconds(milliseconds);
            Assert.True(advanced.Accepted, advanced.DiagnosticMessage);
            expected += rate * (advanced.Snapshot.SimulationTimeSeconds - beforeTime) / 3600;
        }
        Assert.Equal(expected, advanced.Snapshot.ScoreTotal, 10);
        Assert.Equal(PracticeScoring.RmsRipple(advanced.Snapshot.Core.Channels.Select(c => c.PowerWatts).ToArray(),
            initial.Ripple.ReferenceChannelPowerWatts), advanced.Snapshot.Ripple.RmsDeviationFraction, 12);
        session.Pause();
        var paused = session.AdvanceWallMilliseconds(1000);
        Assert.Equal(advanced.Snapshot.ScoreTotal, paused.Snapshot.ScoreTotal);
    }
}
