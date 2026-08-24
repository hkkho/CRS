using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One explicit periodic spatial-solve schedule. The next event time is
    /// always constructed from the stored epoch, period, and integer event
    /// index; no wall clock or frame timing is involved.
    /// </summary>
    public sealed class SpatialRecomputeCadenceV1
    {
        public const uint CurrentSchemaVersion = 1;

        private readonly uint _schemaVersion;

        private SpatialRecomputeCadenceV1(
            double epochTimeSeconds,
            double periodSeconds,
            ulong eventIndex)
        {
            _schemaVersion = CurrentSchemaVersion;
            EpochTimeSeconds = epochTimeSeconds;
            PeriodSeconds = periodSeconds;
            EventIndex = eventIndex;
        }

        public uint SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public double EpochTimeSeconds { get; }

        public double PeriodSeconds { get; }

        public ulong EventIndex { get; }

        public static ContractValidationResult<SpatialRecomputeCadenceV1> TryCreate(
            double epochTimeSeconds,
            double periodSeconds,
            ulong eventIndex)
        {
            if (!ContractValidation.IsFinite(epochTimeSeconds) || epochTimeSeconds < 0)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    "SpatialRecompute.Cadence.Epoch.Invalid",
                    "cadence.epoch_time_s",
                    "The cadence epoch must be finite and nonnegative SI seconds.");
            }

            if (!ContractValidation.IsFinite(periodSeconds) || periodSeconds <= 0)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    "SpatialRecompute.Cadence.Period.Invalid",
                    "cadence.period_s",
                    "The cadence period must be finite and strictly positive SI seconds.");
            }

            SpatialRecomputeCadenceV1 cadence = new SpatialRecomputeCadenceV1(
                epochTimeSeconds,
                periodSeconds,
                eventIndex);
            ContractValidationResult<double> nextTime = cadence.TryGetNextEventTime();
            if (!nextTime.IsValid)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    nextTime.FirstDiagnostic.Code,
                    nextTime.FirstDiagnostic.Path,
                    nextTime.FirstDiagnostic.Message);
            }

            return ContractValidationResult<SpatialRecomputeCadenceV1>.Valid(cadence);
        }

        public ContractValidationResult<double> TryGetNextEventTime()
        {
            double eventIndexAsDouble = EventIndex;
            double elapsed = eventIndexAsDouble * PeriodSeconds;
            double nextEventTime = EpochTimeSeconds + elapsed;
            if (!ContractValidation.IsFinite(elapsed) ||
                !ContractValidation.IsFinite(nextEventTime) ||
                nextEventTime < 0)
            {
                return ContractValidationResult<double>.Invalid(
                    "SpatialRecompute.Cadence.NextTime.Invalid",
                    "cadence.next_event_time_s",
                    "The scheduled spatial-solve time must be representable as finite nonnegative SI seconds.");
            }

            return ContractValidationResult<double>.Valid(nextEventTime);
        }

        public ContractValidationResult<SpatialRecomputeCadenceV1> TryAdvance()
        {
            if (EventIndex == ulong.MaxValue)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    "SpatialRecompute.Cadence.EventIndex.Overflow",
                    "cadence.event_index",
                    "The cadence event index cannot advance beyond UInt64.MaxValue.");
            }

            ContractValidationResult<double> currentTime = TryGetNextEventTime();
            if (!currentTime.IsValid)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    currentTime.FirstDiagnostic.Code,
                    currentTime.FirstDiagnostic.Path,
                    currentTime.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialRecomputeCadenceV1> advanced =
                TryCreate(EpochTimeSeconds, PeriodSeconds, EventIndex + 1);
            if (!advanced.IsValid)
            {
                return advanced;
            }

            ContractValidationResult<double> nextTime = advanced.Value.TryGetNextEventTime();
            if (!nextTime.IsValid)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    nextTime.FirstDiagnostic.Code,
                    nextTime.FirstDiagnostic.Path,
                    nextTime.FirstDiagnostic.Message);
            }

            if (nextTime.Value <= currentTime.Value)
            {
                return ContractValidationResult<SpatialRecomputeCadenceV1>.Invalid(
                    "SpatialRecompute.Cadence.NextTime.NoProgress",
                    "cadence.next_event_time_s",
                    "Advancing the cadence must produce a strictly later representable event time.");
            }

            return advanced;
        }
    }

    /// <summary>
    /// Explicit positive node volume required when burnup-selected material
    /// values are rebound to the P2-T02 spatial coefficient contract.
    /// </summary>
    public sealed class SpatialNodeVolumeV1
    {
        private SpatialNodeVolumeV1(NodeKey node, double volumeM3)
        {
            Node = node;
            VolumeM3 = volumeM3;
        }

        public NodeKey Node { get; }

        public double VolumeM3 { get; }

        public static ContractValidationResult<SpatialNodeVolumeV1> TryCreate(
            NodeKey node,
            double volumeM3)
        {
            if (!ContractValidation.IsFinite(volumeM3) || volumeM3 <= 0)
            {
                return ContractValidationResult<SpatialNodeVolumeV1>.Invalid(
                    "SpatialRecompute.NodeVolume.Invalid",
                    ContractValidation.NodePath(node, ".volume_m3"),
                    "A spatial node volume must be finite and strictly positive SI cubic metres.");
            }

            return ContractValidationResult<SpatialNodeVolumeV1>.Valid(
                new SpatialNodeVolumeV1(node, volumeM3));
        }
    }

    /// <summary>
    /// Audit binding between one persistent bundle and the coefficient row
    /// used for its spatial node at the recomputation boundary.
    /// </summary>
    public sealed class SpatialCoefficientLookupBindingV1
    {
        internal SpatialCoefficientLookupBindingV1(
            StableId bundleId,
            NodeKey node,
            MaterialVariantId materialVariantId,
            double burnupJPerKgHm,
            BurnupCoefficientLookupResultV1 lookup)
        {
            BundleId = bundleId;
            Node = node;
            MaterialVariantId = materialVariantId;
            BurnupJPerKgHm = burnupJPerKgHm;
            Lookup = lookup;
        }

        public StableId BundleId { get; }

        public NodeKey Node { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public double BurnupJPerKgHm { get; }

        public BurnupCoefficientLookupResultV1 Lookup { get; }
    }

    /// <summary>
    /// Immutable inputs for one atomic coefficient-rebind and spatial-solve
    /// boundary. All state identities and numerical policies are caller-owned;
    /// this request selects no hidden defaults.
    /// </summary>
    public sealed class SpatialRecomputeRequestV1
    {
        public const uint CurrentSchemaVersion = 1;

        private SpatialRecomputeRequestV1(
            VersionLifecycleV1 lifecycle,
            BundleInventory inventory,
            SpatialCoefficientSet conductanceSource,
            IReadOnlyList<BurnupCoefficientTableV1> coefficientTables,
            IReadOnlyList<SpatialNodeVolumeV1> nodeVolumes,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            double targetPowerW,
            double initialEigenvalue,
            IReadOnlyList<double>? initialGroup1Flux,
            IReadOnlyList<double>? initialGroup2Flux,
            SpatialRecomputeCadenceV1 cadence,
            double exactSimulationTimeSeconds,
            string expectedDataVersion,
            StableId spatialSolveId,
            StableId powerSnapshotId,
            Digest32 coefficientDigest,
            Digest32 snapshotDigest)
        {
            Lifecycle = lifecycle;
            Inventory = inventory;
            ConductanceSource = conductanceSource;
            CoefficientTables = coefficientTables;
            NodeVolumes = nodeVolumes;
            LinearSolvePolicy = linearSolvePolicy;
            ConvergencePolicy = convergencePolicy;
            TargetPowerW = targetPowerW;
            InitialEigenvalue = initialEigenvalue;
            InitialGroup1Flux = initialGroup1Flux == null
                ? null
                : new ReadOnlyCollection<double>(initialGroup1Flux.ToArray());
            InitialGroup2Flux = initialGroup2Flux == null
                ? null
                : new ReadOnlyCollection<double>(initialGroup2Flux.ToArray());
            Cadence = cadence;
            ExactSimulationTimeSeconds = exactSimulationTimeSeconds;
            ExpectedDataVersion = expectedDataVersion;
            SpatialSolveId = spatialSolveId;
            PowerSnapshotId = powerSnapshotId;
            CoefficientDigest = coefficientDigest;
            SnapshotDigest = snapshotDigest;
        }

        public VersionLifecycleV1 Lifecycle { get; }

        public BundleInventory Inventory { get; }

        public SpatialCoefficientSet ConductanceSource { get; }

        public IReadOnlyList<BurnupCoefficientTableV1> CoefficientTables { get; }

        public IReadOnlyList<SpatialNodeVolumeV1> NodeVolumes { get; }

        public SpatialLinearSolvePolicy LinearSolvePolicy { get; }

        public SpatialConvergencePolicy ConvergencePolicy { get; }

        public double TargetPowerW { get; }

        public double InitialEigenvalue { get; }

        public IReadOnlyList<double>? InitialGroup1Flux { get; }

        public IReadOnlyList<double>? InitialGroup2Flux { get; }

        public SpatialRecomputeCadenceV1 Cadence { get; }

        public double ExactSimulationTimeSeconds { get; }

        public string ExpectedDataVersion { get; }

        public StableId SpatialSolveId { get; }

        public StableId PowerSnapshotId { get; }

        public Digest32 CoefficientDigest { get; }

        public Digest32 SnapshotDigest { get; }

        public static ContractValidationResult<SpatialRecomputeRequestV1> TryCreate(
            VersionLifecycleV1 lifecycle,
            BundleInventory inventory,
            SpatialCoefficientSet conductanceSource,
            IEnumerable<BurnupCoefficientTableV1> coefficientTables,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            double targetPowerW,
            double initialEigenvalue,
            IEnumerable<double>? initialGroup1Flux,
            IEnumerable<double>? initialGroup2Flux,
            SpatialRecomputeCadenceV1 cadence,
            double exactSimulationTimeSeconds,
            string expectedDataVersion,
            StableId spatialSolveId,
            StableId powerSnapshotId,
            Digest32 coefficientDigest,
            Digest32 snapshotDigest)
        {
            if (lifecycle == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Lifecycle.Missing",
                    "lifecycle",
                    "A recomputation requires a validated lifecycle boundary.");
            }

            if (inventory == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Inventory.Missing",
                    "inventory",
                    "A recomputation requires a validated bundle inventory.");
            }

            if (conductanceSource == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Conductances.Missing",
                    "conductance_source",
                    "A recomputation requires an existing validated topology-bound conductance set.");
            }

            if (coefficientTables == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Tables.Missing",
                    "coefficient_tables",
                    "A recomputation requires validated burnup coefficient tables.");
            }

            if (nodeVolumes == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.NodeVolumes.Missing",
                    "node_volumes",
                    "A recomputation requires an explicit volume for every spatial node.");
            }

            if (linearSolvePolicy == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.LinearSolvePolicy.Missing",
                    "linear_solve_policy",
                    "A recomputation requires an explicit inner-solve policy.");
            }

            if (convergencePolicy == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.ConvergencePolicy.Missing",
                    "convergence_policy",
                    "A recomputation requires an explicit outer convergence policy.");
            }

            if (cadence == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Cadence.Missing",
                    "cadence",
                    "A recomputation requires an explicit spatial cadence.");
            }

            if (!ContractValidation.IsFinite(targetPowerW) || targetPowerW <= 0)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.TargetPower.Invalid",
                    "target_power_w",
                    "The requested normalization target must be finite and strictly positive SI watts.");
            }

            if (!ContractValidation.IsFinite(initialEigenvalue) || initialEigenvalue <= 0)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.InitialEigenvalue.Invalid",
                    "initial_eigenvalue",
                    "The initial eigenvalue must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(exactSimulationTimeSeconds) || exactSimulationTimeSeconds < 0)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Time.Invalid",
                    "simulation_time_s",
                    "The recomputation time must be finite and nonnegative SI seconds.");
            }

            if (exactSimulationTimeSeconds != lifecycle.CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Time.Stale",
                    "simulation_time_s",
                    "The recomputation must bind the lifecycle's exact current simulation time.");
            }

            if (string.IsNullOrWhiteSpace(expectedDataVersion) ||
                !string.Equals(expectedDataVersion, lifecycle.DataPackVersion, StringComparison.Ordinal))
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.DataVersion.Stale",
                    "expected_data_version",
                    "The requested coefficient data version must equal the lifecycle data-pack version.");
            }

            if (spatialSolveId.IsEmpty)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.SpatialSolveId.Empty",
                    "spatial_solve_id",
                    "An accepted recomputation requires an explicit spatial-solve identity.");
            }

            if (powerSnapshotId.IsEmpty)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.PowerSnapshotId.Empty",
                    "power_snapshot_id",
                    "An accepted recomputation requires an explicit power-snapshot identity.");
            }

            if (coefficientDigest == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.CoefficientDigest.Missing",
                    "coefficient_digest",
                    "An accepted recomputation requires an explicit coefficient digest.");
            }

            if (snapshotDigest == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.SnapshotDigest.Missing",
                    "snapshot_digest",
                    "An accepted recomputation requires an explicit snapshot digest.");
            }

            if (!lifecycle.StateDigest.IsApplicable || lifecycle.StateDigest.Value == null ||
                !lifecycle.TopologyDigest.IsApplicable || lifecycle.TopologyDigest.Value == null ||
                !lifecycle.DataPackDigest.IsApplicable || lifecycle.DataPackDigest.Value == null)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Lifecycle.DigestBinding.Missing",
                    "lifecycle",
                    "The recomputation requires applicable state, topology, and data-pack digests.");
            }

            SpatialStencil stencil = conductanceSource.Stencil;
            if (inventory.Topology.ChannelCount != stencil.ChannelCount ||
                inventory.Topology.BundlePositionCount != stencil.BundlePositionCount)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Topology.Mismatch",
                    "inventory.topology",
                    "The inventory and conductance source must describe the same channel/position dimensions.");
            }

            if (!ReferenceEquals(stencil.Topology, lifecycle.Topology))
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Topology.IdentityMismatch",
                    "conductance_source.stencil.topology",
                    "The conductance source must be assembled from the exact lifecycle topology instance.");
            }

            BurnupCoefficientTableV1[] tables = coefficientTables.ToArray();
            if (tables.Length == 0)
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.Tables.Empty",
                    "coefficient_tables",
                    "At least one material coefficient table is required.");
            }

            var materialIds = new HashSet<MaterialVariantId>();
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] == null)
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.Table.Null",
                        "coefficient_tables[" + i + "]",
                        "A coefficient table may not be null.");
                }

                if (!string.Equals(tables[i].DataVersion, expectedDataVersion, StringComparison.Ordinal))
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.Table.DataVersion.Mismatch",
                        "coefficient_tables[" + i + "].data_version",
                        "Every coefficient table must match the requested data-pack version.");
                }

                if (!materialIds.Add(tables[i].MaterialVariantId))
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.Table.Material.Duplicate",
                        "coefficient_tables[" + i + "].material_variant_id",
                        "Exactly one coefficient table is required per material variant.");
                }
            }

            SpatialNodeVolumeV1[] volumes = nodeVolumes.ToArray();
            var expectedNodes = new HashSet<NodeKey>(stencil.Nodes.Select(node => node.Node));
            var volumeMap = new Dictionary<NodeKey, SpatialNodeVolumeV1>();
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] == null)
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.NodeVolume.Null",
                        "node_volumes[" + i + "]",
                        "A node-volume record may not be null.");
                }

                if (!expectedNodes.Contains(volumes[i].Node))
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.NodeVolume.Unknown",
                        ContractValidation.NodePath(volumes[i].Node, ".volume_m3"),
                        "A node-volume record must match the conductance-source stencil.");
                }

                if (!volumeMap.TryAdd(volumes[i].Node, volumes[i]))
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.NodeVolume.Duplicate",
                        ContractValidation.NodePath(volumes[i].Node, ".volume_m3"),
                        "Exactly one volume is required per spatial node.");
                }
            }

            foreach (NodeKey node in expectedNodes)
            {
                if (!volumeMap.ContainsKey(node))
                {
                    return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                        "SpatialRecompute.NodeVolume.Missing",
                        ContractValidation.NodePath(node, ".volume_m3"),
                        "Exactly one volume is required per spatial node.");
                }
            }

            double[]? group1 = initialGroup1Flux == null ? null : initialGroup1Flux.ToArray();
            double[]? group2 = initialGroup2Flux == null ? null : initialGroup2Flux.ToArray();
            if ((group1 == null) != (group2 == null))
            {
                return ContractValidationResult<SpatialRecomputeRequestV1>.Invalid(
                    "SpatialRecompute.InitialFlux.Incomplete",
                    "initial_flux",
                    "Both group flux vectors must be supplied together or both omitted.");
            }

            return ContractValidationResult<SpatialRecomputeRequestV1>.Valid(
                new SpatialRecomputeRequestV1(
                    lifecycle,
                    inventory,
                    conductanceSource,
                    new ReadOnlyCollection<BurnupCoefficientTableV1>(
                        tables.OrderBy(table => table.MaterialVariantId).ToArray()),
                    new ReadOnlyCollection<SpatialNodeVolumeV1>(
                        volumes.OrderBy(volume => volume.Node).ToArray()),
                    linearSolvePolicy,
                    convergencePolicy,
                    targetPowerW,
                    initialEigenvalue,
                    group1,
                    group2,
                    cadence,
                    exactSimulationTimeSeconds,
                    expectedDataVersion,
                    spatialSolveId,
                    powerSnapshotId,
                    coefficientDigest,
                    snapshotDigest));
        }
    }

    /// <summary>
    /// The immutable result of one successful coefficient lookup/rebind and
    /// converged spatial solve. Lifecycle advancement is returned as a
    /// replacement object; the request's original lifecycle is unchanged.
    /// </summary>
    public sealed class SpatialRecomputeResultV1
    {
        private readonly ReadOnlyCollection<SpatialCoefficientLookupBindingV1> _lookupBindings;
        private readonly uint _schemaVersion;

        internal SpatialRecomputeResultV1(
            SpatialRecomputeRequestV1 request,
            SpatialCoefficientSet coefficients,
            IReadOnlyList<SpatialCoefficientLookupBindingV1> lookupBindings,
            SpatialSolveResult spatialSolve,
            VersionLifecycleV1 acceptedLifecycle,
            SpatialRecomputeCadenceV1 nextCadence)
        {
            _schemaVersion = SpatialRecomputeRequestV1.CurrentSchemaVersion;
            Request = request;
            Coefficients = coefficients;
            _lookupBindings = new ReadOnlyCollection<SpatialCoefficientLookupBindingV1>(
                lookupBindings.ToArray());
            SpatialSolve = spatialSolve;
            AcceptedLifecycle = acceptedLifecycle;
            NextCadence = nextCadence;
        }

        public uint SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public SpatialRecomputeRequestV1 Request { get; }

        public SpatialCoefficientSet Coefficients { get; }

        public IReadOnlyList<SpatialCoefficientLookupBindingV1> LookupBindings
        {
            get { return _lookupBindings; }
        }

        public SpatialSolveResult SpatialSolve { get; }

        public VersionLifecycleV1 AcceptedLifecycle { get; }

        public SpatialRecomputeCadenceV1 NextCadence { get; }

        public double SimulationTimeSeconds
        {
            get { return Request.ExactSimulationTimeSeconds; }
        }

        public StableId SpatialSolveId
        {
            get { return Request.SpatialSolveId; }
        }

        public StableId PowerSnapshotId
        {
            get { return Request.PowerSnapshotId; }
        }

        public Digest32 CoefficientDigest
        {
            get { return Request.CoefficientDigest; }
        }

        public Digest32 SnapshotDigest
        {
            get { return Request.SnapshotDigest; }
        }
    }

    /// <summary>
    /// Atomic P5-T06 boundary: resolve every live node's burnup coefficient,
    /// preserve the validated leakage conductances, run the existing
    /// deterministic P2-T02 solve, and accept lifecycle bindings only after a
    /// converged result and the next cadence event are both valid.
    /// </summary>
    public static class SpatialStateRecomputeV1
    {
        public static ContractValidationResult<SpatialRecomputeResultV1> TryApply(
            SpatialRecomputeRequestV1 request)
        {
            if (request == null)
            {
                return ContractValidationResult<SpatialRecomputeResultV1>.Invalid(
                    "SpatialRecompute.Request.Missing",
                    "request",
                    "A spatial recomputation request is required.");
            }

            ContractValidationResult<double> scheduledTime = request.Cadence.TryGetNextEventTime();
            if (!scheduledTime.IsValid)
            {
                return InvalidFromDiagnostic(scheduledTime.FirstDiagnostic);
            }

            if (scheduledTime.Value != request.ExactSimulationTimeSeconds)
            {
                return ContractValidationResult<SpatialRecomputeResultV1>.Invalid(
                    "SpatialRecompute.Cadence.TimeMismatch",
                    "cadence.next_event_time_s",
                    "The recomputation time must equal the exact next configured spatial-solve event.");
            }

            ContractValidationResult<bool> locationBinding = ValidateInventoryLocations(request);
            if (!locationBinding.IsValid)
            {
                return InvalidFromDiagnostic(locationBinding.FirstDiagnostic);
            }

            ContractValidationResult<SpatialCoefficientSet> coefficientResult =
                BuildCoefficientSet(request, out List<SpatialCoefficientLookupBindingV1> lookupBindings);
            if (!coefficientResult.IsValid)
            {
                return InvalidFromDiagnostic(coefficientResult.FirstDiagnostic);
            }

            SpatialStencil stencil = request.ConductanceSource.Stencil;
            double[]? initialGroup1 = request.InitialGroup1Flux == null
                ? null
                : request.InitialGroup1Flux.ToArray();
            double[]? initialGroup2 = request.InitialGroup2Flux == null
                ? null
                : request.InitialGroup2Flux.ToArray();
            ContractValidationResult<SpatialEigenIteration> iterationResult =
                SpatialEigenIteration.TryCreate(
                    stencil,
                    coefficientResult.Value,
                    request.LinearSolvePolicy,
                    request.TargetPowerW,
                    request.InitialEigenvalue,
                    initialGroup1,
                    initialGroup2);
            if (!iterationResult.IsValid)
            {
                return InvalidFromDiagnostic(iterationResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialEigenSolve> solveResult =
                SpatialEigenSolve.TryCreate(iterationResult.Value, request.ConvergencePolicy);
            if (!solveResult.IsValid)
            {
                return InvalidFromDiagnostic(solveResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialSolveResult> spatialResult = solveResult.Value.TrySolve();
            if (!spatialResult.IsValid)
            {
                return InvalidFromDiagnostic(spatialResult.FirstDiagnostic);
            }

            if (!spatialResult.Value.IsConverged)
            {
                ContractDiagnostic? failure = spatialResult.Value.Diagnostics.FailureDiagnostic;
                if (failure != null)
                {
                    return InvalidFromDiagnostic(failure);
                }

                return ContractValidationResult<SpatialRecomputeResultV1>.Invalid(
                    "SpatialRecompute.Solve.NotConverged",
                    "spatial_solve",
                    "The affected-state recomputation did not produce a converged spatial state.");
            }

            ContractValidationResult<SpatialRecomputeCadenceV1> nextCadence =
                request.Cadence.TryAdvance();
            if (!nextCadence.IsValid)
            {
                return InvalidFromDiagnostic(nextCadence.FirstDiagnostic);
            }

            ContractValidationResult<VersionLifecycleV1> acceptedLifecycle =
                AcceptLifecycle(request);
            if (!acceptedLifecycle.IsValid)
            {
                return InvalidFromDiagnostic(acceptedLifecycle.FirstDiagnostic);
            }

            return ContractValidationResult<SpatialRecomputeResultV1>.Valid(
                new SpatialRecomputeResultV1(
                    request,
                    coefficientResult.Value,
                    lookupBindings,
                    spatialResult.Value,
                    acceptedLifecycle.Value,
                    nextCadence.Value));
        }

        private static ContractValidationResult<SpatialCoefficientSet> BuildCoefficientSet(
            SpatialRecomputeRequestV1 request,
            out List<SpatialCoefficientLookupBindingV1> lookupBindings)
        {
            lookupBindings = new List<SpatialCoefficientLookupBindingV1>();
            SpatialStencil stencil = request.ConductanceSource.Stencil;
            var tables = request.CoefficientTables.ToDictionary(
                table => table.MaterialVariantId,
                table => table);
            var volumes = request.NodeVolumes.ToDictionary(
                volume => volume.Node,
                volume => volume.VolumeM3);
            var nodeCoefficients = new List<SpatialNodeCoefficients>(stencil.NodeCount);

            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                BundleState? bundle;
                if (!request.Inventory.TryGet(node.Node, out bundle) || bundle == null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialRecompute.Inventory.Bundle.Missing",
                        ContractValidation.NodePath(node.Node, ".bundle_id"),
                        "Every spatial node must contain a live bundle before recomputation.");
                }

                BurnupCoefficientTableV1 table;
                if (!tables.TryGetValue(bundle.MaterialVariantId, out table!))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialRecompute.Table.Missing",
                        "bundle[" + bundle.BundleId + "].material_variant_id",
                        "A burnup coefficient table is required for every live material variant.");
                }

                double burnup = bundle.CurrentBurnupJPerKgHm;
                if (!ContractValidation.IsFinite(burnup) || burnup < 0)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialRecompute.Burnup.Invalid",
                        "bundle[" + bundle.BundleId + "].burnup_j_per_kg_hm",
                        "The bundle's derived burnup must be finite and nonnegative.");
                }

                ContractValidationResult<BurnupCoefficientLookupResultV1> lookup =
                    table.TryLookup(burnup);
                if (!lookup.IsValid)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        lookup.FirstDiagnostic.Code,
                        "bundle[" + bundle.BundleId + "]." + lookup.FirstDiagnostic.Path,
                        lookup.FirstDiagnostic.Message);
                }

                double volume;
                if (!volumes.TryGetValue(node.Node, out volume))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "SpatialRecompute.NodeVolume.Missing",
                        ContractValidation.NodePath(node.Node, ".volume_m3"),
                        "Every spatial node requires an explicit volume.");
                }

                BurnupCoefficientValuesV1 values = lookup.Value.Coefficients;
                nodeCoefficients.Add(new SpatialNodeCoefficients(
                    node.Node,
                    volume,
                    values.AbsorptionGroup1PerM,
                    values.AbsorptionGroup2PerM,
                    values.DownscatterGroup1To2PerM,
                    values.FissionGroup1PerM,
                    values.FissionGroup2PerM,
                    values.NuFissionGroup1PerM,
                    values.NuFissionGroup2PerM,
                    values.ChiGroup1,
                    values.ChiGroup2,
                    values.EnergyPerFissionJ));
                lookupBindings.Add(new SpatialCoefficientLookupBindingV1(
                    bundle.BundleId,
                    node.Node,
                    bundle.MaterialVariantId,
                    burnup,
                    lookup.Value));
            }

            List<SpatialEdgeConductance> edges = CopyEdges(request.ConductanceSource, stencil, out ContractDiagnostic? edgeFailure);
            if (edgeFailure != null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    edgeFailure.Code,
                    edgeFailure.Path,
                    edgeFailure.Message);
            }

            List<SpatialBoundaryConductance> boundaries = CopyBoundaries(
                request.ConductanceSource,
                stencil,
                out ContractDiagnostic? boundaryFailure);
            if (boundaryFailure != null)
            {
                return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                    boundaryFailure.Code,
                    boundaryFailure.Path,
                    boundaryFailure.Message);
            }

            return SpatialCoefficientSet.TryCreate(
                stencil,
                nodeCoefficients,
                edges,
                boundaries);
        }

        private static List<SpatialEdgeConductance> CopyEdges(
            SpatialCoefficientSet source,
            SpatialStencil stencil,
            out ContractDiagnostic? failure)
        {
            failure = null;
            var result = new List<SpatialEdgeConductance>();
            var seen = new HashSet<SpatialEdgeKey>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                {
                    SpatialEdgeKey key = new SpatialEdgeKey(node.Node, neighbor.TargetNode);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    SpatialConductancePair conductance;
                    if (!source.TryGetEdge(key, out conductance))
                    {
                        failure = new ContractDiagnostic(
                            "SpatialRecompute.Conductance.Edge.Missing",
                            "edges[" + key + "]",
                            "The validated conductance source is missing a required reciprocal edge.");
                        return result;
                    }

                    result.Add(new SpatialEdgeConductance(
                        key.First,
                        key.Second,
                        conductance.Group1,
                        conductance.Group2));
                }
            }

            return result;
        }

        private static List<SpatialBoundaryConductance> CopyBoundaries(
            SpatialCoefficientSet source,
            SpatialStencil stencil,
            out ContractDiagnostic? failure)
        {
            failure = null;
            var result = new List<SpatialBoundaryConductance>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                {
                    SpatialBoundaryKey key = new SpatialBoundaryKey(node.Node, boundary.Face);
                    SpatialConductancePair conductance;
                    if (!source.TryGetBoundary(key, out conductance))
                    {
                        failure = new ContractDiagnostic(
                            "SpatialRecompute.Conductance.Boundary.Missing",
                            "boundaries[" + key + "]",
                            "The validated conductance source is missing a required boundary face.");
                        return result;
                    }

                    result.Add(new SpatialBoundaryConductance(
                        node.Node,
                        boundary.Face,
                        conductance.Group1,
                        conductance.Group2));
                }
            }

            return result;
        }

        private static ContractValidationResult<bool> ValidateInventoryLocations(
            SpatialRecomputeRequestV1 request)
        {
            BundleState[] bundles = request.Inventory.EnumerateOccupied()
                .OrderBy(bundle => bundle.BundleId)
                .ToArray();
            BundleLocationV1[] locations = request.Lifecycle.BundleLocations
                .OrderBy(location => location.BundleId)
                .ToArray();
            if (bundles.Length != locations.Length)
            {
                return ContractValidationResult<bool>.Invalid(
                    "SpatialRecompute.Lifecycle.Location.CountMismatch",
                    "inventory",
                    "The lifecycle and inventory must contain the same persistent bundle set.");
            }

            for (int i = 0; i < bundles.Length; i++)
            {
                if (bundles[i].BundleId != locations[i].BundleId ||
                    bundles[i].ChannelId != locations[i].ChannelId ||
                    bundles[i].Position != locations[i].Position)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "SpatialRecompute.Lifecycle.Location.Stale",
                        "inventory",
                        "The recomputation inventory location set is stale relative to the lifecycle.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<VersionLifecycleV1> AcceptLifecycle(
            SpatialRecomputeRequestV1 request)
        {
            VersionLifecycleV1 lifecycle = request.Lifecycle;
            return lifecycle.TryAcceptSpatialSolve(
                lifecycle.CoreStateVersion,
                lifecycle.BundleNuclideVersions,
                lifecycle.TopologyVersion,
                request.ExpectedDataVersion,
                lifecycle.TopologyDigest.Value!,
                lifecycle.DataPackDigest.Value!,
                lifecycle.StateDigest.Value!,
                request.SpatialSolveId,
                request.PowerSnapshotId,
                request.CoefficientDigest,
                request.SnapshotDigest,
                request.ExactSimulationTimeSeconds);
        }

        private static ContractValidationResult<SpatialRecomputeResultV1> InvalidFromDiagnostic(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<SpatialRecomputeResultV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }
    }
}
