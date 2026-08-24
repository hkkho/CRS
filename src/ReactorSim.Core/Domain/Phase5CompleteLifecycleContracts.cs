using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One bundle-scoped request in an atomic I/Xe integration batch.
    /// </summary>
    public sealed class CompleteNuclideIntegrationRequestV1
    {
        private CompleteNuclideIntegrationRequestV1(
            StableId bundleId,
            NuclideIntegrationInputV1 input)
        {
            BundleId = bundleId;
            Input = input;
        }

        public StableId BundleId { get; }

        public NuclideIntegrationInputV1 Input { get; }

        public static ContractValidationResult<CompleteNuclideIntegrationRequestV1> TryCreate(
            StableId bundleId,
            NuclideIntegrationInputV1 input)
        {
            if (bundleId.IsEmpty || input == null)
            {
                return ContractValidationResult<CompleteNuclideIntegrationRequestV1>.Invalid(
                    "NuclideBatch.Request.Missing",
                    "request",
                    "An I/Xe batch request requires a nonempty bundle identity and validated input.");
            }

            return ContractValidationResult<CompleteNuclideIntegrationRequestV1>.Valid(
                new CompleteNuclideIntegrationRequestV1(bundleId, input));
        }
    }

    public sealed class CompleteNuclideIntegrationBatchResultV1
    {
        internal CompleteNuclideIntegrationBatchResultV1(
            BundleInventory resultingInventory,
            VersionLifecycleV1 resultingLifecycle,
            IEnumerable<NuclideIntegrationResultV1> transitions)
        {
            ResultingInventory = resultingInventory;
            ResultingLifecycle = resultingLifecycle;
            Transitions = new ReadOnlyCollection<NuclideIntegrationResultV1>(transitions.ToArray());
        }

        public BundleInventory ResultingInventory { get; }

        public VersionLifecycleV1 ResultingLifecycle { get; }

        public IReadOnlyList<NuclideIntegrationResultV1> Transitions { get; }
    }

    /// <summary>
    /// Prepared refuelling input with an exact source-state precondition. A
    /// caller must create this value at the same lifecycle boundary as the
    /// shift; applying it later against changed energy, power, or lifecycle
    /// state is rejected before any proposed state is built.
    /// </summary>
    public sealed class CompleteRefuellingRequestV1
    {
        private CompleteRefuellingRequestV1(
            RefuelShiftResult shift,
            ulong sourceCoreStateVersion,
            Digest32 sourceLifecycleStateDigest,
            Digest32 sourceInventoryDigest)
        {
            Shift = shift;
            SourceCoreStateVersion = sourceCoreStateVersion;
            SourceLifecycleStateDigest = sourceLifecycleStateDigest;
            SourceInventoryDigest = sourceInventoryDigest;
        }

        public RefuelShiftResult Shift { get; }

        public ulong SourceCoreStateVersion { get; }

        public Digest32 SourceLifecycleStateDigest { get; }

        public Digest32 SourceInventoryDigest { get; }

        public static ContractValidationResult<CompleteRefuellingRequestV1> TryCreate(
            RefuelShiftResult shift,
            VersionLifecycleV1 lifecycle)
        {
            if (shift == null || lifecycle == null ||
                !lifecycle.HasSameBundleIdentitySet(shift.SourceInventory) ||
                !lifecycle.StateDigest.IsApplicable || lifecycle.StateDigest.Value == null)
            {
                return ContractValidationResult<CompleteRefuellingRequestV1>.Invalid(
                    "CompleteRefuellingRequest.SourceBinding.Invalid",
                    "request",
                    "A prepared refuelling request requires the exact current inventory and applicable lifecycle state digest.");
            }

            return ContractValidationResult<CompleteRefuellingRequestV1>.Valid(
                new CompleteRefuellingRequestV1(
                    shift,
                    lifecycle.CoreStateVersion,
                    lifecycle.StateDigest.Value,
                    CompleteStateDigestV1.ComputeInventory(
                        "CANDU-REFUEL-SOURCE-INVENTORY-V1",
                        shift.SourceInventory)));
        }
    }

    /// <summary>
    /// Applies a complete I/Xe batch atomically. Each selected bundle advances
    /// exactly once; all spatial/power bindings are invalidated together and
    /// no partial inventory is returned on failure.
    /// </summary>
    public static class CompleteNuclideIntegrationBatchTransitionV1
    {
        public static ContractValidationResult<CompleteNuclideIntegrationBatchResultV1> TryApply(
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            IEnumerable<CompleteNuclideIntegrationRequestV1> requests,
            Digest32 nextStateDigest)
        {
            if (inventory == null || lifecycle == null || requests == null || nextStateDigest == null)
            {
                return Invalid(
                    "NuclideBatch.Input.Missing",
                    "batch",
                    "An I/Xe batch requires inventory, lifecycle, requests, and a next-state digest.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(inventory))
            {
                return Invalid(
                    "NuclideBatch.StateBinding.Stale",
                    "inventory",
                    "The batch must bind the exact current lifecycle inventory identity and location set.");
            }

            foreach (BundleState liveBundle in inventory.EnumerateOccupied())
            {
                BundleNuclideVersionV1? currentVersion = lifecycle.BundleNuclideVersions
                    .FirstOrDefault(candidate => candidate.BundleId == liveBundle.BundleId);
                if (liveBundle.NuclideState == null ||
                    liveBundle.NuclideState.BundleId != liveBundle.BundleId ||
                    liveBundle.NuclideState.Data.MaterialVariantId != liveBundle.MaterialVariantId ||
                    currentVersion == null ||
                    currentVersion.NuclideStateVersion != liveBundle.NuclideState.NuclideStateVersion)
                {
                    return Invalid(
                        "NuclideBatch.Inventory.Incomplete",
                        "inventory[" + liveBundle.BundleId + "]",
                        "An atomic I/Xe batch requires a complete envelope and matching lifecycle version for every live bundle.");
                }
            }

            CompleteNuclideIntegrationRequestV1[] orderedRequests = requests
                .OrderBy(request => request == null ? StableId.Empty : request.BundleId)
                .ToArray();
            if (orderedRequests.Length == 0 || orderedRequests.Any(request => request == null))
            {
                return Invalid(
                    "NuclideBatch.Requests.Invalid",
                    "requests",
                    "An I/Xe batch requires at least one non-null request.");
            }

            var seenBundles = new HashSet<StableId>();
            var seenRecords = new HashSet<string>(StringComparer.Ordinal);
            var replacements = new Dictionary<StableId, BundleState>();
            var transitions = new List<NuclideIntegrationResultV1>(orderedRequests.Length);
            foreach (CompleteNuclideIntegrationRequestV1 request in orderedRequests)
            {
                if (!seenBundles.Add(request.BundleId))
                {
                    return Invalid(
                        "NuclideBatch.Request.BundleDuplicate",
                        "requests",
                        "A bundle may occur at most once in one atomic I/Xe batch.");
                }

                BundleState? bundle;
                if (!inventory.TryFind(request.BundleId, out bundle) || bundle == null || bundle.NuclideState == null)
                {
                    return Invalid(
                        "NuclideBatch.Bundle.Missing",
                        "requests[" + request.BundleId + "]",
                        "Every I/Xe request must target a live bundle with a complete nuclide envelope.");
                }

                NuclideIntegrationInputV1 input = request.Input;
                if (input.CoreStateVersion != lifecycle.CoreStateVersion ||
                    input.EventTimeSeconds != lifecycle.CurrentSimulationTimeSeconds ||
                    !lifecycle.StateDigest.IsApplicable ||
                    input.StateBindingDigest == null ||
                    !input.StateBindingDigest.Equals(lifecycle.StateDigest.Value))
                {
                    return Invalid(
                        "NuclideBatch.Input.StateBinding.Stale",
                        "requests[" + request.BundleId + "]",
                        "The I/Xe input must bind the current core version and exact simulation time.");
                }

                string recordKey = input.OwnerEventId.ToString() + ":" +
                                   input.RecordSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!seenRecords.Add(recordKey))
                {
                    return Invalid(
                        "NuclideBatch.RecordIdentity.Duplicate",
                        "requests[" + request.BundleId + "]",
                        "An owner event and record sequence pair may occur at most once in a batch.");
                }

                ContractValidationResult<NuclideIntegrationResultV1> integrated =
                    NuclideIntegrationTransitionV1.TryApply(bundle.NuclideState, input);
                if (!integrated.IsValid)
                {
                    return Invalid(
                        integrated.FirstDiagnostic.Code,
                        integrated.FirstDiagnostic.Path,
                        integrated.FirstDiagnostic.Message);
                }

                BundleState updated = bundle
                    .WithNuclideState(integrated.Value.ResultingState)
                    .WithPowerBindingInvalidated();
                replacements.Add(bundle.BundleId, updated);
                transitions.Add(integrated.Value);
            }

            var resultingBundles = new List<BundleState>(inventory.OccupiedCount);
            foreach (BundleState bundle in inventory.EnumerateOccupied())
            {
                BundleState replacement;
                resultingBundles.Add(replacements.TryGetValue(bundle.BundleId, out replacement)
                    ? replacement
                    : bundle);
            }

            ContractValidationResult<BundleInventory> resultingInventory = BundleInventory.TryCreate(
                inventory.Topology,
                resultingBundles);
            if (!resultingInventory.IsValid)
            {
                return Invalid(
                    resultingInventory.FirstDiagnostic.Code,
                    resultingInventory.FirstDiagnostic.Path,
                    resultingInventory.FirstDiagnostic.Message);
            }

            BundleNuclideVersionV1[] proposedVersions = resultingInventory.Value.EnumerateOccupied()
                .OrderBy(bundle => bundle.BundleId)
                .Select(bundle => new BundleNuclideVersionV1(
                    bundle.BundleId,
                    FindInitialVersion(lifecycle, bundle.BundleId),
                    bundle.NuclideState == null
                        ? FindCurrentVersion(lifecycle, bundle.BundleId)
                        : bundle.NuclideState.NuclideStateVersion))
                .ToArray();

            ContractValidationResult<VersionLifecycleV1> resultingLifecycle = lifecycle.TryAcceptNuclideIntegration(
                lifecycle.BundleNuclideVersions,
                proposedVersions,
                nextStateDigest);
            if (!resultingLifecycle.IsValid)
            {
                return Invalid(
                    resultingLifecycle.FirstDiagnostic.Code,
                    resultingLifecycle.FirstDiagnostic.Path,
                    resultingLifecycle.FirstDiagnostic.Message);
            }

            return ContractValidationResult<CompleteNuclideIntegrationBatchResultV1>.Valid(
                new CompleteNuclideIntegrationBatchResultV1(
                    resultingInventory.Value,
                    resultingLifecycle.Value,
                    transitions));
        }

        private static ulong FindInitialVersion(VersionLifecycleV1 lifecycle, StableId bundleId)
        {
            BundleNuclideVersionV1? record = lifecycle.BundleNuclideVersions
                .FirstOrDefault(candidate => candidate.BundleId == bundleId);
            return record == null ? 0 : record.InitialNuclideStateVersion;
        }

        private static ulong FindCurrentVersion(VersionLifecycleV1 lifecycle, StableId bundleId)
        {
            BundleNuclideVersionV1? record = lifecycle.BundleNuclideVersions
                .FirstOrDefault(candidate => candidate.BundleId == bundleId);
            return record == null ? 0 : record.NuclideStateVersion;
        }

        private static ContractValidationResult<CompleteNuclideIntegrationBatchResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompleteNuclideIntegrationBatchResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete P2-T03 discharge evidence. The persistent bundle state is
    /// retained with its I/Xe envelope and append-only histories after it
    /// leaves the live inventory.
    /// </summary>
    public sealed class CompleteDischargeRecordV1
    {
        private CompleteDischargeRecordV1(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            MaterialVariantId materialVariantId,
            double initialBurnupJPerKgHm,
            double cumulativeFissionEnergyJ,
            double heavyMetalMassKg,
            double insertedAtSeconds,
            double dischargedAtSeconds,
            RefuelDischargeEndV1 dischargeEnd,
            StableId commandId,
            ulong coreStateVersionAtDischarge,
            IEnumerable<PowerHistoryRecordV1> powerHistory,
            BundleCoefficientBindingV1? coefficientBinding,
            NuclideStateEnvelopeV1 nuclideState,
            ulong stateVersion,
            Digest32 recordDigest)
        {
            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
            MaterialVariantId = materialVariantId;
            InitialBurnupJPerKgHm = initialBurnupJPerKgHm;
            CumulativeFissionEnergyJ = cumulativeFissionEnergyJ;
            HeavyMetalMassKg = heavyMetalMassKg;
            InsertedAtSeconds = insertedAtSeconds;
            DischargedAtSeconds = dischargedAtSeconds;
            DischargeEnd = dischargeEnd;
            CommandId = commandId;
            CoreStateVersionAtDischarge = coreStateVersionAtDischarge;
            PowerHistory = new ReadOnlyCollection<PowerHistoryRecordV1>(powerHistory.ToArray());
            CoefficientBinding = coefficientBinding;
            NuclideState = nuclideState;
            StateVersion = stateVersion;
            RecordDigest = recordDigest;
        }

        public const uint CurrentSchemaVersion = 1;

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public double InitialBurnupJPerKgHm { get; }

        public double CumulativeFissionEnergyJ { get; }

        public double HeavyMetalMassKg { get; }

        public double InsertedAtSeconds { get; }

        public double DischargedAtSeconds { get; }

        public RefuelDischargeEndV1 DischargeEnd { get; }

        public StableId CommandId { get; }

        public ulong CoreStateVersionAtDischarge { get; }

        public IReadOnlyList<PowerHistoryRecordV1> PowerHistory { get; }

        public BundleCoefficientBindingV1? CoefficientBinding { get; }

        public NuclideStateEnvelopeV1 NuclideState { get; }

        public ulong StateVersion { get; }

        public Digest32 RecordDigest { get; }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(true);
        }

        internal static ContractValidationResult<CompleteDischargeRecordV1> TryCreate(
            BundleState bundle,
            double dischargedAtSeconds,
            RefuelDischargeEndV1 dischargeEnd,
            StableId commandId,
            ulong coreStateVersionAtDischarge)
        {
            if (bundle == null || bundle.NuclideState == null)
            {
                return Invalid(
                    "CompleteDischarge.Bundle.Incomplete",
                    "bundle",
                    "A complete discharge record requires the full I/Xe-bearing bundle state.");
            }

            if (bundle.BundleId.IsEmpty || commandId.IsEmpty ||
                bundle.NuclideState.BundleId != bundle.BundleId ||
                bundle.NuclideState.Data.MaterialVariantId != bundle.MaterialVariantId)
            {
                return Invalid(
                    "CompleteDischarge.Identity.BindingMismatch",
                    "bundle",
                    "Discharge identity and nuclide/material bindings must match the bundle state.");
            }

            if (!Enum.IsDefined(typeof(RefuelDischargeEndV1), dischargeEnd))
            {
                return Invalid(
                    "CompleteDischarge.End.Invalid",
                    "discharge_end",
                    "The discharge end must be EndA or EndB.");
            }

            if (!ContractValidation.IsFinite(dischargedAtSeconds) || dischargedAtSeconds < 0 ||
                dischargedAtSeconds < bundle.InsertedAtSeconds)
            {
                return Invalid(
                    "CompleteDischarge.Time.Invalid",
                    "discharged_at_s",
                    "Discharge time must be finite, nonnegative, and no earlier than insertion.");
            }

            if (string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value) ||
                !ContractValidation.IsFinite(bundle.InitialBurnupJPerKgHm) || bundle.InitialBurnupJPerKgHm < 0 ||
                !ContractValidation.IsFinite(bundle.CumulativeFissionEnergyJ) || bundle.CumulativeFissionEnergyJ < 0 ||
                !ContractValidation.IsFinite(bundle.HeavyMetalMassKg) || bundle.HeavyMetalMassKg <= 0 ||
                bundle.PowerWatts == null || bundle.PowerSnapshotId == null)
            {
                return Invalid(
                    "CompleteDischarge.BundleState.Invalid",
                    "bundle",
                    "A complete discharge record may contain only finite valid state and explicit applicability wrappers.");
            }

            if (bundle.PowerWatts.IsApplicable != bundle.PowerSnapshotId.IsApplicable ||
                bundle.PowerHistory.Any(record => record == null) ||
                (bundle.PowerWatts.IsApplicable && bundle.CoefficientBinding == null))
            {
                return Invalid(
                    "CompleteDischarge.PowerState.Invalid",
                    "bundle.power_state",
                    "Power applicability, history, and coefficient binding must be coherent.");
            }

            byte[] body = BuildBytes(
                bundle,
                dischargedAtSeconds,
                dischargeEnd,
                commandId,
                coreStateVersionAtDischarge,
                false,
                null);
            Digest32 digest = new Digest32(
                Phase5CanonicalBytesV1.HashBodyBytes("CANDU-DISCHARGE-RECORD-V1", body));

            return ContractValidationResult<CompleteDischargeRecordV1>.Valid(
                new CompleteDischargeRecordV1(
                    bundle.BundleId,
                    bundle.ChannelId,
                    bundle.Position,
                    bundle.MaterialVariantId,
                    bundle.InitialBurnupJPerKgHm,
                    bundle.CumulativeFissionEnergyJ,
                    bundle.HeavyMetalMassKg,
                    bundle.InsertedAtSeconds,
                    dischargedAtSeconds,
                    dischargeEnd,
                    commandId,
                    coreStateVersionAtDischarge,
                    bundle.PowerHistory,
                    bundle.CoefficientBinding,
                    bundle.NuclideState,
                    bundle.StateVersion,
                    digest));
        }

        private byte[] BuildBytes(bool includeDigest)
        {
            BundleState state = new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                CumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                PowerHistory,
                CoefficientBinding,
                StateVersion);
            return BuildBytes(
                state,
                DischargedAtSeconds,
                DischargeEnd,
                CommandId,
                CoreStateVersionAtDischarge,
                includeDigest,
                includeDigest ? RecordDigest : null);
        }

        private static byte[] BuildBytes(
            BundleState bundle,
            double dischargedAtSeconds,
            RefuelDischargeEndV1 dischargeEnd,
            StableId commandId,
            ulong coreStateVersionAtDischarge,
            bool includeDigest,
            Digest32? recordDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-DISCHARGE-RECORD-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, bundle.BundleId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, bundle.Position.Value);
                Phase5CanonicalBytesV1.WriteString(writer, bundle.MaterialVariantId.Value);
                Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InitialBurnupJPerKgHm);
                Phase5CanonicalBytesV1.WriteDouble(writer, bundle.CumulativeFissionEnergyJ);
                Phase5CanonicalBytesV1.WriteDouble(writer, bundle.HeavyMetalMassKg);
                Phase5CanonicalBytesV1.WriteDouble(writer, bundle.InsertedAtSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, dischargedAtSeconds);
                writer.Write((byte)dischargeEnd);
                CompleteBundleStateCodecV1.WriteOptionalCoefficient(writer, bundle.CoefficientBinding);
                Phase5CanonicalBytesV1.WriteUInt64(writer, bundle.StateVersion);
                Phase5CanonicalBytesV1.WriteUInt64(writer, coreStateVersionAtDischarge);
                Phase5CanonicalBytesV1.WriteStableId(writer, commandId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)bundle.PowerHistory.Count));
                foreach (PowerHistoryRecordV1 record in bundle.PowerHistory)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteBytes(writer, bundle.NuclideState!.ToCanonicalBytes());
                if (includeDigest)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, recordDigest!);
                }
            });
        }

        private static ContractValidationResult<CompleteDischargeRecordV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompleteDischargeRecordV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete committed refuelling transaction envelope. Before/proposed
    /// digests cover the full bundle projections and lifecycle binding; the
    /// unchanged digest is explicitly NotApplicable for a committed result.
    /// </summary>
    public sealed class CompleteRefuellingTransactionV1
    {
        private CompleteRefuellingTransactionV1(
            StableId batchId,
            StableId commandId,
            double effectiveTimeSeconds,
            ulong coreStateVersionBefore,
            ulong coreStateVersionAfter,
            CommitStatusV1 commitStatus,
            OptionalTextV1 rollbackReasonOrNA,
            Digest32 beforeDigest,
            Digest32 proposedDigest,
            OptionalDigest32 unchangedDigestOrNA,
            IEnumerable<CompleteDischargeRecordV1> dischargeRecords,
            IEnumerable<BundleState> sourceBundles,
            IEnumerable<BundleState> resultingBundles)
        {
            BatchId = batchId;
            CommandId = commandId;
            EffectiveTimeSeconds = effectiveTimeSeconds;
            CoreStateVersionBefore = coreStateVersionBefore;
            CoreStateVersionAfter = coreStateVersionAfter;
            CommitStatus = commitStatus;
            RollbackReasonOrNA = rollbackReasonOrNA;
            BeforeDigest = beforeDigest;
            ProposedDigest = proposedDigest;
            UnchangedDigestOrNA = unchangedDigestOrNA;
            DischargeRecords = new ReadOnlyCollection<CompleteDischargeRecordV1>(dischargeRecords.ToArray());
            SourceBundles = new ReadOnlyCollection<BundleState>(sourceBundles.ToArray());
            ResultingBundles = new ReadOnlyCollection<BundleState>(resultingBundles.ToArray());
        }

        public const uint CurrentSchemaVersion = 1;

        public StableId BatchId { get; }

        public StableId CommandId { get; }

        public double EffectiveTimeSeconds { get; }

        public ulong CoreStateVersionBefore { get; }

        public ulong CoreStateVersionAfter { get; }

        public CommitStatusV1 CommitStatus { get; }

        public OptionalTextV1 RollbackReasonOrNA { get; }

        public Digest32 BeforeDigest { get; }

        public Digest32 ProposedDigest { get; }

        public OptionalDigest32 UnchangedDigestOrNA { get; }

        public IReadOnlyList<CompleteDischargeRecordV1> DischargeRecords { get; }

        public IReadOnlyList<BundleState> SourceBundles { get; }

        public IReadOnlyList<BundleState> ResultingBundles { get; }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-REFUEL-TRANSACTION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, BatchId);
                Phase5CanonicalBytesV1.WriteStableId(writer, CommandId);
                Phase5CanonicalBytesV1.WriteDouble(writer, EffectiveTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt64(writer, CoreStateVersionBefore);
                Phase5CanonicalBytesV1.WriteUInt64(writer, CoreStateVersionAfter);
                writer.Write((byte)CommitStatus);
                CompleteBundleStateCodecV1.WriteOptionalText(writer, RollbackReasonOrNA);
                Phase5CanonicalBytesV1.WriteDigest(writer, BeforeDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, ProposedDigest);
                CompleteBundleStateCodecV1.WriteOptionalDigest(writer, UnchangedDigestOrNA);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)DischargeRecords.Count));
                foreach (CompleteDischargeRecordV1 record in DischargeRecords)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, record.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)SourceBundles.Count));
                foreach (BundleState bundle in SourceBundles)
                {
                    Phase5CanonicalBytesV1.WriteBytes(
                        writer,
                        CompleteBundleStateCodecV1.ToCanonicalBytes(bundle, EffectiveTimeSeconds));
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)ResultingBundles.Count));
                foreach (BundleState bundle in ResultingBundles)
                {
                    Phase5CanonicalBytesV1.WriteBytes(
                        writer,
                        CompleteBundleStateCodecV1.ToCanonicalBytes(bundle, EffectiveTimeSeconds));
                }
            });
        }

        internal static ContractValidationResult<CompleteRefuellingTransactionV1> TryCreateCommitted(
            StableId batchId,
            StableId commandId,
            double effectiveTimeSeconds,
            ulong coreStateVersionBefore,
            ulong coreStateVersionAfter,
            Digest32 beforeDigest,
            Digest32 proposedDigest,
            IEnumerable<CompleteDischargeRecordV1> dischargeRecords,
            IEnumerable<BundleState> sourceBundles,
            IEnumerable<BundleState> resultingBundles)
        {
            if (batchId.IsEmpty || commandId.IsEmpty || beforeDigest == null || proposedDigest == null ||
                dischargeRecords == null || sourceBundles == null || resultingBundles == null ||
                !ContractValidation.IsFinite(effectiveTimeSeconds) || effectiveTimeSeconds < 0 ||
                coreStateVersionAfter != coreStateVersionBefore + 1)
            {
                return Invalid(
                    "CompleteRefuelTransaction.Input.Invalid",
                    "transaction",
                    "A committed transaction requires complete identities, digests, time, and an exact core-version increment.");
            }

            if (coreStateVersionBefore == ulong.MaxValue ||
                dischargeRecords.Any(record => record == null) ||
                sourceBundles.Any(bundle => bundle == null) ||
                resultingBundles.Any(bundle => bundle == null))
            {
                return Invalid(
                    "CompleteRefuelTransaction.State.Invalid",
                    "transaction",
                    "A committed transaction may not contain null state records or overflow its core version.");
            }

            return ContractValidationResult<CompleteRefuellingTransactionV1>.Valid(
                new CompleteRefuellingTransactionV1(
                    batchId,
                    commandId,
                    effectiveTimeSeconds,
                    coreStateVersionBefore,
                    coreStateVersionAfter,
                    CommitStatusV1.Committed,
                    OptionalTextV1.NotApplicable,
                    beforeDigest,
                    proposedDigest,
                    OptionalDigest32.NotApplicable,
                    dischargeRecords,
                    sourceBundles,
                    resultingBundles));
        }

        internal static ContractValidationResult<CompleteRefuellingTransactionV1> TryCreateUncommitted(
            StableId batchId,
            StableId commandId,
            double effectiveTimeSeconds,
            ulong coreStateVersionBefore,
            CommitStatusV1 commitStatus,
            OptionalTextV1 rollbackReasonOrNA,
            Digest32 beforeDigest,
            Digest32 proposedDigest,
            Digest32 unchangedDigest,
            IEnumerable<CompleteDischargeRecordV1> dischargeRecords,
            IEnumerable<BundleState> sourceBundles,
            IEnumerable<BundleState> resultingBundles)
        {
            if (batchId.IsEmpty || commandId.IsEmpty || beforeDigest == null || proposedDigest == null ||
                unchangedDigest == null || rollbackReasonOrNA == null || dischargeRecords == null ||
                sourceBundles == null || resultingBundles == null ||
                (commitStatus != CommitStatusV1.RolledBack && commitStatus != CommitStatusV1.Rejected) ||
                !rollbackReasonOrNA.IsApplicable || !unchangedDigest.Equals(beforeDigest) ||
                !ContractValidation.IsFinite(effectiveTimeSeconds) || effectiveTimeSeconds < 0 ||
                dischargeRecords.Any(record => record == null) ||
                sourceBundles.Any(bundle => bundle == null) ||
                resultingBundles.Any(bundle => bundle == null))
            {
                return Invalid(
                    "CompleteRefuelTransaction.Uncommitted.Invalid",
                    "transaction",
                    "A rejected or rolled-back transaction requires a reason, deterministic proposed digest, and unchanged pre-state digest.");
            }

            return ContractValidationResult<CompleteRefuellingTransactionV1>.Valid(
                new CompleteRefuellingTransactionV1(
                    batchId,
                    commandId,
                    effectiveTimeSeconds,
                    coreStateVersionBefore,
                    coreStateVersionBefore,
                    commitStatus,
                    rollbackReasonOrNA,
                    beforeDigest,
                    proposedDigest,
                    OptionalDigest32.Applicable(unchangedDigest),
                    dischargeRecords,
                    sourceBundles,
                    resultingBundles));
        }

        private static ContractValidationResult<CompleteRefuellingTransactionV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompleteRefuellingTransactionV1>.Invalid(code, path, message);
        }
    }

    public sealed class CompleteRefuellingTransitionResultV1
    {
        internal CompleteRefuellingTransitionResultV1(
            BundleInventory resultingInventory,
            VersionLifecycleV1 resultingLifecycle,
            IEnumerable<CompleteDischargeRecordV1> dischargeRecords,
            CompleteRefuellingTransactionV1 transaction)
        {
            ResultingInventory = resultingInventory;
            ResultingLifecycle = resultingLifecycle;
            DischargeRecords = new ReadOnlyCollection<CompleteDischargeRecordV1>(dischargeRecords.ToArray());
            Transaction = transaction;
        }

        public BundleInventory ResultingInventory { get; }

        public VersionLifecycleV1 ResultingLifecycle { get; }

        public IReadOnlyList<CompleteDischargeRecordV1> DischargeRecords { get; }

        public CompleteRefuellingTransactionV1 Transaction { get; }
    }

    /// <summary>
    /// Applies a validated location-layer shift to complete bundle state. The
    /// operation rebinds retained I/Xe densities to explicit destination
    /// volumes, retains power history, invalidates stale current-power
    /// bindings, creates complete discharge records, and commits the lifecycle
    /// exactly once.
    /// </summary>
    public static class CompleteRefuellingTransitionV1
    {
        public static ContractValidationResult<CompleteRefuellingTransitionResultV1> TryApply(
            RefuelShiftResult shift,
            VersionLifecycleV1 lifecycle,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            StableId commandId,
            Digest32 nextStateDigest)
        {
            if (shift == null || lifecycle == null)
            {
                return Invalid(
                    "CompleteRefuelling.Input.Missing",
                    "transition",
                    "A complete refuelling transition requires shift and lifecycle state.");
            }

            ContractValidationResult<CompleteRefuellingRequestV1> prepared =
                CompleteRefuellingRequestV1.TryCreate(shift, lifecycle);
            if (!prepared.IsValid)
            {
                return Invalid(
                    prepared.FirstDiagnostic.Code,
                    prepared.FirstDiagnostic.Path,
                    prepared.FirstDiagnostic.Message);
            }

            return TryApply(
                prepared.Value,
                shift.SourceInventory,
                lifecycle,
                nodeVolumes,
                commandId,
                nextStateDigest);
        }

        /// <summary>
        /// Strict complete-state refuelling boundary. In addition to the
        /// location, envelope, discharge, and lifecycle checks of the legacy
        /// overload, this form performs and stores the post-shift coefficient
        /// lookup for every retained and fresh live bundle before commit.
        /// </summary>
        public static ContractValidationResult<CompleteRefuellingTransitionResultV1> TryApplyWithCoefficientTables(
            RefuelShiftResult shift,
            VersionLifecycleV1 lifecycle,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            StableId commandId,
            Digest32 nextStateDigest,
            IEnumerable<BurnupCoefficientTableV1> coefficientTables)
        {
            if (shift == null || lifecycle == null)
            {
                return Invalid(
                    "CompleteRefuelling.Input.Missing",
                    "transition",
                    "A complete refuelling transition requires shift and lifecycle state.");
            }

            ContractValidationResult<CompleteRefuellingRequestV1> prepared =
                CompleteRefuellingRequestV1.TryCreate(shift, lifecycle);
            if (!prepared.IsValid)
            {
                return Invalid(
                    prepared.FirstDiagnostic.Code,
                    prepared.FirstDiagnostic.Path,
                    prepared.FirstDiagnostic.Message);
            }

            return TryApplyWithCoefficientTables(
                prepared.Value,
                shift.SourceInventory,
                lifecycle,
                nodeVolumes,
                commandId,
                nextStateDigest,
                coefficientTables);
        }

        /// <summary>
        /// Applies a prepared shift only when the complete source inventory
        /// and lifecycle still match the exact state captured by the request.
        /// This overload is the required boundary for callers that prepare a
        /// shift before the commit point.
        /// </summary>
        public static ContractValidationResult<CompleteRefuellingTransitionResultV1> TryApply(
            CompleteRefuellingRequestV1 request,
            BundleInventory currentInventory,
            VersionLifecycleV1 lifecycle,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            StableId commandId,
            Digest32 nextStateDigest)
        {
            return TryApply(
                request,
                currentInventory,
                lifecycle,
                nodeVolumes,
                commandId,
                nextStateDigest,
                null);
        }

        /// <summary>
        /// Applies a prepared shift with the strict post-shift coefficient
        /// lookup contract. All lookups are completed before the lifecycle
        /// commit is constructed.
        /// </summary>
        public static ContractValidationResult<CompleteRefuellingTransitionResultV1> TryApplyWithCoefficientTables(
            CompleteRefuellingRequestV1 request,
            BundleInventory currentInventory,
            VersionLifecycleV1 lifecycle,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            StableId commandId,
            Digest32 nextStateDigest,
            IEnumerable<BurnupCoefficientTableV1> coefficientTables)
        {
            return TryApply(
                request,
                currentInventory,
                lifecycle,
                nodeVolumes,
                commandId,
                nextStateDigest,
                coefficientTables);
        }

        private static ContractValidationResult<CompleteRefuellingTransitionResultV1> TryApply(
            CompleteRefuellingRequestV1 request,
            BundleInventory currentInventory,
            VersionLifecycleV1 lifecycle,
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes,
            StableId commandId,
            Digest32 nextStateDigest,
            IEnumerable<BurnupCoefficientTableV1>? coefficientTables)
        {
            if (request == null || currentInventory == null || lifecycle == null ||
                nodeVolumes == null || commandId.IsEmpty || nextStateDigest == null)
            {
                return Invalid(
                    "CompleteRefuelling.Input.Missing",
                    "transition",
                    "A complete refuelling transition requires prepared request, current inventory, lifecycle, node volumes, command, and next digest.");
            }

            if (request.Shift == null ||
                !lifecycle.StateDigest.IsApplicable || lifecycle.StateDigest.Value == null)
            {
                return Invalid(
                    "CompleteRefuelling.SourceBinding.Invalid",
                    "request",
                    "A complete refuelling transition requires an applicable prepared source binding.");
            }

            if (request.SourceCoreStateVersion != lifecycle.CoreStateVersion)
            {
                return Invalid(
                    "CompleteRefuelling.SourceCoreVersion.Stale",
                    "request.source_core_state_version",
                    "A prepared refuelling request may not cross a core-state version boundary.");
            }

            if (!request.SourceLifecycleStateDigest.Equals(lifecycle.StateDigest.Value))
            {
                return Invalid(
                    "CompleteRefuelling.SourceLifecycleDigest.Stale",
                    "request.source_lifecycle_state_digest",
                    "The prepared refuelling request does not bind the current lifecycle state digest.");
            }

            Digest32 currentInventoryDigest = CompleteStateDigestV1.ComputeInventory(
                "CANDU-REFUEL-SOURCE-INVENTORY-V1",
                currentInventory);
            if (!currentInventoryDigest.Equals(request.SourceInventoryDigest))
            {
                return Invalid(
                    "CompleteRefuelling.SourceInventoryDigest.Stale",
                    "request.source_inventory_digest",
                    "The complete source inventory changed after the refuelling request was prepared.");
            }

            Digest32 preparedShiftDigest = CompleteStateDigestV1.ComputeInventory(
                "CANDU-REFUEL-SOURCE-INVENTORY-V1",
                request.Shift.SourceInventory);
            if (!preparedShiftDigest.Equals(request.SourceInventoryDigest))
            {
                return Invalid(
                    "CompleteRefuelling.Request.Shift.Stale",
                    "request.shift.source_inventory",
                    "The prepared shift no longer matches its captured source inventory digest.");
            }

            if (coefficientTables == null)
            {
                return Invalid(
                    "CompleteRefuelling.CoefficientTables.Required",
                    "coefficient_tables",
                    "Every successful complete-state refuelling commit must perform and store post-shift coefficient lookups.");
            }

            RefuelShiftResult shift = request.Shift;
            if (shift.EffectiveTimeSeconds != lifecycle.CurrentSimulationTimeSeconds)
            {
                return Invalid(
                    "CompleteRefuelling.Time.Stale",
                    "effective_time_s",
                    "The refuelling commit must occur at the lifecycle's exact explicit simulation time.");
            }

            if (!lifecycle.HasSameBundleIdentitySet(currentInventory))
            {
                return Invalid(
                    "CompleteRefuelling.SourceBinding.Stale",
                    "source_inventory",
                    "The refuelling request must bind the exact current lifecycle inventory.");
            }

            ContractValidationResult<Dictionary<NodeKey, double>> volumeMapResult = BuildVolumeMap(nodeVolumes);
            if (!volumeMapResult.IsValid)
            {
                return Invalid(
                    volumeMapResult.FirstDiagnostic.Code,
                    volumeMapResult.FirstDiagnostic.Path,
                    volumeMapResult.FirstDiagnostic.Message);
            }

            Dictionary<NodeKey, double> volumeMap = volumeMapResult.Value;
            ContractValidationResult<bool> sourceComplete = ValidateCompleteInventory(
                currentInventory,
                volumeMap,
                lifecycle);
            if (!sourceComplete.IsValid)
            {
                return Invalid(
                    sourceComplete.FirstDiagnostic.Code,
                    sourceComplete.FirstDiagnostic.Path,
                    sourceComplete.FirstDiagnostic.Message);
            }

            ContractValidationResult<BundleState[]> normalized = NormalizeResult(
                shift,
                currentInventory,
                volumeMap,
                coefficientTables,
                lifecycle.DataPackVersion);
            if (!normalized.IsValid)
            {
                return Invalid(
                    normalized.FirstDiagnostic.Code,
                    normalized.FirstDiagnostic.Path,
                    normalized.FirstDiagnostic.Message);
            }

            ContractValidationResult<BundleInventory> resultingInventory = BundleInventory.TryCreate(
                shift.ResultingInventory.Topology,
                normalized.Value);
            if (!resultingInventory.IsValid)
            {
                return Invalid(
                    resultingInventory.FirstDiagnostic.Code,
                    resultingInventory.FirstDiagnostic.Path,
                    resultingInventory.FirstDiagnostic.Message);
            }

            RefuelDischargeEndV1 end = shift.PositionPlan.ShiftDirection == RefuelShiftDirection.TowardEndB
                ? RefuelDischargeEndV1.EndB
                : RefuelDischargeEndV1.EndA;
            var dischargeRecords = new List<CompleteDischargeRecordV1>(shift.DischargedBundles.Count);
            foreach (BundleState discharged in shift.DischargedBundles)
            {
                BundleState? currentDischarged;
                if (discharged == null ||
                    !currentInventory.TryFind(discharged.BundleId, out currentDischarged) ||
                    currentDischarged == null)
                {
                    return Invalid(
                        "CompleteRefuelling.DischargedState.Stale",
                        "discharged_bundles",
                        "Every discharged record must be sourced from the exact current inventory.");
                }

                ContractValidationResult<CompleteDischargeRecordV1> record = CompleteDischargeRecordV1.TryCreate(
                    currentDischarged,
                    shift.EffectiveTimeSeconds,
                    end,
                    commandId,
                    lifecycle.CoreStateVersion);
                if (!record.IsValid)
                {
                    return Invalid(record.FirstDiagnostic.Code, record.FirstDiagnostic.Path, record.FirstDiagnostic.Message);
                }

                dischargeRecords.Add(record.Value);
            }

            ContractValidationResult<VersionLifecycleV1> resultingLifecycle = lifecycle.TryCommitRefuelling(
                resultingInventory.Value,
                lifecycle.BundleNuclideVersions,
                nextStateDigest);
            if (!resultingLifecycle.IsValid)
            {
                return Invalid(
                    resultingLifecycle.FirstDiagnostic.Code,
                    resultingLifecycle.FirstDiagnostic.Path,
                    resultingLifecycle.FirstDiagnostic.Message);
            }

            Digest32 beforeDigest = CompleteStateDigestV1.Compute(
                "CANDU-REFUEL-BEFORE-V1",
                currentInventory,
                lifecycle);
            Digest32 proposedDigest = CompleteStateDigestV1.Compute(
                "CANDU-REFUEL-PROPOSED-V1",
                resultingInventory.Value,
                resultingLifecycle.Value);
            StableId batchId = DeriveBatchId(shift, commandId);
            ContractValidationResult<CompleteRefuellingTransactionV1> transaction =
                CompleteRefuellingTransactionV1.TryCreateCommitted(
                    batchId,
                    commandId,
                    shift.EffectiveTimeSeconds,
                    lifecycle.CoreStateVersion,
                    resultingLifecycle.Value.CoreStateVersion,
                    beforeDigest,
                    proposedDigest,
                    dischargeRecords,
                    currentInventory.EnumerateOccupied(),
                    resultingInventory.Value.EnumerateOccupied());
            if (!transaction.IsValid)
            {
                return Invalid(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Path,
                    transaction.FirstDiagnostic.Message);
            }

            return ContractValidationResult<CompleteRefuellingTransitionResultV1>.Valid(
                new CompleteRefuellingTransitionResultV1(
                    resultingInventory.Value,
                    resultingLifecycle.Value,
                    dischargeRecords,
                    transaction.Value));
        }

        private static StableId DeriveBatchId(RefuelShiftResult shift, StableId commandId)
        {
            return Phase5CanonicalBytesV1.DeriveUuidV8(
                "CANDU-REFUEL-BATCH-ID-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteDouble(writer, shift.EffectiveTimeSeconds);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                    Phase5CanonicalBytesV1.WriteStableId(writer, commandId);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, shift.ChannelId.Value);
                    Phase5CanonicalBytesV1.WriteString(writer, shift.PositionPlan.SchemeId);
                    writer.Write((byte)shift.PositionPlan.ShiftDirection);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)shift.PositionPlan.InsertedPositions.Count));
                    foreach (BundlePosition position in shift.PositionPlan.InsertedPositions)
                    {
                        BundleState? inserted = shift.ResultingInventory.Get(
                            new NodeKey(shift.ChannelId, position));
                        if (inserted != null)
                        {
                            Phase5CanonicalBytesV1.WriteBytes(
                                writer,
                                CompleteBundleStateCodecV1.ToCanonicalBytes(inserted));
                        }
                    }
                });
        }

        private static ContractValidationResult<Dictionary<NodeKey, double>> BuildVolumeMap(
            IEnumerable<SpatialNodeVolumeV1> nodeVolumes)
        {
            SpatialNodeVolumeV1[] values = nodeVolumes.ToArray();
            var map = new Dictionary<NodeKey, double>();
            for (int index = 0; index < values.Length; index++)
            {
                SpatialNodeVolumeV1 value = values[index];
                if (value == null || !map.TryAdd(value.Node, value.VolumeM3))
                {
                    return ContractValidationResult<Dictionary<NodeKey, double>>.Invalid(
                        "CompleteRefuelling.NodeVolumes.Duplicate",
                        "node_volumes[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Node-volume bindings must be non-null and unique by explicit node identity.");
                }
            }

            return ContractValidationResult<Dictionary<NodeKey, double>>.Valid(map);
        }

        private static ContractValidationResult<bool> ValidateCompleteInventory(
            BundleInventory inventory,
            Dictionary<NodeKey, double> volumeMap,
            VersionLifecycleV1 lifecycle)
        {
            foreach (BundleState bundle in inventory.EnumerateOccupied())
            {
                double expectedVolume;
                if (bundle.NuclideState == null || !volumeMap.TryGetValue(bundle.Node, out expectedVolume) ||
                    bundle.NuclideState.BundleId != bundle.BundleId ||
                    bundle.NuclideState.Data.MaterialVariantId != bundle.MaterialVariantId ||
                    bundle.NuclideState.NodeVolumeM3 != expectedVolume)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CompleteRefuelling.SourceState.Incomplete",
                        "source_inventory[" + bundle.BundleId + "]",
                        "Every source bundle must have a complete envelope bound to its explicit node volume.");
                }

                BundleNuclideVersionV1? version = lifecycle.BundleNuclideVersions
                    .FirstOrDefault(candidate => candidate.BundleId == bundle.BundleId);
                if (version == null || version.NuclideStateVersion != bundle.NuclideState.NuclideStateVersion)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CompleteRefuelling.SourceState.VersionMismatch",
                        "source_inventory[" + bundle.BundleId + "]",
                        "The source envelope version must equal the lifecycle's current bundle version.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<BundleState[]> NormalizeResult(
            RefuelShiftResult shift,
            BundleInventory sourceInventory,
            Dictionary<NodeKey, double> volumeMap,
            IEnumerable<BurnupCoefficientTableV1>? coefficientTables,
            string expectedDataPackVersion)
        {
            Dictionary<MaterialVariantId, BurnupCoefficientTableV1>? tableMap = null;
            if (coefficientTables != null)
            {
                tableMap = new Dictionary<MaterialVariantId, BurnupCoefficientTableV1>();
                int tableIndex = 0;
                foreach (BurnupCoefficientTableV1 table in coefficientTables)
                {
                    string tablePath = "coefficient_tables[" + tableIndex + "]";
                    if (table == null)
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.CoefficientTable.Null",
                            tablePath,
                            "A post-shift coefficient table may not be null.");
                    }

                    if (!string.Equals(table.DataVersion, expectedDataPackVersion, StringComparison.Ordinal))
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.CoefficientTable.DataVersion.Stale",
                            tablePath + ".data_version",
                            "Every post-shift coefficient table must bind the lifecycle data-pack version.");
                    }

                    if (!tableMap.TryAdd(table.MaterialVariantId, table))
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.CoefficientTable.Duplicate",
                            tablePath + ".material_variant_id",
                            "At most one post-shift coefficient table may be supplied per material variant.");
                    }

                    tableIndex++;
                }

                if (tableMap.Count == 0)
                {
                    return ContractValidationResult<BundleState[]>.Invalid(
                        "CompleteRefuelling.CoefficientTable.Empty",
                        "coefficient_tables",
                        "The strict post-shift boundary requires at least one coefficient table.");
                }
            }

            var insertedPositions = new HashSet<BundlePosition>(shift.PositionPlan.InsertedPositions);
            var dischargedIds = new HashSet<StableId>(shift.DischargedBundles.Select(bundle => bundle.BundleId));
            var sourceById = sourceInventory.EnumerateOccupied()
                .ToDictionary(bundle => bundle.BundleId, bundle => bundle);
            var result = new List<BundleState>(shift.ResultingInventory.OccupiedCount);
            foreach (BundleState bundle in shift.ResultingInventory.EnumerateOccupied())
            {
                double volume;
                if (!volumeMap.TryGetValue(bundle.Node, out volume))
                {
                    return ContractValidationResult<BundleState[]>.Invalid(
                        "CompleteRefuelling.Result.NodeVolume.Missing",
                        "result_inventory[" + bundle.BundleId + "]",
                        "Every resulting live node requires an explicit volume binding.");
                }

                if (bundle.NuclideState == null)
                {
                    return ContractValidationResult<BundleState[]>.Invalid(
                        "CompleteRefuelling.Result.NuclideState.Missing",
                        "result_inventory[" + bundle.BundleId + "]",
                        "Every resulting live bundle requires a complete nuclide envelope.");
                }

                bool isInserted = insertedPositions.Contains(bundle.Position) &&
                                  !sourceById.ContainsKey(bundle.BundleId);
                if (isInserted)
                {
                    if (bundle.InsertedAtSeconds != shift.EffectiveTimeSeconds ||
                        bundle.CumulativeFissionEnergyJ != 0 ||
                        bundle.NuclideState.I135XeHistory.Count != 0 ||
                        bundle.NuclideState.I135AtomInventory != bundle.NuclideState.InitialI135 ||
                        bundle.NuclideState.Xe135AtomInventory != bundle.NuclideState.InitialXe135 ||
                        bundle.PowerWatts.IsApplicable || bundle.PowerSnapshotId.IsApplicable ||
                        bundle.PowerHistory.Count != 0 || bundle.CoefficientBinding != null)
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.InsertedTemplate.Invalid",
                            "result_inventory[" + bundle.BundleId + "]",
                            "A fresh bundle must carry explicit initial I/Xe state and no stale power binding or history.");
                    }
                }
                else
                {
                    BundleState source;
                    if (!sourceById.TryGetValue(bundle.BundleId, out source) ||
                        dischargedIds.Contains(bundle.BundleId) ||
                        source.NuclideState == null ||
                        bundle.NuclideState.I135AtomInventory != source.NuclideState.I135AtomInventory ||
                        bundle.NuclideState.Xe135AtomInventory != source.NuclideState.Xe135AtomInventory ||
                        bundle.NuclideState.InitialI135 != source.NuclideState.InitialI135 ||
                        bundle.NuclideState.InitialXe135 != source.NuclideState.InitialXe135 ||
                        bundle.NuclideState.NuclideStateVersion != source.NuclideState.NuclideStateVersion ||
                        !bundle.NuclideState.NuclideHistoryDigest.Equals(source.NuclideState.NuclideHistoryDigest) ||
                        bundle.NuclideState.NuclideDataId != source.NuclideState.NuclideDataId ||
                        !bundle.NuclideState.NuclideDataDigest.Equals(source.NuclideState.NuclideDataDigest))
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.RetainedState.Changed",
                            "result_inventory[" + bundle.BundleId + "]",
                            "A retained bundle may move location but may not change I/Xe state during refuelling.");
                    }
                }

                ContractValidationResult<BundleState> rebound = bundle.TryWithLocationAndNodeVolume(
                    bundle.ChannelId,
                    bundle.Position,
                    volume);
                if (!rebound.IsValid)
                {
                    return ContractValidationResult<BundleState[]>.Invalid(
                        rebound.FirstDiagnostic.Code,
                        rebound.FirstDiagnostic.Path,
                        rebound.FirstDiagnostic.Message);
                }

                BundleState normalizedBundle = rebound.Value.WithPowerBindingInvalidated();
                if (tableMap != null)
                {
                    BurnupCoefficientTableV1 table;
                    if (!tableMap.TryGetValue(normalizedBundle.MaterialVariantId, out table!))
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            "CompleteRefuelling.CoefficientTable.Missing",
                            "result_inventory[" + normalizedBundle.BundleId + "]",
                            "A post-shift coefficient table is required for every live material variant.");
                    }

                    ContractValidationResult<BurnupCoefficientLookupResultV1> lookup = table.TryLookup(
                        normalizedBundle.CurrentBurnupJPerKgHm);
                    if (!lookup.IsValid)
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            lookup.FirstDiagnostic.Code,
                            "result_inventory[" + normalizedBundle.BundleId + "]." + lookup.FirstDiagnostic.Path,
                            lookup.FirstDiagnostic.Message);
                    }

                    ContractValidationResult<BundleCoefficientBindingV1> binding =
                        BundleCoefficientBindingV1.TryCreate(
                            lookup.Value.TableId,
                            lookup.Value.BracketLowerIndex,
                            lookup.Value.BracketUpperIndex,
                            lookup.Value.InterpolationFraction,
                            lookup.Value.Checksum);
                    if (!binding.IsValid)
                    {
                        return ContractValidationResult<BundleState[]>.Invalid(
                            binding.FirstDiagnostic.Code,
                            "result_inventory[" + normalizedBundle.BundleId + "].coefficient_binding",
                            binding.FirstDiagnostic.Message);
                    }

                    normalizedBundle = normalizedBundle.WithCoefficientBinding(binding.Value);
                }

                result.Add(normalizedBundle);
            }

            return ContractValidationResult<BundleState[]>.Valid(result.ToArray());
        }

        private static ContractValidationResult<CompleteRefuellingTransitionResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<CompleteRefuellingTransitionResultV1>.Invalid(code, path, message);
        }
    }

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
