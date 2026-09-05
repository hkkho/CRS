using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class IqsSolverTests
{
    [Fact]
    public void EmbeddedPackLoadsWithExplicitIqsCadenceAndKinetics()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());

        Assert.Equal("candu6-two-group-iqs-v1-calibrated", pack.DataPackVersion);
        Assert.Equal("candu6-two-group-iqs-full-core-v1", pack.ModelId);
        Assert.Equal("spatial-eigen-iqs-v1", pack.SolverId);
        Assert.Equal(2, pack.EnergyGroupOrder.Count);
        Assert.Equal("fast", pack.EnergyGroupOrder[0]);
        Assert.Equal("thermal", pack.EnergyGroupOrder[1]);
        Assert.Equal(6, pack.BetaGroups.Count);
        Assert.Equal(pack.BetaTotal, pack.BetaGroups.Sum(), 12);
        Assert.Equal(600.0, pack.MaximumMicroStepSeconds);
        Assert.Equal(3600.0, pack.ShapeRecomputeIntervalSeconds);
        Assert.Equal(0.0001, pack.GenerationTimeSeconds, 12);
    }

    [Fact]
    public void ZeroRelativeReactivityMaintainsEquilibriumAmplitudeAndPrecursors()
    {
        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());
        double amplitude = solver.Amplitude;
        double[] precursors = solver.Precursors.ToArray();

        ContractValidationResult<double> result = solver.TryAdvancePointKinetics(600.0);

        Assert.True(result.IsValid, Diagnostic(result));
        Assert.Equal(0.0, solver.RelativeReactivity, 15);
        Assert.Equal(amplitude, solver.Amplitude, 12);
        Assert.Equal(precursors, solver.Precursors, new RelativeDoubleComparer(1e-12));
    }

    [Fact]
    public void InitialIqsShapeMatchesStaticSpatialPowerAndPreservesConstraint()
    {
        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());

        Assert.Equal(
            solver.CurrentSpatialSolve.NodePowerWatts,
            solver.CurrentProjection.ShapeNodePowerWatts,
            new RelativeDoubleComparer(1e-12));
        Assert.Equal(
            solver.CurrentSpatialSolve.TotalPowerWatts,
            solver.CurrentProjection.ShapePowerWatts,
            6);
        Assert.Equal(solver.ShapeConstraint, solver.CurrentProjection.Constraint, 12);
    }

    [Fact]
    public void CandidateFailureAndPreviewLeaveKineticsAndShapeUnchanged()
    {
        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());
        IqsSpatialCandidateV1 beforeProjection = solver.CurrentProjection;
        double beforeAmplitude = solver.Amplitude;
        double[] beforePrecursors = solver.Precursors.ToArray();

        ContractValidationResult<IqsSpatialCandidateV1> failed =
            solver.TrySolveCandidate(Array.Empty<BundleState>());
        ContractValidationResult<IqsSpatialCandidateV1> preview =
            solver.TrySolveCandidate(SyntheticGameCoreStateV1.CreatePractice().EnumerateBundles());

        Assert.False(failed.IsValid);
        Assert.True(preview.IsValid, Diagnostic(preview));
        Assert.Same(beforeProjection, solver.CurrentProjection);
        Assert.Equal(beforeAmplitude, solver.Amplitude, 15);
        Assert.Equal(beforePrecursors, solver.Precursors, new RelativeDoubleComparer(1e-15));
        Assert.InRange(
            Math.Abs(preview.Value.Constraint - solver.ShapeConstraint) / solver.ShapeConstraint,
            0.0,
            1e-12);
    }

    [Fact]
    public void CommittedFreshFuelProducesPositiveKineticsResponse()
    {
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver solver = CreateSolver(state);
        GameRefuellingResultV1 first = Require(state.TryRefuel(
            189, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        GameRefuellingResultV1 second = Require(first.ResultingState.TryRefuel(
            190, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        IqsSpatialCandidateV1 candidate = Require(
            solver.TrySolveCandidate(second.ResultingState.EnumerateBundles()));
        double beforeAmplitude = solver.Amplitude;

        Assert.True(candidate.RelativeReactivity > 0.0);
        Assert.True(Require(solver.TryCommitCandidate(candidate)));
        Assert.True(Require(solver.TryAdvancePointKinetics(600.0)) > beforeAmplitude);
        Assert.InRange(
            Math.Abs(solver.CurrentProjection.Constraint - solver.ShapeConstraint) /
                solver.ShapeConstraint,
            0.0,
            1e-12);
    }

    [Fact]
    public void BurnedShapeProducesNegativeRepeatableKineticsResponse()
    {
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver first = CreateSolver(initial);
        IqsFullCoreSolver second = CreateSolver(initial);
        double[] oneDayEnergy = first.CurrentProjection.ShapeNodePowerWatts
            .Select(power => power * 86400.0)
            .ToArray();
        SyntheticGameCoreStateV1 burned = Require(initial.TryAddFissionEnergy(oneDayEnergy));
        IqsSpatialCandidateV1 firstCandidate = Require(first.TrySolveCandidate(burned.EnumerateBundles()));
        IqsSpatialCandidateV1 secondCandidate = Require(second.TrySolveCandidate(burned.EnumerateBundles()));

        Assert.True(firstCandidate.RelativeReactivity < 0.0);
        Assert.Equal(firstCandidate.RelativeReactivity, secondCandidate.RelativeReactivity, 15);
        Assert.True(Require(first.TryCommitCandidate(firstCandidate)));
        Assert.True(Require(second.TryCommitCandidate(secondCandidate)));
        double firstAmplitude = Require(first.TryAdvancePointKinetics(600.0));
        double secondAmplitude = Require(second.TryAdvancePointKinetics(600.0));
        Assert.True(firstAmplitude < 1.0);
        Assert.Equal(firstAmplitude, secondAmplitude, 15);
        Assert.Equal(first.Precursors, second.Precursors, new RelativeDoubleComparer(1e-15));
    }

    [Fact]
    public void InvalidPackAndOversizedMicroStepFailClosed()
    {
        const string invalid = "{\"schema_version\":1}";
        Assert.False(IqsKineticsDataPackV1.TryLoadJson(invalid).IsValid);

        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());
        double before = solver.Amplitude;
        ContractValidationResult<double> result = solver.TryAdvancePointKinetics(600.001);
        Assert.False(result.IsValid);
        Assert.Equal(before, solver.Amplitude, 15);
    }

    private static IqsFullCoreSolver CreateSolver(SyntheticGameCoreStateV1 state)
    {
        FullCoreDiffusionDataPackV1 spatialPack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(spatialPack));
        IqsKineticsDataPackV1 kineticsPack = Require(
            IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());
        return Require(IqsFullCoreSolver.TryCreate(
            model,
            kineticsPack,
            state.EnumerateBundles(),
            1_000_000_000.0));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, Diagnostic(result));
        return result.Value;
    }

    private static string Diagnostic<T>(ContractValidationResult<T> result)
    {
        return result.IsValid ? string.Empty : result.FirstDiagnostic.ToString();
    }

    private sealed class RelativeDoubleComparer : System.Collections.Generic.IEqualityComparer<double>
    {
        private readonly double _tolerance;

        public RelativeDoubleComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(double x, double y)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
            return Math.Abs(x - y) <= _tolerance * scale;
        }

        public int GetHashCode(double obj)
        {
            return 0;
        }
    }
}
