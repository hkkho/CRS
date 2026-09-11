using System;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class LiquidZoneRrsGameSessionTests
{
    [Fact]
    public void ShortTickReusesProjectionWhileHourlyBoundaryRunsStaticRrs()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var initialProjection = session.CurrentEquilibriumProjection;
        double[] initialFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();

        GameSessionCommandResult shortTick = session.AdvanceWallMilliseconds(100);

        Assert.True(shortTick.Accepted, shortTick.DiagnosticMessage);
        Assert.Same(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(initialFills, session.CurrentLiquidZoneRrs.ZoneFills);

        GameSessionCommandResult boundary = session.AdvanceWallMilliseconds(1_900);

        Assert.True(boundary.Accepted, boundary.DiagnosticMessage);
        Assert.NotSame(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(3_600.0, boundary.Snapshot.SimulationTimeSeconds);
        Assert.InRange(session.CurrentLiquidZoneRrs.ControllerIterationCount, 1, 8);
    }

    [Fact]
    public void RefuelRunsDeterministicBoundedControlAndRejectedCommandRollsBackRrs()
    {
        GameSession first = PracticeGameSessionFactory.Create();
        GameSession second = PracticeGameSessionFactory.Create();

        GameSessionCommandResult firstResult = first.RefuelChannel(
            189, "toward-end-b", 4, "NAT-U-SYNTHETIC");
        GameSessionCommandResult secondResult = second.RefuelChannel(
            189, "toward-end-b", 4, "NAT-U-SYNTHETIC");

        Assert.True(firstResult.Accepted, firstResult.DiagnosticMessage);
        Assert.True(secondResult.Accepted, secondResult.DiagnosticMessage);
        Assert.Equal(
            first.CurrentLiquidZoneRrs.StateDigest,
            second.CurrentLiquidZoneRrs.StateDigest);
        Assert.Equal(
            first.CurrentEquilibriumProjection.ReactivityBindingDigest,
            second.CurrentEquilibriumProjection.ReactivityBindingDigest);
        Assert.InRange(first.CurrentLiquidZoneRrs.ControllerIterationCount, 1, 8);
        Assert.All(first.CurrentLiquidZoneRrs.ZoneFills, fill => Assert.InRange(fill, 0.0, 1.0));
        Assert.True(
            Math.Abs(first.CurrentLiquidZoneRrs.CompensatedNetReactivity) <=
            Math.Abs(first.CurrentLiquidZoneRrs.CoreReactivity));

        var acceptedRrs = first.CurrentLiquidZoneRrs;
        var acceptedProjection = first.CurrentEquilibriumProjection;
        GameSessionCommandResult rejected = first.RefuelChannel(
            189, "toward-end-b", 4, "UNSUPPORTED-FUEL");

        Assert.False(rejected.Accepted);
        Assert.Same(acceptedRrs, first.CurrentLiquidZoneRrs);
        Assert.Same(acceptedProjection, first.CurrentEquilibriumProjection);
    }
}
