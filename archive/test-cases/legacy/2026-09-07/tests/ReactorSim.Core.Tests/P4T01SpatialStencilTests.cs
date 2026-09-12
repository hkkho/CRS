using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P4T01SpatialStencilTests
{
    [Fact]
    public void AssemblesCanonicalNodesAndLocalTermOrder()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();

        ContractValidationResult<SpatialStencil> result = SpatialStencil.TryCreate(fixture.Topology);

        Assert.True(result.IsValid);
        SpatialStencil stencil = result.Value;
        Assert.Equal(2u, stencil.ChannelCount);
        Assert.Equal(3u, stencil.BundlePositionCount);
        Assert.Equal(6, stencil.NodeCount);
        Assert.Equal(new NodeKey(new ChannelId(0), new BundlePosition(0)), stencil.Nodes[0].Node);
        Assert.Equal(0, stencil.Nodes[0].FlatIndex);
        Assert.Equal(2, stencil.Nodes[0].NeighborTerms.Count);
        Assert.Equal(NeighborDirection.East, stencil.Nodes[0].NeighborTerms[0].Direction);
        Assert.Equal(new NodeKey(new ChannelId(1), new BundlePosition(0)), stencil.Nodes[0].NeighborTerms[0].TargetNode);
        Assert.Equal(3, stencil.Nodes[0].NeighborTerms[0].TargetFlatIndex);
        Assert.Equal(NeighborDirection.TowardEndB, stencil.Nodes[0].NeighborTerms[1].Direction);
        Assert.Equal(new NodeKey(new ChannelId(0), new BundlePosition(1)), stencil.Nodes[0].NeighborTerms[1].TargetNode);

        Assert.Equal(
            new[] { TopologyFace.North, TopologyFace.South, TopologyFace.West, TopologyFace.EndA },
            stencil.Nodes[0].BoundaryTerms.Select(term => term.Face).ToArray());
        Assert.All(
            stencil.Nodes.SelectMany(node => node.BoundaryTerms),
            term => Assert.Equal(BoundaryClassification.Reflective, term.Classification));
    }

    [Fact]
    public void InputRecordOrderDoesNotChangeTheAssembledStencil()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        CoreTopology reorderedTopology = RecreateReorderedTopology(fixture.Topology);

        SpatialStencil first = SpatialStencil.TryCreate(fixture.Topology).Value;
        SpatialStencil second = SpatialStencil.TryCreate(reorderedTopology).Value;

        Assert.Equal(first.NodeCount, second.NodeCount);
        for (int nodeIndex = 0; nodeIndex < first.NodeCount; nodeIndex++)
        {
            SpatialNodeStencil left = first.Nodes[nodeIndex];
            SpatialNodeStencil right = second.Nodes[nodeIndex];
            Assert.Equal(left.Node, right.Node);
            Assert.Equal(left.FlatIndex, right.FlatIndex);
            Assert.Equal(left.NeighborTerms.Count, right.NeighborTerms.Count);
            for (int termIndex = 0; termIndex < left.NeighborTerms.Count; termIndex++)
            {
                Assert.Equal(left.NeighborTerms[termIndex].Direction, right.NeighborTerms[termIndex].Direction);
                Assert.Equal(left.NeighborTerms[termIndex].TargetNode, right.NeighborTerms[termIndex].TargetNode);
                Assert.Equal(left.NeighborTerms[termIndex].TargetFlatIndex, right.NeighborTerms[termIndex].TargetFlatIndex);
            }

            Assert.Equal(
                left.BoundaryTerms.Select(term => term.Face),
                right.BoundaryTerms.Select(term => term.Face));
        }
    }

    [Fact]
    public void AssembledCollectionsAreImmutable()
    {
        SpatialStencil stencil = SpatialStencil.TryCreate(
            SyntheticFixtures.CreateTwoChannelThreePosition().Topology).Value;

        var nodes = Assert.IsAssignableFrom<IList<SpatialNodeStencil>>(stencil.Nodes);
        var neighbors = Assert.IsAssignableFrom<IList<SpatialNeighborTerm>>(stencil.Nodes[0].NeighborTerms);
        var boundaries = Assert.IsAssignableFrom<IList<SpatialBoundaryTerm>>(stencil.Nodes[0].BoundaryTerms);

        Assert.Throws<NotSupportedException>(() => nodes.Add(stencil.Nodes[0]));
        Assert.Throws<NotSupportedException>(() => neighbors.Clear());
        Assert.Throws<NotSupportedException>(() => boundaries.RemoveAt(0));
    }

    [Fact]
    public void MissingTopologyFailsClosed()
    {
        ContractValidationResult<SpatialStencil> result = SpatialStencil.TryCreate(null!);

        Assert.False(result.IsValid);
        Assert.Equal("SpatialStencil.Topology.Missing", result.FirstDiagnostic.Code);
    }

    private static CoreTopology RecreateReorderedTopology(CoreTopology topology)
    {
        var channels = topology.Channels
            .Reverse()
            .Select(channel => new ChannelTopology(
                channel.ChannelId,
                channel.CoordinateX,
                channel.CoordinateY,
                channel.FlowDirection,
                channel.InletPosition,
                channel.OutletPosition,
                channel.Neighbors.Reverse(),
                channel.BoundaryFaces.Reverse()))
            .ToArray();

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            topology.ChannelCount,
            topology.BundlePositionCount,
            channels);
        Assert.True(result.IsValid);
        return result.Value;
    }
}
