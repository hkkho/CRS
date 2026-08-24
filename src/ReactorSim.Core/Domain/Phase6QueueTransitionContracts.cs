using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The three owner-bound delayed-action queues admitted by P6-T06. The
    /// queue family selects the frozen command/queue domain separators and
    /// the required event rank; it is never inferred from a target's array
    /// position.
    /// </summary>
    public enum P6T06QueueFamilyV1 : byte
    {
        Rrs = 0,
        LiquidZone = 1,
        Adjuster = 2
    }

    /// <summary>
    /// Closed source kinds from the P2-T05 queue schema.
    /// </summary>
    public enum P6T06SourceKindV1 : byte
    {
        Controller = 0,
        Manual = 1,
        Scheduled = 2
    }

    /// <summary>
    /// Physical motion modes supplied by the owning state contract. RRS uses
    /// Manual/Automatic/Held; zone uses RateLimited/Prescribed; adjuster uses
    /// Manual/Prescribed/RateLimited.
    /// </summary>
    public enum P6T06MotionModeV1 : byte
    {
        Manual = 0,
        Automatic = 1,
        Held = 2,
        RateLimited = 3,
        Prescribed = 4
    }

    public enum P6T06QueueTransitionKindV1 : byte
    {
        Enqueue = 0,
        MotionAndConsume = 1
    }

    public enum P6T06SaturationStateV1 : byte
    {
        Unsaturated = 0,
        LowerBound = 1,
        UpperBound = 2,
        NotApplicable = 255
    }

    public enum P6T06TargetKindV1 : byte
    {
        RrsActuator = 0,
        LiquidZone = 1,
        AdjusterBank = 2
    }

    internal static class P6T06QueueValidationV1
    {
        public static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }

        public static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        public static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        public static bool IsKnownFamily(P6T06QueueFamilyV1 family)
        {
            return Enum.IsDefined(typeof(P6T06QueueFamilyV1), family);
        }

        public static bool IsKnownSource(P6T06SourceKindV1 sourceKind)
        {
            return Enum.IsDefined(typeof(P6T06SourceKindV1), sourceKind);
        }

        public static ushort RequiredRank(P6T06QueueFamilyV1 family)
        {
            return family == P6T06QueueFamilyV1.Rrs
                ? (ushort)EventRankV1.ControllerCommandGeneration
                : (ushort)EventRankV1.BranchUpdate;
        }

        public static byte OwnerKind(P6T06QueueFamilyV1 family)
        {
            return family == P6T06QueueFamilyV1.Rrs ? (byte)9 : (byte)7;
        }

        public static string QueueMagic(P6T06QueueFamilyV1 family)
        {
            switch (family)
            {
                case P6T06QueueFamilyV1.Rrs:
                    return "CANDU-RRS-QUEUE-V1";
                case P6T06QueueFamilyV1.LiquidZone:
                    return "CANDU-ZONE-QUEUE-V1";
                case P6T06QueueFamilyV1.Adjuster:
                    return "CANDU-ADJUSTER-QUEUE-V1";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
        }

        public static string CommandIdMagic(P6T06QueueFamilyV1 family)
        {
            switch (family)
            {
                case P6T06QueueFamilyV1.Rrs:
                    return "CANDU-RRS-COMMAND-ID-V1";
                case P6T06QueueFamilyV1.LiquidZone:
                    return "CANDU-ZONE-COMMAND-ID-V1";
                case P6T06QueueFamilyV1.Adjuster:
                    return "CANDU-ADJUSTER-COMMAND-ID-V1";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
        }

        public static string CommandMagic(P6T06QueueFamilyV1 family)
        {
            switch (family)
            {
                case P6T06QueueFamilyV1.Rrs:
                    return "CANDU-RRS-COMMAND-V1";
                case P6T06QueueFamilyV1.LiquidZone:
                    return "CANDU-ZONE-COMMAND-V1";
                case P6T06QueueFamilyV1.Adjuster:
                    return "CANDU-ADJUSTER-COMMAND-V1";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
        }

        public static bool IsModeAllowed(
            P6T06QueueFamilyV1 family,
            P6T06MotionModeV1 mode)
        {
            switch (family)
            {
                case P6T06QueueFamilyV1.Rrs:
                    return mode == P6T06MotionModeV1.Manual ||
                           mode == P6T06MotionModeV1.Automatic ||
                           mode == P6T06MotionModeV1.Held;
                case P6T06QueueFamilyV1.LiquidZone:
                    return mode == P6T06MotionModeV1.RateLimited ||
                           mode == P6T06MotionModeV1.Prescribed;
                case P6T06QueueFamilyV1.Adjuster:
                    return mode == P6T06MotionModeV1.Manual ||
                           mode == P6T06MotionModeV1.Prescribed ||
                           mode == P6T06MotionModeV1.RateLimited;
                default:
                    return false;
            }
        }

        public static bool MotionIsActive(P6T06MotionModeV1 mode)
        {
            return mode == P6T06MotionModeV1.Manual ||
                   mode == P6T06MotionModeV1.Automatic ||
                   mode == P6T06MotionModeV1.RateLimited;
        }

        public static void WriteOptionalDouble(BinaryWriter writer, double? value)
        {
            writer.Write(value.HasValue ? (byte)1 : (byte)0);
            if (value.HasValue)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, value.Value);
            }
        }

        public static bool HasDuplicate(IReadOnlyList<StableId> values)
        {
            for (int index = 1; index < values.Count; index++)
            {
                if (values[index - 1] == values[index])
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsCanonicalOrder<T>(
            IReadOnlyList<T> values,
            Comparison<T> comparison)
        {
            for (int index = 1; index < values.Count; index++)
            {
                if (comparison(values[index - 1], values[index]) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static ContractValidationResult<T> Invalid<T>(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<T>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// P2-T05 ScopeKey.Entity key for a controller or branch owner. The
    /// canonical bytes are exactly (EntityKindOrdinal, EntityKeyBytes), with
    /// the stable identity encoded as a length-prefixed Bytes field.
    /// </summary>
    public sealed class P6T06OwnerKeyV1 : IEquatable<P6T06OwnerKeyV1>
    {
        public const byte BranchKindOrdinal = 7;
        public const byte ControllerKindOrdinal = 9;

        private P6T06OwnerKeyV1(byte entityKindOrdinal, StableId entityId)
        {
            EntityKindOrdinal = entityKindOrdinal;
            EntityId = entityId;
        }

        public byte EntityKindOrdinal { get; }

        public StableId EntityId { get; }

        public static ContractValidationResult<P6T06OwnerKeyV1> TryCreate(
            byte entityKindOrdinal,
            StableId entityId)
        {
            if (entityKindOrdinal != BranchKindOrdinal &&
                entityKindOrdinal != ControllerKindOrdinal)
            {
                return P6T06QueueValidationV1.Invalid<P6T06OwnerKeyV1>(
                    "P6T06.Owner.Kind.Invalid",
                    "owner_key.entity_kind",
                    "Only Entity.Branch(7) and Entity.Controller(9) are valid queue owners.");
            }

            if (entityId.IsEmpty)
            {
                return P6T06QueueValidationV1.Invalid<P6T06OwnerKeyV1>(
                    "P6T06.Owner.Id.Empty",
                    "owner_key.entity_id",
                    "A queue owner requires a non-empty stable identity.");
            }

            return ContractValidationResult<P6T06OwnerKeyV1>.Valid(
                new P6T06OwnerKeyV1(entityKindOrdinal, entityId));
        }

        public static ContractValidationResult<P6T06OwnerKeyV1> TryForFamily(
            P6T06QueueFamilyV1 family,
            StableId entityId)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06OwnerKeyV1>(
                    "P6T06.Owner.Family.Invalid",
                    "family",
                    "The queue family is not part of the closed P6-T06 set.");
            }

            return TryCreate(P6T06QueueValidationV1.OwnerKind(family), entityId);
        }

        public bool IsCompatibleWith(P6T06QueueFamilyV1 family)
        {
            return P6T06QueueValidationV1.IsKnownFamily(family) &&
                   EntityKindOrdinal == P6T06QueueValidationV1.OwnerKind(family);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                writer.Write(EntityKindOrdinal);
                Phase5CanonicalBytesV1.WriteBytes(writer, EntityId.ToCanonicalBytes());
            });
        }

        public bool Equals(P6T06OwnerKeyV1? other)
        {
            return other != null &&
                   EntityKindOrdinal == other.EntityKindOrdinal &&
                   EntityId == other.EntityId;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as P6T06OwnerKeyV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EntityKindOrdinal * 397) ^ EntityId.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Typed target identity for an actuator, logical liquid-zone, or
    /// adjuster bank. The target key is explicit and is the only ordering key
    /// used when a source event supplies several commands.
    /// </summary>
    public sealed class P6T06TargetKeyV1 : IComparable<P6T06TargetKeyV1>, IEquatable<P6T06TargetKeyV1>
    {
        private P6T06TargetKeyV1(
            P6T06TargetKindV1 kind,
            StableId stableId,
            uint logicalZoneId)
        {
            Kind = kind;
            StableId = stableId;
            LogicalZoneId = logicalZoneId;
        }

        public P6T06TargetKindV1 Kind { get; }

        public StableId StableId { get; }

        public uint LogicalZoneId { get; }

        public static ContractValidationResult<P6T06TargetKeyV1> TryForRrsActuator(
            StableId actuatorId)
        {
            return TryForStableTarget(P6T06TargetKindV1.RrsActuator, actuatorId);
        }

        public static ContractValidationResult<P6T06TargetKeyV1> TryForAdjusterBank(
            StableId bankId)
        {
            return TryForStableTarget(P6T06TargetKindV1.AdjusterBank, bankId);
        }

        public static ContractValidationResult<P6T06TargetKeyV1> TryForLiquidZone(
            uint logicalZoneId)
        {
            if (logicalZoneId >= LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return P6T06QueueValidationV1.Invalid<P6T06TargetKeyV1>(
                    "P6T06.Target.Zone.OutOfRange",
                    "target.logical_zone_id",
                    "A liquid-zone target must be one of the 14 declared logical zones.");
            }

            return ContractValidationResult<P6T06TargetKeyV1>.Valid(
                new P6T06TargetKeyV1(
                    P6T06TargetKindV1.LiquidZone,
                    StableId.Empty,
                    logicalZoneId));
        }

        public bool IsCompatibleWith(P6T06QueueFamilyV1 family)
        {
            return (family == P6T06QueueFamilyV1.Rrs && Kind == P6T06TargetKindV1.RrsActuator) ||
                   (family == P6T06QueueFamilyV1.LiquidZone && Kind == P6T06TargetKindV1.LiquidZone) ||
                   (family == P6T06QueueFamilyV1.Adjuster && Kind == P6T06TargetKindV1.AdjusterBank);
        }

        /// <summary>
        /// The untagged target payload used by the P2-T05 TargetId:Bytes field.
        /// The enclosing queue/command domain identifies the target family.
        /// </summary>
        public byte[] ToTargetBytes()
        {
            return Kind == P6T06TargetKindV1.LiquidZone
                ? Phase5CanonicalBytesV1.Build(writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, LogicalZoneId);
                })
                : StableId.ToCanonicalBytes();
        }

        public int CompareTo(P6T06TargetKeyV1? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (Kind != other.Kind)
            {
                return Kind.CompareTo(other.Kind);
            }

            return Kind == P6T06TargetKindV1.LiquidZone
                ? LogicalZoneId.CompareTo(other.LogicalZoneId)
                : StableId.CompareTo(other.StableId);
        }

        public bool Equals(P6T06TargetKeyV1? other)
        {
            return other != null &&
                   Kind == other.Kind &&
                   StableId == other.StableId &&
                   LogicalZoneId == other.LogicalZoneId;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as P6T06TargetKeyV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ StableId.GetHashCode() ^ LogicalZoneId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return Kind == P6T06TargetKindV1.LiquidZone
                ? "zone:" + LogicalZoneId.ToString(CultureInfo.InvariantCulture)
                : Kind + ":" + StableId;
        }

        public static bool operator ==(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return !ReferenceEquals(left, null) && left.Equals(right);
        }

        public static bool operator !=(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            return !(left == right);
        }

        public static bool operator <(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            return ReferenceEquals(left, null)
                ? !ReferenceEquals(right, null)
                : left.CompareTo(right) < 0;
        }

        public static bool operator <=(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(P6T06TargetKeyV1? left, P6T06TargetKeyV1? right)
        {
            return ReferenceEquals(right, null) ||
                   (!ReferenceEquals(left, null) && left.CompareTo(right) >= 0);
        }

        private static ContractValidationResult<P6T06TargetKeyV1> TryForStableTarget(
            P6T06TargetKindV1 kind,
            StableId stableId)
        {
            if (stableId.IsEmpty)
            {
                return P6T06QueueValidationV1.Invalid<P6T06TargetKeyV1>(
                    "P6T06.Target.Id.Empty",
                    "target.id",
                    "A stable target identity may not be empty.");
            }

            return ContractValidationResult<P6T06TargetKeyV1>.Valid(
                new P6T06TargetKeyV1(kind, stableId, 0));
        }
    }

    /// <summary>
    /// One candidate command before queue allocation. Sequence, command ID,
    /// due time, and command digest are deliberately absent until the atomic
    /// enqueue transaction assigns them.
    /// </summary>
    public sealed class P6T06CommandCandidateV1
    {
        private P6T06CommandCandidateV1(
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1 target,
            P6T06SourceKindV1 sourceKind,
            EventRankV1 eventRank,
            double delaySeconds,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            Family = family;
            Target = target;
            SourceKind = sourceKind;
            EventRank = eventRank;
            DelaySeconds = delaySeconds;
            RequestedCommand = requestedCommand;
            BoundedCommand = boundedCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
        }

        public P6T06QueueFamilyV1 Family { get; }

        public P6T06TargetKeyV1 Target { get; }

        public P6T06SourceKindV1 SourceKind { get; }

        public EventRankV1 EventRank { get; }

        public double DelaySeconds { get; }

        public double RequestedCommand { get; }

        public double BoundedCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public bool IsSaturated
        {
            get { return RequestedCommand != BoundedCommand; }
        }

        public P6T06SaturationStateV1 SaturationState
        {
            get
            {
                if (RequestedCommand < BoundedCommand)
                {
                    return P6T06SaturationStateV1.LowerBound;
                }

                if (RequestedCommand > BoundedCommand)
                {
                    return P6T06SaturationStateV1.UpperBound;
                }

                return P6T06SaturationStateV1.Unsaturated;
            }
        }

        public static ContractValidationResult<P6T06CommandCandidateV1> TryCreate(
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1? target,
            P6T06SourceKindV1 sourceKind,
            EventRankV1 eventRank,
            double delaySeconds,
            double requestedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.Family.Invalid",
                    "family",
                    "The candidate queue family is not part of the closed P6-T06 set.");
            }

            if (target == null || !target.IsCompatibleWith(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.Target.Invalid",
                    "target",
                    "The candidate target must be present and belong to the selected queue family.");
            }

            if (!P6T06QueueValidationV1.IsKnownSource(sourceKind))
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.SourceKind.Invalid",
                    "source_kind",
                    "SourceKind must be Controller, Manual, or Scheduled.");
            }

            if (!Enum.IsDefined(typeof(EventRankV1), eventRank) ||
                (ushort)eventRank != P6T06QueueValidationV1.RequiredRank(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.SourceRank.Invalid",
                    "event_rank",
                    "The command source must use the fixed rank for its queue family.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(delaySeconds) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(requestedCommand) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(lowerBound) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(upperBound) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                lowerBound > upperBound)
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.Values.Invalid",
                    "candidate",
                    "Command, bound, delay, and rate values must be finite canonical values with ordered bounds.");
            }

            double boundedCommand = Math.Min(
                upperBound,
                Math.Max(lowerBound, requestedCommand));
            if (!P6T06QueueValidationV1.IsCanonicalFinite(boundedCommand))
            {
                return P6T06QueueValidationV1.Invalid<P6T06CommandCandidateV1>(
                    "P6T06.Candidate.Bounded.Invalid",
                    "bounded_command",
                    "The bounded command must remain finite and canonical.");
            }

            return ContractValidationResult<P6T06CommandCandidateV1>.Valid(
                new P6T06CommandCandidateV1(
                    family,
                    target,
                    sourceKind,
                    eventRank,
                    delaySeconds,
                    requestedCommand,
                    boundedCommand,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond));
        }
    }

    /// <summary>
    /// Self-contained QueueAvailableV1 projection. Bounds and rate are copied
    /// into the queue for replay and are checked against every enqueued command
    /// for the same target.
    /// </summary>
    public sealed class P6T06AvailableCommandV1
    {
        private P6T06AvailableCommandV1(
            P6T06TargetKeyV1 target,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            Target = target;
            BoundedCommand = boundedCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
        }

        public P6T06TargetKeyV1 Target { get; }

        public double BoundedCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public static ContractValidationResult<P6T06AvailableCommandV1> TryCreate(
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1? target,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family) ||
                target == null || !target.IsCompatibleWith(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06AvailableCommandV1>(
                    "P6T06.Available.Target.Invalid",
                    "target",
                    "An available command requires a target from the selected queue family.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalFinite(boundedCommand) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(lowerBound) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(upperBound) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                lowerBound > upperBound ||
                boundedCommand < lowerBound ||
                boundedCommand > upperBound)
            {
                return P6T06QueueValidationV1.Invalid<P6T06AvailableCommandV1>(
                    "P6T06.Available.Values.Invalid",
                    "available",
                    "Available command values must be finite, ordered, and within their explicit bounds.");
            }

            return ContractValidationResult<P6T06AvailableCommandV1>.Valid(
                new P6T06AvailableCommandV1(
                    target,
                    boundedCommand,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond));
        }

        internal P6T06AvailableCommandV1 WithBoundedCommand(double boundedCommand)
        {
            return new P6T06AvailableCommandV1(
                Target,
                boundedCommand,
                LowerBound,
                UpperBound,
                RateLimitPerSecond);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, Target.ToTargetBytes());
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
            });
        }
    }

    /// <summary>
    /// Closed delayed command record. CommandId is derived only after the
    /// queue allocator assigns Sequence; callers cannot supply a random ID.
    /// </summary>
    public sealed class P6T06QueueCommandV1
    {
        private P6T06QueueCommandV1(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            StableId commandId,
            P6T06TargetKeyV1 target,
            P6T06OwnerKeyV1 ownerKey,
            P6T06SourceKindV1 sourceKind,
            StableId sourceEventId,
            Digest32 sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            EventRankV1 eventRank,
            ulong sequence,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32 commandDigest)
        {
            Family = family;
            QueueId = queueId;
            CommandId = commandId;
            Target = target;
            OwnerKey = ownerKey;
            SourceKind = sourceKind;
            SourceEventId = sourceEventId;
            SourceStateBindingDigest = sourceStateBindingDigest;
            EnqueueTimeSeconds = enqueueTimeSeconds;
            DelaySeconds = delaySeconds;
            DueTimeSeconds = dueTimeSeconds;
            EventRank = eventRank;
            Sequence = sequence;
            RequestedCommand = requestedCommand;
            BoundedCommand = boundedCommand;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RateLimitPerSecond = rateLimitPerSecond;
            CommandDigest = commandDigest;
        }

        public P6T06QueueFamilyV1 Family { get; }

        public StableId QueueId { get; }

        public StableId CommandId { get; }

        public P6T06TargetKeyV1 Target { get; }

        public P6T06OwnerKeyV1 OwnerKey { get; }

        public P6T06SourceKindV1 SourceKind { get; }

        public StableId SourceEventId { get; }

        public Digest32 SourceStateBindingDigest { get; }

        public double EnqueueTimeSeconds { get; }

        public double DelaySeconds { get; }

        public double DueTimeSeconds { get; }

        public EventRankV1 EventRank { get; }

        public ulong Sequence { get; }

        public double RequestedCommand { get; }

        public double BoundedCommand { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public Digest32 CommandDigest { get; }

        public bool IsSaturated
        {
            get { return RequestedCommand != BoundedCommand; }
        }

        public P6T06SaturationStateV1 SaturationState
        {
            get
            {
                if (RequestedCommand < BoundedCommand)
                {
                    return P6T06SaturationStateV1.LowerBound;
                }

                if (RequestedCommand > BoundedCommand)
                {
                    return P6T06SaturationStateV1.UpperBound;
                }

                return P6T06SaturationStateV1.Unsaturated;
            }
        }

        public static ContractValidationResult<P6T06QueueCommandV1> TryCreate(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            StableId? expectedCommandId,
            P6T06TargetKeyV1? target,
            P6T06OwnerKeyV1? ownerKey,
            P6T06SourceKindV1 sourceKind,
            StableId sourceEventId,
            Digest32? sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            EventRankV1 eventRank,
            ulong sequence,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32? expectedCommandDigest = null)
        {
            ContractValidationResult<bool> shape = ValidateShape(
                family,
                queueId,
                target,
                ownerKey,
                sourceKind,
                sourceEventId,
                sourceStateBindingDigest,
                enqueueTimeSeconds,
                delaySeconds,
                dueTimeSeconds,
                eventRank,
                requestedCommand,
                boundedCommand,
                lowerBound,
                upperBound,
                rateLimitPerSecond);
            if (!shape.IsValid)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueCommandV1>(
                    shape.FirstDiagnostic.Code,
                    shape.FirstDiagnostic.Path,
                    shape.FirstDiagnostic.Message);
            }

            StableId commandId = DeriveCommandId(
                family,
                queueId,
                sourceEventId,
                ownerKey!,
                target!,
                sequence);
            if (expectedCommandId.HasValue && expectedCommandId.Value != commandId)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueCommandV1>(
                    "P6T06.CommandId.Mismatch",
                    "command_id",
                    "The supplied command identity does not equal the deterministic UUIDv8 derivation.");
            }

            Digest32 commandDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        family,
                        queueId,
                        commandId,
                        target!,
                        ownerKey!,
                        sourceKind,
                        sourceEventId,
                        sourceStateBindingDigest!,
                        enqueueTimeSeconds,
                        delaySeconds,
                        dueTimeSeconds,
                        eventRank,
                        sequence,
                        requestedCommand,
                        boundedCommand,
                        lowerBound,
                        upperBound,
                        rateLimitPerSecond,
                        null)));
            if (expectedCommandDigest != null && !expectedCommandDigest.Equals(commandDigest))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueCommandV1>(
                    "P6T06.CommandDigest.Mismatch",
                    "command_digest",
                    "The supplied command digest does not equal the complete canonical command body.");
            }

            return ContractValidationResult<P6T06QueueCommandV1>.Valid(
                new P6T06QueueCommandV1(
                    family,
                    queueId,
                    commandId,
                    target!,
                    ownerKey!,
                    sourceKind,
                    sourceEventId,
                    sourceStateBindingDigest!,
                    enqueueTimeSeconds,
                    delaySeconds,
                    dueTimeSeconds,
                    eventRank,
                    sequence,
                    requestedCommand,
                    boundedCommand,
                    lowerBound,
                    upperBound,
                    rateLimitPerSecond,
                    commandDigest));
        }

        public static StableId DeriveCommandId(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            StableId sourceEventId,
            P6T06OwnerKeyV1 ownerKey,
            P6T06TargetKeyV1 target,
            ulong sequence)
        {
            if (ownerKey == null)
            {
                throw new ArgumentNullException(nameof(ownerKey));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return Phase5CanonicalBytesV1.DeriveUuidV8(
                P6T06QueueValidationV1.CommandIdMagic(family),
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, queueId.ToCanonicalBytes());
                    Phase5CanonicalBytesV1.WriteStableId(writer, sourceEventId);
                    Phase5CanonicalBytesV1.WriteBytesRaw(writer, ownerKey.ToCanonicalBytes());
                    Phase5CanonicalBytesV1.WriteBytes(writer, target.ToTargetBytes());
                    Phase5CanonicalBytesV1.WriteUInt64(writer, sequence);
                });
        }

        public int CompareCanonical(P6T06QueueCommandV1 other)
        {
            int dueComparison = DueTimeSeconds.CompareTo(other.DueTimeSeconds);
            if (dueComparison != 0)
            {
                return dueComparison;
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

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                Family,
                QueueId,
                CommandId,
                Target,
                OwnerKey,
                SourceKind,
                SourceEventId,
                SourceStateBindingDigest,
                EnqueueTimeSeconds,
                DelaySeconds,
                DueTimeSeconds,
                EventRank,
                Sequence,
                RequestedCommand,
                BoundedCommand,
                LowerBound,
                UpperBound,
                RateLimitPerSecond,
                CommandDigest);
        }

        internal bool IsValidForQueue(P6T06QueueFamilyV1 family, StableId queueId, P6T06OwnerKeyV1 ownerKey)
        {
            if (Family != family || QueueId != queueId || !OwnerKey.Equals(ownerKey))
            {
                return false;
            }

            if (DeriveCommandId(
                    Family,
                    QueueId,
                    SourceEventId,
                    OwnerKey,
                    Target,
                    Sequence) != CommandId)
            {
                return false;
            }

            Digest32 expected = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        Family,
                        QueueId,
                        CommandId,
                        Target,
                        OwnerKey,
                        SourceKind,
                        SourceEventId,
                        SourceStateBindingDigest,
                        EnqueueTimeSeconds,
                        DelaySeconds,
                        DueTimeSeconds,
                        EventRank,
                        Sequence,
                        RequestedCommand,
                        BoundedCommand,
                        LowerBound,
                        UpperBound,
                        RateLimitPerSecond,
                        null)));
            return expected.Equals(CommandDigest);
        }

        private static ContractValidationResult<bool> ValidateShape(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06TargetKeyV1? target,
            P6T06OwnerKeyV1? ownerKey,
            P6T06SourceKindV1 sourceKind,
            StableId sourceEventId,
            Digest32? sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            EventRankV1 eventRank,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family) || queueId.IsEmpty)
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.Identity.Invalid",
                    "command.identity",
                    "A command requires a known family and non-empty queue identity.");
            }

            if (target == null || !target.IsCompatibleWith(family) ||
                ownerKey == null || !ownerKey.IsCompatibleWith(family) ||
                sourceEventId.IsEmpty || sourceStateBindingDigest == null)
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.Binding.Invalid",
                    "command.binding",
                    "Queue, target, typed owner, source event, and source-state binding must be present and compatible.");
            }

            if (!P6T06QueueValidationV1.IsKnownSource(sourceKind) ||
                !Enum.IsDefined(typeof(EventRankV1), eventRank) ||
                (ushort)eventRank != P6T06QueueValidationV1.RequiredRank(family))
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.SourceRank.Invalid",
                    "command.source_rank",
                    "The closed source kind and family-specific event rank are required.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(enqueueTimeSeconds) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(delaySeconds) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(dueTimeSeconds))
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.Time.Invalid",
                    "command.time",
                    "EnqueueTime, Delay, and DueTime must be finite nonnegative canonical seconds.");
            }

            double computedDueTime = enqueueTimeSeconds + delaySeconds;
            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(computedDueTime) ||
                dueTimeSeconds != computedDueTime)
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.DueTime.Mismatch",
                    "due_time_s",
                    "DueTime must equal the exact one-addition EnqueueTime plus Delay result.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalFinite(requestedCommand) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(boundedCommand) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(lowerBound) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(upperBound) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(rateLimitPerSecond) ||
                lowerBound > upperBound ||
                boundedCommand < lowerBound ||
                boundedCommand > upperBound ||
                boundedCommand != Math.Min(upperBound, Math.Max(lowerBound, requestedCommand)))
            {
                return P6T06QueueValidationV1.Invalid<bool>(
                    "P6T06.Command.Values.Invalid",
                    "command.values",
                    "Requested, bounded, bounds, and rate values must satisfy the exact saturation rule.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static byte[] BuildBytes(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            StableId commandId,
            P6T06TargetKeyV1 target,
            P6T06OwnerKeyV1 ownerKey,
            P6T06SourceKindV1 sourceKind,
            StableId sourceEventId,
            Digest32 sourceStateBindingDigest,
            double enqueueTimeSeconds,
            double delaySeconds,
            double dueTimeSeconds,
            EventRankV1 eventRank,
            ulong sequence,
            double requestedCommand,
            double boundedCommand,
            double lowerBound,
            double upperBound,
            double rateLimitPerSecond,
            Digest32? commandDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    P6T06QueueValidationV1.CommandMagic(family));
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteBytes(writer, queueId.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteStableId(writer, commandId);
                if (family == P6T06QueueFamilyV1.LiquidZone)
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, target.LogicalZoneId);
                }
                else
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, target.ToTargetBytes());
                }

                Phase5CanonicalBytesV1.WriteBytesRaw(writer, ownerKey.ToCanonicalBytes());
                writer.Write((byte)sourceKind);
                Phase5CanonicalBytesV1.WriteStableId(writer, sourceEventId);
                Phase5CanonicalBytesV1.WriteDigest(writer, sourceStateBindingDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, enqueueTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, delaySeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, dueTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt16(writer, (ushort)eventRank);
                Phase5CanonicalBytesV1.WriteUInt64(writer, sequence);
                Phase5CanonicalBytesV1.WriteDouble(writer, requestedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, boundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, lowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, upperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, rateLimitPerSecond);
                if (commandDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, commandDigest);
                }
            });
        }
    }

    /// <summary>
    /// Immutable event-phase binding for one owner queue. The scheduler creates
    /// an open token before same-time generation and closes that token after
    /// freezing the due batch; enqueue then cannot silently replace the phase
    /// cutoff with a caller-supplied Boolean.
    /// </summary>
    public sealed class P6T06QueuePhaseTokenV1
    {
        private P6T06QueuePhaseTokenV1(
            StableId queueId,
            Digest32 queueDigest,
            double eventTimeSeconds,
            bool dueBatchCutoffClosed)
        {
            QueueId = queueId;
            QueueDigest = queueDigest;
            EventTimeSeconds = eventTimeSeconds;
            DueBatchCutoffClosed = dueBatchCutoffClosed;
        }

        public StableId QueueId { get; }

        public Digest32 QueueDigest { get; }

        public double EventTimeSeconds { get; }

        public bool DueBatchCutoffClosed { get; }

        public P6T06QueuePhaseTokenV1 CloseDueBatchCutoff()
        {
            return DueBatchCutoffClosed
                ? this
                : new P6T06QueuePhaseTokenV1(
                    QueueId,
                    QueueDigest,
                    EventTimeSeconds,
                    true);
        }

        internal static P6T06QueuePhaseTokenV1 Create(
            StableId queueId,
            Digest32 queueDigest,
            double eventTimeSeconds)
        {
            return new P6T06QueuePhaseTokenV1(
                queueId,
                queueDigest,
                eventTimeSeconds,
                false);
        }

        internal bool IsValidFor(
            StableId queueId,
            Digest32 queueDigest,
            double eventTimeSeconds)
        {
            return QueueId == queueId &&
                   QueueDigest.Equals(queueDigest) &&
                   EventTimeSeconds == eventTimeSeconds;
        }
    }

    /// <summary>
    /// Adapter-validated source-state binding precondition. P6-T06 does not
    /// derive a controller measurement or branch pre-state; the owning adapter
    /// validates that frozen state and supplies its exact digest here. The
    /// queue then preserves the exact event/digest pair in every command.
    /// </summary>
    public sealed class P6T06SourceBindingTokenV1
    {
        private P6T06SourceBindingTokenV1(
            StableId sourceEventId,
            Digest32 bindingDigest)
        {
            SourceEventId = sourceEventId;
            BindingDigest = bindingDigest;
        }

        public StableId SourceEventId { get; }

        public Digest32 BindingDigest { get; }

        public static ContractValidationResult<P6T06SourceBindingTokenV1> TryCreate(
            StableId sourceEventId,
            Digest32? bindingDigest)
        {
            if (sourceEventId.IsEmpty || bindingDigest == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06SourceBindingTokenV1>(
                    "P6T06.SourceBinding.Invalid",
                    "source_binding",
                    "The owner adapter must provide a non-empty source event and validated frozen-state digest.");
            }

            return ContractValidationResult<P6T06SourceBindingTokenV1>.Valid(
                new P6T06SourceBindingTokenV1(sourceEventId, bindingDigest));
        }
    }

    /// <summary>
    /// Validated identity for one controller command-generation event. This is
    /// an adapter handoff value, not a serialized v1 queue record. The event
    /// carries the controller owner, the source event identity, the exact
    /// caller-validated source-state binding, and the projection digest that
    /// the event is admitting.
    /// </summary>
    public sealed class P6T06RrsControllerEventIdentityV1
    {
        private P6T06RrsControllerEventIdentityV1(
            StableId controllerId,
            StableId sourceEventId,
            Digest32 sourceBindingDigest,
            Digest32 projectionDigest)
        {
            ControllerId = controllerId;
            SourceEventId = sourceEventId;
            SourceBindingDigest = sourceBindingDigest;
            ProjectionDigest = projectionDigest;
        }

        public StableId ControllerId { get; }

        public StableId SourceEventId { get; }

        public Digest32 SourceBindingDigest { get; }

        public Digest32 ProjectionDigest { get; }

        public static ContractValidationResult<P6T06RrsControllerEventIdentityV1> TryCreate(
            StableId controllerId,
            StableId sourceEventId,
            Digest32? sourceBindingDigest,
            Digest32? projectionDigest)
        {
            if (controllerId.IsEmpty || sourceEventId.IsEmpty)
            {
                return P6T06QueueValidationV1.Invalid<P6T06RrsControllerEventIdentityV1>(
                    "P6T06.RrsEvent.Identity.Invalid",
                    "event_identity",
                    "A controller event requires non-empty controller and source-event identities.");
            }

            if (sourceBindingDigest == null || projectionDigest == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06RrsControllerEventIdentityV1>(
                    "P6T06.RrsEvent.Digest.Missing",
                    "event_identity",
                    "A controller event requires both its source-state binding and projection digests.");
            }

            return ContractValidationResult<P6T06RrsControllerEventIdentityV1>.Valid(
                new P6T06RrsControllerEventIdentityV1(
                    controllerId,
                    sourceEventId,
                    sourceBindingDigest,
                    projectionDigest));
        }
    }

    /// <summary>
    /// Complete immutable owner-bound queue state. Both historical registries
    /// remain in the state after consume; consumed command IDs are never
    /// released or reallocated.
    /// </summary>
    public sealed class P6T06QueueStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private P6T06QueueStateV1(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<P6T06AvailableCommandV1> availableCommands,
            IEnumerable<P6T06QueueCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds,
            Digest32 queueDigest)
        {
            Family = family;
            QueueId = queueId;
            OwnerKey = ownerKey;
            GenerationCadenceOrNA = generationCadenceOrNA;
            InitialNextSequence = initialNextSequence;
            NextSequence = nextSequence;
            LastMotionTimeSeconds = lastMotionTimeSeconds;
            AvailableCommands = new ReadOnlyCollection<P6T06AvailableCommandV1>(availableCommands.ToArray());
            PendingCommands = new ReadOnlyCollection<P6T06QueueCommandV1>(pendingCommands.ToArray());
            AppliedSourceEventIds = new ReadOnlyCollection<StableId>(appliedSourceEventIds.ToArray());
            AllocatedCommandIds = new ReadOnlyCollection<StableId>(allocatedCommandIds.ToArray());
            QueueDigest = queueDigest;
        }

        public P6T06QueueFamilyV1 Family { get; }

        public StableId QueueId { get; }

        public P6T06OwnerKeyV1 OwnerKey { get; }

        public double? GenerationCadenceOrNA { get; }

        public ulong InitialNextSequence { get; }

        public ulong NextSequence { get; }

        public double LastMotionTimeSeconds { get; }

        public IReadOnlyList<P6T06AvailableCommandV1> AvailableCommands { get; }

        public IReadOnlyList<P6T06QueueCommandV1> PendingCommands { get; }

        public IReadOnlyList<StableId> AppliedSourceEventIds { get; }

        public IReadOnlyList<StableId> AllocatedCommandIds { get; }

        public Digest32 QueueDigest { get; }

        public static ContractValidationResult<P6T06QueueStateV1> TryCreate(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1? ownerKey,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<P6T06AvailableCommandV1>? availableCommands,
            IEnumerable<P6T06QueueCommandV1>? pendingCommands,
            IEnumerable<StableId>? appliedSourceEventIds,
            IEnumerable<StableId>? allocatedCommandIds,
            Digest32? expectedQueueDigest = null)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family) || queueId.IsEmpty ||
                ownerKey == null || !ownerKey.IsCompatibleWith(family))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Identity.Invalid",
                    "queue.identity",
                    "A queue requires a known family, queue identity, and compatible typed owner key.");
            }

            if (generationCadenceOrNA.HasValue &&
                !P6T06QueueValidationV1.IsCanonicalNonnegative(generationCadenceOrNA.Value) ||
                generationCadenceOrNA.HasValue && generationCadenceOrNA.Value <= 0.0)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Cadence.Invalid",
                    "generation_cadence_or_na",
                    "An applicable queue cadence must be finite, positive, and canonical; null is explicit NotApplicable.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(lastMotionTimeSeconds) ||
                nextSequence < initialNextSequence)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.State.Invalid",
                    "queue_state",
                    "LastMotionTime must be canonical nonnegative seconds and NextSequence must not rewind.");
            }

            if (availableCommands == null || pendingCommands == null ||
                appliedSourceEventIds == null || allocatedCommandIds == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Collection.Missing",
                    "queue_state",
                    "Available, pending, and both historical registries are required.");
            }

            P6T06AvailableCommandV1[] availableInput = availableCommands.ToArray();
            P6T06QueueCommandV1[] pendingInput = pendingCommands.ToArray();
            StableId[] appliedInput = appliedSourceEventIds.ToArray();
            StableId[] allocatedInput = allocatedCommandIds.ToArray();
            if (availableInput.Any(item => item == null) || pendingInput.Any(item => item == null))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Collection.Null",
                    "queue_state",
                    "Queue entries may not be null.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalOrder(
                    availableInput,
                    (left, right) => left.Target.CompareTo(right.Target)) ||
                !P6T06QueueValidationV1.IsCanonicalOrder(
                    pendingInput,
                    (left, right) => left.CompareCanonical(right)) ||
                !P6T06QueueValidationV1.IsCanonicalOrder(
                    appliedInput,
                    (left, right) => left.CompareTo(right)) ||
                !P6T06QueueValidationV1.IsCanonicalOrder(
                    allocatedInput,
                    (left, right) => left.CompareTo(right)))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Order.NonCanonical",
                    "queue_state",
                    "Available, pending, and historical registry arrays must already use their exact canonical order.");
            }

            if ((ulong)allocatedInput.Length != nextSequence - initialNextSequence ||
                (ulong)availableInput.Length > uint.MaxValue ||
                (ulong)pendingInput.Length > uint.MaxValue ||
                (ulong)appliedInput.Length > uint.MaxValue ||
                (ulong)allocatedInput.Length > uint.MaxValue)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Count.Invalid",
                    "queue_state.counts",
                    "Registry count, sequence difference, and UInt32 canonical counts must agree.");
            }

            P6T06AvailableCommandV1[] available = availableInput;
            for (int index = 0; index < available.Length; index++)
            {
                if (!available[index].Target.IsCompatibleWith(family))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                        "P6T06.Queue.Available.Target.Invalid",
                        "available_commands[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every available target must belong to the queue family.");
                }

                if (index > 0 && available[index - 1].Target.Equals(available[index].Target))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                        "P6T06.Queue.Available.Duplicate",
                        "available_commands",
                        "A queue has exactly one available projection per target.");
                }
            }

            StableId[] applied = appliedInput;
            StableId[] allocated = allocatedInput;
            if (applied.Any(id => id.IsEmpty) || allocated.Any(id => id.IsEmpty) ||
                P6T06QueueValidationV1.HasDuplicate(applied) ||
                P6T06QueueValidationV1.HasDuplicate(allocated))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Registry.Invalid",
                    "queue_state.registries",
                    "Historical source and command registries must contain unique non-empty identities.");
            }

            P6T06QueueCommandV1[] pending = pendingInput;
            HashSet<StableId> pendingIds = new HashSet<StableId>();
            HashSet<ulong> pendingSequences = new HashSet<ulong>();
            HashSet<P6T06TargetKeyV1> availableTargets = new HashSet<P6T06TargetKeyV1>(
                available.Select(item => item.Target));
            for (int index = 0; index < pending.Length; index++)
            {
                P6T06QueueCommandV1 command = pending[index];
                P6T06AvailableCommandV1? commandAvailable = available.SingleOrDefault(
                    item => item.Target.Equals(command.Target));
                if (!command.IsValidForQueue(family, queueId, ownerKey) ||
                    command.DueTimeSeconds < lastMotionTimeSeconds ||
                    command.Sequence < initialNextSequence ||
                    command.Sequence >= nextSequence ||
                    !pendingIds.Add(command.CommandId) ||
                    !pendingSequences.Add(command.Sequence) ||
                    Array.BinarySearch(allocated, command.CommandId, Comparer<StableId>.Default) < 0 ||
                    Array.BinarySearch(applied, command.SourceEventId, Comparer<StableId>.Default) < 0 ||
                    !availableTargets.Contains(command.Target) ||
                    commandAvailable == null ||
                    commandAvailable.LowerBound != command.LowerBound ||
                    commandAvailable.UpperBound != command.UpperBound ||
                    commandAvailable.RateLimitPerSecond != command.RateLimitPerSecond)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                        "P6T06.Queue.Pending.Invalid",
                        "pending_commands[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Pending records must be owner-bound, canonical, allocated, source-applied, due-valid, and target-complete.");
                }
            }

            Digest32 queueDigest = ComputeDigest(
                family,
                queueId,
                ownerKey,
                generationCadenceOrNA,
                initialNextSequence,
                nextSequence,
                lastMotionTimeSeconds,
                available,
                pending,
                applied,
                allocated);
            if (expectedQueueDigest != null && !expectedQueueDigest.Equals(queueDigest))
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueueStateV1>(
                    "P6T06.Queue.Digest.Mismatch",
                    "queue_digest",
                    "The supplied queue digest does not equal the complete canonical queue state.");
            }

            return ContractValidationResult<P6T06QueueStateV1>.Valid(
                new P6T06QueueStateV1(
                    family,
                    queueId,
                    ownerKey,
                    generationCadenceOrNA,
                    initialNextSequence,
                    nextSequence,
                    lastMotionTimeSeconds,
                    available,
                    pending,
                    applied,
                    allocated,
                    queueDigest));
        }

        public static Digest32 ComputeDigest(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IEnumerable<P6T06AvailableCommandV1> availableCommands,
            IEnumerable<P6T06QueueCommandV1> pendingCommands,
            IEnumerable<StableId> appliedSourceEventIds,
            IEnumerable<StableId> allocatedCommandIds)
        {
            if (ownerKey == null || availableCommands == null || pendingCommands == null ||
                appliedSourceEventIds == null || allocatedCommandIds == null)
            {
                throw new ArgumentNullException(nameof(ownerKey));
            }

            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        family,
                        queueId,
                        ownerKey,
                        generationCadenceOrNA,
                        initialNextSequence,
                        nextSequence,
                        lastMotionTimeSeconds,
                        availableCommands.OrderBy(item => item.Target).ToArray(),
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

        public ContractValidationResult<P6T06QueuePhaseTokenV1> TryCreatePhaseToken(
            double eventTimeSeconds)
        {
            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(eventTimeSeconds) ||
                eventTimeSeconds < LastMotionTimeSeconds)
            {
                return P6T06QueueValidationV1.Invalid<P6T06QueuePhaseTokenV1>(
                    "P6T06.PhaseToken.Time.Invalid",
                    "event_time_s",
                    "A queue phase token requires a canonical event time at or after physical motion time.");
            }

            return ContractValidationResult<P6T06QueuePhaseTokenV1>.Valid(
                P6T06QueuePhaseTokenV1.Create(QueueId, QueueDigest, eventTimeSeconds));
        }

        /// <summary>
        /// Returns the fixed rank-4 branch queue consumption order. Queue
        /// mutation remains one-owner-at-a-time; this projection only closes
        /// the cross-owner ordering boundary required by the branch phase.
        /// </summary>
        public static ContractValidationResult<IReadOnlyList<P6T06QueueStateV1>> TryOrderBranchQueues(
            IEnumerable<P6T06QueueStateV1>? queues)
        {
            if (queues == null)
            {
                return P6T06QueueValidationV1.Invalid<IReadOnlyList<P6T06QueueStateV1>>(
                    "P6T06.BranchQueue.Input.Missing",
                    "queues",
                    "The branch phase requires an explicit queue collection.");
            }

            P6T06QueueStateV1[] input = queues.ToArray();
            if (input.Any(queue => queue == null))
            {
                return P6T06QueueValidationV1.Invalid<IReadOnlyList<P6T06QueueStateV1>>(
                    "P6T06.BranchQueue.Input.Null",
                    "queues",
                    "The branch phase cannot order a null queue.");
            }

            if (input.Any(queue => queue.Family != P6T06QueueFamilyV1.LiquidZone &&
                                   queue.Family != P6T06QueueFamilyV1.Adjuster))
            {
                return P6T06QueueValidationV1.Invalid<IReadOnlyList<P6T06QueueStateV1>>(
                    "P6T06.BranchQueue.Family.Invalid",
                    "queues",
                    "Only liquid-zone and adjuster queues participate in the rank-4 branch order.");
            }

            HashSet<StableId> queueIds = new HashSet<StableId>();
            if (input.Any(queue => !queueIds.Add(queue.QueueId)))
            {
                return P6T06QueueValidationV1.Invalid<IReadOnlyList<P6T06QueueStateV1>>(
                    "P6T06.BranchQueue.Identity.Duplicate",
                    "queues",
                    "A branch queue identity may occur only once in the same rank-4 boundary.");
            }

            P6T06QueueStateV1[] ordered = input
                .OrderBy(queue => queue.OwnerKey.EntityKindOrdinal)
                .ThenBy(queue => queue.OwnerKey.EntityId)
                .ThenBy(queue => queue.QueueId)
                .ToArray();
            return ContractValidationResult<IReadOnlyList<P6T06QueueStateV1>>.Valid(
                new ReadOnlyCollection<P6T06QueueStateV1>(ordered));
        }

        public ContractValidationResult<P6T06EnqueueResultV1> TryEnqueueBatch(
            P6T06SourceBindingTokenV1? sourceBinding,
            double enqueueTimeSeconds,
            P6T06QueuePhaseTokenV1? phaseToken,
            IEnumerable<P6T06CommandCandidateV1>? candidates)
        {
            if (phaseToken == null ||
                !phaseToken.IsValidFor(QueueId, QueueDigest, enqueueTimeSeconds))
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.PhaseToken.Invalid",
                    "phase_token",
                    "Enqueue requires a phase token bound to this queue digest and exact event time.");
            }

            if (phaseToken.DueBatchCutoffClosed)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.CutoffClosed",
                    "enqueue_phase",
                    "No command may be admitted after the same-time due-batch cutoff.");
            }

            if (sourceBinding == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Binding.Invalid",
                    "source_event",
                    "Enqueue requires an adapter-validated source event and frozen state-binding token.");
            }

            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(enqueueTimeSeconds) ||
                enqueueTimeSeconds < LastMotionTimeSeconds)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Time.Invalid",
                    "enqueue_time_s",
                    "Enqueue time must be canonical and not precede the queue physical-state time.");
            }

            if (AppliedSourceEventIds.Contains(sourceBinding.SourceEventId))
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.SourceEvent.Reuse",
                    "source_event_id",
                    "A source event may be committed to one owner queue only once.");
            }

            if (candidates == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Candidates.Missing",
                    "candidates",
                    "An atomic enqueue requires an explicit nonempty candidate batch.");
            }

            P6T06CommandCandidateV1[] input = candidates.ToArray();
            if (input.Length == 0)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Candidates.Empty",
                    "candidates",
                    "An atomic enqueue cannot commit an empty command batch.");
            }

            if (input.Any(candidate => candidate == null))
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Candidates.Null",
                    "candidates",
                    "An atomic enqueue cannot contain a null candidate.");
            }

            P6T06CommandCandidateV1[] canonical = input
                .OrderBy(candidate => candidate.Target)
                .ToArray();
            for (int index = 0; index < canonical.Length; index++)
            {
                P6T06CommandCandidateV1 candidate = canonical[index];
                if (candidate.Family != Family)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.Enqueue.Family.Mismatch",
                        "candidates[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every candidate must belong to the owning queue family.");
                }

                if (index > 0 && canonical[index - 1].Target.Equals(candidate.Target))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.Enqueue.Target.Duplicate",
                        "candidates",
                        "A source event may contain at most one command for each target.");
                }

                P6T06AvailableCommandV1? available = AvailableCommands.SingleOrDefault(
                    item => item.Target.Equals(candidate.Target));
                if (available == null ||
                    available.LowerBound != candidate.LowerBound ||
                    available.UpperBound != candidate.UpperBound ||
                    available.RateLimitPerSecond != candidate.RateLimitPerSecond)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.Enqueue.MotionContract.Mismatch",
                        "candidates[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Command bounds and rate must equal the owning queue's self-contained motion contract.");
                }
            }

            ulong count = (ulong)canonical.Length;
            if (count > ulong.MaxValue - NextSequence ||
                (ulong)AllocatedCommandIds.Count + count > uint.MaxValue ||
                (ulong)AppliedSourceEventIds.Count + 1UL > uint.MaxValue)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.Enqueue.Capacity",
                    "allocator",
                    "The complete batch must fit the representable UInt64 allocator and UInt32 registries.");
            }

            List<P6T06QueueCommandV1> commands = new List<P6T06QueueCommandV1>(canonical.Length);
            for (int index = 0; index < canonical.Length; index++)
            {
                P6T06CommandCandidateV1 candidate = canonical[index];
                ulong sequence = NextSequence + (ulong)index;
                double dueTime = enqueueTimeSeconds + candidate.DelaySeconds;
                ContractValidationResult<P6T06QueueCommandV1> command = P6T06QueueCommandV1.TryCreate(
                    Family,
                    QueueId,
                    null,
                    candidate.Target,
                    OwnerKey,
                    candidate.SourceKind,
                    sourceBinding.SourceEventId,
                    sourceBinding.BindingDigest,
                    enqueueTimeSeconds,
                    candidate.DelaySeconds,
                    dueTime,
                    candidate.EventRank,
                    sequence,
                    candidate.RequestedCommand,
                    candidate.BoundedCommand,
                    candidate.LowerBound,
                    candidate.UpperBound,
                    candidate.RateLimitPerSecond);
                if (!command.IsValid)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        command.FirstDiagnostic.Code,
                        command.FirstDiagnostic.Path,
                        command.FirstDiagnostic.Message);
                }

                if (AllocatedCommandIds.Contains(command.Value.CommandId) ||
                    commands.Any(existing => existing.CommandId == command.Value.CommandId))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.Enqueue.CommandId.Reuse",
                        "candidates[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "A deterministic command identity may occur only once in the owner history.");
                }

                commands.Add(command.Value);
            }

            List<StableId> nextApplied = AppliedSourceEventIds
                .Concat(new[] { sourceBinding.SourceEventId })
                .OrderBy(id => id)
                .ToList();
            List<StableId> nextAllocated = AllocatedCommandIds
                .Concat(commands.Select(command => command.CommandId))
                .OrderBy(id => id)
                .ToList();
            P6T06QueueCommandV1[] nextPending = PendingCommands
                .Concat(commands)
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray();
            ContractValidationResult<P6T06QueueStateV1> nextQueue = TryCreate(
                Family,
                QueueId,
                OwnerKey,
                GenerationCadenceOrNA,
                InitialNextSequence,
                NextSequence + count,
                LastMotionTimeSeconds,
                AvailableCommands,
                nextPending,
                nextApplied,
                nextAllocated);
            if (!nextQueue.IsValid)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    nextQueue.FirstDiagnostic.Code,
                    nextQueue.FirstDiagnostic.Path,
                    nextQueue.FirstDiagnostic.Message);
            }

            P6T06SaturationDiagnosticV1[] saturation = commands
                .Select(command => P6T06SaturationDiagnosticV1.Create(command))
                .ToArray();
            P6T06QueueTransitionV1 transition = P6T06QueueTransitionV1.CreateEnqueue(
                Family,
                QueueId,
                OwnerKey,
                enqueueTimeSeconds,
                QueueDigest,
                nextQueue.Value.QueueDigest,
                NextSequence,
                nextQueue.Value.NextSequence,
                commands,
                AvailableCommands,
                AvailableCommands);
            return ContractValidationResult<P6T06EnqueueResultV1>.Valid(
                new P6T06EnqueueResultV1(nextQueue.Value, commands, saturation, transition));
        }

        /// <summary>
        /// Owns the RRS projection-to-queue boundary. It validates the event
        /// identity against the projection and queue, translates every
        /// projection command into one P6-T06 candidate, and then delegates to
        /// the existing atomic queue transaction. Source-state binding remains
        /// caller-validated and is preserved exactly; this method does not
        /// invent a measurement, equation, or digest.
        /// </summary>
        public ContractValidationResult<P6T06EnqueueResultV1> TryEnqueueRrsControllerProjection(
            RrsControllerProjectionV1? projection,
            P6T06RrsControllerEventIdentityV1? eventIdentity,
            P6T06QueuePhaseTokenV1? phaseToken)
        {
            if (Family != P6T06QueueFamilyV1.Rrs)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.RrsProjection.Queue.FamilyMismatch",
                    "queue.family",
                    "RRS controller projections may be admitted only to an RRS queue.");
            }

            if (projection == null || eventIdentity == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.RrsProjection.Input.Missing",
                    "projection_event",
                    "A validated RRS projection and controller event identity are required.");
            }

            if (!OwnerKey.IsCompatibleWith(P6T06QueueFamilyV1.Rrs) ||
                OwnerKey.EntityId != eventIdentity.ControllerId)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.RrsProjection.Queue.OwnerMismatch",
                    "queue.owner_key",
                    "The RRS queue owner must equal the controller event identity.");
            }

            if (!eventIdentity.ProjectionDigest.Equals(projection.ProjectionDigest))
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.RrsProjection.Event.ProjectionDigestMismatch",
                    "event_identity.projection_digest",
                    "The controller event must admit the exact validated projection digest.");
            }

            P6T06SourceKindV1 sourceKind;
            switch (projection.Mode)
            {
                case RrsModeV1.Automatic:
                    sourceKind = P6T06SourceKindV1.Controller;
                    break;
                case RrsModeV1.Manual:
                    sourceKind = P6T06SourceKindV1.Manual;
                    break;
                case RrsModeV1.Held:
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.RrsProjection.Mode.Held",
                        "projection.mode",
                        "Held projections preserve the current command and do not generate queue admissions.");
                default:
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.RrsProjection.Mode.Invalid",
                        "projection.mode",
                        "The controller projection mode is not part of the closed RRS mode set.");
            }

            if (projection.Commands == null || projection.Commands.Count == 0)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    "P6T06.RrsProjection.Commands.Empty",
                    "projection.commands",
                    "A controller projection must contain at least one command for admission.");
            }

            List<P6T06CommandCandidateV1> candidates = new List<P6T06CommandCandidateV1>(
                projection.Commands.Count);
            for (int index = 0; index < projection.Commands.Count; index++)
            {
                RrsCommandProjectionV1 command = projection.Commands[index];
                if (command == null)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.RrsProjection.Command.Null",
                        "projection.commands[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "A controller projection cannot contain a null command.");
                }

                ContractValidationResult<P6T06TargetKeyV1> target =
                    P6T06TargetKeyV1.TryForRrsActuator(command.ActuatorId);
                if (!target.IsValid)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        target.FirstDiagnostic.Code,
                        target.FirstDiagnostic.Path,
                        target.FirstDiagnostic.Message);
                }

                ContractValidationResult<P6T06CommandCandidateV1> candidate =
                    P6T06CommandCandidateV1.TryCreate(
                        P6T06QueueFamilyV1.Rrs,
                        target.Value,
                        sourceKind,
                        EventRankV1.ControllerCommandGeneration,
                        command.DelaySeconds,
                        command.RequestedCommand,
                        command.LowerBound,
                        command.UpperBound,
                        command.RateLimitPerSecond);
                if (!candidate.IsValid)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        candidate.FirstDiagnostic.Code,
                        candidate.FirstDiagnostic.Path,
                        candidate.FirstDiagnostic.Message);
                }

                if (candidate.Value.BoundedCommand != command.BoundedCommand)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                        "P6T06.RrsProjection.Command.BoundedMismatch",
                        "projection.commands[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "The queue candidate must preserve the projection's bounded command exactly.");
                }

                candidates.Add(candidate.Value);
            }

            ContractValidationResult<P6T06SourceBindingTokenV1> sourceBinding =
                P6T06SourceBindingTokenV1.TryCreate(
                    eventIdentity.SourceEventId,
                    eventIdentity.SourceBindingDigest);
            if (!sourceBinding.IsValid)
            {
                return P6T06QueueValidationV1.Invalid<P6T06EnqueueResultV1>(
                    sourceBinding.FirstDiagnostic.Code,
                    sourceBinding.FirstDiagnostic.Path,
                    sourceBinding.FirstDiagnostic.Message);
            }

            return TryEnqueueBatch(
                sourceBinding.Value,
                projection.CurrentTimeSeconds,
                phaseToken,
                candidates);
        }

        public ContractValidationResult<P6T06MotionResultV1> TryMotionAndConsume(
            double effectiveTimeSeconds,
            P6T06MotionModeV1 mode,
            IEnumerable<P6T06ActuatorStateV1>? actuatorStates,
            IEnumerable<double>? exactSubsteps)
        {
            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(effectiveTimeSeconds) ||
                effectiveTimeSeconds < LastMotionTimeSeconds)
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.Time.Invalid",
                    "effective_time_s",
                    "Motion may not move backward from the queue's physical-state time.");
            }

            if (!Enum.IsDefined(typeof(P6T06MotionModeV1), mode) ||
                !P6T06QueueValidationV1.IsModeAllowed(Family, mode))
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.Mode.Invalid",
                    "mode",
                    "The motion mode is not valid for the owning queue family.");
            }

            if (actuatorStates == null || exactSubsteps == null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.Input.Missing",
                    "motion_input",
                    "Complete actuator states and an explicit stability-policy partition are required.");
            }

            P6T06ActuatorStateV1[] stateInput = actuatorStates.ToArray();
            if (stateInput.Any(state => state == null) || stateInput.Length != AvailableCommands.Count)
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.State.Complete",
                    "actuator_states",
                    "Motion requires exactly one supplied physical state for every available target.");
            }

            P6T06ActuatorStateV1[] states = stateInput.OrderBy(state => state.Target).ToArray();
            P6T06AvailableCommandV1[] available = AvailableCommands.OrderBy(item => item.Target).ToArray();
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].Family != Family ||
                    !states[index].Target.Equals(available[index].Target) ||
                    states[index].RateLimitPerSecond != available[index].RateLimitPerSecond ||
                    states[index].PhysicalState < available[index].LowerBound ||
                    states[index].PhysicalState > available[index].UpperBound)
                {
                    return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                        "P6T06.Motion.State.BindingMismatch",
                        "actuator_states[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Physical state, target, rate, and queue bounds must match the complete owner contract.");
                }
            }

            double deltaTime = effectiveTimeSeconds - LastMotionTimeSeconds;
            List<double> substeps = exactSubsteps.ToList();
            if (!P6T06QueueValidationV1.IsCanonicalNonnegative(deltaTime))
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.Delta.Invalid",
                    "delta_time_s",
                    "The elapsed motion interval must be finite and canonical.");
            }

            double partitionSum = 0.0;
            for (int index = 0; index < substeps.Count; index++)
            {
                if (!P6T06QueueValidationV1.IsCanonicalPositive(substeps[index]))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                        "P6T06.Motion.Substep.Invalid",
                        "substeps[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every supplied positive-duration substep must be finite, positive, and canonical.");
                }

                partitionSum += substeps[index];
                if (!P6T06QueueValidationV1.IsCanonicalFinite(partitionSum))
                {
                    return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                        "P6T06.Motion.Substep.Overflow",
                        "substeps",
                        "The explicit substep partition is not representable in binary64 time.");
                }
            }

            if (partitionSum != deltaTime || (deltaTime > 0.0 && substeps.Count == 0))
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.Substep.PartitionMismatch",
                    "substeps",
                    "The supplied substeps must sum exactly to the elapsed physical-state interval.");
            }

            P6T06QueueCommandV1[] dueBatch = PendingCommands
                .Where(command => command.DueTimeSeconds <= effectiveTimeSeconds)
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray();
            P6T06QueueCommandV1? missed = PendingCommands.FirstOrDefault(
                command => command.DueTimeSeconds < effectiveTimeSeconds);
            if (missed != null)
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    "P6T06.Motion.DueTime.Missed",
                    "pending_commands",
                    "The caller must split the explicit event advance at every exact due-time boundary.");
            }

            Dictionary<P6T06TargetKeyV1, P6T06QueueCommandV1> finalDue = new Dictionary<P6T06TargetKeyV1, P6T06QueueCommandV1>();
            foreach (P6T06QueueCommandV1 command in dueBatch)
            {
                finalDue[command.Target] = command;
            }

            List<P6T06ActuatorStateV1> nextStates = new List<P6T06ActuatorStateV1>(states.Length);
            List<P6T06MotionDiagnosticV1> motionDiagnostics = new List<P6T06MotionDiagnosticV1>(states.Length);
            bool motionIsActive = P6T06QueueValidationV1.MotionIsActive(mode);
            for (int index = 0; index < states.Length; index++)
            {
                P6T06ActuatorStateV1 state = states[index];
                P6T06AvailableCommandV1 availableBefore = available[index];
                double proposed = state.PhysicalState;
                if (motionIsActive)
                {
                    foreach (double substep in substeps)
                    {
                        double difference = availableBefore.BoundedCommand - proposed;
                        if (!P6T06QueueValidationV1.IsCanonicalFinite(difference))
                        {
                            return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                                "P6T06.Motion.Delta.Invalid",
                                "actuator_states[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                                "Available-command minus physical-state motion delta must be finite.");
                        }

                        double travelLimit = availableBefore.RateLimitPerSecond * substep;
                        if (!P6T06QueueValidationV1.IsCanonicalFinite(travelLimit))
                        {
                            return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                                "P6T06.Motion.Travel.Invalid",
                                "actuator_states[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                                "Rate-limited travel must remain finite and representable.");
                        }

                        double travel = Math.Min(Math.Abs(difference), travelLimit);
                        if (difference > 0.0)
                        {
                            proposed += travel;
                        }
                        else if (difference < 0.0)
                        {
                            proposed -= travel;
                        }

                        if (!P6T06QueueValidationV1.IsCanonicalFinite(proposed) ||
                            proposed < availableBefore.LowerBound ||
                            proposed > availableBefore.UpperBound)
                        {
                            return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                                "P6T06.Motion.State.Invalid",
                                "actuator_states[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                                "The causal rate-limited physical state left its explicit bounds.");
                        }
                    }
                }

                P6T06AvailableCommandV1 availableAfter = finalDue.TryGetValue(
                    state.Target,
                    out P6T06QueueCommandV1? lastDue)
                    ? availableBefore.WithBoundedCommand(lastDue.BoundedCommand)
                    : availableBefore;
                P6T06ActuatorStateV1 nextState = new P6T06ActuatorStateV1(
                    Family,
                    state.Target,
                    proposed,
                    state.RateLimitPerSecond);
                nextStates.Add(nextState);
                motionDiagnostics.Add(
                    P6T06MotionDiagnosticV1.Create(
                        QueueId,
                        Family,
                        state.Target,
                        state.PhysicalState,
                        proposed,
                        availableBefore,
                        availableAfter,
                        LastMotionTimeSeconds,
                        effectiveTimeSeconds,
                        deltaTime,
                        dueBatch.Where(command => command.Target.Equals(state.Target))
                            .Select(command => command.CommandId)
                            .ToArray()));
            }

            P6T06QueueCommandV1[] remaining = PendingCommands
                .Where(command => !dueBatch.Contains(command))
                .ToArray();
            P6T06AvailableCommandV1[] nextAvailable = available
                .Select(item => finalDue.TryGetValue(item.Target, out P6T06QueueCommandV1? lastDue)
                    ? item.WithBoundedCommand(lastDue.BoundedCommand)
                    : item)
                .ToArray();
            ContractValidationResult<P6T06QueueStateV1> nextQueue = TryCreate(
                Family,
                QueueId,
                OwnerKey,
                GenerationCadenceOrNA,
                InitialNextSequence,
                NextSequence,
                effectiveTimeSeconds,
                nextAvailable,
                remaining,
                AppliedSourceEventIds,
                AllocatedCommandIds);
            if (!nextQueue.IsValid)
            {
                return P6T06QueueValidationV1.Invalid<P6T06MotionResultV1>(
                    nextQueue.FirstDiagnostic.Code,
                    nextQueue.FirstDiagnostic.Path,
                    nextQueue.FirstDiagnostic.Message);
            }

            P6T06QueueTransitionV1 transition = P6T06QueueTransitionV1.CreateMotionAndConsume(
                Family,
                QueueId,
                OwnerKey,
                effectiveTimeSeconds,
                QueueDigest,
                nextQueue.Value.QueueDigest,
                NextSequence,
                nextQueue.Value.NextSequence,
                LastMotionTimeSeconds,
                effectiveTimeSeconds,
                effectiveTimeSeconds,
                dueBatch,
                available,
                nextAvailable);
            return ContractValidationResult<P6T06MotionResultV1>.Valid(
                new P6T06MotionResultV1(
                    nextQueue.Value,
                    nextStates,
                    dueBatch,
                    motionDiagnostics,
                    transition));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                Family,
                QueueId,
                OwnerKey,
                GenerationCadenceOrNA,
                InitialNextSequence,
                NextSequence,
                LastMotionTimeSeconds,
                AvailableCommands,
                PendingCommands,
                AppliedSourceEventIds,
                AllocatedCommandIds,
                QueueDigest);
        }

        private static byte[] BuildBytes(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            double? generationCadenceOrNA,
            ulong initialNextSequence,
            ulong nextSequence,
            double lastMotionTimeSeconds,
            IReadOnlyList<P6T06AvailableCommandV1> availableCommands,
            IReadOnlyList<P6T06QueueCommandV1> pendingCommands,
            IReadOnlyList<StableId> appliedSourceEventIds,
            IReadOnlyList<StableId> allocatedCommandIds,
            Digest32? queueDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(
                    writer,
                    P6T06QueueValidationV1.QueueMagic(family));
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteBytes(writer, queueId.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteBytesRaw(writer, ownerKey.ToCanonicalBytes());
                P6T06QueueValidationV1.WriteOptionalDouble(writer, generationCadenceOrNA);
                Phase5CanonicalBytesV1.WriteUInt64(writer, initialNextSequence);
                Phase5CanonicalBytesV1.WriteUInt64(writer, nextSequence);
                Phase5CanonicalBytesV1.WriteDouble(writer, lastMotionTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)availableCommands.Count));
                foreach (P6T06AvailableCommandV1 available in availableCommands.OrderBy(item => item.Target))
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, available.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)pendingCommands.Count));
                foreach (P6T06QueueCommandV1 command in pendingCommands
                    .OrderBy(item => item.DueTimeSeconds)
                    .ThenBy(item => item.EventRank)
                    .ThenBy(item => item.Sequence)
                    .ThenBy(item => item.CommandId))
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)appliedSourceEventIds.Count));
                foreach (StableId id in appliedSourceEventIds.OrderBy(id => id))
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)allocatedCommandIds.Count));
                foreach (StableId id in allocatedCommandIds.OrderBy(id => id))
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, id);
                }

                if (queueDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, queueDigest);
                }
            });
        }
    }

    /// <summary>
    /// Physical state supplied to the queue transition. It is separate from
    /// both the requested command and the queue's available command.
    /// </summary>
    public sealed class P6T06ActuatorStateV1
    {
        internal P6T06ActuatorStateV1(
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1 target,
            double physicalState,
            double rateLimitPerSecond)
        {
            Family = family;
            Target = target;
            PhysicalState = physicalState;
            RateLimitPerSecond = rateLimitPerSecond;
        }

        public P6T06QueueFamilyV1 Family { get; }

        public P6T06TargetKeyV1 Target { get; }

        public double PhysicalState { get; }

        public double RateLimitPerSecond { get; }

        public static ContractValidationResult<P6T06ActuatorStateV1> TryCreate(
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1? target,
            double physicalState,
            double rateLimitPerSecond)
        {
            if (!P6T06QueueValidationV1.IsKnownFamily(family) ||
                target == null || !target.IsCompatibleWith(family) ||
                !P6T06QueueValidationV1.IsCanonicalFinite(physicalState) ||
                !P6T06QueueValidationV1.IsCanonicalNonnegative(rateLimitPerSecond))
            {
                return P6T06QueueValidationV1.Invalid<P6T06ActuatorStateV1>(
                    "P6T06.ActuatorState.Invalid",
                    "actuator_state",
                    "A physical state requires a compatible target and finite canonical state/rate values.");
            }

            return ContractValidationResult<P6T06ActuatorStateV1>.Valid(
                new P6T06ActuatorStateV1(family, target, physicalState, rateLimitPerSecond));
        }
    }

    /// <summary>
    /// Explicit saturation evidence attached to one committed command.
    /// </summary>
    public sealed class P6T06SaturationDiagnosticV1
    {
        private P6T06SaturationDiagnosticV1(P6T06QueueCommandV1 command)
        {
            CommandId = command.CommandId;
            SourceEventId = command.SourceEventId;
            RequestedCommand = command.RequestedCommand;
            BoundedCommand = command.BoundedCommand;
            SaturationState = command.SaturationState;
            LowerBound = command.LowerBound;
            UpperBound = command.UpperBound;
            RateLimitPerSecond = command.RateLimitPerSecond;
            DelaySeconds = command.DelaySeconds;
            Saturated = command.IsSaturated;
        }

        public StableId CommandId { get; }

        public StableId SourceEventId { get; }

        /// <summary>
        /// The frozen SaturationBodyV1 EventId projection is the committed
        /// command identity. SourceEventId remains available as provenance
        /// on the enclosing queue command but is not a second saturation-body
        /// field.
        /// </summary>
        public StableId EventId => CommandId;

        public double RequestedCommand { get; }

        public double BoundedCommand { get; }

        public P6T06SaturationStateV1 SaturationState { get; }

        public double LowerBound { get; }

        public double UpperBound { get; }

        public double RateLimitPerSecond { get; }

        public double DelaySeconds { get; }

        public bool Saturated { get; }

        internal static P6T06SaturationDiagnosticV1 Create(P6T06QueueCommandV1 command)
        {
            return new P6T06SaturationDiagnosticV1(command);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, RequestedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, BoundedCommand);
                writer.Write((byte)SaturationState);
                Phase5CanonicalBytesV1.WriteDouble(writer, LowerBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, UpperBound);
                Phase5CanonicalBytesV1.WriteDouble(writer, RateLimitPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, DelaySeconds);
                writer.Write(Saturated ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteStableId(writer, EventId);
            });
        }
    }

    /// <summary>
    /// One per-target physical transition diagnostic. It records the command
    /// available over the elapsed interval and the command that becomes
    /// available only at the effective boundary.
    /// </summary>
    public sealed class P6T06MotionDiagnosticV1
    {
        private P6T06MotionDiagnosticV1(
            StableId queueId,
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1 target,
            double previousState,
            double nextState,
            P6T06AvailableCommandV1 availableBefore,
            P6T06AvailableCommandV1 availableAfter,
            double motionStartTimeSeconds,
            double effectiveTimeSeconds,
            double deltaTimeSeconds,
            IEnumerable<StableId> consumedCommandIds)
        {
            QueueId = queueId;
            Family = family;
            Target = target;
            PreviousState = previousState;
            NextState = nextState;
            AvailableBefore = availableBefore;
            AvailableAfter = availableAfter;
            MotionStartTimeSeconds = motionStartTimeSeconds;
            EffectiveTimeSeconds = effectiveTimeSeconds;
            DeltaTimeSeconds = deltaTimeSeconds;
            ConsumedCommandIds = new ReadOnlyCollection<StableId>(consumedCommandIds.ToArray());
            StateDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(BuildBytes(null)));
        }

        public StableId QueueId { get; }

        public P6T06QueueFamilyV1 Family { get; }

        public P6T06TargetKeyV1 Target { get; }

        public double PreviousState { get; }

        public double NextState { get; }

        public P6T06AvailableCommandV1 AvailableBefore { get; }

        public P6T06AvailableCommandV1 AvailableAfter { get; }

        public double MotionStartTimeSeconds { get; }

        public double EffectiveTimeSeconds { get; }

        public double DeltaTimeSeconds { get; }

        public IReadOnlyList<StableId> ConsumedCommandIds { get; }

        public Digest32 StateDigest { get; }

        internal static P6T06MotionDiagnosticV1 Create(
            StableId queueId,
            P6T06QueueFamilyV1 family,
            P6T06TargetKeyV1 target,
            double previousState,
            double nextState,
            P6T06AvailableCommandV1 availableBefore,
            P6T06AvailableCommandV1 availableAfter,
            double motionStartTimeSeconds,
            double effectiveTimeSeconds,
            double deltaTimeSeconds,
            IEnumerable<StableId> consumedCommandIds)
        {
            return new P6T06MotionDiagnosticV1(
                queueId,
                family,
                target,
                previousState,
                nextState,
                availableBefore,
                availableAfter,
                motionStartTimeSeconds,
                effectiveTimeSeconds,
                deltaTimeSeconds,
                consumedCommandIds);
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(StateDigest);
        }

        private byte[] BuildBytes(Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, QueueId.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteBytes(writer, Target.ToTargetBytes());
                Phase5CanonicalBytesV1.WriteDouble(writer, PreviousState);
                Phase5CanonicalBytesV1.WriteDouble(writer, NextState);
                Phase5CanonicalBytesV1.WriteDouble(writer, AvailableBefore.BoundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, AvailableAfter.BoundedCommand);
                Phase5CanonicalBytesV1.WriteDouble(writer, MotionStartTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, EffectiveTimeSeconds);
                Phase5CanonicalBytesV1.WriteDouble(writer, DeltaTimeSeconds);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)ConsumedCommandIds.Count));
                foreach (StableId commandId in ConsumedCommandIds)
                {
                    Phase5CanonicalBytesV1.WriteStableId(writer, commandId);
                }

                Phase5CanonicalBytesV1.WriteUInt16(
                    writer,
                    Family == P6T06QueueFamilyV1.Rrs
                        ? (ushort)EventRankV1.ActuatorMotion
                        : (ushort)EventRankV1.BranchUpdate);
                writer.Write((byte)0);
                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }
    }

    /// <summary>
    /// QueueBodyV1 projection for one atomic enqueue or motion/consume commit.
    /// It carries both queue digests and the before/after available arrays.
    /// </summary>
    public sealed class P6T06QueueTransitionV1
    {
        private P6T06QueueTransitionV1(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            P6T06QueueTransitionKindV1 transitionKind,
            double eventTimeSeconds,
            Digest32 queueBeforeDigest,
            Digest32 queueAfterDigest,
            ulong nextSequenceBefore,
            ulong nextSequenceAfter,
            double? motionStartTimeOrNA,
            double? motionEndTimeOrNA,
            double? dueBatchCutoffTimeOrNA,
            IEnumerable<P6T06QueueCommandV1> commands,
            IEnumerable<P6T06AvailableCommandV1> availableBefore,
            IEnumerable<P6T06AvailableCommandV1> availableAfter)
        {
            Family = family;
            QueueId = queueId;
            OwnerKey = ownerKey;
            TransitionKind = transitionKind;
            EventTimeSeconds = eventTimeSeconds;
            QueueBeforeDigest = queueBeforeDigest;
            QueueAfterDigest = queueAfterDigest;
            NextSequenceBefore = nextSequenceBefore;
            NextSequenceAfter = nextSequenceAfter;
            MotionStartTimeOrNA = motionStartTimeOrNA;
            MotionEndTimeOrNA = motionEndTimeOrNA;
            DueBatchCutoffTimeOrNA = dueBatchCutoffTimeOrNA;
            Commands = new ReadOnlyCollection<P6T06QueueCommandV1>(commands
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray());
            AvailableBefore = new ReadOnlyCollection<P6T06AvailableCommandV1>(availableBefore.OrderBy(item => item.Target).ToArray());
            AvailableAfter = new ReadOnlyCollection<P6T06AvailableCommandV1>(availableAfter.OrderBy(item => item.Target).ToArray());
        }

        public P6T06QueueFamilyV1 Family { get; }

        public StableId QueueId { get; }

        public P6T06OwnerKeyV1 OwnerKey { get; }

        public P6T06QueueTransitionKindV1 TransitionKind { get; }

        public double EventTimeSeconds { get; }

        public Digest32 QueueBeforeDigest { get; }

        public Digest32 QueueAfterDigest { get; }

        public ulong NextSequenceBefore { get; }

        public ulong NextSequenceAfter { get; }

        public double? MotionStartTimeOrNA { get; }

        public double? MotionEndTimeOrNA { get; }

        public double? DueBatchCutoffTimeOrNA { get; }

        public IReadOnlyList<P6T06QueueCommandV1> Commands { get; }

        public IReadOnlyList<P6T06AvailableCommandV1> AvailableBefore { get; }

        public IReadOnlyList<P6T06AvailableCommandV1> AvailableAfter { get; }

        internal static P6T06QueueTransitionV1 CreateEnqueue(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            double eventTimeSeconds,
            Digest32 queueBeforeDigest,
            Digest32 queueAfterDigest,
            ulong nextSequenceBefore,
            ulong nextSequenceAfter,
            IEnumerable<P6T06QueueCommandV1> commands,
            IEnumerable<P6T06AvailableCommandV1> availableBefore,
            IEnumerable<P6T06AvailableCommandV1> availableAfter)
        {
            return new P6T06QueueTransitionV1(
                family,
                queueId,
                ownerKey,
                P6T06QueueTransitionKindV1.Enqueue,
                eventTimeSeconds,
                queueBeforeDigest,
                queueAfterDigest,
                nextSequenceBefore,
                nextSequenceAfter,
                null,
                null,
                null,
                commands,
                availableBefore,
                availableAfter);
        }

        internal static P6T06QueueTransitionV1 CreateMotionAndConsume(
            P6T06QueueFamilyV1 family,
            StableId queueId,
            P6T06OwnerKeyV1 ownerKey,
            double eventTimeSeconds,
            Digest32 queueBeforeDigest,
            Digest32 queueAfterDigest,
            ulong nextSequenceBefore,
            ulong nextSequenceAfter,
            double motionStartTimeSeconds,
            double motionEndTimeSeconds,
            double dueBatchCutoffTimeSeconds,
            IEnumerable<P6T06QueueCommandV1> commands,
            IEnumerable<P6T06AvailableCommandV1> availableBefore,
            IEnumerable<P6T06AvailableCommandV1> availableAfter)
        {
            return new P6T06QueueTransitionV1(
                family,
                queueId,
                ownerKey,
                P6T06QueueTransitionKindV1.MotionAndConsume,
                eventTimeSeconds,
                queueBeforeDigest,
                queueAfterDigest,
                nextSequenceBefore,
                nextSequenceAfter,
                motionStartTimeSeconds,
                motionEndTimeSeconds,
                dueBatchCutoffTimeSeconds,
                commands,
                availableBefore,
                availableAfter);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-QUEUE-BODY-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteBytes(writer, QueueId.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteBytesRaw(writer, OwnerKey.ToCanonicalBytes());
                writer.Write((byte)TransitionKind);
                Phase5CanonicalBytesV1.WriteDouble(writer, EventTimeSeconds);
                Phase5CanonicalBytesV1.WriteDigest(writer, QueueBeforeDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, QueueAfterDigest);
                Phase5CanonicalBytesV1.WriteUInt64(writer, NextSequenceBefore);
                Phase5CanonicalBytesV1.WriteUInt64(writer, NextSequenceAfter);
                P6T06QueueValidationV1.WriteOptionalDouble(writer, MotionStartTimeOrNA);
                P6T06QueueValidationV1.WriteOptionalDouble(writer, MotionEndTimeOrNA);
                P6T06QueueValidationV1.WriteOptionalDouble(writer, DueBatchCutoffTimeOrNA);
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)Commands.Count));
                foreach (P6T06QueueCommandV1 command in Commands
                    .OrderBy(command => command.DueTimeSeconds)
                    .ThenBy(command => command.EventRank)
                    .ThenBy(command => command.Sequence)
                    .ThenBy(command => command.CommandId))
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, command.ToCanonicalBytes());
                }

                WriteAvailable(writer, AvailableBefore);
                WriteAvailable(writer, AvailableAfter);
            });
        }

        private static void WriteAvailable(
            BinaryWriter writer,
            IReadOnlyList<P6T06AvailableCommandV1> values)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
            foreach (P6T06AvailableCommandV1 value in values.OrderBy(item => item.Target))
            {
                Phase5CanonicalBytesV1.WriteBytes(writer, value.ToCanonicalBytes());
            }
        }
    }

    public sealed class P6T06EnqueueResultV1
    {
        internal P6T06EnqueueResultV1(
            P6T06QueueStateV1 queue,
            IEnumerable<P6T06QueueCommandV1> commands,
            IEnumerable<P6T06SaturationDiagnosticV1> saturationDiagnostics,
            P6T06QueueTransitionV1 transition)
        {
            Queue = queue;
            Commands = new ReadOnlyCollection<P6T06QueueCommandV1>(commands
                .OrderBy(command => command.DueTimeSeconds)
                .ThenBy(command => command.EventRank)
                .ThenBy(command => command.Sequence)
                .ThenBy(command => command.CommandId)
                .ToArray());
            SaturationDiagnostics = new ReadOnlyCollection<P6T06SaturationDiagnosticV1>(saturationDiagnostics
                .OrderBy(diagnostic => diagnostic.EventId)
                .ToArray());
            Transition = transition;
        }

        public P6T06QueueStateV1 Queue { get; }

        public IReadOnlyList<P6T06QueueCommandV1> Commands { get; }

        public IReadOnlyList<P6T06SaturationDiagnosticV1> SaturationDiagnostics { get; }

        public P6T06QueueTransitionV1 Transition { get; }
    }

    public sealed class P6T06MotionResultV1
    {
        internal P6T06MotionResultV1(
            P6T06QueueStateV1 queue,
            IEnumerable<P6T06ActuatorStateV1> actuatorStates,
            IEnumerable<P6T06QueueCommandV1> consumedCommands,
            IEnumerable<P6T06MotionDiagnosticV1> motionDiagnostics,
            P6T06QueueTransitionV1 transition)
        {
            Queue = queue;
            ActuatorStates = new ReadOnlyCollection<P6T06ActuatorStateV1>(actuatorStates.ToArray());
            ConsumedCommands = new ReadOnlyCollection<P6T06QueueCommandV1>(consumedCommands.ToArray());
            MotionDiagnostics = new ReadOnlyCollection<P6T06MotionDiagnosticV1>(motionDiagnostics.ToArray());
            Transition = transition;
        }

        public P6T06QueueStateV1 Queue { get; }

        public IReadOnlyList<P6T06ActuatorStateV1> ActuatorStates { get; }

        public IReadOnlyList<P6T06QueueCommandV1> ConsumedCommands { get; }

        public IReadOnlyList<P6T06MotionDiagnosticV1> MotionDiagnostics { get; }

        public P6T06QueueTransitionV1 Transition { get; }
    }
}
