using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeOperatingLimitsTests
{
    [Theory]
    [InlineData(-0.20001, true)]
    [InlineData(-0.20, false)]
    [InlineData(0.0, false)]
    [InlineData(0.20, false)]
    [InlineData(0.20001, true)]
    public void GlobalTiltLimitAppliesToBothSignsAndAllowsTheBoundary(double tilt, bool terminal)
        => Assert.Equal(terminal, PracticeOperatingLimits.EndReason(0.5, tilt).Length > 0);

    [Theory]
    [InlineData(7_100_000, 900_000, "")]
    [InlineData(7_100_000.001, 900_000, "Channel power exceeds 7,100 kW")]
    [InlineData(7_100_000, 900_000.001, "Bundle power exceeds 900 kW")]
    [InlineData(7_100_001, 900_001, "Channel power exceeds 7,100 kW")]
    [InlineData(0, 900_001, "Bundle power exceeds 900 kW")]
    public void PowerLimitsAreIndependentAndAllowExactBoundaries(double channelWatts,
        double bundleWatts, string reason)
        => Assert.Equal(reason, PracticeOperatingLimits.EndReason(0.5, 0, channelWatts, bundleWatts));
}
