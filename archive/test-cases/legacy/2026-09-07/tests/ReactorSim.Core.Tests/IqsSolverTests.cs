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
    private static readonly string[] CanonicalTwoGroupOrder = { "fast", "thermal" };
    private static readonly int[] ReversedTwoGroupOrder = { 1, 0 };

    [Fact]
    public void EmbeddedPackLoadsWithExplicitAdiabaticIdentityAndKinetics()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());

        Assert.Equal("candu6-two-group-adiabatic-v1-project-authored-synthetic-calibrated", pack.DataPackVersion);
        Assert.Equal("project-authored-synthetic-candu6-kinetics", pack.SourceIdentity);
        Assert.Equal(AdiabaticKineticsIdentityV1.ModelId, pack.ModelId);
        Assert.Equal(AdiabaticKineticsIdentityV1.SolverId, pack.SolverId);
        Assert.Equal(AdiabaticKineticsIdentityV1.FormulationId, pack.FormulationId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ShapeMethodId, pack.ShapeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.AmplitudeMethodId, pack.AmplitudeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ReactivityMethodId, pack.ReactivityMethodId);
        Assert.False(pack.UsesLegacyIdentity);
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
    public void LegacyV1IdentityLoadsWithoutChangingTheExplicitActiveFormulation()
    {
        IqsKineticsDataPackV1 pack = Require(IqsKineticsDataPackV1.TryLoadJson(
            CreatePackJson(
                LegacyBetaGroups,
                LegacyDecayConstants,
                includeSourceIdentity: true,
                generationTimeSeconds: 0.0001,
                thermalVelocity: 2200.0)));

        Assert.Equal(AdiabaticKineticsIdentityV1.LegacyModelId, pack.ModelId);
        Assert.Equal(AdiabaticKineticsIdentityV1.LegacySolverId, pack.SolverId);
        Assert.True(pack.UsesLegacyIdentity);
        Assert.Equal(AdiabaticKineticsIdentityV1.FormulationId, pack.FormulationId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ShapeMethodId, pack.ShapeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.AmplitudeMethodId, pack.AmplitudeMethodId);
        Assert.Equal(AdiabaticKineticsIdentityV1.ReactivityMethodId, pack.ReactivityMethodId);
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
    public void ReferenceAdjointIsCanonicalDeterministicSpatiallyVaryingAndDigestBound()
    {
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver first = CreateSolver(state);
        IqsFullCoreSolver second = CreateSolver(state);
        FullCoreAdjointImportanceV1 firstAdjoint = first.ReferenceAdjoint;
        FullCoreAdjointImportanceV1 secondAdjoint = second.ReferenceAdjoint;

        Assert.Equal("candu6-two-group-reference-adjoint-v1", FullCoreAdjointImportanceV1.SchemaId);
        Assert.Equal("candu6-380x12-grid-v1", firstAdjoint.TopologySchemaId);
        Assert.Equal(380u, firstAdjoint.ChannelCount);
        Assert.Equal(12u, firstAdjoint.BundlePositionCount);
        Assert.Equal(4_560, firstAdjoint.NodeCount);
        Assert.Equal(CanonicalTwoGroupOrder, firstAdjoint.EnergyGroupOrder);
        Assert.Equal("fast|thermal", firstAdjoint.EnergyGroupOrderIdentity);
        Assert.Equal(
            SpatialAdjointEigenSolve.NormalizationIdentity,
            firstAdjoint.NormalizationIdentity);
        Assert.Equal(1.0, firstAdjoint.NormalizationValue, 12);
        Assert.True(firstAdjoint.IterationCount > 0);
        Assert.InRange(firstAdjoint.TransposeResidualRelativeInfinity, 0.0, 2.0e-3);
        Assert.True(firstAdjoint.ValidateDigest(firstAdjoint.Digest).IsValid);

        byte[] wrongDigest = new byte[32];
        wrongDigest[0] = 1;
        Assert.False(firstAdjoint.ValidateDigest(new Digest32(wrongDigest)).IsValid);

        Assert.Equal(firstAdjoint.Digest, secondAdjoint.Digest);
        Assert.Equal(
            firstAdjoint.Group1Importance,
            secondAdjoint.Group1Importance,
            new RelativeDoubleComparer(1.0e-14));
        Assert.Equal(
            firstAdjoint.Group2Importance,
            secondAdjoint.Group2Importance,
            new RelativeDoubleComparer(1.0e-14));
        Assert.All(
            firstAdjoint.Group1Importance,
            value => Assert.True(double.IsFinite(value) && value >= 0.0));
        Assert.All(
            firstAdjoint.Group2Importance,
            value => Assert.True(double.IsFinite(value) && value >= 0.0));

        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(10, 10, out uint centerChannel));
        int centerNode = checked((int)centerChannel * (int)firstAdjoint.BundlePositionCount + 6);
        int edgeNode = 6;
        Assert.True(
            firstAdjoint.Group2Importance[centerNode] > firstAdjoint.Group2Importance[edgeNode],
            "The bounded central channel should carry more reference importance than the edge channel.");

        double expectedConstraint = 0.0;
        for (int nodeIndex = 0; nodeIndex < first.CurrentSpatialSolve.Group1Flux.Count; nodeIndex++)
        {
            expectedConstraint += firstAdjoint.DiffusionDataPack.NodeVolumeM3 * (
                firstAdjoint.Group1Importance[nodeIndex] *
                    first.CurrentSpatialSolve.Group1Flux[nodeIndex] / first.DataPack.GroupVelocitiesMPerSecond[0] +
                firstAdjoint.Group2Importance[nodeIndex] *
                    first.CurrentSpatialSolve.Group2Flux[nodeIndex] / first.DataPack.GroupVelocitiesMPerSecond[1]);
        }

        Assert.InRange(
            Math.Abs(expectedConstraint - first.ShapeConstraint) / first.ShapeConstraint,
            0.0,
            1.0e-12);
    }

    [Fact]
    public void ReferenceStateReportsZeroWeightedPerturbationAndSeparateStaticRho()
    {
        IqsFullCoreSolver solver = CreateSolver(SyntheticGameCoreStateV1.CreatePractice());
        IqsSpatialCandidateV1 projection = solver.CurrentProjection;

        Assert.Equal(0.0, projection.WeightedPerturbationReactivity, 15);
        Assert.Equal(0.0, projection.RelativeReactivity, 15);
        Assert.Equal(0.0, projection.ReactivityNumerator, 15);
        Assert.True(projection.ReactivityDenominator > 0.0);
        Assert.Equal(projection.SpatialSolve.Reactivity, projection.StaticReactivity, 15);
        Assert.NotEqual(
            AdiabaticKineticsIdentityV1.StaticReactivityMethodId,
            projection.ReactivityIdentity);
        Assert.Equal(
            AdjointWeightedReactivityIdentityV1.MethodId,
            projection.ReactivityIdentity);
        Assert.Equal(64, projection.ReactivityBindingDigestHex.Length);
    }

    [Fact]
    public void WeightedPerturbationReplayIsDeterministicAndNotStaticKDelta()
    {
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver first = CreateSolver(initial);
        IqsFullCoreSolver second = CreateSolver(initial);
        uint channel = 189;
        SyntheticGameCoreStateV1 firstState = Require(
            initial.TryRefuel(
                channel,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;
        SyntheticGameCoreStateV1 secondState = Require(
            initial.TryRefuel(
                channel,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;

        IqsSpatialCandidateV1 firstCandidate = Require(
            first.TrySolveCandidate(firstState.EnumerateBundles()));
        IqsSpatialCandidateV1 secondCandidate = Require(
            second.TrySolveCandidate(secondState.EnumerateBundles()));

        Assert.Equal(
            firstCandidate.WeightedPerturbationReactivity,
            secondCandidate.WeightedPerturbationReactivity,
            15);
        Assert.Equal(firstCandidate.ReactivityNumerator, secondCandidate.ReactivityNumerator, 15);
        Assert.Equal(firstCandidate.ReactivityDenominator, secondCandidate.ReactivityDenominator, 15);
        Assert.Equal(
            firstCandidate.ReactivityBindingDigestHex,
            secondCandidate.ReactivityBindingDigestHex);
        Assert.True(firstCandidate.WeightedPerturbationReactivity > 0.0);
        Assert.True(
            Math.Abs(firstCandidate.WeightedPerturbationReactivity -
                     firstCandidate.StaticRelativeReactivity) > 1.0e-14);
    }

    [Fact]
    public void EquivalentCentralAndPeripheralPerturbationsFollowReferenceImportance()
    {
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(10, 10, out uint center));
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        SyntheticGameCoreStateV1 equalized = EqualizeChannelBurnup(initial, center, 0);
        IqsFullCoreSolver solver = CreateSolver(equalized);

        SyntheticGameCoreStateV1 centerState = Require(
            equalized.TryRefuel(
                center,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;
        SyntheticGameCoreStateV1 peripheralState = Require(
            equalized.TryRefuel(
                0,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;

        IqsSpatialCandidateV1 central = Require(
            solver.TrySolveCandidate(centerState.EnumerateBundles()));
        IqsSpatialCandidateV1 peripheral = Require(
            solver.TrySolveCandidate(peripheralState.EnumerateBundles()));

        Assert.True(central.WeightedPerturbationReactivity > 0.0);
        Assert.True(peripheral.WeightedPerturbationReactivity > 0.0);
        Assert.True(
            central.WeightedPerturbationReactivity >
            peripheral.WeightedPerturbationReactivity);
        Assert.True(
            central.ReactivityNumerator > peripheral.ReactivityNumerator);
    }

    [Fact]
    public void SymmetryEquivalentPerturbationsProduceEquivalentWeightedReactivity()
    {
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(9, 10, out uint left));
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(12, 10, out uint right));
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver solver = CreateSolver(initial);
        SyntheticGameCoreStateV1 leftState = Require(
            initial.TryRefuel(
                left,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;
        SyntheticGameCoreStateV1 rightState = Require(
            initial.TryRefuel(
                right,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;

        IqsSpatialCandidateV1 leftCandidate = Require(
            solver.TrySolveCandidate(leftState.EnumerateBundles()));
        IqsSpatialCandidateV1 rightCandidate = Require(
            solver.TrySolveCandidate(rightState.EnumerateBundles()));

        Assert.Equal(
            leftCandidate.WeightedPerturbationReactivity,
            rightCandidate.WeightedPerturbationReactivity,
            10);
        AssertRelativeClose(
            leftCandidate.ReactivityNumerator,
            rightCandidate.ReactivityNumerator,
            1.0e-12);
        AssertRelativeClose(
            leftCandidate.ReactivityDenominator,
            rightCandidate.ReactivityDenominator,
            1.0e-12);
    }

    [Fact]
    public void InvalidWeightedDenominatorAndTopologyBindingFailClosed()
    {
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        IqsFullCoreSolver solver = CreateSolver(state);
        int nodeCount = solver.CurrentSpatialSolve.Group1Flux.Count;
        ContractValidationResult<AdjointWeightedReactivityResultV1> zeroDenominator =
            AdjointWeightedReactivityV1.TryCompute(
                solver.ReferenceAdjoint,
                solver.CurrentSpatialSolve.DataPack,
                solver.DataPack,
                solver.CurrentSpatialSolve.Coefficients,
                solver.CurrentSpatialSolve.Coefficients,
                new double[nodeCount],
                new double[nodeCount]);

        Assert.False(zeroDenominator.IsValid, Diagnostic(zeroDenominator));
        Assert.Equal("AdjointReactivity.Denominator.Invalid", zeroDenominator.FirstDiagnostic.Code);

        FullCoreDiffusionModelV1 otherModel = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(solver.CurrentSpatialSolve.DataPack));
        FullCoreDiffusionSolveResultV1 otherSolve = Require(
            otherModel.TrySolve(state.EnumerateBundles(), 1_000_000_000.0));
        ContractValidationResult<AdjointWeightedReactivityResultV1> staleBinding =
            AdjointWeightedReactivityV1.TryCompute(
                solver.ReferenceAdjoint,
                solver.CurrentSpatialSolve.DataPack,
                solver.DataPack,
                otherSolve.Coefficients,
                solver.CurrentSpatialSolve.Coefficients,
                solver.CurrentSpatialSolve.Group1Flux,
                solver.CurrentSpatialSolve.Group2Flux);

        Assert.False(staleBinding.IsValid, Diagnostic(staleBinding));
        Assert.Equal("AdjointReactivity.Topology.BindingMismatch", staleBinding.FirstDiagnostic.Code);
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
        Assert.Equal(0.0, preview.Value.WeightedPerturbationReactivity, 15);
        Assert.Equal(0.0, solver.WeightedPerturbationReactivity, 15);
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

    private static SyntheticGameCoreStateV1 EqualizeChannelBurnup(
        SyntheticGameCoreStateV1 state,
        uint sourceChannel,
        uint targetChannel)
    {
        var deltaEnergy = new double[
            checked((int)(SyntheticGameCoreStateV1.ChannelCount *
                          SyntheticGameCoreStateV1.BundlePositionCount))];
        for (uint position = 0; position < SyntheticGameCoreStateV1.BundlePositionCount; position++)
        {
            BundleState source = state.GetBundle(sourceChannel, position);
            BundleState target = state.GetBundle(targetChannel, position);
            deltaEnergy[checked((int)(targetChannel * SyntheticGameCoreStateV1.BundlePositionCount + position))] =
                (source.CurrentBurnupJPerKgHm - target.CurrentBurnupJPerKgHm) *
                target.HeavyMetalMassKg;
        }

        return Require(state.TryAddFissionEnergy(deltaEnergy));
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

    private static void AssertRelativeClose(double expected, double actual, double tolerance)
    {
        double scale = Math.Max(1.0, Math.Max(Math.Abs(expected), Math.Abs(actual)));
        Assert.True(
            Math.Abs(expected - actual) <= tolerance * scale,
            $"Expected {expected:R} and {actual:R} to be within {tolerance:R} relative tolerance.");
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
