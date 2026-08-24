using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Closed movement status used by the approved refuelling mapping body.
    /// </summary>
    public enum MovementStatusV1 : byte
    {
        Present = 0,
        Inserted = 1,
        Moved = 2,
        Discharged = 3,
        NotApplicable = 255
    }

    /// <summary>
    /// Explicit applicability wrapper for a channel identity in a position
    /// binding. Null is not used as a schema-level NotApplicable sentinel.
    /// </summary>
    public sealed class OptionalChannelIdV1 : IEquatable<OptionalChannelIdV1>
    {
        private OptionalChannelIdV1(bool isApplicable, ChannelId value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalChannelIdV1 NotApplicable
        {
            get { return new OptionalChannelIdV1(false, new ChannelId(0)); }
        }

        public bool IsApplicable { get; }

        public ChannelId Value { get; }

        public static OptionalChannelIdV1 Applicable(ChannelId value)
        {
            return new OptionalChannelIdV1(true, value);
        }

        public bool Equals(OptionalChannelIdV1? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalChannelIdV1);
        }

        public override int GetHashCode()
        {
            return IsApplicable ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Explicit applicability wrapper for a physical bundle position in a
    /// position binding.
    /// </summary>
    public sealed class OptionalBundlePositionV1 : IEquatable<OptionalBundlePositionV1>
    {
        private OptionalBundlePositionV1(bool isApplicable, BundlePosition value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalBundlePositionV1 NotApplicable
        {
            get { return new OptionalBundlePositionV1(false, new BundlePosition(0)); }
        }

        public bool IsApplicable { get; }

        public BundlePosition Value { get; }

        public static OptionalBundlePositionV1 Applicable(BundlePosition value)
        {
            return new OptionalBundlePositionV1(true, value);
        }

        public bool Equals(OptionalBundlePositionV1? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalBundlePositionV1);
        }

        public override int GetHashCode()
        {
            return IsApplicable ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Explicit applicability wrapper for the optional UTF-8 rollback reason
    /// in the approved atomicity event body.
    /// </summary>
    public sealed class OptionalTextV1 : IEquatable<OptionalTextV1>
    {
        private OptionalTextV1(bool isApplicable, string? value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public static OptionalTextV1 NotApplicable
        {
            get { return new OptionalTextV1(false, null); }
        }

        public bool IsApplicable { get; }

        public string? Value { get; }

        public static OptionalTextV1 Applicable(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An applicable text value may not be empty.", nameof(value));
            }

            return new OptionalTextV1(true, value);
        }

        public bool Equals(OptionalTextV1? other)
        {
            return other != null && IsApplicable == other.IsApplicable &&
                   (!IsApplicable || string.Equals(Value, other.Value, StringComparison.Ordinal));
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OptionalTextV1);
        }

        public override int GetHashCode()
        {
            return IsApplicable && Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }
    }

    /// <summary>
    /// One deterministic old/new location binding for a refuelling mapping.
    /// </summary>
    public sealed class PositionBindingV1
    {
        private PositionBindingV1(
            StableId bundleId,
            OptionalChannelIdV1 oldChannelIdOrNA,
            OptionalBundlePositionV1 oldBundlePositionOrNA,
            OptionalChannelIdV1 newChannelIdOrNA,
            OptionalBundlePositionV1 newBundlePositionOrNA,
            MovementStatusV1 movementStatus)
        {
            BundleId = bundleId;
            OldChannelIdOrNA = oldChannelIdOrNA;
            OldBundlePositionOrNA = oldBundlePositionOrNA;
            NewChannelIdOrNA = newChannelIdOrNA;
            NewBundlePositionOrNA = newBundlePositionOrNA;
            MovementStatus = movementStatus;
        }

        public StableId BundleId { get; }

        public OptionalChannelIdV1 OldChannelIdOrNA { get; }

        public OptionalBundlePositionV1 OldBundlePositionOrNA { get; }

        public OptionalChannelIdV1 NewChannelIdOrNA { get; }

        public OptionalBundlePositionV1 NewBundlePositionOrNA { get; }

        public MovementStatusV1 MovementStatus { get; }

        public static ContractValidationResult<PositionBindingV1> TryCreate(
            StableId bundleId,
            OptionalChannelIdV1 oldChannelIdOrNA,
            OptionalBundlePositionV1 oldBundlePositionOrNA,
            OptionalChannelIdV1 newChannelIdOrNA,
            OptionalBundlePositionV1 newBundlePositionOrNA,
            MovementStatusV1 movementStatus)
        {
            if (bundleId.IsEmpty)
            {
                return Invalid(
                    "PositionBinding.BundleId.Empty",
                    "bundle_id",
                    "A position binding requires a stable bundle identity.");
            }

            if (oldChannelIdOrNA == null || oldBundlePositionOrNA == null ||
                newChannelIdOrNA == null || newBundlePositionOrNA == null)
            {
                return Invalid(
                    "PositionBinding.Applicability.Missing",
                    "position_binding",
                    "Every position field requires an explicit applicability wrapper.");
            }

            if (!Enum.IsDefined(typeof(MovementStatusV1), movementStatus))
            {
                return Invalid(
                    "PositionBinding.MovementStatus.Invalid",
                    "movement_status",
                    "The movement status is not part of the approved closed enum.");
            }

            bool oldApplicable = oldChannelIdOrNA.IsApplicable && oldBundlePositionOrNA.IsApplicable;
            bool newApplicable = newChannelIdOrNA.IsApplicable && newBundlePositionOrNA.IsApplicable;
            bool oldComplete = !oldChannelIdOrNA.IsApplicable && !oldBundlePositionOrNA.IsApplicable;
            bool newComplete = !newChannelIdOrNA.IsApplicable && !newBundlePositionOrNA.IsApplicable;
            if (!oldApplicable && !oldComplete || !newApplicable && !newComplete)
            {
                return Invalid(
                    "PositionBinding.Applicability.Inconsistent",
                    "position_binding",
                    "Channel and position applicability must be paired.");
            }

            bool expectedOld = movementStatus == MovementStatusV1.Moved ||
                               movementStatus == MovementStatusV1.Discharged;
            bool expectedNew = movementStatus == MovementStatusV1.Moved ||
                               movementStatus == MovementStatusV1.Inserted;
            if (oldApplicable != expectedOld || newApplicable != expectedNew)
            {
                return Invalid(
                    "PositionBinding.MovementStatus.MappingMismatch",
                    "movement_status",
                    "Movement status and old/new position applicability disagree.");
            }

            return ContractValidationResult<PositionBindingV1>.Valid(
                new PositionBindingV1(
                    bundleId,
                    oldChannelIdOrNA,
                    oldBundlePositionOrNA,
                    newChannelIdOrNA,
                    newBundlePositionOrNA,
                    movementStatus));
        }

        private static ContractValidationResult<PositionBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PositionBindingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// The approved P2-T05 mapping body for one committed refuelling command.
    /// Arrays are immutable and already supplied in canonical physical order.
    /// </summary>
    public sealed class RefuelMappingBodyV1
    {
        private RefuelMappingBodyV1(
            string schemeId,
            double effectiveTimeSeconds,
            ushort shiftCount,
            IEnumerable<StableId> movedBundleIds,
            IEnumerable<StableId> insertedBundleIds,
            IEnumerable<StableId> dischargedBundleIds,
            IEnumerable<PositionBindingV1> positionBindings)
        {
            SchemeId = schemeId;
            EffectiveTimeSeconds = effectiveTimeSeconds;
            ShiftCount = shiftCount;
            MovedBundleIds = new ReadOnlyCollection<StableId>(movedBundleIds.ToArray());
            InsertedBundleIds = new ReadOnlyCollection<StableId>(insertedBundleIds.ToArray());
            DischargedBundleIds = new ReadOnlyCollection<StableId>(dischargedBundleIds.ToArray());
            PositionBindings = new ReadOnlyCollection<PositionBindingV1>(positionBindings.ToArray());
        }

        public string SchemeId { get; }

        public double EffectiveTimeSeconds { get; }

        public ushort ShiftCount { get; }

        public IReadOnlyList<StableId> MovedBundleIds { get; }

        public IReadOnlyList<StableId> InsertedBundleIds { get; }

        public IReadOnlyList<StableId> DischargedBundleIds { get; }

        public IReadOnlyList<PositionBindingV1> PositionBindings { get; }

        public static ContractValidationResult<RefuelMappingBodyV1> TryCreate(
            string? schemeId,
            double effectiveTimeSeconds,
            ushort shiftCount,
            IEnumerable<StableId>? movedBundleIds,
            IEnumerable<StableId>? insertedBundleIds,
            IEnumerable<StableId>? dischargedBundleIds,
            IEnumerable<PositionBindingV1>? positionBindings)
        {
            if (string.IsNullOrWhiteSpace(schemeId))
            {
                return Invalid(
                    "RefuelMapping.SchemeId.Missing",
                    "scheme_id",
                    "A mapping body requires the resolved scheme identity.");
            }

            if (!ContractValidation.IsFinite(effectiveTimeSeconds) || effectiveTimeSeconds < 0)
            {
                return Invalid(
                    "RefuelMapping.EffectiveTime.Invalid",
                    "effective_time_s",
                    "Effective time must be finite and nonnegative SI seconds.");
            }

            if (shiftCount != 4 && shiftCount != 8)
            {
                return Invalid(
                    "RefuelMapping.ShiftCount.Unsupported",
                    "shift_count",
                    "A v1 mapping body requires a four- or eight-bundle shift.");
            }

            if (movedBundleIds == null || insertedBundleIds == null ||
                dischargedBundleIds == null || positionBindings == null)
            {
                return Invalid(
                    "RefuelMapping.Array.Missing",
                    "mapping",
                    "All mapping arrays are required.");
            }

            StableId[] moved = movedBundleIds.ToArray();
            StableId[] inserted = insertedBundleIds.ToArray();
            StableId[] discharged = dischargedBundleIds.ToArray();
            PositionBindingV1[] bindings = positionBindings.ToArray();
            if (inserted.Length != shiftCount || discharged.Length != shiftCount)
            {
                return Invalid(
                    "RefuelMapping.Array.CountMismatch",
                    "mapping",
                    "Inserted and discharged identity counts must equal ShiftCount.");
            }

            if (bindings.Length != moved.Length + inserted.Length + discharged.Length)
            {
                return Invalid(
                    "RefuelMapping.PositionBinding.CountMismatch",
                    "position_bindings",
                    "There must be exactly one position binding for every mapped identity.");
            }

            var identities = new HashSet<StableId>();
            for (int i = 0; i < moved.Length; i++)
            {
                ContractValidationResult<bool> identity = ValidateIdentity(
                    moved[i], "moved_bundle_ids[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", identities);
                if (!identity.IsValid)
                {
                    return Invalid(identity.FirstDiagnostic.Code, identity.FirstDiagnostic.Path, identity.FirstDiagnostic.Message);
                }

                ContractValidationResult<bool> binding = ValidateBinding(
                    bindings[i], moved[i], MovementStatusV1.Moved, "position_bindings[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                if (!binding.IsValid)
                {
                    return Invalid(binding.FirstDiagnostic.Code, binding.FirstDiagnostic.Path, binding.FirstDiagnostic.Message);
                }
            }

            int bindingIndex = moved.Length;
            for (int i = 0; i < inserted.Length; i++, bindingIndex++)
            {
                ContractValidationResult<bool> identity = ValidateIdentity(
                    inserted[i], "inserted_bundle_ids[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", identities);
                if (!identity.IsValid)
                {
                    return Invalid(identity.FirstDiagnostic.Code, identity.FirstDiagnostic.Path, identity.FirstDiagnostic.Message);
                }

                ContractValidationResult<bool> binding = ValidateBinding(
                    bindings[bindingIndex], inserted[i], MovementStatusV1.Inserted, "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                if (!binding.IsValid)
                {
                    return Invalid(binding.FirstDiagnostic.Code, binding.FirstDiagnostic.Path, binding.FirstDiagnostic.Message);
                }
            }

            for (int i = 0; i < discharged.Length; i++, bindingIndex++)
            {
                ContractValidationResult<bool> identity = ValidateIdentity(
                    discharged[i], "discharged_bundle_ids[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", identities);
                if (!identity.IsValid)
                {
                    return Invalid(identity.FirstDiagnostic.Code, identity.FirstDiagnostic.Path, identity.FirstDiagnostic.Message);
                }

                ContractValidationResult<bool> binding = ValidateBinding(
                    bindings[bindingIndex], discharged[i], MovementStatusV1.Discharged, "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                if (!binding.IsValid)
                {
                    return Invalid(binding.FirstDiagnostic.Code, binding.FirstDiagnostic.Path, binding.FirstDiagnostic.Message);
                }
            }

            ContractValidationResult<bool> canonicalPositions = ValidateCanonicalPositions(
                moved,
                inserted,
                discharged,
                bindings);
            if (!canonicalPositions.IsValid)
            {
                return Invalid(
                    canonicalPositions.FirstDiagnostic.Code,
                    canonicalPositions.FirstDiagnostic.Path,
                    canonicalPositions.FirstDiagnostic.Message);
            }

            return ContractValidationResult<RefuelMappingBodyV1>.Valid(
                new RefuelMappingBodyV1(
                    schemeId,
                    effectiveTimeSeconds,
                    shiftCount,
                    moved,
                    inserted,
                    discharged,
                    bindings));
        }

        private static ContractValidationResult<bool> ValidateCanonicalPositions(
            StableId[] moved,
            StableId[] inserted,
            StableId[] discharged,
            PositionBindingV1[] bindings)
        {
            var oldPositions = new HashSet<uint>();
            var newPositions = new HashSet<uint>();
            ChannelId? oldChannel = null;
            ChannelId? newChannel = null;
            int bindingIndex = 0;

            for (int i = 0; i < moved.Length; i++, bindingIndex++)
            {
                PositionBindingV1 binding = bindings[bindingIndex];
                ContractValidationResult<bool> oldPosition = AddPosition(
                    binding.OldChannelIdOrNA,
                    binding.OldBundlePositionOrNA,
                    oldPositions,
                    ref oldChannel,
                    "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].old");
                if (!oldPosition.IsValid)
                {
                    return oldPosition;
                }

                ContractValidationResult<bool> newPosition = AddPosition(
                    binding.NewChannelIdOrNA,
                    binding.NewBundlePositionOrNA,
                    newPositions,
                    ref newChannel,
                    "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].new");
                if (!newPosition.IsValid)
                {
                    return newPosition;
                }

                if (i > 0 &&
                    (bindings[bindingIndex - 1].OldBundlePositionOrNA.Value.Value >= binding.OldBundlePositionOrNA.Value.Value ||
                     bindings[bindingIndex - 1].NewBundlePositionOrNA.Value.Value >= binding.NewBundlePositionOrNA.Value.Value))
                {
                    return InvalidPositions(
                        "RefuelMapping.PositionBinding.NotAscending",
                        "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Moved old and new positions must be strictly ascending.");
                }
            }

            for (int i = 0; i < inserted.Length; i++, bindingIndex++)
            {
                PositionBindingV1 binding = bindings[bindingIndex];
                ContractValidationResult<bool> newPosition = AddPosition(
                    binding.NewChannelIdOrNA,
                    binding.NewBundlePositionOrNA,
                    newPositions,
                    ref newChannel,
                    "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].new");
                if (!newPosition.IsValid)
                {
                    return newPosition;
                }

                if (i > 0 &&
                    bindings[bindingIndex - 1].NewBundlePositionOrNA.Value.Value >= binding.NewBundlePositionOrNA.Value.Value)
                {
                    return InvalidPositions(
                        "RefuelMapping.PositionBinding.InsertedNotAscending",
                        "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Inserted destination positions must be strictly ascending.");
                }
            }

            for (int i = 0; i < discharged.Length; i++, bindingIndex++)
            {
                PositionBindingV1 binding = bindings[bindingIndex];
                ContractValidationResult<bool> oldPosition = AddPosition(
                    binding.OldChannelIdOrNA,
                    binding.OldBundlePositionOrNA,
                    oldPositions,
                    ref oldChannel,
                    "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].old");
                if (!oldPosition.IsValid)
                {
                    return oldPosition;
                }

                if (i > 0 &&
                    bindings[bindingIndex - 1].OldBundlePositionOrNA.Value.Value >= binding.OldBundlePositionOrNA.Value.Value)
                {
                    return InvalidPositions(
                        "RefuelMapping.PositionBinding.DischargedNotAscending",
                        "position_bindings[" + bindingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Discharged old positions must be strictly ascending.");
                }
            }

            if (!oldChannel.HasValue || !newChannel.HasValue || oldChannel.Value != newChannel.Value)
            {
                return InvalidPositions(
                    "RefuelMapping.PositionBinding.ChannelMismatch",
                    "position_bindings",
                    "Old and new position domains must use the same refuelling channel.");
            }

            uint oldMaximum = oldPositions.Count == 0 ? 0 : oldPositions.Max();
            uint newMaximum = newPositions.Count == 0 ? 0 : newPositions.Max();
            ulong oldPositionCount = oldPositions.Count == 0 ? 0UL : (ulong)oldMaximum + 1UL;
            ulong newPositionCount = newPositions.Count == 0 ? 0UL : (ulong)newMaximum + 1UL;
            ulong expectedPositionCount = (ulong)moved.Length + (ulong)inserted.Length;
            if (oldPositionCount == 0 || oldPositionCount != newPositionCount ||
                oldPositionCount != expectedPositionCount ||
                oldMaximum > int.MaxValue || newMaximum > int.MaxValue)
            {
                return InvalidPositions(
                    "RefuelMapping.PositionBinding.PositionSet.Invalid",
                    "position_bindings",
                    "Old and new bindings must cover one complete representable contiguous channel position set.");
            }

            for (uint position = 0; position < oldPositionCount; position++)
            {
                if (!oldPositions.Contains(position) || !newPositions.Contains(position))
                {
                    return InvalidPositions(
                        "RefuelMapping.PositionBinding.PositionSet.Gap",
                        "position_bindings",
                        "Position bindings may not contain gaps or duplicate physical locations.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> AddPosition(
            OptionalChannelIdV1 channel,
            OptionalBundlePositionV1 position,
            HashSet<uint> positions,
            ref ChannelId? expectedChannel,
            string path)
        {
            if (!channel.IsApplicable || !position.IsApplicable)
            {
                return InvalidPositions(
                    "RefuelMapping.PositionBinding.ApplicabilityInvalid",
                    path,
                    "A canonical position set may contain only applicable old or new locations.");
            }

            if (expectedChannel.HasValue && expectedChannel.Value != channel.Value)
            {
                return InvalidPositions(
                    "RefuelMapping.PositionBinding.ChannelMismatch",
                    path + ".channel_id",
                    "One refuelling mapping body must use one channel identity.");
            }

            expectedChannel = channel.Value;
            if (!positions.Add(position.Value.Value))
            {
                return InvalidPositions(
                    "RefuelMapping.PositionBinding.PositionDuplicate",
                    path + ".position",
                    "A physical position may occur only once in each old/new mapping domain.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> InvalidPositions(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<bool>.Invalid(code, path, message);
        }

        private static ContractValidationResult<bool> ValidateIdentity(
            StableId identity,
            string path,
            HashSet<StableId> identities)
        {
            if (identity.IsEmpty)
            {
                return ContractValidationResult<bool>.Invalid(
                    "RefuelMapping.BundleId.Empty",
                    path,
                    "Mapping identities may not be empty.");
            }

            if (!identities.Add(identity))
            {
                return ContractValidationResult<bool>.Invalid(
                    "RefuelMapping.BundleId.Duplicate",
                    path,
                    "A bundle identity may occur only once in one mapping body.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidateBinding(
            PositionBindingV1? binding,
            StableId expectedIdentity,
            MovementStatusV1 expectedStatus,
            string path)
        {
            if (binding == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "RefuelMapping.PositionBinding.Null",
                    path,
                    "A position binding may not be null.");
            }

            if (binding.BundleId != expectedIdentity || binding.MovementStatus != expectedStatus)
            {
                return ContractValidationResult<bool>.Invalid(
                    "RefuelMapping.PositionBinding.OrderMismatch",
                    path,
                    "Position bindings must match their canonical identity and movement arrays.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<RefuelMappingBodyV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RefuelMappingBodyV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// The approved atomicity body. The digest values are supplied by the
    /// complete state/digest owner; this bounded task never invents a digest.
    /// </summary>
    public sealed class RefuelAtomicityBodyV1
    {
        private RefuelAtomicityBodyV1(
            StableId commandId,
            Digest32 beforeDigest,
            Digest32 proposedDigest,
            CommitStatusV1 commitStatus,
            OptionalTextV1 rollbackReasonOrNA,
            OptionalDigest32 unchangedDigestOrNA)
        {
            CommandId = commandId;
            BeforeDigest = beforeDigest;
            ProposedDigest = proposedDigest;
            CommitStatus = commitStatus;
            RollbackReasonOrNA = rollbackReasonOrNA;
            UnchangedDigestOrNA = unchangedDigestOrNA;
        }

        public StableId CommandId { get; }

        public Digest32 BeforeDigest { get; }

        public Digest32 ProposedDigest { get; }

        public CommitStatusV1 CommitStatus { get; }

        public OptionalTextV1 RollbackReasonOrNA { get; }

        public OptionalDigest32 UnchangedDigestOrNA { get; }

        public static ContractValidationResult<RefuelAtomicityBodyV1> TryCreate(
            StableId commandId,
            Digest32? beforeDigest,
            Digest32? proposedDigest,
            CommitStatusV1 commitStatus,
            OptionalTextV1? rollbackReasonOrNA,
            OptionalDigest32? unchangedDigestOrNA)
        {
            if (commandId.IsEmpty)
            {
                return Invalid(
                    "RefuelAtomicity.CommandId.Empty",
                    "command_id",
                    "The atomicity body requires the explicit command identity.");
            }

            if (beforeDigest == null || proposedDigest == null)
            {
                return Invalid(
                    "RefuelAtomicity.Digest.Missing",
                    "digest",
                    "Before and proposed digests are required even for a rejected projection.");
            }

            if (!Enum.IsDefined(typeof(CommitStatusV1), commitStatus))
            {
                return Invalid(
                    "RefuelAtomicity.CommitStatus.Invalid",
                    "commit_status",
                    "The commit status is not part of the approved closed enum.");
            }

            if (rollbackReasonOrNA == null || unchangedDigestOrNA == null)
            {
                return Invalid(
                    "RefuelAtomicity.Applicability.Missing",
                    "atomicity",
                    "Rollback reason and unchanged digest require explicit applicability wrappers.");
            }

            bool committed = commitStatus == CommitStatusV1.Committed;
            if (committed && (rollbackReasonOrNA.IsApplicable || unchangedDigestOrNA.IsApplicable))
            {
                return Invalid(
                    "RefuelAtomicity.Committed.ApplicabilityInvalid",
                    "atomicity",
                    "A committed body must use NotApplicable rollback and unchanged fields.");
            }

            if (!committed && (!rollbackReasonOrNA.IsApplicable || !unchangedDigestOrNA.IsApplicable))
            {
                return Invalid(
                    "RefuelAtomicity.Rejected.ApplicabilityInvalid",
                    "atomicity",
                    "A rejected or rolled-back body must carry a reason and unchanged digest.");
            }

            return ContractValidationResult<RefuelAtomicityBodyV1>.Valid(
                new RefuelAtomicityBodyV1(
                    commandId,
                    beforeDigest,
                    proposedDigest,
                    commitStatus,
                    rollbackReasonOrNA,
                    unchangedDigestOrNA));
        }

        private static ContractValidationResult<RefuelAtomicityBodyV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RefuelAtomicityBodyV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Explicit discharge-end identity for the bounded basic discharge audit
    /// record. EndA is ordinal zero and EndB is ordinal one.
    /// </summary>
    public enum RefuelDischargeEndV1 : byte
    {
        EndA = 0,
        EndB = 1
    }

    /// <summary>
    /// Immutable discharge evidence over the fields currently represented by
    /// BundleState. It is deliberately not the complete P2-T03
    /// DischargeRecordV1 envelope; I/Xe, power history, coefficient binding,
    /// and canonical record digests remain later state-contract work.
    /// </summary>
    public sealed class RefuelDischargeRecordV1
    {
        private RefuelDischargeRecordV1(
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
            ulong coreStateVersionAtDischarge)
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
        }

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

        internal static ContractValidationResult<RefuelDischargeRecordV1> TryCreate(
            BundleState bundle,
            double dischargedAtSeconds,
            RefuelDischargeEndV1 dischargeEnd,
            StableId commandId,
            ulong coreStateVersionAtDischarge)
        {
            if (bundle == null)
            {
                return Invalid(
                    "RefuelDischarge.Bundle.Null",
                    "bundle",
                    "A discharge record requires a bundle state.");
            }

            if (bundle.BundleId.IsEmpty || commandId.IsEmpty)
            {
                return Invalid(
                    "RefuelDischarge.Identity.Empty",
                    "identity",
                    "A discharge record requires non-empty bundle and command identities.");
            }

            if (!Enum.IsDefined(typeof(RefuelDischargeEndV1), dischargeEnd))
            {
                return Invalid(
                    "RefuelDischarge.End.Invalid",
                    "discharge_end",
                    "The discharge end must be EndA or EndB.");
            }

            if (!ContractValidation.IsFinite(dischargedAtSeconds) || dischargedAtSeconds < 0 ||
                dischargedAtSeconds < bundle.InsertedAtSeconds)
            {
                return Invalid(
                    "RefuelDischarge.Time.Invalid",
                    "discharged_at_s",
                    "Discharge time must be finite, nonnegative, and no earlier than insertion.");
            }

            if (string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value) ||
                !ContractValidation.IsFinite(bundle.InitialBurnupJPerKgHm) || bundle.InitialBurnupJPerKgHm < 0 ||
                !ContractValidation.IsFinite(bundle.CumulativeFissionEnergyJ) || bundle.CumulativeFissionEnergyJ < 0 ||
                !ContractValidation.IsFinite(bundle.HeavyMetalMassKg) || bundle.HeavyMetalMassKg <= 0)
            {
                return Invalid(
                    "RefuelDischarge.BundleState.Invalid",
                    "bundle",
                    "The discharge record may contain only finite valid basic bundle state.");
            }

            return ContractValidationResult<RefuelDischargeRecordV1>.Valid(
                new RefuelDischargeRecordV1(
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
                    coreStateVersionAtDischarge));
        }

        private static ContractValidationResult<RefuelDischargeRecordV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RefuelDischargeRecordV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Immutable result of appending the paired mapping and atomicity events
    /// for one successful P5-T02 shift.
    /// </summary>
    public sealed class RefuelAuditResultV1
    {
        internal RefuelAuditResultV1(
            EventLogV1 finalEventLog,
            EventRecordV1 mappingEvent,
            EventRecordV1 atomicityEvent,
            IEnumerable<RefuelDischargeRecordV1> dischargeRecords)
        {
            FinalEventLog = finalEventLog;
            MappingEvent = mappingEvent;
            AtomicityEvent = atomicityEvent;
            DischargeRecords = new ReadOnlyCollection<RefuelDischargeRecordV1>(dischargeRecords.ToArray());
        }

        public EventLogV1 FinalEventLog { get; }

        public EventRecordV1 MappingEvent { get; }

        public EventRecordV1 AtomicityEvent { get; }

        public IReadOnlyList<RefuelDischargeRecordV1> DischargeRecords { get; }
    }

    /// <summary>
    /// Builds deterministic named refuelling bodies and appends their paired
    /// committed events as one immutable event-log operation.
    /// </summary>
    public static class RefuelAuditTransitionV1
    {
        public static ContractValidationResult<RefuelAuditResultV1> TryAppendCommitted(
            RefuelShiftResult shift,
            EventLogV1 eventLog,
            StableId commandId,
            StableId atomicityEventId,
            ulong sequence,
            Digest32 beforeDigest,
            Digest32 proposedDigest,
            ulong coreStateVersion,
            StateBindingV1 stateBinding)
        {
            if (shift == null)
            {
                return Invalid(
                    "RefuelAudit.Shift.Missing",
                    "shift",
                    "A validated refuelling shift result is required.");
            }

            if (eventLog == null)
            {
                return Invalid(
                    "RefuelAudit.EventLog.Missing",
                    "event_log",
                    "An immutable event log is required.");
            }

            if (commandId.IsEmpty || atomicityEventId.IsEmpty)
            {
                return Invalid(
                    "RefuelAudit.Identity.Empty",
                    "identity",
                    "Command and atomicity event identities are required.");
            }

            if (commandId == atomicityEventId)
            {
                return Invalid(
                    "RefuelAudit.Identity.Duplicate",
                    "atomicity_event_id",
                    "The paired event identities must be distinct.");
            }

            if (sequence == ulong.MaxValue)
            {
                return Invalid(
                    "RefuelAudit.Sequence.Overflow",
                    "sequence",
                    "The paired event sequence cannot allocate a second ordinal.");
            }

            if (beforeDigest == null || proposedDigest == null)
            {
                return Invalid(
                    "RefuelAudit.Digest.Missing",
                    "digest",
                    "The complete pre-state and proposed-state digests are required.");
            }

            if (stateBinding == null || stateBinding.CoreStateVersion != coreStateVersion)
            {
                return Invalid(
                    "RefuelAudit.StateBinding.Mismatch",
                    "state_binding",
                    "The audit events must bind the supplied core state version.");
            }

            if (!ContractValidation.IsFinite(shift.EffectiveTimeSeconds) ||
                shift.EffectiveTimeSeconds < 0)
            {
                return Invalid(
                    "RefuelAudit.Time.Invalid",
                    "effective_time_s",
                    "The shift effective time must be finite and nonnegative.");
            }

            ContractValidationResult<EventOwnerV1> ownerResult = EventOwnerV1.TryForChannel(
                shift.SourceInventory.Topology,
                shift.ChannelId);
            if (!ownerResult.IsValid)
            {
                return Invalid(
                    ownerResult.FirstDiagnostic.Code,
                    ownerResult.FirstDiagnostic.Path,
                    ownerResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<RefuelMappingBodyV1> mappingResult = CreateMappingBody(shift);
            if (!mappingResult.IsValid)
            {
                return Invalid(
                    mappingResult.FirstDiagnostic.Code,
                    mappingResult.FirstDiagnostic.Path,
                    mappingResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<RefuelAtomicityBodyV1> atomicityResult =
                RefuelAtomicityBodyV1.TryCreate(
                    commandId,
                    beforeDigest,
                    proposedDigest,
                    CommitStatusV1.Committed,
                    OptionalTextV1.NotApplicable,
                    OptionalDigest32.NotApplicable);
            if (!atomicityResult.IsValid)
            {
                return Invalid(
                    atomicityResult.FirstDiagnostic.Code,
                    atomicityResult.FirstDiagnostic.Path,
                    atomicityResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<RefuelDischargeRecordV1[]> dischargeResult = CreateDischargeRecords(
                shift,
                commandId,
                coreStateVersion);
            if (!dischargeResult.IsValid)
            {
                return Invalid(
                    dischargeResult.FirstDiagnostic.Code,
                    dischargeResult.FirstDiagnostic.Path,
                    dischargeResult.FirstDiagnostic.Message);
            }

            EventBodyV1 mappingBody = EventBodyV1.FromRefuelMapping(mappingResult.Value);
            EventBodyV1 atomicityBody = EventBodyV1.FromRefuelAtomicity(atomicityResult.Value);
            ContractValidationResult<EventRecordV1> mappingEvent = EventRecordV1.TryCreate(
                EventRankV1.Refuelling,
                sequence,
                commandId,
                ownerResult.Value,
                mappingBody,
                shift.EffectiveTimeSeconds,
                stateBinding,
                CommitStatusV1.Committed,
                null);
            if (!mappingEvent.IsValid)
            {
                return Invalid(
                    mappingEvent.FirstDiagnostic.Code,
                    mappingEvent.FirstDiagnostic.Path,
                    mappingEvent.FirstDiagnostic.Message);
            }

            ContractValidationResult<EventRecordV1> atomicityEvent = EventRecordV1.TryCreate(
                EventRankV1.Refuelling,
                sequence + 1,
                atomicityEventId,
                ownerResult.Value,
                atomicityBody,
                shift.EffectiveTimeSeconds,
                stateBinding,
                CommitStatusV1.Committed,
                null);
            if (!atomicityEvent.IsValid)
            {
                return Invalid(
                    atomicityEvent.FirstDiagnostic.Code,
                    atomicityEvent.FirstDiagnostic.Path,
                    atomicityEvent.FirstDiagnostic.Message);
            }

            if (eventLog.Records.Any(record =>
                    record.EventRank == EventRankV1.Refuelling &&
                    record.SimulationTimeSeconds == shift.EffectiveTimeSeconds &&
                    (record.Sequence == sequence || record.Sequence == sequence + 1)))
            {
                return Invalid(
                    "RefuelAudit.Sequence.Duplicate",
                    "sequence",
                    "The paired refuelling event sequence is already present at this time and rank.");
            }

            ContractValidationResult<EventLogV1> withMapping = eventLog.TryAppend(mappingEvent.Value);
            if (!withMapping.IsValid)
            {
                return Invalid(
                    withMapping.FirstDiagnostic.Code,
                    withMapping.FirstDiagnostic.Path,
                    withMapping.FirstDiagnostic.Message);
            }

            ContractValidationResult<EventLogV1> withAtomicity = withMapping.Value.TryAppend(atomicityEvent.Value);
            if (!withAtomicity.IsValid)
            {
                return Invalid(
                    withAtomicity.FirstDiagnostic.Code,
                    withAtomicity.FirstDiagnostic.Path,
                    withAtomicity.FirstDiagnostic.Message);
            }

            return ContractValidationResult<RefuelAuditResultV1>.Valid(
                new RefuelAuditResultV1(
                    withAtomicity.Value,
                    mappingEvent.Value,
                    atomicityEvent.Value,
                    dischargeResult.Value));
        }

        private static ContractValidationResult<RefuelMappingBodyV1> CreateMappingBody(
            RefuelShiftResult shift)
        {
            int positionCount = checked((int)shift.PositionPlan.BundlePositionCount);
            int shiftCount = shift.PositionPlan.ShiftCount;
            var oldByPosition = new BundleState[positionCount];
            for (int position = 0; position < positionCount; position++)
            {
                BundleState? bundle = shift.SourceInventory.Get(
                    new NodeKey(shift.ChannelId, new BundlePosition((uint)position)));
                if (bundle == null)
                {
                    return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                        "RefuelAudit.Mapping.SourceGap",
                        "source_inventory",
                        "The source channel must remain fully occupied for audit generation.");
                }

                oldByPosition[position] = bundle;
            }

            var movedIds = new List<StableId>(positionCount - shiftCount);
            var insertedIds = new List<StableId>(shiftCount);
            var dischargedIds = new List<StableId>(shiftCount);
            var bindings = new List<PositionBindingV1>(positionCount + shiftCount);

            if (shift.PositionPlan.ShiftDirection == RefuelShiftDirection.TowardEndB)
            {
                for (int source = 0; source < positionCount - shiftCount; source++)
                {
                    BundleState oldBundle = oldByPosition[source];
                    BundlePosition destination = new BundlePosition(checked((uint)(source + shiftCount)));
                    BundleState? resulting = shift.ResultingInventory.Get(
                        new NodeKey(shift.ChannelId, destination));
                    if (resulting == null || resulting.BundleId != oldBundle.BundleId)
                    {
                        return MappingMismatch();
                    }

                    movedIds.Add(oldBundle.BundleId);
                    ContractValidationResult<PositionBindingV1> binding = PositionBindingV1.TryCreate(
                        oldBundle.BundleId,
                        OptionalChannelIdV1.Applicable(shift.ChannelId),
                        OptionalBundlePositionV1.Applicable(oldBundle.Position),
                        OptionalChannelIdV1.Applicable(shift.ChannelId),
                        OptionalBundlePositionV1.Applicable(destination),
                        MovementStatusV1.Moved);
                    if (!binding.IsValid)
                    {
                        return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                            binding.FirstDiagnostic.Code,
                            binding.FirstDiagnostic.Path,
                            binding.FirstDiagnostic.Message);
                    }

                    bindings.Add(binding.Value);
                }
            }
            else
            {
                for (int source = shiftCount; source < positionCount; source++)
                {
                    BundleState oldBundle = oldByPosition[source];
                    BundlePosition destination = new BundlePosition(checked((uint)(source - shiftCount)));
                    BundleState? resulting = shift.ResultingInventory.Get(
                        new NodeKey(shift.ChannelId, destination));
                    if (resulting == null || resulting.BundleId != oldBundle.BundleId)
                    {
                        return MappingMismatch();
                    }

                    movedIds.Add(oldBundle.BundleId);
                    ContractValidationResult<PositionBindingV1> binding = PositionBindingV1.TryCreate(
                        oldBundle.BundleId,
                        OptionalChannelIdV1.Applicable(shift.ChannelId),
                        OptionalBundlePositionV1.Applicable(oldBundle.Position),
                        OptionalChannelIdV1.Applicable(shift.ChannelId),
                        OptionalBundlePositionV1.Applicable(destination),
                        MovementStatusV1.Moved);
                    if (!binding.IsValid)
                    {
                        return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                            binding.FirstDiagnostic.Code,
                            binding.FirstDiagnostic.Path,
                            binding.FirstDiagnostic.Message);
                    }

                    bindings.Add(binding.Value);
                }
            }

            for (int index = 0; index < shift.PositionPlan.InsertedPositions.Count; index++)
            {
                BundlePosition destination = shift.PositionPlan.InsertedPositions[index];
                BundleState? inserted = shift.ResultingInventory.Get(new NodeKey(shift.ChannelId, destination));
                if (inserted == null || inserted.CumulativeFissionEnergyJ != 0 ||
                    inserted.InsertedAtSeconds != shift.EffectiveTimeSeconds)
                {
                    return MappingMismatch();
                }

                insertedIds.Add(inserted.BundleId);
                ContractValidationResult<PositionBindingV1> binding = PositionBindingV1.TryCreate(
                    inserted.BundleId,
                    OptionalChannelIdV1.NotApplicable,
                    OptionalBundlePositionV1.NotApplicable,
                    OptionalChannelIdV1.Applicable(shift.ChannelId),
                    OptionalBundlePositionV1.Applicable(destination),
                    MovementStatusV1.Inserted);
                if (!binding.IsValid)
                {
                    return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                        binding.FirstDiagnostic.Code,
                        binding.FirstDiagnostic.Path,
                        binding.FirstDiagnostic.Message);
                }

                bindings.Add(binding.Value);
            }

            for (int index = 0; index < shift.DischargedBundles.Count; index++)
            {
                BundleState discharged = shift.DischargedBundles[index];
                BundlePosition expected = shift.PositionPlan.DischargedPositions[index];
                if (discharged.Position != expected ||
                    shift.ResultingInventory.TryFind(discharged.BundleId, out _))
                {
                    return MappingMismatch();
                }

                dischargedIds.Add(discharged.BundleId);
                ContractValidationResult<PositionBindingV1> binding = PositionBindingV1.TryCreate(
                    discharged.BundleId,
                    OptionalChannelIdV1.Applicable(shift.ChannelId),
                    OptionalBundlePositionV1.Applicable(expected),
                    OptionalChannelIdV1.NotApplicable,
                    OptionalBundlePositionV1.NotApplicable,
                    MovementStatusV1.Discharged);
                if (!binding.IsValid)
                {
                    return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                        binding.FirstDiagnostic.Code,
                        binding.FirstDiagnostic.Path,
                        binding.FirstDiagnostic.Message);
                }

                bindings.Add(binding.Value);
            }

            return RefuelMappingBodyV1.TryCreate(
                shift.PositionPlan.SchemeId,
                shift.EffectiveTimeSeconds,
                shift.PositionPlan.ShiftCount,
                movedIds,
                insertedIds,
                dischargedIds,
                bindings);
        }

        private static ContractValidationResult<RefuelDischargeRecordV1[]> CreateDischargeRecords(
            RefuelShiftResult shift,
            StableId commandId,
            ulong coreStateVersion)
        {
            RefuelDischargeEndV1 end = shift.PositionPlan.ShiftDirection == RefuelShiftDirection.TowardEndB
                ? RefuelDischargeEndV1.EndB
                : RefuelDischargeEndV1.EndA;
            var records = new RefuelDischargeRecordV1[shift.DischargedBundles.Count];
            for (int index = 0; index < records.Length; index++)
            {
                ContractValidationResult<RefuelDischargeRecordV1> record = RefuelDischargeRecordV1.TryCreate(
                    shift.DischargedBundles[index],
                    shift.EffectiveTimeSeconds,
                    end,
                    commandId,
                    coreStateVersion);
                if (!record.IsValid)
                {
                    return ContractValidationResult<RefuelDischargeRecordV1[]>.Invalid(
                        record.FirstDiagnostic.Code,
                        record.FirstDiagnostic.Path,
                        record.FirstDiagnostic.Message);
                }

                records[index] = record.Value;
            }

            return ContractValidationResult<RefuelDischargeRecordV1[]>.Valid(records);
        }

        private static ContractValidationResult<RefuelMappingBodyV1> MappingMismatch()
        {
            return ContractValidationResult<RefuelMappingBodyV1>.Invalid(
                "RefuelAudit.Mapping.PostconditionMismatch",
                "mapping",
                "The shift result does not match the canonical auditable mapping.");
        }

        private static ContractValidationResult<RefuelAuditResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RefuelAuditResultV1>.Invalid(code, path, message);
        }
    }
}
