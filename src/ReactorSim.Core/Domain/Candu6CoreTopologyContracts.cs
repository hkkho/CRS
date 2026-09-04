using System;
using System.Collections.Generic;

namespace ReactorSim.Core
{
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
            var channels = new List<ChannelTopology>((int)ChannelCount);
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                Candu6GridPositionV1 position = GetPosition(channelIndex);
                FlowDirection flowDirection = GetFlowDirection(position);
                var neighbors = new List<NeighborRecord>();
                var boundaries = new List<BoundaryFaceRecord>();

                for (uint bundlePosition = 0;
                     bundlePosition < BundlePositionCount;
                     bundlePosition++)
                {
                    if (bundlePosition > 0)
                    {
                        neighbors.Add(new NeighborRecord(
                            new ChannelId(channelIndex),
                            new BundlePosition(bundlePosition),
                            new ChannelId(channelIndex),
                            new BundlePosition(bundlePosition - 1),
                            NeighborDirection.TowardEndA));
                    }

                    if (bundlePosition + 1 < BundlePositionCount)
                    {
                        neighbors.Add(new NeighborRecord(
                            new ChannelId(channelIndex),
                            new BundlePosition(bundlePosition),
                            new ChannelId(channelIndex),
                            new BundlePosition(bundlePosition + 1),
                            NeighborDirection.TowardEndB));
                    }

                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.North,
                        boundaries,
                        neighbors);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.East,
                        boundaries,
                        neighbors);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.South,
                        boundaries,
                        neighbors);
                    AddCardinalRelationOrBoundary(
                        channelIndex,
                        bundlePosition,
                        position,
                        NeighborDirection.West,
                        boundaries,
                        neighbors);
                }

                boundaries.Add(new BoundaryFaceRecord(
                    new ChannelId(channelIndex),
                    new BundlePosition(0),
                    TopologyFace.EndA,
                    BoundaryClassification.Vacuum));
                boundaries.Add(new BoundaryFaceRecord(
                    new ChannelId(channelIndex),
                    new BundlePosition(BundlePositionCount - 1),
                    TopologyFace.EndB,
                    BoundaryClassification.Vacuum));

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
            List<NeighborRecord> neighbors)
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

            if (TryGetChannelIndex(column, displayRow, out uint targetChannel))
            {
                neighbors.Add(new NeighborRecord(
                    new ChannelId(channelIndex),
                    new BundlePosition(bundlePosition),
                    new ChannelId(targetChannel),
                    new BundlePosition(bundlePosition),
                    direction));
                return;
            }

            boundaries.Add(new BoundaryFaceRecord(
                new ChannelId(channelIndex),
                new BundlePosition(bundlePosition),
                (TopologyFace)(byte)direction,
                BoundaryClassification.Vacuum));
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
