using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReactorSim.Core
{
    /// <summary>
    /// Versioned identity and compatibility metadata for one immutable data
    /// pack. This descriptor does not contain solver coefficients or select a
    /// physics backend; those contracts belong to the later approved phase.
    /// </summary>
    public sealed class DataPackDescriptor
    {
        public const uint CurrentSchemaVersion = 1;

        public DataPackDescriptor(
            uint schemaVersion,
            StableId dataPackId,
            string dataPackVersion,
            string topologySchemaId,
            uint channelCount,
            uint bundlePositionCount,
            string unitsProfileId,
            byte[] topologyDigest,
            byte[] contentDigest)
        {
            if (dataPackVersion == null)
            {
                throw new ArgumentNullException(nameof(dataPackVersion));
            }

            if (topologySchemaId == null)
            {
                throw new ArgumentNullException(nameof(topologySchemaId));
            }

            if (unitsProfileId == null)
            {
                throw new ArgumentNullException(nameof(unitsProfileId));
            }

            if (topologyDigest == null)
            {
                throw new ArgumentNullException(nameof(topologyDigest));
            }

            if (contentDigest == null)
            {
                throw new ArgumentNullException(nameof(contentDigest));
            }

            SchemaVersion = schemaVersion;
            DataPackId = dataPackId;
            DataPackVersion = dataPackVersion;
            TopologySchemaId = topologySchemaId;
            ChannelCount = channelCount;
            BundlePositionCount = bundlePositionCount;
            UnitsProfileId = unitsProfileId;
            TopologyDigest = new ReadOnlyCollection<byte>((byte[])topologyDigest.Clone());
            ContentDigest = new ReadOnlyCollection<byte>((byte[])contentDigest.Clone());
        }

        public uint SchemaVersion { get; }

        public StableId DataPackId { get; }

        public string DataPackVersion { get; }

        public string TopologySchemaId { get; }

        public uint ChannelCount { get; }

        public uint BundlePositionCount { get; }

        public string UnitsProfileId { get; }

        public IReadOnlyList<byte> TopologyDigest { get; }

        public IReadOnlyList<byte> ContentDigest { get; }

        public static ContractValidationResult<DataPackDescriptor> TryCreate(
            uint schemaVersion,
            StableId dataPackId,
            string dataPackVersion,
            string topologySchemaId,
            uint channelCount,
            uint bundlePositionCount,
            string unitsProfileId,
            byte[] topologyDigest,
            byte[] contentDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only DataPackDescriptor schema version 1 is supported.");
            }

            if (dataPackId.IsEmpty)
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.Id.Empty",
                    "data_pack_id",
                    "The data-pack identity is required and may not be the empty UUID.");
            }

            if (string.IsNullOrWhiteSpace(dataPackVersion))
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.Version.Empty",
                    "data_pack_version",
                    "The data-pack version is required.");
            }

            if (string.IsNullOrWhiteSpace(topologySchemaId))
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.TopologySchema.Empty",
                    "topology_schema_id",
                    "The topology schema identity is required.");
            }

            if (channelCount == 0 || bundlePositionCount < 2)
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.Dimensions.Invalid",
                    "dimensions",
                    "The descriptor must declare positive channels and at least two bundle positions.");
            }

            if (string.IsNullOrWhiteSpace(unitsProfileId))
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.UnitsProfile.Empty",
                    "units_profile_id",
                    "The units profile identity is required.");
            }

            if (topologyDigest == null || topologyDigest.Length != 32)
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.TopologyDigest.Invalid",
                    "topology_digest",
                    "The topology digest must be exactly 32 bytes.");
            }

            if (contentDigest == null || contentDigest.Length != 32)
            {
                return ContractValidationResult<DataPackDescriptor>.Invalid(
                    "DataPack.ContentDigest.Invalid",
                    "content_digest",
                    "The content digest must be exactly 32 bytes.");
            }

            return ContractValidationResult<DataPackDescriptor>.Valid(
                new DataPackDescriptor(
                    schemaVersion,
                    dataPackId,
                    dataPackVersion,
                    topologySchemaId,
                    channelCount,
                    bundlePositionCount,
                    unitsProfileId,
                    topologyDigest,
                    contentDigest));
        }

        public ContractValidationResult<bool> ValidateCompatibility(CoreTopology topology)
        {
            if (SchemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<bool>.Invalid(
                    "DataPack.SchemaVersion.Unsupported",
                    "data_pack.schema_version",
                    "Only DataPackDescriptor schema version 1 is supported.");
            }

            if (DataPackId.IsEmpty || string.IsNullOrWhiteSpace(DataPackVersion) ||
                string.IsNullOrWhiteSpace(TopologySchemaId) || string.IsNullOrWhiteSpace(UnitsProfileId))
            {
                return ContractValidationResult<bool>.Invalid(
                    "DataPack.Identity.Invalid",
                    "data_pack",
                    "The data-pack identity and version fields are required.");
            }

            if (TopologyDigest.Count != 32 || ContentDigest.Count != 32)
            {
                return ContractValidationResult<bool>.Invalid(
                    "DataPack.Digest.Invalid",
                    "data_pack.digest",
                    "Data-pack digests must be exactly 32 bytes.");
            }

            if (topology == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "DataPack.Topology.Missing",
                    "topology",
                    "A topology is required for compatibility validation.");
            }

            if (ChannelCount != topology.ChannelCount || BundlePositionCount != topology.BundlePositionCount)
            {
                return ContractValidationResult<bool>.Invalid(
                    "DataPack.Topology.DimensionMismatch",
                    "dimensions",
                    "The data-pack dimensions must match the validated topology.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }
    }

    /// <summary>
    /// Immutable state/configuration identity shared by the headless runtime.
    /// Initial lifecycle counters are explicit inputs; no hidden zero default
    /// is introduced here.
    /// </summary>
    public sealed class SimulationConfiguration
    {
        public const uint CurrentSchemaVersion = 1;

        private SimulationConfiguration(
            uint schemaVersion,
            CoreTopology topology,
            DataPackDescriptor dataPack,
            double initialSimulationTimeSeconds,
            ulong initialCoreStateVersion,
            ulong initialSpatialStateVersion,
            ulong initialPowerSnapshotVersion)
        {
            SchemaVersion = schemaVersion;
            Topology = topology;
            DataPack = dataPack;
            InitialSimulationTimeSeconds = initialSimulationTimeSeconds;
            InitialCoreStateVersion = initialCoreStateVersion;
            InitialSpatialStateVersion = initialSpatialStateVersion;
            InitialPowerSnapshotVersion = initialPowerSnapshotVersion;
        }

        public uint SchemaVersion { get; }

        public CoreTopology Topology { get; }

        public DataPackDescriptor DataPack { get; }

        public double InitialSimulationTimeSeconds { get; }

        public ulong InitialCoreStateVersion { get; }

        public ulong InitialSpatialStateVersion { get; }

        public ulong InitialPowerSnapshotVersion { get; }

        public static ContractValidationResult<SimulationConfiguration> TryCreate(
            uint schemaVersion,
            CoreTopology topology,
            DataPackDescriptor dataPack,
            double initialSimulationTimeSeconds,
            ulong initialCoreStateVersion,
            ulong initialSpatialStateVersion,
            ulong initialPowerSnapshotVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<SimulationConfiguration>.Invalid(
                    "SimulationConfiguration.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only simulation configuration schema version 1 is supported.");
            }

            if (topology == null)
            {
                return ContractValidationResult<SimulationConfiguration>.Invalid(
                    "SimulationConfiguration.Topology.Missing",
                    "topology",
                    "A validated topology is required.");
            }

            if (dataPack == null)
            {
                return ContractValidationResult<SimulationConfiguration>.Invalid(
                    "SimulationConfiguration.DataPack.Missing",
                    "data_pack",
                    "A validated data-pack descriptor is required.");
            }

            ContractValidationResult<bool> compatibility = dataPack.ValidateCompatibility(topology);
            if (!compatibility.IsValid)
            {
                return ContractValidationResult<SimulationConfiguration>.Invalid(
                    compatibility.FirstDiagnostic.Code,
                    compatibility.FirstDiagnostic.Path,
                    compatibility.FirstDiagnostic.Message);
            }

            if (!ContractValidation.IsFinite(initialSimulationTimeSeconds) || initialSimulationTimeSeconds < 0)
            {
                return ContractValidationResult<SimulationConfiguration>.Invalid(
                    "SimulationConfiguration.Time.Invalid",
                    "initial_simulation_time_s",
                    "The initial simulation time must be finite and nonnegative SI seconds.");
            }

            return ContractValidationResult<SimulationConfiguration>.Valid(
                new SimulationConfiguration(
                    schemaVersion,
                    topology,
                    dataPack,
                    initialSimulationTimeSeconds,
                    initialCoreStateVersion,
                    initialSpatialStateVersion,
                    initialPowerSnapshotVersion));
        }
    }
}
