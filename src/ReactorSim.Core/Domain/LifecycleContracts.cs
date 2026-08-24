using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The validity of an accepted spatial or power binding. A counter remains
    /// an audit value when the binding is invalid; this status is not inferred
    /// from a zero counter.
    /// </summary>
    public enum BindingStatusV1 : byte
    {
        Invalid = 0,
        Valid = 1
    }

    /// <summary>
    /// An immutable SHA-256-sized opaque digest. This type carries an identity;
    /// it does not calculate or approve a digest algorithm for runtime state.
    /// </summary>
    public sealed class Digest32 : IEquatable<Digest32>
    {
        private readonly ReadOnlyCollection<byte> _bytes;

        public Digest32(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != 32)
            {
                throw new ArgumentException("A Digest32 value must contain exactly 32 bytes.", nameof(bytes));
            }

            _bytes = new ReadOnlyCollection<byte>((byte[])bytes.Clone());
        }

        public IReadOnlyList<byte> Bytes
        {
            get { return _bytes; }
        }

        public static ContractValidationResult<Digest32> TryCreate(byte[]? bytes)
        {
            if (bytes == null || bytes.Length != 32)
            {
                return ContractValidationResult<Digest32>.Invalid(
                    "Digest32.Length.Invalid",
                    "digest",
                    "A digest must contain exactly 32 bytes.");
            }

            return ContractValidationResult<Digest32>.Valid(new Digest32(bytes));
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }

        public bool Equals(Digest32? other)
        {
            return other != null && _bytes.SequenceEqual(other._bytes);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Digest32);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < _bytes.Count; i++)
                {
                    hash = (hash * 31) + _bytes[i];
                }

                return hash;
            }
        }

    }

    /// <summary>
    /// Explicit applicability wrapper used where P2-T05 requires a
    /// NotApplicable tag instead of a sentinel digest.
    /// </summary>
    public sealed class OptionalDigest32 : IEquatable<OptionalDigest32>
    {
        private OptionalDigest32(bool isApplicable, Digest32? value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalDigest32 NotApplicable
        {
            get { return new OptionalDigest32(false, null); }
        }

        public bool IsApplicable { get; }

        public Digest32? Value { get; }

        public static OptionalDigest32 Applicable(Digest32 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new OptionalDigest32(true, value);
        }

        public bool Equals(OptionalDigest32? other)
        {
            if (other == null || IsApplicable != other.IsApplicable)
            {
                return false;
            }

            return !IsApplicable || Value!.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalDigest32);
        }

        public override int GetHashCode()
        {
            return IsApplicable && Value != null ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Explicit applicability wrapper for UUID identities.
    /// </summary>
    public sealed class OptionalStableId : IEquatable<OptionalStableId>
    {
        private OptionalStableId(bool isApplicable, StableId value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalStableId NotApplicable
        {
            get { return new OptionalStableId(false, StableId.Empty); }
        }

        public bool IsApplicable { get; }

        public StableId Value { get; }

        public static OptionalStableId Applicable(StableId value)
        {
            if (value.IsEmpty)
            {
                throw new ArgumentException("An applicable stable identity may not be empty.", nameof(value));
            }

            return new OptionalStableId(true, value);
        }

        public bool Equals(OptionalStableId? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalStableId);
        }

        public override int GetHashCode()
        {
            return IsApplicable ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Explicit applicability wrapper for version fields.
    /// </summary>
    public sealed class OptionalUInt64 : IEquatable<OptionalUInt64>
    {
        private OptionalUInt64(bool isApplicable, ulong value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalUInt64 NotApplicable
        {
            get { return new OptionalUInt64(false, 0); }
        }

        public bool IsApplicable { get; }

        public ulong Value { get; }

        public static OptionalUInt64 Applicable(ulong value)
        {
            return new OptionalUInt64(true, value);
        }

        public bool Equals(OptionalUInt64? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalUInt64);
        }

        public override int GetHashCode()
        {
            return IsApplicable ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// The explicit initial and current nuclide-version counters for one
    /// persistent bundle identity. This is lifecycle metadata only; it does
    /// not implement I-135/Xe-135 integration.
    /// </summary>
    public sealed class BundleNuclideVersionV1
    {
        public BundleNuclideVersionV1(
            StableId bundleId,
            ulong initialNuclideStateVersion,
            ulong nuclideStateVersion)
        {
            if (bundleId.IsEmpty)
            {
                throw new ArgumentException("A bundle identity is required.", nameof(bundleId));
            }

            if (nuclideStateVersion < initialNuclideStateVersion)
            {
                throw new ArgumentException(
                    "The current nuclide version may not precede its explicit initial version.",
                    nameof(nuclideStateVersion));
            }

            BundleId = bundleId;
            InitialNuclideStateVersion = initialNuclideStateVersion;
            NuclideStateVersion = nuclideStateVersion;
        }

        public StableId BundleId { get; }

        public ulong InitialNuclideStateVersion { get; }

        public ulong NuclideStateVersion { get; }

    }

    /// <summary>
    /// The explicit location of one persistent bundle at a lifecycle boundary.
    /// Refuelling owns location transitions; this contract uses the binding to
    /// reject a snapshot assembled from a relocated inventory.
    /// </summary>
    public sealed class BundleLocationV1
    {
        public BundleLocationV1(StableId bundleId, ChannelId channelId, BundlePosition position)
        {
            if (bundleId.IsEmpty)
            {
                throw new ArgumentException("A bundle identity is required.", nameof(bundleId));
            }

            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
        }

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }
    }

    /// <summary>
    /// The P2-T05 state-binding tuple projected from a validated lifecycle.
    /// Aggregate state does not invent a bundle nuclide version or a kinetic
    /// step, so those members remain explicitly NotApplicable here.
    /// </summary>
    public sealed class StateBindingV1
    {
        internal StateBindingV1(
            ulong coreStateVersion,
            OptionalUInt64 spatialStateVersion,
            OptionalStableId spatialSolveId,
            OptionalStableId powerSnapshotId,
            OptionalUInt64 powerSnapshotVersion,
            OptionalUInt64 kineticStepIndex,
            OptionalUInt64 nuclideStateVersion,
            string topologyVersion,
            string dataPackVersion,
            OptionalDigest32 coefficientDigest,
            OptionalDigest32 snapshotDigest)
        {
            CoreStateVersion = coreStateVersion;
            SpatialStateVersion = spatialStateVersion;
            SpatialSolveId = spatialSolveId;
            PowerSnapshotId = powerSnapshotId;
            PowerSnapshotVersion = powerSnapshotVersion;
            KineticStepIndex = kineticStepIndex;
            NuclideStateVersion = nuclideStateVersion;
            TopologyVersion = topologyVersion;
            DataPackVersion = dataPackVersion;
            CoefficientDigest = coefficientDigest;
            SnapshotDigest = snapshotDigest;
        }

        public ulong CoreStateVersion { get; }

        public OptionalUInt64 SpatialStateVersion { get; }

        public OptionalStableId SpatialSolveId { get; }

        public OptionalStableId PowerSnapshotId { get; }

        public OptionalUInt64 PowerSnapshotVersion { get; }

        public OptionalUInt64 KineticStepIndex { get; }

        public OptionalUInt64 NuclideStateVersion { get; }

        public string TopologyVersion { get; }

        public string DataPackVersion { get; }

        public OptionalDigest32 CoefficientDigest { get; }

        public OptionalDigest32 SnapshotDigest { get; }
    }

    /// <summary>
    /// Immutable shared lifecycle state for Phase 3 and later approved
    /// runtime owners. The transition helpers only update counters and
    /// bindings; they do not solve physics, integrate burnup, or integrate
    /// nuclides.
    /// </summary>
    public sealed class VersionLifecycleV1
    {
        public const uint CurrentSchemaVersion = 2;

        private readonly ReadOnlyCollection<BundleNuclideVersionV1> _bundleNuclideVersions;
        private readonly ReadOnlyCollection<BundleLocationV1> _bundleLocations;

        private VersionLifecycleV1(
            uint schemaVersion,
            CoreTopology topology,
            double currentSimulationTimeSeconds,
            ulong initialCoreStateVersion,
            ulong coreStateVersion,
            ulong initialSpatialStateVersion,
            ulong spatialStateVersion,
            ulong initialPowerSnapshotVersion,
            ulong powerSnapshotVersion,
            IReadOnlyList<BundleNuclideVersionV1> bundleNuclideVersions,
            BindingStatusV1 spatialBindingStatus,
            BindingStatusV1 powerBindingStatus,
            OptionalStableId spatialSolveId,
            OptionalStableId powerSnapshotId,
            OptionalDigest32 stateDigest,
            OptionalDigest32 coefficientDigest,
            OptionalDigest32 topologyDigest,
            OptionalDigest32 dataPackDigest,
            OptionalDigest32 snapshotDigest,
            string topologyVersion,
            string dataPackVersion,
            IReadOnlyList<BundleLocationV1> bundleLocations,
            OptionalDigest32? powerSnapshotInventoryDigest = null,
            OptionalDigest32? powerSnapshotAcceptedInventoryDigest = null,
            OptionalDigest32? powerSnapshotBurnupEnergyDigest = null)
        {
            SchemaVersion = schemaVersion;
            Topology = topology;
            CurrentSimulationTimeSeconds = currentSimulationTimeSeconds;
            InitialCoreStateVersion = initialCoreStateVersion;
            CoreStateVersion = coreStateVersion;
            InitialSpatialStateVersion = initialSpatialStateVersion;
            SpatialStateVersion = spatialStateVersion;
            InitialPowerSnapshotVersion = initialPowerSnapshotVersion;
            PowerSnapshotVersion = powerSnapshotVersion;
            _bundleNuclideVersions = new ReadOnlyCollection<BundleNuclideVersionV1>(
                bundleNuclideVersions.ToArray());
            _bundleLocations = new ReadOnlyCollection<BundleLocationV1>(
                bundleLocations.ToArray());
            SpatialBindingStatus = spatialBindingStatus;
            PowerBindingStatus = powerBindingStatus;
            SpatialSolveId = spatialSolveId;
            PowerSnapshotId = powerSnapshotId;
            StateDigest = stateDigest;
            CoefficientDigest = coefficientDigest;
            TopologyDigest = topologyDigest;
            DataPackDigest = dataPackDigest;
            SnapshotDigest = snapshotDigest;
            PowerSnapshotInventoryDigest = powerSnapshotInventoryDigest ?? OptionalDigest32.NotApplicable;
            PowerSnapshotAcceptedInventoryDigest = powerSnapshotAcceptedInventoryDigest ?? OptionalDigest32.NotApplicable;
            PowerSnapshotBurnupEnergyDigest = powerSnapshotBurnupEnergyDigest ?? OptionalDigest32.NotApplicable;
            TopologyVersion = topologyVersion;
            DataPackVersion = dataPackVersion;
        }

        public uint SchemaVersion { get; }

        internal CoreTopology Topology { get; }

        public double CurrentSimulationTimeSeconds { get; }

        public ulong InitialCoreStateVersion { get; }

        public ulong CoreStateVersion { get; }

        public ulong InitialSpatialStateVersion { get; }

        public ulong SpatialStateVersion { get; }

        public ulong InitialPowerSnapshotVersion { get; }

        public ulong PowerSnapshotVersion { get; }

        public IReadOnlyList<BundleNuclideVersionV1> BundleNuclideVersions
        {
            get { return _bundleNuclideVersions; }
        }

        public IReadOnlyList<BundleLocationV1> BundleLocations
        {
            get { return _bundleLocations; }
        }

        public BindingStatusV1 SpatialBindingStatus { get; }

        public BindingStatusV1 PowerBindingStatus { get; }

        public OptionalStableId SpatialSolveId { get; }

        public OptionalStableId PowerSnapshotId { get; }

        public OptionalDigest32 StateDigest { get; }

        public OptionalDigest32 CoefficientDigest { get; }

        public OptionalDigest32 TopologyDigest { get; }

        public OptionalDigest32 DataPackDigest { get; }

        public OptionalDigest32 SnapshotDigest { get; }

        /// <summary>
        /// Full canonical inventory digest before the accepted power snapshot
        /// appends its current history record and bindings.
        /// </summary>
        public OptionalDigest32 PowerSnapshotInventoryDigest { get; }

        /// <summary>
        /// Full canonical inventory digest immediately after the accepted
        /// power snapshot. Burnup uses this lifecycle-authenticated value.
        /// </summary>
        public OptionalDigest32 PowerSnapshotAcceptedInventoryDigest { get; }

        /// <summary>
        /// Canonical burnup/energy projection bound by the accepted power
        /// snapshot.
        /// </summary>
        public OptionalDigest32 PowerSnapshotBurnupEnergyDigest { get; }

        public string TopologyVersion { get; }

        public string DataPackVersion { get; }

        public static ContractValidationResult<VersionLifecycleV1> TryCreate(
            SimulationConfiguration configuration,
            BundleInventory inventory,
            IEnumerable<BundleNuclideVersionV1> initialBundleNuclideVersions,
            Digest32 initialStateDigest)
        {
            if (configuration == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required.");
            }

            if (inventory == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Inventory.Missing",
                    "inventory",
                    "A validated bundle inventory is required.");
            }

            if (initialBundleNuclideVersions == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.BundleVersions.Missing",
                    "bundle_nuclide_versions",
                    "Every persistent bundle requires an explicit nuclide-version record.");
            }

            if (initialStateDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.StateDigest.Missing",
                    "state_digest",
                    "The initial state digest must be explicit.");
            }

            if (configuration.SchemaVersion != SimulationConfiguration.CurrentSchemaVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Configuration.Unsupported",
                    "configuration.schema_version",
                    "The lifecycle requires the current simulation configuration schema.");
            }

            ContractValidationResult<bool> compatibility =
                configuration.DataPack.ValidateCompatibility(configuration.Topology);
            if (!compatibility.IsValid)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    compatibility.FirstDiagnostic.Code,
                    compatibility.FirstDiagnostic.Path,
                    compatibility.FirstDiagnostic.Message);
            }

            if (!AreTopologiesEquivalent(inventory.Topology, configuration.Topology))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Inventory.TopologyMismatch",
                    "inventory.topology",
                    "The inventory topology must match the simulation configuration structurally.");
            }

            List<BundleNuclideVersionV1> records = initialBundleNuclideVersions.ToList();
            if (records.Any(record => record == null))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.BundleVersion.Null",
                    "bundle_nuclide_versions",
                    "A bundle nuclide-version record may not be null.");
            }

            BundleNuclideVersionV1[] canonical = records
                .OrderBy(record => record.BundleId)
                .ToArray();
            for (int i = 0; i < canonical.Length; i++)
            {
                BundleNuclideVersionV1 record = canonical[i];
                if (record.BundleId.IsEmpty)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.BundleId.Empty",
                        "bundle_nuclide_versions",
                        "A bundle nuclide-version record requires a stable identity.");
                }

                if (record.InitialNuclideStateVersion != record.NuclideStateVersion)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.BundleVersion.InitialMismatch",
                        "bundle[" + record.BundleId + "].nuclide_state_version",
                        "Initialization requires current and explicit initial nuclide versions to match.");
                }

                if (i > 0 && canonical[i - 1].BundleId == record.BundleId)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.BundleId.Duplicate",
                        "bundle[" + record.BundleId + "].bundle_id",
                        "Bundle nuclide identities must be unique.");
                }
            }

            StableId[] inventoryIds = inventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId)
                .OrderBy(bundleId => bundleId)
                .ToArray();
            if (inventoryIds.Length != canonical.Length)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.BundleVersion.CountMismatch",
                    "bundle_nuclide_versions",
                    "The lifecycle requires exactly one nuclide-version record for each live bundle.");
            }

            for (int i = 0; i < inventoryIds.Length; i++)
            {
                if (inventoryIds[i] != canonical[i].BundleId)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.BundleVersion.IdentityMismatch",
                        "bundle_nuclide_versions",
                        "The lifecycle bundle identities must equal the canonical live inventory identities.");
                }
            }

            BundleLocationV1[] locations = inventory.EnumerateOccupied()
                .Select(bundle => new BundleLocationV1(bundle.BundleId, bundle.ChannelId, bundle.Position))
                .OrderBy(location => location.BundleId)
                .ToArray();

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    CurrentSchemaVersion,
                    configuration.Topology,
                    configuration.InitialSimulationTimeSeconds,
                    configuration.InitialCoreStateVersion,
                    configuration.InitialCoreStateVersion,
                    configuration.InitialSpatialStateVersion,
                    configuration.InitialSpatialStateVersion,
                    configuration.InitialPowerSnapshotVersion,
                    configuration.InitialPowerSnapshotVersion,
                    canonical,
                    BindingStatusV1.Invalid,
                    BindingStatusV1.Invalid,
                    OptionalStableId.NotApplicable,
                    OptionalStableId.NotApplicable,
                    OptionalDigest32.Applicable(initialStateDigest),
                    OptionalDigest32.NotApplicable,
                    OptionalDigest32.Applicable(new Digest32(configuration.DataPack.TopologyDigest.ToArray())),
                    OptionalDigest32.Applicable(new Digest32(configuration.DataPack.ContentDigest.ToArray())),
                    OptionalDigest32.NotApplicable,
                    configuration.DataPack.TopologySchemaId,
                    configuration.DataPack.DataPackVersion,
                    locations));
        }

        public ContractValidationResult<VersionLifecycleV1> TryAcceptSpatialSolve(
            ulong expectedCoreStateVersion,
            IEnumerable<BundleNuclideVersionV1> expectedBundleNuclideVersions,
            string expectedTopologyVersion,
            string expectedDataPackVersion,
            Digest32 expectedTopologyDigest,
            Digest32 expectedDataPackDigest,
            Digest32 expectedStateDigest,
            StableId spatialSolveId,
            StableId powerSnapshotId,
            Digest32 coefficientDigest,
            Digest32 snapshotDigest,
            double exactSimulationTimeSeconds,
            Digest32? powerSnapshotInventoryDigest = null,
            Digest32? powerSnapshotAcceptedInventoryDigest = null,
            Digest32? powerSnapshotBurnupEnergyDigest = null)
        {
            if (expectedCoreStateVersion != CoreStateVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.CoreVersion.Stale",
                    "expected_core_state_version",
                    "The candidate solve must bind the current core state version.");
            }

            ContractValidationResult<bool> bundleMatch = ValidateExpectedBundleVersions(
                expectedBundleNuclideVersions);
            if (!bundleMatch.IsValid)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    bundleMatch.FirstDiagnostic.Code,
                    bundleMatch.FirstDiagnostic.Path,
                    bundleMatch.FirstDiagnostic.Message);
            }

            if (string.IsNullOrWhiteSpace(expectedTopologyVersion) ||
                !string.Equals(expectedTopologyVersion, TopologyVersion, StringComparison.Ordinal))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.TopologyVersion.Stale",
                    "expected_topology_version",
                    "The candidate solve must bind the current topology version.");
            }

            if (string.IsNullOrWhiteSpace(expectedDataPackVersion) ||
                !string.Equals(expectedDataPackVersion, DataPackVersion, StringComparison.Ordinal))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.DataPackVersion.Stale",
                    "expected_data_pack_version",
                    "The candidate solve must bind the current data-pack version.");
            }

            if (expectedTopologyDigest == null || !expectedTopologyDigest.Equals(TopologyDigest.Value))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.TopologyDigest.Stale",
                    "expected_topology_digest",
                    "The candidate solve must bind the current topology digest.");
            }

            if (expectedDataPackDigest == null || !expectedDataPackDigest.Equals(DataPackDigest.Value))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.DataPackDigest.Stale",
                    "expected_data_pack_digest",
                    "The candidate solve must bind the current data-pack digest.");
            }

            if (expectedStateDigest == null || !expectedStateDigest.Equals(StateDigest.Value))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.StateDigest.Stale",
                    "expected_state_digest",
                    "The candidate solve must bind the current state digest.");
            }

            if (spatialSolveId.IsEmpty)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.Id.Empty",
                    "spatial_solve_id",
                    "An accepted spatial solve requires a stable identity.");
            }

            if (powerSnapshotId.IsEmpty)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.PowerSnapshot.Id.Empty",
                    "power_snapshot_id",
                    "An accepted power snapshot requires a stable identity.");
            }

            if (coefficientDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.CoefficientDigest.Missing",
                    "coefficient_digest",
                    "An accepted solve requires an explicit coefficient digest.");
            }

            if (snapshotDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SnapshotDigest.Missing",
                    "snapshot_digest",
                    "An accepted power snapshot requires an explicit snapshot digest.");
            }

            bool anyPowerSnapshotStateDigest = powerSnapshotInventoryDigest != null ||
                powerSnapshotAcceptedInventoryDigest != null ||
                powerSnapshotBurnupEnergyDigest != null;
            if (anyPowerSnapshotStateDigest &&
                (powerSnapshotInventoryDigest == null ||
                 powerSnapshotAcceptedInventoryDigest == null ||
                 powerSnapshotBurnupEnergyDigest == null))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.PowerSnapshotStateDigest.Incomplete",
                    "power_snapshot_state_digests",
                    "The accepted power snapshot must supply pre-accept inventory, post-accept inventory, and burnup/energy digests together.");
            }

            if (!ContractValidation.IsFinite(exactSimulationTimeSeconds) || exactSimulationTimeSeconds < 0 ||
                exactSimulationTimeSeconds != CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialSolve.Time.Stale",
                    "simulation_time_s",
                    "The accepted solve time must equal the current explicit state time.");
            }

            if (SpatialStateVersion == ulong.MaxValue)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SpatialStateVersion.Overflow",
                    "spatial_state_version",
                    "The spatial state version cannot increment beyond UInt64.MaxValue.");
            }

            if (PowerSnapshotVersion == ulong.MaxValue)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.PowerSnapshotVersion.Overflow",
                    "power_snapshot_version",
                    "The power snapshot version cannot increment beyond UInt64.MaxValue.");
            }

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    SchemaVersion,
                    Topology,
                    CurrentSimulationTimeSeconds,
                    InitialCoreStateVersion,
                    CoreStateVersion,
                    InitialSpatialStateVersion,
                    SpatialStateVersion + 1,
                    InitialPowerSnapshotVersion,
                    PowerSnapshotVersion + 1,
                    _bundleNuclideVersions,
                    BindingStatusV1.Valid,
                    BindingStatusV1.Valid,
                    OptionalStableId.Applicable(spatialSolveId),
                    OptionalStableId.Applicable(powerSnapshotId),
                    StateDigest,
                    OptionalDigest32.Applicable(coefficientDigest),
                    TopologyDigest,
                    DataPackDigest,
                    OptionalDigest32.Applicable(snapshotDigest),
                    TopologyVersion,
                    DataPackVersion,
                    _bundleLocations,
                    powerSnapshotInventoryDigest == null
                        ? OptionalDigest32.NotApplicable
                        : OptionalDigest32.Applicable(powerSnapshotInventoryDigest),
                    powerSnapshotAcceptedInventoryDigest == null
                        ? OptionalDigest32.NotApplicable
                        : OptionalDigest32.Applicable(powerSnapshotAcceptedInventoryDigest),
                    powerSnapshotBurnupEnergyDigest == null
                        ? OptionalDigest32.NotApplicable
                        : OptionalDigest32.Applicable(powerSnapshotBurnupEnergyDigest)));
        }

        public ContractValidationResult<VersionLifecycleV1> TryCommitCoreState(
            ulong expectedCoreStateVersion,
            Digest32 nextStateDigest)
        {
            if (expectedCoreStateVersion != CoreStateVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.CoreStateVersion.Stale",
                    "expected_core_state_version",
                    "The commit must bind the current core state version.");
            }

            if (nextStateDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.StateDigest.Missing",
                    "state_digest",
                    "A committed core state requires an explicit state digest.");
            }

            if (CoreStateVersion == ulong.MaxValue)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.CoreStateVersion.Overflow",
                    "core_state_version",
                    "The core state version cannot increment beyond UInt64.MaxValue.");
            }

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    SchemaVersion,
                    Topology,
                    CurrentSimulationTimeSeconds,
                    InitialCoreStateVersion,
                    CoreStateVersion + 1,
                    InitialSpatialStateVersion,
                    SpatialStateVersion,
                    InitialPowerSnapshotVersion,
                    PowerSnapshotVersion,
                    _bundleNuclideVersions,
                    BindingStatusV1.Invalid,
                    BindingStatusV1.Invalid,
                    OptionalStableId.NotApplicable,
                    OptionalStableId.NotApplicable,
                    OptionalDigest32.Applicable(nextStateDigest),
                    OptionalDigest32.NotApplicable,
                    TopologyDigest,
                    DataPackDigest,
                    OptionalDigest32.NotApplicable,
                    TopologyVersion,
                    DataPackVersion,
                    _bundleLocations));
        }

        /// <summary>
        /// Commits one positive-duration complete-state burnup interval. The
        /// caller must have validated a current accepted power snapshot before
        /// entering this method. The commit advances explicit simulation time
        /// and the core epoch together, then invalidates the old spatial and
        /// power bindings so another snapshot is mandatory.
        /// </summary>
        public ContractValidationResult<VersionLifecycleV1> TryCommitBurnupInterval(
            ulong expectedCoreStateVersion,
            double targetTimeSeconds,
            Digest32 nextStateDigest)
        {
            if (expectedCoreStateVersion != CoreStateVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.CoreVersion.Stale",
                    "expected_core_state_version",
                    "The burnup commit must bind the current core state version.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalTime(targetTimeSeconds))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.TargetTime.Invalid",
                    "target_time_s",
                    "The burnup target time must be finite, nonnegative, and canonical.");
            }

            if (targetTimeSeconds <= CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.TargetTime.NotAfterCurrent",
                    "target_time_s",
                    "A burnup commit requires a strictly later target time.");
            }

            if (nextStateDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.StateDigest.Missing",
                    "state_digest",
                    "A committed burnup state requires an explicit next state digest.");
            }

            if (SpatialBindingStatus != BindingStatusV1.Valid ||
                PowerBindingStatus != BindingStatusV1.Valid ||
                !SpatialSolveId.IsApplicable ||
                !PowerSnapshotId.IsApplicable ||
                !CoefficientDigest.IsApplicable ||
                !SnapshotDigest.IsApplicable)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.PowerSnapshot.Invalid",
                    "power_snapshot",
                    "A positive-duration burnup commit requires one complete accepted spatial and power binding.");
            }

            if (CoreStateVersion == ulong.MaxValue)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Burnup.CoreStateVersion.Overflow",
                    "core_state_version",
                    "The core state version cannot increment beyond UInt64.MaxValue.");
            }

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    SchemaVersion,
                    Topology,
                    targetTimeSeconds,
                    InitialCoreStateVersion,
                    CoreStateVersion + 1,
                    InitialSpatialStateVersion,
                    SpatialStateVersion,
                    InitialPowerSnapshotVersion,
                    PowerSnapshotVersion,
                    _bundleNuclideVersions,
                    BindingStatusV1.Invalid,
                    BindingStatusV1.Invalid,
                    OptionalStableId.NotApplicable,
                    OptionalStableId.NotApplicable,
                    OptionalDigest32.Applicable(nextStateDigest),
                    OptionalDigest32.NotApplicable,
                    TopologyDigest,
                    DataPackDigest,
                    OptionalDigest32.NotApplicable,
                    TopologyVersion,
                    DataPackVersion,
                    _bundleLocations));
        }

        /// <summary>
        /// Commits one atomic I/Xe integration batch. Nuclide versions advance
        /// only for records that changed; the global core epoch is unchanged,
        /// while both spatial/power bindings become explicitly invalid.
        /// </summary>
        public ContractValidationResult<VersionLifecycleV1> TryAcceptNuclideIntegration(
            IEnumerable<BundleNuclideVersionV1> expectedCurrentVersions,
            IEnumerable<BundleNuclideVersionV1> proposedVersions,
            Digest32 nextStateDigest)
        {
            ContractValidationResult<bool> currentMatch = ValidateExpectedBundleVersions(
                expectedCurrentVersions);
            if (!currentMatch.IsValid)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    currentMatch.FirstDiagnostic.Code,
                    currentMatch.FirstDiagnostic.Path,
                    currentMatch.FirstDiagnostic.Message);
            }

            if (proposedVersions == null || nextStateDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.NuclideIntegration.Input.Missing",
                    "proposed_versions",
                    "An I/Xe commit requires proposed versions and an explicit next state digest.");
            }

            BundleNuclideVersionV1[] proposedInput = proposedVersions.ToArray();
            if (proposedInput.Any(record => record == null))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.NuclideIntegration.BundleVersions.Invalid",
                    "proposed_versions",
                    "The proposed I/Xe version set may not contain null records.");
            }

            BundleNuclideVersionV1[] proposed = proposedInput
                .OrderBy(record => record.BundleId)
                .ToArray();
            if (proposed.Length != _bundleNuclideVersions.Count)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.NuclideIntegration.BundleVersions.Invalid",
                    "proposed_versions",
                    "The proposed I/Xe version set must contain exactly the current live bundle identities.");
            }

            bool anyChanged = false;
            for (int index = 0; index < proposed.Length; index++)
            {
                BundleNuclideVersionV1 current = _bundleNuclideVersions[index];
                BundleNuclideVersionV1 next = proposed[index];
                if (current.BundleId != next.BundleId ||
                    current.InitialNuclideStateVersion != next.InitialNuclideStateVersion ||
                    next.NuclideStateVersion < current.NuclideStateVersion ||
                    next.NuclideStateVersion > current.NuclideStateVersion +
                        (current.NuclideStateVersion == ulong.MaxValue ? 0UL : 1UL))
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.NuclideIntegration.VersionStep.Invalid",
                        "proposed_versions",
                        "Each affected bundle version must advance exactly once and every other version must remain unchanged.");
                }

                if (next.NuclideStateVersion != current.NuclideStateVersion)
                {
                    if (current.NuclideStateVersion == ulong.MaxValue)
                    {
                        return ContractValidationResult<VersionLifecycleV1>.Invalid(
                            "VersionLifecycle.NuclideIntegration.VersionOverflow",
                            "proposed_versions",
                            "A nuclide version cannot increment beyond UInt64.MaxValue.");
                    }

                    anyChanged = true;
                }
            }

            if (!anyChanged)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.NuclideIntegration.NoChange",
                    "proposed_versions",
                    "An accepted I/Xe integration must increment at least one bundle version.");
            }

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    SchemaVersion,
                    Topology,
                    CurrentSimulationTimeSeconds,
                    InitialCoreStateVersion,
                    CoreStateVersion,
                    InitialSpatialStateVersion,
                    SpatialStateVersion,
                    InitialPowerSnapshotVersion,
                    PowerSnapshotVersion,
                    proposed,
                    BindingStatusV1.Invalid,
                    BindingStatusV1.Invalid,
                    OptionalStableId.NotApplicable,
                    OptionalStableId.NotApplicable,
                    OptionalDigest32.Applicable(nextStateDigest),
                    OptionalDigest32.NotApplicable,
                    TopologyDigest,
                    DataPackDigest,
                    OptionalDigest32.NotApplicable,
                    TopologyVersion,
                    DataPackVersion,
                    _bundleLocations));
        }

        /// <summary>
        /// Commits a complete refuelling replacement. Retained bundle
        /// versions are preserved, fresh bundle versions come from their
        /// explicit envelopes, and the location binding is replaced with the
        /// proposed live inventory.
        /// </summary>
        public ContractValidationResult<VersionLifecycleV1> TryCommitRefuelling(
            BundleInventory resultingInventory,
            IEnumerable<BundleNuclideVersionV1> expectedCurrentVersions,
            Digest32 nextStateDigest)
        {
            ContractValidationResult<bool> currentMatch = ValidateExpectedBundleVersions(
                expectedCurrentVersions);
            if (!currentMatch.IsValid)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    currentMatch.FirstDiagnostic.Code,
                    currentMatch.FirstDiagnostic.Path,
                    currentMatch.FirstDiagnostic.Message);
            }

            if (resultingInventory == null || nextStateDigest == null ||
                !AreTopologiesEquivalent(Topology, resultingInventory.Topology))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Refuelling.Input.Invalid",
                    "resulting_inventory",
                    "A refuelling commit requires a matching topology, complete result, and next state digest.");
            }

            if (CoreStateVersion == ulong.MaxValue)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.CoreStateVersion.Overflow",
                    "core_state_version",
                    "The core state version cannot increment beyond UInt64.MaxValue.");
            }

            BundleState[] resultBundles = resultingInventory.EnumerateOccupied()
                .OrderBy(bundle => bundle.BundleId)
                .ToArray();
            var resultVersions = new List<BundleNuclideVersionV1>(resultBundles.Length);
            foreach (BundleState bundle in resultBundles)
            {
                if (bundle.NuclideState == null || bundle.NuclideState.BundleId != bundle.BundleId)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Refuelling.NuclideState.Missing",
                        "resulting_inventory",
                        "Every live bundle in a complete refuelling commit requires its explicit nuclide envelope.");
                }

                resultVersions.Add(new BundleNuclideVersionV1(
                    bundle.BundleId,
                    bundle.NuclideState.NuclideStateVersion,
                    bundle.NuclideState.NuclideStateVersion));
            }

            BundleNuclideVersionV1[] currentVersions = _bundleNuclideVersions.ToArray();
            var currentById = currentVersions.ToDictionary(record => record.BundleId, record => record);
            var preservedResultVersions = new List<BundleNuclideVersionV1>(resultVersions.Count);
            foreach (BundleState bundle in resultBundles)
            {
                BundleNuclideVersionV1 result = resultVersions
                    .Single(candidate => candidate.BundleId == bundle.BundleId);
                BundleNuclideVersionV1 current;
                if (currentById.TryGetValue(result.BundleId, out current))
                {
                    if (result.NuclideStateVersion != current.NuclideStateVersion)
                    {
                        return ContractValidationResult<VersionLifecycleV1>.Invalid(
                            "VersionLifecycle.Refuelling.MovedVersion.Changed",
                            "resulting_inventory",
                            "A moved live bundle may not change its nuclide version during refuelling.");
                    }

                    preservedResultVersions.Add(new BundleNuclideVersionV1(
                        result.BundleId,
                        current.InitialNuclideStateVersion,
                        result.NuclideStateVersion));
                }
                else
                {
                    preservedResultVersions.Add(result);
                }
            }

            BundleLocationV1[] locations = resultBundles
                .Select(bundle => new BundleLocationV1(bundle.BundleId, bundle.ChannelId, bundle.Position))
                .OrderBy(location => location.BundleId)
                .ToArray();

            return ContractValidationResult<VersionLifecycleV1>.Valid(
                new VersionLifecycleV1(
                    SchemaVersion,
                    Topology,
                    CurrentSimulationTimeSeconds,
                    InitialCoreStateVersion,
                    CoreStateVersion + 1,
                    InitialSpatialStateVersion,
                    SpatialStateVersion,
                    InitialPowerSnapshotVersion,
                    PowerSnapshotVersion,
                    preservedResultVersions,
                    BindingStatusV1.Invalid,
                    BindingStatusV1.Invalid,
                    OptionalStableId.NotApplicable,
                    OptionalStableId.NotApplicable,
                    OptionalDigest32.Applicable(nextStateDigest),
                    OptionalDigest32.NotApplicable,
                    TopologyDigest,
                    DataPackDigest,
                    OptionalDigest32.NotApplicable,
                    TopologyVersion,
                    DataPackVersion,
                    locations));
        }

        public StateBindingV1 CreateStateBinding()
        {
            bool spatialValid = SpatialBindingStatus == BindingStatusV1.Valid;
            bool powerValid = PowerBindingStatus == BindingStatusV1.Valid;
            return new StateBindingV1(
                CoreStateVersion,
                spatialValid ? OptionalUInt64.Applicable(SpatialStateVersion) : OptionalUInt64.NotApplicable,
                spatialValid ? SpatialSolveId : OptionalStableId.NotApplicable,
                powerValid ? PowerSnapshotId : OptionalStableId.NotApplicable,
                powerValid ? OptionalUInt64.Applicable(PowerSnapshotVersion) : OptionalUInt64.NotApplicable,
                OptionalUInt64.NotApplicable,
                OptionalUInt64.NotApplicable,
                TopologyVersion,
                DataPackVersion,
                spatialValid ? CoefficientDigest : OptionalDigest32.NotApplicable,
                powerValid ? SnapshotDigest : OptionalDigest32.NotApplicable);
        }

        internal static ContractValidationResult<VersionLifecycleV1> TryRestoreFromSerialization(
            SimulationConfiguration configuration,
            BundleInventory inventory,
            uint schemaVersion,
            double currentSimulationTimeSeconds,
            ulong initialCoreStateVersion,
            ulong coreStateVersion,
            ulong initialSpatialStateVersion,
            ulong spatialStateVersion,
            ulong initialPowerSnapshotVersion,
            ulong powerSnapshotVersion,
            IEnumerable<BundleNuclideVersionV1> bundleNuclideVersions,
            BindingStatusV1 spatialBindingStatus,
            BindingStatusV1 powerBindingStatus,
            OptionalStableId spatialSolveId,
            OptionalStableId powerSnapshotId,
            OptionalDigest32 stateDigest,
            OptionalDigest32 coefficientDigest,
            OptionalDigest32 topologyDigest,
            OptionalDigest32 dataPackDigest,
            OptionalDigest32 snapshotDigest,
            string topologyVersion,
            string dataPackVersion,
            IEnumerable<BundleLocationV1> bundleLocations,
            OptionalDigest32? powerSnapshotInventoryDigest = null,
            OptionalDigest32? powerSnapshotAcceptedInventoryDigest = null,
            OptionalDigest32? powerSnapshotBurnupEnergyDigest = null)
        {
            if (configuration == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required for lifecycle restore.");
            }

            if (inventory == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Inventory.Missing",
                    "inventory",
                    "A validated bundle inventory is required for lifecycle restore.");
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.SchemaVersion.Unsupported",
                    "lifecycle.schema_version",
                    "Only version lifecycle schema version 2 is supported.");
            }

            if (configuration.SchemaVersion != SimulationConfiguration.CurrentSchemaVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Configuration.Unsupported",
                    "configuration.schema_version",
                    "The lifecycle restore requires the current simulation configuration schema.");
            }

            ContractValidationResult<bool> compatibility =
                configuration.DataPack.ValidateCompatibility(configuration.Topology);
            if (!compatibility.IsValid)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    compatibility.FirstDiagnostic.Code,
                    compatibility.FirstDiagnostic.Path,
                    compatibility.FirstDiagnostic.Message);
            }

            if (!AreTopologiesEquivalent(configuration.Topology, inventory.Topology))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.TopologyMismatch",
                    "inventory.topology",
                    "The restored inventory topology must match the configuration topology.");
            }

            if (!CommandClockValidation.IsCanonicalNonnegativeFinite(currentSimulationTimeSeconds) ||
                currentSimulationTimeSeconds < configuration.InitialSimulationTimeSeconds)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Time.Invalid",
                    "lifecycle.current_simulation_time_s",
                    "The restored lifecycle time must be canonical, finite, nonnegative, and not precede configuration time.");
            }

            if (initialCoreStateVersion != configuration.InitialCoreStateVersion ||
                initialSpatialStateVersion != configuration.InitialSpatialStateVersion ||
                initialPowerSnapshotVersion != configuration.InitialPowerSnapshotVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.InitialVersion.Mismatch",
                    "lifecycle.initial_versions",
                    "Serialized initial lifecycle counters must equal the immutable configuration counters.");
            }

            if (coreStateVersion < initialCoreStateVersion ||
                spatialStateVersion < initialSpatialStateVersion ||
                powerSnapshotVersion < initialPowerSnapshotVersion)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Version.Range",
                    "lifecycle.current_versions",
                    "Serialized current lifecycle counters may not precede their initial counters.");
            }

            if (!Enum.IsDefined(typeof(BindingStatusV1), spatialBindingStatus) ||
                !Enum.IsDefined(typeof(BindingStatusV1), powerBindingStatus))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.BindingStatus.Invalid",
                    "lifecycle.binding_status",
                    "Serialized binding status is not part of the approved closed enum.");
            }

            if (bundleNuclideVersions == null || bundleLocations == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Collections.Missing",
                    "lifecycle",
                    "Serialized bundle version and location collections are required.");
            }

            if (spatialSolveId == null || powerSnapshotId == null || stateDigest == null ||
                coefficientDigest == null || topologyDigest == null || dataPackDigest == null ||
                snapshotDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.OptionalField.Missing",
                    "lifecycle",
                    "Serialized optional lifecycle fields are required, including explicit applicability tags.");
            }

            if (powerSnapshotInventoryDigest == null ||
                powerSnapshotAcceptedInventoryDigest == null ||
                powerSnapshotBurnupEnergyDigest == null)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.PowerSnapshotStateDigest.Missing",
                    "lifecycle.power_snapshot_state_digests",
                    "Serialized lifecycle restore requires all three power-snapshot state digest wrappers.");
            }

            bool anyPowerSnapshotStateDigest = powerSnapshotInventoryDigest.IsApplicable ||
                powerSnapshotAcceptedInventoryDigest.IsApplicable ||
                powerSnapshotBurnupEnergyDigest.IsApplicable;
            bool allPowerSnapshotStateDigests = powerSnapshotInventoryDigest.IsApplicable &&
                powerSnapshotAcceptedInventoryDigest.IsApplicable &&
                powerSnapshotBurnupEnergyDigest.IsApplicable;
            if (anyPowerSnapshotStateDigest != allPowerSnapshotStateDigests)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.PowerSnapshotStateDigest.Incomplete",
                    "lifecycle.power_snapshot_state_digests",
                    "Serialized power-snapshot inventory and burnup digests must be all applicable or all NotApplicable.");
            }

            if (string.IsNullOrWhiteSpace(topologyVersion) || string.IsNullOrWhiteSpace(dataPackVersion) ||
                !string.Equals(topologyVersion, configuration.DataPack.TopologySchemaId, StringComparison.Ordinal) ||
                !string.Equals(dataPackVersion, configuration.DataPack.DataPackVersion, StringComparison.Ordinal))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Provenance.Mismatch",
                    "lifecycle.provenance",
                    "Serialized topology and data-pack versions must equal the validated configuration identities.");
            }

            if (!stateDigest.IsApplicable || stateDigest.Value == null ||
                !topologyDigest.IsApplicable || topologyDigest.Value == null ||
                !dataPackDigest.IsApplicable || dataPackDigest.Value == null ||
                !topologyDigest.Value.Equals(new Digest32(configuration.DataPack.TopologyDigest.ToArray())) ||
                !dataPackDigest.Value.Equals(new Digest32(configuration.DataPack.ContentDigest.ToArray())))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Digest.Mismatch",
                    "lifecycle.digests",
                    "Serialized lifecycle digests must include state identity and match the validated data-pack digests.");
            }

            bool bindingsValid = spatialBindingStatus == BindingStatusV1.Valid &&
                                 powerBindingStatus == BindingStatusV1.Valid;
            if (spatialBindingStatus != powerBindingStatus)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.BindingStatus.Incoherent",
                    "lifecycle.binding_status",
                    "Spatial and power binding statuses must transition together in the current contract.");
            }

            if (bindingsValid)
            {
                if (!spatialSolveId.IsApplicable || !powerSnapshotId.IsApplicable ||
                    !coefficientDigest.IsApplicable || coefficientDigest.Value == null ||
                    !snapshotDigest.IsApplicable || snapshotDigest.Value == null)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Restore.Binding.Missing",
                        "lifecycle.binding",
                        "A valid serialized solve binding requires all applicable identities and digests.");
                }
            }
            else if (spatialSolveId.IsApplicable || powerSnapshotId.IsApplicable ||
                     coefficientDigest.IsApplicable || snapshotDigest.IsApplicable ||
                     anyPowerSnapshotStateDigest)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Binding.Stale",
                    "lifecycle.binding",
                    "An invalid serialized binding may not retain accepted solve identities or digests.");
            }

            List<BundleNuclideVersionV1> records = bundleNuclideVersions.ToList();
            if (records.Any(record => record == null))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.BundleVersion.Null",
                    "lifecycle.bundle_nuclide_versions",
                    "A serialized bundle version record may not be null.");
            }

            BundleNuclideVersionV1[] canonicalRecords = records
                .OrderBy(record => record.BundleId)
                .ToArray();
            BundleState[] inventoryBundles = inventory.EnumerateOccupied()
                .OrderBy(bundle => bundle.BundleId)
                .ToArray();
            StableId[] inventoryIds = inventoryBundles
                .Select(bundle => bundle.BundleId)
                .ToArray();
            if (canonicalRecords.Length != inventoryIds.Length)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.BundleVersion.CountMismatch",
                    "lifecycle.bundle_nuclide_versions",
                    "The serialized bundle version count must equal the live inventory count.");
            }

            for (int i = 0; i < canonicalRecords.Length; i++)
            {
                if (canonicalRecords[i].BundleId != inventoryIds[i] ||
                    (i > 0 && canonicalRecords[i - 1].BundleId == canonicalRecords[i].BundleId))
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Restore.BundleVersion.IdentityMismatch",
                        "lifecycle.bundle_nuclide_versions",
                        "Serialized bundle version identities must equal the unique canonical inventory identities.");
                }

                NuclideStateEnvelopeV1? inventoryState = inventoryBundles[i].NuclideState;
                if (inventoryState != null &&
                    canonicalRecords[i].NuclideStateVersion != inventoryState.NuclideStateVersion)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Restore.BundleVersion.ValueMismatch",
                        "lifecycle.bundle_nuclide_versions",
                        "Serialized current bundle nuclide versions must equal the restored inventory envelope versions.");
                }
            }

            List<BundleLocationV1> locations = bundleLocations.ToList();
            if (locations.Any(location => location == null))
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Location.Null",
                    "lifecycle.bundle_locations",
                    "A serialized bundle location record may not be null.");
            }

            BundleLocationV1[] canonicalLocations = locations
                .OrderBy(location => location.BundleId)
                .ToArray();
            if (canonicalLocations.Length != inventoryBundles.Length)
            {
                return ContractValidationResult<VersionLifecycleV1>.Invalid(
                    "VersionLifecycle.Restore.Location.CountMismatch",
                    "lifecycle.bundle_locations",
                    "The serialized bundle location count must equal the live inventory count.");
            }

            for (int i = 0; i < canonicalLocations.Length; i++)
            {
                BundleLocationV1 location = canonicalLocations[i];
                BundleState bundle = inventoryBundles[i];
                if (location.BundleId != bundle.BundleId || location.ChannelId != bundle.ChannelId ||
                    location.Position != bundle.Position ||
                    (i > 0 && canonicalLocations[i - 1].BundleId == location.BundleId))
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Restore.Location.Mismatch",
                        "lifecycle.bundle_locations",
                        "Serialized bundle locations must equal the canonical inventory locations.");
                }
            }

            VersionLifecycleV1 restored = new VersionLifecycleV1(
                schemaVersion,
                configuration.Topology,
                currentSimulationTimeSeconds,
                initialCoreStateVersion,
                coreStateVersion,
                initialSpatialStateVersion,
                spatialStateVersion,
                initialPowerSnapshotVersion,
                powerSnapshotVersion,
                canonicalRecords,
                spatialBindingStatus,
                powerBindingStatus,
                spatialSolveId,
                powerSnapshotId,
                stateDigest,
                coefficientDigest,
                topologyDigest,
                dataPackDigest,
                snapshotDigest,
                topologyVersion,
                dataPackVersion,
                canonicalLocations,
                powerSnapshotInventoryDigest,
                powerSnapshotAcceptedInventoryDigest,
                powerSnapshotBurnupEnergyDigest);

            if (allPowerSnapshotStateDigests)
            {
                ContractValidationResult<CompletePowerSnapshotV1> reconstructedSnapshot =
                    CompletePowerSnapshotV1.TryReconstructFromAcceptedState(inventory, restored);
                if (!reconstructedSnapshot.IsValid)
                {
                    return ContractValidationResult<VersionLifecycleV1>.Invalid(
                        "VersionLifecycle.Restore.PowerSnapshotState.Invalid",
                        reconstructedSnapshot.FirstDiagnostic.Path,
                        reconstructedSnapshot.FirstDiagnostic.Message);
                }
            }

            return ContractValidationResult<VersionLifecycleV1>.Valid(restored);
        }

        internal bool HasSameBundleIdentitySet(BundleInventory inventory)
        {
            if (inventory == null || !AreTopologiesEquivalent(Topology, inventory.Topology) ||
                inventory.OccupiedCount != _bundleNuclideVersions.Count)
            {
                return false;
            }

            BundleLocationV1[] inventoryLocations = inventory.EnumerateOccupied()
                .Select(bundle => new BundleLocationV1(bundle.BundleId, bundle.ChannelId, bundle.Position))
                .OrderBy(location => location.BundleId)
                .ToArray();
            for (int i = 0; i < inventoryLocations.Length; i++)
            {
                if (inventoryLocations[i].BundleId != _bundleLocations[i].BundleId ||
                    inventoryLocations[i].ChannelId != _bundleLocations[i].ChannelId ||
                    inventoryLocations[i].Position != _bundleLocations[i].Position)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreTopologiesEquivalent(CoreTopology left, CoreTopology right)
        {
            if (left == null || right == null || left.ChannelCount != right.ChannelCount ||
                left.BundlePositionCount != right.BundlePositionCount)
            {
                return false;
            }

            for (uint channelValue = 0; channelValue < left.ChannelCount; channelValue++)
            {
                ChannelTopology leftChannel = left.GetChannel(new ChannelId(channelValue));
                ChannelTopology rightChannel = right.GetChannel(new ChannelId(channelValue));
                if (leftChannel.ChannelId != rightChannel.ChannelId ||
                    leftChannel.CoordinateX != rightChannel.CoordinateX ||
                    leftChannel.CoordinateY != rightChannel.CoordinateY ||
                    leftChannel.FlowDirection != rightChannel.FlowDirection ||
                    leftChannel.InletPosition != rightChannel.InletPosition ||
                    leftChannel.OutletPosition != rightChannel.OutletPosition ||
                    leftChannel.Neighbors.Count != rightChannel.Neighbors.Count ||
                    leftChannel.BoundaryFaces.Count != rightChannel.BoundaryFaces.Count)
                {
                    return false;
                }

                for (int neighborIndex = 0; neighborIndex < leftChannel.Neighbors.Count; neighborIndex++)
                {
                    NeighborRecord leftNeighbor = leftChannel.Neighbors[neighborIndex];
                    NeighborRecord rightNeighbor = rightChannel.Neighbors[neighborIndex];
                    if (leftNeighbor.SourceChannelId != rightNeighbor.SourceChannelId ||
                        leftNeighbor.SourcePosition != rightNeighbor.SourcePosition ||
                        leftNeighbor.TargetChannelId != rightNeighbor.TargetChannelId ||
                        leftNeighbor.TargetPosition != rightNeighbor.TargetPosition ||
                        leftNeighbor.Direction != rightNeighbor.Direction)
                    {
                        return false;
                    }
                }

                for (int boundaryIndex = 0; boundaryIndex < leftChannel.BoundaryFaces.Count; boundaryIndex++)
                {
                    BoundaryFaceRecord leftBoundary = leftChannel.BoundaryFaces[boundaryIndex];
                    BoundaryFaceRecord rightBoundary = rightChannel.BoundaryFaces[boundaryIndex];
                    if (leftBoundary.ChannelId != rightBoundary.ChannelId ||
                        leftBoundary.Position != rightBoundary.Position ||
                        leftBoundary.Face != rightBoundary.Face ||
                        leftBoundary.Classification != rightBoundary.Classification)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private ContractValidationResult<bool> ValidateExpectedBundleVersions(
            IEnumerable<BundleNuclideVersionV1> expectedBundleNuclideVersions)
        {
            if (expectedBundleNuclideVersions == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "VersionLifecycle.SpatialSolve.BundleVersions.Missing",
                    "expected_bundle_nuclide_versions",
                    "An accepted solve must bind the explicit current bundle-version set.");
            }

            List<BundleNuclideVersionV1> records = expectedBundleNuclideVersions.ToList();
            if (records.Any(record => record == null))
            {
                return ContractValidationResult<bool>.Invalid(
                    "VersionLifecycle.SpatialSolve.BundleVersion.Null",
                    "expected_bundle_nuclide_versions",
                    "A candidate solve may not contain a null bundle-version record.");
            }

            BundleNuclideVersionV1[] canonical = records
                .OrderBy(record => record.BundleId)
                .ToArray();
            if (canonical.Length != _bundleNuclideVersions.Count)
            {
                return ContractValidationResult<bool>.Invalid(
                    "VersionLifecycle.SpatialSolve.BundleVersions.Stale",
                    "expected_bundle_nuclide_versions",
                    "The candidate solve bundle-version set is not the current set.");
            }

            for (int i = 0; i < canonical.Length; i++)
            {
                BundleNuclideVersionV1 candidate = canonical[i];
                BundleNuclideVersionV1 current = _bundleNuclideVersions[i];
                if (candidate.BundleId != current.BundleId ||
                    candidate.InitialNuclideStateVersion != current.InitialNuclideStateVersion ||
                    candidate.NuclideStateVersion != current.NuclideStateVersion)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "VersionLifecycle.SpatialSolve.BundleVersions.Stale",
                        "expected_bundle_nuclide_versions",
                        "The candidate solve bundle-version set is not the current set.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }
    }
}
