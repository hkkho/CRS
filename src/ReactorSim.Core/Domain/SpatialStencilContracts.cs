using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit topology neighbor in the canonical static-solve order.
    /// Coefficients are deliberately not part of this topology-only contract.
    /// </summary>
    public sealed class SpatialNeighborTerm
    {
        internal SpatialNeighborTerm(
            NeighborDirection direction,
            NodeKey targetNode,
            int targetFlatIndex)
        {
            Direction = direction;
            TargetNode = targetNode;
            TargetFlatIndex = targetFlatIndex;
        }

        public NeighborDirection Direction { get; }

        public NodeKey TargetNode { get; }

        public int TargetFlatIndex { get; }
    }

    /// <summary>
    /// One explicit boundary face in the canonical static-solve order.
    /// Numerical conductances are supplied by the data-pack composition layer.
    /// </summary>
    public sealed class SpatialBoundaryTerm
    {
        internal SpatialBoundaryTerm(
            TopologyFace face,
            BoundaryClassification classification)
        {
            Face = face;
            Classification = classification;
        }

        public TopologyFace Face { get; }

        public BoundaryClassification Classification { get; }
    }

    /// <summary>
    /// Immutable stencil terms for one explicit spatial node.
    /// </summary>
    public sealed class SpatialNodeStencil
    {
        internal SpatialNodeStencil(
            NodeKey node,
            int flatIndex,
            IEnumerable<SpatialNeighborTerm> neighborTerms,
            IEnumerable<SpatialBoundaryTerm> boundaryTerms)
        {
            Node = node;
            FlatIndex = flatIndex;
            NeighborTerms = new ReadOnlyCollection<SpatialNeighborTerm>(neighborTerms.ToArray());
            BoundaryTerms = new ReadOnlyCollection<SpatialBoundaryTerm>(boundaryTerms.ToArray());
        }

        public NodeKey Node { get; }

        public int FlatIndex { get; }

        public IReadOnlyList<SpatialNeighborTerm> NeighborTerms { get; }

        public IReadOnlyList<SpatialBoundaryTerm> BoundaryTerms { get; }
    }

    /// <summary>
    /// Deterministic topology-driven stencil assembly for the P2-T02 spatial
    /// operator. This task does not apply coefficients or perform iteration.
    /// </summary>
    public sealed class SpatialStencil
    {
        private readonly ReadOnlyCollection<SpatialNodeStencil> _nodes;
        private readonly CoreTopology _topology;

        private SpatialStencil(
            CoreTopology topology,
            uint channelCount,
            uint bundlePositionCount,
            IReadOnlyList<SpatialNodeStencil> nodes)
        {
            _topology = topology;
            ChannelCount = channelCount;
            BundlePositionCount = bundlePositionCount;
            _nodes = new ReadOnlyCollection<SpatialNodeStencil>(nodes.ToArray());
        }

        public uint ChannelCount { get; }

        public uint BundlePositionCount { get; }

        internal CoreTopology Topology
        {
            get { return _topology; }
        }

        public int NodeCount
        {
            get { return _nodes.Count; }
        }

        public IReadOnlyList<SpatialNodeStencil> Nodes
        {
            get { return _nodes; }
        }

        public static ContractValidationResult<SpatialStencil> TryCreate(CoreTopology topology)
        {
            if (topology == null)
            {
                return ContractValidationResult<SpatialStencil>.Invalid(
                    "SpatialStencil.Topology.Missing",
                    "topology",
                    "Stencil assembly requires a validated topology.");
            }

            var nodes = new List<SpatialNodeStencil>(topology.SlotCount);
            foreach (NodeKey node in topology.EnumerateNodes())
            {
                ChannelTopology channel = topology.GetChannel(node.ChannelId);
                var neighborTerms = channel.Neighbors
                    .Where(record => record.SourcePosition == node.Position)
                    .OrderBy(record => DirectionRank(record.Direction))
                    .ThenBy(record => record.TargetChannelId.Value)
                    .ThenBy(record => record.TargetPosition.Value)
                    .Select(record =>
                    {
                        NodeKey target = new NodeKey(
                            record.TargetChannelId,
                            record.TargetPosition);
                        return new SpatialNeighborTerm(
                            record.Direction,
                            target,
                            topology.GetFlatIndex(target));
                    })
                    .ToArray();

                var boundaryTerms = channel.BoundaryFaces
                    .Where(record => record.Position == node.Position)
                    .OrderBy(record => FaceRank(record.Face))
                    .Select(record => new SpatialBoundaryTerm(
                        record.Face,
                        record.Classification))
                    .ToArray();

                nodes.Add(new SpatialNodeStencil(
                    node,
                    topology.GetFlatIndex(node),
                    neighborTerms,
                    boundaryTerms));
            }

            return ContractValidationResult<SpatialStencil>.Valid(
                new SpatialStencil(
                    topology,
                    topology.ChannelCount,
                    topology.BundlePositionCount,
                    nodes));
        }

        private static byte DirectionRank(NeighborDirection direction)
        {
            switch (direction)
            {
                case NeighborDirection.North:
                    return 0;
                case NeighborDirection.East:
                    return 1;
                case NeighborDirection.South:
                    return 2;
                case NeighborDirection.West:
                    return 3;
                case NeighborDirection.TowardEndA:
                    return 4;
                case NeighborDirection.TowardEndB:
                    return 5;
                default:
                    throw new InvalidOperationException("The topology contains an unknown neighbor direction.");
            }
        }

        private static byte FaceRank(TopologyFace face)
        {
            switch (face)
            {
                case TopologyFace.North:
                    return 0;
                case TopologyFace.East:
                    return 1;
                case TopologyFace.South:
                    return 2;
                case TopologyFace.West:
                    return 3;
                case TopologyFace.EndA:
                    return 4;
                case TopologyFace.EndB:
                    return 5;
                default:
                    throw new InvalidOperationException("The topology contains an unknown boundary face.");
            }
        }
    }
}
