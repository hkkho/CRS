using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The closed P2-T04/P2-T05 event rank table. The rank is explicit and is
    /// never inferred from collection or array order.
    /// </summary>
    public enum EventRankV1 : ushort
    {
        Burnup = 0,
        Refuelling = 1,
        ControllerCommandGeneration = 2,
        ActuatorMotion = 3,
        BranchUpdate = 4,
        SpatialSolve = 5,
        KineticNuclideStep = 6
    }

    public enum EventOwnerKindV1 : byte
    {
        Channel = 0,
        Bundle = 1,
        Snapshot = 2,
        Actuator = 3,
        Branch = 4,
        Controller = 5,
        Queue = 6,
        NotApplicable = 255
    }

    /// <summary>
    /// The currently implemented event-body projection is the explicit
    /// NotApplicable body. Named refuelling, burnup, and command bodies remain
    /// owned by their later bounded Phase 3 tasks.
    /// </summary>
    public enum EventBodyKindV1 : byte
    {
        RefuelMapping = 0,
        RefuelAtomicity = 1,
        ActuatorCommand = 2,
        ActuatorTransition = 3,
        Saturation = 4,
        Queue = 5,
        BranchUpdate = 6,
        Burnup = 7,
        NotApplicable = 255
    }

    public enum CommitStatusV1 : byte
    {
        Committed = 0,
        RolledBack = 1,
        Rejected = 2,
        NotApplicable = 255
    }

    public enum DiagnosticSeverityV1 : byte
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Fatal = 3
    }

    /// <summary>
    /// Typed owner key for the P2-T05 Event scope. Channel owners use the
    /// explicit ChannelId field; the other owner kinds use copied opaque key
    /// bytes. This is an in-memory contract, not a codec.
    /// </summary>
    public sealed class EventOwnerV1
    {
        private readonly ReadOnlyCollection<byte> _keyBytes;

        private EventOwnerV1(EventOwnerKindV1 kind, ChannelId channelId, byte[] keyBytes)
        {
            Kind = kind;
            ChannelId = channelId;
            _keyBytes = new ReadOnlyCollection<byte>((byte[])keyBytes.Clone());
        }

        public static EventOwnerV1 NotApplicable
        {
            get { return new EventOwnerV1(EventOwnerKindV1.NotApplicable, new ChannelId(0), Array.Empty<byte>()); }
        }

        public EventOwnerKindV1 Kind { get; }

        public ChannelId ChannelId { get; }

        public IReadOnlyList<byte> KeyBytes
        {
            get { return _keyBytes; }
        }

        public static ContractValidationResult<EventOwnerV1> TryForChannel(
            CoreTopology topology,
            ChannelId channelId)
        {
            if (topology == null)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Topology.Missing",
                    "topology",
                    "A topology is required to validate a channel owner.");
            }

            if (channelId.Value >= topology.ChannelCount)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Channel.OutOfRange",
                    "channel_id",
                    "The channel owner is outside the validated topology.");
            }

            return ContractValidationResult<EventOwnerV1>.Valid(
                new EventOwnerV1(EventOwnerKindV1.Channel, channelId, Array.Empty<byte>()));
        }

        public static ContractValidationResult<EventOwnerV1> TryForStableId(
            EventOwnerKindV1 kind,
            StableId ownerId)
        {
            if (!Enum.IsDefined(typeof(EventOwnerKindV1), kind))
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Kind.Invalid",
                    "owner_kind",
                    "The event owner kind is not part of the approved closed enum.");
            }

            if (kind == EventOwnerKindV1.Channel || kind == EventOwnerKindV1.NotApplicable)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Kind.Invalid",
                    "owner_kind",
                    "A channel or NotApplicable owner requires its dedicated representation.");
            }

            if (ownerId.IsEmpty)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Id.Empty",
                    "owner_id",
                    "An applicable event owner requires a stable identity.");
            }

            return ContractValidationResult<EventOwnerV1>.Valid(
                new EventOwnerV1(kind, new ChannelId(0), ownerId.ToCanonicalBytes()));
        }

        public static ContractValidationResult<EventOwnerV1> TryForOpaque(
            EventOwnerKindV1 kind,
            byte[] keyBytes)
        {
            if (!Enum.IsDefined(typeof(EventOwnerKindV1), kind))
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Kind.Invalid",
                    "owner_kind",
                    "The event owner kind is not part of the approved closed enum.");
            }

            if (kind == EventOwnerKindV1.Channel || kind == EventOwnerKindV1.NotApplicable)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Kind.Invalid",
                    "owner_kind",
                    "An opaque owner key is not valid for a channel or NotApplicable owner.");
            }

            if (keyBytes == null || keyBytes.Length == 0)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Key.Empty",
                    "owner_key",
                    "An applicable event owner requires nonempty key bytes.");
            }

            return ContractValidationResult<EventOwnerV1>.Valid(
                new EventOwnerV1(kind, new ChannelId(0), keyBytes));
        }

        internal static ContractValidationResult<EventOwnerV1> TryRestoreSerialized(
            EventOwnerKindV1 kind,
            ChannelId channelId,
            byte[] keyBytes)
        {
            if (!Enum.IsDefined(typeof(EventOwnerKindV1), kind))
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Serialized.Kind.Invalid",
                    "owner.kind",
                    "The serialized event owner kind is not part of the approved closed enum.");
            }

            if (keyBytes == null)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Serialized.Key.Missing",
                    "owner.key_hex",
                    "The serialized owner key field is required.");
            }

            if (kind == EventOwnerKindV1.NotApplicable)
            {
                if (channelId.Value != 0 || keyBytes.Length != 0)
                {
                    return ContractValidationResult<EventOwnerV1>.Invalid(
                        "EventOwner.Serialized.NotApplicable.Invalid",
                        "owner",
                        "A NotApplicable owner must have zero channel and empty key fields.");
                }

                return ContractValidationResult<EventOwnerV1>.Valid(NotApplicable);
            }

            if (kind == EventOwnerKindV1.Channel)
            {
                if (keyBytes.Length != 0)
                {
                    return ContractValidationResult<EventOwnerV1>.Invalid(
                        "EventOwner.Serialized.ChannelKey.Invalid",
                        "owner.key_hex",
                        "A channel owner must use its explicit channel field and an empty key.");
                }

                return ContractValidationResult<EventOwnerV1>.Valid(
                    new EventOwnerV1(kind, channelId, Array.Empty<byte>()));
            }

            if (channelId.Value != 0 || keyBytes.Length == 0)
            {
                return ContractValidationResult<EventOwnerV1>.Invalid(
                    "EventOwner.Serialized.Opaque.Invalid",
                    "owner",
                    "An opaque owner must use zero channel and nonempty key fields.");
            }

            return ContractValidationResult<EventOwnerV1>.Valid(
                new EventOwnerV1(kind, new ChannelId(0), keyBytes));
        }
    }

    /// <summary>
    /// Closed event-body discriminant. This bounded refuelling slice constructs
    /// the approved mapping and atomicity bodies; the remaining discriminants
    /// remain reserved for their own later task contracts.
    /// </summary>
    public sealed class EventBodyV1
    {
        private EventBodyV1(EventBodyKindV1 kind, string schemaId, object? payload)
        {
            Kind = kind;
            SchemaId = schemaId;
            Payload = payload;
        }

        public static EventBodyV1 NotApplicable
        {
            get { return new EventBodyV1(EventBodyKindV1.NotApplicable, "NotApplicable", null); }
        }

        public static EventBodyV1 FromRefuelMapping(RefuelMappingBodyV1 body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            return new EventBodyV1(EventBodyKindV1.RefuelMapping, "RefuelMappingBodyV1", body);
        }

        public static EventBodyV1 FromRefuelAtomicity(RefuelAtomicityBodyV1 body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            return new EventBodyV1(EventBodyKindV1.RefuelAtomicity, "RefuelAtomicityBodyV1", body);
        }

        public EventBodyKindV1 Kind { get; }

        public string SchemaId { get; }

        public object? Payload { get; }

        internal bool IsConsistent
        {
            get
            {
                switch (Kind)
                {
                    case EventBodyKindV1.RefuelMapping:
                        return string.Equals(SchemaId, "RefuelMappingBodyV1", StringComparison.Ordinal) &&
                               Payload is RefuelMappingBodyV1;
                    case EventBodyKindV1.RefuelAtomicity:
                        return string.Equals(SchemaId, "RefuelAtomicityBodyV1", StringComparison.Ordinal) &&
                               Payload is RefuelAtomicityBodyV1;
                    case EventBodyKindV1.NotApplicable:
                        return string.Equals(SchemaId, "NotApplicable", StringComparison.Ordinal) &&
                               Payload == null;
                    default:
                        return false;
                }
            }
        }

        internal bool IsCompatibleWithEnvelope(
            double simulationTimeSeconds,
            CommitStatusV1 commitStatus)
        {
            if (!IsConsistent)
            {
                return false;
            }

            if (Payload is RefuelMappingBodyV1 mapping)
            {
                return mapping.EffectiveTimeSeconds == simulationTimeSeconds;
            }

            if (Payload is RefuelAtomicityBodyV1 atomicity)
            {
                return atomicity.CommitStatus == commitStatus;
            }

            return true;
        }
    }

    /// <summary>
    /// A deterministic first-failure diagnostic. CanonicalOrder is supplied by
    /// the owning validator; ties are resolved by the immutable fields below.
    /// </summary>
    public sealed class DiagnosticRecordV1
    {
        public DiagnosticRecordV1(
            DiagnosticSeverityV1 severity,
            ulong canonicalOrder,
            string code,
            string path,
            string message,
            OptionalStableId relatedEventId,
            OptionalStableId relatedSnapshotId)
        {
            if (!Enum.IsDefined(typeof(DiagnosticSeverityV1), severity))
            {
                throw new ArgumentOutOfRangeException(nameof(severity));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A diagnostic path is required.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A diagnostic message is required.", nameof(message));
            }

            if (relatedEventId == null)
            {
                throw new ArgumentNullException(nameof(relatedEventId));
            }

            if (relatedSnapshotId == null)
            {
                throw new ArgumentNullException(nameof(relatedSnapshotId));
            }

            Severity = severity;
            CanonicalOrder = canonicalOrder;
            Code = code;
            Path = path;
            Message = message;
            RelatedEventId = relatedEventId;
            RelatedSnapshotId = relatedSnapshotId;
        }

        public DiagnosticSeverityV1 Severity { get; }

        public ulong CanonicalOrder { get; }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public OptionalStableId RelatedEventId { get; }

        public OptionalStableId RelatedSnapshotId { get; }

        public bool IsFailure
        {
            get { return Severity == DiagnosticSeverityV1.Error || Severity == DiagnosticSeverityV1.Fatal; }
        }

        internal int CompareCanonical(DiagnosticRecordV1 other)
        {
            int orderComparison = CanonicalOrder.CompareTo(other.CanonicalOrder);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            int severityComparison = Severity.CompareTo(other.Severity);
            if (severityComparison != 0)
            {
                return severityComparison;
            }

            int codeComparison = string.CompareOrdinal(Code, other.Code);
            if (codeComparison != 0)
            {
                return codeComparison;
            }

            int pathComparison = string.CompareOrdinal(Path, other.Path);
            if (pathComparison != 0)
            {
                return pathComparison;
            }

            int messageComparison = string.CompareOrdinal(Message, other.Message);
            if (messageComparison != 0)
            {
                return messageComparison;
            }

            int eventComparison = CompareOptionalStableId(RelatedEventId, other.RelatedEventId);
            return eventComparison != 0
                ? eventComparison
                : CompareOptionalStableId(RelatedSnapshotId, other.RelatedSnapshotId);
        }

        private static int CompareOptionalStableId(OptionalStableId left, OptionalStableId right)
        {
            if (left.IsApplicable != right.IsApplicable)
            {
                return left.IsApplicable ? 1 : -1;
            }

            return left.IsApplicable ? left.Value.CompareTo(right.Value) : 0;
        }
    }

    /// <summary>
    /// Immutable canonical diagnostic collection. A failure is selected after
    /// sorting, never by input enumeration order.
    /// </summary>
    public sealed class DiagnosticLogV1
    {
        private readonly ReadOnlyCollection<DiagnosticRecordV1> _records;

        private DiagnosticLogV1(IReadOnlyList<DiagnosticRecordV1> records)
        {
            _records = new ReadOnlyCollection<DiagnosticRecordV1>(records.ToArray());
        }

        public IReadOnlyList<DiagnosticRecordV1> Records
        {
            get { return _records; }
        }

        public DiagnosticRecordV1? FirstFailure
        {
            get { return _records.FirstOrDefault(record => record.IsFailure); }
        }

        public static ContractValidationResult<DiagnosticLogV1> TryCreate(
            IEnumerable<DiagnosticRecordV1> records)
        {
            if (records == null)
            {
                return ContractValidationResult<DiagnosticLogV1>.Invalid(
                    "DiagnosticLog.Records.Missing",
                    "records",
                    "A diagnostic record collection is required.");
            }

            List<DiagnosticRecordV1> input = records.ToList();
            if (input.Any(record => record == null))
            {
                return ContractValidationResult<DiagnosticLogV1>.Invalid(
                    "DiagnosticLog.Record.Null",
                    "records",
                    "A diagnostic record may not be null.");
            }

            DiagnosticRecordV1[] canonical = input
                .OrderBy(record => record.CanonicalOrder)
                .ThenBy(record => record.Severity)
                .ThenBy(record => record.Code, StringComparer.Ordinal)
                .ThenBy(record => record.Path, StringComparer.Ordinal)
                .ThenBy(record => record.Message, StringComparer.Ordinal)
                .ThenBy(record => record.RelatedEventId.IsApplicable)
                .ThenBy(record => record.RelatedEventId.Value)
                .ThenBy(record => record.RelatedSnapshotId.IsApplicable)
                .ThenBy(record => record.RelatedSnapshotId.Value)
                .ToArray();
            for (int i = 1; i < canonical.Length; i++)
            {
                if (canonical[i - 1].CompareCanonical(canonical[i]) == 0)
                {
                    return ContractValidationResult<DiagnosticLogV1>.Invalid(
                        "DiagnosticLog.Record.Duplicate",
                        "records",
                        "Diagnostic records must have unique canonical identities.");
                }
            }

            return ContractValidationResult<DiagnosticLogV1>.Valid(
                new DiagnosticLogV1(canonical));
        }
    }

    /// <summary>
    /// One state-bound event envelope. Named transition bodies are deliberately
    /// not implemented here; this task only defines the immutable envelope,
    /// ordering, lifecycle binding, and failure status.
    /// </summary>
    public sealed class EventRecordV1
    {
        private EventRecordV1(
            EventRankV1 eventRank,
            ulong sequence,
            StableId eventId,
            EventOwnerV1 owner,
            EventBodyV1 body,
            double simulationTimeSeconds,
            StateBindingV1 stateBinding,
            CommitStatusV1 commitStatus,
            DiagnosticRecordV1? diagnostic)
        {
            EventRank = eventRank;
            Sequence = sequence;
            EventId = eventId;
            Owner = owner;
            Body = body;
            SimulationTimeSeconds = simulationTimeSeconds;
            StateBinding = stateBinding;
            CommitStatus = commitStatus;
            Diagnostic = diagnostic;
        }

        public EventRankV1 EventRank { get; }

        public ulong Sequence { get; }

        public StableId EventId { get; }

        public EventOwnerV1 Owner { get; }

        public EventBodyV1 Body { get; }

        public double SimulationTimeSeconds { get; }

        public StateBindingV1 StateBinding { get; }

        public CommitStatusV1 CommitStatus { get; }

        public DiagnosticRecordV1? Diagnostic { get; }

        public static ContractValidationResult<EventRecordV1> TryCreate(
            EventRankV1 eventRank,
            ulong sequence,
            StableId eventId,
            EventOwnerV1 owner,
            EventBodyV1 body,
            double simulationTimeSeconds,
            StateBindingV1 stateBinding,
            CommitStatusV1 commitStatus,
            DiagnosticRecordV1? diagnostic)
        {
            if (!Enum.IsDefined(typeof(EventRankV1), eventRank))
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Rank.Invalid",
                    "event_rank",
                    "The event rank must be one of the approved P2-T04 ranks.");
            }

            if (eventId.IsEmpty)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Id.Empty",
                    "event_id",
                    "An event requires an explicit stable identity.");
            }

            if (owner == null)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Owner.Missing",
                    "owner",
                    "An event owner key is required, including an explicit NotApplicable owner.");
            }

            if (body == null)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Body.Missing",
                    "body",
                    "An event body is required, including an explicit NotApplicable body.");
            }

            if (!body.IsConsistent)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Body.Invalid",
                    "body",
                    "The event body kind, schema identity, and typed payload must agree.");
            }

            if (!ContractValidation.IsFinite(simulationTimeSeconds) || simulationTimeSeconds < 0)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Time.Invalid",
                    "simulation_time_s",
                    "Event time must be finite and nonnegative SI seconds.");
            }

            if (stateBinding == null)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.StateBinding.Missing",
                    "state_binding",
                    "An event must bind the explicit lifecycle state tuple.");
            }

            if (!Enum.IsDefined(typeof(CommitStatusV1), commitStatus))
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.CommitStatus.Invalid",
                    "commit_status",
                    "The event commit status is not part of the approved closed enum.");
            }

            if (!body.IsCompatibleWithEnvelope(simulationTimeSeconds, commitStatus))
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Body.EnvelopeMismatch",
                    "body",
                    "The typed refuelling body must agree with the event time and commit status.");
            }

            if (commitStatus == CommitStatusV1.Committed && diagnostic != null)
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Diagnostic.Unexpected",
                    "diagnostic",
                    "A committed event may not carry a rejection or rollback diagnostic.");
            }

            if ((commitStatus == CommitStatusV1.Rejected || commitStatus == CommitStatusV1.RolledBack) &&
                (diagnostic == null || !diagnostic.IsFailure))
            {
                return ContractValidationResult<EventRecordV1>.Invalid(
                    "Event.Diagnostic.Missing",
                    "diagnostic",
                    "A rejected or rolled-back event requires an error or fatal first diagnostic.");
            }

            return ContractValidationResult<EventRecordV1>.Valid(
                new EventRecordV1(
                    eventRank,
                    sequence,
                    eventId,
                    owner,
                    body,
                    simulationTimeSeconds,
                    stateBinding,
                    commitStatus,
                    diagnostic));
        }

        internal int CompareCanonical(EventRecordV1 other)
        {
            int timeComparison = SimulationTimeSeconds.CompareTo(other.SimulationTimeSeconds);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            int coreVersionComparison = StateBinding.CoreStateVersion.CompareTo(other.StateBinding.CoreStateVersion);
            if (coreVersionComparison != 0)
            {
                return coreVersionComparison;
            }

            int rankComparison = EventRank.CompareTo(other.EventRank);
            if (rankComparison != 0)
            {
                return rankComparison;
            }

            int sequenceComparison = Sequence.CompareTo(other.Sequence);
            return sequenceComparison != 0 ? sequenceComparison : EventId.CompareTo(other.EventId);
        }
    }

    /// <summary>
    /// Canonically ordered immutable event log. Appending returns a new value,
    /// so a rejected append cannot partially mutate a prior state.
    /// </summary>
    public sealed class EventLogV1
    {
        private readonly ReadOnlyCollection<EventRecordV1> _records;

        private EventLogV1(IReadOnlyList<EventRecordV1> records)
        {
            _records = new ReadOnlyCollection<EventRecordV1>(records.ToArray());
        }

        public IReadOnlyList<EventRecordV1> Records
        {
            get { return _records; }
        }

        public static ContractValidationResult<EventLogV1> TryCreate(
            IEnumerable<EventRecordV1> records)
        {
            if (records == null)
            {
                return ContractValidationResult<EventLogV1>.Invalid(
                    "EventLog.Records.Missing",
                    "records",
                    "An event collection is required.");
            }

            List<EventRecordV1> input = records.ToList();
            if (input.Any(record => record == null))
            {
                return ContractValidationResult<EventLogV1>.Invalid(
                    "EventLog.Record.Null",
                    "records",
                    "An event record may not be null.");
            }

            EventRecordV1[] byId = input
                .OrderBy(record => record.EventId)
                .ThenBy(record => record.SimulationTimeSeconds)
                .ThenBy(record => record.Sequence)
                .ToArray();
            for (int i = 1; i < byId.Length; i++)
            {
                if (byId[i - 1].EventId == byId[i].EventId)
                {
                    return ContractValidationResult<EventLogV1>.Invalid(
                        "EventLog.EventId.Duplicate",
                        "records[event_id=" + byId[i].EventId + "]",
                        "Event identities must be unique in one immutable event log.");
                }
            }

            EventRecordV1[] canonical = input
                .OrderBy(record => record.SimulationTimeSeconds)
                .ThenBy(record => record.StateBinding.CoreStateVersion)
                .ThenBy(record => record.EventRank)
                .ThenBy(record => record.Sequence)
                .ThenBy(record => record.EventId)
                .ToArray();

            EventRecordV1? nonCommitted = canonical.FirstOrDefault(
                record => record.CommitStatus != CommitStatusV1.Committed);
            if (nonCommitted != null)
            {
                return ContractValidationResult<EventLogV1>.Invalid(
                    "EventLog.CommitStatus.Invalid",
                    "records[event_id=" + nonCommitted.EventId + "]",
                    "The authoritative event log may contain committed events only; rejected and rolled-back records are diagnostics.");
            }

            return ContractValidationResult<EventLogV1>.Valid(new EventLogV1(canonical));
        }

        public ContractValidationResult<EventLogV1> TryAppend(EventRecordV1 record)
        {
            if (record == null)
            {
                return ContractValidationResult<EventLogV1>.Invalid(
                    "EventLog.Record.Null",
                    "record",
                    "An event record may not be null.");
            }

            if (_records.Any(existing => existing.EventId == record.EventId))
            {
                return ContractValidationResult<EventLogV1>.Invalid(
                    "EventLog.EventId.Duplicate",
                    "record[event_id=" + record.EventId + "]",
                    "An event identity may be appended only once.");
            }

            return TryCreate(_records.Concat(new[] { record }));
        }
    }
}
