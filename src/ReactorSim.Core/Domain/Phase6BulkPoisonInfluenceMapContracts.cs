using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit sparse bulk-poison influence entry. The source is the
    /// derived concentration in kg/m^3 and the target is a local m^-1
    /// absorption overlay coefficient.
    /// </summary>
    public sealed class BulkPoisonInfluenceMapEntryV1
    {
        public const uint CurrentSchemaVersion = 1;

        private BulkPoisonInfluenceMapEntryV1(
            StableId poisonSourceId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerKgPerM3,
            string sourceUnit,
            string targetUnit)
        {
            PoisonSourceId = poisonSourceId;
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            WeightMInversePerKgPerM3 = weightMInversePerKgPerM3;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
        }

        public StableId PoisonSourceId { get; }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double WeightMInversePerKgPerM3 { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public static ContractValidationResult<BulkPoisonInfluenceMapEntryV1> TryCreate(
            StableId poisonSourceId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerKgPerM3,
            string? sourceUnit,
            string? targetUnit)
        {
            if (poisonSourceId != BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId)
            {
                return Invalid(
                    "BulkPoisonInfluenceMapEntry.SourceId.Unapproved",
                    "poison_source_id",
                    "Only the owner-approved synthetic poison source identity is admitted.");
            }

            if (groupIndex >= BulkPoisonInfluenceMapV1.GroupCount)
            {
                return Invalid(
                    "BulkPoisonInfluenceMapEntry.GroupIndex.OutOfRange",
                    "group_index",
                    "GroupIndex must be 0 or 1 for the approved synthetic map.");
            }

            double expectedWeight = groupIndex == 0
                ? BulkPoisonInfluenceMapV1.ApprovedGroup0WeightMInversePerKgPerM3
                : BulkPoisonInfluenceMapV1.ApprovedGroup1WeightMInversePerKgPerM3;
            if (!BulkPoisonValidationV1.IsCanonicalPositive(weightMInversePerKgPerM3) ||
                weightMInversePerKgPerM3 != expectedWeight)
            {
                return Invalid(
                    "BulkPoisonInfluenceMapEntry.Weight.Unapproved",
                    "weight_m_inverse_per_kg_per_m3",
                    "The entry weight must equal the owner-approved synthetic positive absorption weight.");
            }

            if (!string.Equals(
                    sourceUnit,
                    BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    targetUnit,
                    BulkPoisonInfluenceMapV1.TargetUnitMInverse,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "BulkPoisonInfluenceMapEntry.Unit.Invalid",
                    "unit",
                    "The approved synthetic map uses kg/m^3 source and m^-1 target units.");
            }

            return ContractValidationResult<BulkPoisonInfluenceMapEntryV1>.Valid(
                new BulkPoisonInfluenceMapEntryV1(
                    poisonSourceId,
                    targetNode,
                    groupIndex,
                    weightMInversePerKgPerM3,
                    sourceUnit!,
                    targetUnit!));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-BULK-POISON-INFLUENCE-MAP-ENTRY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, PoisonSourceId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, WeightMInversePerKgPerM3);
                Phase5CanonicalBytesV1.WriteString(writer, SourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, TargetUnit);
            });
        }

        private static ContractValidationResult<BulkPoisonInfluenceMapEntryV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonInfluenceMapEntryV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// The owner-approved synthetic P6-T04 bulk-poison map. It is a test-only
    /// engine-neutral fixture and is not a production CANDU coefficient,
    /// external-reference result, or golden-data authority.
    /// </summary>
    public sealed class BulkPoisonInfluenceMapV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint GroupCount = 2;
        public const uint TargetNodeCount = 6;
        public const uint EntryCount = TargetNodeCount * GroupCount;
        public const string SchemaId = "CANDU-BULK-POISON-INFLUENCE-MAP-V1";
        public const string ApprovedDataVersion =
            "p6-t04-synthetic-bulk-poison-map-v1";
        public const string ApprovedOwnerId = "Kevin Ho";
        public const string SourceUnitKgPerM3 = "kg/m^3";
        public const string TargetUnitMInverse = "m^-1";
        public const string ApprovedSignCertificate =
            "P6-T04-SYNTHETIC-POSITIVE-ABSORPTION-V1";
        public const string ApprovedNormalization = "None";
        public const double ApprovedModeratorVolumeM3 = 5.0;
        public const double ApprovedReferenceConcentrationKgPerM3 = 0.0;
        public const double ApprovedAddRateKgPerSecond = 0.1;
        public const double ApprovedWithdrawRateKgPerSecond = 0.01;
        public const double ApprovedSetupMaximumMassKg = 1.0;
        public const double ApprovedGroup0WeightMInversePerKgPerM3 = 0.08;
        public const double ApprovedGroup1WeightMInversePerKgPerM3 = 0.05;

        public static readonly StableId ApprovedPoisonSourceId = StableId.Parse(
            "00000000-0000-0000-0000-00000000b405");

        public static readonly StableId ApprovedMapId = StableId.Parse(
            "00000000-0000-0000-0000-00000000b401");

        public static readonly StableId ApprovedTopologyId = StableId.Parse(
            "00000000-0000-0000-0000-00000000b404");

        private BulkPoisonInfluenceMapV1(
            uint schemaVersion,
            StableId poisonSourceId,
            StableId mapId,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            double setupMaximumMassKg,
            Digest32 referenceStateDigest,
            IEnumerable<BulkPoisonInfluenceMapEntryV1> entries,
            Digest32 mapDigest)
        {
            SchemaVersion = schemaVersion;
            PoisonSourceId = poisonSourceId;
            MapId = mapId;
            TopologyId = topologyId;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
            TopologyDigest = topologyDigest;
            DataVersion = dataVersion;
            OwnerId = ownerId;
            SignCertificate = signCertificate;
            Normalization = normalization;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            ModeratorVolumeM3 = moderatorVolumeM3;
            ReferenceConcentrationKgPerM3 = referenceConcentrationKgPerM3;
            AddRateKgPerSecond = addRateKgPerSecond;
            WithdrawRateKgPerSecond = withdrawRateKgPerSecond;
            SetupMaximumMassKg = setupMaximumMassKg;
            ReferenceStateDigest = referenceStateDigest;
            Entries = new ReadOnlyCollection<BulkPoisonInfluenceMapEntryV1>(
                entries.ToArray());
            MapDigest = mapDigest;
        }

        public uint SchemaVersion { get; }

        public StableId PoisonSourceId { get; }

        public StableId MapId { get; }

        public StableId TopologyId { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public Digest32 TopologyDigest { get; }

        public string DataVersion { get; }

        public string OwnerId { get; }

        public string SignCertificate { get; }

        public string Normalization { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public double ModeratorVolumeM3 { get; }

        public double ReferenceConcentrationKgPerM3 { get; }

        public double AddRateKgPerSecond { get; }

        public double WithdrawRateKgPerSecond { get; }

        public double SetupMaximumMassKg { get; }

        public Digest32 ReferenceStateDigest { get; }

        public IReadOnlyList<BulkPoisonInfluenceMapEntryV1> Entries { get; }

        public Digest32 MapDigest { get; }

        public static ContractValidationResult<BulkPoisonInfluenceMapV1> TryCreate(
            uint schemaVersion,
            StableId poisonSourceId,
            StableId mapId,
            StableId topologyId,
            IEnumerable<NodeKey>? targetNodes,
            Digest32? topologyDigest,
            string? dataVersion,
            string? ownerId,
            string? signCertificate,
            string? normalization,
            string? sourceUnit,
            string? targetUnit,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            double setupMaximumMassKg,
            Digest32? referenceStateDigest,
            IEnumerable<BulkPoisonInfluenceMapEntryV1>? entries,
            Digest32? expectedMapDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic P6-T04 influence-map schema version 1 is accepted.");
            }

            if (poisonSourceId != ApprovedPoisonSourceId || mapId != ApprovedMapId ||
                topologyId != ApprovedTopologyId)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Identity.Unapproved",
                    "identity",
                    "The P6-T04 map requires the three owner-approved synthetic identities.");
            }

            if (targetNodes == null)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.TargetNodes.Missing",
                    "target_nodes",
                    "An explicit target-node set is required; array order is never inferred.");
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            if (canonicalTargets.Length != (int)TargetNodeCount)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.TargetNodes.CountMismatch",
                    "target_nodes",
                    "The approved synthetic map requires exactly six explicit target nodes.");
            }

            for (int index = 1; index < canonicalTargets.Length; index++)
            {
                if (canonicalTargets[index - 1] == canonicalTargets[index])
                {
                    return Invalid(
                        "BulkPoisonInfluenceMap.TargetNodes.Duplicate",
                        "target_nodes",
                        "Target nodes must be unique explicit NodeKey values.");
                }
            }

            for (uint channelId = 0; channelId < TargetNodeCount; channelId++)
            {
                NodeKey expectedNode = new NodeKey(
                    new ChannelId(channelId),
                    new BundlePosition(0));
                if (canonicalTargets[(int)channelId] != expectedNode)
                {
                    return Invalid(
                        "BulkPoisonInfluenceMap.TargetNodes.TopologyMismatch",
                        "target_nodes",
                        "The approved topology requires (ChannelId=0..5, BundlePosition=0).");
                }
            }

            if (topologyDigest == null)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.TopologyDigest.Missing",
                    "topology_digest",
                    "The explicit topology digest is required.");
            }

            Digest32 calculatedTopologyDigest = ComputeTopologyDigest(
                topologyId,
                canonicalTargets);
            if (!topologyDigest.Equals(calculatedTopologyDigest))
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.TopologyDigest.Mismatch",
                    "topology_digest",
                    "The topology digest does not equal the canonical target-node topology bytes.");
            }

            if (!string.Equals(dataVersion, ApprovedDataVersion, StringComparison.Ordinal) ||
                !string.Equals(ownerId, ApprovedOwnerId, StringComparison.Ordinal) ||
                !string.Equals(signCertificate, ApprovedSignCertificate, StringComparison.Ordinal) ||
                !string.Equals(normalization, ApprovedNormalization, StringComparison.Ordinal) ||
                !string.Equals(sourceUnit, SourceUnitKgPerM3, StringComparison.Ordinal) ||
                !string.Equals(targetUnit, TargetUnitMInverse, StringComparison.Ordinal))
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Metadata.Unapproved",
                    "metadata",
                    "Map metadata must equal the owner-approved synthetic P6-T04 identity, sign, normalization, and units.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalPositive(moderatorVolumeM3) ||
                moderatorVolumeM3 != ApprovedModeratorVolumeM3)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Volume.Unapproved",
                    "moderator_volume_m3",
                    "The approved moderator volume is exactly 5.0 m^3.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(referenceConcentrationKgPerM3) ||
                referenceConcentrationKgPerM3 != ApprovedReferenceConcentrationKgPerM3)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.ReferenceConcentration.Unapproved",
                    "reference_concentration_kg_per_m3",
                    "The approved reference concentration is exactly 0.0 kg/m^3.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalNonnegative(addRateKgPerSecond) ||
                addRateKgPerSecond != ApprovedAddRateKgPerSecond ||
                !BulkPoisonValidationV1.IsCanonicalNonnegative(withdrawRateKgPerSecond) ||
                withdrawRateKgPerSecond != ApprovedWithdrawRateKgPerSecond)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Rate.Unapproved",
                    "rate",
                    "The approved synthetic rates are exactly 0.1 kg/s add and 0.01 kg/s withdraw.");
            }

            if (!BulkPoisonValidationV1.IsCanonicalPositive(setupMaximumMassKg) ||
                setupMaximumMassKg != ApprovedSetupMaximumMassKg)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.SetupLimit.Unapproved",
                    "setup_maximum_mass_kg",
                    "The approved synthetic prescribed setup limit is exactly 1.0 kg.");
            }

            if (referenceStateDigest == null)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.ReferenceStateDigest.Missing",
                    "reference_state_digest",
                    "The complete reference-state binding digest is required.");
            }

            Digest32 calculatedReferenceStateDigest = ComputeReferenceStateDigest(
                poisonSourceId,
                mapId,
                topologyId,
                topologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                moderatorVolumeM3,
                referenceConcentrationKgPerM3,
                addRateKgPerSecond,
                withdrawRateKgPerSecond,
                setupMaximumMassKg);
            if (!referenceStateDigest.Equals(calculatedReferenceStateDigest))
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.ReferenceStateDigest.Mismatch",
                    "reference_state_digest",
                    "The reference-state digest does not equal the approved concentration, volume, rate, and setup binding.");
            }

            if (entries == null)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Entries.Missing",
                    "entries",
                    "The explicit sparse map entries are required.");
            }

            BulkPoisonInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.PoisonSourceId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            if (canonicalEntries.Length != (int)EntryCount)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.Entries.CountMismatch",
                    "entries",
                    "The approved synthetic map requires exactly twelve entries.");
            }

            for (int index = 1; index < canonicalEntries.Length; index++)
            {
                BulkPoisonInfluenceMapEntryV1 previous = canonicalEntries[index - 1];
                BulkPoisonInfluenceMapEntryV1 current = canonicalEntries[index];
                if (previous.PoisonSourceId == current.PoisonSourceId &&
                    previous.TargetNode == current.TargetNode &&
                    previous.GroupIndex == current.GroupIndex)
                {
                    return Invalid(
                        "BulkPoisonInfluenceMap.Entry.DuplicateKey",
                        "entries",
                        "The poison-source, target-node, and group key must be unique.");
                }
            }

            for (int targetIndex = 0; targetIndex < canonicalTargets.Length; targetIndex++)
            {
                NodeKey target = canonicalTargets[targetIndex];
                for (ushort groupIndex = 0; groupIndex < GroupCount; groupIndex++)
                {
                    BulkPoisonInfluenceMapEntryV1? entry = canonicalEntries
                        .FirstOrDefault(candidate =>
                            candidate.PoisonSourceId == poisonSourceId &&
                            candidate.TargetNode == target &&
                            candidate.GroupIndex == groupIndex);
                    if (entry == null)
                    {
                        return Invalid(
                            "BulkPoisonInfluenceMap.Entry.Missing",
                            "entries",
                            "Every target node requires exactly one entry for each group.");
                    }
                }
            }

            if (expectedMapDigest == null)
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.MapDigest.Missing",
                    "map_digest",
                    "The map digest is required and may not be generated implicitly by a consumer.");
            }

            Digest32 calculatedMapDigest = ComputeDigest(
                schemaVersion,
                poisonSourceId,
                mapId,
                topologyId,
                canonicalTargets,
                topologyDigest,
                dataVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                moderatorVolumeM3,
                referenceConcentrationKgPerM3,
                addRateKgPerSecond,
                withdrawRateKgPerSecond,
                setupMaximumMassKg,
                referenceStateDigest,
                canonicalEntries);
            if (!expectedMapDigest.Equals(calculatedMapDigest))
            {
                return Invalid(
                    "BulkPoisonInfluenceMap.MapDigest.Mismatch",
                    "map_digest",
                    "The supplied map digest does not equal the canonical map identity and entry bytes.");
            }

            return ContractValidationResult<BulkPoisonInfluenceMapV1>.Valid(
                new BulkPoisonInfluenceMapV1(
                    schemaVersion,
                    poisonSourceId,
                    mapId,
                    topologyId,
                    canonicalTargets,
                    topologyDigest,
                    dataVersion!,
                    ownerId!,
                    signCertificate!,
                    normalization!,
                    sourceUnit!,
                    targetUnit!,
                    moderatorVolumeM3,
                    referenceConcentrationKgPerM3,
                    addRateKgPerSecond,
                    withdrawRateKgPerSecond,
                    setupMaximumMassKg,
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
                            "CANDU-BULK-POISON-TOPOLOGY-V1");
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
            StableId poisonSourceId,
            StableId mapId,
            StableId topologyId,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            double setupMaximumMassKg)
        {
            if (topologyDigest == null)
            {
                throw new ArgumentNullException(nameof(topologyDigest));
            }

            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Phase5CanonicalBytesV1.Build(writer =>
                    {
                        Phase5CanonicalBytesV1.WriteAscii(
                            writer,
                            "CANDU-BULK-POISON-REFERENCE-STATE-V1");
                        writer.Write((byte)0);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                        Phase5CanonicalBytesV1.WriteStableId(writer, poisonSourceId);
                        Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                        Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                        Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                        Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                        Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                        Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                        Phase5CanonicalBytesV1.WriteString(writer, normalization);
                        Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                        Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                        Phase5CanonicalBytesV1.WriteDouble(writer, moderatorVolumeM3);
                        Phase5CanonicalBytesV1.WriteDouble(writer, referenceConcentrationKgPerM3);
                        Phase5CanonicalBytesV1.WriteDouble(writer, addRateKgPerSecond);
                        Phase5CanonicalBytesV1.WriteDouble(writer, withdrawRateKgPerSecond);
                        Phase5CanonicalBytesV1.WriteDouble(writer, setupMaximumMassKg);
                    })));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId poisonSourceId,
            StableId mapId,
            StableId topologyId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            double setupMaximumMassKg,
            Digest32 referenceStateDigest,
            IEnumerable<BulkPoisonInfluenceMapEntryV1> entries)
        {
            if (targetNodes == null)
            {
                throw new ArgumentNullException(nameof(targetNodes));
            }

            if (topologyDigest == null)
            {
                throw new ArgumentNullException(nameof(topologyDigest));
            }

            if (referenceStateDigest == null)
            {
                throw new ArgumentNullException(nameof(referenceStateDigest));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            BulkPoisonInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.PoisonSourceId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        schemaVersion,
                        poisonSourceId,
                        mapId,
                        topologyId,
                        canonicalTargets,
                        topologyDigest,
                        dataVersion,
                        ownerId,
                        signCertificate,
                        normalization,
                        sourceUnit,
                        targetUnit,
                        moderatorVolumeM3,
                        referenceConcentrationKgPerM3,
                        addRateKgPerSecond,
                        withdrawRateKgPerSecond,
                        setupMaximumMassKg,
                        referenceStateDigest,
                        canonicalEntries,
                        null)));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                PoisonSourceId,
                MapId,
                TopologyId,
                TargetNodes,
                TopologyDigest,
                DataVersion,
                OwnerId,
                SignCertificate,
                Normalization,
                SourceUnit,
                TargetUnit,
                ModeratorVolumeM3,
                ReferenceConcentrationKgPerM3,
                AddRateKgPerSecond,
                WithdrawRateKgPerSecond,
                SetupMaximumMassKg,
                ReferenceStateDigest,
                Entries,
                MapDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId poisonSourceId,
            StableId mapId,
            StableId topologyId,
            IReadOnlyList<NodeKey> targetNodes,
            Digest32 topologyDigest,
            string dataVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            double moderatorVolumeM3,
            double referenceConcentrationKgPerM3,
            double addRateKgPerSecond,
            double withdrawRateKgPerSecond,
            double setupMaximumMassKg,
            Digest32 referenceStateDigest,
            IReadOnlyList<BulkPoisonInfluenceMapEntryV1> entries,
            Digest32? mapDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, SchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, poisonSourceId);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                Phase5CanonicalBytesV1.WriteString(writer, normalization);
                Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                Phase5CanonicalBytesV1.WriteDouble(writer, moderatorVolumeM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, referenceConcentrationKgPerM3);
                Phase5CanonicalBytesV1.WriteDouble(writer, addRateKgPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, withdrawRateKgPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, setupMaximumMassKg);
                Phase5CanonicalBytesV1.WriteDigest(writer, referenceStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)targetNodes.Count));
                foreach (NodeKey node in targetNodes)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)entries.Count));
                foreach (BulkPoisonInfluenceMapEntryV1 entry in entries)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, entry.ToCanonicalBytes());
                }

                if (mapDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                }
            });
        }

        private static ContractValidationResult<BulkPoisonInfluenceMapV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonInfluenceMapV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// One deterministic node/group local absorption overlay value.
    /// </summary>
    public sealed class BulkPoisonOverlayValueV1
    {
        public const uint CurrentSchemaVersion = 1;

        private BulkPoisonOverlayValueV1(
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

        internal static ContractValidationResult<BulkPoisonOverlayValueV1> TryCreate(
            NodeKey targetNode,
            ushort groupIndex,
            double deltaSigmaAMInverse)
        {
            if (groupIndex >= BulkPoisonInfluenceMapV1.GroupCount)
            {
                return Invalid(
                    "BulkPoisonOverlayValue.GroupIndex.OutOfRange",
                    "group_index",
                    "Overlay group index must be 0 or 1.");
            }

            if (!ContractValidation.IsFinite(deltaSigmaAMInverse) ||
                BitConverter.DoubleToInt64Bits(deltaSigmaAMInverse) == long.MinValue)
            {
                return Invalid(
                    "BulkPoisonOverlayValue.Value.Invalid",
                    "delta_sigma_a_m_inverse",
                    "Overlay values must be finite and may not use signed negative zero.");
            }

            return ContractValidationResult<BulkPoisonOverlayValueV1>.Valid(
                new BulkPoisonOverlayValueV1(
                    targetNode,
                    groupIndex,
                    BulkPoisonValidationV1.NormalizeZero(deltaSigmaAMInverse)));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-BULK-POISON-OVERLAY-VALUE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, DeltaSigmaAMInverse);
            });
        }

        private static ContractValidationResult<BulkPoisonOverlayValueV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonOverlayValueV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete deterministic local overlay projection. It carries an exact
    /// disabled-zero assertion and never mutates either supplied input.
    /// </summary>
    public sealed class BulkPoisonOverlayEvaluationV1
    {
        public const uint CurrentSchemaVersion = 1;

        private BulkPoisonOverlayEvaluationV1(
            StableId poisonSourceId,
            StableId mapId,
            Digest32 mapDigest,
            Digest32 stateDigest,
            IEnumerable<BulkPoisonOverlayValueV1> values,
            IEnumerable<BulkPoisonDisabledZeroAssertionV1> disabledZeroAssertions,
            Digest32 overlayDigest)
        {
            PoisonSourceId = poisonSourceId;
            MapId = mapId;
            MapDigest = mapDigest;
            StateDigest = stateDigest;
            Values = new ReadOnlyCollection<BulkPoisonOverlayValueV1>(values.ToArray());
            DisabledZeroAssertions = new ReadOnlyCollection<BulkPoisonDisabledZeroAssertionV1>(
                disabledZeroAssertions.ToArray());
            OverlayDigest = overlayDigest;
        }

        public StableId PoisonSourceId { get; }

        public StableId MapId { get; }

        public Digest32 MapDigest { get; }

        public Digest32 StateDigest { get; }

        public IReadOnlyList<BulkPoisonOverlayValueV1> Values { get; }

        public IReadOnlyList<BulkPoisonDisabledZeroAssertionV1> DisabledZeroAssertions { get; }

        public Digest32 OverlayDigest { get; }

        internal static BulkPoisonOverlayEvaluationV1 Create(
            StableId poisonSourceId,
            StableId mapId,
            Digest32 mapDigest,
            Digest32 stateDigest,
            IEnumerable<BulkPoisonOverlayValueV1> values,
            IEnumerable<BulkPoisonDisabledZeroAssertionV1> disabledZeroAssertions)
        {
            BulkPoisonOverlayValueV1[] canonicalValues = values
                .OrderBy(value => value.TargetNode)
                .ThenBy(value => value.GroupIndex)
                .ToArray();
            BulkPoisonDisabledZeroAssertionV1[] canonicalAssertions = disabledZeroAssertions
                .OrderBy(assertion => assertion.PoisonSourceId)
                .ToArray();
            Digest32 overlayDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        poisonSourceId,
                        mapId,
                        mapDigest,
                        stateDigest,
                        canonicalValues,
                        canonicalAssertions,
                        null)));
            return new BulkPoisonOverlayEvaluationV1(
                poisonSourceId,
                mapId,
                mapDigest,
                stateDigest,
                canonicalValues,
                canonicalAssertions,
                overlayDigest);
        }

        public ContractValidationResult<BulkPoisonOverlayValueV1> TryGetValue(
            NodeKey targetNode,
            ushort groupIndex)
        {
            BulkPoisonOverlayValueV1? value = Values.FirstOrDefault(candidate =>
                candidate.TargetNode == targetNode && candidate.GroupIndex == groupIndex);
            if (value == null)
            {
                return ContractValidationResult<BulkPoisonOverlayValueV1>.Invalid(
                    "BulkPoisonOverlay.Value.NotFound",
                    "target_node_group",
                    "No overlay value exists for the requested target and group.");
            }

            return ContractValidationResult<BulkPoisonOverlayValueV1>.Valid(value);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                PoisonSourceId,
                MapId,
                MapDigest,
                StateDigest,
                Values,
                DisabledZeroAssertions,
                OverlayDigest);
        }

        private static byte[] BuildBytes(
            StableId poisonSourceId,
            StableId mapId,
            Digest32 mapDigest,
            Digest32 stateDigest,
            IReadOnlyList<BulkPoisonOverlayValueV1> values,
            IReadOnlyList<BulkPoisonDisabledZeroAssertionV1> disabledZeroAssertions,
            Digest32? overlayDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-BULK-POISON-OVERLAY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, poisonSourceId);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
                foreach (BulkPoisonOverlayValueV1 value in values)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)disabledZeroAssertions.Count));
                foreach (BulkPoisonDisabledZeroAssertionV1 assertion in disabledZeroAssertions)
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
    /// Exact-zero diagnostic for a disabled poison branch.
    /// </summary>
    public sealed class BulkPoisonDisabledZeroAssertionV1
    {
        public const uint CurrentSchemaVersion = 1;

        private BulkPoisonDisabledZeroAssertionV1(
            StableId poisonSourceId,
            Digest32 stateDigest,
            double deltaSigmaAMInverse)
        {
            PoisonSourceId = poisonSourceId;
            StateDigest = stateDigest;
            DeltaSigmaAMInverse = deltaSigmaAMInverse;
        }

        public StableId PoisonSourceId { get; }

        public Digest32 StateDigest { get; }

        public double DeltaSigmaAMInverse { get; }

        internal static BulkPoisonDisabledZeroAssertionV1 Create(
            StableId poisonSourceId,
            Digest32 stateDigest)
        {
            return new BulkPoisonDisabledZeroAssertionV1(
                poisonSourceId,
                stateDigest,
                0.0);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-BULK-POISON-DISABLED-ZERO-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, PoisonSourceId);
                Phase5CanonicalBytesV1.WriteDigest(writer, StateDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, DeltaSigmaAMInverse);
            });
        }
    }

    public static class BulkPoisonOverlayV1
    {
        /// <summary>
        /// Validates exact state/map binding and evaluates only the local
        /// absorption overlay. No direct reactivity, queue, or scenario path
        /// exists in this method.
        /// </summary>
        public static ContractValidationResult<BulkPoisonOverlayEvaluationV1> TryEvaluate(
            BulkPoisonInfluenceMapV1? map,
            BulkPoisonStateV1? state)
        {
            if (map == null)
            {
                return Invalid(
                    "BulkPoisonOverlay.Map.Missing",
                    "map",
                    "A validated bulk-poison influence map is required.");
            }

            if (state == null)
            {
                return Invalid(
                    "BulkPoisonOverlay.State.Missing",
                    "state",
                    "A validated bulk-poison state is required.");
            }

            if (state.PoisonSourceId != map.PoisonSourceId ||
                state.InfluenceMapId != map.MapId ||
                !string.Equals(state.DataVersion, map.DataVersion, StringComparison.Ordinal) ||
                !state.InfluenceMapDigest.Equals(map.MapDigest))
            {
                return Invalid(
                    "BulkPoisonOverlay.Binding.Mismatch",
                    "state",
                    "State source, map identity, data version, and map digest must equal the admitted map.");
            }

            if (state.ModeratorVolumeM3 != map.ModeratorVolumeM3 ||
                state.ReferenceConcentrationKgPerM3 != map.ReferenceConcentrationKgPerM3)
            {
                return Invalid(
                    "BulkPoisonOverlay.ReferenceBinding.Mismatch",
                    "state.reference",
                    "State volume and reference concentration must equal the admitted map reference binding.");
            }

            double deltaConcentration =
                state.PoisonMassConcentrationKgPerM3 - map.ReferenceConcentrationKgPerM3;
            if (!ContractValidation.IsFinite(deltaConcentration))
            {
                return Invalid(
                    "BulkPoisonOverlay.Concentration.NonFinite",
                    "delta_concentration_kg_per_m3",
                    "Overlay evaluation fails closed on a non-finite concentration difference.");
            }

            double[,] sums = new double[
                (int)BulkPoisonInfluenceMapV1.TargetNodeCount,
                (int)BulkPoisonInfluenceMapV1.GroupCount];
            List<BulkPoisonDisabledZeroAssertionV1> disabledAssertions =
                new List<BulkPoisonDisabledZeroAssertionV1>();
            if (!state.Enabled)
            {
                disabledAssertions.Add(
                    BulkPoisonDisabledZeroAssertionV1.Create(
                        state.PoisonSourceId,
                        state.StateDigest));
            }

            foreach (BulkPoisonInfluenceMapEntryV1 entry in map.Entries)
            {
                if (!state.Enabled)
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
                        "BulkPoisonOverlay.TargetNode.Unknown",
                        "map.entries",
                        "Every map entry target must be present in the explicit target-node set.");
                }

                double contribution =
                    entry.WeightMInversePerKgPerM3 * deltaConcentration;
                if (!ContractValidation.IsFinite(contribution))
                {
                    return Invalid(
                        "BulkPoisonOverlay.Value.NonFinite",
                        "overlay",
                        "Overlay evaluation fails closed on a non-finite contribution.");
                }

                double sum = sums[targetIndex, entry.GroupIndex] + contribution;
                if (!ContractValidation.IsFinite(sum))
                {
                    return Invalid(
                        "BulkPoisonOverlay.Value.Overflow",
                        "overlay",
                        "Overlay accumulation fails closed on a non-finite sum.");
                }

                sums[targetIndex, entry.GroupIndex] = sum;
            }

            List<BulkPoisonOverlayValueV1> values = new List<BulkPoisonOverlayValueV1>(
                checked((int)(BulkPoisonInfluenceMapV1.TargetNodeCount *
                              BulkPoisonInfluenceMapV1.GroupCount)));
            for (int targetIndex = 0; targetIndex < map.TargetNodes.Count; targetIndex++)
            {
                for (ushort groupIndex = 0;
                     groupIndex < BulkPoisonInfluenceMapV1.GroupCount;
                     groupIndex++)
                {
                    ContractValidationResult<BulkPoisonOverlayValueV1> value =
                        BulkPoisonOverlayValueV1.TryCreate(
                            map.TargetNodes[targetIndex],
                            groupIndex,
                            sums[targetIndex, groupIndex]);
                    if (!value.IsValid)
                    {
                        return Invalid(
                            "BulkPoisonOverlay.Value.Invalid",
                            "overlay",
                            value.FirstDiagnostic.Message);
                    }

                    values.Add(value.Value);
                }
            }

            return ContractValidationResult<BulkPoisonOverlayEvaluationV1>.Valid(
                BulkPoisonOverlayEvaluationV1.Create(
                    state.PoisonSourceId,
                    map.MapId,
                    map.MapDigest,
                    state.StateDigest,
                    values,
                    disabledAssertions));
        }

        private static ContractValidationResult<BulkPoisonOverlayEvaluationV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BulkPoisonOverlayEvaluationV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
