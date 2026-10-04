using System;
using System.Linq;
using System.Reflection;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PowerLimitGameSessionTests
{
    [Fact]
    public void AnAlreadyOverpoweredCoreIsTerminalAndRejectsFurtherOperations()
    {
        var session = PracticeGameSessionFactory.Create();
        SetTestPowerAmplitude(session, 1.5);
        var snapshot = session.Snapshot;
        Assert.True(snapshot.Core.Channels.Max(c => c.PowerWatts) > 7_300_000);
        Assert.True(snapshot.IsGameOver);
        Assert.Equal("Channel power exceeds 7,300 kW", snapshot.GameOverReason);
        Assert.False(snapshot.Rrs.IsGameOver);
        Assert.Equal("ended", snapshot.Shift.Outcome);
        Assert.False(snapshot.Shift.RewardEarned);
        Assert.False(session.AdvanceWallMilliseconds(100).Accepted);
        Assert.False(session.RefuelChannel(75, "toward-end-a", 8, "NAT-U-SYNTHETIC").Accepted);
        Assert.False(session.Resume().Accepted);
        Assert.Equal(snapshot.SimulationTimeSeconds, session.Snapshot.SimulationTimeSeconds);
        Assert.Equal(snapshot.FreshBundlesAvailable, session.Snapshot.FreshBundlesAvailable);
    }

    [Fact]
    public void AppliedPowerTargetTripsOnTheFirstTickWithoutWaitingForASpatialSolve()
    {
        var session = PracticeGameSessionFactory.Create();
        SetTestPowerAmplitude(session, 0.4);
        Assert.False(session.Snapshot.IsGameOver);
        var projection = session.CurrentEquilibriumProjection;
        Assert.True(session.QueuePowerTarget(1.2).Accepted);
        Assert.False(session.Snapshot.IsGameOver); // Queued power is not yet applied.
        var result = session.AdvanceWallMilliseconds(5_000);
        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.True(result.Snapshot.IsGameOver);
        Assert.Equal("Channel power exceeds 7,300 kW", result.Snapshot.GameOverReason);
        Assert.Equal(0.1, result.Snapshot.WallElapsedSeconds, 10);
        Assert.Equal(1, result.Snapshot.SimulationTimeSeconds, 10);
        Assert.Same(projection, session.CurrentEquilibriumProjection);
        Assert.Equal(1.2, result.Snapshot.Physics.PowerAmplitude, 10);
        Assert.False(session.AdvanceWallMilliseconds(100).Accepted);
        Assert.Equal(result.Snapshot.ScoreTotal, session.Snapshot.ScoreTotal);
        Assert.Equal(result.Snapshot.Shift.ThermalEnergyMwh, session.Snapshot.Shift.ThermalEnergyMwh);
    }

    [Fact]
    public void PausedRefuellingBelowTheLimitsRetainsActualPowerScalingAndTimestamp()
    {
        var session = PracticeGameSessionFactory.Create();
        var projection = session.CurrentEquilibriumProjection;
        double safeAmplitude = 0.9 * Math.Min(1, Math.Min(7_300_000 / projection.ShapeChannelPowerWatts.Max(),
            935_000 / projection.ShapeNodePowerWatts.Max()));
        SetTestPowerAmplitude(session, safeAmplitude);
        Assert.False(session.Snapshot.IsGameOver);
        Assert.True(session.Pause().Accepted);
        var before = session.Snapshot;
        var result = session.RefuelChannel(75, "toward-end-a", 8, "NAT-U-SYNTHETIC");
        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.False(result.Snapshot.IsGameOver);
        Assert.All(result.Snapshot.Core.Channels, channel =>
        {
            Assert.InRange(channel.PowerWatts, 0, 7_300_000);
            Assert.All(channel.Bundles, bundle => Assert.InRange(bundle.PowerWatts, 0, 935_000));
        });
        Assert.Equal(before.SimulationTimeSeconds, result.Snapshot.SimulationTimeSeconds);
        Assert.Equal(before.ScoreTotal, result.Snapshot.ScoreTotal);
        Assert.Equal(before.Shift.ThermalEnergyMwh, result.Snapshot.Shift.ThermalEnergyMwh);
        Assert.Equal(before.FreshBundlesAvailable - 8, result.Snapshot.FreshBundlesAvailable);
        Assert.Equal(safeAmplitude, result.Snapshot.Physics.PowerAmplitude);
    }

    private static void SetTestPowerAmplitude(GameSession session, double amplitude)
    {
        // Fixture-only state injection isolates actual-power scaling and the
        // transition across the caps without changing fuel or spatial shape.
        var clock = (PracticeRunClock)typeof(GameSession).GetField("_runtime",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        typeof(PracticeRunClock).GetField("<NormalizedPowerFraction>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clock, amplitude);
    }
}
