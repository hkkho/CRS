using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit sparse liquid-zone influence entry. The P6-T02 admitted
    /// fixture uses a dimensionless fill source and a local m^-1 absorption
    /// target. The target is a NodeKey, never an inferred array position.
    /// </summary>
    public sealed class LiquidZoneInfluenceMapEntryV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneInfluenceMapEntryV1(
            uint logicalZoneId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerFillFraction,
            string sourceUnit,
            string targetUnit)
        {
            LogicalZoneId = logicalZoneId;
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            WeightMInversePerFillFraction = weightMInversePerFillFraction;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
        }

        public uint LogicalZoneId { get; }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double WeightMInversePerFillFraction { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public static ContractValidationResult<LiquidZoneInfluenceMapEntryV1> TryCreate(
            uint logicalZoneId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerFillFraction,
            string? sourceUnit,
            string? targetUnit)
        {
            if (logicalZoneId >= LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneInfluenceMapEntry.LogicalZoneId.OutOfRange",
                    "logical_zone_id",
                    "LogicalZoneId must be in [0,13].");
            }

            if (groupIndex >= LiquidZoneInfluenceMapV1.GroupCount)
            {
                return Invalid(
                    "LiquidZoneInfluenceMapEntry.GroupIndex.OutOfRange",
                    "group_index",
                    "GroupIndex must be 0 or 1 for the approved v1 fixture.");
            }

            if (!IsCanonicalPositive(weightMInversePerFillFraction))
            {
                return Invalid(
                    "LiquidZoneInfluenceMapEntry.Weight.Invalid",
                    "weight_m_inverse_per_fill_fraction",
                    "The approved positive-absorption weight must be finite and strictly positive without signed zero.");
            }

            if (!string.Equals(sourceUnit, LiquidZoneInfluenceMapV1.SourceUnitDimensionless, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMapEntry.SourceUnit.Invalid",
                    "source_unit",
                    "The approved fixture source unit is exactly dimensionless.");
            }

            if (!string.Equals(targetUnit, LiquidZoneInfluenceMapV1.TargetUnitMInverse, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMapEntry.TargetUnit.Invalid",
                    "target_unit",
                    "The approved fixture target unit is exactly m^-1.");
            }

            return ContractValidationResult<LiquidZoneInfluenceMapEntryV1>.Valid(
                new LiquidZoneInfluenceMapEntryV1(
                    logicalZoneId,
                    targetNode,
                    groupIndex,
                    weightMInversePerFillFraction,
                    sourceUnit!,
                    targetUnit!));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-LIQUID-ZONE-INFLUENCE-MAP-ENTRY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, LogicalZoneId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, WeightMInversePerFillFraction);
                Phase5CanonicalBytesV1.WriteString(writer, SourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, TargetUnit);
            });
        }

        private static bool IsCanonicalPositive(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static ContractValidationResult<LiquidZoneInfluenceMapEntryV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneInfluenceMapEntryV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// The owner-approved synthetic P6-T02 influence-map package. This is a
    /// deliberately narrow admission contract: it accepts only the approved
    /// test-only identity, topology, units, sign certificate, reference state,
    /// and 28-entry positive fixture. It is not a production CANDU data model.
    /// </summary>
    public sealed class LiquidZoneInfluenceMapV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint GroupCount = 2;
        public const uint TargetNodeCount = LiquidZoneGroupingV1.PhysicalAssemblyCount;
        public const uint EntryCount = LiquidZoneGroupingV1.LogicalZoneCount * GroupCount;
        public const string SchemaId = "CANDU-LIQUID-ZONE-INFLUENCE-MAP-V1";
        public const string ApprovedDataVersion = "p6-t02-synthetic-liquid-zone-map-v1";
        public const string ApprovedMappingVersion = "p6-t02-synthetic-grouping-v1";
        public const string ApprovedOwnerId = "Kevin Ho";
        public const string SourceUnitDimensionless = "dimensionless";
        public const string TargetUnitMInverse = "m^-1";
        public const string ApprovedSignCertificate =
            "P6-T02-SYNTHETIC-POSITIVE-ABSORPTION-V1";
        public const double ApprovedReferenceFillFraction = 0.5;
        public const double ApprovedGroup0WeightMInversePerFillFraction = 0.0010;
        public const double ApprovedGroup1WeightMInversePerFillFraction = 0.0005;
        public const string ApprovedMappingDigestHex =
            "0be2b126d2fbe6fa17bda290fba044468192266159d11921330a6352f688a713";
        public const string ApprovedMapDigestHex =
            "4bc51694741efd9777cb5f9492ecf9fb72376a2a4ccbfd783d88be7f6c33047a";

        private static readonly uint[] ApprovedPhysicalAssemblyByLogicalZone =
            { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0, 1 };

        public static readonly Digest32 ApprovedMappingDigest = ParseDigestHex(
            ApprovedMappingDigestHex);

        public static readonly Digest32 ApprovedMapDigest = ParseDigestHex(
            ApprovedMapDigestHex);

        public static readonly StableId ApprovedMapId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a602");

        public static readonly StableId ApprovedMappingId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a601");

        public static readonly StableId ApprovedTopologyId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a603");

        private LiquidZoneInfluenceMapV1(
            uint schemaVersion,
            StableId mapId,
            LiquidZoneGroupingV1 grouping,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string sourceUnit,
            string targetUnit,
            IEnumerable<double> referenceFillFractions,
            Digest32 referenceStateDigest,
            IEnumerable<LiquidZoneInfluenceMapEntryV1> entries,
            Digest32 mapDigest)
        {
            SchemaVersion = schemaVersion;
            MapId = mapId;
            MappingId = grouping.MappingId;
            MappingVersion = grouping.MappingVersion;
            MappingDigest = grouping.MappingDigest;
            Grouping = grouping;
            TopologyId = topologyId;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
            TopologyDigest = topologyDigest;
            DataVersion = dataVersion;
            OwnerId = ownerId;
            SignCertificate = signCertificate;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            ReferenceFillFractions = new ReadOnlyCollection<double>(referenceFillFractions.ToArray());
            ReferenceStateDigest = referenceStateDigest;
            Entries = new ReadOnlyCollection<LiquidZoneInfluenceMapEntryV1>(entries.ToArray());
            MapDigest = mapDigest;
        }

        public uint SchemaVersion { get; }

        public StableId MapId { get; }

        public StableId MappingId { get; }

        public string MappingVersion { get; }

        public Digest32 MappingDigest { get; }

        public LiquidZoneGroupingV1 Grouping { get; }

        public StableId TopologyId { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public Digest32 TopologyDigest { get; }

        public string DataVersion { get; }

        public string OwnerId { get; }

        public string SignCertificate { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public IReadOnlyList<double> ReferenceFillFractions { get; }

        public Digest32 ReferenceStateDigest { get; }

        public IReadOnlyList<LiquidZoneInfluenceMapEntryV1> Entries { get; }

        public Digest32 MapDigest { get; }

        public static ContractValidationResult<LiquidZoneInfluenceMapV1> TryCreate(
            uint schemaVersion,
            StableId mapId,
            LiquidZoneGroupingV1? grouping,
            StableId topologyId,
            IEnumerable<NodeKey>? targetNodes,
            Digest32? topologyDigest,
            string? dataVersion,
            string? ownerId,
            string? signCertificate,
            string? sourceUnit,
            string? targetUnit,
            IEnumerable<double>? referenceFillFractions,
            Digest32? referenceStateDigest,
            IEnumerable<LiquidZoneInfluenceMapEntryV1>? entries,
            Digest32? expectedMapDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic P6-T02 influence-map schema version 1 is accepted.");
            }

            if (mapId != ApprovedMapId)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MapId.Unapproved",
                    "map_id",
                    "The P6-T02 task admits only the owner-approved synthetic map identity.");
            }

            if (grouping == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.Grouping.Missing",
                    "grouping",
                    "A validated logical-to-physical grouping is required.");
            }

            if (grouping.MappingId != ApprovedMappingId ||
                !string.Equals(grouping.MappingVersion, ApprovedMappingVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MappingIdentity.Unapproved",
                    "mapping",
                    "The supplied grouping is not the owner-approved synthetic P6-T02 grouping.");
            }

            if (!grouping.MappingDigest.Equals(ApprovedMappingDigest) ||
                !MatchesApprovedGrouping(grouping))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MappingContent.Unapproved",
                    "mapping",
                    "The supplied grouping content and digest must equal the owner-approved synthetic 14-to-6 table.");
            }

            if (topologyId != ApprovedTopologyId)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.TopologyId.Unapproved",
                    "topology_id",
                    "The P6-T02 task admits only the owner-approved synthetic topology identity.");
            }

            if (targetNodes == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.TargetNodes.Missing",
                    "target_nodes",
                    "An explicit target-node set is required; channel order is never inferred.");
            }

            NodeKey[] targetEntries = targetNodes.ToArray();
            if (targetEntries.Length != (int)TargetNodeCount)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.TargetNodes.CountMismatch",
                    "target_nodes",
                    "The approved synthetic topology requires exactly six target nodes.");
            }

            NodeKey[] canonicalTargets = targetEntries.OrderBy(node => node).ToArray();
            for (int index = 1; index < canonicalTargets.Length; index++)
            {
                if (canonicalTargets[index - 1] == canonicalTargets[index])
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.TargetNodes.Duplicate",
                        "target_nodes",
                        "Target nodes must be unique explicit NodeKey values.");
                }
            }

            for (uint physicalAssemblyId = 0; physicalAssemblyId < TargetNodeCount; physicalAssemblyId++)
            {
                NodeKey expectedNode = new NodeKey(
                    new ChannelId(physicalAssemblyId),
                    new BundlePosition(0));
                if (canonicalTargets[(int)physicalAssemblyId] != expectedNode)
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.TargetNodes.TopologyMismatch",
                        "target_nodes",
                        "The approved synthetic topology requires (ChannelId=p, BundlePosition=0) for physical assembly p.");
                }
            }

            if (topologyDigest == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.TopologyDigest.Missing",
                    "topology_digest",
                    "The explicit topology digest is required.");
            }

            Digest32 expectedTopologyDigest = ComputeTopologyDigest(topologyId, canonicalTargets);
            if (!topologyDigest.Equals(expectedTopologyDigest))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.TopologyDigest.Mismatch",
                    "topology_digest",
                    "The topology digest does not equal the canonical target-node topology bytes.");
            }

            if (!string.Equals(dataVersion, ApprovedDataVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.DataVersion.Unapproved",
                    "data_version",
                    "The P6-T02 task admits only the owner-approved synthetic data version.");
            }

            if (!string.Equals(ownerId, ApprovedOwnerId, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.Owner.Unapproved",
                    "owner_id",
                    "The admitted synthetic map owner must be Kevin Ho.");
            }

            if (!string.Equals(signCertificate, ApprovedSignCertificate, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.SignCertificate.Unapproved",
                    "sign_certificate",
                    "The admitted synthetic map must use the approved positive-absorption sign certificate.");
            }

            if (!string.Equals(sourceUnit, SourceUnitDimensionless, StringComparison.Ordinal) ||
                !string.Equals(targetUnit, TargetUnitMInverse, StringComparison.Ordinal))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.Units.Invalid",
                    "units",
                    "The admitted synthetic map uses dimensionless source fill and m^-1 target absorption.");
            }

            if (referenceFillFractions == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.ReferenceFill.Missing",
                    "reference_fill_fractions",
                    "Exactly fourteen explicit reference fill fractions are required.");
            }

            double[] referenceEntries = referenceFillFractions.ToArray();
            if (referenceEntries.Length != (int)LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.ReferenceFill.CountMismatch",
                    "reference_fill_fractions",
                    "Exactly fourteen logical-zone reference fill fractions are required.");
            }

            for (int index = 0; index < referenceEntries.Length; index++)
            {
                if (!IsCanonicalFraction(referenceEntries[index]) ||
                    referenceEntries[index] != ApprovedReferenceFillFraction)
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.ReferenceFill.Unapproved",
                        "reference_fill_fractions[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every approved synthetic logical zone has reference fill fraction 0.5.");
                }
            }

            if (referenceStateDigest == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.ReferenceStateDigest.Missing",
                    "reference_state_digest",
                    "The explicit reference-state binding digest is required.");
            }

            Digest32 expectedReferenceStateDigest = ComputeReferenceStateDigest(
                mapId,
                grouping,
                topologyId,
                topologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                sourceUnit!,
                targetUnit!,
                referenceEntries);
            if (!referenceStateDigest.Equals(expectedReferenceStateDigest))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.ReferenceStateDigest.Mismatch",
                    "reference_state_digest",
                    "The reference-state digest does not equal the canonical synthetic reference binding.");
            }

            if (entries == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.Entries.Missing",
                    "entries",
                    "The explicit sparse influence-entry collection is required.");
            }

            LiquidZoneInfluenceMapEntryV1[] entryEntries = entries.ToArray();
            if (entryEntries.Length != (int)EntryCount)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.Entries.CountMismatch",
                    "entries",
                    "The approved synthetic map requires exactly 28 entries: fourteen logical zones times two groups.");
            }

            for (int index = 0; index < entryEntries.Length; index++)
            {
                if (entryEntries[index] == null)
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.Entry.Null",
                        "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Influence-map entries may not be null.");
                }
            }

            LiquidZoneInfluenceMapEntryV1[] canonicalEntries = entryEntries
                .OrderBy(entry => entry.LogicalZoneId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();

            for (int index = 1; index < canonicalEntries.Length; index++)
            {
                LiquidZoneInfluenceMapEntryV1 previous = canonicalEntries[index - 1];
                LiquidZoneInfluenceMapEntryV1 current = canonicalEntries[index];
                if (previous.LogicalZoneId == current.LogicalZoneId &&
                    previous.TargetNode == current.TargetNode &&
                    previous.GroupIndex == current.GroupIndex)
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.Entry.DuplicateKey",
                        "entries",
                        "The logical-zone, target-node, and group key must be unique.");
                }
            }

            for (uint logicalZoneId = 0; logicalZoneId < LiquidZoneGroupingV1.LogicalZoneCount; logicalZoneId++)
            {
                uint physicalAssemblyId = grouping.Mappings[(int)logicalZoneId].PhysicalAssemblyId;
                NodeKey expectedTargetNode = canonicalTargets[(int)physicalAssemblyId];
                LiquidZoneInfluenceMapEntryV1[] zoneEntries = canonicalEntries
                    .Where(entry => entry.LogicalZoneId == logicalZoneId)
                    .ToArray();
                if (zoneEntries.Length != (int)GroupCount)
                {
                    return Invalid(
                        "LiquidZoneInfluenceMap.Entry.LogicalZoneCountMismatch",
                        "entries",
                        "Every logical zone must have exactly one entry for each approved group.");
                }

                for (ushort groupIndex = 0; groupIndex < GroupCount; groupIndex++)
                {
                    LiquidZoneInfluenceMapEntryV1? entry = zoneEntries
                        .FirstOrDefault(candidate => candidate.GroupIndex == groupIndex);
                    if (entry == null)
                    {
                        return Invalid(
                            "LiquidZoneInfluenceMap.Entry.GroupMissing",
                            "entries",
                            "Every logical zone must have group 0 and group 1 entries.");
                    }

                    if (entry.TargetNode != expectedTargetNode)
                    {
                        return Invalid(
                            "LiquidZoneInfluenceMap.Entry.TargetMismatch",
                            "entries",
                            "An influence target must equal the explicit node bound to the logical zone's physical assembly.");
                    }

                    double expectedWeight = groupIndex == 0
                        ? ApprovedGroup0WeightMInversePerFillFraction
                        : ApprovedGroup1WeightMInversePerFillFraction;
                    if (entry.WeightMInversePerFillFraction != expectedWeight ||
                        !string.Equals(entry.SourceUnit, sourceUnit, StringComparison.Ordinal) ||
                        !string.Equals(entry.TargetUnit, targetUnit, StringComparison.Ordinal))
                    {
                        return Invalid(
                            "LiquidZoneInfluenceMap.Entry.Value.Unapproved",
                            "entries",
                            "The admitted synthetic map entry does not equal the owner-approved weight or unit binding.");
                    }
                }
            }

            if (expectedMapDigest == null)
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MapDigest.Missing",
                    "map_digest",
                    "The map digest is required for package admission and may not be generated implicitly by a consumer.");
            }

            if (!expectedMapDigest.Equals(ApprovedMapDigest))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MapDigest.Unapproved",
                    "map_digest",
                    "The admitted map digest must equal the owner-approved synthetic package digest.");
            }

            Digest32 calculatedMapDigest = ComputeDigest(
                schemaVersion,
                mapId,
                grouping,
                topologyId,
                canonicalTargets,
                topologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                sourceUnit!,
                targetUnit!,
                referenceEntries,
                referenceStateDigest,
                canonicalEntries);
            if (!expectedMapDigest.Equals(calculatedMapDigest))
            {
                return Invalid(
                    "LiquidZoneInfluenceMap.MapDigest.Mismatch",
                    "map_digest",
                    "The supplied map digest does not equal the canonical identity, units, reference, topology, and entry bytes.");
            }

            return ContractValidationResult<LiquidZoneInfluenceMapV1>.Valid(
                new LiquidZoneInfluenceMapV1(
                    schemaVersion,
                    mapId,
                    grouping,
                    topologyId,
                    canonicalTargets,
                    topologyDigest,
                    dataVersion!,
                    ownerId!,
                    signCertificate!,
                    sourceUnit!,
                    targetUnit!,
                    referenceEntries,
                    referenceStateDigest,
                    canonicalEntries,
                    expectedMapDigest));
        }

        public static Digest32 ComputeTopologyDigest(
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes)
        {
            if (targetNodes == null)
            {
                throw new ArgumentNullException(nameof(targetNodes));
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Phase5CanonicalBytesV1.Build(writer =>
                    {
                        Phase5CanonicalBytesV1.WriteAscii(
                            writer,
                            "CANDU-LIQUID-ZONE-TOPOLOGY-V1");
                        writer.Write((byte)0);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                        Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            checked((uint)canonicalTargets.Length));
                        foreach (NodeKey node in canonicalTargets)
                        {
                            Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                            Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                        }
                    })));
        }

        public static Digest32 ComputeReferenceStateDigest(
            StableId mapId,
            LiquidZoneGroupingV1 grouping,
            StableId topologyId,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string sourceUnit,
            string targetUnit,
            IEnumerable<double> referenceFillFractions)
        {
            if (grouping == null)
            {
                throw new ArgumentNullException(nameof(grouping));
            }

            if (referenceFillFractions == null)
            {
                throw new ArgumentNullException(nameof(referenceFillFractions));
            }

            double[] references = referenceFillFractions.ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Phase5CanonicalBytesV1.Build(writer =>
                    {
                        Phase5CanonicalBytesV1.WriteAscii(
                            writer,
                            "CANDU-LIQUID-ZONE-REFERENCE-STATE-V1");
                        writer.Write((byte)0);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                        Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                        Phase5CanonicalBytesV1.WriteStableId(writer, grouping.MappingId);
                        Phase5CanonicalBytesV1.WriteString(writer, grouping.MappingVersion);
                        Phase5CanonicalBytesV1.WriteDigest(writer, grouping.MappingDigest);
                        Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                        Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                        Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                        Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                        Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                        Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                        Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            checked((uint)references.Length));
                        foreach (double reference in references)
                        {
                            Phase5CanonicalBytesV1.WriteDouble(writer, reference);
                        }
                    })));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId mapId,
            LiquidZoneGroupingV1 grouping,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string sourceUnit,
            string targetUnit,
            IEnumerable<double> referenceFillFractions,
            Digest32 referenceStateDigest,
            IEnumerable<LiquidZoneInfluenceMapEntryV1> entries)
        {
            if (grouping == null)
            {
                throw new ArgumentNullException(nameof(grouping));
            }

            if (targetNodes == null)
            {
                throw new ArgumentNullException(nameof(targetNodes));
            }

            if (referenceFillFractions == null)
            {
                throw new ArgumentNullException(nameof(referenceFillFractions));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            LiquidZoneInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.LogicalZoneId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        schemaVersion,
                        mapId,
                        grouping,
                        topologyId,
                        canonicalTargets,
                        topologyDigest,
                        dataVersion,
                        ownerId,
                        signCertificate,
                        sourceUnit,
                        targetUnit,
                        referenceFillFractions.ToArray(),
                        referenceStateDigest,
                        canonicalEntries,
                        null)));
        }

        public bool TryGetEntry(
            uint logicalZoneId,
            NodeKey targetNode,
            ushort groupIndex,
            out LiquidZoneInfluenceMapEntryV1 entry)
        {
            entry = Entries.FirstOrDefault(candidate =>
                candidate.LogicalZoneId == logicalZoneId &&
                candidate.TargetNode == targetNode &&
                candidate.GroupIndex == groupIndex)!;
            return entry != null;
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                MapId,
                Grouping,
                TopologyId,
                TargetNodes,
                TopologyDigest,
                DataVersion,
                OwnerId,
                SignCertificate,
                SourceUnit,
                TargetUnit,
                ReferenceFillFractions,
                ReferenceStateDigest,
                Entries,
                MapDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId mapId,
            LiquidZoneGroupingV1 grouping,
            StableId topologyId,
            IReadOnlyList<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string sourceUnit,
            string targetUnit,
            IReadOnlyList<double> referenceFillFractions,
            Digest32 referenceStateDigest,
            IReadOnlyList<LiquidZoneInfluenceMapEntryV1> entries,
            Digest32? mapDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, SchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteStableId(writer, grouping.MappingId);
                Phase5CanonicalBytesV1.WriteString(writer, grouping.MappingVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, grouping.MappingDigest);
                Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                Phase5CanonicalBytesV1.WriteDigest(writer, referenceStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)referenceFillFractions.Count));
                foreach (double reference in referenceFillFractions)
                {
                    Phase5CanonicalBytesV1.WriteDouble(writer, reference);
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)targetNodes.Count));
                foreach (NodeKey node in targetNodes)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)entries.Count));
                foreach (LiquidZoneInfluenceMapEntryV1 entry in entries)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, entry.ToCanonicalBytes());
                }

                if (mapDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                }
            });
        }

        private static bool IsCanonicalFraction(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 && value <= 1.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static bool MatchesApprovedGrouping(LiquidZoneGroupingV1 grouping)
        {
            if (grouping.Mappings.Count != ApprovedPhysicalAssemblyByLogicalZone.Length)
            {
                return false;
            }

            for (int logicalZoneIndex = 0;
                 logicalZoneIndex < ApprovedPhysicalAssemblyByLogicalZone.Length;
                 logicalZoneIndex++)
            {
                LiquidZoneAssemblyBindingV1 mapping = grouping.Mappings[logicalZoneIndex];
                if (mapping.LogicalZoneId != (uint)logicalZoneIndex ||
                    mapping.PhysicalAssemblyId != ApprovedPhysicalAssemblyByLogicalZone[logicalZoneIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private static Digest32 ParseDigestHex(string value)
        {
            if (value == null || value.Length != 64)
            {
                throw new ArgumentException("A SHA-256 digest requires 64 hexadecimal characters.", nameof(value));
            }

            byte[] bytes = new byte[32];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            }

            return new Digest32(bytes);
        }

        private static ContractValidationResult<LiquidZoneInfluenceMapV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneInfluenceMapV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One deterministic node/group local absorption-overlay value.
    /// </summary>
    public sealed class LiquidZoneOverlayValueV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneOverlayValueV1(
            NodeKey targetNode,
            ushort groupIndex,
            double deltaSigmaAMInverse)
        {
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            DeltaSigmaAMInverse = deltaSigmaAMInverse;
        }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double DeltaSigmaAMInverse { get; }

        internal static ContractValidationResult<LiquidZoneOverlayValueV1> TryCreate(
            NodeKey targetNode,
            ushort groupIndex,
            double deltaSigmaAMInverse)
        {
            if (groupIndex >= LiquidZoneInfluenceMapV1.GroupCount)
            {
                return ContractValidationResult<LiquidZoneOverlayValueV1>.Invalid(
                    "LiquidZoneOverlayValue.GroupIndex.OutOfRange",
                    "group_index",
                    "Overlay group index must be 0 or 1.");
            }

            if (!ContractValidation.IsFinite(deltaSigmaAMInverse) ||
                BitConverter.DoubleToInt64Bits(deltaSigmaAMInverse) == long.MinValue)
            {
                return ContractValidationResult<LiquidZoneOverlayValueV1>.Invalid(
                    "LiquidZoneOverlayValue.Value.Invalid",
                    "delta_sigma_a_m_inverse",
                    "Overlay values must be finite and may not use signed negative zero.");
            }

            if (deltaSigmaAMInverse == 0.0)
            {
                deltaSigmaAMInverse = 0.0;
            }

            return ContractValidationResult<LiquidZoneOverlayValueV1>.Valid(
                new LiquidZoneOverlayValueV1(targetNode, groupIndex, deltaSigmaAMInverse));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-OVERLAY-VALUE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, DeltaSigmaAMInverse);
            });
        }
    }

    /// <summary>
    /// Complete deterministic overlay projection. It contains one value for
    /// each explicit synthetic target node and group and records disabled-zone
    /// exact-zero assertions without changing authoritative zone state.
    /// </summary>
    public sealed class LiquidZoneOverlayEvaluationV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneOverlayEvaluationV1(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 systemStateDigest,
            IEnumerable<LiquidZoneOverlayValueV1> values,
            IEnumerable<LiquidZoneDisabledZeroAssertionV1> disabledZeroAssertions,
            Digest32 overlayDigest)
        {
            MapId = mapId;
            MapDigest = mapDigest;
            SystemStateDigest = systemStateDigest;
            Values = new ReadOnlyCollection<LiquidZoneOverlayValueV1>(values.ToArray());
            DisabledZeroAssertions = new ReadOnlyCollection<LiquidZoneDisabledZeroAssertionV1>(
                disabledZeroAssertions.ToArray());
            OverlayDigest = overlayDigest;
        }

        public StableId MapId { get; }

        public Digest32 MapDigest { get; }

        public Digest32 SystemStateDigest { get; }

        public IReadOnlyList<LiquidZoneOverlayValueV1> Values { get; }

        public IReadOnlyList<LiquidZoneDisabledZeroAssertionV1> DisabledZeroAssertions { get; }

        public Digest32 OverlayDigest { get; }

        internal static LiquidZoneOverlayEvaluationV1 Create(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 systemStateDigest,
            IEnumerable<LiquidZoneOverlayValueV1> values,
            IEnumerable<LiquidZoneDisabledZeroAssertionV1> disabledZeroAssertions)
        {
            LiquidZoneOverlayValueV1[] canonicalValues = values
                .OrderBy(value => value.TargetNode)
                .ThenBy(value => value.GroupIndex)
                .ToArray();
            LiquidZoneDisabledZeroAssertionV1[] canonicalAssertions = disabledZeroAssertions
                .OrderBy(assertion => assertion.LogicalZoneId)
                .ToArray();
            Digest32 overlayDigest = ComputeDigest(
                mapId,
                mapDigest,
                systemStateDigest,
                canonicalValues,
                canonicalAssertions);
            return new LiquidZoneOverlayEvaluationV1(
                mapId,
                mapDigest,
                systemStateDigest,
                canonicalValues,
                canonicalAssertions,
                overlayDigest);
        }

        public ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> TryGetDisabledZeroAssertion(
            uint logicalZoneId)
        {
            LiquidZoneDisabledZeroAssertionV1? assertion = DisabledZeroAssertions
                .FirstOrDefault(candidate => candidate.LogicalZoneId == logicalZoneId);
            if (assertion == null)
            {
                return ContractValidationResult<LiquidZoneDisabledZeroAssertionV1>.Invalid(
                    "LiquidZoneOverlay.DisabledZero.NotApplicable",
                    "logical_zone_id",
                    "No disabled-zero assertion exists for the requested logical zone.");
            }

            return ContractValidationResult<LiquidZoneDisabledZeroAssertionV1>.Valid(assertion);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                MapId,
                MapDigest,
                SystemStateDigest,
                Values,
                DisabledZeroAssertions,
                OverlayDigest);
        }

        internal static Digest32 ComputeDigest(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 systemStateDigest,
            IReadOnlyList<LiquidZoneOverlayValueV1> values,
            IReadOnlyList<LiquidZoneDisabledZeroAssertionV1> disabledZeroAssertions)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        mapId,
                        mapDigest,
                        systemStateDigest,
                        values,
                        disabledZeroAssertions,
                        null)));
        }

        private static byte[] BuildBytes(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 systemStateDigest,
            IReadOnlyList<LiquidZoneOverlayValueV1> values,
            IReadOnlyList<LiquidZoneDisabledZeroAssertionV1> disabledZeroAssertions,
            Digest32? overlayDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-OVERLAY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, systemStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
                foreach (LiquidZoneOverlayValueV1 value in values)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)disabledZeroAssertions.Count));
                foreach (LiquidZoneDisabledZeroAssertionV1 assertion in disabledZeroAssertions)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, assertion.ToCanonicalBytes());
                }

                if (overlayDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, overlayDigest);
                }
            });
        }
    }

    /// <summary>
    /// Pure P6-T02 motion projection. Queue allocation, due-batch freezing,
    /// consumption, and rollback are deliberately absent and remain owned by
    /// P6-T06. The available command supplied here is the command projection
    /// being evaluated; it cannot move the physical state before its explicit
    /// command-available time.
    /// </summary>
    public sealed class LiquidZoneMotionResultV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneMotionResultV1(
            uint logicalZoneId,
            LiquidZoneModeV1 mode,
            bool enabled,
            bool commandDelaySatisfied,
            double stateFillFractionBefore,
            double stateFillFractionAfter,
            double availableCommandFillFraction,
            double lastMotionTimeSeconds,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            double appliedDeltaFraction,
            Digest32 sourceStateDigest,
            Digest32 motionDigest)
        {
            LogicalZoneId = logicalZoneId;
            Mode = mode;
            Enabled = enabled;
            CommandDelaySatisfied = commandDelaySatisfied;
            StateFillFractionBefore = stateFillFractionBefore;
            StateFillFractionAfter = stateFillFractionAfter;
            AvailableCommandFillFraction = availableCommandFillFraction;
            LastMotionTimeSeconds = lastMotionTimeSeconds;
            CommandAvailableTimeSeconds = commandAvailableTimeSeconds;
            CurrentTimeSeconds = currentTimeSeconds;
            AppliedDeltaFraction = appliedDeltaFraction;
            SourceStateDigest = sourceStateDigest;
            MotionDigest = motionDigest;
        }

        public uint LogicalZoneId { get; }

        public LiquidZoneModeV1 Mode { get; }

        public bool Enabled { get; }

        public bool CommandDelaySatisfied { get; }

        public double StateFillFractionBefore { get; }

        public double StateFillFractionAfter { get; }

        public double AvailableCommandFillFraction { get; }

        public double LastMotionTimeSeconds { get; }

        public double CommandAvailableTimeSeconds { get; }

        public double CurrentTimeSeconds { get; }

        public double AppliedDeltaFraction { get; }

        public Digest32 SourceStateDigest { get; }

        public Digest32 MotionDigest { get; }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                LogicalZoneId,
                Mode,
                Enabled,
                CommandDelaySatisfied,
                StateFillFractionBefore,
                StateFillFractionAfter,
                AvailableCommandFillFraction,
                LastMotionTimeSeconds,
                CommandAvailableTimeSeconds,
                CurrentTimeSeconds,
                AppliedDeltaFraction,
                SourceStateDigest,
                MotionDigest);
        }

        internal static LiquidZoneMotionResultV1 Create(
            LiquidZoneStateV1 zone,
            bool commandDelaySatisfied,
            double stateFillFractionAfter,
            double availableCommandFillFraction,
            double lastMotionTimeSeconds,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            double appliedDeltaFraction)
        {
            Digest32 sourceStateDigest = zone.StateDigest;
            Digest32 motionDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        zone.LogicalZoneId,
                        zone.Mode,
                        zone.Enabled,
                        commandDelaySatisfied,
                        zone.StateFillFraction,
                        stateFillFractionAfter,
                        availableCommandFillFraction,
                        lastMotionTimeSeconds,
                        commandAvailableTimeSeconds,
                        currentTimeSeconds,
                        appliedDeltaFraction,
                        sourceStateDigest,
                        null)));
            return new LiquidZoneMotionResultV1(
                zone.LogicalZoneId,
                zone.Mode,
                zone.Enabled,
                commandDelaySatisfied,
                zone.StateFillFraction,
                stateFillFractionAfter,
                availableCommandFillFraction,
                lastMotionTimeSeconds,
                commandAvailableTimeSeconds,
                currentTimeSeconds,
                appliedDeltaFraction,
                sourceStateDigest,
                motionDigest);
        }

        private static byte[] BuildBytes(
            uint logicalZoneId,
            LiquidZoneModeV1 mode,
            bool enabled,
            bool commandDelaySatisfied,
            double stateFillFractionBefore,
            double stateFillFractionAfter,
            double availableCommandFillFraction,
            double lastMotionTimeSeconds,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            double appliedDeltaFraction,
            Digest32 sourceStateDigest,
            Digest32? motionDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-MOTION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, logicalZoneId);
                writer.Write((byte)mode);
                writer.Write(enabled ? (byte)1 : (byte)0);
                writer.Write(commandDelaySatisfied ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFillFractionBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFillFractionAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, availableCommandFillFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, lastMotionTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, commandAvailableTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, currentTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, appliedDeltaFraction);
                Phase5CanonicalBytesV1.WriteDigest(writer, sourceStateDigest);
                if (motionDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, motionDigest);
                }
            });
        }
    }

    public static class LiquidZoneOverlayV1
    {
        /// <summary>
        /// Validates the owner-approved map/state bindings and evaluates only
        /// the local absorption overlay. It has no direct-reactivity path and
        /// does not mutate the supplied system state.
        /// </summary>
        public static ContractValidationResult<LiquidZoneOverlayEvaluationV1> TryEvaluate(
            LiquidZoneInfluenceMapV1? map,
            LiquidZoneSystemStateV1? systemState)
        {
            if (map == null)
            {
                return Invalid(
                    "LiquidZoneOverlay.Map.Missing",
                    "map",
                    "A validated influence map is required.");
            }

            if (systemState == null)
            {
                return Invalid(
                    "LiquidZoneOverlay.SystemState.Missing",
                    "system_state",
                    "A validated liquid-zone system state is required.");
            }

            LiquidZoneGroupingV1 grouping = systemState.Grouping;
            if (grouping.MappingId != map.MappingId ||
                !string.Equals(grouping.MappingVersion, map.MappingVersion, StringComparison.Ordinal) ||
                !grouping.MappingDigest.Equals(map.MappingDigest))
            {
                return Invalid(
                    "LiquidZoneOverlay.MappingBinding.Mismatch",
                    "system_state.grouping",
                    "The system grouping identity and digest must equal the admitted map grouping.");
            }

            if (systemState.Zones.Count != (int)LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneOverlay.Zones.CountMismatch",
                    "system_state.zones",
                    "Overlay evaluation requires all fourteen validated logical-zone states.");
            }

            LiquidZoneStateV1[] zones = systemState.Zones
                .OrderBy(zone => zone.LogicalZoneId)
                .ToArray();
            for (int index = 0; index < zones.Length; index++)
            {
                LiquidZoneStateV1 zone = zones[index];
                if (zone.InfluenceMapId != map.MapId)
                {
                    return Invalid(
                        "LiquidZoneOverlay.ZoneMapId.Mismatch",
                        "system_state.zones[" + index.ToString(CultureInfo.InvariantCulture) + "].influence_map_id",
                        "Every zone state must bind the admitted map identity.");
                }

                if (!string.Equals(zone.DataVersion, map.DataVersion, StringComparison.Ordinal) ||
                    !zone.DataDigest.Equals(map.MapDigest))
                {
                    return Invalid(
                        "LiquidZoneOverlay.ZoneDataBinding.Mismatch",
                        "system_state.zones[" + index.ToString(CultureInfo.InvariantCulture) + "].data_identity",
                        "Every zone state must bind the admitted map data version and digest.");
                }

                if (zone.ReferenceFillFraction != map.ReferenceFillFractions[(int)zone.LogicalZoneId])
                {
                    return Invalid(
                        "LiquidZoneOverlay.ReferenceFill.Mismatch",
                        "system_state.zones[" + index.ToString(CultureInfo.InvariantCulture) + "].reference_fill_fraction",
                        "Every zone reference fill must equal the admitted map reference profile.");
                }
            }

            double[,] sums = new double[
                (int)LiquidZoneInfluenceMapV1.TargetNodeCount,
                (int)LiquidZoneInfluenceMapV1.GroupCount];
            List<LiquidZoneDisabledZeroAssertionV1> disabledAssertions =
                new List<LiquidZoneDisabledZeroAssertionV1>();

            foreach (LiquidZoneStateV1 zone in zones)
            {
                if (!zone.Enabled)
                {
                    ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> assertion =
                        zone.TryGetDisabledZeroAssertion();
                    if (!assertion.IsValid)
                    {
                        return Invalid(
                            "LiquidZoneOverlay.DisabledState.Invalid",
                            "system_state.zones",
                            assertion.FirstDiagnostic.Message);
                    }

                    disabledAssertions.Add(assertion.Value);
                }
            }

            foreach (LiquidZoneInfluenceMapEntryV1 entry in map.Entries)
            {
                LiquidZoneStateV1 zone = zones[(int)entry.LogicalZoneId];
                if (!zone.Enabled)
                {
                    continue;
                }

                int targetIndex = -1;
                for (int candidateIndex = 0; candidateIndex < map.TargetNodes.Count; candidateIndex++)
                {
                    if (map.TargetNodes[candidateIndex] == entry.TargetNode)
                    {
                        targetIndex = candidateIndex;
                        break;
                    }
                }
                if (targetIndex < 0)
                {
                    return Invalid(
                        "LiquidZoneOverlay.TargetNode.Unknown",
                        "map.entries",
                        "Every map entry target must be present in the admitted target-node set.");
                }

                double deltaFill = zone.StateFillFraction - zone.ReferenceFillFraction;
                double contribution = entry.WeightMInversePerFillFraction * deltaFill;
                if (!ContractValidation.IsFinite(deltaFill) || !ContractValidation.IsFinite(contribution))
                {
                    return Invalid(
                        "LiquidZoneOverlay.Value.NonFinite",
                        "overlay",
                        "Overlay aggregation fails closed on non-finite fill differences or contributions.");
                }

                double sum = sums[targetIndex, entry.GroupIndex] + contribution;
                if (!ContractValidation.IsFinite(sum))
                {
                    return Invalid(
                        "LiquidZoneOverlay.Value.Overflow",
                        "overlay",
                        "Overlay aggregation fails closed on non-finite accumulated values.");
                }

                sums[targetIndex, entry.GroupIndex] = sum;
            }

            List<LiquidZoneOverlayValueV1> values = new List<LiquidZoneOverlayValueV1>(
                checked((int)(LiquidZoneInfluenceMapV1.TargetNodeCount * LiquidZoneInfluenceMapV1.GroupCount)));
            for (int targetIndex = 0; targetIndex < map.TargetNodes.Count; targetIndex++)
            {
                for (ushort groupIndex = 0; groupIndex < LiquidZoneInfluenceMapV1.GroupCount; groupIndex++)
                {
                    ContractValidationResult<LiquidZoneOverlayValueV1> value =
                        LiquidZoneOverlayValueV1.TryCreate(
                            map.TargetNodes[targetIndex],
                            groupIndex,
                            sums[targetIndex, groupIndex]);
                    if (!value.IsValid)
                    {
                        return Invalid(
                            "LiquidZoneOverlay.Value.Invalid",
                            "overlay",
                            value.FirstDiagnostic.Message);
                    }

                    values.Add(value.Value);
                }
            }

            return ContractValidationResult<LiquidZoneOverlayEvaluationV1>.Valid(
                LiquidZoneOverlayEvaluationV1.Create(
                    map.MapId,
                    map.MapDigest,
                    systemState.StateDigest,
                    values,
                    disabledAssertions));
        }

        public static ContractValidationResult<LiquidZoneMotionResultV1> TryAdvance(
            LiquidZoneStateV1? zone,
            double availableCommandFillFraction,
            double lastMotionTimeSeconds,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds)
        {
            if (zone == null)
            {
                return MotionInvalid(
                    "LiquidZoneMotion.Zone.Missing",
                    "zone",
                    "A validated zone state is required.");
            }

            if (!IsCanonicalFraction(availableCommandFillFraction))
            {
                return MotionInvalid(
                    "LiquidZoneMotion.AvailableCommand.Invalid",
                    "available_command_fill_fraction",
                    "The queue-projected available command fill must be finite and in [0,1] without signed zero.");
            }

            if (!IsCanonicalNonnegative(lastMotionTimeSeconds) ||
                !IsCanonicalNonnegative(commandAvailableTimeSeconds) ||
                !IsCanonicalNonnegative(currentTimeSeconds))
            {
                return MotionInvalid(
                    "LiquidZoneMotion.Time.Invalid",
                    "motion_time",
                    "Motion times must be finite, nonnegative SI seconds without signed zero.");
            }

            if (currentTimeSeconds < lastMotionTimeSeconds)
            {
                return MotionInvalid(
                    "LiquidZoneMotion.Time.Order",
                    "current_time_s",
                    "Current event time may not precede the queue-projected last motion time.");
            }

            bool commandDelaySatisfied = currentTimeSeconds >= commandAvailableTimeSeconds;
            double stateAfter = zone.StateFillFraction;
            double appliedDelta = 0.0;
            double deltaTime = currentTimeSeconds - lastMotionTimeSeconds;

            if (commandDelaySatisfied &&
                zone.Enabled &&
                zone.Mode == LiquidZoneModeV1.RateLimited &&
                deltaTime > 0.0)
            {
                double deltaFill = availableCommandFillFraction - zone.StateFillFraction;
                double maximumTravel = zone.RateLimitPerSecond * deltaTime;
                if (!ContractValidation.IsFinite(deltaFill) || !ContractValidation.IsFinite(maximumTravel))
                {
                    return MotionInvalid(
                        "LiquidZoneMotion.NonFinite",
                        "motion",
                        "Rate-limited motion fails closed on non-finite delta or travel.");
                }

                if (deltaFill > 0.0)
                {
                    appliedDelta = Math.Min(deltaFill, maximumTravel);
                }
                else if (deltaFill < 0.0)
                {
                    appliedDelta = -Math.Min(-deltaFill, maximumTravel);
                }

                if (appliedDelta == 0.0)
                {
                    appliedDelta = 0.0;
                }

                stateAfter = zone.StateFillFraction + appliedDelta;
                if (!IsCanonicalFraction(stateAfter))
                {
                    return MotionInvalid(
                        "LiquidZoneMotion.StateAfter.Invalid",
                        "state_fill_fraction_after",
                        "Rate-limited motion must remain a finite fill fraction in [0,1].");
                }
            }

            LiquidZoneMotionResultV1 result = LiquidZoneMotionResultV1.Create(
                zone,
                commandDelaySatisfied,
                stateAfter,
                availableCommandFillFraction,
                lastMotionTimeSeconds,
                commandAvailableTimeSeconds,
                currentTimeSeconds,
                appliedDelta);
            return ContractValidationResult<LiquidZoneMotionResultV1>.Valid(result);
        }

        private static bool IsCanonicalFraction(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 && value <= 1.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static ContractValidationResult<LiquidZoneOverlayEvaluationV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneOverlayEvaluationV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<LiquidZoneMotionResultV1> MotionInvalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneMotionResultV1>.Invalid(code, path, message);
        }
    }
}
