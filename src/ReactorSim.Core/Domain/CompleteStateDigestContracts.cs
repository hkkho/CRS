using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Canonical projection of a complete bundle state. It is deliberately a
    /// fixed field-order digest input, not a JSON or Unity serialization
    /// format.
    /// </summary>
    internal static class CompleteBundleStateCodecV1
    {
        public static byte[] ToCanonicalBytes(BundleState bundle)
        {
            return ToCanonicalBytes(bundle, bundle.InsertedAtSeconds);
        }

        public static byte[] ToCanonicalBytes(BundleState bundle, double currentTimeSeconds)
        {
            return Phase5CanonicalBytesV1.Build(writer => Write(writer, bundle, currentTimeSeconds));
        }

        public static void Write(BinaryWriter writer, BundleState bundle, double currentTimeSeconds)
        {
            Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-BUNDLE-STATE-V1");
            writer.Write((byte)0);
            Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
            Phase5CanonicalBytesV1.WriteStableId(writer, bundle.BundleId);
            Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.ChannelId.Value);
            Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.Position.Value);
            Phase5CanonicalBytesV1.WriteString(writer, bundle.MaterialVariantId.Value);
            Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InitialBurnupJPerKgHm);
            Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CumulativeFissionEnergyJ);
            Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CurrentBurnupJPerKgHm);
            Phase5CanonicalBytesV1.WriteDouble(writer, bundle.HeavyMetalMassKg);
            Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InsertedAtSeconds);
            Phase5CanonicalBytesV1.WriteDouble(writer, currentTimeSeconds - bundle.InsertedAtSeconds);
            Phase5CanonicalBytesV1.WriteUInt64(writer, bundle.StateVersion);
            WritePowerState(writer, bundle);
            Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)bundle.PowerHistory.Count));
            foreach (PowerHistoryRecordV1 record in bundle.PowerHistory)
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
            }

            WriteOptionalCoefficient(writer, bundle.CoefficientBinding);
            writer.Write(bundle.NuclideState == null ? (byte)0 : (byte)1);
            if (bundle.NuclideState != null)
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, bundle.NuclideState.ToCanonicalBytes());
            }
        }

        public static void WritePowerState(BinaryWriter writer, BundleState bundle)
        {
            OptionalPowerWattsV1 power = bundle.PowerWatts ?? OptionalPowerWattsV1.NotApplicable;
            OptionalStableId snapshotId = bundle.PowerSnapshotId ?? OptionalStableId.NotApplicable;
            writer.Write(power.IsApplicable ? (byte)1 : (byte)0);
            if (power.IsApplicable)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, power.Value);
            }

            WriteOptionalStableId(writer, snapshotId);
        }

        public static void WriteOptionalCoefficient(
            BinaryWriter writer,
            BundleCoefficientBindingV1? coefficientBinding)
        {
            writer.Write(coefficientBinding == null ? (byte)0 : (byte)1);
            if (coefficientBinding != null)
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, coefficientBinding.ToCanonicalBytes());
            }
        }

        public static void WriteOptionalStableId(BinaryWriter writer, OptionalStableId value)
        {
            writer.Write(value.IsApplicable ? (byte)1 : (byte)0);
            if (value.IsApplicable)
            {
                Phase5CanonicalBytesV1.WriteStableId(writer, value.Value);
            }
        }

        public static void WriteOptionalDigest(BinaryWriter writer, OptionalDigest32 value)
        {
            writer.Write(value.IsApplicable ? (byte)1 : (byte)0);
            if (value.IsApplicable)
            {
                Phase5CanonicalBytesV1.WriteDigest(writer, value.Value!);
            }
        }

        public static void WriteOptionalText(BinaryWriter writer, OptionalTextV1 value)
        {
            writer.Write(value.IsApplicable ? (byte)1 : (byte)0);
            if (value.IsApplicable)
            {
                Phase5CanonicalBytesV1.WriteString(writer, value.Value!);
            }
        }
    }

    /// <summary>
    /// Deterministic before/proposed state digest projection used by the
    /// complete refuelling transaction. The lifecycle's explicit counters and
    /// bindings are included alongside the ordered complete inventory.
    /// </summary>
    public static class CompleteStateDigestV1
    {
        public static Digest32 ComputeInventory(BundleInventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return ComputeInventory("CANDU-COMPLETE-INVENTORY-V1", inventory);
        }

        /// <summary>
        /// Computes the full inventory digest expected immediately after a
        /// complete power snapshot is accepted. The pre-accept inventory is
        /// authenticated separately by <see cref="ComputeInventory(BundleInventory)"/>;
        /// this projection authenticates every retained history record and
        /// the current power/coefficient bindings that the burnup boundary
        /// consumes.
        /// </summary>
        public static ContractValidationResult<Digest32> TryComputeAcceptedInventory(
            BundleInventory inventory,
            StableId powerSnapshotId,
            double snapshotTimeSeconds,
            ulong coreStateVersion,
            ulong spatialStateVersion,
            ulong powerSnapshotVersion,
            IEnumerable<CompletePowerSnapshotBundleV1> snapshotBundles)
        {
            if (inventory == null || snapshotBundles == null || powerSnapshotId.IsEmpty)
            {
                return ContractValidationResult<Digest32>.Invalid(
                    "PowerSnapshot.AcceptedInventoryDigest.Input.Missing",
                    "snapshot",
                    "An accepted inventory digest requires the source inventory, snapshot identity, and entries.");
            }

            CompletePowerSnapshotBundleV1[] entries = snapshotBundles.ToArray();
            if (entries.Any(entry => entry == null))
            {
                return ContractValidationResult<Digest32>.Invalid(
                    "PowerSnapshot.AcceptedInventoryDigest.Entry.Null",
                    "snapshot.bundles",
                    "Accepted inventory digest entries may not be null.");
            }

            var entriesById = new Dictionary<StableId, CompletePowerSnapshotBundleV1>();
            foreach (CompletePowerSnapshotBundleV1 entry in entries)
            {
                if (!entriesById.TryAdd(entry.BundleId, entry))
                {
                    return ContractValidationResult<Digest32>.Invalid(
                        "PowerSnapshot.AcceptedInventoryDigest.Entry.Duplicate",
                        "snapshot.bundles",
                        "Accepted inventory digest entries must have unique bundle identities.");
                }
            }

            var proposed = new List<BundleState>(inventory.OccupiedCount);
            foreach (BundleState bundle in inventory.EnumerateOccupied()
                         .OrderBy(candidate => candidate.ChannelId.Value)
                         .ThenBy(candidate => candidate.Position.Value)
                         .ThenBy(candidate => candidate.BundleId))
            {
                CompletePowerSnapshotBundleV1 entry;
                if (bundle.NuclideState == null ||
                    !entriesById.TryGetValue(bundle.BundleId, out entry) ||
                    entry.ChannelId != bundle.ChannelId ||
                    entry.Position != bundle.Position ||
                    entry.NuclideStateVersion != bundle.NuclideState.NuclideStateVersion)
                {
                    return ContractValidationResult<Digest32>.Invalid(
                        "PowerSnapshot.AcceptedInventoryDigest.BundleBinding.Stale",
                        "snapshot.bundles",
                        "Accepted inventory digest entries must bind every complete bundle identity, location, and nuclide version.");
                }

                ContractValidationResult<PowerHistoryRecordV1> history = PowerHistoryRecordV1.TryCreate(
                    snapshotTimeSeconds,
                    entry.PowerWatts,
                    coreStateVersion,
                    spatialStateVersion,
                    powerSnapshotVersion);
                if (!history.IsValid)
                {
                    return ContractValidationResult<Digest32>.Invalid(
                        history.FirstDiagnostic.Code,
                        history.FirstDiagnostic.Path,
                        history.FirstDiagnostic.Message);
                }

                ContractValidationResult<BundleState> updated = bundle.TryWithAcceptedPowerSnapshot(
                    powerSnapshotId,
                    history.Value,
                    entry.CoefficientBinding);
                if (!updated.IsValid)
                {
                    return ContractValidationResult<Digest32>.Invalid(
                        updated.FirstDiagnostic.Code,
                        updated.FirstDiagnostic.Path,
                        updated.FirstDiagnostic.Message);
                }

                proposed.Add(updated.Value);
            }

            if (entries.Length != proposed.Count)
            {
                return ContractValidationResult<Digest32>.Invalid(
                    "PowerSnapshot.AcceptedInventoryDigest.BundleCount.Mismatch",
                    "snapshot.bundles",
                    "Accepted inventory digest entries must contain exactly one entry per live bundle.");
            }

            ContractValidationResult<BundleInventory> resulting = BundleInventory.TryCreate(
                inventory.Topology,
                proposed);
            if (!resulting.IsValid)
            {
                return ContractValidationResult<Digest32>.Invalid(
                    resulting.FirstDiagnostic.Code,
                    resulting.FirstDiagnostic.Path,
                    resulting.FirstDiagnostic.Message);
            }

            return ContractValidationResult<Digest32>.Valid(ComputeInventory(resulting.Value));
        }

        /// <summary>
        /// Computes the canonical identity and burnup/energy projection used
        /// to authenticate a positive-duration power snapshot. It deliberately
        /// excludes current power and solver bindings so energy edits cannot
        /// hide behind an otherwise unchanged accepted snapshot.
        /// </summary>
        public static Digest32 ComputeBurnupEnergy(BundleInventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-BURNUP-ENERGY-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)inventory.OccupiedCount));
                    foreach (BundleState bundle in inventory.EnumerateOccupied()
                                 .OrderBy(candidate => candidate.BundleId))
                    {
                        Phase5CanonicalBytesV1.WriteStableId(writer, bundle.BundleId);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.Position.Value);
                        Phase5CanonicalBytesV1.WriteString(writer, bundle.MaterialVariantId.Value);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InitialBurnupJPerKgHm);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CumulativeFissionEnergyJ);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CurrentBurnupJPerKgHm);
                        Phase5CanonicalBytesV1.WriteDouble(writer, bundle.HeavyMetalMassKg);
                    }
                }));
        }

        public static Digest32 ComputeInventory(
            string magic,
            BundleInventory inventory)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                magic,
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)inventory.OccupiedCount));
                    foreach (BundleState bundle in inventory.EnumerateOccupied()
                                 .OrderBy(candidate => candidate.BundleId))
                    {
                        Phase5CanonicalBytesV1.WriteBytes(
                            writer,
                            CompleteBundleStateCodecV1.ToCanonicalBytes(bundle));
                    }
                }));
        }

        public static Digest32 Compute(
            string magic,
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                magic,
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, VersionLifecycleV1.CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteDouble(writer, lifecycle.CurrentSimulationTimeSeconds);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.InitialCoreStateVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.CoreStateVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.InitialSpatialStateVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.SpatialStateVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.InitialPowerSnapshotVersion);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, lifecycle.PowerSnapshotVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, lifecycle.TopologyVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, lifecycle.DataPackVersion);
                    writer.Write((byte)lifecycle.SpatialBindingStatus);
                    writer.Write((byte)lifecycle.PowerBindingStatus);
                    CompleteBundleStateCodecV1.WriteOptionalStableId(writer, lifecycle.SpatialSolveId);
                    CompleteBundleStateCodecV1.WriteOptionalStableId(writer, lifecycle.PowerSnapshotId);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.StateDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.CoefficientDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.TopologyDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.DataPackDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.SnapshotDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.PowerSnapshotInventoryDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.PowerSnapshotAcceptedInventoryDigest);
                    CompleteBundleStateCodecV1.WriteOptionalDigest(writer, lifecycle.PowerSnapshotBurnupEnergyDigest);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)lifecycle.BundleNuclideVersions.Count));
                    foreach (BundleNuclideVersionV1 version in lifecycle.BundleNuclideVersions.OrderBy(record => record.BundleId))
                    {
                        Phase5CanonicalBytesV1.WriteStableId(writer, version.BundleId);
                        Phase5CanonicalBytesV1.WriteUInt64(writer, version.InitialNuclideStateVersion);
                        Phase5CanonicalBytesV1.WriteUInt64(writer, version.NuclideStateVersion);
                    }

                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)inventory.OccupiedCount));
                    foreach (BundleState bundle in inventory.EnumerateOccupied()
                                 .OrderBy(candidate => candidate.BundleId))
                    {
                        Phase5CanonicalBytesV1.WriteBytes(
                            writer,
                            CompleteBundleStateCodecV1.ToCanonicalBytes(
                                bundle,
                                lifecycle.CurrentSimulationTimeSeconds));
                    }
                }));
        }
    }
}
