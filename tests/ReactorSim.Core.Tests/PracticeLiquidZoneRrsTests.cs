using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeLiquidZoneRrsTests
{
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

    private static void CreateRunFixture(
        out SyntheticGameCoreStateV1 inventory,
        out EquilibriumCoreSolverV1 solver,
        out PracticeLiquidZoneRrsV1 initial)
    {
        inventory = SyntheticGameCoreStateV1.CreatePractice();
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
