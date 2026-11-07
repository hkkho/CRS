using System;
using System.Linq;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeScoringTests
{
    private static readonly double[] MixedBurnups = { 1.0, 6.0, 9.0 };

    [Theory]
    [InlineData(1.0799, 0.5)]
    [InlineData(1.08, 0.25)]
    [InlineData(1.09, 0.25)]
    [InlineData(1.10, 0.25)]
    [InlineData(1.1001, 0.0)]
    public void PeakRippleHasThreeBonusTiers(double maximumRipple, double expected)
    {
        Assert.Equal(expected, PracticeScoring.RipplePointsPerHour(maximumRipple));
    }

    [Fact]
    public void OperatingPointsCombineRippleAndAbsoluteTiltOverSimulatedTime()
    {
        Assert.Equal(24, PracticeScoring.OperatingPoints(86400, 1, 0));
        Assert.Equal(0.25, PracticeScoring.OperatingPoints(3600, 1.11, 0.10));
        Assert.Equal(0.25, PracticeScoring.OperatingPoints(3600, 1.11, -0.10));
        Assert.Equal(0, PracticeScoring.OperatingPoints(3600, 1.11, 0.20));
        Assert.Equal(0, PracticeScoring.OperatingPoints(0, 1, 0));
    }

    [Fact]
    public void DischargeRewardsHigherBurnupAndPenalizesEachEarlyBundle()
    {
        Assert.Equal(-6, PracticeScoring.DischargeBundlePoints(0));
        Assert.Equal(0, PracticeScoring.DischargeBundlePoints(6));
        Assert.Equal(2, PracticeScoring.DischargeBundlePoints(8));
        Assert.Equal(4, PracticeScoring.DischargeBundlePoints(10));
        var breakdown = PracticeScoring.DescribeDischarge(MixedBurnups);
        Assert.Equal(PracticeScoring.PolicyId, breakdown.PolicyId);
        Assert.Equal(3, breakdown.DischargeReward);
        Assert.Equal(5, breakdown.FreshFuelCost);
        Assert.Equal(-2, breakdown.NetPoints);
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
        Assert.All(initial.Ripple.ReferenceChannelPowerWatts, p => Assert.InRange(p, 0, 7_100_000));
        var moved = session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.True(moved.Accepted, moved.DiagnosticMessage);
        var score = moved.Snapshot.LastRefuellingScore!;
        Assert.Equal(score.NetPoints, moved.Snapshot.ScoreTotal);
        Assert.Equal(2 * (1 - moved.Snapshot.Rrs.Zones.Average(z => Math.Abs(z.FillFraction - 0.5)) / 0.5),
            score.ZoneBalanceBonus, 12);
        Assert.Equal(initial.Ripple.ReferenceChannelPowerWatts, moved.Snapshot.Ripple.ReferenceChannelPowerWatts);
        // Integrate each retained-shape interval; the rate now refreshes every
        // three simulated minutes rather than staying fixed for this run.
        double expected = score.NetPoints;
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
        Assert.Equal(advanced.Snapshot.Ripple.ChannelRippleFractions.Max(), advanced.Snapshot.Ripple.MaximumRipple);
        Assert.Equal(PracticeScoring.OperatingPoints(3600, advanced.Snapshot.Ripple.MaximumRipple,
            advanced.Snapshot.AxialTiltFraction), advanced.Snapshot.Ripple.PointsPerHour);
        session.Pause();
        var paused = session.AdvanceWallMilliseconds(1000);
        Assert.Equal(advanced.Snapshot.ScoreTotal, paused.Snapshot.ScoreTotal);
    }
}
