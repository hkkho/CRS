using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Owner-approved synthetic P6-T05 identities and scalar fixture values.
    /// These values are test-only and are not production controller defaults.
    /// </summary>
    public static class RrsFixtureV1
    {
        public const string FixtureId = "P6-T05-SYNTHETIC-RRS-CONTROLLER-MAP-V1";
        public const string ApprovedDataVersion = "p6-t05-synthetic-rrs-controller-map-v1";
        public const string ApprovedMapVersion = "p6-t05-synthetic-rrs-controller-map-v1";
        public const string ControllerSchemaId = "CANDU-RRS-CONTROLLER-STATE-V1";
        public const string MapSchemaId = "CANDU-RRS-INFLUENCE-MAP-V1";
        public const string QueueSchemaId = "CANDU-RRS-QUEUE-V1";
        public const string RegionSetSchemaId = "CANDU-RRS-REGION-SET-V1";
        public const string SignCertificate = "P6-T05-SYNTHETIC-NEGATIVE-FEEDBACK-V1";
        public const string OwnerId = "Kevin Ho";
        public const string Normalization = "None";
        public const string SourceUnit = "1";
        public const string TargetUnit = "m^-1";
        public const string SourceUnitDescription = "dimensionless actuator state";
        public const double PowerSetpointWatts = 1000.0;
        public const double AutomaticCadenceSeconds = 1.0;
        public const double InitialTimeSeconds = 0.0;
        public const double InitialActuatorState = 0.5;
        public const double LowerBound = 0.0;
        public const double UpperBound = 1.0;
        public const double RateLimitPerSecond = 0.1;
        public const double DelaySeconds = 0.5;
        public const double TargetFraction = 0.5;
        public const double TotalPowerBias = 0.5;
        public const double TotalPowerKpPerWatt = 0.001;
        public const double TotalPowerKiPerWattSecond = 0.0001;
        public const double TiltBias = 0.5;
        public const double TiltKpPerWatt = 0.0;
        public const double TiltKiPerWattSecond = 0.0;
        public const double TiltKtLeft = -0.5;
        public const double TiltKtRight = 0.5;
        public const double TiltKtiLeftPerSecond = -0.05;
        public const double TiltKtiRightPerSecond = 0.05;
        public const ushort GroupCount = 2;
        public const ushort TargetNodeCount = 6;
        public const ushort MapEntryCount = 24;
        public const double TotalPowerGroup0WeightMInverse = -0.02;
        public const double TotalPowerGroup1WeightMInverse = -0.01;
        public const double TiltLeftGroup0WeightMInverse = 0.01;
        public const double TiltLeftGroup1WeightMInverse = 0.006;
        public const double TiltRightGroup0WeightMInverse = -0.01;
        public const double TiltRightGroup1WeightMInverse = -0.006;

        public static readonly StableId ControllerId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c501");

        public static readonly StableId MapId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c502");

        public static readonly StableId QueueId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c503");

        public static readonly StableId TopologyId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c504");

        public static readonly StableId RegionSetId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c505");

        public static readonly StableId LeftRegionId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c511");

        public static readonly StableId RightRegionId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c512");

        public static readonly StableId TotalPowerActuatorId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c521");

        public static readonly StableId TiltActuatorId = StableId.Parse(
            "00000000-0000-0000-0000-00000000c522");

        public static readonly Digest32 ApprovedTopologyDigest = ParseDigest(
            "77dac0b13d6d4d7aca0c34f6bd8cf932b22146dff697ec89979790a4f7e4a472");

        public static readonly Digest32 ApprovedRegionSetDigest = ParseDigest(
            "8932d81d8268c64318b4624d2eae1fa13f7d02139c66896c42b2eccfde334eb6");

        public static readonly Digest32 ApprovedReferenceStateDigest = ParseDigest(
            "97b54fd75bd5636bb6b5433b10b6ff831f7dca0fa04647569b15aaa485776603");

        public static readonly Digest32 ApprovedMapDigest = ParseDigest(
            "d198c27636f58b612fe7c184e90b329874535d0d9dd1f5de1ce6a954ba0fd1e8");

        public static readonly Digest32 ApprovedQueueDigest = ParseDigest(
            "59ae75e357b6ecf48c39a47daf7c2bfbdc6dce91b8a7f66eef887ac893de55a9");

        public static NodeKey[] TargetNodes()
        {
            return Enumerable.Range(0, (int)TargetNodeCount)
                .Select(index => new NodeKey(
                    new ChannelId((uint)index),
                    new BundlePosition(0)))
                .ToArray();
        }

        public static NodeKey[] LeftNodes()
        {
            return Enumerable.Range(0, 3)
                .Select(index => new NodeKey(
                    new ChannelId((uint)index),
                    new BundlePosition(0)))
                .ToArray();
        }

        public static NodeKey[] RightNodes()
        {
            return Enumerable.Range(3, 3)
                .Select(index => new NodeKey(
                    new ChannelId((uint)index),
                    new BundlePosition(0)))
                .ToArray();
        }

        private static Digest32 ParseDigest(string hex)
        {
            if (hex == null || hex.Length != 64)
            {
                throw new ArgumentException("A synthetic digest must contain exactly 64 hexadecimal characters.", nameof(hex));
            }

            byte[] bytes = new byte[32];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(
                    hex.AsSpan(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return new Digest32(bytes);
        }
    }

    public enum RrsModeV1 : byte
    {
        Manual = 0,
        Automatic = 1,
        Held = 2
    }

    public enum RrsControlPolarityV1 : byte
    {
        NegativeFeedback = 0
    }

    internal static class RrsValidationV1
    {
        public static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }

        public static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        public static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        public static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalNonnegative(value) && value <= 1.0;
        }

        public static bool IsCanonicalSignedGain(double value)
        {
            return IsCanonicalFinite(value);
        }

        public static bool IsKnownActuator(StableId actuatorId)
        {
            return actuatorId == RrsFixtureV1.TotalPowerActuatorId ||
                   actuatorId == RrsFixtureV1.TiltActuatorId;
        }

        public static bool IsKnownRegion(StableId regionId)
        {
            return regionId == RrsFixtureV1.LeftRegionId ||
                   regionId == RrsFixtureV1.RightRegionId;
        }

        public static void WriteOptionalDouble(BinaryWriter writer, double? value)
        {
            writer.Write(value.HasValue ? (byte)1 : (byte)0);
            if (value.HasValue)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, value.Value);
            }
        }

        public static bool IsZero(double value)
        {
            return value == 0.0 && BitConverter.DoubleToInt64Bits(value) >= 0;
        }
    }

    /// <summary>
    /// One explicit, complete target region. Region ordering and node ordering
    /// are canonicalized; no array position is interpreted as a physical map.
    /// </summary>
    public sealed class RrsRegionDefinitionV1
    {
        public const uint CurrentSchemaVersion = 1;

        private RrsRegionDefinitionV1(
            StableId regionId,
            double targetFraction,
            IEnumerable<NodeKey> targetNodes)
        {
            RegionId = regionId;
            TargetFraction = targetFraction;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
        }

        public StableId RegionId { get; }

        public double TargetFraction { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public static ContractValidationResult<RrsRegionDefinitionV1> TryCreate(
            StableId regionId,
            double targetFraction,
            IEnumerable<NodeKey>? targetNodes)
        {
            if (!RrsValidationV1.IsKnownRegion(regionId))
            {
                return Invalid(
                    "RrsRegionDefinition.RegionId.Unapproved",
                    "region_id",
                    "Only the two owner-approved synthetic region identities are accepted.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(targetFraction) ||
                targetFraction != RrsFixtureV1.TargetFraction)
            {
                return Invalid(
                    "RrsRegionDefinition.TargetFraction.Unapproved",
                    "target_fraction",
                    "Each approved synthetic region target fraction is exactly 0.5.");
            }

            if (targetNodes == null)
            {
                return Invalid(
                    "RrsRegionDefinition.TargetNodes.Missing",
                    "target_nodes",
                    "An explicit region target-node list is required.");
            }

            NodeKey[] canonical = targetNodes.OrderBy(node => node).ToArray();
            NodeKey[] expected = regionId == RrsFixtureV1.LeftRegionId
                ? RrsFixtureV1.LeftNodes()
                : RrsFixtureV1.RightNodes();
            if (!canonical.SequenceEqual(expected))
            {
                return Invalid(
                    "RrsRegionDefinition.TargetNodes.Unapproved",
                    "target_nodes",
                    "The approved synthetic left/right region node partition is required exactly.");
            }

            return ContractValidationResult<RrsRegionDefinitionV1>.Valid(
                new RrsRegionDefinitionV1(regionId, targetFraction, canonical));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-REGION-DEFINITION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, RegionId);
                Phase5CanonicalBytesV1.WriteDouble(writer, TargetFraction);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)TargetNodes.Count));
                foreach (NodeKey node in TargetNodes)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }
            });
        }

        private static ContractValidationResult<RrsRegionDefinitionV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsRegionDefinitionV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete disjoint region partition bound to the six explicit synthetic
    /// target nodes.
    /// </summary>
    public sealed class RrsRegionSetV1
    {
        public const uint CurrentSchemaVersion = 1;

        private RrsRegionSetV1(
            StableId regionSetId,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            IEnumerable<RrsRegionDefinitionV1> regions,
            Digest32 regionSetDigest)
        {
            RegionSetId = regionSetId;
            TopologyId = topologyId;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
            TopologyDigest = topologyDigest;
            Regions = new ReadOnlyCollection<RrsRegionDefinitionV1>(regions.ToArray());
            RegionSetDigest = regionSetDigest;
        }

        public StableId RegionSetId { get; }

        public StableId TopologyId { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public Digest32 TopologyDigest { get; }

        public IReadOnlyList<RrsRegionDefinitionV1> Regions { get; }

        public Digest32 RegionSetDigest { get; }

        public static ContractValidationResult<RrsRegionSetV1> TryCreate(
            uint schemaVersion,
            StableId regionSetId,
            StableId topologyId,
            IEnumerable<NodeKey>? targetNodes,
            Digest32? topologyDigest,
            IEnumerable<RrsRegionDefinitionV1>? regions,
            Digest32? expectedRegionSetDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "RrsRegionSet.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic RRS region-set schema version 1 is accepted.");
            }

            if (regionSetId != RrsFixtureV1.RegionSetId || topologyId != RrsFixtureV1.TopologyId)
            {
                return Invalid(
                    "RrsRegionSet.Identity.Unapproved",
                    "identity",
                    "The approved synthetic region-set and topology identities are required.");
            }

            if (targetNodes == null || topologyDigest == null || regions == null)
            {
                return Invalid(
                    "RrsRegionSet.CompleteState.Missing",
                    "region_set",
                    "Target nodes, topology digest, and complete regions are required.");
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            NodeKey[] expectedTargets = RrsFixtureV1.TargetNodes();
            if (!canonicalTargets.SequenceEqual(expectedTargets))
            {
                return Invalid(
                    "RrsRegionSet.TargetNodes.Unapproved",
                    "target_nodes",
                    "The six explicit (ChannelId=0..5, BundlePosition=0) nodes are required.");
            }

            Digest32 calculatedTopology = ComputeTopologyDigest(topologyId, canonicalTargets);
            if (!topologyDigest.Equals(calculatedTopology))
            {
                return Invalid(
                    "RrsRegionSet.TopologyDigest.Mismatch",
                    "topology_digest",
                    "The topology digest does not equal the canonical target-node topology.");
            }

            RrsRegionDefinitionV1[] canonicalRegions = regions
                .OrderBy(region => region.RegionId)
                .ToArray();
            if (canonicalRegions.Length != 2 ||
                canonicalRegions[0].RegionId != RrsFixtureV1.LeftRegionId ||
                canonicalRegions[1].RegionId != RrsFixtureV1.RightRegionId)
            {
                return Invalid(
                    "RrsRegionSet.Regions.Incomplete",
                    "regions",
                    "The complete left/right region partition is required.");
            }

            if (canonicalRegions.Sum(region => region.TargetFraction) != 1.0)
            {
                return Invalid(
                    "RrsRegionSet.TargetFractions.SumMismatch",
                    "regions",
                    "Region target fractions must sum exactly to one.");
            }

            HashSet<NodeKey> covered = new HashSet<NodeKey>();
            foreach (RrsRegionDefinitionV1 region in canonicalRegions)
            {
                foreach (NodeKey node in region.TargetNodes)
                {
                    if (!covered.Add(node))
                    {
                        return Invalid(
                            "RrsRegionSet.Nodes.Overlap",
                            "regions",
                            "Region target nodes must be disjoint.");
                    }
                }
            }

            if (covered.Count != canonicalTargets.Length ||
                !canonicalTargets.All(covered.Contains))
            {
                return Invalid(
                    "RrsRegionSet.Nodes.Incomplete",
                    "regions",
                    "Region target nodes must cover every explicit topology node exactly once.");
            }

            if (expectedRegionSetDigest == null)
            {
                return Invalid(
                    "RrsRegionSet.Digest.Missing",
                    "region_set_digest",
                    "The complete region-set digest is required.");
            }

            Digest32 calculatedRegionSetDigest = ComputeRegionSetDigest(
                regionSetId,
                topologyId,
                canonicalTargets,
                calculatedTopology,
                canonicalRegions);
            if (!expectedRegionSetDigest.Equals(calculatedRegionSetDigest))
            {
                return Invalid(
                    "RrsRegionSet.Digest.Mismatch",
                    "region_set_digest",
                    "The supplied region-set digest does not equal the canonical partition.");
            }

            return ContractValidationResult<RrsRegionSetV1>.Valid(
                new RrsRegionSetV1(
                    regionSetId,
                    topologyId,
                    canonicalTargets,
                    calculatedTopology,
                    canonicalRegions,
                    expectedRegionSetDigest));
        }

        public static Digest32 ComputeTopologyDigest(
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes)
        {
            NodeKey[] canonical = targetNodes.OrderBy(node => node).ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-TOPOLOGY-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonical.Length));
                    foreach (NodeKey node in canonical)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                    }
                })));
        }

        public static Digest32 ComputeRegionSetDigest(
            StableId regionSetId,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            IEnumerable<RrsRegionDefinitionV1> regions)
        {
            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            RrsRegionDefinitionV1[] canonicalRegions = regions
                .OrderBy(region => region.RegionId)
                .ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, RrsFixtureV1.RegionSetSchemaId);
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteStableId(writer, regionSetId);
                    Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                    Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonicalTargets.Length));
                    foreach (NodeKey node in canonicalTargets)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                    }

                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonicalRegions.Length));
                    foreach (RrsRegionDefinitionV1 region in canonicalRegions)
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, region.ToCanonicalBytes());
                    }
                })));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, RrsFixtureV1.RegionSetSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, RegionSetId);
                Phase5CanonicalBytesV1.WriteStableId(writer, TopologyId);
                Phase5CanonicalBytesV1.WriteDigest(writer, TopologyDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)TargetNodes.Count));
                foreach (NodeKey node in TargetNodes)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)Regions.Count));
                foreach (RrsRegionDefinitionV1 region in Regions)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, region.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteDigest(writer, RegionSetDigest);
            });
        }

        private static ContractValidationResult<RrsRegionSetV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsRegionSetV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One queue-owned available command. It is a read-only projection in
    /// P6-T05; queue allocation and mutation are reserved for P6-T06.
    /// </summary>
    public sealed class RrsAvailableCommandV1
    {
        private RrsAvailableCommandV1(
            StableId actuatorId,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            ActuatorId = actuatorId;
            BoundedCommand = boundedCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
        }

        public StableId ActuatorId { get; }

        public double BoundedCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public static ContractValidationResult<RrsAvailableCommandV1> TryCreate(
            StableId actuatorId,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            if (!RrsValidationV1.IsKnownActuator(actuatorId))
            {
                return Invalid(
                    "RrsAvailableCommand.ActuatorId.Unapproved",
                    "actuator_id",
                    "The available command must target one of the two approved actuators.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(boundedCommand) ||
                !RrsValidationV1.IsCanonicalFraction(lowerBound) ||
                !RrsValidationV1.IsCanonicalFraction(upperBound) ||
                lowerBound != RrsFixtureV1.LowerBound ||
                upperBound != RrsFixtureV1.UpperBound ||
                boundedCommand < lowerBound || boundedCommand > upperBound)
            {
                return Invalid(
                    "RrsAvailableCommand.Bounds.Invalid",
                    "bounds",
                    "Available commands require the approved inclusive [0,1] bounds.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                rateLimitPerSecond != RrsFixtureV1.RateLimitPerSecond)
            {
                return Invalid(
                    "RrsAvailableCommand.Rate.Unapproved",
                    "rate_limit_per_second",
                    "The approved synthetic actuator rate limit is exactly 0.1 s^-1.");
            }

            return ContractValidationResult<RrsAvailableCommandV1>.Valid(
                new RrsAvailableCommandV1(
                    actuatorId,
                    boundedCommand,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-QUEUE-AVAILABLE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
            });
        }

        private static ContractValidationResult<RrsAvailableCommandV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsAvailableCommandV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete pending-command record shape retained for queue binding. The
    /// approved P6-T05 supplied projection is explicitly empty, so this task
    /// does not expose a constructor that allocates one.
    /// </summary>
    public sealed class RrsPendingCommandV1
    {
        private RrsPendingCommandV1(
            StableId queueId,
            StableId commandId,
            StableId actuatorId,
            StableId sourceEventId,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            ulong sequence,
            double requestedCommand,
            double boundedCommand,
            Digest32 commandDigest)
        {
            QueueId = queueId;
            CommandId = commandId;
            ActuatorId = actuatorId;
            SourceEventId = sourceEventId;
            EnqueueTimeSeconds = enqueueTimeSeconds;
            DelaySeconds = delaySeconds;
            DueTimeSeconds = dueTimeSeconds;
            Sequence = sequence;
            RequestedCommand = requestedCommand;
            BoundedCommand = boundedCommand;
            CommandDigest = commandDigest;
        }

        public StableId QueueId { get; }

        public StableId CommandId { get; }

        public StableId ActuatorId { get; }

        public StableId SourceEventId { get; }

        public double EnqueueTimeSeconds { get; }

        public double DelaySeconds { get; }

        public double DueTimeSeconds { get; }

        public ulong Sequence { get; }

        public double RequestedCommand { get; }

        public double BoundedCommand { get; }

        public Digest32 CommandDigest { get; }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-PENDING-COMMAND-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteStableId(writer, QueueId);
                Phase5CanonicalBytesV1.WriteStableId(writer, CommandId);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteStableId(writer, SourceEventId);
                Phase5CanonicalBytesV1.WriteDouble(writer, EnqueueTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, DelaySeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, DueTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt64(writer, Sequence);
                Phase5CanonicalBytesV1.WriteDouble(writer, RequestedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedCommand);
                Phase5CanonicalBytesV1.WriteDigest(writer, CommandDigest);
            });
        }
    }

    /// <summary>
    /// Complete, immutable supplied queue projection. It contains no mutating
    /// API; P6-T06 owns allocation, enqueue, consume, transition, and rollback.
    /// </summary>
    public sealed class RrsQueueStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private RrsQueueStateV1(
            uint schemaVersion,
            StableId queueId,
            StableId ownerControllerId,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<RrsAvailableCommandV1> availableCommands,
            IEnumerable<RrsPendingCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds,
            Digest32 queueDigest)
        {
            SchemaVersion = schemaVersion;
            QueueId = queueId;
            OwnerControllerId = ownerControllerId;
            GenerationCadenceOrNA = generationCadenceOrNA;
            InitialNextSequence = initialNextSequence;
            NextSequence = nextSequence;
            LastMotionTimeSeconds = lastMotionTimeSeconds;
            AvailableCommands = new ReadOnlyCollection<RrsAvailableCommandV1>(availableCommands.ToArray());
            PendingCommands = new ReadOnlyCollection<RrsPendingCommandV1>(pendingCommands.ToArray());
            AppliedSourceEventIds = new ReadOnlyCollection<StableId>(appliedSourceEventIds.ToArray());
            AllocatedCommandIds = new ReadOnlyCollection<StableId>(allocatedCommandIds.ToArray());
            QueueDigest = queueDigest;
        }

        public uint SchemaVersion { get; }

        public StableId QueueId { get; }

        public StableId OwnerControllerId { get; }

        public double? GenerationCadenceOrNA { get; }

        public ulong InitialNextSequence { get; }

        public ulong NextSequence { get; }

        public double LastMotionTimeSeconds { get; }

        public IReadOnlyList<RrsAvailableCommandV1> AvailableCommands { get; }

        public IReadOnlyList<RrsPendingCommandV1> PendingCommands { get; }

        public IReadOnlyList<StableId> AppliedSourceEventIds { get; }

        public IReadOnlyList<StableId> AllocatedCommandIds { get; }

        public Digest32 QueueDigest { get; }

        public static ContractValidationResult<RrsQueueStateV1> TryCreate(
            uint schemaVersion,
            StableId queueId,
            StableId ownerControllerId,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<RrsAvailableCommandV1>? availableCommands,
            IEnumerable<RrsPendingCommandV1>? pendingCommands,
            IEnumerable<StableId>? appliedSourceEventIds,
            IEnumerable<StableId>? allocatedCommandIds,
            Digest32? expectedQueueDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "RrsQueue.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic RRS queue schema version 1 is accepted.");
            }

            if (queueId != RrsFixtureV1.QueueId || ownerControllerId != RrsFixtureV1.ControllerId)
            {
                return Invalid(
                    "RrsQueue.Identity.Unapproved",
                    "identity",
                    "The supplied queue must bind to the owner-approved controller and queue identities.");
            }

            if (!generationCadenceOrNA.HasValue ||
                !RrsValidationV1.IsCanonicalPositive(generationCadenceOrNA.Value) ||
                generationCadenceOrNA.Value != RrsFixtureV1.AutomaticCadenceSeconds)
            {
                return Invalid(
                    "RrsQueue.GenerationCadence.Unapproved",
                    "generation_cadence_or_na",
                    "The supplied synthetic queue cadence is exactly 1.0 s when applicable.");
            }

            if (initialNextSequence != 0 || nextSequence != 0)
            {
                return Invalid(
                    "RrsQueue.Sequence.Invalid",
                    "next_sequence",
                    "The approved supplied queue projection requires InitialNextSequence and NextSequence both equal zero.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(lastMotionTimeSeconds))
            {
                return Invalid(
                    "RrsQueue.LastMotionTime.Invalid",
                    "last_motion_time_s",
                    "LastMotionTime must be finite, nonnegative SI seconds.");
            }

            if (lastMotionTimeSeconds != RrsFixtureV1.InitialTimeSeconds)
            {
                return Invalid(
                    "RrsQueue.LastMotionTime.Unapproved",
                    "last_motion_time_s",
                    "The approved supplied queue projection requires LastMotionTime exactly 0.0 s.");
            }

            if (availableCommands == null || pendingCommands == null ||
                appliedSourceEventIds == null || allocatedCommandIds == null)
            {
                return Invalid(
                    "RrsQueue.CompleteState.Missing",
                    "queue",
                    "The complete supplied queue projection is required.");
            }

            RrsAvailableCommandV1[] available = availableCommands
                .OrderBy(command => command.ActuatorId)
                .ToArray();
            if (available.Length != 2 ||
                available[0].ActuatorId != RrsFixtureV1.TotalPowerActuatorId ||
                available[1].ActuatorId != RrsFixtureV1.TiltActuatorId)
            {
                return Invalid(
                    "RrsQueue.AvailableCommands.Incomplete",
                    "available_commands",
                    "The two complete approved available actuator commands are required.");
            }

            if (available.Any(command =>
                    command.BoundedCommand != RrsFixtureV1.InitialActuatorState ||
                    command.LowerBound != RrsFixtureV1.LowerBound ||
                    command.UpperBound != RrsFixtureV1.UpperBound ||
                    command.RateLimitPerSecond != RrsFixtureV1.RateLimitPerSecond))
            {
                return Invalid(
                    "RrsQueue.AvailableCommands.Unapproved",
                    "available_commands",
                    "The supplied queue available commands must match the approved reference projection.");
            }

            RrsPendingCommandV1[] pending = pendingCommands.ToArray();
            if (pending.Length != 0)
            {
                return Invalid(
                    "RrsQueue.PendingCommands.NonEmpty",
                    "pending_commands",
                    "P6-T05 accepts only the explicitly supplied empty pending-command projection.");
            }

            StableId[] applied = appliedSourceEventIds.ToArray();
            StableId[] allocated = allocatedCommandIds.ToArray();
            if (applied.Length != 0 || allocated.Length != 0)
            {
                return Invalid(
                    "RrsQueue.Registries.NonEmpty",
                    "registries",
                    "P6-T05 accepts only the explicitly supplied empty queue registries.");
            }

            if (expectedQueueDigest == null)
            {
                return Invalid(
                    "RrsQueue.QueueDigest.Missing",
                    "queue_digest",
                    "The complete queue digest is required.");
            }

            Digest32 calculated = ComputeDigest(
                schemaVersion,
                queueId,
                ownerControllerId,
                generationCadenceOrNA,
                initialNextSequence,
                nextSequence,
                lastMotionTimeSeconds,
                available,
                pending,
                applied,
                allocated);
            if (!expectedQueueDigest.Equals(calculated))
            {
                return Invalid(
                    "RrsQueue.QueueDigest.Mismatch",
                    "queue_digest",
                    "The queue digest does not equal the complete supplied queue projection.");
            }

            return ContractValidationResult<RrsQueueStateV1>.Valid(
                new RrsQueueStateV1(
                    schemaVersion,
                    queueId,
                    ownerControllerId,
                    generationCadenceOrNA,
                    initialNextSequence,
                    nextSequence,
                    lastMotionTimeSeconds,
                    available,
                    pending,
                    applied,
                    allocated,
                    expectedQueueDigest));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId queueId,
            StableId ownerControllerId,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<RrsAvailableCommandV1> availableCommands,
            IEnumerable<RrsPendingCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds)
        {
            RrsAvailableCommandV1[] available = availableCommands
                .OrderBy(command => command.ActuatorId)
                .ToArray();
            RrsPendingCommandV1[] pending = pendingCommands.ToArray();
            StableId[] applied = appliedSourceEventIds.OrderBy(id => id).ToArray();
            StableId[] allocated = allocatedCommandIds.OrderBy(id => id).ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                BuildBytes(
                    schemaVersion,
                    queueId,
                    ownerControllerId,
                    generationCadenceOrNA,
                    initialNextSequence,
                    nextSequence,
                    lastMotionTimeSeconds,
                    available,
                    pending,
                    applied,
                    allocated,
                    null)));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                CurrentSchemaVersion,
                QueueId,
                OwnerControllerId,
                GenerationCadenceOrNA,
                InitialNextSequence,
                NextSequence,
                LastMotionTimeSeconds,
                AvailableCommands,
                PendingCommands,
                AppliedSourceEventIds,
                AllocatedCommandIds,
                QueueDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId queueId,
            StableId ownerControllerId,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<RrsAvailableCommandV1> availableCommands,
            IEnumerable<RrsPendingCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds,
            Digest32? queueDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, RrsFixtureV1.QueueSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, queueId);
                writer.Write((byte)1);
                Phase5CanonicalBytesV1.WriteStableId(writer, ownerControllerId);
                RrsValidationV1.WriteOptionalDouble(writer, generationCadenceOrNA);
                Phase5CanonicalBytesV1.WriteUInt64(writer, initialNextSequence);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nextSequence);
                Phase5CanonicalBytesV1.WriteDouble(writer, lastMotionTimeSeconds);

                RrsAvailableCommandV1[] available = availableCommands
                    .OrderBy(command => command.ActuatorId)
                    .ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)available.Length));
                foreach (RrsAvailableCommandV1 command in available)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                RrsPendingCommandV1[] pending = pendingCommands.ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)pending.Length));
                foreach (RrsPendingCommandV1 command in pending)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                StableId[] applied = appliedSourceEventIds.OrderBy(id => id).ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)applied.Length));
                foreach (StableId id in applied)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                StableId[] allocated = allocatedCommandIds.OrderBy(id => id).ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)allocated.Length));
                foreach (StableId id in allocated)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                if (queueDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, queueDigest);
                }
            });
        }

        private static ContractValidationResult<RrsQueueStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsQueueStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One controller actuator with approved synthetic gains, bounds, delay,
    /// and current/reference state. State motion is not applied by P6-T05.
    /// </summary>
    public sealed class RrsActuatorStateV1
    {
        private RrsActuatorStateV1(
            StableId actuatorId,
            double bias,
            double kpPerWatt,
            double kiPerWattSecond,
            double ktLeft,
            double ktRight,
            double ktiLeftPerSecond,
            double ktiRightPerSecond,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            double delaySeconds,
            double referenceState,
            double state,
            double command,
            double availableCommand)
        {
            ActuatorId = actuatorId;
            Bias = bias;
            KpPerWatt = kpPerWatt;
            KiPerWattSecond = kiPerWattSecond;
            KtLeft = ktLeft;
            KtRight = ktRight;
            KtiLeftPerSecond = ktiLeftPerSecond;
            KtiRightPerSecond = ktiRightPerSecond;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
            DelaySeconds = delaySeconds;
            ReferenceState = referenceState;
            State = state;
            Command = command;
            AvailableCommand = availableCommand;
        }

        public StableId ActuatorId { get; }

        public double Bias { get; }

        public double KpPerWatt { get; }

        public double KiPerWattSecond { get; }

        public double KtLeft { get; }

        public double KtRight { get; }

        public double KtiLeftPerSecond { get; }

        public double KtiRightPerSecond { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public double DelaySeconds { get; }

        public double ReferenceState { get; }

        public double State { get; }

        public double Command { get; }

        public double AvailableCommand { get; }

        public static ContractValidationResult<RrsActuatorStateV1> TryCreate(
            StableId actuatorId,
            double bias,
            double kpPerWatt,
            double kiPerWattSecond,
            double ktLeft,
            double ktRight,
            double ktiLeftPerSecond,
            double ktiRightPerSecond,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            double delaySeconds,
            double referenceState,
            double state,
            double command,
            double availableCommand)
        {
            if (!RrsValidationV1.IsKnownActuator(actuatorId))
            {
                return Invalid(
                    "RrsActuator.ActuatorId.Unapproved",
                    "actuator_id",
                    "Only the two owner-approved synthetic actuator identities are accepted.");
            }

            if (!RrsValidationV1.IsCanonicalSignedGain(bias) ||
                !RrsValidationV1.IsCanonicalSignedGain(kpPerWatt) ||
                !RrsValidationV1.IsCanonicalSignedGain(kiPerWattSecond) ||
                !RrsValidationV1.IsCanonicalSignedGain(ktLeft) ||
                !RrsValidationV1.IsCanonicalSignedGain(ktRight) ||
                !RrsValidationV1.IsCanonicalSignedGain(ktiLeftPerSecond) ||
                !RrsValidationV1.IsCanonicalSignedGain(ktiRightPerSecond))
            {
                return Invalid(
                    "RrsActuator.Gains.NonFinite",
                    "gains",
                    "All approved gain fields must be finite and free of signed zero.");
            }

            bool isTotalPower = actuatorId == RrsFixtureV1.TotalPowerActuatorId;
            bool gainsMatch = isTotalPower
                ? bias == RrsFixtureV1.TotalPowerBias &&
                  kpPerWatt == RrsFixtureV1.TotalPowerKpPerWatt &&
                  kiPerWattSecond == RrsFixtureV1.TotalPowerKiPerWattSecond &&
                  RrsValidationV1.IsZero(ktLeft) && RrsValidationV1.IsZero(ktRight) &&
                  RrsValidationV1.IsZero(ktiLeftPerSecond) && RrsValidationV1.IsZero(ktiRightPerSecond)
                : bias == RrsFixtureV1.TiltBias &&
                  RrsValidationV1.IsZero(kpPerWatt) &&
                  RrsValidationV1.IsZero(kiPerWattSecond) &&
                  ktLeft == RrsFixtureV1.TiltKtLeft &&
                  ktRight == RrsFixtureV1.TiltKtRight &&
                  ktiLeftPerSecond == RrsFixtureV1.TiltKtiLeftPerSecond &&
                  ktiRightPerSecond == RrsFixtureV1.TiltKtiRightPerSecond;
            if (!gainsMatch)
            {
                return Invalid(
                    "RrsActuator.Gains.Unapproved",
                    "gains",
                    "The supplied gains and bias must equal the approved synthetic fixture values.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(lowerBound) ||
                !RrsValidationV1.IsCanonicalFraction(upperBound) ||
                lowerBound != RrsFixtureV1.LowerBound ||
                upperBound != RrsFixtureV1.UpperBound)
            {
                return Invalid(
                    "RrsActuator.Bounds.Unapproved",
                    "bounds",
                    "The approved actuator bounds are exactly [0,1].");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                rateLimitPerSecond != RrsFixtureV1.RateLimitPerSecond ||
                !RrsValidationV1.IsCanonicalNonnegative(delaySeconds) ||
                delaySeconds != RrsFixtureV1.DelaySeconds)
            {
                return Invalid(
                    "RrsActuator.MotionLimits.Unapproved",
                    "motion_limits",
                    "The approved synthetic rate and delay are exactly 0.1 s^-1 and 0.5 s.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(referenceState) ||
                referenceState != RrsFixtureV1.InitialActuatorState ||
                !RrsValidationV1.IsCanonicalFraction(state) ||
                !RrsValidationV1.IsCanonicalFraction(command) ||
                !RrsValidationV1.IsCanonicalFraction(availableCommand))
            {
                return Invalid(
                    "RrsActuator.State.Invalid",
                    "state",
                    "Actuator reference, state, command, and available values must be canonical fractions.");
            }

            return ContractValidationResult<RrsActuatorStateV1>.Valid(
                new RrsActuatorStateV1(
                    actuatorId,
                    bias,
                    kpPerWatt,
                    kiPerWattSecond,
                    ktLeft,
                    ktRight,
                    ktiLeftPerSecond,
                    ktiRightPerSecond,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond,
                    delaySeconds,
                    referenceState,
                    state,
                    command,
                    availableCommand));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-ACTUATOR-STATE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteDouble(writer, Bias);
                Phase5CanonicalBytesV1.WriteDouble(writer, KpPerWatt);
                Phase5CanonicalBytesV1.WriteDouble(writer, KiPerWattSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, KtLeft);
                Phase5CanonicalBytesV1.WriteDouble(writer, KtRight);
                Phase5CanonicalBytesV1.WriteDouble(writer, KtiLeftPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, KtiRightPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, DelaySeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, ReferenceState);
                Phase5CanonicalBytesV1.WriteDouble(writer, State);
                Phase5CanonicalBytesV1.WriteDouble(writer, Command);
                Phase5CanonicalBytesV1.WriteDouble(writer, AvailableCommand);
            });
        }

        private static ContractValidationResult<RrsActuatorStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsActuatorStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Frozen complete measurement snapshot used by a pure controller
    /// projection. It carries no queue or actuator mutation.
    /// </summary>
    public sealed class RrsMeasurementSnapshotV1
    {
        private RrsMeasurementSnapshotV1(
            double snapshotTimeSeconds,
            double measuredPowerWatts,
            double leftPowerWatts,
            double rightPowerWatts,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            Digest32 regionSetDigest,
            Digest32 snapshotDigest)
        {
            SnapshotTimeSeconds = snapshotTimeSeconds;
            MeasuredPowerWatts = measuredPowerWatts;
            LeftPowerWatts = leftPowerWatts;
            RightPowerWatts = rightPowerWatts;
            LeftMeasuredFraction = leftMeasuredFraction;
            RightMeasuredFraction = rightMeasuredFraction;
            RegionSetDigest = regionSetDigest;
            SnapshotDigest = snapshotDigest;
        }

        public double SnapshotTimeSeconds { get; }

        public double MeasuredPowerWatts { get; }

        public double LeftPowerWatts { get; }

        public double RightPowerWatts { get; }

        public double LeftMeasuredFraction { get; }

        public double RightMeasuredFraction { get; }

        public Digest32 RegionSetDigest { get; }

        public Digest32 SnapshotDigest { get; }

        public static ContractValidationResult<RrsMeasurementSnapshotV1> TryCreate(
            RrsRegionSetV1? regionSet,
            double snapshotTimeSeconds,
            double measuredPowerWatts,
            double leftPowerWatts,
            double rightPowerWatts,
            Digest32? expectedSnapshotDigest = null)
        {
            if (regionSet == null)
            {
                return Invalid(
                    "RrsMeasurement.RegionSet.Missing",
                    "region_set",
                    "A complete approved region set is required for a measurement snapshot.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(snapshotTimeSeconds) ||
                !RrsValidationV1.IsCanonicalPositive(measuredPowerWatts) ||
                !RrsValidationV1.IsCanonicalNonnegative(leftPowerWatts) ||
                !RrsValidationV1.IsCanonicalNonnegative(rightPowerWatts))
            {
                return Invalid(
                    "RrsMeasurement.Values.Invalid",
                    "measurement",
                    "Measurement time and powers must be finite canonical SI values with positive total power.");
            }

            if (leftPowerWatts + rightPowerWatts != measuredPowerWatts)
            {
                return Invalid(
                    "RrsMeasurement.RegionPower.SumMismatch",
                    "region_power",
                    "The complete region powers must sum exactly to measured total power.");
            }

            double leftFraction = leftPowerWatts / measuredPowerWatts;
            double rightFraction = rightPowerWatts / measuredPowerWatts;
            if (!RrsValidationV1.IsCanonicalFraction(leftFraction) ||
                !RrsValidationV1.IsCanonicalFraction(rightFraction) ||
                leftFraction + rightFraction != 1.0)
            {
                return Invalid(
                    "RrsMeasurement.RegionFraction.Invalid",
                    "region_fraction",
                    "Measured region fractions must be finite, bounded, and sum exactly to one.");
            }

            Digest32 digest = ComputeDigest(
                snapshotTimeSeconds,
                measuredPowerWatts,
                leftPowerWatts,
                rightPowerWatts,
                leftFraction,
                rightFraction,
                regionSet.RegionSetDigest);
            if (expectedSnapshotDigest != null && !expectedSnapshotDigest.Equals(digest))
            {
                return Invalid(
                    "RrsMeasurement.SnapshotDigest.Mismatch",
                    "snapshot_digest",
                    "The supplied measurement digest does not equal the snapshot fields.");
            }

            return ContractValidationResult<RrsMeasurementSnapshotV1>.Valid(
                new RrsMeasurementSnapshotV1(
                    snapshotTimeSeconds,
                    measuredPowerWatts,
                    leftPowerWatts,
                    rightPowerWatts,
                    leftFraction,
                    rightFraction,
                    regionSet.RegionSetDigest,
                    digest));
        }

        public static Digest32 ComputeDigest(
            double snapshotTimeSeconds,
            double measuredPowerWatts,
            double leftPowerWatts,
            double rightPowerWatts,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            Digest32 regionSetDigest)
        {
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-MEASUREMENT-SNAPSHOT-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                    Phase5CanonicalBytesV1.WriteDouble(writer, snapshotTimeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, measuredPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, leftPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, rightPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, leftMeasuredFraction);
                    Phase5CanonicalBytesV1.WriteDouble(writer, rightMeasuredFraction);
                    Phase5CanonicalBytesV1.WriteDigest(writer, regionSetDigest);
                })));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-MEASUREMENT-SNAPSHOT-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteDouble(writer, SnapshotTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, MeasuredPowerWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, LeftPowerWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, RightPowerWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, LeftMeasuredFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, RightMeasuredFraction);
                Phase5CanonicalBytesV1.WriteDigest(writer, RegionSetDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, SnapshotDigest);
            });
        }

        private static ContractValidationResult<RrsMeasurementSnapshotV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsMeasurementSnapshotV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Immutable controller-owned state. Queue state is complete and bound but
    /// is never changed by this task.
    /// </summary>
    public sealed class RrsControllerStateV1
    {
        private RrsControllerStateV1(
            uint schemaVersion,
            StableId controllerId,
            RrsModeV1 mode,
            double powerSetpointWatts,
            double measuredPowerWatts,
            double powerErrorWatts,
            double integralErrorWattSeconds,
            RrsRegionSetV1 regionSet,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralSeconds,
            double rightTiltIntegralSeconds,
            double automaticCadenceSeconds,
            double lastUpdateTimeSeconds,
            IEnumerable<RrsActuatorStateV1> actuators,
            StableId influenceMapId,
            string mapVersion,
            Digest32 influenceMapDigest,
            StableId queueId,
            RrsControlPolarityV1 controlPolarity,
            string signCertificateId,
            string normalization,
            Digest32 feedbackReferenceStateDigest,
            RrsQueueStateV1 queueState,
            Digest32 stateDigest)
        {
            SchemaVersion = schemaVersion;
            ControllerId = controllerId;
            Mode = mode;
            PowerSetpointWatts = powerSetpointWatts;
            MeasuredPowerWatts = measuredPowerWatts;
            PowerErrorWatts = powerErrorWatts;
            IntegralErrorWattSeconds = integralErrorWattSeconds;
            RegionSet = regionSet;
            LeftMeasuredFraction = leftMeasuredFraction;
            RightMeasuredFraction = rightMeasuredFraction;
            LeftTiltError = leftTiltError;
            RightTiltError = rightTiltError;
            LeftTiltIntegralSeconds = leftTiltIntegralSeconds;
            RightTiltIntegralSeconds = rightTiltIntegralSeconds;
            AutomaticCadenceSeconds = automaticCadenceSeconds;
            LastUpdateTimeSeconds = lastUpdateTimeSeconds;
            Actuators = new ReadOnlyCollection<RrsActuatorStateV1>(
                actuators.OrderBy(actuator => actuator.ActuatorId).ToArray());
            InfluenceMapId = influenceMapId;
            MapVersion = mapVersion;
            InfluenceMapDigest = influenceMapDigest;
            QueueId = queueId;
            ControlPolarity = controlPolarity;
            SignCertificateId = signCertificateId;
            Normalization = normalization;
            FeedbackReferenceStateDigest = feedbackReferenceStateDigest;
            QueueState = queueState;
            StateDigest = stateDigest;
        }

        public uint SchemaVersion { get; }

        public StableId ControllerId { get; }

        public RrsModeV1 Mode { get; }

        public double PowerSetpointWatts { get; }

        public double MeasuredPowerWatts { get; }

        public double PowerErrorWatts { get; }

        public double IntegralErrorWattSeconds { get; }

        public RrsRegionSetV1 RegionSet { get; }

        public double LeftMeasuredFraction { get; }

        public double RightMeasuredFraction { get; }

        public double LeftTiltError { get; }

        public double RightTiltError { get; }

        public double LeftTiltIntegralSeconds { get; }

        public double RightTiltIntegralSeconds { get; }

        public double AutomaticCadenceSeconds { get; }

        public double LastUpdateTimeSeconds { get; }

        public IReadOnlyList<RrsActuatorStateV1> Actuators { get; }

        public StableId InfluenceMapId { get; }

        public string MapVersion { get; }

        public Digest32 InfluenceMapDigest { get; }

        public StableId QueueId { get; }

        public RrsControlPolarityV1 ControlPolarity { get; }

        public string SignCertificateId { get; }

        public string Normalization { get; }

        public Digest32 FeedbackReferenceStateDigest { get; }

        public RrsQueueStateV1 QueueState { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<RrsControllerStateV1> TryCreate(
            uint schemaVersion,
            StableId controllerId,
            RrsModeV1 mode,
            double powerSetpointWatts,
            double measuredPowerWatts,
            double powerErrorWatts,
            double integralErrorWattSeconds,
            RrsRegionSetV1? regionSet,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralSeconds,
            double rightTiltIntegralSeconds,
            double automaticCadenceSeconds,
            double lastUpdateTimeSeconds,
            IEnumerable<RrsActuatorStateV1>? actuators,
            StableId influenceMapId,
            string? mapVersion,
            Digest32? influenceMapDigest,
            StableId queueId,
            RrsControlPolarityV1 controlPolarity,
            string? signCertificateId,
            string? normalization,
            Digest32? feedbackReferenceStateDigest,
            RrsQueueStateV1? queueState,
            Digest32? expectedStateDigest)
        {
            if (schemaVersion != 1)
            {
                return Invalid(
                    "RrsController.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic RRS controller schema version 1 is accepted.");
            }

            if (controllerId != RrsFixtureV1.ControllerId ||
                influenceMapId != RrsFixtureV1.MapId ||
                queueId != RrsFixtureV1.QueueId)
            {
                return Invalid(
                    "RrsController.Identity.Unapproved",
                    "identity",
                    "The approved controller, influence-map, and queue identities are required.");
            }

            if ((byte)mode > (byte)RrsModeV1.Held ||
                controlPolarity != RrsControlPolarityV1.NegativeFeedback)
            {
                return Invalid(
                    "RrsController.ModeOrPolarity.Unsupported",
                    "mode",
                    "Only Manual, Automatic, Held, and NegativeFeedback are approved.");
            }

            if (!RrsValidationV1.IsCanonicalPositive(powerSetpointWatts) ||
                powerSetpointWatts != RrsFixtureV1.PowerSetpointWatts ||
                !RrsValidationV1.IsCanonicalPositive(measuredPowerWatts) ||
                !RrsValidationV1.IsCanonicalFinite(powerErrorWatts) ||
                powerErrorWatts != powerSetpointWatts - measuredPowerWatts ||
                !RrsValidationV1.IsCanonicalFinite(integralErrorWattSeconds) ||
                !RrsValidationV1.IsCanonicalFinite(leftTiltError) ||
                !RrsValidationV1.IsCanonicalFinite(rightTiltError) ||
                !RrsValidationV1.IsCanonicalFinite(leftTiltIntegralSeconds) ||
                !RrsValidationV1.IsCanonicalFinite(rightTiltIntegralSeconds))
            {
                return Invalid(
                    "RrsController.PowerState.Invalid",
                    "power_state",
                    "Power setpoint, measured power, errors, and integrals must satisfy the approved units and equations.");
            }

            if (regionSet == null)
            {
                return Invalid(
                    "RrsController.RegionSet.Missing",
                    "region_set",
                    "The complete approved region set is required.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(leftMeasuredFraction) ||
                !RrsValidationV1.IsCanonicalFraction(rightMeasuredFraction) ||
                leftMeasuredFraction + rightMeasuredFraction != 1.0 ||
                leftMeasuredFraction != RrsFixtureV1.TargetFraction ||
                rightMeasuredFraction != RrsFixtureV1.TargetFraction ||
                leftTiltError != RrsFixtureV1.TargetFraction - leftMeasuredFraction ||
                rightTiltError != RrsFixtureV1.TargetFraction - rightMeasuredFraction)
            {
                return Invalid(
                    "RrsController.RegionState.Unapproved",
                    "region_state",
                    "The approved initial controller state requires measured fractions and tilt errors of 0.5/0.5 and 0/0.");
            }

            if (!RrsValidationV1.IsCanonicalPositive(automaticCadenceSeconds) ||
                automaticCadenceSeconds != RrsFixtureV1.AutomaticCadenceSeconds ||
                !RrsValidationV1.IsCanonicalNonnegative(lastUpdateTimeSeconds))
            {
                return Invalid(
                    "RrsController.Cadence.Invalid",
                    "cadence",
                    "The approved automatic cadence is exactly 1.0 s and update time is nonnegative.");
            }

            if (actuators == null)
            {
                return Invalid(
                    "RrsController.Actuators.Missing",
                    "actuators",
                    "Both approved actuator gain/state records are required.");
            }

            RrsActuatorStateV1[] canonicalActuators = actuators
                .OrderBy(actuator => actuator.ActuatorId)
                .ToArray();
            if (canonicalActuators.Length != 2 ||
                canonicalActuators[0].ActuatorId != RrsFixtureV1.TotalPowerActuatorId ||
                canonicalActuators[1].ActuatorId != RrsFixtureV1.TiltActuatorId)
            {
                return Invalid(
                    "RrsController.Actuators.Incomplete",
                    "actuators",
                    "The complete total-power and tilt actuator state is required.");
            }

            if (canonicalActuators.Any(actuator =>
                    actuator.ReferenceState != RrsFixtureV1.InitialActuatorState ||
                    actuator.State != RrsFixtureV1.InitialActuatorState ||
                    actuator.Command != RrsFixtureV1.InitialActuatorState ||
                    actuator.AvailableCommand != RrsFixtureV1.InitialActuatorState))
            {
                return Invalid(
                    "RrsController.Actuators.ReferenceState.Unapproved",
                    "actuators",
                    "The approved controller pre-state requires all actuator reference/state/command/available values to equal 0.5.");
            }

            if (!string.Equals(mapVersion, RrsFixtureV1.ApprovedMapVersion, StringComparison.Ordinal) ||
                !string.Equals(signCertificateId, RrsFixtureV1.SignCertificate, StringComparison.Ordinal) ||
                !string.Equals(normalization, RrsFixtureV1.Normalization, StringComparison.Ordinal) ||
                influenceMapDigest == null || feedbackReferenceStateDigest == null || queueState == null)
            {
                return Invalid(
                    "RrsController.Binding.MissingOrUnapproved",
                    "binding",
                    "Map version, sign certificate, normalization, map digest, reference digest, and queue state are required.");
            }

            if (queueState.OwnerControllerId != controllerId || queueState.QueueId != queueId ||
                queueState.GenerationCadenceOrNA != automaticCadenceSeconds ||
                !queueState.QueueDigest.Equals(RrsFixtureV1.ApprovedQueueDigest))
            {
                return Invalid(
                    "RrsController.Queue.BindingMismatch",
                    "queue_state",
                    "The complete supplied queue must bind to this controller and cadence.");
            }

            if (!regionSet.RegionSetDigest.Equals(RrsFixtureV1.ApprovedRegionSetDigest) ||
                !regionSet.TopologyDigest.Equals(RrsFixtureV1.ApprovedTopologyDigest) ||
                !influenceMapDigest.Equals(RrsFixtureV1.ApprovedMapDigest) ||
                !feedbackReferenceStateDigest.Equals(RrsFixtureV1.ApprovedReferenceStateDigest))
            {
                return Invalid(
                    "RrsController.BindingDigest.Unapproved",
                    "binding_digest",
                    "The controller must bind the owner-approved topology, region-set, map, reference, and queue digests.");
            }

            if (expectedStateDigest == null)
            {
                return Invalid(
                    "RrsController.StateDigest.Missing",
                    "state_digest",
                    "The complete controller state digest is required.");
            }

            Digest32 calculatedState = ComputeDigest(
                schemaVersion,
                controllerId,
                mode,
                powerSetpointWatts,
                measuredPowerWatts,
                powerErrorWatts,
                integralErrorWattSeconds,
                regionSet,
                leftMeasuredFraction,
                rightMeasuredFraction,
                leftTiltError,
                rightTiltError,
                leftTiltIntegralSeconds,
                rightTiltIntegralSeconds,
                automaticCadenceSeconds,
                lastUpdateTimeSeconds,
                canonicalActuators,
                influenceMapId,
                mapVersion!,
                influenceMapDigest,
                queueId,
                controlPolarity,
                signCertificateId!,
                normalization!,
                feedbackReferenceStateDigest,
                queueState);
            if (!expectedStateDigest.Equals(calculatedState))
            {
                return Invalid(
                    "RrsController.StateDigest.Mismatch",
                    "state_digest",
                    "The controller state digest does not equal the complete state fields.");
            }

            return ContractValidationResult<RrsControllerStateV1>.Valid(
                new RrsControllerStateV1(
                    schemaVersion,
                    controllerId,
                    mode,
                    powerSetpointWatts,
                    measuredPowerWatts,
                    powerErrorWatts,
                    integralErrorWattSeconds,
                    regionSet,
                    leftMeasuredFraction,
                    rightMeasuredFraction,
                    leftTiltError,
                    rightTiltError,
                    leftTiltIntegralSeconds,
                    rightTiltIntegralSeconds,
                    automaticCadenceSeconds,
                    lastUpdateTimeSeconds,
                    canonicalActuators,
                    influenceMapId,
                    mapVersion!,
                    influenceMapDigest,
                    queueId,
                    controlPolarity,
                    signCertificateId!,
                    normalization!,
                    feedbackReferenceStateDigest,
                    queueState,
                    expectedStateDigest));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId controllerId,
            RrsModeV1 mode,
            double powerSetpointWatts,
            double measuredPowerWatts,
            double powerErrorWatts,
            double integralErrorWattSeconds,
            RrsRegionSetV1 regionSet,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralSeconds,
            double rightTiltIntegralSeconds,
            double automaticCadenceSeconds,
            double lastUpdateTimeSeconds,
            IEnumerable<RrsActuatorStateV1> actuators,
            StableId influenceMapId,
            string mapVersion,
            Digest32 influenceMapDigest,
            StableId queueId,
            RrsControlPolarityV1 controlPolarity,
            string signCertificateId,
            string normalization,
            Digest32 feedbackReferenceStateDigest,
            RrsQueueStateV1 queueState)
        {
            RrsActuatorStateV1[] canonicalActuators = actuators
                .OrderBy(actuator => actuator.ActuatorId)
                .ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                BuildBytes(
                    schemaVersion,
                    controllerId,
                    mode,
                    powerSetpointWatts,
                    measuredPowerWatts,
                    powerErrorWatts,
                    integralErrorWattSeconds,
                    regionSet,
                    leftMeasuredFraction,
                    rightMeasuredFraction,
                    leftTiltError,
                    rightTiltError,
                    leftTiltIntegralSeconds,
                    rightTiltIntegralSeconds,
                    automaticCadenceSeconds,
                    lastUpdateTimeSeconds,
                    canonicalActuators,
                    influenceMapId,
                    mapVersion,
                    influenceMapDigest,
                    queueId,
                    controlPolarity,
                    signCertificateId,
                    normalization,
                    feedbackReferenceStateDigest,
                    queueState,
                    null)));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                ControllerId,
                Mode,
                PowerSetpointWatts,
                MeasuredPowerWatts,
                PowerErrorWatts,
                IntegralErrorWattSeconds,
                RegionSet,
                LeftMeasuredFraction,
                RightMeasuredFraction,
                LeftTiltError,
                RightTiltError,
                LeftTiltIntegralSeconds,
                RightTiltIntegralSeconds,
                AutomaticCadenceSeconds,
                LastUpdateTimeSeconds,
                Actuators,
                InfluenceMapId,
                MapVersion,
                InfluenceMapDigest,
                QueueId,
                ControlPolarity,
                SignCertificateId,
                Normalization,
                FeedbackReferenceStateDigest,
                QueueState,
                StateDigest);
        }

        public RrsActuatorStateV1 GetActuator(StableId actuatorId)
        {
            RrsActuatorStateV1? actuator = Actuators.FirstOrDefault(
                candidate => candidate.ActuatorId == actuatorId);
            if (actuator == null)
            {
                throw new InvalidOperationException("The requested approved RRS actuator is not present.");
            }

            return actuator;
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId controllerId,
            RrsModeV1 mode,
            double powerSetpointWatts,
            double measuredPowerWatts,
            double powerErrorWatts,
            double integralErrorWattSeconds,
            RrsRegionSetV1 regionSet,
            double leftMeasuredFraction,
            double rightMeasuredFraction,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralSeconds,
            double rightTiltIntegralSeconds,
            double automaticCadenceSeconds,
            double lastUpdateTimeSeconds,
            IEnumerable<RrsActuatorStateV1> actuators,
            StableId influenceMapId,
            string mapVersion,
            Digest32 influenceMapDigest,
            StableId queueId,
            RrsControlPolarityV1 controlPolarity,
            string signCertificateId,
            string normalization,
            Digest32 feedbackReferenceStateDigest,
            RrsQueueStateV1 queueState,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, RrsFixtureV1.ControllerSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, controllerId);
                writer.Write((byte)mode);
                Phase5CanonicalBytesV1.WriteDouble(writer, powerSetpointWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, measuredPowerWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, powerErrorWatts);
                Phase5CanonicalBytesV1.WriteDouble(writer, integralErrorWattSeconds);
                Phase5CanonicalBytesV1.WriteBytes(writer, regionSet.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteDouble(writer, leftMeasuredFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, rightMeasuredFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, leftTiltError);
                Phase5CanonicalBytesV1.WriteDouble(writer, rightTiltError);
                Phase5CanonicalBytesV1.WriteDouble(writer, leftTiltIntegralSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, rightTiltIntegralSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, automaticCadenceSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, lastUpdateTimeSeconds);

                RrsActuatorStateV1[] canonicalActuators = actuators
                    .OrderBy(actuator => actuator.ActuatorId)
                    .ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonicalActuators.Length));
                foreach (RrsActuatorStateV1 actuator in canonicalActuators)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, actuator.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                Phase5CanonicalBytesV1.WriteString(writer, mapVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, influenceMapDigest);
                Phase5CanonicalBytesV1.WriteStableId(writer, queueId);
                writer.Write((byte)controlPolarity);
                Phase5CanonicalBytesV1.WriteString(writer, signCertificateId);
                Phase5CanonicalBytesV1.WriteString(writer, normalization);
                Phase5CanonicalBytesV1.WriteDigest(writer, feedbackReferenceStateDigest);
                Phase5CanonicalBytesV1.WriteBytes(writer, queueState.ToCanonicalBytes());
                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static ContractValidationResult<RrsControllerStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsControllerStateV1>.Invalid(code, path, message);
        }
    }

    public sealed class RrsCommandProjectionV1
    {
        private RrsCommandProjectionV1(
            StableId actuatorId,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            double delaySeconds,
            Digest32 commandDigest)
        {
            ActuatorId = actuatorId;
            RequestedCommand = requestedCommand;
            BoundedCommand = boundedCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
            DelaySeconds = delaySeconds;
            CommandDigest = commandDigest;
        }

        public StableId ActuatorId { get; }

        public double RequestedCommand { get; }

        public double BoundedCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public double DelaySeconds { get; }

        public Digest32 CommandDigest { get; }

        internal static RrsCommandProjectionV1 Create(
            RrsActuatorStateV1 actuator,
            double requestedCommand)
        {
            double bounded = Math.Min(
                actuator.UpperBound,
                Math.Max(actuator.LowerBound, requestedCommand));
            Digest32 digest = new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-COMMAND-PROJECTION-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                    Phase5CanonicalBytesV1.WriteStableId(writer, actuator.ActuatorId);
                    Phase5CanonicalBytesV1.WriteDouble(writer, requestedCommand);
                    Phase5CanonicalBytesV1.WriteDouble(writer, bounded);
                    Phase5CanonicalBytesV1.WriteDouble(writer, actuator.LowerBound);
                    Phase5CanonicalBytesV1.WriteDouble(writer, actuator.UpperBound);
                    Phase5CanonicalBytesV1.WriteDouble(writer, actuator.RateLimitPerSecond);
                    Phase5CanonicalBytesV1.WriteDouble(writer, actuator.DelaySeconds);
                })));
            return new RrsCommandProjectionV1(
                actuator.ActuatorId,
                requestedCommand,
                bounded,
                actuator.LowerBound,
                actuator.UpperBound,
                actuator.RateLimitPerSecond,
                actuator.DelaySeconds,
                digest);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-COMMAND-PROJECTION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteDouble(writer, RequestedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, DelaySeconds);
                Phase5CanonicalBytesV1.WriteDigest(writer, CommandDigest);
            });
        }
    }

    /// <summary>
    /// Pure automatic, manual, and held controller projection. It reports the
    /// command that would be handed to the future P6-T06 queue; it never writes
    /// to controller, actuator, or queue state.
    /// </summary>
    public sealed class RrsControllerProjectionV1
    {
        private RrsControllerProjectionV1(
            RrsModeV1 mode,
            double currentTimeSeconds,
            double deltaTimeSeconds,
            double powerErrorWatts,
            double integralBeforeWattSeconds,
            double integralAfterWattSeconds,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralBeforeSeconds,
            double rightTiltIntegralBeforeSeconds,
            double leftTiltIntegralAfterSeconds,
            double rightTiltIntegralAfterSeconds,
            double lastUpdateBeforeSeconds,
            double lastUpdateAfterSeconds,
            IEnumerable<RrsCommandProjectionV1> commands,
            Digest32 sourceStateDigest,
            Digest32 measurementDigest,
            Digest32 projectionDigest)
        {
            Mode = mode;
            CurrentTimeSeconds = currentTimeSeconds;
            DeltaTimeSeconds = deltaTimeSeconds;
            PowerErrorWatts = powerErrorWatts;
            IntegralBeforeWattSeconds = integralBeforeWattSeconds;
            IntegralAfterWattSeconds = integralAfterWattSeconds;
            LeftTiltError = leftTiltError;
            RightTiltError = rightTiltError;
            LeftTiltIntegralBeforeSeconds = leftTiltIntegralBeforeSeconds;
            RightTiltIntegralBeforeSeconds = rightTiltIntegralBeforeSeconds;
            LeftTiltIntegralAfterSeconds = leftTiltIntegralAfterSeconds;
            RightTiltIntegralAfterSeconds = rightTiltIntegralAfterSeconds;
            LastUpdateBeforeSeconds = lastUpdateBeforeSeconds;
            LastUpdateAfterSeconds = lastUpdateAfterSeconds;
            Commands = new ReadOnlyCollection<RrsCommandProjectionV1>(commands.ToArray());
            SourceStateDigest = sourceStateDigest;
            MeasurementDigest = measurementDigest;
            ProjectionDigest = projectionDigest;
        }

        public RrsModeV1 Mode { get; }

        public double CurrentTimeSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public double PowerErrorWatts { get; }

        public double IntegralBeforeWattSeconds { get; }

        public double IntegralAfterWattSeconds { get; }

        public double LeftTiltError { get; }

        public double RightTiltError { get; }

        public double LeftTiltIntegralBeforeSeconds { get; }

        public double RightTiltIntegralBeforeSeconds { get; }

        public double LeftTiltIntegralAfterSeconds { get; }

        public double RightTiltIntegralAfterSeconds { get; }

        public double LastUpdateBeforeSeconds { get; }

        public double LastUpdateAfterSeconds { get; }

        public IReadOnlyList<RrsCommandProjectionV1> Commands { get; }

        public Digest32 SourceStateDigest { get; }

        public Digest32 MeasurementDigest { get; }

        public Digest32 ProjectionDigest { get; }

        public static ContractValidationResult<RrsControllerProjectionV1> TryProjectAutomatic(
            RrsControllerStateV1? state,
            RrsMeasurementSnapshotV1? measurement,
            double currentTimeSeconds)
        {
            if (state == null || measurement == null)
            {
                return Invalid(
                    "RrsProjection.Input.Missing",
                    "input",
                    "A validated controller, complete queue, and frozen measurement are required.");
            }

            if (state.Mode != RrsModeV1.Automatic)
            {
                return Invalid(
                    "RrsProjection.Mode.NotAutomatic",
                    "mode",
                    "Automatic projection requires an explicit Automatic pre-state.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(currentTimeSeconds) ||
                currentTimeSeconds <= state.LastUpdateTimeSeconds)
            {
                return Invalid(
                    "RrsProjection.Time.Invalid",
                    "current_time_s",
                    "Automatic projection requires a later finite nonnegative time.");
            }

            double delta = currentTimeSeconds - state.LastUpdateTimeSeconds;
            if (delta != state.AutomaticCadenceSeconds)
            {
                return Invalid(
                    "RrsProjection.Cadence.Mismatch",
                    "delta_t_s",
                    "Automatic projection requires exactly the declared RRS cadence.");
            }

            if (!measurement.RegionSetDigest.Equals(state.RegionSet.RegionSetDigest) ||
                measurement.SnapshotTimeSeconds != currentTimeSeconds)
            {
                return Invalid(
                    "RrsProjection.Measurement.BindingMismatch",
                    "measurement",
                    "The measurement snapshot must bind to the complete region set and projection time.");
            }

            double error = state.PowerSetpointWatts - measurement.MeasuredPowerWatts;
            double integralAfter = state.IntegralErrorWattSeconds + error * delta;
            double leftTiltError = RrsFixtureV1.TargetFraction - measurement.LeftMeasuredFraction;
            double rightTiltError = RrsFixtureV1.TargetFraction - measurement.RightMeasuredFraction;
            double leftTiltIntegralAfter = state.LeftTiltIntegralSeconds + leftTiltError * delta;
            double rightTiltIntegralAfter = state.RightTiltIntegralSeconds + rightTiltError * delta;
            if (!RrsValidationV1.IsCanonicalFinite(error) ||
                !RrsValidationV1.IsCanonicalFinite(integralAfter) ||
                !RrsValidationV1.IsCanonicalFinite(leftTiltError) ||
                !RrsValidationV1.IsCanonicalFinite(rightTiltError) ||
                !RrsValidationV1.IsCanonicalFinite(leftTiltIntegralAfter) ||
                !RrsValidationV1.IsCanonicalFinite(rightTiltIntegralAfter))
            {
                return Invalid(
                    "RrsProjection.Result.NonFinite",
                    "projection",
                    "Controller equations must produce finite canonical values.");
            }

            RrsActuatorStateV1 total = state.GetActuator(RrsFixtureV1.TotalPowerActuatorId);
            RrsActuatorStateV1 tilt = state.GetActuator(RrsFixtureV1.TiltActuatorId);
            double totalRequested = total.Bias +
                total.KpPerWatt * error +
                total.KiPerWattSecond * integralAfter +
                total.KtLeft * leftTiltError +
                total.KtRight * rightTiltError +
                total.KtiLeftPerSecond * leftTiltIntegralAfter +
                total.KtiRightPerSecond * rightTiltIntegralAfter;
            double tiltRequested = tilt.Bias +
                tilt.KpPerWatt * error +
                tilt.KiPerWattSecond * integralAfter +
                tilt.KtLeft * leftTiltError +
                tilt.KtRight * rightTiltError +
                tilt.KtiLeftPerSecond * leftTiltIntegralAfter +
                tilt.KtiRightPerSecond * rightTiltIntegralAfter;
            if (!RrsValidationV1.IsCanonicalFinite(totalRequested) ||
                !RrsValidationV1.IsCanonicalFinite(tiltRequested))
            {
                return Invalid(
                    "RrsProjection.Command.NonFinite",
                    "commands",
                    "Controller command equations must produce finite values.");
            }

            RrsCommandProjectionV1[] commands =
            {
                RrsCommandProjectionV1.Create(total, totalRequested),
                RrsCommandProjectionV1.Create(tilt, tiltRequested)
            };
            return Valid(
                state,
                measurement.SnapshotDigest,
                RrsModeV1.Automatic,
                currentTimeSeconds,
                delta,
                error,
                state.IntegralErrorWattSeconds,
                integralAfter,
                leftTiltError,
                rightTiltError,
                state.LeftTiltIntegralSeconds,
                state.RightTiltIntegralSeconds,
                leftTiltIntegralAfter,
                rightTiltIntegralAfter,
                state.LastUpdateTimeSeconds,
                currentTimeSeconds,
                commands);
        }

        public static ContractValidationResult<RrsControllerProjectionV1> TryProjectManual(
            RrsControllerStateV1? state,
            StableId actuatorId,
            double requestedCommand,
            double currentTimeSeconds)
        {
            if (state == null)
            {
                return Invalid(
                    "RrsProjection.Input.Missing",
                    "state",
                    "A validated Manual controller state with a complete queue is required.");
            }

            if (state.Mode != RrsModeV1.Manual)
            {
                return Invalid(
                    "RrsProjection.Mode.NotManual",
                    "mode",
                    "Manual projection requires an explicit Manual pre-state.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(currentTimeSeconds) ||
                !RrsValidationV1.IsCanonicalFinite(requestedCommand))
            {
                return Invalid(
                    "RrsProjection.Manual.Input.Invalid",
                    "manual",
                    "Manual projection time and command must be finite canonical values.");
            }

            if (!RrsValidationV1.IsKnownActuator(actuatorId))
            {
                return Invalid(
                    "RrsProjection.Manual.Actuator.Unapproved",
                    "actuator_id",
                    "Manual projection requires one of the two approved actuator identities.");
            }

            RrsActuatorStateV1 actuator = state.GetActuator(actuatorId);
            if (requestedCommand < actuator.LowerBound || requestedCommand > actuator.UpperBound)
            {
                return Invalid(
                    "RrsProjection.Manual.Command.OutOfBounds",
                    "requested_command",
                    "Manual command must be within the explicit actuator bounds.");
            }

            RrsCommandProjectionV1[] commands =
            {
                RrsCommandProjectionV1.Create(actuator, requestedCommand)
            };
            return Valid(
                state,
                state.StateDigest,
                RrsModeV1.Manual,
                currentTimeSeconds,
                0.0,
                state.PowerErrorWatts,
                state.IntegralErrorWattSeconds,
                state.IntegralErrorWattSeconds,
                state.LeftTiltError,
                state.RightTiltError,
                state.LeftTiltIntegralSeconds,
                state.RightTiltIntegralSeconds,
                state.LeftTiltIntegralSeconds,
                state.RightTiltIntegralSeconds,
                state.LastUpdateTimeSeconds,
                state.LastUpdateTimeSeconds,
                commands);
        }

        public static ContractValidationResult<RrsControllerProjectionV1> TryProjectHeld(
            RrsControllerStateV1? state,
            double currentTimeSeconds)
        {
            if (state == null)
            {
                return Invalid(
                    "RrsProjection.Input.Missing",
                    "state",
                    "A validated Held controller state with a complete queue is required.");
            }

            if (state.Mode != RrsModeV1.Held)
            {
                return Invalid(
                    "RrsProjection.Mode.NotHeld",
                    "mode",
                    "Held projection requires an explicit Held pre-state.");
            }

            if (!RrsValidationV1.IsCanonicalNonnegative(currentTimeSeconds))
            {
                return Invalid(
                    "RrsProjection.Held.Time.Invalid",
                    "current_time_s",
                    "Held projection time must be finite and nonnegative.");
            }

            RrsCommandProjectionV1[] commands = state.Actuators
                .Select(actuator => RrsCommandProjectionV1.Create(actuator, actuator.Command))
                .ToArray();
            return Valid(
                state,
                state.StateDigest,
                RrsModeV1.Held,
                currentTimeSeconds,
                0.0,
                state.PowerErrorWatts,
                state.IntegralErrorWattSeconds,
                state.IntegralErrorWattSeconds,
                state.LeftTiltError,
                state.RightTiltError,
                state.LeftTiltIntegralSeconds,
                state.RightTiltIntegralSeconds,
                state.LeftTiltIntegralSeconds,
                state.RightTiltIntegralSeconds,
                state.LastUpdateTimeSeconds,
                state.LastUpdateTimeSeconds,
                commands);
        }

        private static ContractValidationResult<RrsControllerProjectionV1> Valid(
            RrsControllerStateV1 state,
            Digest32 measurementDigest,
            RrsModeV1 mode,
            double currentTimeSeconds,
            double deltaTimeSeconds,
            double powerErrorWatts,
            double integralBeforeWattSeconds,
            double integralAfterWattSeconds,
            double leftTiltError,
            double rightTiltError,
            double leftTiltIntegralBeforeSeconds,
            double rightTiltIntegralBeforeSeconds,
            double leftTiltIntegralAfterSeconds,
            double rightTiltIntegralAfterSeconds,
            double lastUpdateBeforeSeconds,
            double lastUpdateAfterSeconds,
            RrsCommandProjectionV1[] commands)
        {
            Digest32 digest = new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-CONTROLLER-PROJECTION-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                    Phase5CanonicalBytesV1.WriteDigest(writer, state.StateDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, measurementDigest);
                    writer.Write((byte)mode);
                    Phase5CanonicalBytesV1.WriteDouble(writer, currentTimeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, deltaTimeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, powerErrorWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, integralBeforeWattSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, integralAfterWattSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, leftTiltError);
                    Phase5CanonicalBytesV1.WriteDouble(writer, rightTiltError);
                    Phase5CanonicalBytesV1.WriteDouble(writer, leftTiltIntegralBeforeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, rightTiltIntegralBeforeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, leftTiltIntegralAfterSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, rightTiltIntegralAfterSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, lastUpdateBeforeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, lastUpdateAfterSeconds);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)commands.Length));
                    foreach (RrsCommandProjectionV1 command in commands.OrderBy(command => command.ActuatorId))
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                    }
                })));

            return ContractValidationResult<RrsControllerProjectionV1>.Valid(
                new RrsControllerProjectionV1(
                    mode,
                    currentTimeSeconds,
                    deltaTimeSeconds,
                    powerErrorWatts,
                    integralBeforeWattSeconds,
                    integralAfterWattSeconds,
                    leftTiltError,
                    rightTiltError,
                    leftTiltIntegralBeforeSeconds,
                    rightTiltIntegralBeforeSeconds,
                    leftTiltIntegralAfterSeconds,
                    rightTiltIntegralAfterSeconds,
                    lastUpdateBeforeSeconds,
                    lastUpdateAfterSeconds,
                    commands,
                    state.StateDigest,
                    measurementDigest,
                    digest));
        }

        private static ContractValidationResult<RrsControllerProjectionV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsControllerProjectionV1>.Invalid(code, path, message);
        }
    }
}
