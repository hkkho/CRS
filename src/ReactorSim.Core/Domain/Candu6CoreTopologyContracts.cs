using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Requests a reflective boundary on one explicit spatial face.
    ///
    /// For an interior face, the CANDU-6 topology factory applies the same
    /// reflection to both orientations of the shared face.  Callers can
    /// therefore store either orientation for a selected cell and receive a
    /// reciprocal topology that remains valid for stencil assembly.
    /// </summary>
    public sealed class ReflectiveFaceOverrideV1
    {
        public ReflectiveFaceOverrideV1(NodeKey node, TopologyFace face)
        {
            Node = node;
            Face = face;
        }

        public NodeKey Node { get; }

        public TopologyFace Face { get; }
    }

    /// <summary>
    /// The fixed face geometry used by the first full-core CANDU-6 solve.
    /// Coordinates are a 22 by 22 cartesian lattice with the top display row
    /// mapped to the largest Y coordinate. Channel IDs are row-major over the
    /// stepped 380-channel outline, and bundle positions are channel-major.
    /// </summary>
    public static class Candu6CoreTopologyFactoryV1
    {
        public const uint ChannelCount = 380;
        public const uint BundlePositionCount = 12;
        public const int GridWidth = 22;
        public const int GridHeight = 22;
        public const string TopologySchemaId = "candu6-380x12-grid-v1";

        private static readonly int[] RowLengths =
        {
            6, 12, 14, 16, 18, 18, 20, 20,
            22, 22, 22, 22, 22, 22,
            20, 20, 18, 18, 16, 14, 12, 6
        };

        private static readonly Candu6GridPositionV1[] Positions = CreatePositions();
        private static readonly Dictionary<int, uint> ChannelIndices = CreateChannelIndices();

        public static CoreTopology Create()
        {
            ContractValidationResult<CoreTopology> result = TryCreate();
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            }

            return result.Value;
        }

        public static ContractValidationResult<CoreTopology> TryCreate()
        {
            return TryCreate(Array.Empty<ReflectiveFaceOverrideV1>());
        }

        /// <summary>
        /// Creates the canonical 380 by 12 topology with an optional set of
        /// reflective face overrides.  The input order is not observable in
        /// the returned topology: overrides are validated and applied by
        /// NodeKey/face order.
        /// </summary>
        public static ContractValidationResult<CoreTopology> TryCreate(
            IEnumerable<ReflectiveFaceOverrideV1> reflectiveFaceOverrides)
        {
            if (reflectiveFaceOverrides == null)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ReflectiveOverride.Missing",
                    "reflective_face_overrides",
                    "The reflective face override collection may not be null.");
            }

            ReflectiveFaceOverrideV1[] overrides = reflectiveFaceOverrides.ToArray();
            var facesByNode = new Dictionary<NodeKey, HashSet<TopologyFace>>();
            ReflectiveFaceOverrideV1[] orderedOverrides = overrides
                .Where(overrideRecord => overrideRecord != null)
                .OrderBy(overrideRecord => overrideRecord.Node)
                .ThenBy(overrideRecord => (byte)overrideRecord.Face)
                .ToArray();

            if (orderedOverrides.Length != overrides.Length)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ReflectiveOverride.Null",
                    "reflective_face_overrides",
                    "A reflective face override may not be null.");
            }

            for (int index = 0; index < orderedOverrides.Length; index++)
            {
                ReflectiveFaceOverrideV1 overrideRecord = orderedOverrides[index];
                if (overrideRecord.Node.ChannelId.Value >= ChannelCount ||
                    overrideRecord.Node.Position.Value >= BundlePositionCount)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.ReflectiveOverride.Node.OutOfRange",
                        "reflective_face_overrides[" + index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "A reflective face override must name a canonical channel and bundle position.");
                }

                if (!Enum.IsDefined(typeof(TopologyFace), overrideRecord.Face))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.ReflectiveOverride.Face.Invalid",
                        "reflective_face_overrides[" + index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "A reflective face override must use a known topology face.");
                }

                if (!facesByNode.TryGetValue(overrideRecord.Node, out HashSet<TopologyFace>? faces))
                {
                    faces = new HashSet<TopologyFace>();
                    facesByNode.Add(overrideRecord.Node, faces);
                }

                if (!faces.Add(overrideRecord.Face))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.ReflectiveOverride.Duplicate",
                        "reflective_face_overrides[" + index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "A reflective face override may be declared only once for a node and face.");
                }
            }

            return TryCreateCore(facesByNode);
        }

        public static CoreTopology Create(
            IEnumerable<ReflectiveFaceOverrideV1> reflectiveFaceOverrides)
        {
            ContractValidationResult<CoreTopology> result = TryCreate(reflectiveFaceOverrides);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            }

            return result.Value;
        }

        private static ContractValidationResult<CoreTopology> TryCreateCore(
            IReadOnlyDictionary<NodeKey, HashSet<TopologyFace>> reflectiveFaces)
        {
            var channels = new List<ChannelTopology>((int)ChannelCount);
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                Candu6GridPositionV1 position = GetPosition(channelIndex);
                FlowDirection flowDirection = GetFlowDirection(position);
                var neighbors = new List<NeighborRecord>();
                var boundaries = new List<BoundaryFaceRecord>();

                for (uint bundlePosition = 0;
                     bundlePosition + 1 < BundlePositionCount;
                     bundlePosition++)
                {
                    NodeKey source = new NodeKey(
                        new ChannelId(channelIndex),
                        new BundlePosition(bundlePosition));
                    NodeKey target = new NodeKey(
                        new ChannelId(channelIndex),
                        new BundlePosition(bundlePosition + 1));
                    if (IsReflective(reflectiveFaces, source, TopologyFace.EndB) ||
                        IsReflective(reflectiveFaces, target, TopologyFace.EndA))
                    {
                        boundaries.Add(new BoundaryFaceRecord(
                            source.ChannelId,
                            source.Position,
                            TopologyFace.EndB,
                            BoundaryClassification.Reflective));
                        boundaries.Add(new BoundaryFaceRecord(
                            target.ChannelId,
                            target.Position,
                            TopologyFace.EndA,
                            BoundaryClassification.Reflective));
                    }
                    else
                    {
                        neighbors.Add(new NeighborRecord(
                            source.ChannelId,
                            source.Position,
                            target.ChannelId,
                            target.Position,
                            NeighborDirection.TowardEndB));
                        neighbors.Add(new NeighborRecord(
                            target.ChannelId,
                            target.Position,
                            source.ChannelId,
                            source.Position,
                            NeighborDirection.TowardEndA));
                    }

                }

                for (uint bundlePosition = 0;
                     bundlePosition < BundlePositionCount;
                     bundlePosition++)
                {
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.North,
                        boundaries,
                        neighbors,
                        reflectiveFaces);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.East,
                        boundaries,
                        neighbors,
                        reflectiveFaces);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.South,
                        boundaries,
                        neighbors,
                        reflectiveFaces);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.West,
                        boundaries,
                        neighbors,
                        reflectiveFaces);
                }

                boundaries.Add(new BoundaryFaceRecord(
                    new ChannelId(channelIndex),
                    new BundlePosition(0),
                    TopologyFace.EndA,
                    IsReflective(
                        reflectiveFaces,
                        new NodeKey(new ChannelId(channelIndex), new BundlePosition(0)),
                        TopologyFace.EndA)
                        ? BoundaryClassification.Reflective
                        : BoundaryClassification.Vacuum));
                boundaries.Add(new BoundaryFaceRecord(
                    new ChannelId(channelIndex),
                    new BundlePosition(BundlePositionCount - 1),
                    TopologyFace.EndB,
                    IsReflective(
                        reflectiveFaces,
                        new NodeKey(
                            new ChannelId(channelIndex),
                            new BundlePosition(BundlePositionCount - 1)),
                        TopologyFace.EndB)
                        ? BoundaryClassification.Reflective
                        : BoundaryClassification.Vacuum));

                BundlePosition inlet = flowDirection == FlowDirection.EndAtoEndB
                    ? new BundlePosition(0)
                    : new BundlePosition(BundlePositionCount - 1);
                BundlePosition outlet = flowDirection == FlowDirection.EndAtoEndB
                    ? new BundlePosition(BundlePositionCount - 1)
                    : new BundlePosition(0);
                channels.Add(new ChannelTopology(
                    new ChannelId(channelIndex),
                    position.Column,
                    position.CartesianY,
                    flowDirection,
                    inlet,
                    outlet,
                    neighbors,
                    boundaries));
            }

            return CoreTopology.TryCreate(ChannelCount, BundlePositionCount, channels);
        }

        public static Candu6GridPositionV1 GetPosition(uint channelIndex)
        {
            if (channelIndex >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Positions[(int)channelIndex];
        }

        public static int GetRowLength(int displayRow)
        {
            if (displayRow < 0 || displayRow >= RowLengths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(displayRow));
            }

            return RowLengths[displayRow];
        }

        public static bool TryGetChannelIndex(
            int column,
            int displayRow,
            out uint channelIndex)
        {
            if (column < 0 || column >= GridWidth ||
                displayRow < 0 || displayRow >= GridHeight)
            {
                channelIndex = 0;
                return false;
            }

            return ChannelIndices.TryGetValue(
                checked(displayRow * GridWidth + column),
                out channelIndex);
        }

        public static FlowDirection GetFlowDirection(Candu6GridPositionV1 position)
        {
            return (position.Column + position.DisplayRow) % 2 == 0
                ? FlowDirection.EndAtoEndB
                : FlowDirection.EndBtoEndA;
        }

        public static FlowDirection GetFlowDirection(int column, int displayRow)
        {
            if (column < 0 || column >= GridWidth ||
                displayRow < 0 || displayRow >= GridHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(displayRow));
            }

            return (column + displayRow) % 2 == 0
                ? FlowDirection.EndAtoEndB
                : FlowDirection.EndBtoEndA;
        }

        public static string GetTopologyIdentity()
        {
            return TopologySchemaId + ":22x22:380:12:"
                   + "6,12,14,16,18,18,20,20,22,22,22,22,22,22,20,20,18,18,16,14,12,6";
        }

        private static void AddCardinalRelationOrBoundary(
            uint channelIndex,
            uint bundlePosition,
            Candu6GridPositionV1 position,
            NeighborDirection direction,
            List<BoundaryFaceRecord> boundaries,
            List<NeighborRecord> neighbors,
            IReadOnlyDictionary<NodeKey, HashSet<TopologyFace>> reflectiveFaces)
        {
            int column = position.Column;
            int displayRow = position.DisplayRow;
            switch (direction)
            {
                case NeighborDirection.North:
                    displayRow--;
                    break;
                case NeighborDirection.East:
                    column++;
                    break;
                case NeighborDirection.South:
                    displayRow++;
                    break;
                case NeighborDirection.West:
                    column--;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }

            NodeKey source = new NodeKey(
                new ChannelId(channelIndex),
                new BundlePosition(bundlePosition));
            TopologyFace face = (TopologyFace)(byte)direction;
            if (TryGetChannelIndex(column, displayRow, out uint targetChannel))
            {
                NodeKey target = new NodeKey(
                    new ChannelId(targetChannel),
                    new BundlePosition(bundlePosition));
                TopologyFace inverseFace = Inverse(face);
                if (IsReflective(reflectiveFaces, source, face) ||
                    IsReflective(reflectiveFaces, target, inverseFace))
                {
                    boundaries.Add(new BoundaryFaceRecord(
                        source.ChannelId,
                        source.Position,
                        face,
                        BoundaryClassification.Reflective));
                }
                else
                {
                    neighbors.Add(new NeighborRecord(
                        source.ChannelId,
                        source.Position,
                        target.ChannelId,
                        target.Position,
                        direction));
                }
                return;
            }

            boundaries.Add(new BoundaryFaceRecord(
                source.ChannelId,
                source.Position,
                face,
                IsReflective(reflectiveFaces, source, face)
                    ? BoundaryClassification.Reflective
                    : BoundaryClassification.Vacuum));
        }

        private static bool IsReflective(
            IReadOnlyDictionary<NodeKey, HashSet<TopologyFace>> reflectiveFaces,
            NodeKey node,
            TopologyFace face)
        {
            return reflectiveFaces.TryGetValue(node, out HashSet<TopologyFace>? faces) &&
                   faces.Contains(face);
        }

        private static TopologyFace Inverse(TopologyFace face)
        {
            switch (face)
            {
                case TopologyFace.North:
                    return TopologyFace.South;
                case TopologyFace.East:
                    return TopologyFace.West;
                case TopologyFace.South:
                    return TopologyFace.North;
                case TopologyFace.West:
                    return TopologyFace.East;
                case TopologyFace.EndA:
                    return TopologyFace.EndB;
                case TopologyFace.EndB:
                    return TopologyFace.EndA;
                default:
                    throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        private static Candu6GridPositionV1[] CreatePositions()
        {
            var positions = new List<Candu6GridPositionV1>((int)ChannelCount);
            for (int displayRow = 0; displayRow < RowLengths.Length; displayRow++)
            {
                int rowLength = RowLengths[displayRow];
                int firstColumn = (GridWidth - rowLength) / 2;
                for (int offset = 0; offset < rowLength; offset++)
                {
                    positions.Add(new Candu6GridPositionV1(
                        firstColumn + offset,
                        displayRow,
                        GridHeight - 1 - displayRow));
                }
            }

            if (positions.Count != ChannelCount)
            {
                throw new InvalidOperationException(
                    "The CANDU-6 topology must contain exactly 380 channels.");
            }

            return positions.ToArray();
        }

        private static Dictionary<int, uint> CreateChannelIndices()
        {
            var result = new Dictionary<int, uint>();
            for (uint channelIndex = 0; channelIndex < Positions.Length; channelIndex++)
            {
                Candu6GridPositionV1 position = Positions[(int)channelIndex];
                result.Add(
                    checked(position.DisplayRow * GridWidth + position.Column),
                    channelIndex);
            }

            return result;
        }
    }

    public readonly struct Candu6GridPositionV1
    {
        internal Candu6GridPositionV1(int column, int displayRow, int cartesianY)
        {
            Column = column;
            DisplayRow = displayRow;
            CartesianY = cartesianY;
        }

        public int Column { get; }

        public int DisplayRow { get; }

        public int CartesianY { get; }
    }
}
