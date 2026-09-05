using System;
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
        Assert.True(result.Snapshot.Physics.TotalPowerWatts > 0.0);
        Assert.True(result.Snapshot.ScoreTotal > before.ScoreTotal);
        Assert.True(result.Snapshot.Core.GetChannel(0).PowerWatts >
            before.Core.GetChannel(0).PowerWatts);
    }

    [Fact]
    public void PracticePhysicsProjectionSeparatesSetpointFromAdiabaticAmplitude()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot snapshot = session.Snapshot;

        Assert.Equal(AdiabaticKineticsIdentityV1.ModelId, snapshot.Physics.SourceId);
        Assert.Equal(AdiabaticKineticsIdentityV1.FormulationId, snapshot.Physics.FormulationId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ShapeMethodId, snapshot.Physics.ShapeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.AmplitudeMethodId, snapshot.Physics.AmplitudeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ReactivityMethodId, snapshot.Physics.ReactivityMethodId);
        Assert.Equal("converged", snapshot.Physics.SolveState);
        Assert.True(snapshot.Physics.IsAuthoritative);
        Assert.Contains(AdiabaticKineticsIdentityV1.SolverId, snapshot.Physics.SolverIdentity);
        Assert.Contains("spatial-eigen-jacobi-v1", snapshot.Physics.SolverIdentity);
        Assert.True(snapshot.Physics.SolverIterationCount > 0);
        Assert.Equal(
            snapshot.Physics.EffectiveK,
            (1.0 / (1.0 - snapshot.Physics.Reactivity)),
            12);
        Assert.Equal(
            snapshot.Physics.ReferencePowerWatts * snapshot.NormalizedPowerFraction,
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
    public void PracticeAdvanceUsesBoundedSteadyStateRegulation()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionCommandResult refuelled = session.RefuelChannel(
            0,
            "toward-end-b",
            8,
            "NAT-U-SYNTHETIC");
        Assert.True(refuelled.Accepted, refuelled.DiagnosticMessage);
        double perturbation = refuelled.Snapshot.Physics.CompensatedNetReactivity;
        Assert.True(perturbation > 0.0);

        GameSessionCommandResult result = session.AdvanceWallMilliseconds(48_000);

        Assert.True(result.Accepted, result.DiagnosticMessage);
        Assert.True(
            Math.Abs(result.Snapshot.Physics.CompensatedNetReactivity) < perturbation);
        Assert.InRange(result.Snapshot.Physics.ActualPowerFraction, 0.0, 1.5);
        Assert.Equal(
            result.Snapshot.Physics.ReferencePowerWatts *
                result.Snapshot.Physics.ActualPowerFraction,
            result.Snapshot.Physics.TotalPowerWatts,
            4);
    }

    [Fact]
    public void StaticShapeRecomputesAtTheHourlyBoundaryAndIsPartitionDeterministic()
    {
        GameSession oneCommand = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSession splitCommand = PracticeGameSessionFactory.CreateBrowserPlaytest();
        double initialReactivity = oneCommand.Snapshot.Physics.Reactivity;

        GameSessionCommandResult beforeBoundary = oneCommand.AdvanceWallMilliseconds(1_999);
        Assert.True(beforeBoundary.Accepted, beforeBoundary.DiagnosticMessage);
        Assert.Equal(initialReactivity, beforeBoundary.Snapshot.Physics.Reactivity, 15);

        GameSessionCommandResult atBoundary = oneCommand.AdvanceWallMilliseconds(1);
        Assert.True(atBoundary.Accepted, atBoundary.DiagnosticMessage);
        Assert.Equal(
            beforeBoundary.Snapshot.Physics.BindingVersion + 2,
            atBoundary.Snapshot.Physics.BindingVersion);

        Assert.True(splitCommand.AdvanceWallMilliseconds(1_000).Accepted);
        GameSessionCommandResult split = splitCommand.AdvanceWallMilliseconds(1_000);
        Assert.True(split.Accepted, split.DiagnosticMessage);
        Assert.Equal(atBoundary.Snapshot.Physics.Reactivity, split.Snapshot.Physics.Reactivity, 15);
        Assert.Equal(atBoundary.Snapshot.Physics.PowerAmplitude, split.Snapshot.Physics.PowerAmplitude, 15);
        Assert.Equal(atBoundary.Snapshot.Physics.TotalPowerWatts, split.Snapshot.Physics.TotalPowerWatts, 6);
        Assert.Equal(
            atBoundary.Snapshot.Core.GetChannel(189).Bundles[5].PowerWatts,
            split.Snapshot.Core.GetChannel(189).Bundles[5].PowerWatts,
            9);
    }

    [Fact]
    public void PracticeRegulationPreservesLocalSpatialPowerDifference()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionCommandResult refuelled = session.RefuelChannel(
            189,
            "toward-end-b",
            8,
            "NAT-U-SYNTHETIC");
        Assert.True(refuelled.Accepted, refuelled.DiagnosticMessage);

        GameChannelPresentationSnapshot channelA =
            refuelled.Snapshot.Core.GetChannel(189);
        GameChannelPresentationSnapshot channelB =
            refuelled.Snapshot.Core.GetChannel(190);
        Assert.NotEqual(channelA.LocalPowerFraction, channelB.LocalPowerFraction);

        GameSessionCommandResult advanced = session.AdvanceWallMilliseconds(2_000);
        Assert.True(advanced.Accepted, advanced.DiagnosticMessage);
        Assert.True(
            Math.Abs(advanced.Snapshot.Physics.CompensatedNetReactivity) <
            Math.Abs(refuelled.Snapshot.Physics.CompensatedNetReactivity));
        Assert.NotEqual(
            advanced.Snapshot.Core.GetChannel(189).LocalPowerFraction,
            advanced.Snapshot.Core.GetChannel(190).LocalPowerFraction);
    }

    [Fact]
    public void PracticeRegulationIsStableWhenALongAdvanceIsPartitioned()
    {
        GameSession oneCommand = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSession splitCommand = PracticeGameSessionFactory.CreateBrowserPlaytest();

        Assert.True(oneCommand.RefuelChannel(
            189,
            "toward-end-b",
            8,
            "NAT-U-SYNTHETIC").Accepted);
        Assert.True(splitCommand.RefuelChannel(
            189,
            "toward-end-b",
            8,
            "NAT-U-SYNTHETIC").Accepted);

        GameSessionCommandResult one = oneCommand.AdvanceWallMilliseconds(4_000);
        Assert.True(one.Accepted, one.DiagnosticMessage);
        Assert.True(splitCommand.AdvanceWallMilliseconds(2_000).Accepted);
        GameSessionCommandResult split = splitCommand.AdvanceWallMilliseconds(2_000);
        Assert.True(split.Accepted, split.DiagnosticMessage);

        Assert.Equal(one.Snapshot.SimulationTimeSeconds, split.Snapshot.SimulationTimeSeconds, 12);
        Assert.Equal(one.Snapshot.Physics.CoreReactivity, split.Snapshot.Physics.CoreReactivity, 12);
        Assert.Equal(one.Snapshot.Physics.CompensatedNetReactivity, split.Snapshot.Physics.CompensatedNetReactivity, 12);
        Assert.Equal(one.Snapshot.Physics.CompensationState, split.Snapshot.Physics.CompensationState, 12);
        Assert.Equal(one.Snapshot.Physics.CompensationCommand, split.Snapshot.Physics.CompensationCommand, 12);
        Assert.Equal(one.Snapshot.Physics.CadenceIdentity, split.Snapshot.Physics.CadenceIdentity);
        Assert.Equal(
            one.Snapshot.Core.GetChannel(189).Bundles[5].PowerWatts,
            split.Snapshot.Core.GetChannel(189).Bundles[5].PowerWatts,
            6);
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
