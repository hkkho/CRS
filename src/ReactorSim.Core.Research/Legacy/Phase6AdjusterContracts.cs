using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// The three approved mechanical-adjuster motion modes. Disabled is an
    /// explicit state flag; it is not inferred from a numeric fraction.
    /// </summary>
    public enum AdjusterMotionModeV1 : byte
    {
        Manual = 0,
        Prescribed = 1,
        RateLimited = 2
    }

    /// <summary>
    /// One explicit queue-owned available command projection. Queue mutation
    /// is intentionally not implemented by P6-T03.
    /// </summary>
    public sealed class AdjusterAvailableCommandV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterAvailableCommandV1(
            StableId targetBankId,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            TargetBankId = targetBankId;
            BoundedFraction = boundedFraction;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
        }

        public StableId TargetBankId { get; }

        public double BoundedFraction { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public static ContractValidationResult<AdjusterAvailableCommandV1> TryCreate(
            StableId targetBankId,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            if (targetBankId.IsEmpty)
            {
                return Invalid(
                    "AdjusterAvailableCommand.TargetBankId.Empty",
                    "target_bank_id",
                    "An available adjuster command requires an explicit target bank identity.");
            }

            if (!AdjusterValidationV1.IsCanonicalFraction(boundedFraction) ||
                !AdjusterValidationV1.IsCanonicalFraction(lowerBound) ||
                !AdjusterValidationV1.IsCanonicalFraction(upperBound))
            {
                return Invalid(
                    "AdjusterAvailableCommand.Fraction.Invalid",
                    "fraction",
                    "Available command fractions and bounds must be finite, nonnegative, and in [0,1] without signed zero.");
            }

            if (lowerBound != 0.0 || upperBound != 1.0 || boundedFraction < lowerBound ||
                boundedFraction > upperBound)
            {
                return Invalid(
                    "AdjusterAvailableCommand.Bounds.Invalid",
                    "bounds",
                    "The approved adjuster command bounds are exactly [0,1] and must contain the bounded fraction.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(rateLimitPerSecond))
            {
                return Invalid(
                    "AdjusterAvailableCommand.RateLimit.Invalid",
                    "rate_limit_per_second",
                    "The available command rate limit must be finite and nonnegative fraction per second without signed zero.");
            }

            return ContractValidationResult<AdjusterAvailableCommandV1>.Valid(
                new AdjusterAvailableCommandV1(
                    targetBankId,
                    boundedFraction,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-QUEUE-AVAILABLE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, TargetBankId);
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
            });
        }

        private static ContractValidationResult<AdjusterAvailableCommandV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterAvailableCommandV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One immutable pending delayed adjuster command. P6-T03 validates this
    /// queue-state record but does not allocate, enqueue, consume, or mutate it.
    /// </summary>
    public sealed class AdjusterPendingCommandV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const ushort BranchUpdateEventRank = 4;
        public const byte EntityBranchKindOrdinal = 7;

        private AdjusterPendingCommandV1(
            StableId queueId,
            StableId commandId,
            StableId targetBankId,
            StableId ownerBranchId,
            StableId sourceEventId,
            Digest32 sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            ulong sequence,
            double requestedFraction,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32 commandDigest)
        {
            QueueId = queueId;
            CommandId = commandId;
            TargetBankId = targetBankId;
            OwnerBranchId = ownerBranchId;
            SourceEventId = sourceEventId;
            SourceStateBindingDigest = sourceStateBindingDigest;
            EnqueueTimeSeconds = enqueueTimeSeconds;
            DelaySeconds = delaySeconds;
            DueTimeSeconds = dueTimeSeconds;
            EventRank = BranchUpdateEventRank;
            Sequence = sequence;
            RequestedFraction = requestedFraction;
            BoundedFraction = boundedFraction;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
            CommandDigest = commandDigest;
        }

        public StableId QueueId { get; }

        public StableId CommandId { get; }

        public StableId TargetBankId { get; }

        public StableId OwnerBranchId { get; }

        public StableId SourceEventId { get; }

        public Digest32 SourceStateBindingDigest { get; }

        public double EnqueueTimeSeconds { get; }

        public double DelaySeconds { get; }

        public double DueTimeSeconds { get; }

        public ushort EventRank { get; }

        public ulong Sequence { get; }

        public double RequestedFraction { get; }

        public double BoundedFraction { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public Digest32 CommandDigest { get; }

        public static ContractValidationResult<AdjusterPendingCommandV1> TryCreate(
            StableId queueId,
            StableId commandId,
            StableId targetBankId,
            StableId ownerBranchId,
            StableId sourceEventId,
            Digest32? sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            ulong sequence,
            double requestedFraction,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32? expectedCommandDigest = null)
        {
            if (queueId.IsEmpty || commandId.IsEmpty || targetBankId.IsEmpty ||
                ownerBranchId.IsEmpty || sourceEventId.IsEmpty || sourceStateBindingDigest == null)
            {
                return Invalid(
                    "AdjusterPendingCommand.Identity.Missing",
                    "command",
                    "A pending command requires queue, command, target, typed owner, source-event, and state-binding identities.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(enqueueTimeSeconds) ||
                !AdjusterValidationV1.IsCanonicalNonnegative(delaySeconds) ||
                !AdjusterValidationV1.IsCanonicalNonnegative(dueTimeSeconds))
            {
                return Invalid(
                    "AdjusterPendingCommand.Time.Invalid",
                    "time",
                    "Pending command times must be finite, nonnegative SI seconds without signed zero.");
            }

            if (dueTimeSeconds != enqueueTimeSeconds + delaySeconds)
            {
                return Invalid(
                    "AdjusterPendingCommand.DueTime.Mismatch",
                    "due_time_s",
                    "DueTime must equal EnqueueTime plus Delay exactly.");
            }

            if (!AdjusterValidationV1.IsCanonicalFraction(requestedFraction) ||
                !AdjusterValidationV1.IsCanonicalFraction(boundedFraction) ||
                !AdjusterValidationV1.IsCanonicalFraction(lowerBound) ||
                !AdjusterValidationV1.IsCanonicalFraction(upperBound) ||
                lowerBound != 0.0 || upperBound != 1.0 || boundedFraction < lowerBound ||
                boundedFraction > upperBound)
            {
                return Invalid(
                    "AdjusterPendingCommand.Fraction.Invalid",
                    "fraction",
                    "Pending command fractions must use the exact inclusive [0,1] bounds.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(rateLimitPerSecond))
            {
                return Invalid(
                    "AdjusterPendingCommand.RateLimit.Invalid",
                    "rate_limit_per_second",
                    "Pending command rate limit must be finite and nonnegative fraction per second without signed zero.");
            }

            Digest32 commandDigest = ComputeDigest(
                queueId,
                commandId,
                targetBankId,
                ownerBranchId,
                sourceEventId,
                sourceStateBindingDigest,
                enqueueTimeSeconds,
                delaySeconds,
                dueTimeSeconds,
                sequence,
                requestedFraction,
                boundedFraction,
                lowerBound,
                upperBound,
                rateLimitPerSecond);
            if (expectedCommandDigest != null && !expectedCommandDigest.Equals(commandDigest))
            {
                return Invalid(
                    "AdjusterPendingCommand.CommandDigest.Mismatch",
                    "command_digest",
                    "The supplied command digest does not equal the canonical command body.");
            }

            return ContractValidationResult<AdjusterPendingCommandV1>.Valid(
                new AdjusterPendingCommandV1(
                    queueId,
                    commandId,
                    targetBankId,
                    ownerBranchId,
                    sourceEventId,
                    sourceStateBindingDigest,
                    enqueueTimeSeconds,
                    delaySeconds,
                    dueTimeSeconds,
                    sequence,
                    requestedFraction,
                    boundedFraction,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond,
                    commandDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(CommandDigest);
        }

        private byte[] BuildBytes(Digest32? commandDigest)
        {
            return BuildBytes(
                QueueId,
                CommandId,
                TargetBankId,
                OwnerBranchId,
                SourceEventId,
                SourceStateBindingDigest,
                EnqueueTimeSeconds,
                DelaySeconds,
                DueTimeSeconds,
                Sequence,
                RequestedFraction,
                BoundedFraction,
                LowerBound,
                UpperBound,
                RateLimitPerSecond,
                commandDigest);
        }

        private static byte[] BuildBytes(
            StableId queueId,
            StableId commandId,
            StableId targetBankId,
            StableId ownerBranchId,
            StableId sourceEventId,
            Digest32 sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            ulong sequence,
            double requestedFraction,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32? commandDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-ADJUSTER-COMMAND-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, queueId);
                Phase5CanonicalBytesV1.WriteStableId(writer, commandId);
                Phase5CanonicalBytesV1.WriteStableId(writer, targetBankId);
                writer.Write(EntityBranchKindOrdinal);
                Phase5CanonicalBytesV1.WriteStableId(writer, ownerBranchId);
                Phase5CanonicalBytesV1.WriteStableId(writer, sourceEventId);
                Phase5CanonicalBytesV1.WriteDigest(writer, sourceStateBindingDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, enqueueTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, delaySeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, dueTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt16(writer, BranchUpdateEventRank);
                Phase5CanonicalBytesV1.WriteUInt64(writer, sequence);
                Phase5CanonicalBytesV1.WriteDouble(writer, requestedFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, boundedFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, lowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, upperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, rateLimitPerSecond);
                if (commandDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, commandDigest);
                }
            });
        }

        private static Digest32 ComputeDigest(
            StableId queueId,
            StableId commandId,
            StableId targetBankId,
            StableId ownerBranchId,
            StableId sourceEventId,
            Digest32 sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            ulong sequence,
            double requestedFraction,
            double boundedFraction,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        queueId,
                        commandId,
                        targetBankId,
                        ownerBranchId,
                        sourceEventId,
                        sourceStateBindingDigest,
                        enqueueTimeSeconds,
                        delaySeconds,
                        dueTimeSeconds,
                        sequence,
                        requestedFraction,
                        boundedFraction,
                        lowerBound,
                        upperBound,
                        rateLimitPerSecond,
                        null)));
        }

        private static ContractValidationResult<AdjusterPendingCommandV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterPendingCommandV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete immutable queue state supplied to the P6-T03 motion
    /// projection. It is deliberately a read-only contract; queue allocation,
    /// enqueue, consume, transition, and rollback belong to P6-T06.
    /// </summary>
    public sealed class AdjusterQueueStateV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string QueueSchemaId = "CANDU-ADJUSTER-QUEUE-V1";
        public const byte EntityBranchKindOrdinal = 7;

        private AdjusterQueueStateV1(
            StableId queueId,
            StableId ownerBranchId,
            AdjusterOptionalDoubleV1 generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            IEnumerable<AdjusterAvailableCommandV1> availableCommands,
            double lastMotionTimeSeconds,
            IEnumerable<AdjusterPendingCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds,
            Digest32 queueDigest)
        {
            QueueId = queueId;
            OwnerBranchId = ownerBranchId;
            GenerationCadenceOrNA = generationCadenceOrNA;
            InitialNextSequence = initialNextSequence;
            NextSequence = nextSequence;
            AvailableCommands = new ReadOnlyCollection<AdjusterAvailableCommandV1>(
                availableCommands.ToArray());
            LastMotionTimeSeconds = lastMotionTimeSeconds;
            PendingCommands = new ReadOnlyCollection<AdjusterPendingCommandV1>(
                pendingCommands.ToArray());
            AppliedSourceEventIds = new ReadOnlyCollection<StableId>(
                appliedSourceEventIds.ToArray());
            AllocatedCommandIds = new ReadOnlyCollection<StableId>(
                allocatedCommandIds.ToArray());
            QueueDigest = queueDigest;
        }

        public StableId QueueId { get; }

        public StableId OwnerBranchId { get; }

        public AdjusterOptionalDoubleV1 GenerationCadenceOrNA { get; }

        public ulong InitialNextSequence { get; }

        public ulong NextSequence { get; }

        public IReadOnlyList<AdjusterAvailableCommandV1> AvailableCommands { get; }

        public double LastMotionTimeSeconds { get; }

        public IReadOnlyList<AdjusterPendingCommandV1> PendingCommands { get; }

        public IReadOnlyList<StableId> AppliedSourceEventIds { get; }

        public IReadOnlyList<StableId> AllocatedCommandIds { get; }

        public Digest32 QueueDigest { get; }

        public static ContractValidationResult<AdjusterQueueStateV1> TryCreate(
            uint schemaVersion,
            StableId queueId,
            StableId ownerBranchId,
            AdjusterOptionalDoubleV1? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            IEnumerable<AdjusterAvailableCommandV1>? availableCommands,
            double lastMotionTimeSeconds,
            IEnumerable<AdjusterPendingCommandV1>? pendingCommands,
            IEnumerable<StableId>? appliedSourceEventIds,
            IEnumerable<StableId>? allocatedCommandIds,
            Digest32? expectedQueueDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "AdjusterQueueState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only adjuster queue schema version 1 is accepted.");
            }

            if (queueId.IsEmpty || ownerBranchId.IsEmpty || generationCadenceOrNA == null)
            {
                return Invalid(
                    "AdjusterQueueState.Identity.Missing",
                    "queue",
                    "A queue state requires a queue identity, typed Entity.Branch owner identity, and generation cadence applicability.");
            }

            if (generationCadenceOrNA.IsApplicable &&
                !AdjusterValidationV1.IsCanonicalPositive(generationCadenceOrNA.Value))
            {
                return Invalid(
                    "AdjusterQueueState.GenerationCadence.Invalid",
                    "generation_cadence_or_na",
                    "Automatic generation cadence must be finite, strictly positive SI seconds without signed zero.");
            }

            if (initialNextSequence > nextSequence)
            {
                return Invalid(
                    "AdjusterQueueState.Sequence.Order",
                    "next_sequence",
                    "NextSequence may not precede InitialNextSequence.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(lastMotionTimeSeconds))
            {
                return Invalid(
                    "AdjusterQueueState.LastMotionTime.Invalid",
                    "last_motion_time_s",
                    "LastMotionTime must be finite and nonnegative SI seconds without signed zero.");
            }

            if (availableCommands == null || pendingCommands == null ||
                appliedSourceEventIds == null || allocatedCommandIds == null)
            {
                return Invalid(
                    "AdjusterQueueState.Collections.Missing",
                    "queue_state",
                    "A complete queue state requires available, pending, applied-event, and allocated-command collections.");
            }

            AdjusterAvailableCommandV1[] available = availableCommands.ToArray();
            if (available.Any(command => command == null))
            {
                return Invalid(
                    "AdjusterQueueState.Available.Null",
                    "available_commands",
                    "Available command records may not be null.");
            }

            AdjusterAvailableCommandV1[] canonicalAvailable = available
                .OrderBy(command => command.TargetBankId)
                .ToArray();
            for (int index = 1; index < canonicalAvailable.Length; index++)
            {
                if (canonicalAvailable[index - 1].TargetBankId == canonicalAvailable[index].TargetBankId)
                {
                    return Invalid(
                        "AdjusterQueueState.Available.DuplicateTarget",
                        "available_commands",
                        "A queue may contain only one available command projection per target bank.");
                }
            }

            AdjusterPendingCommandV1[] pending = pendingCommands.ToArray();
            if (pending.Any(command => command == null))
            {
                return Invalid(
                    "AdjusterQueueState.Pending.Null",
                    "pending_commands",
                    "Pending command records may not be null.");
            }

            AdjusterPendingCommandV1[] canonicalPending = pending
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray();
            for (int index = 0; index < canonicalPending.Length; index++)
            {
                AdjusterPendingCommandV1 command = canonicalPending[index];
                if (command.QueueId != queueId || command.OwnerBranchId != ownerBranchId)
                {
                    return Invalid(
                        "AdjusterQueueState.Pending.OwnerMismatch",
                        "pending_commands",
                        "Every pending command must bind this queue and its typed branch owner.");
                }

                if (index > 0 && canonicalPending[index - 1].CommandId == command.CommandId)
                {
                    return Invalid(
                        "AdjusterQueueState.Pending.DuplicateCommand",
                        "pending_commands",
                        "Pending command identities must be unique.");
                }
            }

            StableId[] applied = appliedSourceEventIds.ToArray();
            StableId[] allocated = allocatedCommandIds.ToArray();
            if (applied.Any(id => id.IsEmpty) || allocated.Any(id => id.IsEmpty))
            {
                return Invalid(
                    "AdjusterQueueState.Registry.EmptyId",
                    "queue_state",
                    "Applied source-event and allocated-command registries may not contain empty identities.");
            }

            StableId[] canonicalApplied = applied.OrderBy(id => id).ToArray();
            StableId[] canonicalAllocated = allocated.OrderBy(id => id).ToArray();
            if (HasDuplicate(canonicalApplied) || HasDuplicate(canonicalAllocated))
            {
                return Invalid(
                    "AdjusterQueueState.Registry.Duplicate",
                    "queue_state",
                    "Applied source-event and allocated-command registries must be unique.");
            }

            if (canonicalPending.Any(command => canonicalAllocated.Contains(command.CommandId)))
            {
                return Invalid(
                    "AdjusterQueueState.CommandRegistry.Overlap",
                    "queue_state",
                    "A command identity may not be both pending and already allocated.");
            }

            if (nextSequence - initialNextSequence != (ulong)canonicalAllocated.Length)
            {
                return Invalid(
                    "AdjusterQueueState.Sequence.CountMismatch",
                    "allocated_command_ids",
                    "Allocated command count must equal NextSequence minus InitialNextSequence.");
            }

            Digest32 queueDigest = ComputeDigest(
                queueId,
                ownerBranchId,
                generationCadenceOrNA,
                initialNextSequence,
                nextSequence,
                canonicalAvailable,
                lastMotionTimeSeconds,
                canonicalPending,
                canonicalApplied,
                canonicalAllocated);
            if (expectedQueueDigest != null && !expectedQueueDigest.Equals(queueDigest))
            {
                return Invalid(
                    "AdjusterQueueState.QueueDigest.Mismatch",
                    "queue_digest",
                    "The supplied queue digest does not equal the complete canonical queue state.");
            }

            return ContractValidationResult<AdjusterQueueStateV1>.Valid(
                new AdjusterQueueStateV1(
                    queueId,
                    ownerBranchId,
                    generationCadenceOrNA,
                    initialNextSequence,
                    nextSequence,
                    canonicalAvailable,
                    lastMotionTimeSeconds,
                    canonicalPending,
                    canonicalApplied,
                    canonicalAllocated,
                    queueDigest));
        }

        public ContractValidationResult<AdjusterAvailableCommandV1> TryGetAvailableCommand(
            StableId targetBankId)
        {
            if (targetBankId.IsEmpty)
            {
                return ContractValidationResult<AdjusterAvailableCommandV1>.Invalid(
                    "AdjusterQueueState.TargetBankId.Empty",
                    "target_bank_id",
                    "An available command lookup requires a target bank identity.");
            }

            AdjusterAvailableCommandV1? command = AvailableCommands.SingleOrDefault(
                candidate => candidate.TargetBankId == targetBankId);
            return command == null
                ? ContractValidationResult<AdjusterAvailableCommandV1>.Invalid(
                    "AdjusterQueueState.AvailableCommand.Missing",
                    "available_commands",
                    "The complete queue state has no available command for the requested bank.")
                : ContractValidationResult<AdjusterAvailableCommandV1>.Valid(command);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                QueueId,
                OwnerBranchId,
                GenerationCadenceOrNA,
                InitialNextSequence,
                NextSequence,
                AvailableCommands,
                LastMotionTimeSeconds,
                PendingCommands,
                AppliedSourceEventIds,
                AllocatedCommandIds,
                QueueDigest);
        }

        public static Digest32 ComputeDigest(
            StableId queueId,
            StableId ownerBranchId,
            AdjusterOptionalDoubleV1 generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            IEnumerable<AdjusterAvailableCommandV1> availableCommands,
            double lastMotionTimeSeconds,
            IEnumerable<AdjusterPendingCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds)
        {
            if (availableCommands == null || pendingCommands == null ||
                appliedSourceEventIds == null || allocatedCommandIds == null)
            {
                throw new ArgumentNullException(nameof(availableCommands));
            }

            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        queueId,
                        ownerBranchId,
                        generationCadenceOrNA,
                        initialNextSequence,
                        nextSequence,
                        availableCommands.OrderBy(command => command.TargetBankId).ToArray(),
                        lastMotionTimeSeconds,
                        pendingCommands
                            .OrderBy(command => command.DueTimeSeconds)
                            .ThenBy(command => command.EventRank)
                            .ThenBy(command => command.Sequence)
                            .ThenBy(command => command.CommandId)
                            .ToArray(),
                        appliedSourceEventIds.OrderBy(id => id).ToArray(),
                        allocatedCommandIds.OrderBy(id => id).ToArray(),
                        null)));
        }

        private static byte[] BuildBytes(
            StableId queueId,
            StableId ownerBranchId,
            AdjusterOptionalDoubleV1 generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            IReadOnlyList<AdjusterAvailableCommandV1> availableCommands,
            double lastMotionTimeSeconds,
            IReadOnlyList<AdjusterPendingCommandV1> pendingCommands,
            IReadOnlyList<StableId> appliedSourceEventIds,
            IReadOnlyList<StableId> allocatedCommandIds,
            Digest32? queueDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, QueueSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, queueId);
                writer.Write(EntityBranchKindOrdinal);
                Phase5CanonicalBytesV1.WriteStableId(writer, ownerBranchId);
                generationCadenceOrNA.WriteCanonicalBytes(writer);
                Phase5CanonicalBytesV1.WriteUInt64(writer, initialNextSequence);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nextSequence);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)availableCommands.Count));
                foreach (AdjusterAvailableCommandV1 command in availableCommands)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteDouble(writer, lastMotionTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)pendingCommands.Count));
                foreach (AdjusterPendingCommandV1 command in pendingCommands)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)appliedSourceEventIds.Count));
                foreach (StableId id in appliedSourceEventIds)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                Phase5CanonicalBytesV1.WriteUInt32(
                    writer,
                    checked((uint)allocatedCommandIds.Count));
                foreach (StableId id in allocatedCommandIds)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                if (queueDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, queueDigest);
                }
            });
        }

        private static bool HasDuplicate(StableId[] values)
        {
            for (int index = 1; index < values.Length; index++)
            {
                if (values[index - 1] == values[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static ContractValidationResult<AdjusterQueueStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterQueueStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One explicit bank-to-node grouping entry. The approved fixture carries
    /// ordinal order and bank identity so that no array position is inferred.
    /// </summary>
    public sealed class AdjusterBankTargetBindingV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterBankTargetBindingV1(
            uint bankOrdinal,
            StableId bankId,
            NodeKey targetNode)
        {
            BankOrdinal = bankOrdinal;
            BankId = bankId;
            TargetNode = targetNode;
        }

        public uint BankOrdinal { get; }

        public StableId BankId { get; }

        public NodeKey TargetNode { get; }

        public static ContractValidationResult<AdjusterBankTargetBindingV1> TryCreate(
            uint bankOrdinal,
            StableId bankId,
            NodeKey targetNode)
        {
            if (bankOrdinal >= AdjusterBankGroupingV1.BankCount)
            {
                return Invalid(
                    "AdjusterBankTargetBinding.BankOrdinal.OutOfRange",
                    "bank_ordinal",
                    "The approved synthetic grouping has exactly two bank ordinals, 0 and 1.");
            }

            if (bankId.IsEmpty)
            {
                return Invalid(
                    "AdjusterBankTargetBinding.BankId.Empty",
                    "bank_id",
                    "A bank grouping entry requires an explicit bank identity.");
            }

            return ContractValidationResult<AdjusterBankTargetBindingV1>.Valid(
                new AdjusterBankTargetBindingV1(bankOrdinal, bankId, targetNode));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-BANK-TARGET-BINDING-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, BankOrdinal);
                Phase5CanonicalBytesV1.WriteStableId(writer, BankId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.ChannelId.Value);
                Phase5CanonicalBytesV1.WriteUInt32(writer, TargetNode.Position.Value);
            });
        }

        private static ContractValidationResult<AdjusterBankTargetBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterBankTargetBindingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// The owner-approved synthetic two-bank grouping. It is intentionally
    /// narrow and does not claim a production adjuster geometry.
    /// </summary>
    public sealed class AdjusterBankGroupingV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint BankCount = 2;
        public const uint TargetNodeCount = 6;
        public const uint TargetsPerBank = 3;
        public const string SchemaId = "CANDU-ADJUSTER-BANK-GROUPING-V1";
        public const string ApprovedMappingVersion =
            "p6-t03-synthetic-bank-grouping-v1";
        public const string ApprovedOwnerId = "Kevin Ho";

        public static readonly StableId ApprovedGroupingId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a703");

        public static readonly StableId ApprovedBankAId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a705");

        public static readonly StableId ApprovedBankBId = StableId.Parse(
            "00000000-0000-0000-0000-00000000a706");

        private AdjusterBankGroupingV1(
            StableId groupingId,
            string mappingVersion,
            IEnumerable<AdjusterBankTargetBindingV1> mappings,
            Digest32 mappingDigest)
        {
            GroupingId = groupingId;
            MappingVersion = mappingVersion;
            Mappings = new ReadOnlyCollection<AdjusterBankTargetBindingV1>(
                mappings.ToArray());
            MappingDigest = mappingDigest;
        }

        public StableId GroupingId { get; }

        public string MappingVersion { get; }

        public IReadOnlyList<AdjusterBankTargetBindingV1> Mappings { get; }

        public Digest32 MappingDigest { get; }

        public static ContractValidationResult<AdjusterBankGroupingV1> TryCreate(
            uint schemaVersion,
            StableId groupingId,
            string? mappingVersion,
            IEnumerable<AdjusterBankTargetBindingV1>? mappings,
            Digest32? mappingDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "AdjusterBankGrouping.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only synthetic adjuster grouping schema version 1 is accepted.");
            }

            if (groupingId != ApprovedGroupingId)
            {
                return Invalid(
                    "AdjusterBankGrouping.GroupingId.Unapproved",
                    "grouping_id",
                    "Only the owner-approved synthetic grouping identity is admitted.");
            }

            if (!AdjusterValidationV1.IsCanonicalText(mappingVersion) ||
                !string.Equals(mappingVersion, ApprovedMappingVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterBankGrouping.MappingVersion.Unapproved",
                    "mapping_version",
                    "Only the owner-approved synthetic grouping version is admitted.");
            }

            if (mappings == null || mappingDigest == null)
            {
                return Invalid(
                    "AdjusterBankGrouping.Mapping.Missing",
                    "mapping",
                    "A grouping requires explicit mappings and a mapping digest.");
            }

            AdjusterBankTargetBindingV1[] entries = mappings.ToArray();
            if (entries.Length != (int)(BankCount * TargetsPerBank))
            {
                return Invalid(
                    "AdjusterBankGrouping.Mapping.CountMismatch",
                    "mappings",
                    "The approved grouping requires exactly six bank-to-node bindings.");
            }

            if (entries.Any(entry => entry == null))
            {
                return Invalid(
                    "AdjusterBankGrouping.Mapping.Null",
                    "mappings",
                    "Grouping entries may not be null.");
            }

            AdjusterBankTargetBindingV1[] canonical = entries
                .OrderBy(entry => entry.BankOrdinal)
                .ThenBy(entry => entry.BankId)
                .ThenBy(entry => entry.TargetNode)
                .ToArray();
            for (int index = 1; index < canonical.Length; index++)
            {
                if (canonical[index - 1].BankOrdinal == canonical[index].BankOrdinal &&
                    canonical[index - 1].BankId == canonical[index].BankId &&
                    canonical[index - 1].TargetNode == canonical[index].TargetNode)
                {
                    return Invalid(
                        "AdjusterBankGrouping.Mapping.Duplicate",
                        "mappings",
                        "A bank-to-node grouping identity may occur only once.");
                }
            }

            if (!MatchesApprovedContent(canonical))
            {
                return Invalid(
                    "AdjusterBankGrouping.MappingContent.Unapproved",
                    "mappings",
                    "The grouping content must equal the approved two-bank synthetic table.");
            }

            Digest32 expectedDigest = ComputeDigest(
                schemaVersion,
                groupingId,
                mappingVersion!,
                canonical);
            if (!mappingDigest.Equals(expectedDigest))
            {
                return Invalid(
                    "AdjusterBankGrouping.MappingDigest.Mismatch",
                    "mapping_digest",
                    "The supplied grouping digest does not equal the canonical grouping body.");
            }

            return ContractValidationResult<AdjusterBankGroupingV1>.Valid(
                new AdjusterBankGroupingV1(
                    groupingId,
                    mappingVersion!,
                    canonical,
                    expectedDigest));
        }

        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId groupingId,
            string mappingVersion,
            IEnumerable<AdjusterBankTargetBindingV1> mappings)
        {
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            AdjusterBankTargetBindingV1[] canonical = mappings
                .OrderBy(entry => entry.BankOrdinal)
                .ThenBy(entry => entry.BankId)
                .ThenBy(entry => entry.TargetNode)
                .ToArray();
            return Phase6CanonicalDigestPrimitives.ComputeVersionedCollectionDigest(
                SchemaId,
                schemaVersion,
                groupingId,
                mappingVersion,
                canonical.Select(mapping => mapping.ToCanonicalBytes()).ToArray());
        }

        public bool TryGetBankOrdinal(StableId bankId, out uint bankOrdinal)
        {
            AdjusterBankTargetBindingV1? entry = Mappings.FirstOrDefault(
                mapping => mapping.BankId == bankId);
            if (entry == null)
            {
                bankOrdinal = 0;
                return false;
            }

            bankOrdinal = entry.BankOrdinal;
            return true;
        }

        public IReadOnlyList<NodeKey> GetTargetNodes(StableId bankId)
        {
            return new ReadOnlyCollection<NodeKey>(Mappings
                .Where(mapping => mapping.BankId == bankId)
                .OrderBy(mapping => mapping.TargetNode)
                .Select(mapping => mapping.TargetNode)
                .ToArray());
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                CurrentSchemaVersion,
                GroupingId,
                MappingVersion,
                Mappings,
                MappingDigest);
        }

        private static bool MatchesApprovedContent(
            AdjusterBankTargetBindingV1[] mappings)
        {
            if (mappings.Length != 6)
            {
                return false;
            }

            for (uint index = 0; index < 3; index++)
            {
                AdjusterBankTargetBindingV1 first = mappings[(int)index];
                AdjusterBankTargetBindingV1 second = mappings[(int)(index + 3)];
                if (first.BankOrdinal != 0 || first.BankId != ApprovedBankAId ||
                    first.TargetNode != new NodeKey(new ChannelId(index), new BundlePosition(0)) ||
                    second.BankOrdinal != 1 || second.BankId != ApprovedBankBId ||
                    second.TargetNode != new NodeKey(new ChannelId(index + 3), new BundlePosition(0)))
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId groupingId,
            string mappingVersion,
            IReadOnlyList<AdjusterBankTargetBindingV1> mappings,
            Digest32? mappingDigest)
        {
            return Phase6CanonicalDigestPrimitives.BuildVersionedCollectionBytes(
                SchemaId,
                schemaVersion,
                groupingId,
                mappingVersion,
                mappings.Select(mapping => mapping.ToCanonicalBytes()).ToArray(),
                mappingDigest);
        }

        private static ContractValidationResult<AdjusterBankGroupingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterBankGroupingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One immutable approved adjuster-bank state. Queue-owned available
    /// command and motion time are not duplicated in this state.
    /// </summary>
    public sealed class AdjusterBankStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterBankStateV1(
            StableId bankId,
            bool enabled,
            AdjusterMotionModeV1 mode,
            double referenceFraction,
            double commandFraction,
            double stateFraction,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            AdjusterQueueStateV1? queueState,
            Digest32 stateDigest)
        {
            BankId = bankId;
            Enabled = enabled;
            Mode = mode;
            ReferenceFraction = referenceFraction;
            CommandFraction = commandFraction;
            StateFraction = stateFraction;
            RateLimitPerSecond = rateLimitPerSecond;
            DelaySeconds = delaySeconds;
            InfluenceMapId = influenceMapId;
            DataVersion = dataVersion;
            DataDigest = dataDigest;
            UpdateTimeSeconds = updateTimeSeconds;
            QueueState = queueState;
            StateDigest = stateDigest;
        }

        public StableId BankId { get; }

        public bool Enabled { get; }

        public AdjusterMotionModeV1 Mode { get; }

        public double ReferenceFraction { get; }

        public double CommandFraction { get; }

        public double StateFraction { get; }

        public double RateLimitPerSecond { get; }

        public double DelaySeconds { get; }

        public StableId InfluenceMapId { get; }

        public string DataVersion { get; }

        public Digest32 DataDigest { get; }

        public double UpdateTimeSeconds { get; }

        public AdjusterQueueStateV1? QueueState { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<AdjusterBankStateV1> TryCreate(
            uint schemaVersion,
            StableId bankId,
            bool enabled,
            AdjusterMotionModeV1 mode,
            double referenceFraction,
            double commandFraction,
            double stateFraction,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string? dataVersion,
            Digest32? dataDigest,
            double updateTimeSeconds,
            AdjusterQueueStateV1? queueState,
            Digest32? expectedStateDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "AdjusterBankState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only adjuster bank state schema version 1 is accepted.");
            }

            if (bankId != AdjusterBankGroupingV1.ApprovedBankAId &&
                bankId != AdjusterBankGroupingV1.ApprovedBankBId)
            {
                return Invalid(
                    "AdjusterBankState.BankId.Unapproved",
                    "bank_id",
                    "Only the two owner-approved synthetic bank identities are admitted.");
            }

            if (!Enum.IsDefined(typeof(AdjusterMotionModeV1), mode))
            {
                return Invalid(
                    "AdjusterBankState.Mode.Invalid",
                    "mode",
                    "Mode must be Manual, Prescribed, or RateLimited.");
            }

            if (!AdjusterValidationV1.IsCanonicalFraction(referenceFraction) ||
                !AdjusterValidationV1.IsCanonicalFraction(commandFraction) ||
                !AdjusterValidationV1.IsCanonicalFraction(stateFraction))
            {
                return Invalid(
                    "AdjusterBankState.Fraction.Invalid",
                    "fraction",
                    "Reference, command, and state fractions must be finite, nonnegative, and in [0,1] without signed zero.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                !AdjusterValidationV1.IsCanonicalNonnegative(delaySeconds) ||
                !AdjusterValidationV1.IsCanonicalNonnegative(updateTimeSeconds))
            {
                return Invalid(
                    "AdjusterBankState.MotionLimits.Invalid",
                    "motion_limits",
                    "Rate limit, delay, and update time must be finite and nonnegative SI values without signed zero.");
            }

            if (influenceMapId.IsEmpty ||
                !AdjusterValidationV1.IsCanonicalText(dataVersion) ||
                dataDigest == null)
            {
                return Invalid(
                    "AdjusterBankState.DataIdentity.Missing",
                    "data_identity",
                    "An adjuster bank requires an influence-map identity, data version, and data digest.");
            }

            if (mode == AdjusterMotionModeV1.RateLimited)
            {
                if (queueState == null)
                {
                    return Invalid(
                        "AdjusterBankState.QueueState.Missing",
                        "queue_state",
                        "RateLimited mode requires a complete supplied queue state.");
                }

                ContractValidationResult<AdjusterAvailableCommandV1> available =
                    queueState.TryGetAvailableCommand(bankId);
                if (!available.IsValid)
                {
                    return Invalid(
                        "AdjusterBankState.QueueState.AvailableCommand.Missing",
                        "queue_state.available_commands",
                        available.FirstDiagnostic.Message);
                }

                if (available.Value.RateLimitPerSecond != rateLimitPerSecond ||
                    available.Value.LowerBound != 0.0 || available.Value.UpperBound != 1.0)
                {
                    return Invalid(
                        "AdjusterBankState.QueueState.MotionMismatch",
                        "queue_state.available_commands",
                        "The queue projection bounds and rate must equal the bank motion contract.");
                }
            }
            else if (queueState != null)
            {
                return Invalid(
                    "AdjusterBankState.QueueState.NotApplicableMismatch",
                    "queue_state",
                    "Manual and Prescribed modes use an explicit NotApplicable queue state in P6-T03.");
            }

            Digest32 stateDigest = ComputeDigest(
                bankId,
                enabled,
                mode,
                referenceFraction,
                commandFraction,
                stateFraction,
                rateLimitPerSecond,
                delaySeconds,
                influenceMapId,
                dataVersion!,
                dataDigest,
                updateTimeSeconds,
                queueState);
            if (expectedStateDigest != null && !expectedStateDigest.Equals(stateDigest))
            {
                return Invalid(
                    "AdjusterBankState.StateDigest.Mismatch",
                    "state_digest",
                    "The supplied bank state digest does not equal the canonical bank state body.");
            }

            return ContractValidationResult<AdjusterBankStateV1>.Valid(
                new AdjusterBankStateV1(
                    bankId,
                    enabled,
                    mode,
                    referenceFraction,
                    commandFraction,
                    stateFraction,
                    rateLimitPerSecond,
                    delaySeconds,
                    influenceMapId,
                    dataVersion!,
                    dataDigest,
                    updateTimeSeconds,
                    queueState,
                    stateDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                BankId,
                Enabled,
                Mode,
                ReferenceFraction,
                CommandFraction,
                StateFraction,
                RateLimitPerSecond,
                DelaySeconds,
                InfluenceMapId,
                DataVersion,
                DataDigest,
                UpdateTimeSeconds,
                QueueState,
                StateDigest);
        }

        private static Digest32 ComputeDigest(
            StableId bankId,
            bool enabled,
            AdjusterMotionModeV1 mode,
            double referenceFraction,
            double commandFraction,
            double stateFraction,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            AdjusterQueueStateV1? queueState)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        bankId,
                        enabled,
                        mode,
                        referenceFraction,
                        commandFraction,
                        stateFraction,
                        rateLimitPerSecond,
                        delaySeconds,
                        influenceMapId,
                        dataVersion,
                        dataDigest,
                        updateTimeSeconds,
                        queueState,
                        null)));
        }

        private static byte[] BuildBytes(
            StableId bankId,
            bool enabled,
            AdjusterMotionModeV1 mode,
            double referenceFraction,
            double commandFraction,
            double stateFraction,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            AdjusterQueueStateV1? queueState,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-ADJUSTER-BANK-STATE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, bankId);
                writer.Write(enabled ? (byte)1 : (byte)0);
                writer.Write((byte)mode);
                Phase5CanonicalBytesV1.WriteDouble(writer, referenceFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, commandFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, rateLimitPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, delaySeconds);
                Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, dataDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, updateTimeSeconds);
                writer.Write(queueState == null ? (byte)0 : (byte)1);
                if (queueState != null)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, queueState.ToCanonicalBytes());
                }

                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static ContractValidationResult<AdjusterBankStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterBankStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Complete deterministic state for the approved two-bank adjuster set.
    /// </summary>
    public sealed class AdjusterSetStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterSetStateV1(
            StableId adjusterSetId,
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            StableId influenceMapId,
            Digest32 influenceMapDigest,
            AdjusterBankGroupingV1 grouping,
            IEnumerable<AdjusterBankStateV1> banks,
            Digest32 stateDigest)
        {
            AdjusterSetId = adjusterSetId;
            BranchId = branchId;
            CoreStateVersion = coreStateVersion;
            TopologyVersion = topologyVersion;
            DataPackVersion = dataPackVersion;
            InfluenceMapId = influenceMapId;
            InfluenceMapDigest = influenceMapDigest;
            Grouping = grouping;
            Banks = new ReadOnlyCollection<AdjusterBankStateV1>(banks.ToArray());
            StateDigest = stateDigest;
        }

        public StableId AdjusterSetId { get; }

        public StableId BranchId { get; }

        public ulong CoreStateVersion { get; }

        public string TopologyVersion { get; }

        public string DataPackVersion { get; }

        public StableId InfluenceMapId { get; }

        public Digest32 InfluenceMapDigest { get; }

        public AdjusterBankGroupingV1 Grouping { get; }

        public IReadOnlyList<AdjusterBankStateV1> Banks { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<AdjusterSetStateV1> TryCreate(
            uint schemaVersion,
            StableId adjusterSetId,
            StableId branchId,
            ulong coreStateVersion,
            string? topologyVersion,
            string? dataPackVersion,
            AdjusterInfluenceMapV1? map,
            AdjusterBankGroupingV1? grouping,
            IEnumerable<AdjusterBankStateV1>? banks,
            Digest32? expectedStateDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "AdjusterSetState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only adjuster set state schema version 1 is accepted.");
            }

            if (adjusterSetId != AdjusterInfluenceMapV1.ApprovedAdjusterSetId)
            {
                return Invalid(
                    "AdjusterSetState.SetId.Unapproved",
                    "adjuster_set_id",
                    "Only the owner-approved synthetic adjuster-set identity is admitted.");
            }

            if (branchId.IsEmpty || !AdjusterValidationV1.IsCanonicalText(topologyVersion) ||
                !AdjusterValidationV1.IsCanonicalText(dataPackVersion))
            {
                return Invalid(
                    "AdjusterSetState.Identity.Invalid",
                    "state_identity",
                    "A set state requires a typed branch identity and canonical topology/data versions.");
            }

            if (map == null || grouping == null || banks == null)
            {
                return Invalid(
                    "AdjusterSetState.Input.Missing",
                    "state",
                    "A set state requires a validated map, grouping, and bank collection.");
            }

            if (grouping.GroupingId != map.GroupingId ||
                !string.Equals(grouping.MappingVersion, map.MappingVersion, StringComparison.Ordinal) ||
                !grouping.MappingDigest.Equals(map.GroupingDigest))
            {
                return Invalid(
                    "AdjusterSetState.GroupingBinding.Mismatch",
                    "grouping",
                    "The state grouping must equal the admitted map grouping identity and digest.");
            }

            if (map.MapId != AdjusterInfluenceMapV1.ApprovedMapId ||
                !string.Equals(dataPackVersion, map.DataVersion, StringComparison.Ordinal))
            {
                return Invalid(
                    "AdjusterSetState.MapBinding.Mismatch",
                    "data_pack_version",
                    "The set data-pack version must equal the admitted synthetic map version.");
            }

            AdjusterBankStateV1[] entries = banks.ToArray();
            if (entries.Length != (int)AdjusterBankGroupingV1.BankCount ||
                entries.Any(bank => bank == null))
            {
                return Invalid(
                    "AdjusterSetState.Banks.CountMismatch",
                    "banks",
                    "The approved synthetic adjuster set requires exactly two non-null bank states.");
            }

            AdjusterBankStateV1[] canonical = entries
                .OrderBy(bank => bank.BankId)
                .ToArray();
            if (canonical[0].BankId != AdjusterBankGroupingV1.ApprovedBankAId ||
                canonical[1].BankId != AdjusterBankGroupingV1.ApprovedBankBId)
            {
                return Invalid(
                    "AdjusterSetState.Banks.IdentityMismatch",
                    "banks",
                    "The bank collection must contain the approved A and B identities exactly once.");
            }

            for (int index = 0; index < canonical.Length; index++)
            {
                AdjusterBankStateV1 bank = canonical[index];
                if (bank.InfluenceMapId != map.MapId ||
                    !string.Equals(bank.DataVersion, map.DataVersion, StringComparison.Ordinal) ||
                    !bank.DataDigest.Equals(map.MapDigest))
                {
                    return Invalid(
                        "AdjusterSetState.BankMapBinding.Mismatch",
                        "banks[" + index.ToString(CultureInfo.InvariantCulture) + "].data_identity",
                        "Every bank must bind the admitted map identity, version, and digest.");
                }

                if (!map.TryGetReferenceFraction(bank.BankId, out double referenceFraction) ||
                    bank.ReferenceFraction != referenceFraction)
                {
                    return Invalid(
                        "AdjusterSetState.ReferenceFraction.Mismatch",
                        "banks[" + index.ToString(CultureInfo.InvariantCulture) + "].reference_fraction",
                        "Every bank reference fraction must equal the admitted map reference state.");
                }

                if (bank.Mode == AdjusterMotionModeV1.RateLimited)
                {
                    if (bank.QueueState == null || bank.QueueState.OwnerBranchId != branchId)
                    {
                        return Invalid(
                            "AdjusterSetState.QueueOwner.Mismatch",
                            "banks[" + index.ToString(CultureInfo.InvariantCulture) + "].queue_state.owner_key",
                            "Every RateLimited bank queue must be owned by the enclosing typed branch.");
                    }
                }
            }

            Digest32 stateDigest = ComputeDigest(
                adjusterSetId,
                branchId,
                coreStateVersion,
                topologyVersion!,
                dataPackVersion!,
                map.MapId,
                map.MapDigest,
                grouping,
                canonical);
            if (expectedStateDigest != null && !expectedStateDigest.Equals(stateDigest))
            {
                return Invalid(
                    "AdjusterSetState.StateDigest.Mismatch",
                    "state_digest",
                    "The supplied set-state digest does not equal the canonical set-state body.");
            }

            return ContractValidationResult<AdjusterSetStateV1>.Valid(
                new AdjusterSetStateV1(
                    adjusterSetId,
                    branchId,
                    coreStateVersion,
                    topologyVersion!,
                    dataPackVersion!,
                    map.MapId,
                    map.MapDigest,
                    grouping,
                    canonical,
                    stateDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                AdjusterSetId,
                BranchId,
                CoreStateVersion,
                TopologyVersion,
                DataPackVersion,
                InfluenceMapId,
                InfluenceMapDigest,
                Grouping,
                Banks,
                StateDigest);
        }

        private static Digest32 ComputeDigest(
            StableId adjusterSetId,
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            StableId influenceMapId,
            Digest32 influenceMapDigest,
            AdjusterBankGroupingV1 grouping,
            IReadOnlyList<AdjusterBankStateV1> banks)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        adjusterSetId,
                        branchId,
                        coreStateVersion,
                        topologyVersion,
                        dataPackVersion,
                        influenceMapId,
                        influenceMapDigest,
                        grouping,
                        banks,
                        null)));
        }

        private static byte[] BuildBytes(
            StableId adjusterSetId,
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            StableId influenceMapId,
            Digest32 influenceMapDigest,
            AdjusterBankGroupingV1 grouping,
            IReadOnlyList<AdjusterBankStateV1> banks,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-ADJUSTER-SET-STATE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, adjusterSetId);
                Phase5CanonicalBytesV1.WriteStableId(writer, branchId);
                Phase5CanonicalBytesV1.WriteUInt64(writer, coreStateVersion);
                Phase5CanonicalBytesV1.WriteString(writer, topologyVersion);
                Phase5CanonicalBytesV1.WriteString(writer, dataPackVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                Phase5CanonicalBytesV1.WriteDigest(writer, influenceMapDigest);
                Phase5CanonicalBytesV1.WriteBytes(writer, grouping.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)banks.Count));
                foreach (AdjusterBankStateV1 bank in banks)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, bank.ToCanonicalBytes());
                }

                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static ContractValidationResult<AdjusterSetStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterSetStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Explicitly distinguishes a value that is not applicable to a mode from
    /// a numeric zero. This keeps Manual and Prescribed states from acquiring
    /// a hidden queue projection.
    /// </summary>
    public sealed class AdjusterOptionalDoubleV1 : IEquatable<AdjusterOptionalDoubleV1>
    {
        private AdjusterOptionalDoubleV1(bool isApplicable, double value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public bool IsApplicable { get; }

        public double Value { get; }

        public static AdjusterOptionalDoubleV1 NotApplicable
        {
            get { return new AdjusterOptionalDoubleV1(false, 0.0); }
        }

        internal static AdjusterOptionalDoubleV1 Applicable(double value)
        {
            return new AdjusterOptionalDoubleV1(true, value);
        }

        public bool Equals(AdjusterOptionalDoubleV1? other)
        {
            if (other == null)
            {
                return false;
            }

            return IsApplicable == other.IsApplicable &&
                   (!IsApplicable || Value == other.Value);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as AdjusterOptionalDoubleV1);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsApplicable, Value);
        }

        internal void WriteCanonicalBytes(BinaryWriter writer)
        {
            writer.Write(IsApplicable ? (byte)1 : (byte)0);
            if (IsApplicable)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, Value);
            }
        }
    }

    /// <summary>
    /// Pure, deterministic projection of one adjuster bank's delayed motion.
    /// The projection records queue inputs but never writes them back.
    /// </summary>
    public sealed class AdjusterMotionProjectionV1
    {
        public const uint CurrentSchemaVersion = 1;

        private AdjusterMotionProjectionV1(
            StableId bankId,
            AdjusterMotionModeV1 mode,
            bool enabled,
            bool commandDelaySatisfied,
            double stateFractionBefore,
            double stateFractionAfter,
            double appliedDeltaFraction,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            Digest32 sourceStateDigest,
            Digest32? queueDigest,
            double? availableCommandFraction,
            double? lastMotionTimeSeconds,
            Digest32 motionDigest)
        {
            BankId = bankId;
            Mode = mode;
            Enabled = enabled;
            CommandDelaySatisfied = commandDelaySatisfied;
            StateFractionBefore = stateFractionBefore;
            StateFractionAfter = stateFractionAfter;
            AppliedDeltaFraction = appliedDeltaFraction;
            CommandAvailableTimeSeconds = commandAvailableTimeSeconds;
            CurrentTimeSeconds = currentTimeSeconds;
            SourceStateDigest = sourceStateDigest;
            QueueDigest = queueDigest == null
                ? AdjusterOptionalDigestV1.NotApplicable
                : AdjusterOptionalDigestV1.Applicable(queueDigest);
            AvailableCommandFraction = availableCommandFraction == null
                ? AdjusterOptionalDoubleV1.NotApplicable
                : AdjusterOptionalDoubleV1.Applicable(availableCommandFraction.Value);
            LastMotionTimeSeconds = lastMotionTimeSeconds == null
                ? AdjusterOptionalDoubleV1.NotApplicable
                : AdjusterOptionalDoubleV1.Applicable(lastMotionTimeSeconds.Value);
            MotionDigest = motionDigest;
        }

        public StableId BankId { get; }

        public AdjusterMotionModeV1 Mode { get; }

        public bool Enabled { get; }

        public bool CommandDelaySatisfied { get; }

        public double StateFractionBefore { get; }

        public double StateFractionAfter { get; }

        public double AppliedDeltaFraction { get; }

        public double CommandAvailableTimeSeconds { get; }

        public double CurrentTimeSeconds { get; }

        public Digest32 SourceStateDigest { get; }

        public AdjusterOptionalDigestV1 QueueDigest { get; }

        public AdjusterOptionalDoubleV1 AvailableCommandFraction { get; }

        public AdjusterOptionalDoubleV1 LastMotionTimeSeconds { get; }

        public Digest32 MotionDigest { get; }

        internal static AdjusterMotionProjectionV1 Create(
            AdjusterBankStateV1 bank,
            bool commandDelaySatisfied,
            double stateFractionAfter,
            double appliedDeltaFraction,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            AdjusterQueueStateV1? queueState,
            double? availableCommandFraction)
        {
            Digest32? queueDigest = queueState == null ? null : queueState.QueueDigest;
            double? lastMotionTimeSeconds = queueState == null
                ? (double?)null
                : queueState.LastMotionTimeSeconds;
            Digest32 motionDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        bank.BankId,
                        bank.Mode,
                        bank.Enabled,
                        commandDelaySatisfied,
                        bank.StateFraction,
                        stateFractionAfter,
                        appliedDeltaFraction,
                        commandAvailableTimeSeconds,
                        currentTimeSeconds,
                        bank.StateDigest,
                        queueDigest,
                        availableCommandFraction,
                        lastMotionTimeSeconds,
                        null)));
            return new AdjusterMotionProjectionV1(
                bank.BankId,
                bank.Mode,
                bank.Enabled,
                commandDelaySatisfied,
                bank.StateFraction,
                stateFractionAfter,
                appliedDeltaFraction,
                commandAvailableTimeSeconds,
                currentTimeSeconds,
                bank.StateDigest,
                queueDigest,
                availableCommandFraction,
                lastMotionTimeSeconds,
                motionDigest);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                BankId,
                Mode,
                Enabled,
                CommandDelaySatisfied,
                StateFractionBefore,
                StateFractionAfter,
                AppliedDeltaFraction,
                CommandAvailableTimeSeconds,
                CurrentTimeSeconds,
                SourceStateDigest,
                QueueDigest.IsApplicable ? QueueDigest.Value : (Digest32?)null,
                AvailableCommandFraction.IsApplicable
                    ? AvailableCommandFraction.Value
                    : (double?)null,
                LastMotionTimeSeconds.IsApplicable
                    ? LastMotionTimeSeconds.Value
                    : (double?)null,
                MotionDigest);
        }

        private static byte[] BuildBytes(
            StableId bankId,
            AdjusterMotionModeV1 mode,
            bool enabled,
            bool commandDelaySatisfied,
            double stateFractionBefore,
            double stateFractionAfter,
            double appliedDeltaFraction,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds,
            Digest32 sourceStateDigest,
            Digest32? queueDigest,
            double? availableCommandFraction,
            double? lastMotionTimeSeconds,
            Digest32? motionDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    "CANDU-ADJUSTER-MOTION-PROJECTION-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, bankId);
                writer.Write((byte)mode);
                writer.Write(enabled ? (byte)1 : (byte)0);
                writer.Write(commandDelaySatisfied ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFractionBefore);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFractionAfter);
                Phase5CanonicalBytesV1.WriteDouble(writer, appliedDeltaFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, commandAvailableTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, currentTimeSeconds);
                Phase5CanonicalBytesV1.WriteDigest(writer, sourceStateDigest);

                writer.Write(queueDigest == null ? (byte)0 : (byte)1);
                if (queueDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, queueDigest);
                }

                writer.Write(availableCommandFraction == null ? (byte)0 : (byte)1);
                if (availableCommandFraction != null)
                {
                    Phase5CanonicalBytesV1.WriteDouble(writer, availableCommandFraction.Value);
                }

                writer.Write(lastMotionTimeSeconds == null ? (byte)0 : (byte)1);
                if (lastMotionTimeSeconds != null)
                {
                    Phase5CanonicalBytesV1.WriteDouble(writer, lastMotionTimeSeconds.Value);
                }

                if (motionDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, motionDigest);
                }
            });
        }
    }

    /// <summary>
    /// Pure P6-T03 adjuster motion evaluator. Its only queue interaction is
    /// reading the complete supplied queue projection from a validated bank.
    /// </summary>
    public static class AdjusterMotionV1
    {
        public static ContractValidationResult<AdjusterMotionProjectionV1> TryAdvance(
            AdjusterBankStateV1? bank,
            double commandAvailableTimeSeconds,
            double currentTimeSeconds)
        {
            if (bank == null)
            {
                return Invalid(
                    "AdjusterMotion.Bank.Missing",
                    "bank",
                    "A validated adjuster bank state is required.");
            }

            if (!AdjusterValidationV1.IsCanonicalNonnegative(commandAvailableTimeSeconds) ||
                !AdjusterValidationV1.IsCanonicalNonnegative(currentTimeSeconds))
            {
                return Invalid(
                    "AdjusterMotion.Time.Invalid",
                    "motion_time",
                    "Motion times must be finite, nonnegative SI seconds without signed zero.");
            }

            AdjusterQueueStateV1? queueState = bank.QueueState;
            double? availableCommandFraction = null;
            if (bank.Mode == AdjusterMotionModeV1.RateLimited)
            {
                if (queueState == null)
                {
                    return Invalid(
                        "AdjusterMotion.QueueState.Missing",
                        "queue_state",
                        "RateLimited motion requires the complete supplied queue state.");
                }

                if (currentTimeSeconds < queueState.LastMotionTimeSeconds)
                {
                    return Invalid(
                        "AdjusterMotion.Time.Order",
                        "current_time_s",
                        "Current event time may not precede the queue-projected last motion time.");
                }

                ContractValidationResult<AdjusterAvailableCommandV1> available =
                    queueState.TryGetAvailableCommand(bank.BankId);
                if (!available.IsValid)
                {
                    return Invalid(
                        "AdjusterMotion.AvailableCommand.Missing",
                        "queue_state.available_commands",
                        available.FirstDiagnostic.Message);
                }

                if (available.Value.RateLimitPerSecond != bank.RateLimitPerSecond ||
                    available.Value.LowerBound != 0.0 || available.Value.UpperBound != 1.0)
                {
                    return Invalid(
                        "AdjusterMotion.AvailableCommand.Mismatch",
                        "queue_state.available_commands",
                        "The complete queue projection must equal the bank rate and [0,1] bounds.");
                }

                availableCommandFraction = available.Value.BoundedFraction;
            }

            bool commandDelaySatisfied = currentTimeSeconds >= commandAvailableTimeSeconds;
            double stateAfter = bank.StateFraction;
            double appliedDelta = 0.0;

            if (commandDelaySatisfied &&
                bank.Enabled &&
                bank.Mode == AdjusterMotionModeV1.RateLimited)
            {
                double deltaTime = currentTimeSeconds - queueState!.LastMotionTimeSeconds;
                if (deltaTime > 0.0)
                {
                    double delta = availableCommandFraction!.Value - bank.StateFraction;
                    double maximumTravel = bank.RateLimitPerSecond * deltaTime;
                    if (!ContractValidation.IsFinite(delta) ||
                        !ContractValidation.IsFinite(maximumTravel))
                    {
                        return Invalid(
                            "AdjusterMotion.NonFinite",
                            "motion",
                            "Rate-limited motion fails closed on non-finite delta or travel.");
                    }

                    if (delta > 0.0)
                    {
                        appliedDelta = Math.Min(delta, maximumTravel);
                    }
                    else if (delta < 0.0)
                    {
                        appliedDelta = -Math.Min(-delta, maximumTravel);
                    }

                    if (appliedDelta == 0.0)
                    {
                        appliedDelta = 0.0;
                    }

                    stateAfter = bank.StateFraction + appliedDelta;
                    if (!AdjusterValidationV1.IsCanonicalFraction(stateAfter))
                    {
                        return Invalid(
                            "AdjusterMotion.StateAfter.Invalid",
                            "state_fraction_after",
                            "Rate-limited motion must remain a finite fraction in [0,1].");
                    }
                }
            }

            return ContractValidationResult<AdjusterMotionProjectionV1>.Valid(
                AdjusterMotionProjectionV1.Create(
                    bank,
                    commandDelaySatisfied,
                    stateAfter,
                    appliedDelta,
                    commandAvailableTimeSeconds,
                    currentTimeSeconds,
                    queueState,
                    availableCommandFraction));
        }

        private static ContractValidationResult<AdjusterMotionProjectionV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<AdjusterMotionProjectionV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Shared strict input checks for the synthetic adjuster contracts.
    /// </summary>
    internal static class AdjusterValidationV1
    {
        public static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalNonnegative(value) && value <= 1.0;
        }

        public static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        public static bool IsCanonicalPositive(double value)
        {
            return ContractValidation.IsFinite(value) && value > 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        public static bool IsCanonicalText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                _ = new UTF8Encoding(false, true).GetBytes(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Optional digest used by projections whose queue binding is not
    /// applicable. This is deliberately separate from a zero digest.
    /// </summary>
    public sealed class AdjusterOptionalDigestV1 : IEquatable<AdjusterOptionalDigestV1>
    {
        private AdjusterOptionalDigestV1(bool isApplicable, Digest32? value)
        {
            IsApplicable = isApplicable;
            Value = value;
        }

        public bool IsApplicable { get; }

        public Digest32? Value { get; }

        public static AdjusterOptionalDigestV1 NotApplicable
        {
            get { return new AdjusterOptionalDigestV1(false, null); }
        }

        internal static AdjusterOptionalDigestV1 Applicable(Digest32 value)
        {
            return new AdjusterOptionalDigestV1(true, value);
        }

        public bool Equals(AdjusterOptionalDigestV1? other)
        {
            if (other == null)
            {
                return false;
            }

            return IsApplicable == other.IsApplicable &&
                   (!IsApplicable || (Value != null && other.Value != null && Value.Equals(other.Value)));
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as AdjusterOptionalDigestV1);
        }

        public override int GetHashCode()
        {
            return IsApplicable && Value != null ? Value.GetHashCode() : 0;
        }
    }
}
