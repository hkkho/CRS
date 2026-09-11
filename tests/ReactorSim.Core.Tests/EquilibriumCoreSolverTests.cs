using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class EquilibriumCoreSolverTests
{
    [Fact]
    public void EquilibriumProjectionIsStaticCanonicalAndSideEffectFreeUntilCommit()
    {
        FullCoreDiffusionDataPackV1 dataPack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(dataPack));
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        EquilibriumCoreSolverV1 solver = Require(
            EquilibriumCoreSolverV1.TryCreate(model, state.EnumerateBundles(), 1.0e9));
        EquilibriumCoreProjectionV1 accepted = solver.CurrentProjection;

        EquilibriumCoreProjectionV1 candidate = Require(
            solver.TrySolveCandidate(state.EnumerateBundles()));

        Assert.Same(accepted, solver.CurrentProjection);
        Assert.NotSame(accepted, candidate);
        Assert.Equal(380 * 12, candidate.ShapeNodePowerWatts.Count);
        Assert.Equal(380, candidate.ShapeChannelPowerWatts.Count);
        Assert.Equal(380 * 12, candidate.ShapeBundlePowerWatts.Count);
        Assert.Null(candidate.SpatialSolve.XenonCoupling);
        Assert.Equal(
            EquilibriumCoreSolverIdentityV1.ReactivityIdentity,
            candidate.ReactivityIdentity);

        Assert.True(Require(solver.TryCommitCandidate(candidate)));
        Assert.Same(candidate, solver.CurrentProjection);
    }

    [Fact]
    public void PreparedCandidateReusesValidatedInputsWithoutChangingPhysics()
    {
        FullCoreDiffusionDataPackV1 dataPack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(dataPack));
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionSolveResultV1 initial = Require(
            model.TrySolve(state.EnumerateBundles(), 1.0e9));
        FullCoreDiffusionPreparedSolveV1 prepared = Require(
            model.TryPrepareSolve(state.EnumerateBundles()));
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        StaticAbsorptionOverlayV1 overlay = Require(
            mapping.TryBuildOverlay(Enumerable.Repeat(0.58, 14).ToArray()));

        FullCoreDiffusionSolveResultV1 standard = Require(
            model.TrySolve(
                state.EnumerateBundles(),
                overlay,
                1.0e9,
                initial.EffectiveK,
                initial.Group1Flux,
                initial.Group2Flux));
        FullCoreDiffusionSolveResultV1 optimized = Require(
            model.TrySolvePrepared(
                prepared,
                1.0e9,
                initial.EffectiveK,
                initial.Group1Flux,
                initial.Group2Flux,
                overlay));

        Assert.Equal(standard.EffectiveK, optimized.EffectiveK);
        Assert.Equal(standard.Reactivity, optimized.Reactivity);
        Assert.Equal(standard.InventoryBindingDigest, optimized.InventoryBindingDigest);
        Assert.Equal(standard.CoefficientBindingDigest, optimized.CoefficientBindingDigest);
        Assert.Equal(standard.Group1Flux, optimized.Group1Flux);
        Assert.Equal(standard.Group2Flux, optimized.Group2Flux);
        Assert.Equal(standard.NodePowerWatts, optimized.NodePowerWatts);
        Assert.Equal(standard.IterationCount, optimized.IterationCount);

        FullCoreDiffusionModelV1 foreignModel = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(dataPack));
        ContractValidationResult<FullCoreDiffusionSolveResultV1> rejected =
            foreignModel.TrySolvePrepared(
                prepared,
                1.0e9,
                initial.EffectiveK,
                initial.Group1Flux,
                initial.Group2Flux,
                overlay);

        Assert.False(rejected.IsValid);
        Assert.Equal(
            "FullCoreDiffusionSolve.Prepared.OwnerMismatch",
            rejected.FirstDiagnostic.Code);

        ContractValidationResult<SpatialCoefficientSet> reordered =
            prepared.BaseCoefficients.TryRebindCanonicalNodeCoefficients(
                prepared.BaseCoefficients.Nodes.Reverse());
        Assert.False(reordered.IsValid);
        Assert.Equal(
            "SpatialCoefficients.Rebind.Nodes.OrderMismatch",
            reordered.FirstDiagnostic.Code);
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
