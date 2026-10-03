using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeLiquidZoneRrsTests
{
    [Fact]
    public void TestedSecondCorrectionCanRetainTheFirstAcceptedMove()
    {
        Assert.Equal("command-retained", PracticeLiquidZoneRrsV1.DescribeControllerDecision(false, true, false,
            false, Enumerable.Repeat(0.5, 14).ToArray(), Enumerable.Repeat(0.02, 14).ToArray(), correctionTested: true));
    }

    [Theory]
    [InlineData(true, false, false, true, 0.5, 0, "already-balanced")]
    [InlineData(false, false, false, false, 0.5, 0, "retained-best")]
    [InlineData(false, true, false, true, 0.5, 0.02, "command-applied")]
    [InlineData(false, true, true, true, 0.5, 0.02, "correction-applied")]
    [InlineData(false, true, false, false, 0, 0.02, "fill-limits")]
    [InlineData(false, true, false, false, 0.5, 0.08, "event-limit")]
    [InlineData(false, false, false, false, 0, 0, "exhausted-empty")]
    [InlineData(false, false, false, false, 1, 0, "exhausted-full")]
    public void DecisionCodesDistinguishRetainedAcceptedBoundedAndExhaustedFacts(bool balanced, bool accepted,
        bool corrected, bool converged, double fill, double command, string expected)
    {
        var fills = Enumerable.Repeat(0.5, 14).ToArray();
        fills[0] = fill;
        if (expected.StartsWith("exhausted", StringComparison.Ordinal)) fills = Enumerable.Repeat(fill, 14).ToArray();
        Assert.Equal(expected, PracticeLiquidZoneRrsV1.DescribeControllerDecision(balanced, accepted, corrected,
            converged, fills, Enumerable.Repeat(command, 14).ToArray()));
    }

    [Theory]
    [InlineData(0.00005, true)]
    [InlineData(-0.00005, true)]
    [InlineData(0.000050001, false)]
    [InlineData(-0.000050001, false)]
    public void ControllerAcceptsInclusivePointZeroFiveMkBand(double reactivity, bool expected)
    {
        Assert.Equal(expected, PracticeLiquidZoneRrsV1.IsControllerConverged(new double[14], reactivity));
        var shapeErrors = new double[14];
        shapeErrors[0] = 0.010000001;
        Assert.False(PracticeLiquidZoneRrsV1.IsControllerConverged(shapeErrors, reactivity));
    }

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(-0.01, true)]
    [InlineData(0.010000001, false)]
    [InlineData(-0.010000001, false)]
    public void ControllerAcceptsInclusiveOnePercentagePointRegionalBand(double error, bool expected)
    {
        var shapeErrors = new double[14];
        shapeErrors[13] = error;
        Assert.Equal(expected, PracticeLiquidZoneRrsV1.IsControllerConverged(shapeErrors, 0.00005));
        Assert.False(PracticeLiquidZoneRrsV1.IsControllerConverged(shapeErrors, 0.000050001));
    }

    [Fact]
    public void AbsorberCompartmentCanDifferFromMeasuredRegionAndInactiveCellsRemainZero()
    {
        var original = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        var selected = original.Nodes[0];
        uint compartment = (selected.LogicalZoneId + 1) % 14;
        var edited = Require(PracticeLiquidZoneRrsMappingV1.TryCreate(original.Nodes.Select((n, i) =>
            new PracticeLiquidZoneRrsNodeBindingV1(n.Node, n.LogicalZoneId,
                i == 0 ? 0.020 : 0, i == 0 ? 0.008 : 0, i == 0 ? compartment : n.AbsorberZoneId))));
        Assert.Equal(selected.LogicalZoneId, edited.GetLogicalZoneId(selected.Node));
        Assert.NotEqual(original.MappingDigest, edited.MappingDigest);
        var fills = Enumerable.Repeat(0.25, 14).ToArray();
        fills[compartment] = 0.75;
        var overlay = Require(edited.TryBuildOverlay(fills));
        Assert.Equal(0.015, overlay.GetDeltaAbsorptionGroup1PerM(selected.Node), 12);
        Assert.Equal(0.006, overlay.GetDeltaAbsorptionGroup2PerM(selected.Node), 12);
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(overlay.GetDeltaAbsorptionGroup1PerM(original.Nodes[1].Node)));
        fills[selected.LogicalZoneId] = double.NaN;
        Assert.False(edited.TryBuildOverlay(fills).IsValid);
    }
    [Theory]
    [InlineData(4, 15, 1)]
    [InlineData(4, 6, 2)]
    [InlineData(10, 17, 3)]
    [InlineData(10, 11, 4)]
    [InlineData(10, 4, 5)]
    [InlineData(17, 15, 6)]
    [InlineData(17, 6, 7)]
    public void TraditionalCandu6RegionsHaveTwoSideAndThreeCentreCompartments(
        int column, int row, uint plantZone)
    {
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(column, row, out uint channel));
        for (uint bundle = 0; bundle < 12; bundle++)
        {
            uint expected = plantZone - 1U + (bundle < 6 ? 0U : 7U);
            Assert.Equal(expected, mapping.GetLogicalZoneId(
                new NodeKey(new ChannelId(channel), new BundlePosition(bundle))));
        }
    }

    [Fact]
    public void PracticeMapCoversEveryNodeOnceAndSplitsEachChannelAxially()
    {
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());

        Assert.Equal(380 * 12, mapping.Nodes.Count);
        Assert.Equal(380 * 12, mapping.Nodes.Select(node => node.Node).Distinct().Count());
        Assert.Equal(14, mapping.NodeIndicesByZone.Count);
        Assert.All(mapping.NodeIndicesByZone, zone => Assert.NotEmpty(zone));

        for (uint channel = 0; channel < 380; channel++)
        {
            uint endA = mapping.GetLogicalZoneId(
                new NodeKey(new ChannelId(channel), new BundlePosition(0)));
            uint endB = mapping.GetLogicalZoneId(
                new NodeKey(new ChannelId(channel), new BundlePosition(6)));

            Assert.Equal(endA + 7U, endB);
            for (uint bundle = 0; bundle < 6; bundle++)
            {
                Assert.Equal(endA, mapping.GetLogicalZoneId(
                    new NodeKey(new ChannelId(channel), new BundlePosition(bundle))));
                Assert.Equal(endB, mapping.GetLogicalZoneId(
                    new NodeKey(new ChannelId(channel), new BundlePosition(bundle + 6U))));
            }
        }
    }

    [Fact]
    public void IncreasingFillAddsAbsorptionForOnlyTheMappedZone()
    {
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double[] low = Enumerable.Repeat(0.5, 14).ToArray();
        double[] high = Enumerable.Repeat(0.5, 14).ToArray();
        high[3] = 0.75;

        StaticAbsorptionOverlayV1 lowOverlay = Require(mapping.TryBuildOverlay(low));
        StaticAbsorptionOverlayV1 highOverlay = Require(mapping.TryBuildOverlay(high));
        PracticeLiquidZoneRrsNodeBindingV1 controlled = mapping.Nodes
            .First(node => node.LogicalZoneId == 3U);
        PracticeLiquidZoneRrsNodeBindingV1 untouched = mapping.Nodes
            .First(node => node.LogicalZoneId != 3U);

        Assert.True(
            highOverlay.GetDeltaAbsorptionGroup1PerM(controlled.Node) >
            lowOverlay.GetDeltaAbsorptionGroup1PerM(controlled.Node));
        Assert.True(
            highOverlay.GetDeltaAbsorptionGroup2PerM(controlled.Node) >
            lowOverlay.GetDeltaAbsorptionGroup2PerM(controlled.Node));
        Assert.Equal(
            lowOverlay.GetDeltaAbsorptionGroup1PerM(untouched.Node),
            highOverlay.GetDeltaAbsorptionGroup1PerM(untouched.Node));
    }

    [Fact]
    public void InitialStateIsDeterministicCenteredAndUsesTheInitialShapeAsReference()
    {
        SyntheticGameCoreStateV1 inventory = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        EquilibriumCoreSolverV1 solver = Require(
            EquilibriumCoreSolverV1.TryCreate(model, inventory.EnumerateBundles(), 1.0e9));
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());

        PracticeLiquidZoneRrsV1 first = Require(
            PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));
        PracticeLiquidZoneRrsV1 second = Require(
            PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));

        Assert.Equal(14, first.ZoneFills.Count);
        Assert.All(first.ZoneFills, fill => Assert.Equal(0.5, fill));
        Assert.Equal(first.ReferenceZonalPowerFractions, first.TargetZonalPowerFractions);
        Assert.Equal(first.TargetZonalPowerFractions, first.MeasuredZonalPowerFractions);
        Assert.Equal(first.StateDigest, second.StateDigest);
        Assert.False(first.LowExhaustion);
        Assert.False(first.HighExhaustion);
    }

    [Fact]
    public void ResponseModelHasDeterministicConservativeShapeAndAbsorbingCommonMode()
    {
        double[] baseline = Enumerable.Repeat(1.0 / 14.0, 14).ToArray();

        PracticeLiquidZoneRrsResponseModelV1 first = Require(
            PracticeLiquidZoneRrsResponseModelV1.TryCreate(baseline));
        PracticeLiquidZoneRrsResponseModelV1 second = Require(
            PracticeLiquidZoneRrsResponseModelV1.TryCreate(baseline));

        Assert.Equal(14, first.VariableCount);
        Assert.Equal(15, first.OutputCount);
        Assert.Equal(Enumerable.Range(0, 14).Select(value => (uint)value), first.VariableOrder);
        Assert.Equal(first.ModelDigest, second.ModelDigest);
        Assert.Equal(15, first.Jacobian.Count);
        Assert.All(first.Jacobian, row => Assert.Equal(14, row.Count));

        for (int variable = 0; variable < first.VariableCount; variable++)
        {
            double shapeColumnSum = 0.0;
            for (int output = 0; output < first.VariableCount; output++)
            {
                Assert.True(double.IsFinite(first.Jacobian[output][variable]));
                shapeColumnSum += first.Jacobian[output][variable];
            }

            Assert.InRange(Math.Abs(shapeColumnSum), 0.0, 1.0e-15);
            Assert.True(first.Jacobian[first.VariableCount][variable] < 0.0);
        }
    }

    [Fact]
    public void CalibratedZonesHaveValidPositiveAbsorptionAndSixToSevenMkWorth()
    {
        CreateRunFixture(out var inventory, out var solver, out _);
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double RhoAt(double fill)
        {
            var overlay = Require(mapping.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()));
            return Require(solver.TrySolveCandidate(inventory.EnumerateBundles(), overlay)).RelativeReactivity;
        }

        double empty = RhoAt(0), half = RhoAt(0.5), full = RhoAt(1);
        Assert.True(empty > half && half > full);
        Assert.InRange(1000 * (empty - full), 6, 7);
        Assert.InRange(Math.Abs(1000 * half), 0, 0.05);
        Assert.Equal(0, PracticeLiquidZoneRrsIdentityV1.AbsorptionReferenceFillFraction);
    }

    [Fact]
    public void EquilibriumControllerIsDeterministicBoundedAndNeverUsesMoreThanFourCandidates()
    {
        CreateRunFixture(
            out SyntheticGameCoreStateV1 firstInventory,
            out EquilibriumCoreSolverV1 firstSolver,
            out PracticeLiquidZoneRrsV1 firstInitial);
        CreateRunFixture(
            out SyntheticGameCoreStateV1 secondInventory,
            out EquilibriumCoreSolverV1 secondSolver,
            out PracticeLiquidZoneRrsV1 secondInitial);

        PracticeLiquidZoneRrsEquilibriumResultV1 first = Require(
            PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                firstSolver,
                firstInventory.EnumerateBundles(),
                firstInitial,
                1.0));
        PracticeLiquidZoneRrsEquilibriumResultV1 second = Require(
            PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                secondSolver,
                secondInventory.EnumerateBundles(),
                secondInitial,
                1.0));

        PracticeLiquidZoneRrsV1 state = first.State;
        Assert.Equal(state.StateDigest, second.State.StateDigest);
        Assert.Equal(state.DecisionCode, second.State.DecisionCode);
        Assert.Equal(state.DecisionCode, Require(state.TryWithSimulationTime(2)).DecisionCode);
        Assert.NotEqual("initial-reference", state.DecisionCode);

        Assert.Equal(state.ZoneFills, second.State.ZoneFills);
        Assert.Equal(1, state.BaseCandidateSolveCount);
        Assert.Equal(1, state.ControlledBaselineCandidateSolveCount);
        Assert.InRange(state.VerificationCandidateSolveCount, 0, 1);
        Assert.InRange(state.CorrectionCandidateSolveCount, 0, 1);
        Assert.Equal(
            state.BaseCandidateSolveCount +
            state.ControlledBaselineCandidateSolveCount +
            state.VerificationCandidateSolveCount +
            state.CorrectionCandidateSolveCount,
            state.TotalCandidateSolveCount);
        Assert.InRange(
            state.TotalCandidateSolveCount,
            2,
            PracticeLiquidZoneRrsIdentityV1.MaximumCandidateSolveCount);
        Assert.All(state.ZoneFills, fill => Assert.InRange(fill, 0.0, 1.0));
        Assert.Equal(14, state.AppliedFillCommand.Count);
        Assert.All(
            state.AppliedFillCommand,
            command => Assert.InRange(
                Math.Abs(command),
                0.0,
                PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent + 1.0e-12));
        Assert.True(
            state.CombinedWeightedResidual <=
            state.ControlledBaselineWeightedResidual +
            PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance);
        Assert.Equal(state.OverlayDigest, first.Projection.StaticAbsorptionOverlay!.OverlayDigest);
        if (state.CorrectionApplied)
        {
            Assert.Equal(1, state.CorrectionCandidateSolveCount);
        }
    }

    [Fact]
    public void ExcessFuelReactivityIsRegulatedBeforeSpatialShapeOptimisation()
    {
        CreateRunFixture(out var inventory, out var solver, out var initial);
        var settled = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(
            solver, inventory.EnumerateBundles(), initial, 0.0));
        Require(solver.TryCommitCandidate(settled.Projection));
        var fuelled = Require(inventory.TryRefuel(
            75, GameRefuellingDirectionV1.TowardEndA, 8, "NAT-U-SYNTHETIC", 0.0)).ResultingState;
        var uncompensated = Require(solver.TrySolveCandidate(fuelled.EnumerateBundles()));
        var retained = Require(solver.TrySolveCandidate(
            Require(solver.TryPrepareCandidates(fuelled.EnumerateBundles())), uncompensated.SpatialSolve,
            Require(settled.State.TryBuildOverlay())));
        var regulated = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(
            solver, fuelled.EnumerateBundles(), settled.State, 0.0));

        Assert.True(Math.Abs(regulated.Projection.RelativeReactivity) < Math.Abs(retained.RelativeReactivity));
        Assert.InRange(Math.Abs(regulated.Projection.RelativeReactivity), 0, 2.0e-5);
        Assert.True(regulated.State.AverageFillFraction > settled.State.AverageFillFraction);
        Assert.Equal(0, regulated.State.SimulationTimeSeconds);
    }

    private static void CreateRunFixture(
        out SyntheticGameCoreStateV1 inventory,
        out EquilibriumCoreSolverV1 solver,
        out PracticeLiquidZoneRrsV1 initial)
    {
        // The calibrated controller operates around the aged half-fill
        // reference; a fresh core exceeds its 6.5 mk control authority.
        inventory = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        solver = Require(
            EquilibriumCoreSolverV1.TryCreate(model, inventory.EnumerateBundles(), 1.0e9));
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        initial = Require(
            PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid)
        {
            Assert.Fail(result.FirstDiagnostic.Message);
        }

        return result.Value;
    }
}
