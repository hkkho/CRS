using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeSingleSolveTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void InitializationSettlesThenEveryStepUsesLastAcceptedSnapshotEvenBeforeCommit(int checks)
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2064e6));
        var original = solver.CurrentProjection;
        var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(
            Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), original));
        var settled = Require(PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial));
        Assert.Null(settled.WarmStartProjection);
        Assert.True(settled.ControllerConverged);
        Assert.InRange(Math.Abs(settled.Projection.RelativeReactivity), 0, PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance);
        Assert.Equal(0, settled.SimulationTimeSeconds);
        Assert.Same(original, solver.CurrentProjection);

        var xenon = PracticeXenonStateV1.CreateEquilibrium(core, settled.Projection, 0);
        var fuelAfterFirstInterval = Require(core.TryAddFissionEnergy(
            settled.Projection.ShapeNodePowerWatts.Select(power => power * 180).ToArray()));
        var poisonAfterFirstInterval = xenon.Advance(settled.Projection, 1, 180);
        ContractValidationResult<PracticeSingleSolveStateV1> Run(PracticeSingleSolveStateV1 previous, double time) =>
            checks == 1
                ? PracticeLiquidZoneRrsV1.TryRunSingleSolve(solver, fuelAfterFirstInterval.EnumerateBundles(), previous, time, poisonAfterFirstInterval.Overlay)
                : PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver, fuelAfterFirstInterval.EnumerateBundles(), previous, time, poisonAfterFirstInterval.Overlay);
        var first = Require(Run(settled, 180));
        Assert.Same(settled.Projection, first.WarmStartProjection);
        Assert.Equal(checks, first.CandidateSolveCount);
        Assert.NotNull(first.PredictedProjection);
        Assert.Equal(checks == 2, first.FineTunedProjection != null);
        Assert.Equal(first.PredictedProjection.SolverIterationCount + (first.FineTunedProjection?.SolverIterationCount ?? 0), first.SpatialIterationCount);
        Assert.All(first.ZoneFills.Zip(settled.ZoneFills, (next, prior) => Math.Abs(next - prior)),
            movement => Assert.InRange(movement, 0, 0.008 + 1e-12));
        var second = Require(Run(first, 360));
        Assert.Same(first.Projection, second.WarmStartProjection);
        Assert.Equal(checks, second.CandidateSolveCount);
        Assert.Same(original, solver.CurrentProjection);
        var repeated = Require(Run(first, 360));
        Assert.Equal(second.ZoneFills, repeated.ZoneFills);
        Assert.Equal(second.Projection.SpatialSolve.Group1Flux, repeated.Projection.SpatialSolve.Group1Flux);
        Assert.Equal(second.Projection.EffectiveK, repeated.Projection.EffectiveK);

        var failed = checks == 1
            ? PracticeLiquidZoneRrsV1.TryRunSingleSolve(solver, Array.Empty<BundleState>(), second, 540)
            : PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver, Array.Empty<BundleState>(), second, 540);
        Assert.False(failed.IsValid);
        Assert.Same(original, solver.CurrentProjection);
        Assert.False(Run(second, 360).IsValid);
    }

    [Fact]
    public void SecondCheckFineTunesMeasuredPredictionWithOneSharedMovementBudget()
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2064e6));
        var original = solver.CurrentProjection;
        var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(
            Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), original));
        var settled = Require(PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial));
        var poison = PracticeXenonStateV1.CreateEquilibrium(core, settled.Projection, 0);
        var fuelled = Require(core.TryRefuel(210, GameRefuellingDirectionV1.TowardEndA, 8,
            "NAT-U-SYNTHETIC", 0)).ResultingState;
        poison = poison.Rebind(fuelled).Advance(settled.Projection, 0.95, 180);
        var single = Require(PracticeLiquidZoneRrsV1.TryRunSingleSolve(solver,
            fuelled.EnumerateBundles(), settled, 180, poison.Overlay));
        var two = Require(PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver,
            fuelled.EnumerateBundles(), settled, 180, poison.Overlay));
        Assert.Equal(2, two.CandidateSolveCount);
        Assert.NotNull(two.PredictedProjection);
        Assert.NotNull(two.FineTunedProjection);
        Assert.Equal(single.Projection.EffectiveK, two.PredictedProjection.EffectiveK);
        Assert.Equal(single.Projection.SpatialSolve.Group1Flux, two.PredictedProjection.SpatialSolve.Group1Flux);
        Assert.True(two.FineTuningAccepted);
        Assert.Same(two.FineTunedProjection, two.Projection);
        Assert.True(Math.Abs(two.Projection.RelativeReactivity) < Math.Abs(single.Projection.RelativeReactivity));
        Assert.All(two.ZoneFills.Zip(settled.ZoneFills, (next, prior) => Math.Abs(next - prior)),
            movement => Assert.InRange(movement, 0, 0.008 + 1e-12));
        Assert.Equal(180, poison.SimulationTimeSeconds);
        Assert.Equal(180, two.SimulationTimeSeconds);
        Assert.Same(original, solver.CurrentProjection);
    }

    [Fact]
    public void InventoryEventsCanBeCheckedSequentiallyWithoutAdvancingTime()
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2064e6));
        var original = solver.CurrentProjection;
        var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(
            Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), original));
        var accepted = Require(PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial));
        var poison = PracticeXenonStateV1.CreateEquilibrium(core, accepted.Projection, 0);
        foreach (uint channel in new uint[] { 210, 75 })
        {
            var previous = accepted;
            core = Require(core.TryRefuel(channel, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0)).ResultingState;
            poison = poison.Rebind(core);
            accepted = Require(PracticeLiquidZoneRrsV1.TryRunTwoSolveInventoryEvent(solver, core.EnumerateBundles(), previous, 0, poison.Overlay));
            Assert.Equal(2, accepted.CandidateSolveCount);
            Assert.Same(previous.Projection, accepted.WarmStartProjection);
            Assert.Equal(0, accepted.SimulationTimeSeconds);
            Assert.Equal(0, poison.SimulationTimeSeconds);
            Assert.All(accepted.ZoneFills.Zip(previous.ZoneFills, (next, prior) => Math.Abs(next - prior)),
                movement => Assert.InRange(movement, 0, PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent + 1e-12));
            Assert.Same(original, solver.CurrentProjection);
        }
        Assert.False(PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver, core.EnumerateBundles(), accepted, 0, poison.Overlay).IsValid);
    }

    [Fact]
    public void BootstrapDoesNotReturnUnsettledStateWhenPassBudgetIsExhausted()
    {
        var core = SyntheticGameCoreStateV1.CreatePractice();
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2064e6));
        var original = solver.CurrentProjection;
        var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(
            Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), original));
        var result = PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial, maximumPasses: 1);
        Assert.False(result.IsValid);
        Assert.Equal("PracticeSingleSolve.Initialization.NotSettled", result.FirstDiagnostic.Code);
        Assert.Same(original, solver.CurrentProjection);
    }

    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
