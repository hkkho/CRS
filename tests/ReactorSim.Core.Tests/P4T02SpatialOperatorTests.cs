using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P4T02SpatialOperatorTests
{
    [Fact]
    public void BindsUnorderedCoefficientsAndAppliesBothGroupOperators()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = SpatialStencil.TryCreate(fixture.Topology).Value;
        SpatialCoefficientSet coefficients = CreateCoefficients(stencil);
        SpatialOperator spatialOperator = SpatialOperator.TryCreate(stencil, coefficients).Value;
        double[] flux = { 1, 2, 3, 4, 5, 6 };
        double[] group1 = new double[stencil.NodeCount];
        double[] group2 = new double[stencil.NodeCount];

        Assert.True(spatialOperator.TryApply(SpatialEnergyGroup.Group1, flux, group1, out ContractDiagnostic group1Diagnostic));
        Assert.Null(group1Diagnostic);
        Assert.True(spatialOperator.TryApply(SpatialEnergyGroup.Group2, flux, group2, out ContractDiagnostic group2Diagnostic));
        Assert.Null(group2Diagnostic);

        Assert.Equal(-2.75, group1[0], 12);
        Assert.Equal(-3.5, group2[0], 12);
        Assert.Equal(-0.5, group1[1], 12);
        Assert.Equal(-2.0, group2[1], 12);
    }

    [Fact]
    public void ReflectiveAndVacuumBoundaryBindingsUseTheirExplicitRules()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = SpatialStencil.TryCreate(fixture.Topology).Value;
        SpatialStencil vacuumStencil = ReclassifyBoundary(
            fixture.Topology,
            new ChannelId(0),
            new BundlePosition(0),
            TopologyFace.North,
            BoundaryClassification.Vacuum);
        SpatialCoefficientSet coefficients = CreateCoefficients(vacuumStencil, new SpatialBoundaryConductance(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            TopologyFace.North,
            1.5,
            2.0));
        SpatialOperator spatialOperator = SpatialOperator.TryCreate(vacuumStencil, coefficients).Value;
        double[] output = new double[vacuumStencil.NodeCount];

        Assert.True(spatialOperator.TryApply(
            SpatialEnergyGroup.Group1,
            Enumerable.Repeat(1.0, vacuumStencil.NodeCount).ToArray(),
            output,
            out ContractDiagnostic diagnostic));
        Assert.Null(diagnostic);
        Assert.Equal(2.0, output[0], 12);
        Assert.Equal(1.25, output[1], 12);
        Assert.Equal(1.25, output[2], 12);
        Assert.Equal(1.25, output[3], 12);
        Assert.Equal(1.25, output[4], 12);
        Assert.Equal(1.25, output[5], 12);
        _ = stencil;
    }

    [Fact]
    public void CoefficientBindingRejectsMissingDuplicateAndInvalidRecords()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = SpatialStencil.TryCreate(fixture.Topology).Value;
        SpatialCoefficientSet valid = CreateCoefficients(stencil);

        var missingEdge = validEdgeRecords(stencil).Skip(1).ToArray();
        ContractValidationResult<SpatialCoefficientSet> missing = SpatialCoefficientSet.TryCreate(
            stencil,
            valid.Nodes,
            missingEdge,
            validBoundaryRecords(stencil));
        Assert.False(missing.IsValid);
        Assert.Equal("SpatialCoefficients.Edge.Missing", missing.FirstDiagnostic.Code);

        var duplicateEdge = validEdgeRecords(stencil).Concat(validEdgeRecords(stencil).Take(1)).ToArray();
        ContractValidationResult<SpatialCoefficientSet> duplicate = SpatialCoefficientSet.TryCreate(
            stencil,
            valid.Nodes,
            duplicateEdge,
            validBoundaryRecords(stencil));
        Assert.False(duplicate.IsValid);
        Assert.Equal("SpatialCoefficients.Edge.Duplicate", duplicate.FirstDiagnostic.Code);

        var invalidNodes = valid.Nodes
            .Select(node => node.Node == new NodeKey(new ChannelId(0), new BundlePosition(0))
                ? new SpatialNodeCoefficients(
                    node.Node,
                    node.VolumeM3,
                    0.1,
                    node.AbsorptionGroup2PerM,
                    node.DownscatterGroup1To2PerM,
                    0.2,
                    node.FissionGroup2PerM,
                    node.NuFissionGroup1PerM,
                    node.NuFissionGroup2PerM,
                    node.ChiGroup1,
                    node.ChiGroup2,
                    node.EnergyPerFissionJ)
                : node)
            .ToArray();
        ContractValidationResult<SpatialCoefficientSet> invalid = SpatialCoefficientSet.TryCreate(
            stencil,
            invalidNodes,
            validEdgeRecords(stencil),
            validBoundaryRecords(stencil));
        Assert.False(invalid.IsValid);
        Assert.Equal("SpatialCoefficients.AbsorptionBelowFission", invalid.FirstDiagnostic.Code);
    }

    [Fact]
    public void OperatorRejectsInvalidFluxAndClearsDestination()
    {
        SpatialStencil stencil = SpatialStencil.TryCreate(
            SyntheticFixtures.CreateTwoChannelThreePosition().Topology).Value;
        SpatialCoefficientSet coefficients = CreateCoefficients(stencil);
        SpatialOperator spatialOperator = SpatialOperator.TryCreate(stencil, coefficients).Value;
        double[] invalidFlux = { 1, 2, double.NaN, 4, 5, 6 };
        double[] destination = Enumerable.Repeat(9.0, stencil.NodeCount).ToArray();

        Assert.False(spatialOperator.TryApply(
            SpatialEnergyGroup.Group1,
            invalidFlux,
            destination,
            out ContractDiagnostic diagnostic));
        Assert.Equal("SpatialOperator.Flux.Invalid", diagnostic.Code);
        Assert.All(destination, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void OperatorClearsDestinationForEveryRejectedShapeCall()
    {
        SpatialStencil stencil = SpatialStencil.TryCreate(
            SyntheticFixtures.CreateTwoChannelThreePosition().Topology).Value;
        SpatialCoefficientSet coefficients = CreateCoefficients(stencil);
        SpatialOperator spatialOperator = SpatialOperator.TryCreate(stencil, coefficients).Value;
        double[] validFlux = Enumerable.Repeat(1.0, stencil.NodeCount).ToArray();

        double[] invalidGroupDestination = Enumerable.Repeat(9.0, stencil.NodeCount).ToArray();
        Assert.False(spatialOperator.TryApply(
            (SpatialEnergyGroup)99,
            validFlux,
            invalidGroupDestination,
            out ContractDiagnostic invalidGroup));
        Assert.Equal("SpatialOperator.Group.Invalid", invalidGroup.Code);
        Assert.All(invalidGroupDestination, value => Assert.Equal(0.0, value));

        double[] missingFluxDestination = Enumerable.Repeat(9.0, stencil.NodeCount).ToArray();
        Assert.False(spatialOperator.TryApply(
            SpatialEnergyGroup.Group1,
            null!,
            missingFluxDestination,
            out ContractDiagnostic missingFlux));
        Assert.Equal("SpatialOperator.Flux.Missing", missingFlux.Code);
        Assert.All(missingFluxDestination, value => Assert.Equal(0.0, value));

        double[] shortFluxDestination = Enumerable.Repeat(9.0, stencil.NodeCount).ToArray();
        Assert.False(spatialOperator.TryApply(
            SpatialEnergyGroup.Group1,
            new double[stencil.NodeCount - 1],
            shortFluxDestination,
            out ContractDiagnostic shortFlux));
        Assert.Equal("SpatialOperator.Flux.DimensionMismatch", shortFlux.Code);
        Assert.All(shortFluxDestination, value => Assert.Equal(0.0, value));

        double[] shortDestination = Enumerable.Repeat(9.0, stencil.NodeCount - 1).ToArray();
        Assert.False(spatialOperator.TryApply(
            SpatialEnergyGroup.Group1,
            validFlux,
            shortDestination,
            out ContractDiagnostic shortOutput));
        Assert.Equal("SpatialOperator.Destination.DimensionMismatch", shortOutput.Code);
        Assert.All(shortDestination, value => Assert.Equal(0.0, value));
    }

    private static SpatialCoefficientSet CreateCoefficients(
        SpatialStencil stencil,
        SpatialBoundaryConductance? additionalBoundary = null)
    {
        var nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                2.0,
                1.0,
                0.5,
                0.25,
                0.1,
                0.1,
                0.2,
                0.2,
                0.5,
                0.5,
                1.0))
            .Reverse()
            .ToArray();
        var edges = validEdgeRecords(stencil).Reverse().ToArray();
        var boundaries = validBoundaryRecords(stencil).Reverse().ToList();
        if (additionalBoundary != null)
        {
            boundaries = boundaries
                .Where(record => !(record.Node == additionalBoundary.Node && record.Face == additionalBoundary.Face))
                .Concat(new[] { additionalBoundary })
                .ToList();
        }

        ContractValidationResult<SpatialCoefficientSet> result = SpatialCoefficientSet.TryCreate(
            stencil,
            nodes,
            edges,
            boundaries);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialEdgeConductance[] validEdgeRecords(SpatialStencil stencil)
    {
        return stencil.Nodes
            .SelectMany(node => node.NeighborTerms.Select(term => (node.Node, term.TargetNode)))
            .Select(pair => new
            {
                Key = new[] { pair.Node, pair.TargetNode }.OrderBy(node => node).ToArray(),
                pair.Node,
                pair.TargetNode
            })
            .GroupBy(pair => string.Join("/", pair.Key.Select(node => node.ToString())), StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(pair => new SpatialEdgeConductance(pair.Key[0], pair.Key[1], 2.0, 2.0))
            .ToArray();
    }

    private static SpatialBoundaryConductance[] validBoundaryRecords(SpatialStencil stencil)
    {
        return stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .ToArray();
    }

    private static SpatialStencil ReclassifyBoundary(
        CoreTopology topology,
        ChannelId channelId,
        BundlePosition position,
        TopologyFace face,
        BoundaryClassification classification)
    {
        var channels = topology.Channels
            .Select(channel =>
            {
                BoundaryFaceRecord[] boundaries = channel.BoundaryFaces
                    .Select(boundary => boundary.ChannelId == channelId &&
                                        boundary.Position == position &&
                                        boundary.Face == face
                        ? new BoundaryFaceRecord(boundary.ChannelId, boundary.Position, boundary.Face, classification)
                        : boundary)
                    .ToArray();
                return new ChannelTopology(
                    channel.ChannelId,
                    channel.CoordinateX,
                    channel.CoordinateY,
                    channel.FlowDirection,
                    channel.InletPosition,
                    channel.OutletPosition,
                    channel.Neighbors,
                    boundaries);
            })
            .ToArray();
        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            topology.ChannelCount,
            topology.BundlePositionCount,
            channels);
        Assert.True(result.IsValid);
        return SpatialStencil.TryCreate(result.Value).Value;
    }
}
