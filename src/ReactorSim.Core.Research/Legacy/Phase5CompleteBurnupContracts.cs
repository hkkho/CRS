using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Result of one complete-state burnup interval. The accepted snapshot is
    /// retained as evidence, while the resulting lifecycle explicitly moves
    /// to the target time and invalidates the snapshot binding.
    /// </summary>
    public sealed class CompleteBurnupIntervalResultV1
    {
        internal CompleteBurnupIntervalResultV1(
            BundleInventory sourceInventory,
            BundleInventory resultingInventory,
            VersionLifecycleV1 sourceLifecycle,
            VersionLifecycleV1 resultingLifecycle,
            CompletePowerSnapshotV1 powerSnapshot,
            IEnumerable<BurnupIntervalRecordV1> records,
            IEnumerable<SpatialCoefficientLookupBindingV1> lookupBindings)
        {
            SourceInventory = sourceInventory;
            ResultingInventory = resultingInventory;
            SourceLifecycle = sourceLifecycle;
            ResultingLifecycle = resultingLifecycle;
            PowerSnapshot = powerSnapshot;
            Records = new ReadOnlyCollection<BurnupIntervalRecordV1>(records.ToArray());
            LookupBindings = new ReadOnlyCollection<SpatialCoefficientLookupBindingV1>(
                lookupBindings.ToArray());
        }

        public BundleInventory SourceInventory { get; }

        public BundleInventory ResultingInventory { get; }

        public VersionLifecycleV1 SourceLifecycle { get; }

        public VersionLifecycleV1 ResultingLifecycle { get; }

        public CompletePowerSnapshotV1 PowerSnapshot { get; }

        public double CurrentTimeSeconds
        {
            get { return PowerSnapshot.SnapshotTimeSeconds; }
        }

        public double TargetTimeSeconds
        {
            get { return ResultingLifecycle.CurrentSimulationTimeSeconds; }
        }

        public double DeltaTimeSeconds
        {
            get { return TargetTimeSeconds - CurrentTimeSeconds; }
        }

        public ulong CoreStateVersionBefore
        {
            get { return SourceLifecycle.CoreStateVersion; }
        }

        public ulong CoreStateVersionAfter
        {
            get { return ResultingLifecycle.CoreStateVersion; }
        }

        public bool PowerSnapshotInvalidated
        {
            get { return ResultingLifecycle.PowerBindingStatus == BindingStatusV1.Invalid; }
        }

        public IReadOnlyList<BurnupIntervalRecordV1> Records { get; }

        public IReadOnlyList<SpatialCoefficientLookupBindingV1> LookupBindings { get; }
    }

    /// <summary>
    /// Atomic complete-state implementation of the frozen v1 left-endpoint
    /// burnup rule. It consumes only a complete accepted snapshot, performs
    /// every next-state coefficient lookup before constructing a result, and
    /// commits the inventory and lifecycle as one immutable replacement.
    /// </summary>
    public static class CompleteBurnupIntervalTransitionV1
    {
        public static ContractValidationResult<CompleteBurnupIntervalResultV1> TryApply(
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            CompletePowerSnapshotV1 powerSnapshot,
            double targetTimeSeconds,
            IEnumerable<BurnupCoefficientTableV1> coefficientTables,
            Digest32 nextStateDigest)
        {
            if (inventory == null || lifecycle == null || powerSnapshot == null ||
                coefficientTables == null || nextStateDigest == null)
            {
                return Invalid(
                    "CompleteBurnup.Input.Missing",
                    "transition",
                    "A complete burnup interval requires inventory, lifecycle, accepted snapshot, coefficient tables, and next digest.");
            }

            if (powerSnapshot.CoreStateVersion != lifecycle.CoreStateVersion ||
                powerSnapshot.SnapshotTimeSeconds != lifecycle.CurrentSimulationTimeSeconds)
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.StateBinding.Stale",
                    "power_snapshot",
                    "The accepted snapshot must bind the current core version and exact simulation time.");
            }

            if (!lifecycle.StateDigest.IsApplicable || lifecycle.StateDigest.Value == null ||
                !powerSnapshot.StateDigest.Equals(lifecycle.StateDigest.Value))
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.StateDigest.Stale",
                    "power_snapshot.state_digest",
                    "The accepted snapshot must bind the current lifecycle state digest.");
            }

            if (!lifecycle.PowerSnapshotInventoryDigest.IsApplicable ||
                !lifecycle.PowerSnapshotAcceptedInventoryDigest.IsApplicable ||
                !lifecycle.PowerSnapshotBurnupEnergyDigest.IsApplicable ||
                lifecycle.PowerSnapshotInventoryDigest.Value == null ||
                lifecycle.PowerSnapshotAcceptedInventoryDigest.Value == null ||
                lifecycle.PowerSnapshotBurnupEnergyDigest.Value == null ||
                !powerSnapshot.InventoryDigest.Equals(lifecycle.PowerSnapshotInventoryDigest.Value) ||
                !powerSnapshot.AcceptedInventoryDigest.Equals(lifecycle.PowerSnapshotAcceptedInventoryDigest.Value) ||
                !powerSnapshot.BurnupEnergyDigest.Equals(lifecycle.PowerSnapshotBurnupEnergyDigest.Value))
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.StateDigestBinding.Stale",
                    "power_snapshot.state_digests",
                    "The burnup transition must use the exact inventory and burnup digests authenticated by the accepted lifecycle.");
            }

            if (!CompleteStateDigestV1.ComputeInventory(inventory)
                .Equals(lifecycle.PowerSnapshotAcceptedInventoryDigest.Value!))
            {
                return Invalid(
                    "CompleteBurnup.AcceptedInventoryDigest.Stale",
                    "power_snapshot.accepted_inventory_digest",
                    "The accepted snapshot must bind the full post-accept inventory, including every power-history record and current power/coefficient binding.");
            }

            if (!CompleteStateDigestV1.ComputeBurnupEnergy(inventory).Equals(powerSnapshot.BurnupEnergyDigest))
            {
                return Invalid(
                    "CompleteBurnup.BurnupEnergyDigest.Stale",
                    "power_snapshot.burnup_energy_digest",
                    "The accepted snapshot must bind the canonical burnup and energy digest.");
            }

            if (!lifecycle.SpatialSolveId.IsApplicable ||
                !lifecycle.PowerSnapshotId.IsApplicable ||
                lifecycle.PowerSnapshotId.Value != powerSnapshot.PowerSnapshotId ||
                lifecycle.SpatialBindingStatus != BindingStatusV1.Valid ||
                lifecycle.PowerBindingStatus != BindingStatusV1.Valid ||
                !lifecycle.CoefficientDigest.IsApplicable ||
                !lifecycle.SnapshotDigest.IsApplicable ||
                lifecycle.SpatialStateVersion != powerSnapshot.SpatialStateVersion ||
                lifecycle.PowerSnapshotVersion != powerSnapshot.PowerSnapshotVersion)
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.Binding.Invalid",
                    "power_snapshot.binding",
                    "A positive-duration interval requires the current complete spatial and power lifecycle binding.");
            }

            if (lifecycle.SpatialSolveId.Value != powerSnapshot.SpatialSolveId ||
                lifecycle.CoefficientDigest.Value == null ||
                !lifecycle.CoefficientDigest.Value.Equals(powerSnapshot.CoefficientDigest) ||
                lifecycle.TopologyDigest.Value == null ||
                !lifecycle.TopologyDigest.Value.Equals(powerSnapshot.TopologyDigest) ||
                lifecycle.DataPackDigest.Value == null ||
                !lifecycle.DataPackDigest.Value.Equals(powerSnapshot.DataPackDigest) ||
                lifecycle.SnapshotDigest.Value == null ||
                !lifecycle.SnapshotDigest.Value.Equals(powerSnapshot.SnapshotDigest))
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.DigestBinding.Stale",
                    "power_snapshot.digests",
                    "The accepted snapshot must bind the exact live coefficient, topology, data-pack, and snapshot digests.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(inventory))
            {
                return Invalid(
                    "CompleteBurnup.InventoryBinding.Stale",
                    "inventory",
                    "The accepted snapshot must bind the exact current lifecycle identity and location set.");
            }

            if (!PowerHistoryRecordV1.IsCanonicalTime(targetTimeSeconds) ||
                targetTimeSeconds <= lifecycle.CurrentSimulationTimeSeconds)
            {
                return Invalid(
                    "CompleteBurnup.TargetTime.Invalid",
                    "target_time_s",
                    "A complete burnup interval requires a finite canonical target strictly after current time.");
            }

            double deltaTimeSeconds = targetTimeSeconds - lifecycle.CurrentSimulationTimeSeconds;
            if (!ContractValidation.IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0)
            {
                return Invalid(
                    "CompleteBurnup.DeltaTime.Invalid",
                    "delta_time_s",
                    "The complete burnup interval duration must be finite and strictly positive.");
            }

            ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>> tablesResult =
                BuildTableMap(coefficientTables, lifecycle.DataPackVersion);
            if (!tablesResult.IsValid)
            {
                return Invalid(
                    tablesResult.FirstDiagnostic.Code,
                    tablesResult.FirstDiagnostic.Path,
                    tablesResult.FirstDiagnostic.Message);
            }

            Dictionary<StableId, CompletePowerSnapshotBundleV1> snapshotEntries =
                powerSnapshot.Bundles.ToDictionary(entry => entry.BundleId, entry => entry);
            Dictionary<StableId, BundleNuclideVersionV1> lifecycleVersions = lifecycle.BundleNuclideVersions
                .ToDictionary(version => version.BundleId, version => version);
            var proposedBundles = new List<BundleState>(inventory.OccupiedCount);
            var records = new List<BurnupIntervalRecordV1>(inventory.OccupiedCount);
            var lookupBindings = new List<SpatialCoefficientLookupBindingV1>(inventory.OccupiedCount);

            foreach (BundleState bundle in inventory.EnumerateOccupied()
                         .OrderBy(candidate => candidate.ChannelId.Value)
                         .ThenBy(candidate => candidate.Position.Value)
                         .ThenBy(candidate => candidate.BundleId))
            {
                string path = "bundle[" + bundle.BundleId + "].burnup";
                if (bundle.NuclideState == null)
                {
                    return Invalid(
                        "CompleteBurnup.NuclideState.Missing",
                        path,
                        "Every complete burnup bundle requires an explicit nuclide envelope.");
                }

                BundleNuclideVersionV1 lifecycleVersion;
                if (!lifecycleVersions.TryGetValue(bundle.BundleId, out lifecycleVersion) ||
                    lifecycleVersion.NuclideStateVersion != bundle.NuclideState.NuclideStateVersion)
                {
                    return Invalid(
                        "CompleteBurnup.NuclideState.VersionMismatch",
                        path,
                        "The bundle envelope version must equal the current lifecycle version.");
                }

                CompletePowerSnapshotBundleV1 snapshotEntry;
                if (!snapshotEntries.TryGetValue(bundle.BundleId, out snapshotEntry) ||
                    snapshotEntry.ChannelId != bundle.ChannelId ||
                    snapshotEntry.Position != bundle.Position ||
                    snapshotEntry.NuclideStateVersion != bundle.NuclideState.NuclideStateVersion)
                {
                    return Invalid(
                        "CompleteBurnup.PowerSnapshot.BundleBinding.Stale",
                        path,
                        "Every snapshot entry must bind the current bundle location and nuclide version.");
                }

                if (!bundle.PowerWatts.IsApplicable ||
                    !bundle.PowerSnapshotId.IsApplicable ||
                    bundle.PowerSnapshotId.Value != powerSnapshot.PowerSnapshotId ||
                    bundle.PowerWatts.Value != snapshotEntry.PowerWatts ||
                    bundle.CoefficientBinding == null ||
                    !SameCoefficientBinding(bundle.CoefficientBinding, snapshotEntry.CoefficientBinding))
                {
                    return Invalid(
                        "CompleteBurnup.PowerSnapshot.BundleBinding.Invalid",
                        path,
                        "The live bundle must retain the exact accepted power and coefficient binding before advancement.");
                }

                if (bundle.PowerHistory.Count == 0)
                {
                    return Invalid(
                        "CompleteBurnup.PowerHistory.Missing",
                        path + ".power_history",
                        "The current complete power binding must have an append-only accepted history record.");
                }

                PowerHistoryRecordV1 currentHistory = bundle.PowerHistory[bundle.PowerHistory.Count - 1];
                if (currentHistory.SnapshotTimeSeconds != powerSnapshot.SnapshotTimeSeconds ||
                    currentHistory.PowerWatts != snapshotEntry.PowerWatts ||
                    currentHistory.CoreStateVersion != powerSnapshot.CoreStateVersion ||
                    currentHistory.SpatialStateVersion != powerSnapshot.SpatialStateVersion ||
                    currentHistory.PowerSnapshotVersion != powerSnapshot.PowerSnapshotVersion)
                {
                    return Invalid(
                        "CompleteBurnup.PowerHistory.Stale",
                        path + ".power_history",
                        "The latest append-only power-history record must bind the accepted snapshot exactly.");
                }

                BurnupCoefficientTableV1 table;
                if (!tablesResult.Value.TryGetValue(bundle.MaterialVariantId, out table!))
                {
                    return Invalid(
                        "CompleteBurnup.CoefficientTable.Missing",
                        path + ".material_variant_id",
                        "A coefficient table is required for every live material variant.");
                }

                double oldBurnup = bundle.CurrentBurnupJPerKgHm;
                if (!ContractValidation.IsFinite(oldBurnup) || oldBurnup < 0)
                {
                    return Invalid(
                        "CompleteBurnup.Burnup.Invalid",
                        path + ".old_burnup_j_per_kg_hm",
                        "The current derived burnup must be finite and nonnegative.");
                }

                double deltaEnergyJ = snapshotEntry.PowerWatts * deltaTimeSeconds;
                double newEnergyJ = bundle.CumulativeFissionEnergyJ + deltaEnergyJ;
                if (!ContractValidation.IsFinite(snapshotEntry.PowerWatts) || snapshotEntry.PowerWatts < 0 ||
                    !ContractValidation.IsFinite(deltaEnergyJ) || deltaEnergyJ < 0 ||
                    !ContractValidation.IsFinite(newEnergyJ) || newEnergyJ < bundle.CumulativeFissionEnergyJ)
                {
                    return Invalid(
                        "CompleteBurnup.Energy.Invalid",
                        path + ".cumulative_fission_energy_j",
                        "Burnup energy must remain finite, nonnegative, and monotone without clamping.");
                }

                double newBurnup = bundle.InitialBurnupJPerKgHm + newEnergyJ / bundle.HeavyMetalMassKg;
                if (!ContractValidation.IsFinite(newBurnup) || newBurnup < 0 || newBurnup < oldBurnup)
                {
                    return Invalid(
                        "CompleteBurnup.Burnup.Monotonicity.Invalid",
                        path + ".burnup_j_per_kg_hm",
                        "Derived burnup must remain finite, nonnegative, and monotone.");
                }

                ContractValidationResult<BurnupCoefficientLookupResultV1> lookup = table.TryLookup(newBurnup);
                if (!lookup.IsValid)
                {
                    return Invalid(
                        lookup.FirstDiagnostic.Code,
                        path + "." + lookup.FirstDiagnostic.Path,
                        lookup.FirstDiagnostic.Message);
                }

                ContractValidationResult<BundleCoefficientBindingV1> coefficientBinding =
                    BundleCoefficientBindingV1.TryCreate(
                        lookup.Value.TableId,
                        lookup.Value.BracketLowerIndex,
                        lookup.Value.BracketUpperIndex,
                        lookup.Value.InterpolationFraction,
                        lookup.Value.Checksum);
                if (!coefficientBinding.IsValid)
                {
                    return Invalid(
                        coefficientBinding.FirstDiagnostic.Code,
                        path + ".coefficient_binding",
                        coefficientBinding.FirstDiagnostic.Message);
                }

                proposedBundles.Add(bundle.WithEnergyAndCoefficientBinding(
                    newEnergyJ,
                    coefficientBinding.Value));
                records.Add(new BurnupIntervalRecordV1(
                    bundle.BundleId,
                    bundle.ChannelId,
                    bundle.Position,
                    deltaTimeSeconds,
                    snapshotEntry.PowerWatts,
                    deltaEnergyJ,
                    bundle.CumulativeFissionEnergyJ,
                    newEnergyJ,
                    oldBurnup,
                    newBurnup));
                lookupBindings.Add(new SpatialCoefficientLookupBindingV1(
                    bundle.BundleId,
                    bundle.Node,
                    bundle.MaterialVariantId,
                    newBurnup,
                    lookup.Value));
            }

            if (snapshotEntries.Count != proposedBundles.Count)
            {
                return Invalid(
                    "CompleteBurnup.PowerSnapshot.BundleCount.Mismatch",
                    "power_snapshot.bundles",
                    "The accepted snapshot must contain exactly one entry per live bundle.");
            }

            ContractValidationResult<BundleInventory> resultingInventory = BundleInventory.TryCreate(
                inventory.Topology,
                proposedBundles);
            if (!resultingInventory.IsValid)
            {
                return Invalid(
                    resultingInventory.FirstDiagnostic.Code,
                    resultingInventory.FirstDiagnostic.Path,
                    resultingInventory.FirstDiagnostic.Message);
            }

            ContractValidationResult<VersionLifecycleV1> resultingLifecycle =
                lifecycle.TryCommitBurnupInterval(
                    lifecycle.CoreStateVersion,
                    targetTimeSeconds,
                    nextStateDigest);
            if (!resultingLifecycle.IsValid)
            {
                return Invalid(
                    resultingLifecycle.FirstDiagnostic.Code,
                    resultingLifecycle.FirstDiagnostic.Path,
                    resultingLifecycle.FirstDiagnostic.Message);
            }

            return ContractValidationResult<CompleteBurnupIntervalResultV1>.Valid(
                new CompleteBurnupIntervalResultV1(
                    inventory,
                    resultingInventory.Value,
                    lifecycle,
                    resultingLifecycle.Value,
                    powerSnapshot,
                    records,
                    lookupBindings));
        }

        private static ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>> BuildTableMap(
            IEnumerable<BurnupCoefficientTableV1> coefficientTables,
            string expectedDataPackVersion)
        {
            var map = new Dictionary<MaterialVariantId, BurnupCoefficientTableV1>();
            int index = 0;
            foreach (BurnupCoefficientTableV1 table in coefficientTables)
            {
                string path = "coefficient_tables[" + index + "]";
                if (table == null)
                {
                    return ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>>.Invalid(
                        "CompleteBurnup.CoefficientTable.Null",
                        path,
                        "A coefficient table may not be null.");
                }

                if (!string.Equals(table.DataVersion, expectedDataPackVersion, StringComparison.Ordinal))
                {
                    return ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>>.Invalid(
                        "CompleteBurnup.CoefficientTable.DataVersion.Stale",
                        path + ".data_version",
                        "Every coefficient table must bind the lifecycle data-pack version.");
                }

                if (!map.TryAdd(table.MaterialVariantId, table))
                {
                    return ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>>.Invalid(
                        "CompleteBurnup.CoefficientTable.Duplicate",
                        path + ".material_variant_id",
                        "At most one coefficient table may be supplied per material variant.");
                }

                index++;
            }

            if (map.Count == 0)
            {
                return ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>>.Invalid(
                    "CompleteBurnup.CoefficientTable.Empty",
                    "coefficient_tables",
                    "At least one coefficient table is required for a complete burnup commit.");
            }

            return ContractValidationResult<Dictionary<MaterialVariantId, BurnupCoefficientTableV1>>.Valid(map);
        }

        private static bool SameCoefficientBinding(
            BundleCoefficientBindingV1 left,
            BundleCoefficientBindingV1 right)
        {
            return left.TableId == right.TableId &&
                   left.LowerIndex == right.LowerIndex &&
                   left.UpperIndex == right.UpperIndex &&
                   left.InterpolationFraction == right.InterpolationFraction &&
                   left.TableDigest.Equals(right.TableDigest);
        }

        private static ContractValidationResult<CompleteBurnupIntervalResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompleteBurnupIntervalResultV1>.Invalid(code, path, message);
        }
    }
}
