using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeRefuellingCampaignTests
{
    private const uint CampaignChannel = 189;
    private const string FuelType = "NAT-U-SYNTHETIC";

    [Fact]
    public void DirectRefuelPublishesTheAcceptedFourBundleShift()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot before = session.Snapshot;
        SyntheticGameCoreStateV1 beforeCoreState = session.CoreState;
        EquilibriumCoreProjectionV1 beforeEquilibrium = session.CurrentEquilibriumProjection;
        IqsSpatialCandidateV1 beforeSpatialCandidate = session.CurrentSpatialCandidate;
        GameChannelPresentationSnapshot beforeChannel = before.Core.GetChannel(CampaignChannel);

        GameSessionCommandResult committed = session.RefuelChannel(
            CampaignChannel,
            "toward-end-b",
            4,
            FuelType);

        Assert.True(committed.Accepted, committed.DiagnosticMessage);
        GameChannelPresentationSnapshot committedChannel =
            committed.Snapshot.Core.GetChannel(CampaignChannel);
        for (int position = 0; position < 4; position++)
        {
            Assert.True(committedChannel.Bundles[position].IsFresh);
            Assert.NotEqual(
                beforeChannel.Bundles[position].BundleId,
                committedChannel.Bundles[position].BundleId);
        }

        for (int position = 4; position < 12; position++)
        {
            Assert.Equal(
                beforeChannel.Bundles[position - 4].BundleId,
                committedChannel.Bundles[position].BundleId);
        }

        Assert.Equal(1u, committed.Snapshot.RefuellingOperationCount);
        Assert.Equal(before.FreshBundlesAvailable - 4u, committed.Snapshot.FreshBundlesAvailable);
        Assert.Equal((int)CampaignChannel, committed.Snapshot.LastRefuelledChannel);
        Assert.Equal("toward-end-b", committed.Snapshot.LastRefuellingDirectionId);
        Assert.Equal((ushort)4, committed.Snapshot.LastRefuellingShiftCount);
        Assert.True(committed.Snapshot.ScoreTotal > before.ScoreTotal);
        Assert.True(
            committed.Snapshot.Rrs.CoreReactivity >
            before.Rrs.CoreReactivity);
        Assert.True(
            Math.Abs(committed.Snapshot.Rrs.CompensatedNetReactivity) <=
            Math.Abs(committed.Snapshot.Rrs.CoreReactivity));
        Assert.Equal(0UL, committed.Snapshot.Xenon.StateVersion);
        Assert.Equal(0.0, committed.Snapshot.Xenon.MeanXe135NumberDensityM3);
        Assert.NotSame(beforeCoreState, session.CoreState);
        Assert.NotSame(beforeEquilibrium, session.CurrentEquilibriumProjection);
        Assert.NotSame(beforeSpatialCandidate, session.CurrentSpatialCandidate);
    }

    [Fact]
    public void UnsupportedFuelRejectionLeavesTheEntireAcceptedStateAtomic()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        GameSessionSnapshot before = session.Snapshot;
        SyntheticGameCoreStateV1 beforeCoreState = session.CoreState;
        EquilibriumCoreProjectionV1 beforeEquilibrium = session.CurrentEquilibriumProjection;
        IqsSpatialCandidateV1 beforeSpatialCandidate = session.CurrentSpatialCandidate;

        GameSessionCommandResult rejected = session.RefuelChannel(
            CampaignChannel,
            "toward-end-b",
            4,
            "UNSUPPORTED-FUEL");

        Assert.False(rejected.Accepted);
        Assert.NotEmpty(rejected.DiagnosticCode);
        Assert.NotEmpty(rejected.DiagnosticMessage);
        AssertAcceptedStateEqual(before, rejected.Snapshot);
        AssertAcceptedStateEqual(before, session.Snapshot);
        Assert.Same(beforeCoreState, session.CoreState);
        Assert.Same(beforeEquilibrium, session.CurrentEquilibriumProjection);
        Assert.Same(beforeSpatialCandidate, session.CurrentSpatialCandidate);
    }

    [Fact]
    public void BrowserAdvanceBeforeHourlyBoundaryReusesStaticEquilibriumProjection()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        EquilibriumCoreProjectionV1 before = session.CurrentEquilibriumProjection;

        GameSessionCommandResult advanced = session.AdvanceWallMilliseconds(100);

        Assert.True(advanced.Accepted, advanced.DiagnosticMessage);
        Assert.Equal(180.0, advanced.Snapshot.SimulationTimeSeconds);
        Assert.Same(before, session.CurrentEquilibriumProjection);
        Assert.Equal(
            EquilibriumCoreSolverIdentityV1.ModelId,
            advanced.Snapshot.Physics.SourceId);
        Assert.Equal(0UL, advanced.Snapshot.Xenon.StateVersion);
        Assert.Equal(0.0, advanced.Snapshot.Xenon.MeanXe135NumberDensityM3);
    }

    [Fact]
    public void RefuelledHourProducesTheSameBurnupAndEquilibriumAcrossWallTimePartitions()
    {
        GameSession oneAdvance = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSession twoAdvances = PracticeGameSessionFactory.CreateBrowserPlaytest();

        GameSessionCommandResult oneRefuel = oneAdvance.RefuelChannel(
            CampaignChannel,
            "toward-end-a",
            8,
            FuelType);
        GameSessionCommandResult splitRefuel = twoAdvances.RefuelChannel(
            CampaignChannel,
            "toward-end-a",
            8,
            FuelType);
        Assert.True(oneRefuel.Accepted, oneRefuel.DiagnosticMessage);
        Assert.True(splitRefuel.Accepted, splitRefuel.DiagnosticMessage);
        AssertAcceptedStateEqual(oneRefuel.Snapshot, splitRefuel.Snapshot);

        EquilibriumCoreProjectionV1 initialProjection = oneAdvance.CurrentEquilibriumProjection;
        double initialBurnup = oneRefuel.Snapshot.Core
            .GetChannel(CampaignChannel)
            .Bundles[0]
            .CurrentBurnupMwDayPerKg;

        GameSessionCommandResult whole = oneAdvance.AdvanceWallMilliseconds(2_000);
        GameSessionCommandResult firstHalf = twoAdvances.AdvanceWallMilliseconds(1_000);
        GameSessionCommandResult secondHalf = twoAdvances.AdvanceWallMilliseconds(1_000);

        Assert.True(whole.Accepted, whole.DiagnosticMessage);
        Assert.True(firstHalf.Accepted, firstHalf.DiagnosticMessage);
        Assert.True(secondHalf.Accepted, secondHalf.DiagnosticMessage);
        Assert.Equal(3_600.0, whole.Snapshot.SimulationTimeSeconds);
        Assert.True(
            whole.Snapshot.Core.GetChannel(CampaignChannel)
                .Bundles[0].CurrentBurnupMwDayPerKg > initialBurnup);
        Assert.NotSame(initialProjection, oneAdvance.CurrentEquilibriumProjection);
        Assert.Equal(0UL, whole.Snapshot.Xenon.StateVersion);
        Assert.Equal(0.0, whole.Snapshot.Xenon.MeanXe135NumberDensityM3);
        AssertAcceptedStateEqual(
            whole.Snapshot,
            secondHalf.Snapshot,
            compareTurnSummaryCount: false);
        Assert.Equal(
            oneAdvance.CurrentEquilibriumProjection.ReactivityBindingDigest,
            twoAdvances.CurrentEquilibriumProjection.ReactivityBindingDigest);
    }

    private static void AssertAcceptedStateEqual(
        GameSessionSnapshot expected,
        GameSessionSnapshot actual,
        bool compareTurnSummaryCount = true)
    {
        Assert.Equal(expected.ScenarioId, actual.ScenarioId);
        Assert.Equal(expected.DifficultyId, actual.DifficultyId);
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.PlaybackModeId, actual.PlaybackModeId);
        Assert.Equal(expected.AccelerationFactor, actual.AccelerationFactor);
        Assert.Equal(expected.SimulationTimeSeconds, actual.SimulationTimeSeconds);
        Assert.Equal(expected.WallElapsedSeconds, actual.WallElapsedSeconds);
        Assert.Equal(expected.NormalizedPowerFraction, actual.NormalizedPowerFraction);
        Assert.Equal(expected.AbsoluteTiltFraction, actual.AbsoluteTiltFraction);
        Assert.Equal(expected.ControlMarginFraction, actual.ControlMarginFraction);
        Assert.Equal(expected.DeviceAvailableFraction, actual.DeviceAvailableFraction);
        Assert.Equal(expected.RefuelRequestsRemaining, actual.RefuelRequestsRemaining);
        Assert.Equal(expected.PendingActionCount, actual.PendingActionCount);
        Assert.Equal(expected.ProcessedScriptedEventCount, actual.ProcessedScriptedEventCount);
        Assert.Equal(expected.ScoreTotal, actual.ScoreTotal);
        if (compareTurnSummaryCount)
        {
            Assert.Equal(expected.TurnSummaryCount, actual.TurnSummaryCount);
        }
        Assert.Equal(expected.OutcomeId, actual.OutcomeId);
        Assert.Equal(expected.IsPaused, actual.IsPaused);
        Assert.Equal(expected.FreshBundlesAvailable, actual.FreshBundlesAvailable);
        Assert.Equal(expected.RefuellingOperationCount, actual.RefuellingOperationCount);
        Assert.Equal(expected.LastRefuelledChannel, actual.LastRefuelledChannel);
        Assert.Equal(expected.LastRefuellingDirectionId, actual.LastRefuellingDirectionId);
        Assert.Equal(expected.LastRefuellingShiftCount, actual.LastRefuellingShiftCount);
        AssertCoreEqual(expected.Core, actual.Core);
    }

    private static void AssertCoreEqual(
        GameCorePresentationSnapshot expected,
        GameCorePresentationSnapshot actual,
        bool compareBindingVersion = true)
    {
        Assert.Equal(expected.ChannelCount, actual.ChannelCount);
        if (compareBindingVersion)
        {
            Assert.Equal(expected.Physics.BindingVersion, actual.Physics.BindingVersion);
        }
        Assert.Equal(expected.Physics.PowerAmplitude, actual.Physics.PowerAmplitude);
        Assert.Equal(expected.Physics.ActualPowerFraction, actual.Physics.ActualPowerFraction);
        Assert.Equal(expected.Physics.TargetPowerWatts, actual.Physics.TargetPowerWatts);
        Assert.Equal(expected.Physics.TotalPowerWatts, actual.Physics.TotalPowerWatts);
        Assert.Equal(expected.Physics.EffectiveK, actual.Physics.EffectiveK);
        Assert.Equal(expected.Physics.Reactivity, actual.Physics.Reactivity);
        Assert.Equal(
            expected.Physics.WeightedPerturbationReactivity,
            actual.Physics.WeightedPerturbationReactivity);
        Assert.Equal(
            expected.Physics.ReactivityBindingDigestHex,
            actual.Physics.ReactivityBindingDigestHex);
        Assert.Equal(expected.Physics.CompensationState, actual.Physics.CompensationState);
        Assert.Equal(expected.Physics.CompensationCommand, actual.Physics.CompensationCommand);
        Assert.Equal(expected.Physics.SolverIdentity, actual.Physics.SolverIdentity);
        Assert.Equal(expected.Xenon.StateDigestHex, actual.Xenon.StateDigestHex);
        Assert.Equal(expected.Xenon.StateVersion, actual.Xenon.StateVersion);
        Assert.Equal(expected.Xenon.SimulationTimeSeconds, actual.Xenon.SimulationTimeSeconds);
        Assert.Equal(expected.Xenon.DynamicXenonDigestHex, actual.Xenon.DynamicXenonDigestHex);
        Assert.Equal(expected.Xenon.EffectiveCoefficientDigestHex, actual.Xenon.EffectiveCoefficientDigestHex);

        for (int channelIndex = 0; channelIndex < expected.Channels.Count; channelIndex++)
        {
            GameChannelPresentationSnapshot expectedChannel = expected.Channels[channelIndex];
            GameChannelPresentationSnapshot actualChannel = actual.Channels[channelIndex];
            Assert.Equal(expectedChannel.ChannelIndex, actualChannel.ChannelIndex);
            Assert.Equal(expectedChannel.AverageBurnupMwDayPerKg, actualChannel.AverageBurnupMwDayPerKg);
            Assert.Equal(expectedChannel.PowerWatts, actualChannel.PowerWatts);
            Assert.Equal(expectedChannel.LocalPowerFraction, actualChannel.LocalPowerFraction);
            Assert.Equal(expectedChannel.LocalTiltFraction, actualChannel.LocalTiltFraction);

            for (int position = 0; position < expectedChannel.Bundles.Count; position++)
            {
                GameBundlePresentationSnapshot expectedBundle = expectedChannel.Bundles[position];
                GameBundlePresentationSnapshot actualBundle = actualChannel.Bundles[position];
                Assert.Equal(expectedBundle.Position, actualBundle.Position);
                Assert.Equal(expectedBundle.BundleId, actualBundle.BundleId);
                Assert.Equal(expectedBundle.FuelTypeId, actualBundle.FuelTypeId);
                Assert.Equal(expectedBundle.CurrentBurnupMwDayPerKg, actualBundle.CurrentBurnupMwDayPerKg);
                Assert.Equal(expectedBundle.PowerWatts, actualBundle.PowerWatts);
                Assert.Equal(expectedBundle.InsertedAtSeconds, actualBundle.InsertedAtSeconds);
                Assert.Equal(expectedBundle.StateVersion, actualBundle.StateVersion);
            }
        }
    }
}
