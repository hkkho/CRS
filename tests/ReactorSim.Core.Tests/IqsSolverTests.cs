using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class IqsSolverTests
{
    private static readonly double[] LegacyBetaGroups =
        { 0.00022, 0.00114, 0.00098, 0.00221, 0.00062, 0.00023 };
    private static readonly double[] LegacyDecayConstants =
        { 0.0124, 0.0305, 0.111, 0.301, 1.14, 3.01 };
    private static readonly double[] TwoGroupFractions = { 0.0027, 0.0027 };
    private static readonly double[] OneGroupDecayConstants = { 0.0124 };
    private static readonly double[] TwoGroupDecayConstants = { 0.0124, 0.0305 };
    private static readonly string[] OneFissionFamily =
        { IqsKineticsDataPackV1.FissionGroupFamily };
    private static readonly string[] FissionAndUnknownFamilies =
        { IqsKineticsDataPackV1.FissionGroupFamily, "unknown" };
    private static readonly string[] FissionAndPhotoneutronFamilies =
        { IqsKineticsDataPackV1.FissionGroupFamily, IqsKineticsDataPackV1.PhotoneutronGroupFamily };
    private static readonly int[] ReversedTwoGroupOrder = { 1, 0 };

    [Fact]
    public void EmbeddedPackLoadsWithExplicitIqsCadenceAndKinetics()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());

        Assert.Equal("candu6-two-group-iqs-v1-project-authored-synthetic-calibrated", pack.DataPackVersion);
        Assert.Equal("project-authored-synthetic-candu6-kinetics", pack.SourceIdentity);
        Assert.Equal("candu6-two-group-iqs-full-core-v1", pack.ModelId);
        Assert.Equal("spatial-eigen-iqs-v1", pack.SolverId);
        Assert.Equal(2, pack.EnergyGroupOrder.Count);
        Assert.Equal("fast", pack.EnergyGroupOrder[0]);
        Assert.Equal("thermal", pack.EnergyGroupOrder[1]);
        Assert.Equal(6, pack.BetaGroups.Count);
        Assert.Equal(6, pack.DelayedGroupCount);
        Assert.Equal(pack.BetaTotal, pack.BetaGroups.Sum(), 12);
        Assert.Equal(2900.0, pack.GroupVelocitiesMPerSecond[1], 12);
        Assert.Equal(Enumerable.Range(0, 6), pack.DelayedGroupOrder);
        Assert.All(
            pack.GroupFamilies,
            family => Assert.Equal(IqsKineticsDataPackV1.FissionGroupFamily, family));
        Assert.DoesNotContain(
            IqsKineticsDataPackV1.PhotoneutronGroupFamily,
            pack.GroupFamilies);
        Assert.True(pack.SourceProvenance.Contains(
            "project-authored synthetic CANDU calibration",
            StringComparison.Ordinal));
        Assert.Equal(600.0, pack.MaximumMicroStepSeconds);
        Assert.Equal(3600.0, pack.ShapeRecomputeIntervalSeconds);
        Assert.Equal(0.0009, pack.GenerationTimeSeconds, 12);
    }

    [Fact]
    public void LegacySixGroupArraysLoadWithFissionFamilyDefaults()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadJson(
            CreatePackJson(
                LegacyBetaGroups,
                LegacyDecayConstants,
                includeSourceIdentity: false,
                generationTimeSeconds: 0.0001,
                thermalVelocity: 2200.0)));

        Assert.Equal(6, pack.DelayedGroupCount);
        Assert.Equal("fixture-v1", pack.SourceIdentity);
        Assert.Equal(0.0001, pack.GenerationTimeSeconds, 12);
        Assert.Equal(2200.0, pack.GroupVelocitiesMPerSecond[1], 12);
        Assert.Equal(Enumerable.Range(0, 6), pack.DelayedGroupOrder);
        Assert.All(
            pack.GroupFamilies,
            family => Assert.Equal(IqsKineticsDataPackV1.FissionGroupFamily, family));
    }

    [Fact]
    public void MoreThanSixSyntheticGroupsLoadAndAdvanceDeterministically()
    {
        double[] betaGroups = { 0.0005, 0.0007, 0.0009, 0.0011, 0.0008, 0.0006, 0.0008 };
        double[] decayConstants = { 0.012, 0.025, 0.08, 0.2, 0.7, 1.6, 3.2 };
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadJson(
            CreatePackJson(betaGroups, decayConstants)));

        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver first = CreateSolver(state, pack);
        IqsFullCoreSolver second = CreateSolver(state, pack);

        Assert.Equal(7, first.Precursors.Count);
        Assert.Equal(betaGroups.Sum(), pack.BetaTotal, 12);
        Assert.Equal(Enumerable.Range(0, 7), pack.DelayedGroupOrder);

        double firstAmplitude = Require(first.TryAdvancePointKinetics(600.0));
        double secondAmplitude = Require(second.TryAdvancePointKinetics(600.0));

        Assert.Equal(1.0, firstAmplitude, 12);
        Assert.Equal(firstAmplitude, secondAmplitude, 15);
        Assert.Equal(first.Precursors, second.Precursors, new RelativeDoubleComparer(1e-15));
        Assert.All(
            first.Precursors,
            precursor => Assert.True(double.IsFinite(precursor) && precursor >= 0.0));
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

    [Fact]
    public void DelayedGroupDimensionAndFamilyMismatchesFailClosed()
    {
        ContractValidationResult<IqsKineticsDataPackV1> dimension =
            IqsKineticsDataPackV1.TryLoadJson(
                CreatePackJson(
                    TwoGroupFractions,
                    OneGroupDecayConstants));
        AssertInvalid(dimension, "IqsDataPack.DelayedGroups.Dimension");

        ContractValidationResult<IqsKineticsDataPackV1> familyDimension =
            IqsKineticsDataPackV1.TryLoadJson(
                CreatePackJson(
                    TwoGroupFractions,
                    TwoGroupDecayConstants,
                    groupFamilies: OneFissionFamily));
        AssertInvalid(familyDimension, "IqsDataPack.GroupFamilies.Dimension");

        ContractValidationResult<IqsKineticsDataPackV1> unsupportedFamily =
            IqsKineticsDataPackV1.TryLoadJson(
                CreatePackJson(
                    TwoGroupFractions,
                    TwoGroupDecayConstants,
                    groupFamilies: FissionAndUnknownFamilies));
        AssertInvalid(unsupportedFamily, "IqsDataPack.GroupFamilies.Unsupported");

        ContractValidationResult<IqsKineticsDataPackV1> orderMismatch =
            IqsKineticsDataPackV1.TryLoadJson(
                CreatePackJson(
                    TwoGroupFractions,
                    TwoGroupDecayConstants,
                    groupOrder: ReversedTwoGroupOrder));
        AssertInvalid(orderMismatch, "IqsDataPack.DelayedGroups.Order.Invalid");

        ContractValidationResult<IqsKineticsDataPackV1> missingSourceIdentity =
            IqsKineticsDataPackV1.TryLoadJson(
                CreatePackJson(
                    TwoGroupFractions,
                    TwoGroupDecayConstants,
                    groupFamilies: FissionAndPhotoneutronFamilies,
                    includeSourceIdentity: false));
        AssertInvalid(missingSourceIdentity, "IqsDataPack.SourceIdentity.Missing");
    }

    private static IqsFullCoreSolver CreateSolver(SyntheticGameCoreStateV1 state)
    {
        return CreateSolver(
            state,
            Require(IqsKineticsDataPackV1.TryLoadEmbeddedCandu6()));
    }

    private static IqsFullCoreSolver CreateSolver(
        SyntheticGameCoreStateV1 state,
        IqsKineticsDataPackV1 kineticsPack)
    {
        FullCoreDiffusionDataPackV1 spatialPack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(spatialPack));
        return Require(IqsFullCoreSolver.TryCreate(
            model,
            kineticsPack,
            state.EnumerateBundles(),
            1_000_000_000.0));
    }

    private static string CreatePackJson(
        double[] betaGroups,
        double[] decayConstants,
        string[]? groupFamilies = null,
        int[]? groupOrder = null,
        bool includeSourceIdentity = true,
        double generationTimeSeconds = 0.0009,
        double thermalVelocity = 2900.0)
    {
        string sourceIdentity = includeSourceIdentity
            ? ",\n  \"source_identity\": \"project-authored-synthetic-candu6-fixture\""
            : string.Empty;
        string families = groupFamilies == null
            ? string.Empty
            : ",\n    \"group_families\": [ \"" +
              string.Join("\", \"", groupFamilies) + "\" ]";
        string order = groupOrder == null
            ? string.Empty
            : ",\n    \"group_order\": [ " + string.Join(", ", groupOrder) + " ]";

        return $@"{{
  ""schema_version"": 1,
  ""data_pack_version"": ""fixture-v1""{sourceIdentity},
  ""topology_schema_id"": ""candu6-380x12-grid-v1"",
  ""units_profile_id"": ""SI-v1"",
  ""model_id"": ""candu6-two-group-iqs-full-core-v1"",
  ""solver_id"": ""spatial-eigen-iqs-v1"",
  ""energy_group_order"": [ ""fast"", ""thermal"" ],
  ""evidence_class"": ""project-authored-synthetic-calibration"",
  ""source_provenance"": ""project-authored synthetic CANDU calibration fixture"",
  ""delayed_neutron_data"": {{
    ""beta_total"": {FormatNumber(betaGroups.Sum())},
    ""group_fractions"": [ {JoinNumbers(betaGroups)} ],
    ""decay_constants_per_sec"": [ {JoinNumbers(decayConstants)} ],
    ""group_velocities_m_per_s"": [ 1.0e7, {FormatNumber(thermalVelocity)} ]{families}{order}
  }},
  ""time_integration"": {{
    ""generation_time_seconds"": {FormatNumber(generationTimeSeconds)},
    ""maximum_micro_step_seconds"": 600.0,
    ""shape_recompute_interval_seconds"": 3600.0
  }}
}}";
    }

    private static string JoinNumbers(IEnumerable<double> values)
    {
        return string.Join(", ", values.Select(FormatNumber));
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
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

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid, Diagnostic(result));
        Assert.Equal(code, result.FirstDiagnostic.Code);
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
