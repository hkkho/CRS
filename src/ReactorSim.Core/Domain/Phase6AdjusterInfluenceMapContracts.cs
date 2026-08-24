using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit sparse adjuster-bank influence entry. The entry records
    /// source and target units so a synthetic weight cannot be reinterpreted
    /// as an untyped coefficient.
    /// </summary>
    public sealed class AdjusterInfluenceMapEntryV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterInfluenceMapEntryV1(
            StableId bankId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerFraction,
            string sourceUnit,
            string targetUnit)
        {
            BankId = bankId;
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            WeightMInversePerFraction = weightMInversePerFraction;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
        }

        public StableId BankId { get; }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double WeightMInversePerFraction { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public static ContractValidationResult<AdjusterInfluenceMapEntryV1> TryCreate(
            StableId bankId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerFraction,
            string? sourceUnit,
            string? targetUnit)
        {
            if (bankId != AdjusterBankGroupingV1.ApprovedBankAId &&
                bankId != AdjusterBankGroupingV1.ApprovedBankBId)
            {
                return Invalid(
                    "AdjusterInfluenceMapEntry.BankId.Unapproved",
                    "bank_id",
                    "Only the two owner-approved synthetic bank identities are admitted.");
            }

            if (groupIndex >= AdjusterInfluenceMapV1.GroupCount)
            {
                return Invalid(
                    "AdjusterInfluenceMapEntry.GroupIndex.OutOfRange",
                    "group_index",
                    "GroupIndex must be 0 or 1 for the approved synthetic map.");
            }

            if (!AdjusterValidationV1.IsCanonicalPositive(weightMInversePerFraction))
            {
                return Invalid(
                    "AdjusterInfluenceMapEntry.Weight.Invalid",
                    "weight_m_inverse_per_fraction",
                    "Synthetic absorption weights must be finite and strictly positive without signed zero.");
            }

            if (!string.Equals(
                    sourceUnit,
                    AdjusterInfluenceMapV1.SourceUnitDimensionless,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    targetUnit,
                    AdjusterInfluenceMapV1.TargetUnitMInverse,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterInfluenceMapEntry.Unit.Invalid",
                    "unit",
                    "The approved synthetic map uses dimensionless source and m^-1 target units.");
            }

            return ContractValidationResult<AdjusterInfluenceMapEntryV1>.Valid(
                new AdjusterInfluenceMapEntryV1(
                    bankId,
                    targetNode,
                    groupIndex,
                    weightMInversePerFraction,
                    sourceUnit!,
                    targetUnit!));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-INFLUENCE-MAP-ENTRY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, BankId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, WeightMInversePerFraction);
                Phase5CanonicalBytesV1.WriteString(writer, SourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, TargetUnit);
            });
        }

        private static ContractValidationResult<AdjusterInfluenceMapEntryV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterInfluenceMapEntryV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// The owner-approved synthetic P6-T03 adjuster influence map. This map
    /// is a test-only engine-neutral fixture; it is not a production CANDU
    /// coefficient or an external-reference or golden-data authority.
    /// </summary>
    public sealed class AdjusterInfluenceMapV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint GroupCount = 2;
        public const uint TargetNodeCount = 6;
        public const uint EntryCount = 12;
        public const string SchemaId = "CANDU-ADJUSTER-INFLUENCE-MAP-V1";
        public const string ApprovedDataVersion =
            "p6-t03-synthetic-adjuster-bank-map-v1";
        public const string ApprovedOwnerId = "Kevin Ho";
        public const string SourceUnitDimensionless = "dimensionless";
        public const string TargetUnitMInverse = "m^-1";
        public const string ApprovedSignCertificate =
            "P6-T03-SYNTHETIC-POSITIVE-ABSORPTION-V1";
        public const string ApprovedNormalization = "None";
        public const double ApprovedReferenceFraction = 0.5;
        public const double ApprovedBankAGroup0Weight = 0.0020;
        public const double ApprovedBankAGroup1Weight = 0.0010;
        public const double ApprovedBankBGroup0Weight = 0.0015;
        public const double ApprovedBankBGroup1Weight = 0.00075;

        public static readonly StableId ApprovedAdjusterSetId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a701");

        public static readonly StableId ApprovedMapId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a702");

        public static readonly StableId ApprovedGroupingId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a703");

        public static readonly StableId ApprovedTopologyId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a704");

        private AdjusterInfluenceMapV1(
            uint schemaVersion,
            StableId adjusterSetId,
            StableId mapId,
            AdjusterBankGroupingV1 grouping,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            IEnumerable<KeyValuePair<StableId, double>> referenceFractions,
            Digest32 referenceStateDigest,
            IEnumerable<AdjusterInfluenceMapEntryV1> entries,
            Digest32 mapDigest)
        {
            SchemaVersion = schemaVersion;
            AdjusterSetId = adjusterSetId;
            MapId = mapId;
            GroupingId = grouping.GroupingId;
            MappingVersion = grouping.MappingVersion;
            GroupingDigest = grouping.MappingDigest;
            Grouping = grouping;
            TopologyId = topologyId;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
            TopologyDigest = topologyDigest;
            DataVersion = dataVersion;
            OwnerId = ownerId;
            SignCertificate = signCertificate;
            Normalization = normalization;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            ReferenceFractionsByBank = new ReadOnlyDictionary<StableId, double>(
                referenceFractions.ToDictionary(pair => pair.Key, pair => pair.Value));
            ReferenceStateDigest = referenceStateDigest;
            Entries = new ReadOnlyCollection<AdjusterInfluenceMapEntryV1>(
                entries.ToArray());
            MapDigest = mapDigest;
        }

        public uint SchemaVersion { get; }

        public StableId AdjusterSetId { get; }

        public StableId MapId { get; }

        public StableId GroupingId { get; }

        public string MappingVersion { get; }

        public Digest32 GroupingDigest { get; }

        public AdjusterBankGroupingV1 Grouping { get; }

        public StableId TopologyId { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public Digest32 TopologyDigest { get; }

        public string DataVersion { get; }

        public string OwnerId { get; }

        public string SignCertificate { get; }

        public string Normalization { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public IReadOnlyDictionary<StableId, double> ReferenceFractionsByBank { get; }

        public Digest32 ReferenceStateDigest { get; }

        public IReadOnlyList<AdjusterInfluenceMapEntryV1> Entries { get; }

        public Digest32 MapDigest { get; }

        public static ContractValidationResult<AdjusterInfluenceMapV1> TryCreate(
            uint schemaVersion,
            StableId adjusterSetId,
            StableId mapId,
            AdjusterBankGroupingV1? grouping,
            StableId topologyId,
            IEnumerable<NodeKey>? targetNodes,
            Digest32? topologyDigest,
            string? dataVersion,
            string? ownerId,
            string? signCertificate,
            string? normalization,
            string? sourceUnit,
            string? targetUnit,
            IEnumerable<KeyValuePair<StableId, double>>? referenceFractions,
            Digest32? referenceStateDigest,
            IEnumerable<AdjusterInfluenceMapEntryV1>? entries,
            Digest32? expectedMapDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "AdjusterInfluenceMap.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic adjuster influence-map schema version 1 is accepted.");
            }

            if (adjusterSetId != ApprovedAdjusterSetId || mapId != ApprovedMapId)
            {
                return Invalid(
                    "AdjusterInfluenceMap.Identity.Unapproved",
                    "identity",
                    "Only the owner-approved synthetic adjuster-set and map identities are admitted.");
            }

            if (grouping == null)
            {
                return Invalid(
                    "AdjusterInfluenceMap.Grouping.Missing",
                    "grouping",
                    "The influence map requires the validated owner-approved grouping.");
            }

            if (grouping.GroupingId != ApprovedGroupingId ||
                !string.Equals(
                    grouping.MappingVersion,
                    AdjusterBankGroupingV1.ApprovedMappingVersion,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterInfluenceMap.Grouping.Unapproved",
                    "grouping",
                    "The map grouping must equal the owner-approved synthetic grouping identity and version.");
            }

            if (topologyId != ApprovedTopologyId || targetNodes == null || topologyDigest == null)
            {
                return Invalid(
                    "AdjusterInfluenceMap.Topology.Missing",
                    "topology",
                    "The map requires the approved topology identity, target set, and digest.");
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            if (canonicalTargets.Length != (int)TargetNodeCount ||
                HasDuplicate(canonicalTargets) ||
                !MatchesApprovedTargets(canonicalTargets))
            {
                return Invalid(
                    "AdjusterInfluenceMap.Topology.Unapproved",
                    "target_nodes",
                    "The synthetic map target set must be exactly channels 0 through 5 at bundle position 0.");
            }

            Digest32 expectedTopologyDigest = ComputeTopologyDigest(
                topologyId,
                canonicalTargets);
            if (!topologyDigest.Equals(expectedTopologyDigest))
            {
                return Invalid(
                    "AdjusterInfluenceMap.TopologyDigest.Mismatch",
                    "topology_digest",
                    "The supplied topology digest does not equal the canonical target-node set.");
            }

            if (!string.Equals(dataVersion, ApprovedDataVersion, StringComparison.Ordinal) ||
                !string.Equals(ownerId, ApprovedOwnerId, StringComparison.Ordinal) ||
                !string.Equals(signCertificate, ApprovedSignCertificate, StringComparison.Ordinal) ||
                !string.Equals(normalization, ApprovedNormalization, StringComparison.Ordinal) ||
                !string.Equals(sourceUnit, SourceUnitDimensionless, StringComparison.Ordinal) ||
                !string.Equals(targetUnit, TargetUnitMInverse, StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterInfluenceMap.Metadata.Unapproved",
                    "metadata",
                    "Synthetic map version, owner, sign, normalization, and units must equal the approved fixture.");
            }

            if (referenceFractions == null || referenceStateDigest == null)
            {
                return Invalid(
                    "AdjusterInfluenceMap.ReferenceState.Missing",
                    "reference_state",
                    "The map requires a complete explicit bank reference state and digest.");
            }

            KeyValuePair<StableId, double>[] references = referenceFractions.ToArray();
            if (references.Length != (int)AdjusterBankGroupingV1.BankCount ||
                references.Any(pair =>
                    pair.Key != AdjusterBankGroupingV1.ApprovedBankAId &&
                    pair.Key != AdjusterBankGroupingV1.ApprovedBankBId) ||
                references.GroupBy(pair => pair.Key).Any(group => group.Count() != 1) ||
                references.Any(pair =>
                    !AdjusterValidationV1.IsCanonicalFraction(pair.Value) ||
                    pair.Value != ApprovedReferenceFraction))
            {
                return Invalid(
                    "AdjusterInfluenceMap.ReferenceState.Invalid",
                    "reference_state",
                    "Both approved banks must have exactly the synthetic reference fraction 0.5.");
            }

            KeyValuePair<StableId, double>[] canonicalReferences = references
                .OrderBy(pair => pair.Key)
                .ToArray();
            Digest32 expectedReferenceDigest = ComputeReferenceStateDigest(
                mapId,
                grouping,
                topologyId,
                expectedTopologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                canonicalReferences);
            if (!referenceStateDigest.Equals(expectedReferenceDigest))
            {
                return Invalid(
                    "AdjusterInfluenceMap.ReferenceStateDigest.Mismatch",
                    "reference_state_digest",
                    "The supplied reference-state digest does not equal the canonical bank reference state.");
            }

            if (entries == null)
            {
                return Invalid(
                    "AdjusterInfluenceMap.Entries.Missing",
                    "entries",
                    "The map requires all twelve explicit bank, target, and group entries.");
            }

            AdjusterInfluenceMapEntryV1[] suppliedEntries = entries.ToArray();
            if (suppliedEntries.Length != (int)EntryCount ||
                suppliedEntries.Any(entry => entry == null))
            {
                return Invalid(
                    "AdjusterInfluenceMap.Entries.CountMismatch",
                    "entries",
                    "The approved synthetic map requires exactly twelve non-null entries.");
            }

            AdjusterInfluenceMapEntryV1[] canonicalEntries = suppliedEntries
                .OrderBy(entry => entry.BankId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            if (!MatchesApprovedEntries(canonicalEntries))
            {
                return Invalid(
                    "AdjusterInfluenceMap.Entries.Unapproved",
                    "entries",
                    "The influence entries must equal the owner-approved positive synthetic table.");
            }

            Digest32 computedMapDigest = ComputeDigest(
                schemaVersion,
                adjusterSetId,
                mapId,
                grouping,
                topologyId,
                canonicalTargets,
                expectedTopologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                canonicalReferences,
                expectedReferenceDigest,
                canonicalEntries);
            if (expectedMapDigest != null && !expectedMapDigest.Equals(computedMapDigest))
            {
                return Invalid(
                    "AdjusterInfluenceMap.MapDigest.Mismatch",
                    "map_digest",
                    "The supplied map digest does not equal the canonical identity, metadata, reference, topology, and entry bytes.");
            }

            return ContractValidationResult<AdjusterInfluenceMapV1>.Valid(
                new AdjusterInfluenceMapV1(
                    schemaVersion,
                    adjusterSetId,
                    mapId,
                    grouping,
                    topologyId,
                    canonicalTargets,
                    expectedTopologyDigest,
                    dataVersion!,
                    ownerId!,
                    signCertificate!,
                    normalization!,
                    sourceUnit!,
                    targetUnit!,
                    canonicalReferences,
                    expectedReferenceDigest,
                    canonicalEntries,
                    computedMapDigest));
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
                            "CANDU-ADJUSTER-TOPOLOGY-V1");
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
            AdjusterBankGroupingV1 grouping,
            StableId topologyId,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            IEnumerable<KeyValuePair<StableId, double>> referenceFractions)
        {
            if (grouping == null)
            {
                throw new ArgumentNullException(nameof(grouping));
            }

            if (referenceFractions == null)
            {
                throw new ArgumentNullException(nameof(referenceFractions));
            }

            KeyValuePair<StableId, double>[] canonicalReferences = referenceFractions
                .OrderBy(pair => pair.Key)
                .ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Phase5CanonicalBytesV1.Build(writer =>
                    {
                        Phase5CanonicalBytesV1.WriteAscii(
                            writer,
                            "CANDU-ADJUSTER-REFERENCE-STATE-V1");
                        writer.Write((byte)0);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                        Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                        Phase5CanonicalBytesV1.WriteStableId(writer, grouping.GroupingId);
                        Phase5CanonicalBytesV1.WriteString(writer, grouping.MappingVersion);
                        Phase5CanonicalBytesV1.WriteDigest(writer, grouping.MappingDigest);
                        Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                        Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                        Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                        Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                        Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                        Phase5CanonicalBytesV1.WriteString(writer, normalization);
                        Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                        Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            checked((uint)canonicalReferences.Length));
                        foreach (KeyValuePair<StableId, double> reference in canonicalReferences)
                        {
                            Phase5CanonicalBytesV1.WriteStableId(writer, reference.Key);
                            Phase5CanonicalBytesV1.WriteDouble(writer, reference.Value);
                        }
                    })));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId adjusterSetId,
            StableId mapId,
            AdjusterBankGroupingV1 grouping,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            IEnumerable<KeyValuePair<StableId, double>> referenceFractions,
            Digest32 referenceStateDigest,
            IEnumerable<AdjusterInfluenceMapEntryV1> entries)
        {
            if (grouping == null || targetNodes == null || referenceFractions == null || entries == null)
            {
                throw new ArgumentNullException(nameof(grouping));
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            KeyValuePair<StableId, double>[] canonicalReferences = referenceFractions
                .OrderBy(pair => pair.Key)
                .ToArray();
            AdjusterInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.BankId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        schemaVersion,
                        adjusterSetId,
                        mapId,
                        grouping,
                        topologyId,
                        canonicalTargets,
                        topologyDigest,
                        dataVersion,
                        ownerId,
                        signCertificate,
                        normalization,
                        sourceUnit,
                        targetUnit,
                        canonicalReferences,
                        referenceStateDigest,
                        canonicalEntries,
                        null)));
        }

        public bool TryGetReferenceFraction(StableId bankId, out double referenceFraction)
        {
            if (ReferenceFractionsByBank.TryGetValue(bankId, out referenceFraction))
            {
                return true;
            }

            referenceFraction = 0.0;
            return false;
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                AdjusterSetId,
                MapId,
                Grouping,
                TopologyId,
                TargetNodes.ToArray(),
                TopologyDigest,
                DataVersion,
                OwnerId,
                SignCertificate,
                Normalization,
                SourceUnit,
                TargetUnit,
                ReferenceFractionsByBank.OrderBy(pair => pair.Key).ToArray(),
                ReferenceStateDigest,
                Entries.ToArray(),
                MapDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId adjusterSetId,
            StableId mapId,
            AdjusterBankGroupingV1 grouping,
            StableId topologyId,
            NodeKey[] targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            KeyValuePair<StableId, double>[] referenceFractions,
            Digest32 referenceStateDigest,
            AdjusterInfluenceMapEntryV1[] entries,
            Digest32? mapDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, SchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, adjusterSetId);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteStableId(writer, grouping.GroupingId);
                Phase5CanonicalBytesV1.WriteString(writer, grouping.MappingVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, grouping.MappingDigest);
                Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                Phase5CanonicalBytesV1.WriteString(writer, normalization);
                Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                Phase5CanonicalBytesV1.WriteDigest(writer, referenceStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)referenceFractions.Length));
                foreach (KeyValuePair<StableId, double> reference in referenceFractions)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, reference.Key);
                    Phase5CanonicalBytesV1.WriteDouble(writer, reference.Value);
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)targetNodes.Length));
                foreach (NodeKey node in targetNodes)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)entries.Length));
                foreach (AdjusterInfluenceMapEntryV1 entry in entries)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, entry.ToCanonicalBytes());
                }

                if (mapDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                }
            });
        }

        private static bool MatchesApprovedTargets(NodeKey[] targets)
        {
            if (targets.Length != (int)TargetNodeCount)
            {
                return false;
            }

            for (uint index = 0; index < TargetNodeCount; index++)
            {
                if (targets[(int)index] != new NodeKey(new ChannelId(index), new BundlePosition(0)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesApprovedEntries(
            AdjusterInfluenceMapEntryV1[] entries)
        {
            if (entries.Length != (int)EntryCount)
            {
                return false;
            }

            for (uint index = 0; index < 3; index++)
            {
                if (!MatchesEntry(
                        entries[(int)(index * 2)],
                        AdjusterBankGroupingV1.ApprovedBankAId,
                        index,
                        0,
                        ApprovedBankAGroup0Weight) ||
                    !MatchesEntry(
                        entries[(int)(index * 2 + 1)],
                        AdjusterBankGroupingV1.ApprovedBankAId,
                        index,
                        1,
                        ApprovedBankAGroup1Weight))
                {
                    return false;
                }
            }

            for (uint index = 0; index < 3; index++)
            {
                uint channel = index + 3;
                int offset = 6 + (int)(index * 2);
                if (!MatchesEntry(
                        entries[offset],
                        AdjusterBankGroupingV1.ApprovedBankBId,
                        channel,
                        0,
                        ApprovedBankBGroup0Weight) ||
                    !MatchesEntry(
                        entries[offset + 1],
                        AdjusterBankGroupingV1.ApprovedBankBId,
                        channel,
                        1,
                        ApprovedBankBGroup1Weight))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesEntry(
            AdjusterInfluenceMapEntryV1 entry,
            StableId bankId,
            uint channel,
            ushort groupIndex,
            double weight)
        {
            return entry.BankId == bankId &&
                   entry.TargetNode == new NodeKey(
                       new ChannelId(channel),
                       new BundlePosition(0)) &&
                   entry.GroupIndex == groupIndex &&
                   entry.WeightMInversePerFraction == weight &&
                   string.Equals(entry.SourceUnit, SourceUnitDimensionless, StringComparison.Ordinal) &&
                   string.Equals(entry.TargetUnit, TargetUnitMInverse, StringComparison.Ordinal);
        }

        private static bool HasDuplicate(NodeKey[] values)
        {
            for (int index = 1; index < values.Length; index++)
            {
                if (values[index - 1] == values[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static ContractValidationResult<AdjusterInfluenceMapV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterInfluenceMapV1>.Invalid(
                code,
                path,
                message);
        }

    }

    /// <summary>
    /// One target-node/group synthetic absorption-overlay value.
    /// </summary>
    public sealed class AdjusterOverlayValueV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterOverlayValueV1(
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

        internal static ContractValidationResult<AdjusterOverlayValueV1> TryCreate(
            NodeKey targetNode,
            ushort groupIndex,
            double deltaSigmaAMInverse)
        {
            if (groupIndex >= AdjusterInfluenceMapV1.GroupCount)
            {
                return ContractValidationResult<AdjusterOverlayValueV1>.Invalid(
                    "AdjusterOverlayValue.GroupIndex.OutOfRange",
                    "group_index",
                    "Overlay group index must be 0 or 1.");
            }

            if (!ContractValidation.IsFinite(deltaSigmaAMInverse) ||
                BitConverter.DoubleToInt64Bits(deltaSigmaAMInverse) == long.MinValue)
            {
                return ContractValidationResult<AdjusterOverlayValueV1>.Invalid(
                    "AdjusterOverlayValue.Value.Invalid",
                    "delta_sigma_a_m_inverse",
                    "Overlay values must be finite and may not use signed negative zero.");
            }

            if (deltaSigmaAMInverse == 0.0)
            {
                deltaSigmaAMInverse = 0.0;
            }

            return ContractValidationResult<AdjusterOverlayValueV1>.Valid(
                new AdjusterOverlayValueV1(
                    targetNode,
                    groupIndex,
                    deltaSigmaAMInverse));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-OVERLAY-V1");
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
    /// Complete deterministic local overlay projection. It records the map
    /// and set-state digests and never mutates either input.
    /// </summary>
    public sealed class AdjusterOverlayEvaluationV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterOverlayEvaluationV1(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 adjusterSetStateDigest,
            IEnumerable<AdjusterOverlayValueV1> values,
            Digest32 overlayDigest)
        {
            MapId = mapId;
            MapDigest = mapDigest;
            AdjusterSetStateDigest = adjusterSetStateDigest;
            Values = new ReadOnlyCollection<AdjusterOverlayValueV1>(values.ToArray());
            OverlayDigest = overlayDigest;
        }

        public StableId MapId { get; }

        public Digest32 MapDigest { get; }

        public Digest32 AdjusterSetStateDigest { get; }

        public IReadOnlyList<AdjusterOverlayValueV1> Values { get; }

        public Digest32 OverlayDigest { get; }

        internal static AdjusterOverlayEvaluationV1 Create(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 adjusterSetStateDigest,
            IEnumerable<AdjusterOverlayValueV1> values)
        {
            AdjusterOverlayValueV1[] canonicalValues = values
                .OrderBy(value => value.TargetNode)
                .ThenBy(value => value.GroupIndex)
                .ToArray();
            Digest32 overlayDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        mapId,
                        mapDigest,
                        adjusterSetStateDigest,
                        canonicalValues,
                        null)));
            return new AdjusterOverlayEvaluationV1(
                mapId,
                mapDigest,
                adjusterSetStateDigest,
                canonicalValues,
                overlayDigest);
        }

        public ContractValidationResult<AdjusterOverlayValueV1> TryGetValue(
            NodeKey targetNode,
            ushort groupIndex)
        {
            AdjusterOverlayValueV1? value = Values.FirstOrDefault(candidate =>
                candidate.TargetNode == targetNode && candidate.GroupIndex == groupIndex);
            return value == null
                ? ContractValidationResult<AdjusterOverlayValueV1>.Invalid(
                    "AdjusterOverlay.Value.Missing",
                    "overlay",
                    "The requested target-node/group overlay value is not present.")
                : ContractValidationResult<AdjusterOverlayValueV1>.Valid(value);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                MapId,
                MapDigest,
                AdjusterSetStateDigest,
                Values,
                OverlayDigest);
        }

        private static byte[] BuildBytes(
            StableId mapId,
            Digest32 mapDigest,
            Digest32 adjusterSetStateDigest,
            IReadOnlyList<AdjusterOverlayValueV1> values,
            Digest32? overlayDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-OVERLAY-EVALUATION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, adjusterSetStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
                foreach (AdjusterOverlayValueV1 value in values)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
                }

                if (overlayDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, overlayDigest);
                }
            });
        }
    }

    public static class AdjusterOverlayV1
    {
        /// <summary>
        /// Evaluates only the approved local absorption overlay. The supplied
        /// set state and every bank remain immutable; there is no queue or
        /// scenario mutation in this operation.
        /// </summary>
        public static ContractValidationResult<AdjusterOverlayEvaluationV1> TryEvaluate(
            AdjusterInfluenceMapV1? map,
            AdjusterSetStateV1? state)
        {
            if (map == null)
            {
                return Invalid(
                    "AdjusterOverlay.Map.Missing",
                    "map",
                    "A validated adjuster influence map is required.");
            }

            if (state == null)
            {
                return Invalid(
                    "AdjusterOverlay.State.Missing",
                    "state",
                    "A validated adjuster set state is required.");
            }

            if (state.AdjusterSetId != map.AdjusterSetId ||
                state.InfluenceMapId != map.MapId ||
                !state.InfluenceMapDigest.Equals(map.MapDigest) ||
                state.Grouping.GroupingId != map.GroupingId ||
                !string.Equals(state.Grouping.MappingVersion, map.MappingVersion, StringComparison.Ordinal) ||
                !state.Grouping.MappingDigest.Equals(map.GroupingDigest) ||
                !string.Equals(state.DataPackVersion, map.DataVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterOverlay.Binding.Mismatch",
                    "state",
                    "The set state must bind the admitted synthetic map, grouping, version, and digest.");
            }

            if (state.Banks.Count != (int)AdjusterBankGroupingV1.BankCount)
            {
                return Invalid(
                    "AdjusterOverlay.Banks.CountMismatch",
                    "state.banks",
                    "Overlay evaluation requires both approved bank states.");
            }

            Dictionary<StableId, AdjusterBankStateV1> banks = state.Banks
                .ToDictionary(bank => bank.BankId, bank => bank);
            if (!banks.ContainsKey(AdjusterBankGroupingV1.ApprovedBankAId) ||
                !banks.ContainsKey(AdjusterBankGroupingV1.ApprovedBankBId))
            {
                return Invalid(
                    "AdjusterOverlay.Banks.IdentityMismatch",
                    "state.banks",
                    "Overlay evaluation requires the approved bank A and B identities.");
            }

            double[,] sums = new double[
                (int)AdjusterInfluenceMapV1.TargetNodeCount,
                (int)AdjusterInfluenceMapV1.GroupCount];
            foreach (AdjusterInfluenceMapEntryV1 entry in map.Entries)
            {
                if (!banks.TryGetValue(entry.BankId, out AdjusterBankStateV1? bank))
                {
                    return Invalid(
                        "AdjusterOverlay.EntryBank.Missing",
                        "map.entries",
                        "Every map entry must bind an approved bank state.");
                }

                int targetIndex = -1;
                for (int index = 0; index < map.TargetNodes.Count; index++)
                {
                    if (map.TargetNodes[index] == entry.TargetNode)
                    {
                        targetIndex = index;
                        break;
                    }
                }

                if (targetIndex < 0)
                {
                    return Invalid(
                        "AdjusterOverlay.TargetNode.Unknown",
                        "map.entries",
                        "Every map entry target must be present in the admitted topology.");
                }

                if (!bank.Enabled)
                {
                    continue;
                }

                if (!map.TryGetReferenceFraction(bank.BankId, out double referenceFraction) ||
                    bank.ReferenceFraction != referenceFraction)
                {
                    return Invalid(
                        "AdjusterOverlay.ReferenceFraction.Mismatch",
                        "state.banks",
                        "Every enabled bank must bind the admitted reference fraction.");
                }

                double deltaFraction = bank.StateFraction - referenceFraction;
                double contribution = entry.WeightMInversePerFraction * deltaFraction;
                if (!ContractValidation.IsFinite(deltaFraction) ||
                    !ContractValidation.IsFinite(contribution))
                {
                    return Invalid(
                        "AdjusterOverlay.Value.NonFinite",
                        "overlay",
                        "Overlay aggregation fails closed on non-finite fraction differences or contributions.");
                }

                double sum = sums[targetIndex, entry.GroupIndex] + contribution;
                if (!ContractValidation.IsFinite(sum))
                {
                    return Invalid(
                        "AdjusterOverlay.Value.Overflow",
                        "overlay",
                        "Overlay aggregation fails closed on non-finite accumulated values.");
                }

                sums[targetIndex, entry.GroupIndex] = sum;
            }

            List<AdjusterOverlayValueV1> values = new List<AdjusterOverlayValueV1>(
                checked((int)(AdjusterInfluenceMapV1.TargetNodeCount *
                              AdjusterInfluenceMapV1.GroupCount)));
            for (int targetIndex = 0; targetIndex < map.TargetNodes.Count; targetIndex++)
            {
                for (ushort groupIndex = 0;
                     groupIndex < AdjusterInfluenceMapV1.GroupCount;
                     groupIndex++)
                {
                    ContractValidationResult<AdjusterOverlayValueV1> value =
                        AdjusterOverlayValueV1.TryCreate(
                            map.TargetNodes[targetIndex],
                            groupIndex,
                            sums[targetIndex, groupIndex]);
                    if (!value.IsValid)
                    {
                        return Invalid(
                            "AdjusterOverlay.Value.Invalid",
                            "overlay",
                            value.FirstDiagnostic.Message);
                    }

                    values.Add(value.Value);
                }
            }

            return ContractValidationResult<AdjusterOverlayEvaluationV1>.Valid(
                AdjusterOverlayEvaluationV1.Create(
                    map.MapId,
                    map.MapDigest,
                    state.StateDigest,
                    values));
        }

        private static ContractValidationResult<AdjusterOverlayEvaluationV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterOverlayEvaluationV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
