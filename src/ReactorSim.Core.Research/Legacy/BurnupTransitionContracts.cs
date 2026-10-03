using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One accepted node-power value projected onto a persistent bundle.
    /// This bounded P5-T04 record carries the identity and location needed to
    /// bind power to the current BundleInventory; the complete P2-T03 power
    /// history and digest envelope remain later state-contract work.
    /// </summary>
    public sealed class BundlePowerSampleV1
    {
        private BundlePowerSampleV1(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            double powerWatts)
        {
            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
            PowerWatts = powerWatts;
        }

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public NodeKey Node
        {
            get { return new NodeKey(ChannelId, Position); }
        }

        public double PowerWatts { get; }

        public static ContractValidationResult<BundlePowerSampleV1> TryCreate(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            double powerWatts)
        {
            if (bundleId.IsEmpty)
            {
                return ContractValidationResult<BundlePowerSampleV1>.Invalid(
                    "BundlePowerSample.BundleId.Empty",
                    "bundle_id",
                    "Every accepted bundle-power sample requires a stable bundle identity.");
            }

            if (!ContractValidation.IsFinite(powerWatts) || powerWatts < 0)
            {
                return ContractValidationResult<BundlePowerSampleV1>.Invalid(
                    "BundlePowerSample.Power.Invalid",
                    "power_w",
                    "Accepted node power must be finite and nonnegative SI watts.");
            }

            return ContractValidationResult<BundlePowerSampleV1>.Valid(
                new BundlePowerSampleV1(bundleId, channelId, position, powerWatts));
        }
    }

    /// <summary>
    /// The bounded accepted-power input for one explicit burnup interval.
    /// It requires a stable snapshot identity, exact snapshot time, state
    /// version, and one finite nonnegative sample per live bundle after the
    /// transition binds it to an inventory. It is not the complete P2-T03
    /// PowerSnapshot with all digest and retained power-history fields.
    /// </summary>
    public sealed class AcceptedBundlePowerSnapshotV1
    {
        private AcceptedBundlePowerSnapshotV1(
            StableId snapshotId,
            double snapshotTimeSeconds,
            ulong coreStateVersion,
            Digest32 stateDigest,
            IEnumerable<BundlePowerSampleV1> bundlePowers)
        {
            SnapshotId = snapshotId;
            SnapshotTimeSeconds = snapshotTimeSeconds;
            CoreStateVersion = coreStateVersion;
            StateDigest = stateDigest;
            BundlePowers = new ReadOnlyCollection<BundlePowerSampleV1>(bundlePowers.ToArray());
        }

        public StableId SnapshotId { get; }

        public double SnapshotTimeSeconds { get; }

        public ulong CoreStateVersion { get; }

        /// <summary>
        /// Opaque digest of the exact source state. The state owner supplies
        /// and verifies the canonical bytes; this bounded transition only
        /// requires the digest to match at application time.
        /// </summary>
        public Digest32 StateDigest { get; }

        public IReadOnlyList<BundlePowerSampleV1> BundlePowers { get; }

        public static ContractValidationResult<AcceptedBundlePowerSnapshotV1> TryCreate(
            StableId snapshotId,
            double snapshotTimeSeconds,
            ulong coreStateVersion,
            Digest32 stateDigest,
            IEnumerable<BundlePowerSampleV1> bundlePowers)
        {
            if (snapshotId.IsEmpty)
            {
                return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                    "AcceptedPowerSnapshot.Id.Empty",
                    "snapshot_id",
                    "An accepted power snapshot requires a stable identity.");
            }

            if (!ContractValidation.IsFinite(snapshotTimeSeconds) || snapshotTimeSeconds < 0)
            {
                return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                    "AcceptedPowerSnapshot.Time.Invalid",
                    "snapshot_time_s",
                    "A power snapshot time must be finite and nonnegative SI seconds.");
            }

            if (stateDigest == null)
            {
                return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                    "AcceptedPowerSnapshot.StateDigest.Missing",
                    "state_digest",
                    "An accepted power snapshot requires an explicit source-state digest.");
            }

            if (bundlePowers == null)
            {
                return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                    "AcceptedPowerSnapshot.BundlePowers.Missing",
                    "bundle_powers",
                    "An accepted power snapshot requires its bundle-power records.");
            }

            BundlePowerSampleV1[] records = bundlePowers.ToArray();
            var knownBundleIds = new HashSet<StableId>();
            var knownNodes = new HashSet<NodeKey>();
            for (int index = 0; index < records.Length; index++)
            {
                BundlePowerSampleV1 sample = records[index];
                string path = "bundle_powers[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (sample == null)
                {
                    return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                        "AcceptedPowerSnapshot.BundlePower.Null",
                        path,
                        "A bundle-power record may not be null.");
                }

                if (sample.BundleId.IsEmpty)
                {
                    return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                        "AcceptedPowerSnapshot.BundlePower.BundleId.Empty",
                        path + ".bundle_id",
                        "Every bundle-power record requires a stable bundle identity.");
                }

                if (!knownBundleIds.Add(sample.BundleId))
                {
                    return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                        "AcceptedPowerSnapshot.BundlePower.BundleId.Duplicate",
                        path + ".bundle_id",
                        "An accepted power snapshot may contain at most one sample per bundle identity.");
                }

                if (!knownNodes.Add(sample.Node))
                {
                    return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                        "AcceptedPowerSnapshot.BundlePower.Node.Duplicate",
                        path + ".node",
                        "An accepted power snapshot may contain at most one sample per spatial node.");
                }

                if (!ContractValidation.IsFinite(sample.PowerWatts) || sample.PowerWatts < 0)
                {
                    return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Invalid(
                        "AcceptedPowerSnapshot.BundlePower.Power.Invalid",
                        path + ".power_w",
                        "Accepted node power must be finite and nonnegative SI watts.");
                }
            }

            BundlePowerSampleV1[] canonical = records
                .OrderBy(sample => sample.ChannelId.Value)
                .ThenBy(sample => sample.Position.Value)
                .ThenBy(sample => sample.BundleId)
                .ToArray();

            return ContractValidationResult<AcceptedBundlePowerSnapshotV1>.Valid(
                new AcceptedBundlePowerSnapshotV1(
                    snapshotId,
                    snapshotTimeSeconds,
                    coreStateVersion,
                    stateDigest,
                    canonical));
        }
    }

    /// <summary>
    /// Auditable per-bundle result for one successful left-endpoint burnup
    /// interval. All energy values are SI joules, powers are SI watts, time
    /// values are SI seconds, and burnups are SI J/kg_HM.
    /// </summary>
    public sealed class BurnupIntervalRecordV1
    {
        internal BurnupIntervalRecordV1(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            double deltaTimeSeconds,
            double powerWatts,
            double deltaFissionEnergyJ,
            double oldCumulativeFissionEnergyJ,
            double newCumulativeFissionEnergyJ,
            double oldBurnupJPerKgHm,
            double newBurnupJPerKgHm)
        {
            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
            DeltaTimeSeconds = deltaTimeSeconds;
            PowerWatts = powerWatts;
            DeltaFissionEnergyJ = deltaFissionEnergyJ;
            OldCumulativeFissionEnergyJ = oldCumulativeFissionEnergyJ;
            NewCumulativeFissionEnergyJ = newCumulativeFissionEnergyJ;
            OldBurnupJPerKgHm = oldBurnupJPerKgHm;
            NewBurnupJPerKgHm = newBurnupJPerKgHm;
        }

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public double DeltaTimeSeconds { get; }

        public double PowerWatts { get; }

        public double DeltaFissionEnergyJ { get; }

        public double OldCumulativeFissionEnergyJ { get; }

        public double NewCumulativeFissionEnergyJ { get; }

        public double OldBurnupJPerKgHm { get; }

        public double NewBurnupJPerKgHm { get; }
    }

    /// <summary>
    /// Immutable proposed state from one successful P5-T04 burnup interval.
    /// The old accepted power snapshot is marked invalid for the resulting
    /// state; a later solver task must provide the next exact-time snapshot.
    /// </summary>
    public sealed class BurnupIntervalResultV1
    {
        internal BurnupIntervalResultV1(
            BundleInventory sourceInventory,
            BundleInventory resultingInventory,
            AcceptedBundlePowerSnapshotV1 powerSnapshot,
            double currentTimeSeconds,
            double targetTimeSeconds,
            ulong coreStateVersionBefore,
            ulong coreStateVersionAfter,
            IEnumerable<BurnupIntervalRecordV1> records)
        {
            SourceInventory = sourceInventory;
            ResultingInventory = resultingInventory;
            PowerSnapshot = powerSnapshot;
            CurrentTimeSeconds = currentTimeSeconds;
            TargetTimeSeconds = targetTimeSeconds;
            DeltaTimeSeconds = targetTimeSeconds - currentTimeSeconds;
            CoreStateVersionBefore = coreStateVersionBefore;
            CoreStateVersionAfter = coreStateVersionAfter;
            PowerSnapshotInvalidated = true;
            Records = new ReadOnlyCollection<BurnupIntervalRecordV1>(records.ToArray());
        }

        public BundleInventory SourceInventory { get; }

        public BundleInventory ResultingInventory { get; }

        public AcceptedBundlePowerSnapshotV1 PowerSnapshot { get; }

        public double CurrentTimeSeconds { get; }

        public double TargetTimeSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public ulong CoreStateVersionBefore { get; }

        public ulong CoreStateVersionAfter { get; }

        public bool PowerSnapshotInvalidated { get; }

        public IReadOnlyList<BurnupIntervalRecordV1> Records { get; }
    }

    /// <summary>
    /// Applies the approved deterministic left-endpoint energy rule to the
    /// fields represented by the current immutable BundleInventory. Every
    /// bundle is validated and computed before a replacement inventory is
    /// returned, so failure leaves the source inventory untouched.
    /// </summary>
    public static class BurnupIntervalTransition
    {
        public static ContractValidationResult<BurnupIntervalResultV1> TryApply(
            BundleInventory inventory,
            ulong currentCoreStateVersion,
            double currentTimeSeconds,
            double targetTimeSeconds,
            Digest32 currentStateDigest,
            AcceptedBundlePowerSnapshotV1 powerSnapshot)
        {
            if (inventory == null)
            {
                return Invalid(
                    "BurnupInterval.Inventory.Missing",
                    "inventory",
                    "A validated bundle inventory is required.");
            }

            if (powerSnapshot == null)
            {
                return Invalid(
                    "BurnupInterval.PowerSnapshot.Missing",
                    "power_snapshot",
                    "A validated accepted bundle-power snapshot is required.");
            }

            if (currentStateDigest == null)
            {
                return Invalid(
                    "BurnupInterval.StateDigest.Missing",
                    "state_digest",
                    "The explicit current source-state digest is required for snapshot binding.");
            }

            if (!ContractValidation.IsFinite(currentTimeSeconds) || currentTimeSeconds < 0)
            {
                return Invalid(
                    "BurnupInterval.CurrentTime.Invalid",
                    "current_time_s",
                    "Current time must be finite and nonnegative SI seconds.");
            }

            if (!ContractValidation.IsFinite(targetTimeSeconds) || targetTimeSeconds < 0)
            {
                return Invalid(
                    "BurnupInterval.TargetTime.Invalid",
                    "target_time_s",
                    "Target time must be finite and nonnegative SI seconds.");
            }

            if (targetTimeSeconds <= currentTimeSeconds)
            {
                return Invalid(
                    "BurnupInterval.TargetTime.NotAfterCurrent",
                    "target_time_s",
                    "A burnup interval requires a strictly later target time.");
            }

            if (powerSnapshot.SnapshotTimeSeconds != currentTimeSeconds)
            {
                return Invalid(
                    "BurnupInterval.PowerSnapshot.Time.Stale",
                    "power_snapshot.snapshot_time_s",
                    "The accepted power snapshot time must equal the current explicit state time.");
            }

            if (powerSnapshot.CoreStateVersion != currentCoreStateVersion)
            {
                return Invalid(
                    "BurnupInterval.PowerSnapshot.CoreStateVersion.Stale",
                    "power_snapshot.core_state_version",
                    "The accepted power snapshot must bind the current core-state version.");
            }

            if (!powerSnapshot.StateDigest.Equals(currentStateDigest))
            {
                return Invalid(
                    "BurnupInterval.PowerSnapshot.StateDigest.Stale",
                    "power_snapshot.state_digest",
                    "The accepted power snapshot must bind the exact current source state.");
            }

            if (currentCoreStateVersion == ulong.MaxValue)
            {
                return Invalid(
                    "BurnupInterval.CoreStateVersion.Overflow",
                    "core_state_version",
                    "A successful burnup interval must increment the core-state version.");
            }

            double deltaTimeSeconds = targetTimeSeconds - currentTimeSeconds;
            if (!ContractValidation.IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0)
            {
                return Invalid(
                    "BurnupInterval.DeltaTime.Invalid",
                    "delta_time_s",
                    "The explicit interval duration must be finite and strictly positive SI seconds.");
            }

            BundleState[] canonicalBundles = inventory.EnumerateOccupied()
                .OrderBy(bundle => bundle.ChannelId.Value)
                .ThenBy(bundle => bundle.Position.Value)
                .ThenBy(bundle => bundle.BundleId)
                .ToArray();

            var powersByBundleId = new Dictionary<StableId, BundlePowerSampleV1>();
            foreach (BundlePowerSampleV1 sample in powerSnapshot.BundlePowers)
            {
                if (!powersByBundleId.TryAdd(sample.BundleId, sample))
                {
                    return Invalid(
                        "BurnupInterval.PowerSnapshot.BundlePower.Duplicate",
                        "power_snapshot.bundle_powers.bundle_id",
                        "The accepted power snapshot may contain only one power sample per bundle.");
                }
            }

            if (powersByBundleId.Count != canonicalBundles.Length)
            {
                return Invalid(
                    "BurnupInterval.PowerSnapshot.BundlePower.CountMismatch",
                    "power_snapshot.bundle_powers",
                    "The accepted power snapshot must contain exactly one power sample for every live bundle.");
            }

            BundleState[] proposedBundles = new BundleState[canonicalBundles.Length];
            var records = new BurnupIntervalRecordV1[canonicalBundles.Length];
            for (int index = 0; index < canonicalBundles.Length; index++)
            {
                BundleState bundle = canonicalBundles[index];
                string path = ContractValidation.NodePath(bundle.Node, ".burnup");
                BundlePowerSampleV1 sample;
                if (!powersByBundleId.TryGetValue(bundle.BundleId, out sample!))
                {
                    return Invalid(
                        "BurnupInterval.PowerSnapshot.BundlePower.Missing",
                        path + ".power_w",
                        "The accepted power snapshot is missing the live bundle's node power.");
                }

                if (sample.Node != bundle.Node)
                {
                    return Invalid(
                        "BurnupInterval.PowerSnapshot.BundlePower.LocationMismatch",
                        path + ".power_w",
                        "The accepted node power must bind the live bundle's explicit channel and position.");
                }

                double oldEnergy = bundle.CumulativeFissionEnergyJ;
                double oldBurnup = bundle.CurrentBurnupJPerKgHm;
                double intervalEnergy = sample.PowerWatts * deltaTimeSeconds;
                if (!ContractValidation.IsFinite(intervalEnergy) || intervalEnergy < 0)
                {
                    return Invalid(
                        "BurnupInterval.EnergyIncrement.Invalid",
                        path + ".delta_fission_energy_j",
                        "The power-times-time energy increment must be finite and nonnegative SI joules.");
                }

                double newEnergy = oldEnergy + intervalEnergy;
                if (!ContractValidation.IsFinite(newEnergy) || newEnergy < 0 || newEnergy < oldEnergy)
                {
                    return Invalid(
                        "BurnupInterval.CumulativeEnergy.Invalid",
                        path + ".new_cumulative_fission_energy_j",
                        "Cumulative fission energy must remain finite, nonnegative, and monotone.");
                }

                if (intervalEnergy > 0 && newEnergy == oldEnergy)
                {
                    return Invalid(
                        "BurnupInterval.CumulativeEnergy.NoRepresentableIncrease",
                        path + ".new_cumulative_fission_energy_j",
                        "A positive interval energy must produce a representable cumulative-energy increase.");
                }

                double newBurnup = bundle.InitialBurnupJPerKgHm + newEnergy / bundle.HeavyMetalMassKg;
                if (!ContractValidation.IsFinite(oldBurnup) || oldBurnup < 0)
                {
                    return Invalid(
                        "BurnupInterval.OldBurnup.Invalid",
                        path + ".old_burnup_j_per_kg_hm",
                        "The source bundle's derived burnup must be finite and nonnegative.");
                }

                if (!ContractValidation.IsFinite(newBurnup) || newBurnup < 0 || newBurnup < oldBurnup)
                {
                    return Invalid(
                        "BurnupInterval.NewBurnup.Invalid",
                        path + ".new_burnup_j_per_kg_hm",
                        "Derived burnup must remain finite, nonnegative, and monotone.");
                }

                proposedBundles[index] = bundle.WithEnergy(newEnergy);
                records[index] = new BurnupIntervalRecordV1(
                    bundle.BundleId,
                    bundle.ChannelId,
                    bundle.Position,
                    deltaTimeSeconds,
                    sample.PowerWatts,
                    intervalEnergy,
                    oldEnergy,
                    newEnergy,
                    oldBurnup,
                    newBurnup);
            }

            ContractValidationResult<BundleInventory> resultingInventory =
                BundleInventory.TryCreate(inventory.Topology, proposedBundles);
            if (!resultingInventory.IsValid)
            {
                return Invalid(
                    "BurnupInterval.ResultingInventory.Invalid",
                    resultingInventory.FirstDiagnostic.Path,
                    resultingInventory.FirstDiagnostic.Message);
            }

            return ContractValidationResult<BurnupIntervalResultV1>.Valid(
                new BurnupIntervalResultV1(
                    inventory,
                    resultingInventory.Value,
                    powerSnapshot,
                    currentTimeSeconds,
                    targetTimeSeconds,
                    currentCoreStateVersion,
                    currentCoreStateVersion + 1,
                    records));
        }

        private static ContractValidationResult<BurnupIntervalResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<BurnupIntervalResultV1>.Invalid(code, path, message);
        }
    }
}
