using System.Collections.Generic;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class GameSessionTests
{
    [Fact]
    public void PracticeSnapshotExposesDeterministicCoreChannelsAndBundles()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        GameSessionSnapshot snapshot = session.Snapshot;
        Assert.Equal(GameCorePresentationConstants.ChannelCount, snapshot.Core.ChannelCount);
        Assert.Equal(GameCorePresentationConstants.BundlePositionCount,
            (uint)snapshot.Core.GetChannel(0).Bundles.Count);
        Assert.Equal((uint)0, snapshot.Core.GetChannel(0).ChannelIndex);
        Assert.Equal((uint)379, snapshot.Core.GetChannel(379).ChannelIndex);
        Assert.NotEqual(
            snapshot.Core.GetChannel(0).GridRow,
            snapshot.Core.GetChannel(379).GridRow);
        Assert.Equal(FlowDirection.EndAtoEndB, snapshot.Core.GetChannel(0).FlowDirection);
        Assert.Equal(FlowDirection.EndBtoEndA, snapshot.Core.GetChannel(1).FlowDirection);

        HashSet<(int Column, int Row)> coordinates =
            new HashSet<(int Column, int Row)>();
        foreach (GameChannelPresentationSnapshot channel in snapshot.Core.Channels)
        {
            Assert.InRange(channel.GridColumn, 0, GameCorePresentationConstants.GridWidth - 1);
            Assert.InRange(channel.GridRow, 0, GameCorePresentationConstants.GridHeight - 1);
            Assert.True(
                coordinates.Add((channel.GridColumn, channel.GridRow)),
                $"Duplicate channel grid coordinate ({channel.GridColumn}, {channel.GridRow}).");
        }

        Assert.Equal((int)GameCorePresentationConstants.ChannelCount, coordinates.Count);
    }

    [Fact]
    public void PracticeSessionAdvancesAndAppliesPlayerCommands()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        Assert.Equal(0.0, session.Snapshot.SimulationTimeSeconds);
        Assert.Equal(PracticeGameSessionFactory.PlayPlaybackModeId, session.Snapshot.PlaybackModeId);

        GameSessionCommandResult queued = session.QueuePowerTarget(0.95);
        Assert.True(queued.Accepted);
        Assert.Equal(1u, queued.Snapshot.PendingActionCount);

        GameSessionCommandResult advanced = session.AdvanceWallMilliseconds(100);
        Assert.True(advanced.Accepted);
        Assert.Equal(1.0, advanced.Snapshot.SimulationTimeSeconds);
        Assert.Equal(0.95, advanced.Snapshot.NormalizedPowerFraction);

        Assert.True(session.Pause().Accepted);
        Assert.Equal(1.0, session.AdvanceWallMilliseconds(100).Snapshot.SimulationTimeSeconds);
        Assert.True(session.Resume().Accepted);
    }

    [Fact]
    public void SelectingLivePlaybackModeAfterPauseResumesTheSession()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        Assert.True(session.Pause().Accepted);
        GameSessionCommandResult mode = session.SetPlaybackMode(
            PracticeGameSessionFactory.RealTimePlaybackModeId);

        Assert.True(mode.Accepted, mode.DiagnosticMessage);
        Assert.False(mode.Snapshot.IsPaused);
        Assert.Equal(PracticeGameSessionFactory.RealTimePlaybackModeId,
            mode.Snapshot.PlaybackModeId);
    }

    [Fact]
    public void RefuellingCommandReturnsUpdatedSnapshotAndReadableResult()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        var retainedBundleId = session.CoreState.GetBundle(12, 0).BundleId;

        GameSessionCommandResult result = session.RefuelChannel(
            12,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.Equal(1u, result.Snapshot.RefuellingOperationCount);
        Assert.Equal(124u, result.Snapshot.FreshBundlesAvailable);
        Assert.Equal(12, result.Snapshot.LastRefuelledChannel);
        Assert.Equal("toward-end-b", result.Snapshot.LastRefuellingDirectionId);
        Assert.Equal((ushort)4, result.Snapshot.LastRefuellingShiftCount);
        Assert.Contains("Channel 12 refuelled toward End B", result.Message);
        Assert.Equal(retainedBundleId, session.CoreState.GetBundle(12, 4).BundleId);
    }

    [Fact]
    public void PreviewRefuellingReturnsProjectedCoreWithoutMutatingSession()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot before = session.Snapshot;

        GameSessionCommandResult result = session.PreviewRefuelChannel(
            12,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.NotNull(result.PreviewCore);
        Assert.Equal(before.FreshBundlesAvailable, result.Snapshot.FreshBundlesAvailable);
        Assert.Equal(before.RefuellingOperationCount, result.Snapshot.RefuellingOperationCount);
        Assert.Equal(before.ScoreTotal, result.Snapshot.ScoreTotal);
        Assert.False(session.Snapshot.Core.GetChannel(12).Bundles[0].IsFresh);
        Assert.True(result.PreviewCore.GetChannel(12).Bundles[0].IsFresh);
    }

    [Fact]
    public void RefuellingReprojectsStateDerivedPowerReactivityAndScore()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot before = session.Snapshot;

        GameSessionCommandResult result = session.RefuelChannel(
            0,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC");

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.Equal(before.NormalizedPowerFraction, result.Snapshot.NormalizedPowerFraction);
        Assert.Equal(before.AbsoluteTiltFraction, result.Snapshot.AbsoluteTiltFraction);
        Assert.True(result.Snapshot.Physics.Reactivity > before.Physics.Reactivity);
        Assert.True(result.Snapshot.Physics.TotalPowerWatts >
            before.Physics.TotalPowerWatts);
        Assert.True(result.Snapshot.ScoreTotal > before.ScoreTotal);
        Assert.True(result.Snapshot.Core.GetChannel(0).PowerWatts >
            before.Core.GetChannel(0).PowerWatts);
    }

    [Fact]
    public void PracticePhysicsProjectionSeparatesSetpointFromCriticalityResponse()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot snapshot = session.Snapshot;

        Assert.Equal("candu6-two-group-full-core-diffusion-v1", snapshot.Physics.SourceId);
        Assert.Equal("converged", snapshot.Physics.SolveState);
        Assert.True(snapshot.Physics.IsAuthoritative);
        Assert.Contains("spatial-eigen-jacobi-v1", snapshot.Physics.SolverIdentity);
        Assert.True(snapshot.Physics.SolverIterationCount > 0);
        Assert.Equal(
            snapshot.Physics.EffectiveK,
            (1.0 / (1.0 - snapshot.Physics.Reactivity)),
            12);
        Assert.Equal(
            snapshot.Physics.ReferencePowerWatts * snapshot.Physics.PowerAmplitude,
            snapshot.Physics.TargetPowerWatts,
            6);
        Assert.Equal(1.0, snapshot.Physics.ActualPowerFraction, 12);
        Assert.Equal(
            snapshot.Physics.ReferencePowerWatts * snapshot.Physics.ActualPowerFraction,
            snapshot.Physics.TotalPowerWatts,
            4);
        Assert.InRange(snapshot.Physics.PowerBalanceRelativeError, 0.0, 1e-12);

        double channelPowerTotal = 0.0;
        double bundlePowerTotal = 0.0;
        foreach (GameChannelPresentationSnapshot channel in snapshot.Core.Channels)
        {
            channelPowerTotal += channel.PowerWatts;
            foreach (GameBundlePresentationSnapshot bundle in channel.Bundles)
            {
                Assert.True(bundle.PowerWatts >= 0.0);
                bundlePowerTotal += bundle.PowerWatts;
            }
        }

        Assert.Equal(snapshot.Physics.TotalPowerWatts, channelPowerTotal, 4);
        Assert.Equal(snapshot.Physics.TotalPowerWatts, bundlePowerTotal, 4);
    }

    [Fact]
    public void PracticeAdvanceIntegratesTheProjectedBundlePowerIntoBurnup()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot before = session.Snapshot;
        GameBundlePresentationSnapshot beforeBundle = before.Core.GetChannel(0).Bundles[0];
        const double stepSeconds = 1.0;

        GameSessionCommandResult result = session.AdvanceWallMilliseconds(100);

        Assert.True(result.Accepted, result.DiagnosticMessage);
        GameBundlePresentationSnapshot afterBundle = result.Snapshot.Core.GetChannel(0).Bundles[0];
        double expectedBurnupDelta = beforeBundle.PowerWatts * stepSeconds /
                                     SyntheticGameCoreStateV1.DefaultHeavyMetalMassKg /
                                     GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram;
        Assert.Equal(
            beforeBundle.CurrentBurnupMwDayPerKg + expectedBurnupDelta,
            afterBundle.CurrentBurnupMwDayPerKg,
            12);
        Assert.Equal(
            beforeBundle.PowerWatts * stepSeconds,
            session.CoreState.GetBundle(0, 0).CumulativeFissionEnergyJ,
            6);
        Assert.True(result.Snapshot.Physics.BindingVersion > before.Physics.BindingVersion);
    }

    [Fact]
    public void PracticeAdvanceRespondsToTheReactivityChangeWithLowerPower()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionSnapshot before = session.Snapshot;

        GameSessionCommandResult result = session.AdvanceWallMilliseconds(48_000);

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.True(result.Snapshot.Physics.Reactivity < before.Physics.Reactivity);
        Assert.True(result.Snapshot.Physics.ActualPowerFraction <
            before.Physics.ActualPowerFraction);
        Assert.True(result.Snapshot.Physics.TotalPowerWatts <
            before.Physics.TotalPowerWatts);
        Assert.Equal(
            result.Snapshot.Physics.ReferencePowerWatts *
                result.Snapshot.Physics.ActualPowerFraction,
            result.Snapshot.Physics.TotalPowerWatts,
            4);
    }

    [Fact]
    public void DebugCommandsAdjustOnlyExplicitPracticeControls()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        Assert.True(session.QueuePowerTarget(0.95).Accepted);
        GameSessionCommandResult cleared = session.DebugClearPendingActions();
        Assert.True(cleared.Accepted, cleared.DiagnosticMessage);
        Assert.Equal(0u, cleared.Snapshot.PendingActionCount);

        Assert.True(session.DebugGrantFreshBundles(100).Accepted);
        Assert.Equal(228u, session.Snapshot.FreshBundlesAvailable);

        Assert.True(session.RefuelChannel(
            0,
            "toward-end-b",
            4,
            "NAT-U-SYNTHETIC").Accepted);
        Assert.True(session.Snapshot.ScoreTotal > 0.0);
        Assert.True(session.DebugResetSyntheticResponse().Accepted);
        Assert.Equal(1.0, session.Snapshot.NormalizedPowerFraction);
        Assert.Equal(0.0, session.Snapshot.AbsoluteTiltFraction);
        Assert.Equal(0.0, session.Snapshot.ScoreTotal);
    }

    [Fact]
    public void DebugPlaybackModeProvidesBoundedSixtyTimesPracticePacing()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        GameSessionCommandResult mode = session.SetPlaybackMode(
            PracticeGameSessionFactory.DebugPlaybackModeId);
        Assert.True(mode.Accepted, mode.DiagnosticMessage);
        Assert.Equal(60.0, mode.Snapshot.AccelerationFactor);

        GameSessionCommandResult advance = session.AdvanceWallMilliseconds(100);
        Assert.True(advance.Accepted, advance.DiagnosticMessage);
        Assert.Equal(6.0, advance.Snapshot.SimulationTimeSeconds);
    }
}
