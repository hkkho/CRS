using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The immutable result of one due-command application transaction. The
    /// transaction commits only the queue boundary and event envelopes; named
    /// command bodies own their later state transitions.
    /// </summary>
    public sealed class CommandApplicationResultV1
    {
        internal CommandApplicationResultV1(
            CommandQueueV1 finalQueue,
            EventLogV1 finalEventLog,
            IReadOnlyList<EventRecordV1> appliedEvents)
        {
            FinalQueue = finalQueue;
            FinalEventLog = finalEventLog;
            AppliedEvents = new ReadOnlyCollection<EventRecordV1>(appliedEvents.ToArray());
        }

        public CommandQueueV1 FinalQueue { get; }

        public EventLogV1 FinalEventLog { get; }

        public IReadOnlyList<EventRecordV1> AppliedEvents { get; }
    }

    /// <summary>
    /// Applies every command released at one explicit clock boundary as one
    /// immutable event-envelope transaction. No physics, refuelling, solver,
    /// depletion, replay, or Unity behavior is executed here.
    /// </summary>
    public static class CommandApplicationV1
    {
        public static ContractValidationResult<CommandApplicationResultV1> TryApply(
            CommandQueueV1 queue,
            EventLogV1 eventLog,
            SimulationClockV1 clock,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            if (queue == null)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    "CommandApplication.Queue.Missing",
                    "queue",
                    "An immutable command queue is required for application.");
            }

            if (eventLog == null)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    "CommandApplication.EventLog.Missing",
                    "event_log",
                    "An immutable event log is required for application.");
            }

            if (clock == null)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    "CommandApplication.Clock.Missing",
                    "clock",
                    "An explicit simulation clock is required for application.");
            }

            if (configuration == null)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    "CommandApplication.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required for application.");
            }

            if (expectedStateBinding == null)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    "CommandApplication.StateBinding.Missing",
                    "expected_state_binding",
                    "The application requires the current immutable state-binding authority.");
            }

            ContractValidationResult<bool> configurationValidation = ValidateConfiguration(
                configuration,
                expectedStateBinding);
            if (!configurationValidation.IsValid)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    configurationValidation.FirstDiagnostic.Code,
                    configurationValidation.FirstDiagnostic.Path,
                    configurationValidation.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> queueValidation = ValidatePendingCommands(
                queue,
                configuration,
                expectedStateBinding);
            if (!queueValidation.IsValid)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    queueValidation.FirstDiagnostic.Code,
                    queueValidation.FirstDiagnostic.Path,
                    queueValidation.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> eventLogValidation = ValidateEventLog(
                eventLog,
                clock,
                configuration,
                expectedStateBinding);
            if (!eventLogValidation.IsValid)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    eventLogValidation.FirstDiagnostic.Code,
                    eventLogValidation.FirstDiagnostic.Path,
                    eventLogValidation.FirstDiagnostic.Message);
            }

            ContractValidationResult<CommandQueueReleaseV1> release = queue.TryReleaseDue(clock);
            if (!release.IsValid)
            {
                return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                    release.FirstDiagnostic.Code,
                    release.FirstDiagnostic.Path,
                    release.FirstDiagnostic.Message);
            }

            var appliedEvents = new List<EventRecordV1>(release.Value.ReleasedCommands.Count);
            foreach (SimulationCommandV1 command in release.Value.ReleasedCommands)
            {
                ContractValidationResult<EventRecordV1> record = EventRecordV1.TryCreate(
                    command.EventRank,
                    command.Sequence,
                    command.CommandId,
                    command.Owner,
                    command.Body,
                    clock.CurrentSimulationTimeSeconds,
                    command.StateBinding,
                    CommitStatusV1.Committed,
                    null);
                if (!record.IsValid)
                {
                    return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                        record.FirstDiagnostic.Code,
                        "released_commands[" + appliedEvents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]." + record.FirstDiagnostic.Path,
                        record.FirstDiagnostic.Message);
                }

                appliedEvents.Add(record.Value);
            }

            EventLogV1 nextEventLog = eventLog;
            foreach (EventRecordV1 record in appliedEvents)
            {
                ContractValidationResult<EventLogV1> appended = nextEventLog.TryAppend(record);
                if (!appended.IsValid)
                {
                    return ContractValidationResult<CommandApplicationResultV1>.Invalid(
                        appended.FirstDiagnostic.Code,
                        appended.FirstDiagnostic.Path,
                        appended.FirstDiagnostic.Message);
                }

                nextEventLog = appended.Value;
            }

            return ContractValidationResult<CommandApplicationResultV1>.Valid(
                new CommandApplicationResultV1(
                    release.Value.Queue,
                    nextEventLog,
                    appliedEvents));
        }

        private static ContractValidationResult<bool> ValidateConfiguration(
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            if (configuration.SchemaVersion != SimulationConfiguration.CurrentSchemaVersion ||
                configuration.Topology == null || configuration.DataPack == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.Configuration.Invalid",
                    "configuration",
                    "The application requires the current validated configuration schema and authorities.");
            }

            ContractValidationResult<bool> compatibility = configuration.DataPack.ValidateCompatibility(
                configuration.Topology);
            if (!compatibility.IsValid)
            {
                return ContractValidationResult<bool>.Invalid(
                    compatibility.FirstDiagnostic.Code,
                    "configuration." + compatibility.FirstDiagnostic.Path,
                    compatibility.FirstDiagnostic.Message);
            }

            if (string.IsNullOrWhiteSpace(expectedStateBinding.TopologyVersion) ||
                string.IsNullOrWhiteSpace(expectedStateBinding.DataPackVersion) ||
                !string.Equals(
                    expectedStateBinding.TopologyVersion,
                    configuration.DataPack.TopologySchemaId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    expectedStateBinding.DataPackVersion,
                    configuration.DataPack.DataPackVersion,
                    StringComparison.Ordinal))
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.StateBinding.ProvenanceMismatch",
                    "expected_state_binding",
                    "The state-binding authority must match the validated configuration provenance.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidatePendingCommands(
            CommandQueueV1 queue,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            for (int i = 0; i < queue.PendingCommands.Count; i++)
            {
                SimulationCommandV1 command = queue.PendingCommands[i];
                string path = "pending_commands[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (command == null)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.Command.Null",
                        path,
                        "A pending command may not be null.");
                }

                if (!BindingsEqual(command.StateBinding, expectedStateBinding))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.StateBinding.Mismatch",
                        path + ".state_binding",
                        "Every pending command must bind the supplied immutable state authority.");
                }

                ContractValidationResult<bool> bodyValidation = ValidateBody(
                    command.Body,
                    path + ".body",
                    command.DueTimeSeconds,
                    null);
                if (!bodyValidation.IsValid)
                {
                    return ContractValidationResult<bool>.Invalid(
                        bodyValidation.FirstDiagnostic.Code,
                        bodyValidation.FirstDiagnostic.Path,
                        bodyValidation.FirstDiagnostic.Message);
                }

                ContractValidationResult<bool> ownerValidation = ValidateOwner(
                    command.Owner,
                    configuration,
                    path + ".owner");
                if (!ownerValidation.IsValid)
                {
                    return ContractValidationResult<bool>.Invalid(
                        ownerValidation.FirstDiagnostic.Code,
                        ownerValidation.FirstDiagnostic.Path,
                        ownerValidation.FirstDiagnostic.Message);
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidateEventLog(
            EventLogV1 eventLog,
            SimulationClockV1 clock,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            for (int i = 0; i < eventLog.Records.Count; i++)
            {
                EventRecordV1 record = eventLog.Records[i];
                string path = "event_log.records[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (record == null)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.EventLog.Record.Null",
                        path,
                        "An existing event log record may not be null.");
                }

                if (record.SimulationTimeSeconds > clock.CurrentSimulationTimeSeconds)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.EventLog.Future",
                        path + ".simulation_time_s",
                        "An event log may not contain an event later than the application boundary.");
                }

                if (record.StateBinding == null ||
                    !string.Equals(record.StateBinding.TopologyVersion, configuration.DataPack.TopologySchemaId, StringComparison.Ordinal) ||
                    !string.Equals(record.StateBinding.DataPackVersion, configuration.DataPack.DataPackVersion, StringComparison.Ordinal))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.EventLog.StateBinding.ProvenanceMismatch",
                        path + ".state_binding",
                        "Every existing event must use the validated configuration provenance.");
                }

                if (record.StateBinding.CoreStateVersion > expectedStateBinding.CoreStateVersion)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandApplication.EventLog.StateBinding.Future",
                        path + ".state_binding.core_state_version",
                        "An existing event may not bind a core state version later than the application authority.");
                }

                ContractValidationResult<bool> bodyValidation = ValidateBody(
                    record.Body,
                    path + ".body",
                    record.SimulationTimeSeconds,
                    record.CommitStatus);
                if (!bodyValidation.IsValid)
                {
                    return bodyValidation;
                }

                ContractValidationResult<bool> ownerValidation = ValidateOwner(
                    record.Owner,
                    configuration,
                    path + ".owner");
                if (!ownerValidation.IsValid)
                {
                    return ownerValidation;
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidateBody(
            EventBodyV1 body,
            string path,
            double? expectedSimulationTimeSeconds,
            CommitStatusV1? expectedCommitStatus)
        {
            if (body == null || !Enum.IsDefined(typeof(EventBodyKindV1), body.Kind) ||
                string.IsNullOrWhiteSpace(body.SchemaId) || !body.IsConsistent)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.Body.Invalid",
                    path,
                    "A command or event body must use an approved closed discriminant and schema identity.");
            }

            if (expectedSimulationTimeSeconds.HasValue && expectedCommitStatus.HasValue &&
                !body.IsCompatibleWithEnvelope(expectedSimulationTimeSeconds.Value, expectedCommitStatus.Value))
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.Body.EnvelopeMismatch",
                    path,
                    "The typed refuelling body must agree with its command/event envelope.");
            }

            if (expectedSimulationTimeSeconds.HasValue && !expectedCommitStatus.HasValue &&
                body.Payload is RefuelMappingBodyV1 mapping &&
                mapping.EffectiveTimeSeconds != expectedSimulationTimeSeconds.Value)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.Body.TimeMismatch",
                    path,
                    "A refuelling mapping command must use its exact due time.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidateOwner(
            EventOwnerV1 owner,
            SimulationConfiguration configuration,
            string path)
        {
            if (owner == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandApplication.Owner.Missing",
                    path,
                    "A command or event owner is required, including explicit NotApplicable.");
            }

            ContractValidationResult<EventOwnerV1> ownerShape = EventOwnerV1.TryRestoreSerialized(
                owner.Kind,
                owner.ChannelId,
                owner.KeyBytes.ToArray());
            if (!ownerShape.IsValid)
            {
                return ContractValidationResult<bool>.Invalid(
                    ownerShape.FirstDiagnostic.Code,
                    path,
                    ownerShape.FirstDiagnostic.Message);
            }

            if (ownerShape.Value.Kind == EventOwnerKindV1.Channel)
            {
                ContractValidationResult<EventOwnerV1> channelOwner = EventOwnerV1.TryForChannel(
                    configuration.Topology,
                    ownerShape.Value.ChannelId);
                if (!channelOwner.IsValid)
                {
                    return ContractValidationResult<bool>.Invalid(
                        channelOwner.FirstDiagnostic.Code,
                        path,
                        channelOwner.FirstDiagnostic.Message);
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static bool BindingsEqual(StateBindingV1 left, StateBindingV1 right)
        {
            return left != null && right != null &&
                   left.CoreStateVersion == right.CoreStateVersion &&
                   left.SpatialStateVersion.Equals(right.SpatialStateVersion) &&
                   left.SpatialSolveId.Equals(right.SpatialSolveId) &&
                   left.PowerSnapshotId.Equals(right.PowerSnapshotId) &&
                   left.PowerSnapshotVersion.Equals(right.PowerSnapshotVersion) &&
                   left.KineticStepIndex.Equals(right.KineticStepIndex) &&
                   left.NuclideStateVersion.Equals(right.NuclideStateVersion) &&
                   string.Equals(left.TopologyVersion, right.TopologyVersion, StringComparison.Ordinal) &&
                   string.Equals(left.DataPackVersion, right.DataPackVersion, StringComparison.Ordinal) &&
                   left.CoefficientDigest.Equals(right.CoefficientDigest) &&
                   left.SnapshotDigest.Equals(right.SnapshotDigest);
        }
    }
}
