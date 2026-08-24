using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    internal static class CommandClockValidation
    {
        public static bool IsCanonicalNonnegativeFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   value >= 0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }
    }

    /// <summary>
    /// Explicit simulation time for the headless runtime. Wall-clock time and
    /// Unity frame timing are never inputs. The caller supplies every advance
    /// interval or target boundary; this contract does not execute a model.
    /// </summary>
    public sealed class SimulationClockV1
    {
        public const uint CurrentSchemaVersion = 1;

        private SimulationClockV1(
            uint schemaVersion,
            double currentSimulationTimeSeconds,
            ulong stepIndex)
        {
            SchemaVersion = schemaVersion;
            CurrentSimulationTimeSeconds = currentSimulationTimeSeconds;
            StepIndex = stepIndex;
        }

        public uint SchemaVersion { get; }

        public double CurrentSimulationTimeSeconds { get; }

        public ulong StepIndex { get; }

        public static ContractValidationResult<SimulationClockV1> TryCreate(
            uint schemaVersion,
            double currentSimulationTimeSeconds,
            ulong stepIndex)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only simulation clock schema version 1 is supported.");
            }

            if (!CommandClockValidation.IsCanonicalNonnegativeFinite(currentSimulationTimeSeconds))
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.Time.Invalid",
                    "current_simulation_time_s",
                    "Current simulation time must be finite and nonnegative SI seconds.");
            }

            return ContractValidationResult<SimulationClockV1>.Valid(
                new SimulationClockV1(schemaVersion, currentSimulationTimeSeconds, stepIndex));
        }

        public ContractValidationResult<SimulationClockV1> TryAdvanceBy(
            double deltaTimeSeconds)
        {
            if (!ContractValidation.IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.Advance.Interval.Invalid",
                    "delta_time_s",
                    "An explicit clock interval must be finite and strictly positive SI seconds.");
            }

            if (StepIndex == ulong.MaxValue)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.StepIndex.Overflow",
                    "step_index",
                    "The explicit simulation step index cannot advance past UInt64.MaxValue.");
            }

            double nextTime = CurrentSimulationTimeSeconds + deltaTimeSeconds;
            if (!ContractValidation.IsFinite(nextTime))
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.Time.Overflow",
                    "current_simulation_time_s",
                    "The explicit time addition produced a non-finite simulation time.");
            }

            if (nextTime <= CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.Time.NoProgress",
                    "current_simulation_time_s",
                    "The explicit interval must advance representable simulation time.");
            }

            return ContractValidationResult<SimulationClockV1>.Valid(
                new SimulationClockV1(SchemaVersion, nextTime, StepIndex + 1));
        }

        public ContractValidationResult<SimulationClockV1> TryAdvanceTo(
            double targetSimulationTimeSeconds)
        {
            if (!CommandClockValidation.IsCanonicalNonnegativeFinite(targetSimulationTimeSeconds))
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.TargetTime.Invalid",
                    "target_simulation_time_s",
                    "The target simulation time must be finite and nonnegative SI seconds.");
            }

            if (targetSimulationTimeSeconds < CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.TargetTime.Backward",
                    "target_simulation_time_s",
                    "The explicit simulation clock cannot move backward.");
            }

            if (targetSimulationTimeSeconds == CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<SimulationClockV1>.Valid(this);
            }

            if (StepIndex == ulong.MaxValue)
            {
                return ContractValidationResult<SimulationClockV1>.Invalid(
                    "SimulationClock.StepIndex.Overflow",
                    "step_index",
                    "The explicit simulation step index cannot advance past UInt64.MaxValue.");
            }

            return ContractValidationResult<SimulationClockV1>.Valid(
                new SimulationClockV1(
                    SchemaVersion,
                    targetSimulationTimeSeconds,
                    StepIndex + 1));
        }
    }

    /// <summary>
    /// A state-bound, not-yet-executed command envelope. The body remains the
    /// approved event-body contract; named command bodies are added by their
    /// own bounded tasks. This type only records scheduling and ordering data.
    /// </summary>
    public sealed class SimulationCommandV1
    {
        private SimulationCommandV1(
            EventRankV1 eventRank,
            ulong sequence,
            StableId commandId,
            EventOwnerV1 owner,
            EventBodyV1 body,
            double enqueueTimeSeconds,
            double dueTimeSeconds,
            StateBindingV1 stateBinding)
        {
            EventRank = eventRank;
            Sequence = sequence;
            CommandId = commandId;
            Owner = owner;
            Body = body;
            EnqueueTimeSeconds = enqueueTimeSeconds;
            DueTimeSeconds = dueTimeSeconds;
            StateBinding = stateBinding;
        }

        public EventRankV1 EventRank { get; }

        public ulong Sequence { get; }

        public StableId CommandId { get; }

        public EventOwnerV1 Owner { get; }

        public EventBodyV1 Body { get; }

        public double EnqueueTimeSeconds { get; }

        public double DueTimeSeconds { get; }

        public StateBindingV1 StateBinding { get; }

        public static ContractValidationResult<SimulationCommandV1> TryCreate(
            EventRankV1 eventRank,
            ulong sequence,
            StableId commandId,
            EventOwnerV1 owner,
            EventBodyV1 body,
            double enqueueTimeSeconds,
            double dueTimeSeconds,
            StateBindingV1 stateBinding)
        {
            if (!Enum.IsDefined(typeof(EventRankV1), eventRank))
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.EventRank.Invalid",
                    "event_rank",
                    "The command event rank is not part of the approved closed enum.");
            }

            if (commandId.IsEmpty)
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.Id.Empty",
                    "command_id",
                    "A command requires an explicit stable identity.");
            }

            if (owner == null)
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.Owner.Missing",
                    "owner",
                    "A command owner key is required.");
            }

            if (body == null)
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.Body.Missing",
                    "body",
                    "A command body is required.");
            }

            if (!CommandClockValidation.IsCanonicalNonnegativeFinite(enqueueTimeSeconds))
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.EnqueueTime.Invalid",
                    "enqueue_time_s",
                    "Command enqueue time must be finite and nonnegative SI seconds.");
            }

            if (!CommandClockValidation.IsCanonicalNonnegativeFinite(dueTimeSeconds))
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.DueTime.Invalid",
                    "due_time_s",
                    "Command due time must be finite and nonnegative SI seconds.");
            }

            if (dueTimeSeconds < enqueueTimeSeconds)
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.DueTime.BeforeEnqueue",
                    "due_time_s",
                    "A command due time may not precede its enqueue time.");
            }

            if (stateBinding == null)
            {
                return ContractValidationResult<SimulationCommandV1>.Invalid(
                    "SimulationCommand.StateBinding.Missing",
                    "state_binding",
                    "A command must bind the explicit lifecycle state tuple.");
            }

            return ContractValidationResult<SimulationCommandV1>.Valid(
                new SimulationCommandV1(
                    eventRank,
                    sequence,
                    commandId,
                    owner,
                    body,
                    enqueueTimeSeconds,
                    dueTimeSeconds,
                    stateBinding));
        }

        internal int CompareCanonical(SimulationCommandV1 other)
        {
            int dueTimeComparison = DueTimeSeconds.CompareTo(other.DueTimeSeconds);
            if (dueTimeComparison != 0)
            {
                return dueTimeComparison;
            }

            int rankComparison = EventRank.CompareTo(other.EventRank);
            if (rankComparison != 0)
            {
                return rankComparison;
            }

            int sequenceComparison = Sequence.CompareTo(other.Sequence);
            return sequenceComparison != 0
                ? sequenceComparison
                : CommandId.CompareTo(other.CommandId);
        }
    }

    /// <summary>
    /// The result of releasing commands due at one explicit clock boundary.
    /// Released commands are returned in canonical queue order and are not
    /// executed by this contract.
    /// </summary>
    public sealed class CommandQueueReleaseV1
    {
        internal CommandQueueReleaseV1(
            CommandQueueV1 queue,
            IReadOnlyList<SimulationCommandV1> releasedCommands)
        {
            Queue = queue;
            ReleasedCommands = new ReadOnlyCollection<SimulationCommandV1>(
                releasedCommands.ToArray());
        }

        public CommandQueueV1 Queue { get; }

        public IReadOnlyList<SimulationCommandV1> ReleasedCommands { get; }
    }

    /// <summary>
    /// Immutable deterministic pending-command queue. Sequence allocation and
    /// due release are the only transitions here; command execution, physics,
    /// refuelling, and replay codecs belong to later bounded tasks.
    /// </summary>
    public sealed class CommandQueueV1
    {
        private readonly ReadOnlyCollection<StableId> _allocatedCommandIds;
        private readonly ReadOnlyCollection<SimulationCommandV1> _pendingCommands;

        private CommandQueueV1(
            ulong initialNextSequence,
            ulong nextSequence,
            double currentSimulationTimeSeconds,
            ulong currentStepIndex,
            IReadOnlyList<StableId> allocatedCommandIds,
            IReadOnlyList<SimulationCommandV1> pendingCommands)
        {
            InitialNextSequence = initialNextSequence;
            NextSequence = nextSequence;
            CurrentSimulationTimeSeconds = currentSimulationTimeSeconds;
            CurrentStepIndex = currentStepIndex;
            _allocatedCommandIds = new ReadOnlyCollection<StableId>(
                allocatedCommandIds.ToArray());
            _pendingCommands = new ReadOnlyCollection<SimulationCommandV1>(
                pendingCommands.ToArray());
        }

        public ulong InitialNextSequence { get; }

        public ulong NextSequence { get; }

        public double CurrentSimulationTimeSeconds { get; }

        public ulong CurrentStepIndex { get; }

        public IReadOnlyList<StableId> AllocatedCommandIds
        {
            get { return _allocatedCommandIds; }
        }

        public IReadOnlyList<SimulationCommandV1> PendingCommands
        {
            get { return _pendingCommands; }
        }

        public double? NextDueTimeSeconds
        {
            get { return _pendingCommands.Count == 0 ? (double?)null : _pendingCommands[0].DueTimeSeconds; }
        }

        public static ContractValidationResult<CommandQueueV1> TryCreate(
            ulong initialNextSequence,
            ulong nextSequence,
            IEnumerable<StableId> allocatedCommandIds,
            IEnumerable<SimulationCommandV1> pendingCommands,
            SimulationClockV1 clock)
        {
            if (allocatedCommandIds == null)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.AllocatedIds.Missing",
                    "allocated_command_ids",
                    "The allocated-command identity registry is required.");
            }

            if (pendingCommands == null)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.PendingCommands.Missing",
                    "pending_commands",
                    "The pending-command collection is required.");
            }

            if (clock == null)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Clock.Missing",
                    "clock",
                    "An explicit simulation clock is required for queue validation.");
            }

            if (nextSequence < initialNextSequence)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Sequence.Range.Invalid",
                    "next_sequence",
                    "NextSequence may not precede InitialNextSequence.");
            }

            StableId[] canonicalIds = allocatedCommandIds
                .OrderBy(id => id)
                .ToArray();
            for (int i = 0; i < canonicalIds.Length; i++)
            {
                if (canonicalIds[i].IsEmpty)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.CommandId.Empty",
                        "allocated_command_ids[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "An allocated-command identity may not be empty.");
                }
            }

            for (int i = 1; i < canonicalIds.Length; i++)
            {
                if (canonicalIds[i - 1] == canonicalIds[i])
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.CommandId.Duplicate",
                        "allocated_command_ids",
                        "Allocated-command identities must be unique.");
                }
            }

            ulong allocatedCount = nextSequence - initialNextSequence;
            if (allocatedCount != (ulong)canonicalIds.Length)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Sequence.CountMismatch",
                    "allocated_command_ids",
                    "The allocated-command count must equal NextSequence minus InitialNextSequence.");
            }

            List<SimulationCommandV1> pending = pendingCommands.ToList();
            if (pending.Any(command => command == null))
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.PendingCommand.Null",
                    "pending_commands",
                    "A pending command may not be null.");
            }

            SimulationCommandV1[] canonicalPending = pending
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray();

            SimulationCommandV1[] pendingById = pending
                .OrderBy(command => command.CommandId)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.DueTimeSeconds)
                .ToArray();
            for (int i = 1; i < pendingById.Length; i++)
            {
                if (pendingById[i - 1].CommandId == pendingById[i].CommandId)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.PendingCommand.Duplicate",
                        "pending_commands",
                        "A command identity may occur only once in the pending queue.");
                }
            }

            SimulationCommandV1[] pendingBySequence = pending
                .OrderBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ThenBy(command => command.DueTimeSeconds)
                .ToArray();
            for (int i = 1; i < pendingBySequence.Length; i++)
            {
                if (pendingBySequence[i - 1].Sequence == pendingBySequence[i].Sequence)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.Sequence.Duplicate",
                        "pending_commands",
                        "A sequence may occur only once in the pending queue.");
                }
            }

            for (int i = 0; i < canonicalPending.Length; i++)
            {
                SimulationCommandV1 command = canonicalPending[i];
                if (command.Sequence < initialNextSequence || command.Sequence >= nextSequence)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.Sequence.OutOfRange",
                        "pending_commands[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].sequence",
                        "A pending command sequence must lie in the allocated half-open sequence range.");
                }

                if (Array.BinarySearch(canonicalIds, command.CommandId) < 0)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.CommandId.NotAllocated",
                        "pending_commands[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].command_id",
                        "Every pending command identity must be present in the allocated-command registry.");
                }

                if (command.EnqueueTimeSeconds > clock.CurrentSimulationTimeSeconds)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.EnqueueTime.Future",
                        "pending_commands[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].enqueue_time_s",
                        "A pending command may not be enqueued in the future of the supplied clock.");
                }

                if (command.DueTimeSeconds < clock.CurrentSimulationTimeSeconds)
                {
                    return ContractValidationResult<CommandQueueV1>.Invalid(
                        "CommandQueue.DueTime.Stale",
                        "pending_commands[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].due_time_s",
                        "A pending command may not have a due time earlier than the supplied clock.");
                }

            }

            return ContractValidationResult<CommandQueueV1>.Valid(
                new CommandQueueV1(
                    initialNextSequence,
                    nextSequence,
                    clock.CurrentSimulationTimeSeconds,
                    clock.StepIndex,
                    canonicalIds,
                    canonicalPending));
        }

        public ContractValidationResult<CommandQueueV1> TryEnqueue(
            SimulationClockV1 clock,
            SimulationCommandV1 command)
        {
            if (clock == null)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Clock.Missing",
                    "clock",
                    "An explicit simulation clock is required for enqueue.");
            }

            if (command == null)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Command.Null",
                    "command",
                    "A command is required for enqueue.");
            }

            if (clock.CurrentSimulationTimeSeconds != CurrentSimulationTimeSeconds ||
                clock.StepIndex != CurrentStepIndex)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Clock.BoundaryMismatch",
                    "clock",
                    "The enqueue clock must represent the queue's exact immutable time and step boundary.");
            }

            if (command.EnqueueTimeSeconds != clock.CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.EnqueueTime.Stale",
                    "command.enqueue_time_s",
                    "A newly enqueued command must bind the exact supplied clock time.");
            }

            if (NextSequence == ulong.MaxValue)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Sequence.Overflow",
                    "next_sequence",
                    "UInt64.MaxValue cannot be allocated because no next-unused sequence would remain.");
            }

            if (command.Sequence != NextSequence)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.Sequence.Expected",
                    "command.sequence",
                    "A newly enqueued command must use the queue's explicit next-unused sequence.");
            }

            if (AllocatedCommandIds.Count >= int.MaxValue)
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.AllocatedIds.Capacity",
                    "allocated_command_ids",
                    "The UInt32-counted allocated-command registry is full.");
            }

            if (AllocatedCommandIds.Contains(command.CommandId))
            {
                return ContractValidationResult<CommandQueueV1>.Invalid(
                    "CommandQueue.CommandId.Duplicate",
                    "command.command_id",
                    "A command identity may be allocated only once.");
            }

            return TryCreate(
                InitialNextSequence,
                NextSequence + 1,
                AllocatedCommandIds.Concat(new[] { command.CommandId }),
                PendingCommands.Concat(new[] { command }),
                clock);
        }

        public ContractValidationResult<CommandQueueReleaseV1> TryReleaseDue(
            SimulationClockV1 clock)
        {
            if (clock == null)
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    "CommandQueue.Clock.Missing",
                    "clock",
                    "An explicit simulation clock is required for due release.");
            }

            if (clock.CurrentSimulationTimeSeconds < CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    "CommandQueue.Clock.Backward",
                    "clock.current_simulation_time_s",
                    "A queue may not be released against a clock earlier than its immutable boundary.");
            }

            if (clock.StepIndex < CurrentStepIndex)
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    "CommandQueue.Clock.StepBackward",
                    "clock.step_index",
                    "A queue may not be released against a clock earlier than its immutable step boundary.");
            }

            if ((clock.CurrentSimulationTimeSeconds == CurrentSimulationTimeSeconds) !=
                (clock.StepIndex == CurrentStepIndex))
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    "CommandQueue.Clock.BoundaryMismatch",
                    "clock",
                    "The release clock must advance time and step index as one explicit boundary.");
            }

            List<SimulationCommandV1> released = new List<SimulationCommandV1>();
            int firstFutureIndex = 0;
            while (firstFutureIndex < _pendingCommands.Count &&
                   _pendingCommands[firstFutureIndex].DueTimeSeconds < clock.CurrentSimulationTimeSeconds)
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    "CommandQueue.DueTime.Missed",
                    "pending_commands[" + firstFutureIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].due_time_s",
                    "The caller must split the explicit advance at every due-time boundary.");
            }

            while (firstFutureIndex < _pendingCommands.Count &&
                   _pendingCommands[firstFutureIndex].DueTimeSeconds == clock.CurrentSimulationTimeSeconds)
            {
                released.Add(_pendingCommands[firstFutureIndex]);
                firstFutureIndex++;
            }

            if (released.Count == 0)
            {
                if (clock.CurrentSimulationTimeSeconds == CurrentSimulationTimeSeconds &&
                    clock.StepIndex == CurrentStepIndex)
                {
                    return ContractValidationResult<CommandQueueReleaseV1>.Valid(
                        new CommandQueueReleaseV1(this, Array.Empty<SimulationCommandV1>()));
                }

                ContractValidationResult<CommandQueueV1> advancedQueue = TryCreate(
                    InitialNextSequence,
                    NextSequence,
                    AllocatedCommandIds,
                    _pendingCommands,
                    clock);
                if (!advancedQueue.IsValid)
                {
                    return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                        advancedQueue.FirstDiagnostic.Code,
                        advancedQueue.FirstDiagnostic.Path,
                        advancedQueue.FirstDiagnostic.Message);
                }

                return ContractValidationResult<CommandQueueReleaseV1>.Valid(
                    new CommandQueueReleaseV1(advancedQueue.Value, Array.Empty<SimulationCommandV1>()));
            }

            SimulationCommandV1[] remaining = _pendingCommands
                .Skip(firstFutureIndex)
                .ToArray();
            ContractValidationResult<CommandQueueV1> nextQueue = TryCreate(
                InitialNextSequence,
                NextSequence,
                AllocatedCommandIds,
                remaining,
                clock);
            if (!nextQueue.IsValid)
            {
                return ContractValidationResult<CommandQueueReleaseV1>.Invalid(
                    nextQueue.FirstDiagnostic.Code,
                    nextQueue.FirstDiagnostic.Path,
                    nextQueue.FirstDiagnostic.Message);
            }

            return ContractValidationResult<CommandQueueReleaseV1>.Valid(
                new CommandQueueReleaseV1(nextQueue.Value, released));
        }
    }
}
