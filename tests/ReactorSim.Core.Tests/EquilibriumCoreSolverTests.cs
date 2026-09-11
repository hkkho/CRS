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

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid)
        {
            Assert.Fail(result.FirstDiagnostic.Message);
        }

        return result.Value;
    }
}
