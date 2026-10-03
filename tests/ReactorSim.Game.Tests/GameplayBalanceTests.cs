using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class GameplayBalanceTests
{
    [Fact]
    public void UsefulDischargeBeatsWaitingAndRepeatedFreshReversalForTheSameSeedAndDuration()
    {
        const ulong seed = 1002;
        var idle = PracticeGameSessionFactory.Create(seed);
        var useful = PracticeGameSessionFactory.Create(seed);
        var waste = PracticeGameSessionFactory.Create(seed);
        uint channel = useful.Snapshot.Core.Channels.OrderByDescending(c => c.AverageBurnupMwDayPerKg).First().ChannelIndex;
        var choices = new[] { GameRefuellingDirectionV1.TowardEndA, GameRefuellingDirectionV1.TowardEndB }
            .Select(direction => new
            {
                Direction = direction,
                Move = useful.CoreState.TryRefuel(channel, direction, 8, "NAT-U-SYNTHETIC", 0)
            })
            .OrderByDescending(choice => PracticeScoring.DescribeDischarge(choice.Move.Value.DischargedBundles.Select(
                bundle => bundle.CurrentBurnupJPerKgHm / 86_400_000_000.0)).NetPoints).ToArray();
        string productive = choices[0].Direction == GameRefuellingDirectionV1.TowardEndA ? "toward-end-a" : "toward-end-b";
        string reverse = productive == "toward-end-a" ? "toward-end-b" : "toward-end-a";
        Assert.True(useful.RefuelChannel(channel, productive, 8, "NAT-U-SYNTHETIC").Accepted);
        foreach (string direction in new[] { productive, reverse, productive, reverse })
            Assert.True(waste.RefuelChannel(channel, direction, 8, "NAT-U-SYNTHETIC").Accepted);
        foreach (var session in new[] { idle, useful, waste })
        {
            var result = session.AdvanceWallMilliseconds(60_000);
            Assert.True(result.Accepted, result.DiagnosticMessage);
            Assert.Equal(600, result.Snapshot.SimulationTimeSeconds);
            Assert.InRange(result.Snapshot.Shift.OperatingPoints, 0, 600.0 / 3600.0);
        }
        Assert.True(useful.Snapshot.ScoreTotal > idle.Snapshot.ScoreTotal);
        Assert.True(idle.Snapshot.ScoreTotal > waste.Snapshot.ScoreTotal);
        Assert.Equal(8u, useful.Snapshot.Shift.FuelConsumed);
        Assert.Equal(32u, waste.Snapshot.Shift.FuelConsumed);
        Assert.Equal(344, useful.Snapshot.Shift.ThermalEnergyMwh, 6);
        Assert.Equal(idle.Snapshot.Shift.ThermalEnergyMwh, useful.Snapshot.Shift.ThermalEnergyMwh, 6);
    }
}
