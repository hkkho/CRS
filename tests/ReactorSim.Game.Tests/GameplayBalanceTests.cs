using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class GameplayBalanceTests
{
    [Fact]
    public void PoliciesAreRankedByRippleForTheSameSeedAndDuration()
    {
        const ulong seed = 1002;
        var idle = PracticeGameSessionFactory.Create(seed);
        var useful = PracticeGameSessionFactory.Create(seed);
        var waste = PracticeGameSessionFactory.Create(seed);
        uint channel = useful.Snapshot.Core.Channels.OrderByDescending(c => c.AverageBurnupMwDayPerKg).First().ChannelIndex;
        string productive = useful.Snapshot.Core.GetChannel(channel).FlowDirection == FlowDirection.EndAtoEndB
            ? "toward-end-b" : "toward-end-a";
        Assert.True(useful.RefuelChannel(channel, productive, 8, "NAT-U-SYNTHETIC").Accepted);
        for (int move = 0; move < 5; move++)
            Assert.True(waste.RefuelChannel(channel, productive, 8, "NAT-U-SYNTHETIC").Accepted);
        foreach (var session in new[] { idle, useful, waste })
        {
            double expected = 0;
            foreach (ulong milliseconds in new ulong[] { 18_000, 18_000, 18_000, 6_000 })
            {
                double beforeTime = session.Snapshot.SimulationTimeSeconds;
                double rate = session.Snapshot.Ripple.PointsPerHour;
                var result = session.AdvanceWallMilliseconds(milliseconds);
                Assert.True(result.Accepted, result.DiagnosticMessage);
                expected += rate * (result.Snapshot.SimulationTimeSeconds - beforeTime) / 3600;
            }
            Assert.Equal(600, session.Snapshot.SimulationTimeSeconds);
            Assert.InRange(session.Snapshot.Shift.OperatingPoints, 0, 600.0 / 3600.0);
            Assert.Equal(expected, session.Snapshot.ScoreTotal, 10);
        }
        Assert.Equal(8u, useful.Snapshot.Shift.FuelConsumed);
        Assert.Equal(40u, waste.Snapshot.Shift.FuelConsumed);
        Assert.Equal(344, useful.Snapshot.Shift.ThermalEnergyMwh, 6);
        Assert.Equal(idle.Snapshot.Shift.ThermalEnergyMwh, useful.Snapshot.Shift.ThermalEnergyMwh, 6);
    }
}
