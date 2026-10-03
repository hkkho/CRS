using System;
using System.Linq;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeScoringTests
{
    private static readonly double[] MixedDischarge = { 0.0, 0.0, 20.0, 20.0 };

    [Fact]
    public void ProductiveDischargeMattersAtTheScaleOfAWholeChallengeDay()
    {
        double idealDay = PracticeScoring.OperatingPoints(86400, 1, 1);
        Assert.Equal(24, idealDay);
        Assert.Equal(idealDay, PracticeScoring.DescribeDischarge(Enumerable.Repeat(6.0, 8)).NetPoints);
        Assert.Equal(-12, PracticeScoring.DescribeDischarge(Enumerable.Repeat(0.0, 8)).NetPoints);
        Assert.Equal(0.7, PracticeScoring.OperatingPoints(3600, 1, 0), 12);
        Assert.Equal(0.3, PracticeScoring.OperatingPoints(3600, 0, 1), 12);
        Assert.Equal(0, PracticeScoring.OperatingPoints(3600, 0, 0));
        Assert.Equal(PracticeScoring.OperatingPoints(3600, 0.8, 0.6),
            PracticeScoring.OperatingPoints(1800, 0.8, 0.6) * 2, 12);
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeScoring.OperatingPoints(double.NaN, 1, 1));
    }
    [Fact]
    public void ExcessBurnupCannotOffsetFreshFuelWasteBeyondThePerBundleCap()
    {
        var breakdown = PracticeScoring.DescribeDischarge(MixedDischarge);
        Assert.Equal(PracticeScoring.PolicyId, breakdown.PolicyId);
        Assert.Equal(15.0, breakdown.DischargeReward);
        Assert.Equal(6.0, breakdown.FreshFuelCost);
        Assert.Equal(9.0, breakdown.NetPoints);
        Assert.Equal(9.0, MixedDischarge.Sum(PracticeScoring.DischargeBundlePoints));
        Assert.Equal(-6.0, Enumerable.Repeat(0.0, 4).Sum(PracticeScoring.DischargeBundlePoints));
        Assert.Equal(48.0, Enumerable.Repeat(10.0, 8).Sum(PracticeScoring.DischargeBundlePoints));
        Assert.Equal(48.0, Enumerable.Repeat(20.0, 8).Sum(PracticeScoring.DischargeBundlePoints));
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeScoring.DischargeBundlePoints(double.NaN));
    }
}
