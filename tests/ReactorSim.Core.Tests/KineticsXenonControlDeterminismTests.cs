using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class KineticsXenonControlDeterminismTests
{
    private const double FullCoreTargetPowerWatts = 1_000_000_000.0;
    private const double XenonHourSeconds = 3_600.0;
    private const uint ReplayChannel = 189;
    private const string ReplayFuelType = "NAT-U-SYNTHETIC";

    private static readonly Lazy<FullCoreFixture> SharedFullCore =
        new Lazy<FullCoreFixture>(CreateFullCoreFixture);

    // CORE-KIN-001
    [Fact]
    public void CoreKin001EquilibriumPrecursorsRemainFiniteNonnegativeAndStationary()
    {
        DelayedNeutronDataV1 data = CreateKineticsData();
        double[] equilibrium = EquilibriumPrecursors(data, 1.0);
        KineticStateV1 initial = CreateKineticState(data, equilibrium, 0.0);

        KineticIntegrationResultV1 first = Require(
            KineticIntegrationTransitionV1.TryApply(initial, data, 0.25));
        KineticIntegrationResultV1 second = Require(
            KineticIntegrationTransitionV1.TryApply(first.ResultingState, data, 0.25));

        Assert.Equal(0.25, first.ResultingState.SimulationTimeSeconds, 12);
        Assert.Equal(0.50, second.ResultingState.SimulationTimeSeconds, 12);
        Assert.Equal(initial.Amplitude, first.ResultingState.Amplitude, 12);
        Assert.Equal(initial.Amplitude, second.ResultingState.Amplitude, 12);
        Assert.Equal(0.0, first.Step.AmplitudeDerivative, 12);
        Assert.Equal(0.0, second.Step.AmplitudeDerivative, 12);
        Assert.True(initial.Precursor.Count == first.ResultingState.Precursor.Count);
        Assert.True(initial.Precursor.Count == second.ResultingState.Precursor.Count);

        for (int index = 0; index < equilibrium.Length; index++)
        {
            Assert.Equal(equilibrium[index], first.ResultingState.Precursor[index], 12);
            Assert.Equal(equilibrium[index], second.ResultingState.Precursor[index], 12);
        }

        AssertFiniteNonnegative(first.ResultingState.Amplitude);
        AssertFiniteNonnegative(second.ResultingState.Amplitude);
        AssertFiniteNonnegative(first.ResultingState.Precursor);
        AssertFiniteNonnegative(second.ResultingState.Precursor);
    }

    // CORE-KIN-002
    [Fact]
    public void CoreKin002SignedPerturbationsFollowTheConfiguredStepAndReplayExactly()
    {
        DelayedNeutronDataV1 data = CreateKineticsData();
        double[] equilibrium = EquilibriumPrecursors(data, 1.0);
        IntegrationStabilityPolicyV1 policy = Require(
            IntegrationStabilityPolicyV1.TryCreate(
                "kinetics-test-policy-v1",
                Digest(0x12),
                0.05,
                0.05,
                0.05,
                0.5,
                1.0,
                0.05));
        IntegrationSubstepScheduleV1 schedule = Require(
            policy.TryPartition(0.025, 0.4, 1.0));
        Assert.Single(schedule);
        double deltaTimeSeconds = schedule[0];
        Assert.True(policy.TryValidateSubstep(deltaTimeSeconds, 0.4, 1.0).IsValid);

        KineticStateV1 positiveState = CreateKineticState(data, equilibrium, 1.0e-3);
        KineticStateV1 negativeState = CreateKineticState(data, equilibrium, -1.0e-3);
        KineticIntegrationResultV1 positiveFirst = Require(
            KineticIntegrationTransitionV1.TryApply(
                positiveState,
                data,
                deltaTimeSeconds));
        KineticIntegrationResultV1 positiveReplay = Require(
            KineticIntegrationTransitionV1.TryApply(
                positiveState,
                data,
                deltaTimeSeconds));
        KineticIntegrationResultV1 negativeFirst = Require(
            KineticIntegrationTransitionV1.TryApply(
                negativeState,
                data,
                deltaTimeSeconds));
        KineticIntegrationResultV1 negativeReplay = Require(
            KineticIntegrationTransitionV1.TryApply(
                negativeState,
                data,
                deltaTimeSeconds));

        Assert.True(positiveFirst.Step.AmplitudeDerivative > 0.0);
        Assert.True(negativeFirst.Step.AmplitudeDerivative < 0.0);
        Assert.True(positiveFirst.ResultingState.Amplitude > positiveState.Amplitude);
        Assert.True(negativeFirst.ResultingState.Amplitude < negativeState.Amplitude);
        Assert.InRange(positiveFirst.ResultingState.Amplitude, 0.0, 1.001);
        Assert.InRange(negativeFirst.ResultingState.Amplitude, 0.0, 1.001);
        Assert.Equal(deltaTimeSeconds, positiveFirst.Step.DeltaTimeSeconds, 14);
        Assert.Equal(deltaTimeSeconds, negativeFirst.Step.DeltaTimeSeconds, 14);

        Assert.Equal(
            positiveFirst.ResultingState.Amplitude,
            positiveReplay.ResultingState.Amplitude);
        Assert.Equal(
            positiveFirst.ResultingState.Precursor,
            positiveReplay.ResultingState.Precursor);
        Assert.Equal(
            positiveFirst.Step.PrecursorDerivative,
            positiveReplay.Step.PrecursorDerivative);
        Assert.Equal(
            negativeFirst.ResultingState.Amplitude,
            negativeReplay.ResultingState.Amplitude);
        Assert.Equal(
            negativeFirst.ResultingState.Precursor,
            negativeReplay.ResultingState.Precursor);
        Assert.Equal(
            negativeFirst.Step.PrecursorDerivative,
            negativeReplay.Step.PrecursorDerivative);

        AssertFiniteNonnegative(positiveFirst.ResultingState.Amplitude);
        AssertFiniteNonnegative(negativeFirst.ResultingState.Amplitude);
        AssertFiniteNonnegative(positiveFirst.ResultingState.Precursor);
        AssertFiniteNonnegative(negativeFirst.ResultingState.Precursor);
    }

    // CORE-XE-001
    [Fact]
    public void CoreXe001ZeroOverlayPreservesBaseSolveAndLocalizesTheCoupledChange()
    {
        FullCoreFixture fixture = SharedFullCore.Value;
        XenonSpatialStateV1 zeroState = CreateXenonState(fixture);
        XenonSpatialCouplingResultV1 zeroCoupling = Require(
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                zeroState,
                1.0));
        FullCoreDiffusionSolveResultV1 zeroSolve = Require(
            fixture.Model.TrySolve(
                fixture.CoreState.EnumerateBundles(),
                zeroCoupling,
                FullCoreTargetPowerWatts,
                fixture.BaseSolve.EffectiveK,
                fixture.BaseSolve.Group1Flux,
                fixture.BaseSolve.Group2Flux));

        Assert.Equal(0.0, zeroState.MeanXe135NumberDensityM3);
        Assert.Equal(0.0, zeroState.MaxXe135NumberDensityM3);
        Assert.All(
            zeroCoupling.Overlays,
            overlay =>
            {
                Assert.Equal(0.0, overlay.Xe135NumberDensity);
                Assert.Equal(0.0, overlay.DynamicAbsorptionGroup1PerM);
                Assert.Equal(0.0, overlay.DynamicAbsorptionGroup2PerM);
            });
        Assert.Equal(fixture.BaseSolve.InventoryBindingDigest, zeroSolve.InventoryBindingDigest);
        Assert.Equal(fixture.BaseSolve.CoefficientBindingDigest, zeroSolve.CoefficientBindingDigest);
        Assert.InRange(
            Math.Abs(fixture.BaseSolve.EffectiveK - zeroSolve.EffectiveK),
            0.0,
            5.0e-4);
        Assert.InRange(
            Math.Abs(fixture.BaseSolve.Reactivity - zeroSolve.Reactivity),
            0.0,
            5.0e-4);
        Assert.Equal(fixture.BaseSolve.TotalPowerWatts, zeroSolve.TotalPowerWatts, 3);
        Assert.Equal(fixture.BaseSolve.NodePowerWatts.Count, zeroSolve.NodePowerWatts.Count);
        AssertFiniteNonnegative(zeroSolve.NodePowerWatts);
        for (int index = 0; index < fixture.BaseSolve.NodePowerWatts.Count; index++)
        {
            Assert.Equal(
                fixture.BaseSolve.Coefficients.Nodes[index].AbsorptionGroup1PerM,
                zeroCoupling.Coefficients.Nodes[index].AbsorptionGroup1PerM,
                14);
            Assert.Equal(
                fixture.BaseSolve.Coefficients.Nodes[index].AbsorptionGroup2PerM,
                zeroCoupling.Coefficients.Nodes[index].AbsorptionGroup2PerM,
                14);
        }

        XenonSpatialStateV1 localizedState = CreateXenonState(
            fixture,
            localNodeIndex: 0,
            xeInventory: 1.0e22);
        XenonSpatialCouplingResultV1 localizedCoupling = Require(
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                localizedState,
                1.0));
        NodeKey localNode = new NodeKey(new ChannelId(0), new BundlePosition(0));
        XenonSpatialOverlayValueV1 localOverlay = localizedCoupling.Overlays
            .Single(overlay => overlay.Node == localNode);
        XenonSpatialOverlayValueV1 zeroLocalOverlay = zeroCoupling.Overlays
            .Single(overlay => overlay.Node == localNode);

        Assert.Equal(fixture.CoreState.GetBundle(0, 0).BundleId, localOverlay.BundleId);
        Assert.True(localOverlay.Xe135NumberDensity > zeroLocalOverlay.Xe135NumberDensity);
        Assert.True(localOverlay.DynamicAbsorptionGroup1PerM > 0.0);
        Assert.True(localOverlay.DynamicAbsorptionGroup2PerM > 0.0);
        Assert.True(
            localizedCoupling.Coefficients.Nodes[0].AbsorptionGroup2PerM >
            fixture.BaseSolve.Coefficients.Nodes[0].AbsorptionGroup2PerM);
        Assert.NotEqual(zeroState.StateDigest, localizedState.StateDigest);
        Assert.NotEqual(zeroCoupling.DynamicXenonDigest, localizedCoupling.DynamicXenonDigest);
        Assert.NotEqual(
            zeroCoupling.EffectiveCoefficientDigest,
            localizedCoupling.EffectiveCoefficientDigest);

        for (int index = 0; index < localizedState.NodeStates.Count; index++)
        {
            AssertFiniteNonnegative(localizedState.NodeStates[index].I135AtomInventory);
            AssertFiniteNonnegative(localizedState.NodeStates[index].Xe135AtomInventory);
            AssertFiniteNonnegative(localizedState.NodeStates[index].I135NumberDensity);
            AssertFiniteNonnegative(localizedState.NodeStates[index].Xe135NumberDensity);
            NodeKey node = localizedState.NodeInputs[index].Node;
            XenonSpatialOverlayValueV1 localizedOverlay = localizedCoupling.Overlays
                .Single(overlay => overlay.Node == node);
            XenonSpatialOverlayValueV1 zeroOverlay = zeroCoupling.Overlays
                .Single(overlay => overlay.Node == node);
            if (node == localNode)
            {
                Assert.NotEqual(zeroOverlay.Xe135AtomInventory, localizedOverlay.Xe135AtomInventory);
            }
            else
            {
                Assert.Equal(zeroOverlay.Xe135AtomInventory, localizedOverlay.Xe135AtomInventory);
                Assert.Equal(
                    zeroOverlay.DynamicAbsorptionGroup1PerM,
                    localizedOverlay.DynamicAbsorptionGroup1PerM);
                Assert.Equal(
                    zeroOverlay.DynamicAbsorptionGroup2PerM,
                    localizedOverlay.DynamicAbsorptionGroup2PerM);
                Assert.Equal(
                    zeroState.NodeStates[index].NuclideStateDigest,
                    localizedState.NodeStates[index].NuclideStateDigest);
            }
        }
    }

    // CORE-XE-002
    [Fact]
    public void CoreXe002SpatialXenonBurnsOutEarlyBuildsUpLaterAndKeepsOneClock()
    {
        FullCoreFixture fixture = SharedFullCore.Value;
        XenonSpatialStateV1 burnoutState = CreateXenonState(
            fixture,
            localNodeIndex: 0,
            xeInventory: 1.0e22);
        double burnoutAtFourHours = 0.0;
        double burnoutAtEightHours = 0.0;
        for (ulong hour = 1; hour <= 8; hour++)
        {
            XenonSpatialAdvanceResultV1 advance = AdvanceXenonOneHour(
                fixture,
                burnoutState,
                hour);
            Assert.Equal(
                burnoutState.SimulationTimeSeconds + XenonHourSeconds,
                advance.ResultingState.SimulationTimeSeconds,
                12);
            Assert.Equal(XenonHourSeconds, advance.DeltaTimeSeconds, 12);
            Assert.Equal(
                burnoutState.SimulationTimeSeconds,
                advance.Transitions[0].EventTimeSeconds,
                12);
            Assert.Equal(fixture.Model.NodeCount, advance.Transitions.Count);
            AssertFiniteNonnegative(advance.ResultingState.NodeStates);
            burnoutState = advance.ResultingState;
            if (hour == 4)
            {
                burnoutAtFourHours = burnoutState.NodeStates[0].Xe135NumberDensity;
            }

            if (hour == 8)
            {
                burnoutAtEightHours = burnoutState.NodeStates[0].Xe135NumberDensity;
            }
        }

        XenonSpatialStateV1 buildupState = CreateXenonState(fixture);
        double buildupAtEightHours = 0.0;
        double buildupAtTwentyFourHours = 0.0;
        for (ulong hour = 1; hour <= 24; hour++)
        {
            XenonSpatialAdvanceResultV1 advance = AdvanceXenonOneHour(
                fixture,
                buildupState,
                1_000UL + hour);
            Assert.Equal(
                buildupState.SimulationTimeSeconds + XenonHourSeconds,
                advance.ResultingState.SimulationTimeSeconds,
                12);
            Assert.Equal(buildupState.SimulationTimeSeconds, advance.Transitions[0].EventTimeSeconds, 12);
            AssertFiniteNonnegative(advance.ResultingState.MeanXe135NumberDensityM3);
            buildupState = advance.ResultingState;
            if (hour == 8)
            {
                buildupAtEightHours = buildupState.MeanXe135NumberDensityM3;
            }

            if (hour == 24)
            {
                buildupAtTwentyFourHours = buildupState.MeanXe135NumberDensityM3;
            }
        }

        Assert.True(
            burnoutAtEightHours < burnoutAtFourHours,
            "A localized high-xenon state should burn down under the accepted local flux.");
        Assert.True(
            buildupAtTwentyFourHours > buildupAtEightHours,
            "A fresh spatial I/Xe state should build xenon over the longer interval.");
        Assert.Equal(8UL, burnoutState.CoreStateVersion);
        Assert.Equal(24UL * 3_600UL, (ulong)buildupState.SimulationTimeSeconds);
        AssertFiniteNonnegative(burnoutState.NodeStates);
        AssertFiniteNonnegative(buildupState.NodeStates);

        XenonSpatialStateV1 replayState = CreateXenonState(
            fixture,
            localNodeIndex: 0,
            xeInventory: 1.0e22);
        for (ulong hour = 1; hour <= 8; hour++)
        {
            replayState = AdvanceXenonOneHour(
                fixture,
                replayState,
                hour).ResultingState;
        }

        Assert.Equal(burnoutState.StateDigest, replayState.StateDigest);
        Assert.Equal(
            burnoutState.NodeStates[0].NuclideStateVersion,
            replayState.NodeStates[0].NuclideStateVersion);
    }

    // CORE-CONTROL-001
    [Fact]
    public void CoreControl001PracticeRegulatorMovesTowardZeroSaturatesAndRejectsInvalidInput()
    {
        SyntheticPracticeRegulatorV1 initial = Require(
            SyntheticPracticeRegulatorV1.TryCreate(0.0, 0.0));
        SyntheticPracticeRegulatorV1 positive = Require(
            initial.TryAdvance(0.10, 2.0));
        SyntheticPracticeRegulatorV1 negative = Require(
            initial.TryAdvance(-0.10, 2.0));
        SyntheticPracticeRegulatorV1 overRange = Require(
            initial.TryAdvance(0.50, 2.0));

        Assert.True(positive.CompensatedNetReactivity > 0.0);
        Assert.True(positive.CompensatedNetReactivity < 0.10);
        Assert.True(negative.CompensatedNetReactivity < 0.0);
        Assert.True(negative.CompensatedNetReactivity > -0.10);
        Assert.False(positive.CompensationSaturated);
        Assert.False(negative.CompensationSaturated);
        Assert.Equal(-0.10, positive.CompensationCommand, 12);
        Assert.Equal(0.10, negative.CompensationCommand, 12);

        Assert.Equal(initial.LowerBound, overRange.CompensationCommand, 12);
        Assert.True(overRange.CompensationSaturated);
        Assert.True(overRange.CompensatedNetReactivity > 0.0);
        Assert.True(overRange.CompensatedNetReactivity < 0.50);

        foreach (SyntheticPracticeRegulatorV1 state in new[] { positive, negative, overRange })
        {
            AssertFinite(state.CoreReactivity);
            AssertFinite(state.CompensatedNetReactivity);
            Assert.InRange(state.CompensationState, state.LowerBound, state.UpperBound);
            Assert.InRange(state.CompensationCommand, state.LowerBound, state.UpperBound);
        }

        ContractValidationResult<SyntheticPracticeRegulatorV1> invalid =
            initial.TryAdvance(double.NaN, 2.0);
        Assert.False(invalid.IsValid);
        Assert.Equal("SyntheticPracticeRegulator.Advance.TimeInvalid", invalid.FirstDiagnostic.Code);
        Assert.Equal(0.0, initial.CoreReactivity);
        Assert.Equal(0.0, initial.CompensatedNetReactivity);
        Assert.Equal(0.0, initial.CompensationState);
        Assert.Equal(0.0, initial.CompensationCommand);
        Assert.Equal(0.0, initial.SimulationTimeSeconds);

        ContractValidationResult<SyntheticPracticeRegulatorV1> stale =
            initial.TryBindCoreReactivity(0.10, 1.0);
        Assert.False(stale.IsValid);
        Assert.Equal("SyntheticPracticeRegulator.Binding.Invalid", stale.FirstDiagnostic.Code);
        Assert.Equal(0.0, initial.SimulationTimeSeconds);
        Assert.Equal(0.0, initial.CompensationState);
    }

    // CORE-DETERMINISM-001
    [Fact]
    public void CoreDeterminism001RefuelAndTimeReplayMatchesAcrossWallPartitions()
    {
        GameSession whole = RunRefuelAndTimeStream(2_000UL);
        GameSession split = RunRefuelAndTimeStream(1_000UL, 1_000UL);

        Assert.Equal(3_600.0, whole.Snapshot.SimulationTimeSeconds, 12);
        Assert.Equal(whole.Snapshot.SimulationTimeSeconds, split.Snapshot.SimulationTimeSeconds, 12);
        Assert.Equal(whole.Snapshot.WallElapsedSeconds, split.Snapshot.WallElapsedSeconds, 12);
        Assert.Equal(whole.XenonState.StateDigest, split.XenonState.StateDigest);
        Assert.Equal(whole.XenonState.TopologyDigest, split.XenonState.TopologyDigest);
        Assert.Equal(whole.XenonState.DataPackDigest, split.XenonState.DataPackDigest);

        FullCoreDiffusionSolveResultV1 wholeSolve = whole.CurrentSpatialCandidate.SpatialSolve;
        FullCoreDiffusionSolveResultV1 splitSolve = split.CurrentSpatialCandidate.SpatialSolve;
        Assert.Equal(wholeSolve.InventoryBindingDigest, splitSolve.InventoryBindingDigest);
        Assert.Equal(wholeSolve.CoefficientBindingDigest, splitSolve.CoefficientBindingDigest);
        Assert.Equal(wholeSolve.XenonDynamicDigest, splitSolve.XenonDynamicDigest);
        Assert.Equal(
            wholeSolve.XenonEffectiveCoefficientDigest,
            splitSolve.XenonEffectiveCoefficientDigest);
        Assert.Equal(
            whole.CurrentSpatialCandidate.ReactivityBindingDigest,
            split.CurrentSpatialCandidate.ReactivityBindingDigest);
        Assert.Equal(
            whole.Snapshot.Physics.ReactivityBindingDigestHex,
            split.Snapshot.Physics.ReactivityBindingDigestHex);
        Assert.Equal(whole.Snapshot.Xenon.StateDigestHex, split.Snapshot.Xenon.StateDigestHex);
        Assert.Equal(whole.Snapshot.Xenon.DynamicXenonDigestHex, split.Snapshot.Xenon.DynamicXenonDigestHex);
        Assert.Equal(
            whole.Snapshot.Xenon.EffectiveCoefficientDigestHex,
            split.Snapshot.Xenon.EffectiveCoefficientDigestHex);

        Assert.Equal(whole.Snapshot.NormalizedPowerFraction, split.Snapshot.NormalizedPowerFraction);
        Assert.Equal(whole.Snapshot.AbsoluteTiltFraction, split.Snapshot.AbsoluteTiltFraction);
        Assert.Equal(whole.Snapshot.ControlMarginFraction, split.Snapshot.ControlMarginFraction);
        Assert.Equal(whole.Snapshot.DeviceAvailableFraction, split.Snapshot.DeviceAvailableFraction);
        Assert.Equal(whole.Snapshot.RefuelRequestsRemaining, split.Snapshot.RefuelRequestsRemaining);
        Assert.Equal(whole.Snapshot.ProcessedScriptedEventCount, split.Snapshot.ProcessedScriptedEventCount);
        Assert.Equal(whole.Snapshot.ScoreTotal, split.Snapshot.ScoreTotal);
        Assert.Equal(whole.Snapshot.OutcomeId, split.Snapshot.OutcomeId);
        Assert.Equal(whole.Snapshot.IsPaused, split.Snapshot.IsPaused);
        Assert.Equal(whole.Snapshot.FreshBundlesAvailable, split.Snapshot.FreshBundlesAvailable);
        Assert.Equal(whole.Snapshot.RefuellingOperationCount, split.Snapshot.RefuellingOperationCount);
        Assert.Equal(whole.Snapshot.LastRefuelledChannel, split.Snapshot.LastRefuelledChannel);
        Assert.Equal(whole.Snapshot.LastRefuellingDirectionId, split.Snapshot.LastRefuellingDirectionId);
        Assert.Equal(whole.Snapshot.LastRefuellingShiftCount, split.Snapshot.LastRefuellingShiftCount);
        AssertCorePresentationEqual(whole.Snapshot.Core, split.Snapshot.Core);
    }

    // CORE-DETERMINISM-002
    [Fact]
    public void CoreDeterminism002NonSemanticReorderingPreservesDigestsAndOrderedGroupsRejectReorder()
    {
        FullCoreFixture fixture = SharedFullCore.Value;
        XenonSpatialStateV1 orderedState = CreateXenonState(fixture);
        ContractValidationResult<XenonSpatialStateV1> orderedResult =
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                orderedState.NodeStates);
        ContractValidationResult<XenonSpatialStateV1> reorderedResult =
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                orderedState.NodeStates.Reverse());

        Assert.Equal(orderedResult.IsValid, reorderedResult.IsValid);
        Assert.True(orderedResult.IsValid, orderedResult.IsValid ? string.Empty : orderedResult.FirstDiagnostic.ToString());
        Assert.Equal(orderedResult.Diagnostics.Count, reorderedResult.Diagnostics.Count);
        Assert.Equal(orderedResult.Value.StateDigest, reorderedResult.Value.StateDigest);
        Assert.Equal(orderedResult.Value.StateDigestHex, reorderedResult.Value.StateDigestHex);
        Assert.Equal(orderedResult.Value.TopologyDigest, reorderedResult.Value.TopologyDigest);
        Assert.Equal(orderedResult.Value.DataPackDigest, reorderedResult.Value.DataPackDigest);
        Assert.Equal(orderedResult.Value.SimulationTimeSeconds, reorderedResult.Value.SimulationTimeSeconds);
        Assert.Equal(orderedResult.Value.CoreStateVersion, reorderedResult.Value.CoreStateVersion);
        Assert.Equal(
            orderedResult.Value.NodeStates.Select(state => state.BundleId),
            reorderedResult.Value.NodeStates.Select(state => state.BundleId));

        XenonSpatialStateBindingV1 orderedBinding = Require(
            orderedResult.Value.TryCreateBinding(1.0));
        XenonSpatialStateBindingV1 reorderedBinding = Require(
            reorderedResult.Value.TryCreateBinding(1.0));
        Assert.Equal(orderedBinding.StateDigest, reorderedBinding.StateDigest);
        Assert.Equal(orderedBinding.TopologyDigest, reorderedBinding.TopologyDigest);
        Assert.Equal(orderedBinding.DataPackDigest, reorderedBinding.DataPackDigest);
        Assert.Equal(
            orderedBinding.NodeVersions.Select(version => version.Node),
            reorderedBinding.NodeVersions.Select(version => version.Node));
        Assert.Equal(
            orderedBinding.NodeVersions.Select(version => version.StateVersion),
            reorderedBinding.NodeVersions.Select(version => version.StateVersion));

        ContractValidationResult<XenonSpatialCouplingResultV1> orderedCoupling =
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                orderedResult.Value,
                1.0);
        ContractValidationResult<XenonSpatialCouplingResultV1> reorderedCoupling =
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                reorderedResult.Value,
                1.0);
        Assert.Equal(orderedCoupling.IsValid, reorderedCoupling.IsValid);
        Assert.True(orderedCoupling.IsValid, orderedCoupling.IsValid ? string.Empty : orderedCoupling.FirstDiagnostic.ToString());
        Assert.Equal(orderedCoupling.Diagnostics.Count, reorderedCoupling.Diagnostics.Count);
        Assert.Equal(
            orderedCoupling.Value.BaseCoefficientDigest,
            reorderedCoupling.Value.BaseCoefficientDigest);
        Assert.Equal(
            orderedCoupling.Value.DynamicXenonDigest,
            reorderedCoupling.Value.DynamicXenonDigest);
        Assert.Equal(
            orderedCoupling.Value.EffectiveCoefficientDigest,
            reorderedCoupling.Value.EffectiveCoefficientDigest);
        Assert.Equal(
            orderedCoupling.Value.Overlays.Select(overlay => overlay.BundleId),
            reorderedCoupling.Value.Overlays.Select(overlay => overlay.BundleId));

        DelayedNeutronGroupV1 firstGroup = Require(
            DelayedNeutronGroupV1.TryCreate(0, 0.006, 0.1));
        DelayedNeutronGroupV1 secondGroup = Require(
            DelayedNeutronGroupV1.TryCreate(1, 0.004, 0.4));
        ContractValidationResult<DelayedNeutronDataV1> validGroups =
            DelayedNeutronDataV1.TryCreate(
                Id("12121212-1212-4212-8212-121212121212"),
                "ordered-groups-v1",
                Digest(0x13),
                1.0,
                new[] { firstGroup, secondGroup });
        ContractValidationResult<DelayedNeutronDataV1> reorderedGroups =
            DelayedNeutronDataV1.TryCreate(
                Id("12121212-1212-4212-8212-121212121212"),
                "ordered-groups-v1",
                Digest(0x13),
                1.0,
                new[] { secondGroup, firstGroup });
        Assert.True(validGroups.IsValid, validGroups.IsValid ? string.Empty : validGroups.FirstDiagnostic.ToString());
        Assert.False(reorderedGroups.IsValid);
        Assert.Equal("DelayedNeutronData.Groups.Order.Invalid", reorderedGroups.FirstDiagnostic.Code);
    }

    private static DelayedNeutronDataV1 CreateKineticsData()
    {
        return Require(
            DelayedNeutronDataV1.TryCreate(
                Id("11111111-1111-4111-8111-111111111111"),
                "kinetics-test-data-v1",
                Digest(0x11),
                1.0,
                new[]
                {
                    Require(DelayedNeutronGroupV1.TryCreate(0, 0.006, 0.1)),
                    Require(DelayedNeutronGroupV1.TryCreate(1, 0.004, 0.4))
                }));
    }

    private static double[] EquilibriumPrecursors(
        DelayedNeutronDataV1 data,
        double amplitude)
    {
        return data.Groups
            .Select(group =>
                group.BetaFraction * amplitude /
                (data.PromptGenerationTimeSeconds * group.DecayConstantPerSecond))
            .ToArray();
    }

    private static KineticStateV1 CreateKineticState(
        DelayedNeutronDataV1 data,
        IEnumerable<double> precursors,
        double spatialReactivity)
    {
        double[] values = precursors.ToArray();
        return Require(
            KineticStateV1.TryCreate(
                0.0,
                1.0,
                values,
                1.0,
                values,
                1_000_000.0,
                data,
                OptionalStableId.Applicable(
                    Id("22222222-2222-4222-8222-222222222222")),
                OptionalUInt64.Applicable(7),
                spatialReactivity,
                OptionalDigest32.Applicable(Digest(0x22)),
                0));
    }

    private static FullCoreFixture CreateFullCoreFixture()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        SyntheticGameCoreStateV1 coreState = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionSolveResultV1 baseSolve = Require(
            model.TrySolve(coreState.EnumerateBundles(), FullCoreTargetPowerWatts));
        NuclideDataV1 nuclideData = Require(
            NuclideDataV1.TryCreate(
                new MaterialVariantId("NAT-U-SYNTHETIC"),
                "core-test-xenon-v1",
                Digest(0x71),
                0.05,
                0.01,
                2.91e-5,
                2.09e-5,
                1.0e-24,
                3.0e-24));
        return new FullCoreFixture(model, coreState, baseSolve, nuclideData);
    }

    private static XenonSpatialStateV1 CreateXenonState(
        FullCoreFixture fixture,
        int localNodeIndex = -1,
        double xeInventory = 0.0)
    {
        NuclideStateEnvelopeV1[] states = fixture.CoreState
            .EnumerateBundles()
            .Select((bundle, index) => Require(
                NuclideStateEnvelopeV1.TryCreateFresh(
                    bundle.BundleId,
                    0.0,
                    index == localNodeIndex ? xeInventory : 0.0,
                    fixture.Model.DataPack.NodeVolumeM3,
                    0UL,
                    fixture.NuclideData)))
            .ToArray();
        return Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                states));
    }

    private static XenonSpatialAdvanceResultV1 AdvanceXenonOneHour(
        FullCoreFixture fixture,
        XenonSpatialStateV1 state,
        ulong ownerSequence)
    {
        XenonSpatialStateBindingV1 binding = Require(state.TryCreateBinding(1.0));
        StableId ownerEventId = StableId.Parse(
            "00000000-0000-0000-0000-" +
            ownerSequence.ToString("D12", CultureInfo.InvariantCulture));
        return Require(
            state.TryAdvance(
                binding,
                state.SimulationTimeSeconds + XenonHourSeconds,
                1.0,
                fixture.BaseSolve.Coefficients,
                fixture.BaseSolve.Group1Flux,
                fixture.BaseSolve.Group2Flux,
                1.0,
                ownerEventId));
    }

    private static GameSession RunRefuelAndTimeStream(params ulong[] wallPartitions)
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        GameSessionCommandResult refuel = session.RefuelChannel(
            ReplayChannel,
            "toward-end-a",
            8,
            ReplayFuelType);
        Assert.True(refuel.Accepted, refuel.DiagnosticMessage);
        foreach (ulong wallMilliseconds in wallPartitions)
        {
            GameSessionCommandResult advance = session.AdvanceWallMilliseconds(wallMilliseconds);
            Assert.True(advance.Accepted, advance.DiagnosticMessage);
        }

        return session;
    }

    private static void AssertCorePresentationEqual(
        GameCorePresentationSnapshot expected,
        GameCorePresentationSnapshot actual)
    {
        Assert.Equal(expected.ChannelCount, actual.ChannelCount);
        AssertPhysicsEqual(expected.Physics, actual.Physics);
        AssertXenonEqual(expected.Xenon, actual.Xenon);
        for (int index = 0; index < expected.Channels.Count; index++)
        {
            GameChannelPresentationSnapshot expectedChannel = expected.Channels[index];
            GameChannelPresentationSnapshot actualChannel = actual.Channels[index];
            Assert.Equal(expectedChannel.ChannelIndex, actualChannel.ChannelIndex);
            Assert.Equal(expectedChannel.GridColumn, actualChannel.GridColumn);
            Assert.Equal(expectedChannel.GridRow, actualChannel.GridRow);
            Assert.Equal(expectedChannel.AverageBurnupMwDayPerKg, actualChannel.AverageBurnupMwDayPerKg);
            Assert.Equal(expectedChannel.PowerWatts, actualChannel.PowerWatts);
            Assert.Equal(expectedChannel.LocalPowerFraction, actualChannel.LocalPowerFraction);
            Assert.Equal(expectedChannel.LocalTiltFraction, actualChannel.LocalTiltFraction);
            Assert.Equal(expectedChannel.FlowDirection, actualChannel.FlowDirection);
            AssertXenonChannelEqual(expectedChannel.Xenon, actualChannel.Xenon);
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

    private static void AssertPhysicsEqual(
        GamePhysicsPresentationSnapshot expected,
        GamePhysicsPresentationSnapshot actual)
    {
        Assert.Equal(expected.SourceId, actual.SourceId);
        Assert.Equal(expected.FormulationId, actual.FormulationId);
        Assert.Equal(expected.ShapeMethodId, actual.ShapeMethodId);
        Assert.Equal(expected.AmplitudeMethodId, actual.AmplitudeMethodId);
        Assert.Equal(expected.ReactivityMethodId, actual.ReactivityMethodId);
        Assert.Equal(expected.SolveState, actual.SolveState);
        Assert.Equal(expected.IsAuthoritative, actual.IsAuthoritative);
        Assert.Equal(expected.BindingVersion, actual.BindingVersion);
        Assert.Equal(expected.ReferencePowerWatts, actual.ReferencePowerWatts);
        Assert.Equal(expected.PowerAmplitude, actual.PowerAmplitude);
        Assert.Equal(expected.ActualPowerFraction, actual.ActualPowerFraction);
        Assert.Equal(expected.TargetPowerWatts, actual.TargetPowerWatts);
        Assert.Equal(expected.TotalPowerWatts, actual.TotalPowerWatts);
        Assert.Equal(expected.MeanChannelPowerWatts, actual.MeanChannelPowerWatts);
        Assert.Equal(expected.MeanBundlePowerWatts, actual.MeanBundlePowerWatts);
        Assert.Equal(expected.EffectiveK, actual.EffectiveK);
        Assert.Equal(expected.Reactivity, actual.Reactivity);
        Assert.Equal(expected.StaticReactivity, actual.StaticReactivity);
        Assert.Equal(expected.StaticReactivityMethodId, actual.StaticReactivityMethodId);
        Assert.Equal(expected.WeightedPerturbationReactivity, actual.WeightedPerturbationReactivity);
        Assert.Equal(expected.ReactivityNumerator, actual.ReactivityNumerator);
        Assert.Equal(expected.ReactivityDenominator, actual.ReactivityDenominator);
        Assert.Equal(expected.ReactivityIdentity, actual.ReactivityIdentity);
        Assert.Equal(expected.ReactivityBindingDigestHex, actual.ReactivityBindingDigestHex);
        Assert.Equal(expected.PowerBalanceRelativeError, actual.PowerBalanceRelativeError);
        Assert.Equal(expected.SolverIdentity, actual.SolverIdentity);
        Assert.Equal(expected.SolverIterationCount, actual.SolverIterationCount);
        Assert.Equal(expected.SolverResidualRelativeInfinity, actual.SolverResidualRelativeInfinity);
        Assert.Equal(expected.CoreReactivity, actual.CoreReactivity);
        Assert.Equal(expected.CompensatedNetReactivity, actual.CompensatedNetReactivity);
        Assert.Equal(expected.CompensationState, actual.CompensationState);
        Assert.Equal(expected.CompensationCommand, actual.CompensationCommand);
        Assert.Equal(expected.CompensationLowerBound, actual.CompensationLowerBound);
        Assert.Equal(expected.CompensationUpperBound, actual.CompensationUpperBound);
        Assert.Equal(expected.CompensationSaturated, actual.CompensationSaturated);
        Assert.Equal(expected.CompensationResponseTimeSeconds, actual.CompensationResponseTimeSeconds);
        Assert.Equal(expected.CadenceIdentity, actual.CadenceIdentity);
        Assert.Equal(expected.AdjointNormalizationIdentity, actual.AdjointNormalizationIdentity);
        Assert.Equal(expected.AdjointDigestHex, actual.AdjointDigestHex);
        Assert.Equal(expected.AdjointIterationCount, actual.AdjointIterationCount);
        Assert.Equal(
            expected.AdjointTransposeResidualRelativeInfinity,
            actual.AdjointTransposeResidualRelativeInfinity);
    }

    private static void AssertXenonEqual(
        GameXenonPresentationSnapshot expected,
        GameXenonPresentationSnapshot actual)
    {
        Assert.Equal(expected.StateIdentity, actual.StateIdentity);
        Assert.Equal(expected.StateDigestHex, actual.StateDigestHex);
        Assert.Equal(expected.StateVersion, actual.StateVersion);
        Assert.Equal(expected.SimulationTimeSeconds, actual.SimulationTimeSeconds);
        Assert.Equal(expected.NodeCount, actual.NodeCount);
        Assert.Equal(expected.CouplingIdentity, actual.CouplingIdentity);
        Assert.Equal(expected.HasCoupling, actual.HasCoupling);
        Assert.Equal(expected.BaseCoefficientDigestHex, actual.BaseCoefficientDigestHex);
        Assert.Equal(expected.DynamicXenonDigestHex, actual.DynamicXenonDigestHex);
        Assert.Equal(expected.EffectiveCoefficientDigestHex, actual.EffectiveCoefficientDigestHex);
        Assert.Equal(expected.MeanI135NumberDensityM3, actual.MeanI135NumberDensityM3);
        Assert.Equal(expected.MaxI135NumberDensityM3, actual.MaxI135NumberDensityM3);
        Assert.Equal(expected.MeanXe135NumberDensityM3, actual.MeanXe135NumberDensityM3);
        Assert.Equal(expected.MaxXe135NumberDensityM3, actual.MaxXe135NumberDensityM3);
        Assert.Equal(expected.MeanDynamicAbsorptionGroup1PerM, actual.MeanDynamicAbsorptionGroup1PerM);
        Assert.Equal(expected.MaxDynamicAbsorptionGroup1PerM, actual.MaxDynamicAbsorptionGroup1PerM);
        Assert.Equal(expected.MeanDynamicAbsorptionGroup2PerM, actual.MeanDynamicAbsorptionGroup2PerM);
        Assert.Equal(expected.MaxDynamicAbsorptionGroup2PerM, actual.MaxDynamicAbsorptionGroup2PerM);
        Assert.Equal(expected.SelectedChannelIndex, actual.SelectedChannelIndex);
        for (int index = 0; index < expected.Channels.Count; index++)
        {
            AssertXenonChannelEqual(expected.Channels[index], actual.Channels[index]);
        }
    }

    private static void AssertXenonChannelEqual(
        GameXenonChannelPresentationSnapshot expected,
        GameXenonChannelPresentationSnapshot actual)
    {
        Assert.Equal(expected.ChannelIndex, actual.ChannelIndex);
        Assert.Equal(expected.MeanI135NumberDensityM3, actual.MeanI135NumberDensityM3);
        Assert.Equal(expected.MaxI135NumberDensityM3, actual.MaxI135NumberDensityM3);
        Assert.Equal(expected.MeanXe135NumberDensityM3, actual.MeanXe135NumberDensityM3);
        Assert.Equal(expected.MaxXe135NumberDensityM3, actual.MaxXe135NumberDensityM3);
        Assert.Equal(expected.MeanDynamicAbsorptionGroup1PerM, actual.MeanDynamicAbsorptionGroup1PerM);
        Assert.Equal(expected.MaxDynamicAbsorptionGroup1PerM, actual.MaxDynamicAbsorptionGroup1PerM);
        Assert.Equal(expected.MeanDynamicAbsorptionGroup2PerM, actual.MeanDynamicAbsorptionGroup2PerM);
        Assert.Equal(expected.MaxDynamicAbsorptionGroup2PerM, actual.MaxDynamicAbsorptionGroup2PerM);
    }

    private static void AssertFiniteNonnegative(double value)
    {
        Assert.True(!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0);
    }

    private static void AssertFinite(double value)
    {
        Assert.True(!double.IsNaN(value) && !double.IsInfinity(value));
    }

    private static void AssertFiniteNonnegative(IEnumerable<double> values)
    {
        Assert.All(values, AssertFiniteNonnegative);
    }

    private static void AssertFiniteNonnegative(IEnumerable<NuclideStateEnvelopeV1> states)
    {
        Assert.All(
            states,
            state =>
            {
                AssertFiniteNonnegative(state.I135AtomInventory);
                AssertFiniteNonnegative(state.Xe135AtomInventory);
                AssertFiniteNonnegative(state.I135NumberDensity);
                AssertFiniteNonnegative(state.Xe135NumberDensity);
            });
    }

    private static StableId Id(string value)
    {
        return StableId.Parse(value);
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private sealed class FullCoreFixture
    {
        public FullCoreFixture(
            FullCoreDiffusionModelV1 model,
            SyntheticGameCoreStateV1 coreState,
            FullCoreDiffusionSolveResultV1 baseSolve,
            NuclideDataV1 nuclideData)
        {
            Model = model;
            CoreState = coreState;
            BaseSolve = baseSolve;
            NuclideData = nuclideData;
        }

        public FullCoreDiffusionModelV1 Model { get; }

        public SyntheticGameCoreStateV1 CoreState { get; }

        public FullCoreDiffusionSolveResultV1 BaseSolve { get; }

        public NuclideDataV1 NuclideData { get; }
    }
}
