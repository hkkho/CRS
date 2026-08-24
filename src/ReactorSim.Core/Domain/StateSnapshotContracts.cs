using System;

namespace ReactorSim.Core
{
    /// <summary>
    /// An immutable event-boundary state snapshot. The digest is supplied by
    /// the owning state/serialization contract; this task does not calculate
    /// canonical bytes or implement save/load.
    /// </summary>
    public sealed class StateSnapshotV1
    {
        public const uint CurrentSchemaVersion = 2;

        private StateSnapshotV1(
            uint schemaVersion,
            StableId snapshotId,
            ulong snapshotVersion,
            double simulationTimeSeconds,
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            StateBindingV1 stateBinding,
            Digest32 snapshotDigest)
        {
            SchemaVersion = schemaVersion;
            SnapshotId = snapshotId;
            SnapshotVersion = snapshotVersion;
            SimulationTimeSeconds = simulationTimeSeconds;
            Inventory = inventory;
            Lifecycle = lifecycle;
            StateBinding = stateBinding;
            SnapshotDigest = snapshotDigest;
        }

        public uint SchemaVersion { get; }

        public StableId SnapshotId { get; }

        public ulong SnapshotVersion { get; }

        public double SimulationTimeSeconds { get; }

        public BundleInventory Inventory { get; }

        public VersionLifecycleV1 Lifecycle { get; }

        public StateBindingV1 StateBinding { get; }

        public Digest32 SnapshotDigest { get; }

        public static ContractValidationResult<StateSnapshotV1> TryCreate(
            StableId snapshotId,
            ulong snapshotVersion,
            double simulationTimeSeconds,
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            Digest32 snapshotDigest)
        {
            if (snapshotId.IsEmpty)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Id.Empty",
                    "snapshot_id",
                    "A state snapshot requires an explicit stable identity.");
            }

            if (!ContractValidation.IsFinite(simulationTimeSeconds) || simulationTimeSeconds < 0)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Time.Invalid",
                    "simulation_time_s",
                    "Snapshot time must be finite and nonnegative SI seconds.");
            }

            if (inventory == null)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Inventory.Missing",
                    "inventory",
                    "A state snapshot requires a validated bundle inventory.");
            }

            if (lifecycle == null)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Lifecycle.Missing",
                    "lifecycle",
                    "A state snapshot requires a validated version lifecycle.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(inventory))
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.StateMismatch",
                    "inventory",
                    "Snapshot inventory and topology must match the lifecycle boundary exactly.");
            }

            if (snapshotDigest == null)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Digest.Missing",
                    "snapshot_digest",
                    "A state snapshot requires an explicit 32-byte digest.");
            }

            if (lifecycle.PowerBindingStatus != BindingStatusV1.Valid ||
                !lifecycle.PowerSnapshotId.IsApplicable ||
                lifecycle.PowerSnapshotId.Value != snapshotId ||
                !lifecycle.SnapshotDigest.IsApplicable ||
                !lifecycle.SnapshotDigest.Value!.Equals(snapshotDigest) ||
                lifecycle.PowerSnapshotVersion != snapshotVersion)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Binding.Mismatch",
                    "snapshot_binding",
                    "A state snapshot must equal the accepted power-snapshot identity, version, and digest.");
            }

            if (simulationTimeSeconds != lifecycle.CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateSnapshot.Time.Stale",
                    "simulation_time_s",
                    "Snapshot time must equal the lifecycle's current explicit state time.");
            }

            return ContractValidationResult<StateSnapshotV1>.Valid(
                new StateSnapshotV1(
                    CurrentSchemaVersion,
                    snapshotId,
                    snapshotVersion,
                    simulationTimeSeconds,
                    inventory,
                    lifecycle,
                    lifecycle.CreateStateBinding(),
                    snapshotDigest));
        }
    }
}
