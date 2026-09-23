using System;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class LiveCoreDesignerGameSessionTests
{
    private const uint Channel = 180;
    private const uint RefuelChannel = 190;
    private const uint Position = 5;

    [Fact]
    public void ConfiguringLiveCellChangesTheAuthoritativeCoreSolve()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionSnapshot before = session.Snapshot;
        double beforeFlux = session.GetCellGroup1Flux(Channel, Position);
        double beforeChannelPower = before.Core.GetChannel(Channel).PowerWatts;

        GameSessionCommandResult result = session.ConfigureCell(
            Channel,
            Position,
            false,
            new[] { TopologyFace.East });

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.False(session.IsFuelCell(Channel, Position));
        Assert.Contains(TopologyFace.East, session.GetReflectiveFaces(Channel, Position));
        Assert.NotEqual(before.Physics.EffectiveK, result.Snapshot.Physics.EffectiveK);
        Assert.NotEqual(before.Rrs.CoreReactivity, result.Snapshot.Rrs.CoreReactivity);
        Assert.NotEqual(beforeFlux, session.GetCellGroup1Flux(Channel, Position));
        Assert.NotEqual(beforeChannelPower, result.Snapshot.Core.GetChannel(Channel).PowerWatts);

        Candu6GridPositionV1 selected = Candu6CoreTopologyFactoryV1.GetPosition(Channel);
        Assert.True(
            Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                selected.Column + 1,
                selected.DisplayRow,
                out uint eastChannel));
        Assert.Contains(
            TopologyFace.West,
            session.GetReflectiveFaces(eastChannel, Position));
    }

    [Fact]
    public void LiveCoreConfigurationSurvivesRefuelAndHourlyRecompute()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionCommandResult configured = session.ConfigureCell(
            Channel,
            Position,
            false,
            new[] { TopologyFace.East });
        Assert.True(configured.Accepted, configured.DiagnosticMessage);
        uint freshBefore = configured.Snapshot.FreshBundlesAvailable;

        GameSessionCommandResult refuel = session.RefuelChannel(
            RefuelChannel,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");
        Assert.True(refuel.Accepted, refuel.DiagnosticMessage);

        GameSessionCommandResult advanced = session.AdvanceWallMilliseconds(2_000);

        Assert.True(advanced.Accepted, advanced.DiagnosticMessage);
        Assert.Equal(3_600.0, advanced.Snapshot.SimulationTimeSeconds);
        Assert.Equal(freshBefore - 4u, advanced.Snapshot.FreshBundlesAvailable);
        Assert.Equal(1u, advanced.Snapshot.RefuellingOperationCount);
        Assert.False(session.IsFuelCell(Channel, Position));
        Assert.NotEmpty(session.GetReflectiveFaces(Channel, Position));
        Assert.True(double.IsFinite(session.GetCellGroup1Flux(Channel, Position)));
        Assert.True(double.IsFinite(session.GetCellGroup2Flux(Channel, Position)));
    }

    [Fact]
    public void NonfuelCellBlocksChannelRefuelUntilFuelIsRestored()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionCommandResult configured = session.ConfigureCell(
            Channel,
            Position,
            false,
            Array.Empty<TopologyFace>());
        Assert.True(configured.Accepted, configured.DiagnosticMessage);

        GameSessionSnapshot beforeRejectedRefuel = session.Snapshot;
        EquilibriumCoreProjectionV1 beforeRejectedProjection =
            session.CurrentEquilibriumProjection;
        GameSessionCommandResult rejected = session.RefuelChannel(
            Channel,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");

        Assert.False(rejected.Accepted);
        Assert.Equal("GameSession.Refuelling.Channel.Nonfuel", rejected.DiagnosticCode);
        Assert.Equal(
            beforeRejectedRefuel.SimulationTimeSeconds,
            rejected.Snapshot.SimulationTimeSeconds);
        Assert.Equal(beforeRejectedRefuel.ScoreTotal, rejected.Snapshot.ScoreTotal);
        Assert.Equal(
            beforeRejectedRefuel.FreshBundlesAvailable,
            rejected.Snapshot.FreshBundlesAvailable);
        Assert.Equal(
            beforeRejectedRefuel.RefuellingOperationCount,
            rejected.Snapshot.RefuellingOperationCount);
        Assert.Same(beforeRejectedProjection, session.CurrentEquilibriumProjection);

        GameSessionCommandResult restored = session.ConfigureCell(
            Channel,
            Position,
            true,
            Array.Empty<TopologyFace>());
        Assert.True(restored.Accepted, restored.DiagnosticMessage);
        Assert.True(session.IsFuelCell(Channel, Position));

        GameSessionCommandResult accepted = session.RefuelChannel(
            Channel,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");
        Assert.True(accepted.Accepted, accepted.DiagnosticMessage);
        Assert.Equal(
            beforeRejectedRefuel.FreshBundlesAvailable - 4u,
            accepted.Snapshot.FreshBundlesAvailable);
        Assert.Equal(1u, accepted.Snapshot.RefuellingOperationCount);
    }

    [Fact]
    public void InvalidLiveCoreConfigurationRollsBackAtomically()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionSnapshot before = session.Snapshot;
        EquilibriumCoreProjectionV1 beforeProjection = session.CurrentEquilibriumProjection;
        bool beforeFuel = session.IsFuelCell(Channel, Position);
        TopologyFace[] beforeFaces = session.GetReflectiveFaces(Channel, Position).ToArray();

        GameSessionCommandResult rejected = session.ConfigureCell(
            Candu6CoreTopologyFactoryV1.ChannelCount,
            Position,
            false,
            new[] { TopologyFace.East });

        Assert.False(rejected.Accepted);
        Assert.NotEmpty(rejected.DiagnosticCode);
        Assert.Equal(before.SimulationTimeSeconds, rejected.Snapshot.SimulationTimeSeconds);
        Assert.Equal(before.ScoreTotal, rejected.Snapshot.ScoreTotal);
        Assert.Equal(before.FreshBundlesAvailable, rejected.Snapshot.FreshBundlesAvailable);
        Assert.Equal(before.RefuellingOperationCount, rejected.Snapshot.RefuellingOperationCount);
        Assert.Same(beforeProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(beforeFuel, session.IsFuelCell(Channel, Position));
        Assert.Equal(beforeFaces, session.GetReflectiveFaces(Channel, Position));
    }

    [Fact]
    public void DefaultLiveCoreIsAllFuelWithNoReflectiveOverrides()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();

        Assert.True(session.IsFuelCell(Channel, Position));
        Assert.Empty(session.GetReflectiveFaces(Channel, Position));
        Assert.True(session.GetCellGroup1Flux(Channel, Position) > 0.0);
        Assert.True(session.GetCellGroup2Flux(Channel, Position) > 0.0);

        GameSessionSnapshot before = session.Snapshot;
        GameSessionCommandResult solved = session.SolveConfiguredCore();

        Assert.True(solved.Accepted, solved.DiagnosticMessage);
        Assert.Equal(before.SimulationTimeSeconds, solved.Snapshot.SimulationTimeSeconds);
        Assert.Equal(before.ScoreTotal, solved.Snapshot.ScoreTotal);
        Assert.Equal(before.FreshBundlesAvailable, solved.Snapshot.FreshBundlesAvailable);
        Assert.Equal(before.RefuellingOperationCount, solved.Snapshot.RefuellingOperationCount);
        Assert.True(session.IsFuelCell(Channel, Position));
        Assert.Empty(session.GetReflectiveFaces(Channel, Position));
    }
}
