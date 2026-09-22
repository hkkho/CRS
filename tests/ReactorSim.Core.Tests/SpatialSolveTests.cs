using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class SpatialSolveTests
{
    private static readonly string[] ExpectedEnergyGroupOrder = { "fast", "thermal" };

    [Fact]
    public void ManufacturedTwoAndThreeNodeSolvesAreFiniteNormalizedAndSymmetric()
    {
        // A one-channel/two-position stencil keeps the manufactured fixture
        // connected while the topology contract also supports one-position
        // lattice references.
        foreach (int positionCount in new[] { 2, 3 })
        {
            ManufacturedSolveFixture fixture = CreateFixture(positionCount);
            SpatialSolveResult result = Require(fixture.Solve.TrySolve());

            Assert.Equal(SpatialSolveStatus.Converged, result.Status);
            Assert.True(result.HasUsableState);
            Assert.NotNull(result.FinalState);
            Assert.Equal(positionCount, result.FinalState!.Group1Flux.Count);
            Assert.Equal(positionCount, result.FinalState.Group2Flux.Count);
            Assert.Equal(fixture.TargetPowerWatts, result.FinalState.TotalPowerW, 10);
            Assert.InRange(
                result.Diagnostics.PowerBalanceRelative!.Value,
                0.0,
                1e-10);

            double[] powers = NodePowers(fixture, result.FinalState);
            Assert.All(
                result.FinalState.Group1Flux,
                flux => Assert.True(double.IsFinite(flux) && flux >= 0.0));
            Assert.All(
                result.FinalState.Group2Flux,
                flux => Assert.True(double.IsFinite(flux) && flux >= 0.0));
            Assert.All(
                powers,
                power => Assert.True(double.IsFinite(power) && power >= 0.0));
            Assert.Equal(fixture.TargetPowerWatts, powers.Sum(), 10);
            Assert.Equal(powers[0], powers[powers.Length - 1], 10);
            Assert.Equal(
                result.FinalState.Group1Flux[0],
                result.FinalState.Group1Flux[result.FinalState.Group1Flux.Count - 1],
                10);
            Assert.Equal(
                result.FinalState.Group2Flux[0],
                result.FinalState.Group2Flux[result.FinalState.Group2Flux.Count - 1],
                10);

            SpatialNodeStencil first = fixture.Stencil.Nodes[0];
            SpatialNodeStencil last = fixture.Stencil.Nodes[fixture.Stencil.NodeCount - 1];
            Assert.Contains(
                first.BoundaryTerms,
                boundary => boundary.Face == TopologyFace.EndA &&
                            boundary.Classification == BoundaryClassification.Vacuum);
            Assert.Contains(
                last.BoundaryTerms,
                boundary => boundary.Face == TopologyFace.EndB &&
                            boundary.Classification == BoundaryClassification.Vacuum);
            Assert.All(
                fixture.Stencil.Nodes.SelectMany(node => node.BoundaryTerms)
                    .Where(boundary => boundary.Face != TopologyFace.EndA &&
                                       boundary.Face != TopologyFace.EndB),
                boundary => Assert.Equal(BoundaryClassification.Reflective, boundary.Classification));
        }
    }

    [Fact]
    public void ReorderedCoefficientRecordsProduceTheSameOperatorAndSolve()
    {
        ManufacturedSolveFixture ordered = CreateFixture(3, reverseRecords: false);
        ManufacturedSolveFixture reordered = CreateFixture(3, reverseRecords: true);

        double[] group1Flux = { 1.0, 1.25, 0.75 };
        double[] group2Flux = { 0.8, 1.1, 1.4 };
        double[] orderedGroup1 = new double[3];
        double[] reorderedGroup1 = new double[3];
        double[] orderedGroup2 = new double[3];
        double[] reorderedGroup2 = new double[3];

        Assert.True(
            ordered.Operator.TryApply(
                SpatialEnergyGroup.Group1,
                group1Flux,
                orderedGroup1,
                out ContractDiagnostic orderedGroup1Diagnostic),
            orderedGroup1Diagnostic?.ToString());
        Assert.True(
            reordered.Operator.TryApply(
                SpatialEnergyGroup.Group1,
                group1Flux,
                reorderedGroup1,
                out ContractDiagnostic reorderedGroup1Diagnostic),
            reorderedGroup1Diagnostic?.ToString());
        Assert.True(
            ordered.Operator.TryApply(
                SpatialEnergyGroup.Group2,
                group2Flux,
                orderedGroup2,
                out ContractDiagnostic orderedGroup2Diagnostic),
            orderedGroup2Diagnostic?.ToString());
        Assert.True(
            reordered.Operator.TryApply(
                SpatialEnergyGroup.Group2,
                group2Flux,
                reorderedGroup2,
                out ContractDiagnostic reorderedGroup2Diagnostic),
            reorderedGroup2Diagnostic?.ToString());
        Assert.Equal(orderedGroup1, reorderedGroup1);
        Assert.Equal(orderedGroup2, reorderedGroup2);

        SpatialSolveResult first = Require(ordered.Solve.TrySolve());
        SpatialSolveResult second = Require(reordered.Solve.TrySolve());
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Diagnostics.IterationCount, second.Diagnostics.IterationCount);
        Assert.Equal(first.Diagnostics.ConvergenceReason, second.Diagnostics.ConvergenceReason);
        Assert.Equal(first.Diagnostics.EigenvalueChangeAbsolute, second.Diagnostics.EigenvalueChangeAbsolute);
        Assert.Equal(first.Diagnostics.EigenvalueChangeRelative, second.Diagnostics.EigenvalueChangeRelative);
        Assert.Equal(first.Diagnostics.ResidualAbsoluteInfinity, second.Diagnostics.ResidualAbsoluteInfinity);
        Assert.Equal(first.Diagnostics.ResidualRelativeInfinity, second.Diagnostics.ResidualRelativeInfinity);
        Assert.Equal(first.Diagnostics.SourceShapeChangeInfinity, second.Diagnostics.SourceShapeChangeInfinity);
        Assert.Equal(first.Diagnostics.PowerBalanceRelative, second.Diagnostics.PowerBalanceRelative);
        Assert.NotNull(first.FinalState);
        Assert.NotNull(second.FinalState);
        Assert.Equal(first.FinalState!.Eigenvalue, second.FinalState!.Eigenvalue, 12);
        Assert.Equal(first.FinalState.Group1Flux, second.FinalState.Group1Flux);
        Assert.Equal(first.FinalState.Group2Flux, second.FinalState.Group2Flux);
    }

    [Fact]
    public void InvalidManufacturedInputsFailClosedAndExhaustedSolveHasNoUsableState()
    {
        ManufacturedSolveFixture fixture = CreateFixture(3);
        double[] invalidGroup1Flux = { double.NaN, 1.0, 1.0 };
        double[] validGroup2Flux = { 1.0, 1.0, 1.0 };

        AssertInvalid(
            SpatialEigenIteration.TryCreate(
                fixture.Stencil,
                fixture.Coefficients,
                fixture.LinearSolvePolicy,
                fixture.TargetPowerWatts,
                1.0,
                invalidGroup1Flux,
                validGroup2Flux),
            "SpatialEigenIteration.InitialFlux.Invalid");

        AssertInvalid(
            SpatialCoefficientSet.TryCreate(
                fixture.Stencil,
                fixture.NodeCoefficients.Skip(1),
                fixture.EdgeConductances,
                fixture.BoundaryConductances),
            "SpatialCoefficients.Node.Missing");

        AssertInvalid(
            SpatialCoefficientSet.TryCreate(
                fixture.Stencil,
                fixture.NodeCoefficients.Concat(new[] { fixture.NodeCoefficients[0] }),
                fixture.EdgeConductances,
                fixture.BoundaryConductances),
            "SpatialCoefficients.Node.Duplicate");

        SpatialBoundaryConductance invalidBoundary = fixture.BoundaryConductances
            .Select((boundary, index) => index == 0
                ? new SpatialBoundaryConductance(boundary.Node, boundary.Face, 1.0, 1.0)
                : boundary)
            .First();
        SpatialBoundaryConductance[] invalidBoundaries = fixture.BoundaryConductances
            .Select((boundary, index) => index == 0 ? invalidBoundary : boundary)
            .ToArray();
        AssertInvalid(
            SpatialCoefficientSet.TryCreate(
                fixture.Stencil,
                fixture.NodeCoefficients,
                fixture.EdgeConductances,
                invalidBoundaries),
            "SpatialCoefficients.Boundary.ReflectiveNonZero");

        SpatialSolveResult accepted = Require(CreateFixture(3).Solve.TrySolve());
        double[] acceptedGroup1 = accepted.FinalState!.Group1Flux.ToArray();
        double[] acceptedGroup2 = accepted.FinalState.Group2Flux.ToArray();
        ManufacturedSolveFixture exhaustedFixture = CreateFixture(3, maximumIterations: 1);
        SpatialSolveResult exhausted = Require(exhaustedFixture.Solve.TrySolve());

        Assert.Equal(SpatialSolveStatus.Nonconverged, exhausted.Status);
        Assert.False(exhausted.HasUsableState);
        Assert.Null(exhausted.FinalState);
        Assert.Equal("maximum_iterations_exhausted", exhausted.Diagnostics.ConvergenceReason);
        Assert.Equal("SpatialEigenSolve.Nonconverged", exhausted.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(1, exhausted.Diagnostics.IterationCount);
        Assert.Equal(acceptedGroup1, accepted.FinalState.Group1Flux);
        Assert.Equal(acceptedGroup2, accepted.FinalState.Group2Flux);
    }

    [Fact]
    public void EmbeddedPracticePackSolvesAllCanonicalNodesWithConsistentPowerSums()
    {
        const double targetPowerWatts = 1_000_000_000.0;
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionSolveResultV1 result = Require(
            model.TrySolve(state.EnumerateBundles(), targetPowerWatts));

        Assert.Equal(Candu6CoreTopologyFactoryV1.TopologySchemaId, pack.Descriptor.TopologySchemaId);
        Assert.Equal(Candu6CoreTopologyFactoryV1.ChannelCount, pack.Descriptor.ChannelCount);
        Assert.Equal(Candu6CoreTopologyFactoryV1.BundlePositionCount, pack.Descriptor.BundlePositionCount);
        Assert.Equal(FullCoreDiffusionDataPackV1.SupportedUnitsProfileId, pack.Descriptor.UnitsProfileId);
        Assert.Equal(ExpectedEnergyGroupOrder, pack.EnergyGroupOrder);
        Assert.True(pack.Descriptor.ValidateCompatibility(model.Topology).IsValid);
        Assert.Equal(4560, model.NodeCount);
        Assert.Equal(4560, result.Coefficients.NodeCount);
        Assert.Equal(4560, result.Group1Flux.Count);
        Assert.Equal(4560, result.Group2Flux.Count);
        Assert.Equal(4560, result.NodePowerWatts.Count);
        Assert.Equal(4560, state.EnumerateBundles().Count());
        Assert.Equal(targetPowerWatts, result.TotalPowerWatts, 3);
        Assert.Equal(targetPowerWatts, result.NodePowerWatts.Sum(), 3);
        Assert.InRange(result.PowerBalanceRelativeError, 0.0, 1e-10);
        Assert.Equal(SpatialSolveStatus.Converged, result.SpatialSolve.Status);
        Assert.True(result.SpatialSolve.HasUsableState);
        Assert.All(
            result.Group1Flux,
            flux => Assert.True(double.IsFinite(flux) && flux >= 0.0));
        Assert.All(
            result.Group2Flux,
            flux => Assert.True(double.IsFinite(flux) && flux >= 0.0));
        Assert.All(
            result.NodePowerWatts,
            power => Assert.True(double.IsFinite(power) && power >= 0.0));

        double channelTotal = 0.0;
        for (int channelIndex = 0;
             channelIndex < (int)Candu6CoreTopologyFactoryV1.ChannelCount;
             channelIndex++)
        {
            double bundleTotal = result.NodePowerWatts
                .Skip(channelIndex * (int)Candu6CoreTopologyFactoryV1.BundlePositionCount)
                .Take((int)Candu6CoreTopologyFactoryV1.BundlePositionCount)
                .Sum();
            Assert.True(double.IsFinite(bundleTotal) && bundleTotal >= 0.0);
            channelTotal += bundleTotal;
        }

        Assert.Equal(result.TotalPowerWatts, channelTotal, 3);
    }

    private static ManufacturedSolveFixture CreateFixture(
        int positionCount,
        bool reverseRecords = false,
        int maximumIterations = 64)
    {
        CoreTopology topology = CreateTopology(positionCount);
        SpatialStencil stencil = Require(SpatialStencil.TryCreate(topology));
        SpatialNodeCoefficients[] nodeCoefficients = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                volumeM3: 1.0,
                absorptionGroup1PerM: 0.3,
                absorptionGroup2PerM: 0.2,
                downscatterGroup1To2PerM: 0.1,
                fissionGroup1PerM: 0.1,
                fissionGroup2PerM: 0.1,
                nuFissionGroup1PerM: 0.15,
                nuFissionGroup2PerM: 0.25,
                chiGroup1: 1.0,
                chiGroup2: 0.0,
                energyPerFissionJ: 1.0))
            .ToArray();
        SpatialEdgeConductance[] edgeConductances = Enumerable.Range(0, positionCount - 1)
            .Select(index => new SpatialEdgeConductance(
                new NodeKey(new ChannelId(0), new BundlePosition((uint)index)),
                new NodeKey(new ChannelId(0), new BundlePosition((uint)(index + 1))),
                group1M2: 0.6,
                group2M2: 0.6))
            .ToArray();
        SpatialBoundaryConductance[] boundaryConductances = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                term.Classification == BoundaryClassification.Vacuum ? 0.4 : 0.0,
                term.Classification == BoundaryClassification.Vacuum ? 0.4 : 0.0)))
            .ToArray();

        if (reverseRecords)
        {
            nodeCoefficients = nodeCoefficients.Reverse().ToArray();
            edgeConductances = edgeConductances.Reverse().ToArray();
            boundaryConductances = boundaryConductances.Reverse().ToArray();
        }

        SpatialCoefficientSet coefficients = Require(
            SpatialCoefficientSet.TryCreate(
                stencil,
                nodeCoefficients,
                edgeConductances,
                boundaryConductances));
        SpatialOperator spatialOperator = Require(
            SpatialOperator.TryCreate(stencil, coefficients));
        SpatialLinearSolvePolicy linearPolicy = Require(
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                absoluteResidualTolerance: 1e-13,
                relativeResidualTolerance: 1e-13,
                maximumInnerIterations: 2048));
        double targetPowerWatts = positionCount * 0.6;
        SpatialEigenIteration iteration = Require(
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficients,
                linearPolicy,
                targetPowerWatts,
                initialEigenvalue: 1.0,
                initialGroup1Flux: Enumerable.Repeat(1.0, positionCount).ToArray(),
                initialGroup2Flux: Enumerable.Repeat(1.0, positionCount).ToArray()));
        SpatialConvergencePolicy convergencePolicy = Require(
            SpatialConvergencePolicy.TryCreate(
                kAbsoluteTolerance: 1e-10,
                kRelativeTolerance: 1e-10,
                residualTolerance: 1e-10,
                sourceShapeTolerance: 1e-10,
                powerBalanceTolerance: 1e-10,
                maximumIterations));
        SpatialEigenSolve solve = Require(
            SpatialEigenSolve.TryCreate(iteration, convergencePolicy));

        return new ManufacturedSolveFixture(
            stencil,
            coefficients,
            spatialOperator,
            solve,
            linearPolicy,
            targetPowerWatts,
            nodeCoefficients,
            edgeConductances,
            boundaryConductances);
    }

    private static CoreTopology CreateTopology(int positionCount)
    {
        ChannelId channelId = new ChannelId(0);
        var neighbors = new List<NeighborRecord>();
        for (uint position = 0; position + 1 < (uint)positionCount; position++)
        {
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition(position),
                channelId,
                new BundlePosition(position + 1),
                NeighborDirection.TowardEndB));
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition(position + 1),
                channelId,
                new BundlePosition(position),
                NeighborDirection.TowardEndA));
        }

        var boundaries = new List<BoundaryFaceRecord>();
        TopologyFace[] cardinalFaces =
        {
            TopologyFace.North,
            TopologyFace.East,
            TopologyFace.South,
            TopologyFace.West
        };
        for (uint position = 0; position < (uint)positionCount; position++)
        {
            foreach (TopologyFace face in cardinalFaces)
            {
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    new BundlePosition(position),
                    face,
                    BoundaryClassification.Reflective));
            }
        }

        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(0),
            TopologyFace.EndA,
            BoundaryClassification.Vacuum));
        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition((uint)positionCount - 1),
            TopologyFace.EndB,
            BoundaryClassification.Vacuum));

        ChannelTopology channel = new ChannelTopology(
            channelId,
            coordinateX: 0,
            coordinateY: 0,
            FlowDirection.EndAtoEndB,
            new BundlePosition(0),
            new BundlePosition((uint)positionCount - 1),
            neighbors,
            boundaries);
        return Require(CoreTopology.TryCreate(1, (uint)positionCount, new[] { channel }));
    }

    private static double[] NodePowers(
        ManufacturedSolveFixture fixture,
        SpatialEigenIterationState state)
    {
        return fixture.Coefficients.Nodes
            .Select((coefficients, index) => coefficients.VolumeM3 *
                coefficients.EnergyPerFissionJ *
                (coefficients.FissionGroup1PerM * state.Group1Flux[index] +
                 coefficients.FissionGroup2PerM * state.Group2Flux[index]))
            .ToArray();
    }

    private static FullCoreDiffusionModelV1 CreateFullCoreModel()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        return Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, result.FirstDiagnostic.Code);
    }

    private sealed class ManufacturedSolveFixture
    {
        public ManufacturedSolveFixture(
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialOperator spatialOperator,
            SpatialEigenSolve solve,
            SpatialLinearSolvePolicy linearSolvePolicy,
            double targetPowerWatts,
            IReadOnlyList<SpatialNodeCoefficients> nodeCoefficients,
            IReadOnlyList<SpatialEdgeConductance> edgeConductances,
            IReadOnlyList<SpatialBoundaryConductance> boundaryConductances)
        {
            Stencil = stencil;
            Coefficients = coefficients;
            Operator = spatialOperator;
            Solve = solve;
            LinearSolvePolicy = linearSolvePolicy;
            TargetPowerWatts = targetPowerWatts;
            NodeCoefficients = nodeCoefficients;
            EdgeConductances = edgeConductances;
            BoundaryConductances = boundaryConductances;
        }

        public SpatialStencil Stencil { get; }

        public SpatialCoefficientSet Coefficients { get; }

        public SpatialOperator Operator { get; }

        public SpatialEigenSolve Solve { get; }

        public SpatialLinearSolvePolicy LinearSolvePolicy { get; }

        public double TargetPowerWatts { get; }

        public IReadOnlyList<SpatialNodeCoefficients> NodeCoefficients { get; }

        public IReadOnlyList<SpatialEdgeConductance> EdgeConductances { get; }

        public IReadOnlyList<SpatialBoundaryConductance> BoundaryConductances { get; }
    }
}
