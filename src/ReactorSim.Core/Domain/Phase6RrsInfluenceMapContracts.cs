using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One sparse local absorption-map entry. The entry carries explicit units,
    /// map version, and reference-state binding; no normalization is inferred.
    /// </summary>
    public sealed class RrsInfluenceMapEntryV1
    {
        public const uint CurrentSchemaVersion = 1;

        private RrsInfluenceMapEntryV1(
            StableId actuatorId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerActuatorUnit,
            string sourceUnit,
            string targetUnit,
            double referenceActuatorState,
            string mapVersion,
            Digest32 referenceStateDigest)
        {
            ActuatorId = actuatorId;
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            WeightMInversePerActuatorUnit = weightMInversePerActuatorUnit;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            ReferenceActuatorState = referenceActuatorState;
            MapVersion = mapVersion;
            ReferenceStateDigest = referenceStateDigest;
        }

        public StableId ActuatorId { get; }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double WeightMInversePerActuatorUnit { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public double ReferenceActuatorState { get; }

        public string MapVersion { get; }

        public Digest32 ReferenceStateDigest { get; }

        public static ContractValidationResult<RrsInfluenceMapEntryV1> TryCreate(
            StableId actuatorId,
            NodeKey targetNode,
            ushort groupIndex,
            double weightMInversePerActuatorUnit,
            string? sourceUnit,
            string? targetUnit,
            double referenceActuatorState,
            string? mapVersion,
            Digest32? referenceStateDigest)
        {
            if (!RrsValidationV1.IsKnownActuator(actuatorId))
            {
                return Invalid(
                    "RrsInfluenceMapEntry.ActuatorId.Unapproved",
                    "actuator_id",
                    "Only the two owner-approved synthetic actuator identities are accepted.");
            }

            if (!RrsFixtureV1.TargetNodes().Contains(targetNode))
            {
                return Invalid(
                    "RrsInfluenceMapEntry.TargetNode.Unapproved",
                    "target_node",
                    "The sparse map target must be one of the six explicit synthetic nodes.");
            }

            if (groupIndex >= RrsFixtureV1.GroupCount)
            {
                return Invalid(
                    "RrsInfluenceMapEntry.Group.Unsupported",
                    "group_index",
                    "Only groups 0 and 1 are approved for the synthetic fixture.");
            }

            if (!RrsValidationV1.IsCanonicalSignedGain(weightMInversePerActuatorUnit))
            {
                return Invalid(
                    "RrsInfluenceMapEntry.Weight.NonFinite",
                    "weight",
                    "Map weights must be finite and free of signed zero.");
            }

            double expectedWeight = ExpectedWeight(actuatorId, targetNode, groupIndex);
            if (weightMInversePerActuatorUnit != expectedWeight)
            {
                return Invalid(
                    "RrsInfluenceMapEntry.Weight.Unapproved",
                    "weight",
                    "The supplied weight does not equal the approved synthetic sign-test entry.");
            }

            if (!string.Equals(sourceUnit, RrsFixtureV1.SourceUnit, StringComparison.Ordinal) ||
                !string.Equals(targetUnit, RrsFixtureV1.TargetUnit, StringComparison.Ordinal) ||
                !string.Equals(mapVersion, RrsFixtureV1.ApprovedMapVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "RrsInfluenceMapEntry.UnitsOrVersion.Unapproved",
                    "metadata",
                    "Every entry requires source unit 1, target unit m^-1, and the approved map version.");
            }

            if (!RrsValidationV1.IsCanonicalFraction(referenceActuatorState) ||
                referenceActuatorState != RrsFixtureV1.InitialActuatorState ||
                referenceStateDigest == null ||
                !referenceStateDigest.Equals(RrsFixtureV1.ApprovedReferenceStateDigest))
            {
                return Invalid(
                    "RrsInfluenceMapEntry.ReferenceState.Invalid",
                    "reference_state",
                    "Every entry requires the approved 0.5 reference actuator state and digest.");
            }

            return ContractValidationResult<RrsInfluenceMapEntryV1>.Valid(
                new RrsInfluenceMapEntryV1(
                    actuatorId,
                    targetNode,
                    groupIndex,
                    weightMInversePerActuatorUnit,
                    sourceUnit!,
                    targetUnit!,
                    referenceActuatorState,
                    mapVersion!,
                    referenceStateDigest));
        }

        public static double ExpectedWeight(
            StableId actuatorId,
            NodeKey targetNode,
            ushort groupIndex)
        {
            if (actuatorId == RrsFixtureV1.TotalPowerActuatorId)
            {
                return groupIndex == 0
                    ? RrsFixtureV1.TotalPowerGroup0WeightMInverse
                    : RrsFixtureV1.TotalPowerGroup1WeightMInverse;
            }

            bool left = targetNode.ChannelId.Value < 3;
            if (left)
            {
                return groupIndex == 0
                    ? RrsFixtureV1.TiltLeftGroup0WeightMInverse
                    : RrsFixtureV1.TiltLeftGroup1WeightMInverse;
            }

            return groupIndex == 0
                ? RrsFixtureV1.TiltRightGroup0WeightMInverse
                : RrsFixtureV1.TiltRightGroup1WeightMInverse;
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-INFLUENCE-MAP-ENTRY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, WeightMInversePerActuatorUnit);
                Phase5CanonicalBytesV1.WriteString(writer, SourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, TargetUnit);
                Phase5CanonicalBytesV1.WriteDouble(writer, ReferenceActuatorState);
                Phase5CanonicalBytesV1.WriteString(writer, MapVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, ReferenceStateDigest);
            });
        }

        private static ContractValidationResult<RrsInfluenceMapEntryV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsInfluenceMapEntryV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Owner-approved synthetic P6-T05 two-actuator, two-group sparse map.
    /// It is local overlay evidence only and is not a direct reactivity path.
    /// </summary>
    public sealed class RrsInfluenceMapV1
    {
        public const uint CurrentSchemaVersion = 1;

        private RrsInfluenceMapV1(
            uint schemaVersion,
            StableId controllerId,
            StableId mapId,
            StableId topologyId,
            StableId regionSetId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            Digest32 regionSetDigest,
            string dataVersion,
            string mapVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            RrsControlPolarityV1 controlPolarity,
            IEnumerable<KeyValuePair<StableId, double>> referenceActuatorStates,
            Digest32 referenceStateDigest,
            IEnumerable<RrsInfluenceMapEntryV1> entries,
            Digest32 mapDigest)
        {
            SchemaVersion = schemaVersion;
            ControllerId = controllerId;
            MapId = mapId;
            TopologyId = topologyId;
            RegionSetId = regionSetId;
            TargetNodes = new ReadOnlyCollection<NodeKey>(targetNodes.ToArray());
            TopologyDigest = topologyDigest;
            RegionSetDigest = regionSetDigest;
            DataVersion = dataVersion;
            MapVersion = mapVersion;
            OwnerId = ownerId;
            SignCertificate = signCertificate;
            Normalization = normalization;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            ControlPolarity = controlPolarity;
            ReferenceActuatorStates = new ReadOnlyDictionary<StableId, double>(
                referenceActuatorStates.ToDictionary(pair => pair.Key, pair => pair.Value));
            ReferenceStateDigest = referenceStateDigest;
            Entries = new ReadOnlyCollection<RrsInfluenceMapEntryV1>(entries.ToArray());
            MapDigest = mapDigest;
        }

        public uint SchemaVersion { get; }

        public StableId ControllerId { get; }

        public StableId MapId { get; }

        public StableId TopologyId { get; }

        public StableId RegionSetId { get; }

        public IReadOnlyList<NodeKey> TargetNodes { get; }

        public Digest32 TopologyDigest { get; }

        public Digest32 RegionSetDigest { get; }

        public string DataVersion { get; }

        public string MapVersion { get; }

        public string OwnerId { get; }

        public string SignCertificate { get; }

        public string Normalization { get; }

        public string SourceUnit { get; }

        public string TargetUnit { get; }

        public RrsControlPolarityV1 ControlPolarity { get; }

        public IReadOnlyDictionary<StableId, double> ReferenceActuatorStates { get; }

        public Digest32 ReferenceStateDigest { get; }

        public IReadOnlyList<RrsInfluenceMapEntryV1> Entries { get; }

        public Digest32 MapDigest { get; }

        public static ContractValidationResult<RrsInfluenceMapV1> TryCreate(
            uint schemaVersion,
            StableId controllerId,
            StableId mapId,
            StableId topologyId,
            StableId regionSetId,
            IEnumerable<NodeKey>? targetNodes,
            Digest32? topologyDigest,
            Digest32? regionSetDigest,
            string? dataVersion,
            string? mapVersion,
            string? ownerId,
            string? signCertificate,
            string? normalization,
            string? sourceUnit,
            string? targetUnit,
            RrsControlPolarityV1 controlPolarity,
            IEnumerable<KeyValuePair<StableId, double>>? referenceActuatorStates,
            Digest32? referenceStateDigest,
            IEnumerable<RrsInfluenceMapEntryV1>? entries,
            Digest32? expectedMapDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "RrsInfluenceMap.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic RRS influence-map schema version 1 is accepted.");
            }

            if (controllerId != RrsFixtureV1.ControllerId ||
                mapId != RrsFixtureV1.MapId ||
                topologyId != RrsFixtureV1.TopologyId ||
                regionSetId != RrsFixtureV1.RegionSetId)
            {
                return Invalid(
                    "RrsInfluenceMap.Identity.Unapproved",
                    "identity",
                    "The approved synthetic controller, map, topology, and region-set identities are required.");
            }

            if (targetNodes == null || topologyDigest == null || regionSetDigest == null ||
                referenceActuatorStates == null || referenceStateDigest == null || entries == null)
            {
                return Invalid(
                    "RrsInfluenceMap.CompleteState.Missing",
                    "map",
                    "The complete target, binding, reference-state, and sparse-entry map is required.");
            }

            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            if (!canonicalTargets.SequenceEqual(RrsFixtureV1.TargetNodes()))
            {
                return Invalid(
                    "RrsInfluenceMap.TargetNodes.Unapproved",
                    "target_nodes",
                    "The six explicit synthetic target nodes are required.");
            }

            Digest32 calculatedTopology = RrsRegionSetV1.ComputeTopologyDigest(
                topologyId,
                canonicalTargets);
            if (!topologyDigest.Equals(calculatedTopology) ||
                !topologyDigest.Equals(RrsFixtureV1.ApprovedTopologyDigest))
            {
                return Invalid(
                    "RrsInfluenceMap.TopologyDigest.Mismatch",
                    "topology_digest",
                    "The map topology digest does not equal the explicit target-node list.");
            }

            if (!string.Equals(dataVersion, RrsFixtureV1.ApprovedDataVersion, StringComparison.Ordinal) ||
                !string.Equals(mapVersion, RrsFixtureV1.ApprovedMapVersion, StringComparison.Ordinal) ||
                !string.Equals(ownerId, RrsFixtureV1.OwnerId, StringComparison.Ordinal) ||
                !string.Equals(signCertificate, RrsFixtureV1.SignCertificate, StringComparison.Ordinal) ||
                !string.Equals(normalization, RrsFixtureV1.Normalization, StringComparison.Ordinal) ||
                !string.Equals(sourceUnit, RrsFixtureV1.SourceUnit, StringComparison.Ordinal) ||
                !string.Equals(targetUnit, RrsFixtureV1.TargetUnit, StringComparison.Ordinal) ||
                controlPolarity != RrsControlPolarityV1.NegativeFeedback)
            {
                return Invalid(
                    "RrsInfluenceMap.Metadata.Unapproved",
                    "metadata",
                    "The map metadata, units, polarity, and sign certificate must equal the approved fixture.");
            }

            KeyValuePair<StableId, double>[] references = referenceActuatorStates
                .OrderBy(pair => pair.Key)
                .ToArray();
            if (references.Length != 2 ||
                references[0].Key != RrsFixtureV1.TotalPowerActuatorId ||
                references[1].Key != RrsFixtureV1.TiltActuatorId ||
                references.Any(pair =>
                    !RrsValidationV1.IsCanonicalFraction(pair.Value) ||
                    pair.Value != RrsFixtureV1.InitialActuatorState))
            {
                return Invalid(
                    "RrsInfluenceMap.ReferenceState.Unapproved",
                    "reference_state",
                    "Both approved actuators require the exact 0.5 reference state.");
            }

            Digest32 calculatedReference = ComputeReferenceStateDigest(
                controllerId,
                mapId,
                topologyId,
                regionSetId,
                calculatedTopology,
                dataVersion!,
                mapVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                controlPolarity,
                references);
            if (!referenceStateDigest.Equals(calculatedReference) ||
                !referenceStateDigest.Equals(RrsFixtureV1.ApprovedReferenceStateDigest))
            {
                return Invalid(
                    "RrsInfluenceMap.ReferenceStateDigest.Mismatch",
                    "reference_state_digest",
                    "The feedback reference-state digest does not equal the canonical controller/map binding.");
            }

            RrsInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.ActuatorId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            if (canonicalEntries.Length != RrsFixtureV1.MapEntryCount)
            {
                return Invalid(
                    "RrsInfluenceMap.Entries.CountMismatch",
                    "entries",
                    "The approved map requires exactly 24 entries.");
            }

            for (int index = 1; index < canonicalEntries.Length; index++)
            {
                RrsInfluenceMapEntryV1 previous = canonicalEntries[index - 1];
                RrsInfluenceMapEntryV1 current = canonicalEntries[index];
                if (previous.ActuatorId == current.ActuatorId &&
                    previous.TargetNode == current.TargetNode &&
                    previous.GroupIndex == current.GroupIndex)
                {
                    return Invalid(
                        "RrsInfluenceMap.Entry.DuplicateKey",
                        "entries",
                        "The actuator, explicit target node, and group key must be unique.");
                }
            }

            foreach (StableId actuatorId in new[]
            {
                RrsFixtureV1.TotalPowerActuatorId,
                RrsFixtureV1.TiltActuatorId
            })
            {
                foreach (NodeKey node in canonicalTargets)
                {
                    for (ushort group = 0; group < RrsFixtureV1.GroupCount; group++)
                    {
                        RrsInfluenceMapEntryV1? entry = canonicalEntries.FirstOrDefault(candidate =>
                            candidate.ActuatorId == actuatorId &&
                            candidate.TargetNode == node &&
                            candidate.GroupIndex == group);
                        if (entry == null ||
                            entry.ReferenceStateDigest == null ||
                            !entry.ReferenceStateDigest.Equals(referenceStateDigest) ||
                            !string.Equals(entry.MapVersion, mapVersion, StringComparison.Ordinal) ||
                            entry.SourceUnit != sourceUnit || entry.TargetUnit != targetUnit ||
                            entry.WeightMInversePerActuatorUnit !=
                                RrsInfluenceMapEntryV1.ExpectedWeight(actuatorId, node, group))
                        {
                            return Invalid(
                                "RrsInfluenceMap.Entry.Content.Unapproved",
                                "entries",
                                "Every approved actuator, node, and group combination must have the exact signed entry.");
                        }
                    }
                }
            }

            if (expectedMapDigest == null)
            {
                return Invalid(
                    "RrsInfluenceMap.MapDigest.Missing",
                    "map_digest",
                    "The complete map digest is required.");
            }

            if (!regionSetDigest.Equals(RrsFixtureV1.ApprovedRegionSetDigest))
            {
                return Invalid(
                    "RrsInfluenceMap.RegionSetDigest.Unapproved",
                    "region_set_digest",
                    "The map must bind the approved complete region-set digest.");
            }

            Digest32 calculatedMap = ComputeMapDigest(
                schemaVersion,
                controllerId,
                mapId,
                topologyId,
                regionSetId,
                canonicalTargets,
                calculatedTopology,
                regionSetDigest,
                dataVersion!,
                mapVersion!,
                ownerId!,
                signCertificate!,
                normalization!,
                sourceUnit!,
                targetUnit!,
                controlPolarity,
                references,
                referenceStateDigest,
                canonicalEntries);
            if (!expectedMapDigest.Equals(calculatedMap) ||
                !expectedMapDigest.Equals(RrsFixtureV1.ApprovedMapDigest))
            {
                return Invalid(
                    "RrsInfluenceMap.MapDigest.Mismatch",
                    "map_digest",
                    "The supplied map digest does not equal the canonical map body.");
            }

            return ContractValidationResult<RrsInfluenceMapV1>.Valid(
                new RrsInfluenceMapV1(
                    schemaVersion,
                    controllerId,
                    mapId,
                    topologyId,
                    regionSetId,
                    canonicalTargets,
                    calculatedTopology,
                    regionSetDigest,
                    dataVersion!,
                    mapVersion!,
                    ownerId!,
                    signCertificate!,
                    normalization!,
                    sourceUnit!,
                    targetUnit!,
                    controlPolarity,
                    references,
                    referenceStateDigest,
                    canonicalEntries,
                    expectedMapDigest));
        }

        public static Digest32 ComputeReferenceStateDigest(
            StableId controllerId,
            StableId mapId,
            StableId topologyId,
            StableId regionSetId,
            Digest32 topologyDigest,
            string dataVersion,
            string mapVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            RrsControlPolarityV1 controlPolarity,
            IEnumerable<KeyValuePair<StableId, double>> referenceActuatorStates)
        {
            KeyValuePair<StableId, double>[] references = referenceActuatorStates
                .OrderBy(pair => pair.Key)
                .ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-FEEDBACK-REFERENCE-STATE-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteStableId(writer, controllerId);
                    Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                    Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                    Phase5CanonicalBytesV1.WriteStableId(writer, regionSetId);
                    Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                    Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, mapVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                    Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                    Phase5CanonicalBytesV1.WriteString(writer, normalization);
                    Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                    Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                    writer.Write((byte)controlPolarity);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)references.Length));
                    foreach (KeyValuePair<StableId, double> reference in references)
                    {
                        Phase5CanonicalBytesV1.WriteStableId(writer, reference.Key);
                        Phase5CanonicalBytesV1.WriteDouble(writer, reference.Value);
                    }
                })));
        }

        public static Digest32 ComputeMapDigest(
            uint schemaVersion,
            StableId controllerId,
            StableId mapId,
            StableId topologyId,
            StableId regionSetId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            Digest32 regionSetDigest,
            string dataVersion,
            string mapVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            RrsControlPolarityV1 controlPolarity,
            IEnumerable<KeyValuePair<StableId, double>> referenceActuatorStates,
            Digest32 referenceStateDigest,
            IEnumerable<RrsInfluenceMapEntryV1> entries)
        {
            NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
            KeyValuePair<StableId, double>[] references = referenceActuatorStates
                .OrderBy(pair => pair.Key)
                .ToArray();
            RrsInfluenceMapEntryV1[] canonicalEntries = entries
                .OrderBy(entry => entry.ActuatorId)
                .ThenBy(entry => entry.TargetNode)
                .ThenBy(entry => entry.GroupIndex)
                .ToArray();
            return new Digest32(Phase5CanonicalBytesV1.Sha256(
                BuildBytes(
                    schemaVersion,
                    controllerId,
                    mapId,
                    topologyId,
                    regionSetId,
                    canonicalTargets,
                    topologyDigest,
                    regionSetDigest,
                    dataVersion,
                    mapVersion,
                    ownerId,
                    signCertificate,
                    normalization,
                    sourceUnit,
                    targetUnit,
                    controlPolarity,
                    references,
                    referenceStateDigest,
                    canonicalEntries,
                    null)));
        }

        public bool TryGetReferenceState(StableId actuatorId, out double referenceState)
        {
            return ReferenceActuatorStates.TryGetValue(actuatorId, out referenceState);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                ControllerId,
                MapId,
                TopologyId,
                RegionSetId,
                TargetNodes,
                TopologyDigest,
                RegionSetDigest,
                DataVersion,
                MapVersion,
                OwnerId,
                SignCertificate,
                Normalization,
                SourceUnit,
                TargetUnit,
                ControlPolarity,
                ReferenceActuatorStates,
                ReferenceStateDigest,
                Entries,
                MapDigest);
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId controllerId,
            StableId mapId,
            StableId topologyId,
            StableId regionSetId,
            IEnumerable<NodeKey> targetNodes,
            Digest32 topologyDigest,
            Digest32 regionSetDigest,
            string dataVersion,
            string mapVersion,
            string ownerId,
            string signCertificate,
            string normalization,
            string sourceUnit,
            string targetUnit,
            RrsControlPolarityV1 controlPolarity,
            IEnumerable<KeyValuePair<StableId, double>> referenceActuatorStates,
            Digest32 referenceStateDigest,
            IEnumerable<RrsInfluenceMapEntryV1> entries,
            Digest32? mapDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, RrsFixtureV1.MapSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, schemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, controllerId);
                Phase5CanonicalBytesV1.WriteStableId(writer, mapId);
                Phase5CanonicalBytesV1.WriteStableId(writer, topologyId);
                Phase5CanonicalBytesV1.WriteStableId(writer, regionSetId);
                NodeKey[] canonicalTargets = targetNodes.OrderBy(node => node).ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonicalTargets.Length));
                foreach (NodeKey node in canonicalTargets)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
                }

                Phase5CanonicalBytesV1.WriteDigest(writer, topologyDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, regionSetDigest);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteString(writer, mapVersion);
                Phase5CanonicalBytesV1.WriteString(writer, ownerId);
                Phase5CanonicalBytesV1.WriteString(writer, signCertificate);
                Phase5CanonicalBytesV1.WriteString(writer, normalization);
                Phase5CanonicalBytesV1.WriteString(writer, sourceUnit);
                Phase5CanonicalBytesV1.WriteString(writer, targetUnit);
                writer.Write((byte)controlPolarity);

                KeyValuePair<StableId, double>[] references = referenceActuatorStates
                    .OrderBy(pair => pair.Key)
                    .ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)references.Length));
                foreach (KeyValuePair<StableId, double> reference in references)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, reference.Key);
                    Phase5CanonicalBytesV1.WriteDouble(writer, reference.Value);
                }

                Phase5CanonicalBytesV1.WriteDigest(writer, referenceStateDigest);
                RrsInfluenceMapEntryV1[] canonicalEntries = entries
                    .OrderBy(entry => entry.ActuatorId)
                    .ThenBy(entry => entry.TargetNode)
                    .ThenBy(entry => entry.GroupIndex)
                    .ToArray();
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)canonicalEntries.Length));
                foreach (RrsInfluenceMapEntryV1 entry in canonicalEntries)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, entry.ToCanonicalBytes());
                }

                if (mapDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, mapDigest);
                }
            });
        }

        private static ContractValidationResult<RrsInfluenceMapV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsInfluenceMapV1>.Invalid(code, path, message);
        }
    }

    public sealed class RrsActuatorSnapshotV1
    {
        private RrsActuatorSnapshotV1(
            StableId actuatorId,
            double referenceState,
            double state)
        {
            ActuatorId = actuatorId;
            ReferenceState = referenceState;
            State = state;
        }

        public StableId ActuatorId { get; }

        public double ReferenceState { get; }

        public double State { get; }

        public static ContractValidationResult<RrsActuatorSnapshotV1> TryCreate(
            StableId actuatorId,
            double referenceState,
            double state)
        {
            if (!RrsValidationV1.IsKnownActuator(actuatorId) ||
                !RrsValidationV1.IsCanonicalFraction(referenceState) ||
                referenceState != RrsFixtureV1.InitialActuatorState ||
                !RrsValidationV1.IsCanonicalFraction(state))
            {
                return Invalid(
                    "RrsActuatorSnapshot.State.Invalid",
                    "state",
                    "Overlay snapshots require an approved actuator, reference 0.5, and bounded state.");
            }

            return ContractValidationResult<RrsActuatorSnapshotV1>.Valid(
                new RrsActuatorSnapshotV1(actuatorId, referenceState, state));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-ACTUATOR-SNAPSHOT-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteStableId(writer, ActuatorId);
                Phase5CanonicalBytesV1.WriteDouble(writer, ReferenceState);
                Phase5CanonicalBytesV1.WriteDouble(writer, State);
            });
        }

        private static ContractValidationResult<RrsActuatorSnapshotV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsActuatorSnapshotV1>.Invalid(code, path, message);
        }
    }

    public sealed class RrsOverlayValueV1
    {
        private RrsOverlayValueV1(NodeKey targetNode, ushort groupIndex, double deltaSigmaAMInverse)
        {
            TargetNode = targetNode;
            GroupIndex = groupIndex;
            DeltaSigmaAMInverse = deltaSigmaAMInverse;
        }

        public NodeKey TargetNode { get; }

        public ushort GroupIndex { get; }

        public double DeltaSigmaAMInverse { get; }

        internal static RrsOverlayValueV1 Create(
            NodeKey targetNode,
            ushort groupIndex,
            double value)
        {
            return new RrsOverlayValueV1(targetNode, groupIndex, value);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-OVERLAY-VALUE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
                Phase5CanonicalBytesV1.WriteUInt16(writer, GroupIndex);
                Phase5CanonicalBytesV1.WriteDouble(writer, DeltaSigmaAMInverse);
            });
        }
    }

    public sealed class RrsOverlayEvaluationV1
    {
        internal RrsOverlayEvaluationV1(
            bool enabled,
            IEnumerable<RrsOverlayValueV1> values,
            Digest32 mapDigest,
            Digest32 referenceStateDigest,
            Digest32 overlayDigest)
        {
            Enabled = enabled;
            Values = new ReadOnlyCollection<RrsOverlayValueV1>(values.ToArray());
            MapDigest = mapDigest;
            ReferenceStateDigest = referenceStateDigest;
            OverlayDigest = overlayDigest;
            DisabledZeroAssertion = !enabled && Values.All(value =>
                value.DeltaSigmaAMInverse == 0.0 &&
                BitConverter.DoubleToInt64Bits(value.DeltaSigmaAMInverse) >= 0);
        }

        public bool Enabled { get; }

        public IReadOnlyList<RrsOverlayValueV1> Values { get; }

        public bool DisabledZeroAssertion { get; }

        public Digest32 MapDigest { get; }

        public Digest32 ReferenceStateDigest { get; }

        public Digest32 OverlayDigest { get; }

        public RrsOverlayValueV1? Find(NodeKey targetNode, ushort groupIndex)
        {
            return Values.FirstOrDefault(value =>
                value.TargetNode == targetNode && value.GroupIndex == groupIndex);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-OVERLAY-EVALUATION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                writer.Write(Enabled ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteDigest(writer, MapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, ReferenceStateDigest);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)Values.Count));
                foreach (RrsOverlayValueV1 value in Values)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteDigest(writer, OverlayDigest);
            });
        }
    }

    /// <summary>
    /// Pure local overlay evaluation. The optional enabled flag is an explicit
    /// scratch-evaluation gate and is not a controller mode or hidden physics.
    /// </summary>
    public static class RrsOverlayV1
    {
        public static ContractValidationResult<RrsOverlayEvaluationV1> TryEvaluate(
            RrsInfluenceMapV1? map,
            IEnumerable<RrsActuatorSnapshotV1>? actuatorSnapshots,
            bool enabled = true)
        {
            if (map == null)
            {
                return Invalid(
                    "RrsOverlay.Map.Missing",
                    "map",
                    "A validated approved influence map is required.");
            }

            if (actuatorSnapshots == null)
            {
                return Invalid(
                    "RrsOverlay.ActuatorSnapshots.Missing",
                    "actuator_snapshots",
                    "Complete total-power and tilt actuator snapshots are required.");
            }

            RrsActuatorSnapshotV1[] snapshots = actuatorSnapshots
                .OrderBy(snapshot => snapshot.ActuatorId)
                .ToArray();
            if (snapshots.Length != 2 ||
                snapshots[0].ActuatorId != RrsFixtureV1.TotalPowerActuatorId ||
                snapshots[1].ActuatorId != RrsFixtureV1.TiltActuatorId)
            {
                return Invalid(
                    "RrsOverlay.ActuatorSnapshots.Incomplete",
                    "actuator_snapshots",
                    "Both approved actuator snapshots are required.");
            }

            foreach (RrsActuatorSnapshotV1 snapshot in snapshots)
            {
                if (!map.TryGetReferenceState(snapshot.ActuatorId, out double mapReference) ||
                    mapReference != snapshot.ReferenceState ||
                    !RrsValidationV1.IsCanonicalFraction(snapshot.State))
                {
                    return Invalid(
                        "RrsOverlay.ReferenceState.Mismatch",
                        "actuator_snapshots",
                        "Overlay actuator states must bind to the map reference state and bounds.");
                }
            }

            List<RrsOverlayValueV1> values = new List<RrsOverlayValueV1>();
            foreach (NodeKey targetNode in map.TargetNodes)
            {
                for (ushort groupIndex = 0; groupIndex < RrsFixtureV1.GroupCount; groupIndex++)
                {
                    double value = 0.0;
                    if (enabled)
                    {
                        foreach (RrsInfluenceMapEntryV1 entry in map.Entries.Where(entry =>
                                     entry.TargetNode == targetNode && entry.GroupIndex == groupIndex))
                        {
                            RrsActuatorSnapshotV1 snapshot = snapshots.First(candidate =>
                                candidate.ActuatorId == entry.ActuatorId);
                            value += entry.WeightMInversePerActuatorUnit *
                                (snapshot.State - entry.ReferenceActuatorState);
                        }
                    }

                    if (!RrsValidationV1.IsCanonicalSignedGain(value))
                    {
                        return Invalid(
                            "RrsOverlay.Value.NonFinite",
                            "overlay",
                            "The local absorption overlay must remain finite.");
                    }

                    values.Add(RrsOverlayValueV1.Create(targetNode, groupIndex, value));
                }
            }

            Digest32 overlayDigest = new Digest32(Phase5CanonicalBytesV1.Sha256(
                Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-RRS-OVERLAY-EVALUATION-V1");
                    writer.Write((byte)0);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                    writer.Write(enabled ? (byte)1 : (byte)0);
                    Phase5CanonicalBytesV1.WriteDigest(writer, map.MapDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, map.ReferenceStateDigest);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)snapshots.Length));
                    foreach (RrsActuatorSnapshotV1 snapshot in snapshots)
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, snapshot.ToCanonicalBytes());
                    }

                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
                    foreach (RrsOverlayValueV1 value in values)
                    {
                        Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
                    }
                })));

            return ContractValidationResult<RrsOverlayEvaluationV1>.Valid(
                new RrsOverlayEvaluationV1(
                    enabled,
                    values,
                    map.MapDigest,
                    map.ReferenceStateDigest,
                    overlayDigest));
        }

        private static ContractValidationResult<RrsOverlayEvaluationV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RrsOverlayEvaluationV1>.Invalid(code, path, message);
        }
    }
}
