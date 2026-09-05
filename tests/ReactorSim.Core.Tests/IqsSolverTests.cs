using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class IqsSolverTests
{
    [Fact]
    public void EmbeddedPackLoadsWithExplicitAdiabaticCadenceAndKinetics()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());

        Assert.Equal("candu6-two-group-adiabatic-point-kinetics-v1-synthetic", pack.DataPackVersion);
        Assert.Equal(IqsKineticsDataPackV1.SupportedModelId, pack.ModelId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedSolverId, pack.SolverId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedFormulationId, pack.FormulationId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedShapeMethodId, pack.ShapeMethodId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedAmplitudeMethodId, pack.AmplitudeMethodId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedReactivityDiagnosticId, pack.ReactivityDiagnosticId);
        Assert.Equal(2, pack.EnergyGroupOrder.Count);
        Assert.Equal("fast", pack.EnergyGroupOrder[0]);
        Assert.Equal("thermal", pack.EnergyGroupOrder[1]);
        Assert.Equal(6, pack.BetaGroups.Count);
        Assert.Equal(pack.BetaGroups.Count, pack.DecayConstantsPerSecond.Count);
        Assert.Equal(pack.BetaGroups.Count, pack.GroupFamilies.Count);
        Assert.All(pack.GroupFamilies,
            family => Assert.Equal(DelayedNeutronFamilyV1.Fission, family));
        Assert.Equal(pack.BetaTotal, pack.BetaGroups.Sum(), 12);
        Assert.Equal(0.0054, pack.FissionBetaTotal, 12);
        Assert.Equal(0.0, pack.PhotoneutronBetaTotal, 12);
        Assert.Equal(600.0, pack.MaximumMicroStepSeconds);
        Assert.Equal(3600.0, pack.ShapeRecomputeIntervalSeconds);
        Assert.Equal(0.0009, pack.GenerationTimeSeconds, 12);
        Assert.Equal(1.0e7, pack.GroupVelocitiesMPerSecond[0], 12);
        Assert.Equal(2900.0, pack.GroupVelocitiesMPerSecond[1], 12);
        Assert.Contains("project-authored", pack.SourceProvenance);
        Assert.Contains("no photoneutron constants", pack.SourceProvenance);
    }

    [Fact]
    public void DelayedNeutronFamiliesPreserveCompatibilityDefaultAndValidateOrder()
    {
        DelayedNeutronGroupV1 legacyGroup = Require(DelayedNeutronGroupV1.TryCreate(0, 0.001, 0.1));
        Assert.Equal(DelayedNeutronFamilyV1.Fission, legacyGroup.Family);

        DelayedNeutronGroupV1 photoneutronGroup = Require(
            DelayedNeutronGroupV1.TryCreate(
                1,
                0.001,
                0.01,
                DelayedNeutronFamilyV1.Photoneutron));
        DelayedNeutronDataV1 data = Require(DelayedNeutronDataV1.TryCreate(
            Id("66666666-6666-4666-8666-666666666666"),
            "synthetic-family-order-v1",
            Digest(0x66),
            0.0009,
            new[] { legacyGroup, photoneutronGroup }));

        Assert.Equal(2, data.Groups.Count);
        Assert.Equal(DelayedNeutronFamilyV1.Fission, data.Groups[0].Family);
        Assert.Equal(DelayedNeutronFamilyV1.Photoneutron, data.Groups[1].Family);
        Assert.False(
            DelayedNeutronGroupV1.TryCreate(
                0,
                0.001,
                0.1,
                (DelayedNeutronFamilyV1)99).IsValid);

        Assert.False(IqsKineticsDataPackV1.TryLoadJson(CreatePackJson(
            "0.002",
            "0.001, 0.001",
            "0.1, 0.01",
            "\"fission\"")).IsValid);
        ContractValidationResult<IqsKineticsDataPackV1> unknownFamily =
            IqsKineticsDataPackV1.TryLoadJson(CreatePackJson(
                "0.002",
                "0.001, 0.001",
                "0.1, 0.01",
                "\"fission\", \"unknown\""));
        Assert.False(unknownFamily.IsValid);
        Assert.Equal("AdiabaticPointKineticsDataPack.DelayedGroups.Family.Invalid", unknownFamily.FirstDiagnostic.Code);

        IqsKineticsDataPackV1 legacyPack = Require(IqsKineticsDataPackV1.TryLoadJson(CreatePackJson(
            "0.002",
            "0.001, 0.001",
            "0.1, 0.01",
            null,
            true)));
        Assert.All(
            legacyPack.GroupFamilies,
            family => Assert.Equal(DelayedNeutronFamilyV1.Fission, family));
        Assert.Equal(IqsKineticsDataPackV1.SupportedModelId, legacyPack.ModelId);
        Assert.Equal(IqsKineticsDataPackV1.SupportedSolverId, legacyPack.SolverId);
    }

    [Fact]
    public void ArbitraryDelayedGroupCountSizesSolverPrecursorStateFromPack()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadJson(CreatePackJson(
            "0.0025",
            "0.001, 0.001, 0.0005",
            "0.1, 0.01, 0.001",
            "\"fission\", \"fission\", \"fission\"")));
        IqsFullCoreSolver solver = CreateSolver(
            SyntheticGameCoreStateV1.CreatePractice(),
            pack);

        Assert.Equal(3, pack.BetaGroups.Count);
        Assert.Equal(3, solver.Precursors.Count);
        Assert.Equal(1.0, Require(solver.TryAdvancePointKinetics(1.0, 0.0)), 12);
    }

    [Fact]
    public void FuturePhotoneutronFamilyMetadataIsSchemaOnly()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadJson(CreatePackJson(
            "0.001",
            "0.001, 0.0",
            "0.1, 0.01",
            "\"fission\", \"photoneutron\"")));

        Assert.Equal(2, pack.GroupFamilies.Count);
        Assert.Equal(DelayedNeutronFamilyV1.Fission, pack.GroupFamilies[0]);
        Assert.Equal(DelayedNeutronFamilyV1.Photoneutron, pack.GroupFamilies[1]);
        Assert.Equal(0.0, pack.PhotoneutronBetaTotal, 12);
        Assert.Equal(0.0, pack.BetaGroups[1], 12);
    }

    [Fact]
    public void InvalidEffectiveReactivityOverrideLeavesStateUnchangedAndOverrideIsOneStep()
    {
        SyntheticGameCoreStateV1 overrideState = SyntheticGameCoreStateV1.CreatePractice();
        SyntheticGameCoreStateV1 currentState = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver overrideSolver = CreateSolver(overrideState);
        IqsFullCoreSolver currentSolver = CreateSolver(currentState);

        GameRefuellingResultV1 overrideFirst = Require(overrideState.TryRefuel(
            189, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        GameRefuellingResultV1 currentFirst = Require(currentState.TryRefuel(
            189, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        GameRefuellingResultV1 overrideRefuel = Require(overrideFirst.ResultingState.TryRefuel(
            190, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        GameRefuellingResultV1 currentRefuel = Require(currentFirst.ResultingState.TryRefuel(
            190, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0.0));
        IqsSpatialCandidateV1 overrideCandidate = Require(
            overrideSolver.TrySolveCandidate(overrideRefuel.ResultingState.EnumerateBundles()));
        IqsSpatialCandidateV1 currentCandidate = Require(
            currentSolver.TrySolveCandidate(currentRefuel.ResultingState.EnumerateBundles()));
        Assert.True(Require(overrideSolver.TryCommitCandidate(overrideCandidate)));
        Assert.True(Require(currentSolver.TryCommitCandidate(currentCandidate)));

        double beforeAmplitude = overrideSolver.Amplitude;
        double[] beforePrecursors = overrideSolver.Precursors.ToArray();
        ContractValidationResult<double> invalid =
            overrideSolver.TryAdvancePointKinetics(1.0, double.NaN);

        Assert.False(invalid.IsValid);
        Assert.Equal("IqsFullCoreSolver.RelativeReactivity.Invalid", invalid.FirstDiagnostic.Code);
        Assert.Equal(beforeAmplitude, overrideSolver.Amplitude, 15);
        Assert.Equal(beforePrecursors, overrideSolver.Precursors, new RelativeDoubleComparer(1e-15));

        double overriddenAmplitude = Require(
            overrideSolver.TryAdvancePointKinetics(0.01, 0.0));
        double currentAmplitude = Require(currentSolver.TryAdvancePointKinetics(0.01));
        Assert.True(overrideSolver.RelativeReactivity > 0.0);
        Assert.True(overriddenAmplitude < currentAmplitude);
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
    public void InitialAdiabaticShapeMatchesStaticSpatialPowerAndPreservesConstraint()
    {
        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());

        Assert.Equal(4560, solver.ReferenceAdjointGroup1.Count);
        Assert.Equal(4560, solver.ReferenceAdjointGroup2.Count);
        Assert.Equal(
            SpatialAdjointReferenceSolutionV1.SolverIdentity,
            solver.ReferenceAdjointSolverIdentity);
        Assert.Equal(
            solver.CurrentSpatialSolve.DataPack.Descriptor.TopologySchemaId,
            solver.ReferenceAdjointTopologySchemaId);
        Assert.Equal(
            solver.CurrentSpatialSolve.DataPack.Descriptor.DataPackVersion,
            solver.ReferenceAdjointDataPackVersion);
        Assert.NotNull(solver.ReferenceAdjointDataPackDigest);
        Assert.True(solver.ReferenceAdjointGroup1.Distinct().Count() > 1);
        Assert.True(solver.ReferenceAdjointGroup2.Distinct().Count() > 1);
        Assert.True(
            solver.ReferenceAdjointResidualRelativeInfinity < 1e-3,
            $"reference adjoint residual={solver.ReferenceAdjointResidualRelativeInfinity:R}, iterations={solver.ReferenceAdjointIterationCount}");
        Assert.Equal(
            solver.CurrentSpatialSolve.NodePowerWatts,
            solver.CurrentProjection.ShapeNodePowerWatts,
            new RelativeDoubleComparer(1e-12));
        Assert.Equal(
            solver.CurrentSpatialSolve.TotalPowerWatts,
            solver.CurrentProjection.ShapePowerWatts,
            6);
        Assert.Equal(solver.ShapeConstraint, solver.CurrentProjection.Constraint, 12);
        Assert.Equal(0.0, solver.CurrentProjection.FirstOrderAdjointWeightedReactivity, 15);
        Assert.Equal(0.0, solver.CurrentProjection.StaticEigenvalueDeltaReactivity, 15);
        Assert.True(solver.CurrentProjection.AdjointWeightedDenominator > 0.0);
        Assert.Equal(
            "static-eigenmode-reference-adjoint-jacobi-v1",
            solver.ReferenceAdjointSolverIdentity);
        Assert.Equal(solver.CurrentSpatialSolve.Group1Flux.Count, solver.ReferenceAdjointGroup1.Count);
        Assert.Equal(solver.CurrentSpatialSolve.Group2Flux.Count, solver.ReferenceAdjointGroup2.Count);
        Assert.True(solver.ReferenceAdjointGroup1.Max() > solver.ReferenceAdjointGroup1.Min());
        Assert.Contains("adiabatic-point-kinetics-static-eigenmode-v1", solver.SolverIdentity);
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
        string betaTotal,
        string groupFractions,
        string decayConstants,
        string? groupFamilies,
        bool legacyIdentity = false)
    {
        string familyProperty = groupFamilies == null
            ? string.Empty
            : "    \"group_families\": [" + groupFamilies + "],\n";
        string modelId = legacyIdentity
            ? IqsKineticsDataPackV1.LegacySupportedModelId
            : IqsKineticsDataPackV1.SupportedModelId;
        string solverId = legacyIdentity
            ? IqsKineticsDataPackV1.LegacySupportedSolverId
            : IqsKineticsDataPackV1.SupportedSolverId;
        return "{\n" +
            "  \"schema_version\": 1,\n" +
            "  \"data_pack_version\": \"synthetic-iqs-test-v1\",\n" +
            "  \"topology_schema_id\": \"candu6-380x12-grid-v1\",\n" +
            "  \"units_profile_id\": \"SI-v1\",\n" +
            "  \"model_id\": \"" + modelId + "\",\n" +
            "  \"solver_id\": \"" + solverId + "\",\n" +
            "  \"energy_group_order\": [\"fast\", \"thermal\"],\n" +
            "  \"evidence_class\": \"synthetic-test\",\n" +
            "  \"source_provenance\": \"project-authored synthetic test pack\",\n" +
            "  \"delayed_neutron_data\": {\n" +
            "    \"beta_total\": " + betaTotal + ",\n" +
            "    \"group_fractions\": [" + groupFractions + "],\n" +
            "    \"decay_constants_per_sec\": [" + decayConstants + "],\n" +
            familyProperty +
            "    \"group_velocities_m_per_s\": [1.0e7, 2900.0]\n" +
            "  },\n" +
            "  \"time_integration\": {\n" +
            "    \"generation_time_seconds\": 0.0009,\n" +
            "    \"maximum_micro_step_seconds\": 600.0,\n" +
            "    \"shape_recompute_interval_seconds\": 3600.0\n" +
            "  }\n" +
            "}";
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
