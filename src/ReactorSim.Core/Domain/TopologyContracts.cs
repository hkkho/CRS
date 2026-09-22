using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    public sealed class NeighborRecord
    {
        public NeighborRecord(
            ChannelId sourceChannelId,
            BundlePosition sourcePosition,
            ChannelId targetChannelId,
            BundlePosition targetPosition,
            NeighborDirection direction)
        {
            SourceChannelId = sourceChannelId;
            SourcePosition = sourcePosition;
            TargetChannelId = targetChannelId;
            TargetPosition = targetPosition;
            Direction = direction;
        }

        public ChannelId SourceChannelId { get; }

        public BundlePosition SourcePosition { get; }

        public ChannelId TargetChannelId { get; }

        public BundlePosition TargetPosition { get; }

        public NeighborDirection Direction { get; }
    }

    public sealed class BoundaryFaceRecord
    {
        public BoundaryFaceRecord(
            ChannelId channelId,
            BundlePosition position,
            TopologyFace face,
            BoundaryClassification classification)
        {
            ChannelId = channelId;
            Position = position;
            Face = face;
            Classification = classification;
        }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public TopologyFace Face { get; }

        public BoundaryClassification Classification { get; }
    }

    /// <summary>
    /// One explicit channel record. Construction only copies input; callers
    /// must use <see cref="CoreTopology.TryCreate"/> before treating it as a
    /// validated topology record.
    /// </summary>
    public sealed class ChannelTopology
    {
        public ChannelTopology(
            ChannelId channelId,
            int coordinateX,
            int coordinateY,
            FlowDirection flowDirection,
            BundlePosition inletPosition,
            BundlePosition outletPosition,
            IEnumerable<NeighborRecord> neighbors,
            IEnumerable<BoundaryFaceRecord> boundaryFaces)
        {
            if (neighbors == null)
            {
                throw new ArgumentNullException(nameof(neighbors));
            }

            if (boundaryFaces == null)
            {
                throw new ArgumentNullException(nameof(boundaryFaces));
            }

            ChannelId = channelId;
            CoordinateX = coordinateX;
            CoordinateY = coordinateY;
            FlowDirection = flowDirection;
            InletPosition = inletPosition;
            OutletPosition = outletPosition;
            Neighbors = new ReadOnlyCollection<NeighborRecord>(neighbors.ToArray());
            BoundaryFaces = new ReadOnlyCollection<BoundaryFaceRecord>(boundaryFaces.ToArray());
        }

        public ChannelId ChannelId { get; }

        public int CoordinateX { get; }

        public int CoordinateY { get; }

        public FlowDirection FlowDirection { get; }

        public BundlePosition InletPosition { get; }

        public BundlePosition OutletPosition { get; }

        public IReadOnlyList<NeighborRecord> Neighbors { get; }

        public IReadOnlyList<BoundaryFaceRecord> BoundaryFaces { get; }

        internal ChannelTopology Canonicalized()
        {
            NeighborRecord[] neighbors = Neighbors
                .OrderBy(record => record.SourcePosition.Value)
                .ThenBy(record => (byte)record.Direction)
                .ThenBy(record => record.TargetChannelId.Value)
                .ThenBy(record => record.TargetPosition.Value)
                .ToArray();
            BoundaryFaceRecord[] boundaries = BoundaryFaces
                .OrderBy(record => record.Position.Value)
                .ThenBy(record => (byte)record.Face)
                .ToArray();
            return new ChannelTopology(
                ChannelId,
                CoordinateX,
                CoordinateY,
                FlowDirection,
                InletPosition,
                OutletPosition,
                neighbors,
                boundaries);
        }
    }

    /// <summary>
    /// A validated, immutable topology with explicit stable node indexing.
    /// </summary>
    public sealed class CoreTopology
    {
        private readonly ReadOnlyCollection<ChannelTopology> _channels;

        private CoreTopology(uint channelCount, uint bundlePositionCount, IReadOnlyList<ChannelTopology> channels)
        {
            ChannelCount = channelCount;
            BundlePositionCount = bundlePositionCount;
            _channels = new ReadOnlyCollection<ChannelTopology>(channels.ToArray());
        }

        public uint ChannelCount { get; }

        public uint BundlePositionCount { get; }

        public int SlotCount
        {
            get { return checked((int)((ulong)ChannelCount * BundlePositionCount)); }
        }

        public IReadOnlyList<ChannelTopology> Channels
        {
            get { return _channels; }
        }

        public static ContractValidationResult<CoreTopology> TryCreate(
            uint channelCount,
            uint bundlePositionCount,
            IEnumerable<ChannelTopology> channels)
        {
            if (channels == null)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.Channels.Missing",
                    "channels",
                    "The channel record collection is required.");
            }

            var records = channels.ToList();
            if (records.Any(record => record == null))
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.Channel.Null",
                    "channels",
                    "A channel record may not be null.");
            }

            if (channelCount == 0 || channelCount > int.MaxValue)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ChannelCount.Invalid",
                    "channel_count",
                    "channel_count must be a positive representable count.");
            }

            if (bundlePositionCount == 0 || bundlePositionCount > int.MaxValue)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.BundlePositionCount.Invalid",
                    "bundle_position_count",
                    "bundle_position_count must be positive and representable.");
            }

            if ((ulong)channelCount * bundlePositionCount > int.MaxValue)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.SlotCount.Invalid",
                    "slot_count",
                    "The declared flat slot count is not representable.");
            }

            if (records.Count != (int)channelCount)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ChannelCount.Mismatch",
                    "channels",
                    "The number of channel records must equal channel_count.");
            }

            ChannelTopology[] orderedById = records
                .OrderBy(record => record.ChannelId.Value)
                .ThenBy(record => record.CoordinateX)
                .ThenBy(record => record.CoordinateY)
                .ToArray();

            uint? outOfRangeId = orderedById
                .Where(record => record.ChannelId.Value >= channelCount)
                .Select(record => (uint?)record.ChannelId.Value)
                .FirstOrDefault();
            if (outOfRangeId.HasValue)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ChannelId.OutOfRange",
                    "channels[channel_id=" + outOfRangeId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                    "Channel IDs must be less than channel_count.");
            }

            uint? duplicateId = FindDuplicateId(orderedById);
            if (duplicateId.HasValue)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.ChannelId.Duplicate",
                    "channels[channel_id=" + duplicateId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                    "Channel IDs must be unique.");
            }

            for (uint expected = 0; expected < channelCount; expected++)
            {
                if (!orderedById.Any(record => record.ChannelId.Value == expected))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.ChannelId.Missing",
                        "channels[channel_id=" + expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "The declared channel ID set must be exactly zero-based.");
                }
            }

            var coordinateRecords = orderedById
                .OrderBy(record => record.CoordinateX)
                .ThenBy(record => record.CoordinateY)
                .ThenBy(record => record.ChannelId.Value)
                .ToArray();
            for (int i = 1; i < coordinateRecords.Length; i++)
            {
                ChannelTopology previous = coordinateRecords[i - 1];
                ChannelTopology current = coordinateRecords[i];
                if (previous.CoordinateX == current.CoordinateX && previous.CoordinateY == current.CoordinateY)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Coordinate.Duplicate",
                        ContractValidation.ChannelPath(current.ChannelId, ".coordinate"),
                        "Channel lattice coordinates must be unique.");
                }
            }

            ChannelTopology[] canonicalChannels = orderedById
                .OrderBy(record => record.ChannelId.Value)
                .ToArray();
            for (int i = 0; i < canonicalChannels.Length; i++)
            {
                ChannelTopology channel = canonicalChannels[i];
                if (channel.Neighbors.Any(record => record == null))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.Null",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "A neighbor record may not be null.");
                }

                if (channel.BoundaryFaces.Any(record => record == null))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.Null",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "A boundary-face record may not be null.");
                }
            }

            for (int i = 0; i < canonicalChannels.Length; i++)
            {
                ContractValidationResult<CoreTopology>? failure = ValidateChannel(
                    canonicalChannels[i],
                    channelCount,
                    bundlePositionCount);
                if (failure != null)
                {
                    return failure;
                }
            }

            for (int i = 0; i < canonicalChannels.Length; i++)
            {
                ChannelTopology channel = canonicalChannels[i];
                for (uint position = 0; position + 1 < bundlePositionCount; position++)
                {
                    int forwardCount = CountNeighbor(channel, position, NeighborDirection.TowardEndB);
                    int reverseCount = CountNeighbor(
                        channel,
                        position + 1,
                        NeighborDirection.TowardEndA);
                    if (forwardCount != 1 || reverseCount != 1)
                    {
                        return ContractValidationResult<CoreTopology>.Invalid(
                            "Topology.WithinChannel.PairMissing",
                            ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                            "Every adjacent position pair must have exactly one reciprocal within-channel relation.");
                    }
                }
            }

            for (int i = 0; i < canonicalChannels.Length; i++)
            {
                ChannelTopology channel = canonicalChannels[i];
                foreach (NeighborRecord neighbor in channel.Neighbors)
                {
                    ChannelTopology target = canonicalChannels[(int)neighbor.TargetChannelId.Value];
                    NeighborDirection inverse = Inverse(neighbor.Direction);
                    bool reciprocal = target.Neighbors.Any(candidate =>
                        candidate.SourceChannelId == neighbor.TargetChannelId &&
                        candidate.SourcePosition == neighbor.TargetPosition &&
                        candidate.TargetChannelId == neighbor.SourceChannelId &&
                        candidate.TargetPosition == neighbor.SourcePosition &&
                        candidate.Direction == inverse);
                    if (!reciprocal)
                    {
                        return ContractValidationResult<CoreTopology>.Invalid(
                            "Topology.Neighbor.ReciprocalMissing",
                            ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                            "Every neighbor relation must have one reciprocal relation.");
                    }
                }
            }

            for (int i = 0; i < canonicalChannels.Length; i++)
            {
                ChannelTopology channel = canonicalChannels[i];
                foreach (NeighborRecord neighbor in channel.Neighbors
                             .Where(record => IsCardinal(record.Direction))
                             .OrderBy(record => record.SourcePosition.Value)
                             .ThenBy(record => (byte)record.Direction)
                             .ThenBy(record => record.TargetChannelId.Value)
                             .ThenBy(record => record.TargetPosition.Value))
                {
                    ChannelTopology target = canonicalChannels[(int)neighbor.TargetChannelId.Value];
                    int deltaX;
                    int deltaY;
                    switch (neighbor.Direction)
                    {
                        case NeighborDirection.North:
                            deltaX = 0;
                            deltaY = 1;
                            break;
                        case NeighborDirection.East:
                            deltaX = 1;
                            deltaY = 0;
                            break;
                        case NeighborDirection.South:
                            deltaX = 0;
                            deltaY = -1;
                            break;
                        case NeighborDirection.West:
                            deltaX = -1;
                            deltaY = 0;
                            break;
                        default:
                            throw new InvalidOperationException("A filtered cardinal relation had an invalid direction.");
                    }

                    long expectedX = (long)channel.CoordinateX + deltaX;
                    long expectedY = (long)channel.CoordinateY + deltaY;
                    if (target.CoordinateX != expectedX || target.CoordinateY != expectedY)
                    {
                        return ContractValidationResult<CoreTopology>.Invalid(
                            "Topology.Neighbor.CoordinateMismatch",
                            ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                            "A cardinal neighbor direction must match the explicit lattice coordinate delta.");
                    }
                }
            }

            for (int channelIndex = 0; channelIndex < canonicalChannels.Length; channelIndex++)
            {
                ChannelTopology channel = canonicalChannels[channelIndex];
                for (uint position = 0; position < bundlePositionCount; position++)
                {
                    foreach (NeighborDirection direction in CardinalDirections)
                    {
                        int neighborCount = CountNeighbor(channel, position, direction);
                        int boundaryCount = CountBoundary(channel, position, ToFace(direction));
                        if (neighborCount + boundaryCount != 1)
                        {
                            return ContractValidationResult<CoreTopology>.Invalid(
                                "Topology.FaceCoverage.Invalid",
                                ContractValidation.ChannelPath(channel.ChannelId, ".faces"),
                                "Every cardinal face must have exactly one explicit neighbor or boundary outcome.");
                        }
                    }
                }
            }

            var canonical = canonicalChannels.Select(channel => channel.Canonicalized()).ToArray();
            return ContractValidationResult<CoreTopology>.Valid(
                new CoreTopology(channelCount, bundlePositionCount, canonical));
        }

        public ChannelTopology GetChannel(ChannelId channelId)
        {
            if (channelId.Value >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelId));
            }

            return _channels[(int)channelId.Value];
        }

        public bool TryGetFlatIndex(NodeKey node, out int index)
        {
            if (node.ChannelId.Value >= ChannelCount || node.Position.Value >= BundlePositionCount)
            {
                index = -1;
                return false;
            }

            index = checked((int)((ulong)node.ChannelId.Value * BundlePositionCount + node.Position.Value));
            return true;
        }

        public int GetFlatIndex(NodeKey node)
        {
            int index;
            if (!TryGetFlatIndex(node, out index))
            {
                throw new ArgumentOutOfRangeException(nameof(node));
            }

            return index;
        }

        public IEnumerable<NodeKey> EnumerateNodes()
        {
            for (uint channel = 0; channel < ChannelCount; channel++)
            {
                for (uint position = 0; position < BundlePositionCount; position++)
                {
                    yield return new NodeKey(new ChannelId(channel), new BundlePosition(position));
                }
            }
        }

        private static ContractValidationResult<CoreTopology>? ValidateChannel(
            ChannelTopology channel,
            uint channelCount,
            uint bundlePositionCount)
        {
            if (!Enum.IsDefined(typeof(FlowDirection), channel.FlowDirection))
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.FlowDirection.Invalid",
                    ContractValidation.ChannelPath(channel.ChannelId, ".flow_direction"),
                    "The flow direction must be an explicit EndAtoEndB or EndBtoEndA value.");
            }

            BundlePosition expectedInlet = channel.FlowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(0)
                : new BundlePosition(bundlePositionCount - 1);
            BundlePosition expectedOutlet = channel.FlowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(bundlePositionCount - 1)
                : new BundlePosition(0);
            if (channel.InletPosition != expectedInlet || channel.OutletPosition != expectedOutlet)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.EndpointBinding.Invalid",
                    ContractValidation.ChannelPath(channel.ChannelId, ".inlet_position"),
                    "Inlet and outlet positions must match the declared flow direction.");
            }

            var neighborKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (NeighborRecord neighbor in channel.Neighbors
                         .OrderBy(record => record.SourcePosition.Value)
                         .ThenBy(record => (byte)record.Direction)
                         .ThenBy(record => record.TargetChannelId.Value)
                         .ThenBy(record => record.TargetPosition.Value))
            {
                string key = neighbor.SourceChannelId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                             neighbor.SourcePosition.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                             ((byte)neighbor.Direction).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!neighborKeys.Add(key))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.Duplicate",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "Neighbor uniqueness is keyed by source channel, source position, and direction.");
                }

                if (neighbor.SourceChannelId != channel.ChannelId)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.SourceMismatch",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "A channel record may contain only relations whose source channel is itself.");
                }

                if (neighbor.SourcePosition.Value >= bundlePositionCount)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.SourcePosition.OutOfRange",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "The neighbor source position is outside bundle_position_count.");
                }

                if (neighbor.TargetChannelId.Value >= channelCount)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.TargetChannel.OutOfRange",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "The neighbor target channel is outside channel_count.");
                }

                if (neighbor.TargetPosition.Value >= bundlePositionCount)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.TargetPosition.OutOfRange",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "The neighbor target position is outside bundle_position_count.");
                }

                if (neighbor.SourceChannelId == neighbor.TargetChannelId &&
                    neighbor.SourcePosition == neighbor.TargetPosition)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.SelfReference",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "A neighbor relation may not target its own source slot.");
                }

                if (!Enum.IsDefined(typeof(NeighborDirection), neighbor.Direction))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Neighbor.Direction.Invalid",
                        ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                        "The neighbor direction is not a v1 direction.");
                }

                if (IsCardinal(neighbor.Direction))
                {
                    if (neighbor.TargetChannelId == neighbor.SourceChannelId ||
                        neighbor.TargetPosition != neighbor.SourcePosition)
                    {
                        return ContractValidationResult<CoreTopology>.Invalid(
                            "Topology.Neighbor.TransverseShape.Invalid",
                            ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                            "A cardinal relation must cross channels at the same bundle position.");
                    }

                    ChannelTopology? target = null;
                    // The target coordinates are checked in the cross-channel pass
                    // below, after all channel IDs have been shown to be complete.
                    _ = target;
                }
                else
                {
                    if (neighbor.TargetChannelId != neighbor.SourceChannelId ||
                        !IsLegalWithinChannelStep(neighbor.SourcePosition.Value, neighbor.TargetPosition.Value, neighbor.Direction))
                    {
                        return ContractValidationResult<CoreTopology>.Invalid(
                            "Topology.Neighbor.WithinChannelShape.Invalid",
                            ContractValidation.ChannelPath(channel.ChannelId, ".neighbors"),
                            "A within-channel relation must connect adjacent positions in its named direction.");
                    }
                }
            }

            BoundaryFaceRecord[] orderedBoundaries = channel.BoundaryFaces
                         .OrderBy(record => record.Position.Value)
                         .ThenBy(record => (byte)record.Face)
                         .ThenBy(record => (byte)record.Classification)
                         .ToArray();
            foreach (BoundaryFaceRecord boundary in orderedBoundaries)
            {
                if (boundary.ChannelId != channel.ChannelId)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.ChannelMismatch",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "A boundary record must belong to its containing channel.");
                }

                if (boundary.Position.Value >= bundlePositionCount)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.Position.OutOfRange",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "The boundary position is outside bundle_position_count.");
                }

                if (!Enum.IsDefined(typeof(TopologyFace), boundary.Face) ||
                    !Enum.IsDefined(typeof(BoundaryClassification), boundary.Classification))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.Label.Invalid",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "Boundary face and classification labels must be known v1 values.");
                }

                if (boundary.Face == TopologyFace.EndA && boundary.Position.Value != 0)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.EndAPosition.Invalid",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "EndA is valid only at position zero.");
                }

                if (boundary.Face == TopologyFace.EndB && boundary.Position.Value != bundlePositionCount - 1)
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.EndBPosition.Invalid",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "EndB is valid only at the final bundle position.");
                }
            }

            var boundaryKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (BoundaryFaceRecord boundary in orderedBoundaries)
            {
                string key = boundary.Position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                             ((byte)boundary.Face).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!boundaryKeys.Add(key))
                {
                    return ContractValidationResult<CoreTopology>.Invalid(
                        "Topology.Boundary.Duplicate",
                        ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                        "Boundary uniqueness is keyed by channel, position, and face.");
                }
            }

            if (CountBoundary(channel, 0, TopologyFace.EndA) != 1 ||
                CountBoundary(channel, bundlePositionCount - 1, TopologyFace.EndB) != 1)
            {
                return ContractValidationResult<CoreTopology>.Invalid(
                    "Topology.EndpointBoundary.Missing",
                    ContractValidation.ChannelPath(channel.ChannelId, ".boundary_faces"),
                    "Each channel must explicitly declare one EndA@0 and one EndB@last boundary.");
            }

            return null;
        }

        private static uint? FindDuplicateId(ChannelTopology[] records)
        {
            for (int i = 1; i < records.Length; i++)
            {
                if (records[i - 1].ChannelId == records[i].ChannelId)
                {
                    return records[i].ChannelId.Value;
                }
            }

            return null;
        }

        private static int CountNeighbor(ChannelTopology channel, uint position, NeighborDirection direction)
        {
            return channel.Neighbors.Count(record =>
                record.SourcePosition.Value == position && record.Direction == direction);
        }

        private static int CountBoundary(ChannelTopology channel, uint position, TopologyFace face)
        {
            return channel.BoundaryFaces.Count(record =>
                record.Position.Value == position && record.Face == face);
        }

        private static bool IsCardinal(NeighborDirection direction)
        {
            return direction == NeighborDirection.North || direction == NeighborDirection.East ||
                   direction == NeighborDirection.South || direction == NeighborDirection.West;
        }

        private static bool IsLegalWithinChannelStep(uint source, uint target, NeighborDirection direction)
        {
            return (direction == NeighborDirection.TowardEndA && source == target + 1) ||
                   (direction == NeighborDirection.TowardEndB && target == source + 1);
        }

        private static TopologyFace ToFace(NeighborDirection direction)
        {
            return (TopologyFace)(byte)direction;
        }

        private static NeighborDirection Inverse(NeighborDirection direction)
        {
            switch (direction)
            {
                case NeighborDirection.North:
                    return NeighborDirection.South;
                case NeighborDirection.East:
                    return NeighborDirection.West;
                case NeighborDirection.South:
                    return NeighborDirection.North;
                case NeighborDirection.West:
                    return NeighborDirection.East;
                case NeighborDirection.TowardEndA:
                    return NeighborDirection.TowardEndB;
                case NeighborDirection.TowardEndB:
                    return NeighborDirection.TowardEndA;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static readonly NeighborDirection[] CardinalDirections =
        {
            NeighborDirection.North,
            NeighborDirection.East,
            NeighborDirection.South,
            NeighborDirection.West
        };
    }
}
