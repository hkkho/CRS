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
}
