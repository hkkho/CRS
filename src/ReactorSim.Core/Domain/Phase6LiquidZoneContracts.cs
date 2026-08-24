using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// The closed v1 update modes for one independently tracked liquid-zone
    /// state. This contract does not advance a zone or evaluate an influence
    /// map; those transitions are owned by later Phase 6 tasks.
    /// </summary>
    public enum LiquidZoneModeV1 : byte
    {
        Disabled = 0,
        Prescribed = 1,
        RateLimited = 2
    }

    /// <summary>
    /// One explicit logical-zone to physical-assembly binding. The binding is
    /// data, not an array-order convention.
    /// </summary>
    public sealed class LiquidZoneAssemblyBindingV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint LogicalZoneCount = 14;
        public const uint PhysicalAssemblyCount = 6;

        private LiquidZoneAssemblyBindingV1(uint logicalZoneId, uint physicalAssemblyId)
        {
            LogicalZoneId = logicalZoneId;
            PhysicalAssemblyId = physicalAssemblyId;
        }

        public uint LogicalZoneId { get; }

        public uint PhysicalAssemblyId { get; }

        public static ContractValidationResult<LiquidZoneAssemblyBindingV1> TryCreate(
            uint logicalZoneId,
            uint physicalAssemblyId)
        {
            if (logicalZoneId >= LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneAssemblyBinding.LogicalZoneId.OutOfRange",
                    "logical_zone_id",
                    "LogicalZoneId must be in [0,13].");
            }

            if (physicalAssemblyId >= PhysicalAssemblyCount)
            {
                return Invalid(
                    "LiquidZoneAssemblyBinding.PhysicalAssemblyId.OutOfRange",
                    "physical_assembly_id",
                    "PhysicalAssemblyId must be in [0,5].");
            }

            return ContractValidationResult<LiquidZoneAssemblyBindingV1>.Valid(
                new LiquidZoneAssemblyBindingV1(logicalZoneId, physicalAssemblyId));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-ZONE-ASSEMBLY-BINDING-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, LogicalZoneId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, PhysicalAssemblyId);
            });
        }

        private static ContractValidationResult<LiquidZoneAssemblyBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneAssemblyBindingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Versioned, complete logical-to-physical liquid-zone grouping. The
    /// supplied digest is checked against canonical mapping bytes and is not a
    /// placeholder for production map weights.
    /// </summary>
    public sealed class LiquidZoneGroupingV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint LogicalZoneCount = LiquidZoneAssemblyBindingV1.LogicalZoneCount;
        public const uint PhysicalAssemblyCount = LiquidZoneAssemblyBindingV1.PhysicalAssemblyCount;

        private LiquidZoneGroupingV1(
            uint schemaVersion,
            StableId mappingId,
            string mappingVersion,
            IEnumerable<LiquidZoneAssemblyBindingV1> mappings,
            Digest32 mappingDigest)
        {
            SchemaVersion = schemaVersion;
            MappingId = mappingId;
            MappingVersion = mappingVersion;
            Mappings = new ReadOnlyCollection<LiquidZoneAssemblyBindingV1>(mappings.ToArray());
            MappingDigest = mappingDigest;
        }

        public uint SchemaVersion { get; }

        public StableId MappingId { get; }

        public string MappingVersion { get; }

        public IReadOnlyList<LiquidZoneAssemblyBindingV1> Mappings { get; }

        public Digest32 MappingDigest { get; }

        /// <summary>
        /// Computes the digest expected by <see cref="TryCreate"/> for a
        /// validated grouping fixture or an approved external data loader.
        /// The helper does not select or provide any influence-map values.
        /// </summary>
        public static Digest32 ComputeDigest(
            uint schemaVersion,
            StableId mappingId,
            string mappingVersion,
            IEnumerable<LiquidZoneAssemblyBindingV1> mappings)
        {
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (mappingId.IsEmpty)
            {
                throw new ArgumentException("A mapping identity is required.", nameof(mappingId));
            }

            if (!IsCanonicalText(mappingVersion))
            {
                throw new ArgumentException("A mapping version is required.", nameof(mappingVersion));
            }

            LiquidZoneAssemblyBindingV1[] entries = mappings.ToArray();
            if (entries.Any(entry => entry == null))
            {
                throw new ArgumentException("Mapping entries may not be null.", nameof(mappings));
            }

            LiquidZoneAssemblyBindingV1[] canonical = entries
                .OrderBy(entry => entry.LogicalZoneId)
                .ThenBy(entry => entry.PhysicalAssemblyId)
                .ToArray();
            return ComputeDigestFromCanonical(
                schemaVersion,
                mappingId,
                mappingVersion,
                canonical);
        }

        public static ContractValidationResult<LiquidZoneGroupingV1> TryCreate(
            uint schemaVersion,
            StableId mappingId,
            string? mappingVersion,
            IEnumerable<LiquidZoneAssemblyBindingV1>? mappings,
            Digest32? mappingDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "LiquidZoneGrouping.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only liquid-zone grouping schema version 1 is accepted.");
            }

            if (mappingId.IsEmpty)
            {
                return Invalid(
                    "LiquidZoneGrouping.MappingId.Empty",
                    "mapping_id",
                    "A versioned grouping requires a non-empty mapping identity.");
            }

            if (!IsCanonicalText(mappingVersion))
            {
                return Invalid(
                    "LiquidZoneGrouping.MappingVersion.Invalid",
                    "mapping_version",
                    "MappingVersion must be a non-empty strict UTF-8 string.");
            }

            if (mappingDigest == null)
            {
                return Invalid(
                    "LiquidZoneGrouping.MappingDigest.Missing",
                    "mapping_digest",
                    "A grouping requires its explicit mapping digest.");
            }

            if (mappings == null)
            {
                return Invalid(
                    "LiquidZoneGrouping.Mappings.Missing",
                    "mappings",
                    "A grouping requires an explicit mapping collection.");
            }

            LiquidZoneAssemblyBindingV1[] entries = mappings.ToArray();
            if (entries.Length != (int)LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneGrouping.Mappings.CountMismatch",
                    "mappings",
                    "A v1 grouping requires exactly 14 logical-zone entries.");
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index] == null)
                {
                    return Invalid(
                        "LiquidZoneGrouping.Mapping.Null",
                        "mappings[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Grouping entries may not be null.");
                }
            }

            LiquidZoneAssemblyBindingV1[] canonical = entries
                .OrderBy(entry => entry.LogicalZoneId)
                .ThenBy(entry => entry.PhysicalAssemblyId)
                .ToArray();

            for (int index = 0; index < canonical.Length; index++)
            {
                LiquidZoneAssemblyBindingV1 entry = canonical[index];
                if (entry.LogicalZoneId >= LogicalZoneCount)
                {
                    return Invalid(
                        "LiquidZoneGrouping.LogicalZoneId.OutOfRange",
                        "mappings[" + index.ToString(CultureInfo.InvariantCulture) + "].logical_zone_id",
                        "LogicalZoneId must be in [0,13].");
                }

                if (entry.PhysicalAssemblyId >= PhysicalAssemblyCount)
                {
                    return Invalid(
                        "LiquidZoneGrouping.PhysicalAssemblyId.OutOfRange",
                        "mappings[" + index.ToString(CultureInfo.InvariantCulture) + "].physical_assembly_id",
                        "PhysicalAssemblyId must be in [0,5].");
                }
            }

            for (int index = 1; index < canonical.Length; index++)
            {
                if (canonical[index - 1].LogicalZoneId == canonical[index].LogicalZoneId)
                {
                    return Invalid(
                        "LiquidZoneGrouping.LogicalZoneId.Duplicate",
                        "mappings",
                        "Every logical-zone identity must occur exactly once.");
                }
            }

            for (uint logicalZoneId = 0; logicalZoneId < LogicalZoneCount; logicalZoneId++)
            {
                if (canonical[(int)logicalZoneId].LogicalZoneId != logicalZoneId)
                {
                    return Invalid(
                        "LiquidZoneGrouping.LogicalZoneId.Missing",
                        "mappings",
                        "The grouping must contain every logical-zone identity from 0 through 13.");
                }
            }

            bool[] physicalAssemblyPresent = new bool[(int)PhysicalAssemblyCount];
            foreach (LiquidZoneAssemblyBindingV1 entry in canonical)
            {
                physicalAssemblyPresent[(int)entry.PhysicalAssemblyId] = true;
            }

            for (uint physicalAssemblyId = 0; physicalAssemblyId < PhysicalAssemblyCount; physicalAssemblyId++)
            {
                if (!physicalAssemblyPresent[(int)physicalAssemblyId])
                {
                    return Invalid(
                        "LiquidZoneGrouping.PhysicalAssemblyId.Missing",
                        "mappings",
                        "The grouping must represent every physical assembly identity from 0 through 5.");
                }
            }

            Digest32 expectedDigest = ComputeDigestFromCanonical(
                schemaVersion,
                mappingId,
                mappingVersion!,
                canonical);
            if (!mappingDigest.Equals(expectedDigest))
            {
                return Invalid(
                    "LiquidZoneGrouping.MappingDigest.Mismatch",
                    "mapping_digest",
                    "The supplied mapping digest does not equal the canonical grouping bytes.");
            }

            return ContractValidationResult<LiquidZoneGroupingV1>.Valid(
                new LiquidZoneGroupingV1(
                    schemaVersion,
                    mappingId,
                    mappingVersion!,
                    canonical,
                    mappingDigest));
        }

        public bool TryGetPhysicalAssemblyId(uint logicalZoneId, out uint physicalAssemblyId)
        {
            if (logicalZoneId >= LogicalZoneCount)
            {
                physicalAssemblyId = 0;
                return false;
            }

            physicalAssemblyId = Mappings[(int)logicalZoneId].PhysicalAssemblyId;
            return true;
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                SchemaVersion,
                MappingId,
                MappingVersion,
                Mappings,
                MappingDigest,
                true);
        }

        private static Digest32 ComputeDigestFromCanonical(
            uint schemaVersion,
            StableId mappingId,
            string mappingVersion,
            IReadOnlyList<LiquidZoneAssemblyBindingV1> mappings)
        {
            return Phase6CanonicalDigestPrimitives.ComputeVersionedCollectionDigest(
                "CANDU-ZONE-GROUPING-V1",
                schemaVersion,
                mappingId,
                mappingVersion,
                mappings.Select(mapping => mapping.ToCanonicalBytes()).ToArray());
        }

        private static byte[] BuildBytes(
            uint schemaVersion,
            StableId mappingId,
            string mappingVersion,
            IReadOnlyList<LiquidZoneAssemblyBindingV1> mappings,
            Digest32? mappingDigest,
            bool includeDigest)
        {
            return Phase6CanonicalDigestPrimitives.BuildVersionedCollectionBytes(
                "CANDU-ZONE-GROUPING-V1",
                schemaVersion,
                mappingId,
                mappingVersion,
                mappings.Select(mapping => mapping.ToCanonicalBytes()).ToArray(),
                includeDigest ? mappingDigest : null);
        }

        private static bool IsCanonicalText(string? value)
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

        private static ContractValidationResult<LiquidZoneGroupingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneGroupingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// The identity binding for a zone's complete owner-bound queue state.
    /// P6-T01 stores the queue identity, typed branch owner, and queue digest;
    /// queue contents and transitions remain owned by later Phase 6 tasks.
    /// </summary>
    public sealed class LiquidZoneQueueBindingV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string BindingSchemaId = "CANDU-LIQUID-ZONE-QUEUE-BINDING-V1";
        public const string QueueSchemaId = "CANDU-ZONE-QUEUE-V1";
        // P2-T05 ScopeKey -> Entity -> Branch.
        public const byte EntityBranchKindOrdinal = 7;

        private LiquidZoneQueueBindingV1(
            StableId queueId,
            StableId ownerBranchId,
            Digest32 queueDigest)
        {
            QueueId = queueId;
            OwnerBranchId = ownerBranchId;
            QueueDigest = queueDigest;
        }

        public StableId QueueId { get; }

        public StableId OwnerBranchId { get; }

        public Digest32 QueueDigest { get; }

        public static ContractValidationResult<LiquidZoneQueueBindingV1> TryCreate(
            uint schemaVersion,
            StableId queueId,
            StableId ownerBranchId,
            Digest32? queueDigest)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "LiquidZoneQueueBinding.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only liquid-zone queue binding schema version 1 is accepted.");
            }

            if (queueId.IsEmpty || ownerBranchId.IsEmpty || queueDigest == null)
            {
                return Invalid(
                    "LiquidZoneQueueBinding.Identity.Missing",
                    "queue_binding",
                    "A queue binding requires queue identity, typed branch owner, and queue digest.");
            }

            return ContractValidationResult<LiquidZoneQueueBindingV1>.Valid(
                new LiquidZoneQueueBindingV1(queueId, ownerBranchId, queueDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, BindingSchemaId);
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteString(writer, QueueSchemaId);
                Phase5CanonicalBytesV1.WriteBytes(writer, QueueId.ToCanonicalBytes());
                writer.Write(EntityBranchKindOrdinal);
                Phase5CanonicalBytesV1.WriteBytes(writer, OwnerBranchId.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteDigest(writer, QueueDigest);
            });
        }

        private static ContractValidationResult<LiquidZoneQueueBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneQueueBindingV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One authoritative v1 logical liquid-zone state. Available command fill
    /// and last-motion time are deliberately absent because they are
    /// read-only projections of the complete queue state.
    /// </summary>
    public sealed class LiquidZoneStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneStateV1(
            uint logicalZoneId,
            uint physicalAssemblyId,
            double stateFillFraction,
            double referenceFillFraction,
            double commandFillFraction,
            bool enabled,
            LiquidZoneModeV1 mode,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            LiquidZoneQueueBindingV1 queueBinding,
            Digest32 stateDigest)
        {
            LogicalZoneId = logicalZoneId;
            PhysicalAssemblyId = physicalAssemblyId;
            StateFillFraction = stateFillFraction;
            ReferenceFillFraction = referenceFillFraction;
            CommandFillFraction = commandFillFraction;
            Enabled = enabled;
            Mode = mode;
            RateLimitPerSecond = rateLimitPerSecond;
            DelaySeconds = delaySeconds;
            InfluenceMapId = influenceMapId;
            DataVersion = dataVersion;
            DataDigest = dataDigest;
            UpdateTimeSeconds = updateTimeSeconds;
            QueueBinding = queueBinding;
            StateDigest = stateDigest;
        }

        public uint LogicalZoneId { get; }

        public uint PhysicalAssemblyId { get; }

        public double StateFillFraction { get; }

        public double ReferenceFillFraction { get; }

        public double CommandFillFraction { get; }

        public bool Enabled { get; }

        public LiquidZoneModeV1 Mode { get; }

        public double RateLimitPerSecond { get; }

        public double DelaySeconds { get; }

        public StableId InfluenceMapId { get; }

        public string DataVersion { get; }

        public Digest32 DataDigest { get; }

        public double UpdateTimeSeconds { get; }

        public LiquidZoneQueueBindingV1 QueueBinding { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<LiquidZoneStateV1> TryCreate(
            uint schemaVersion,
            uint logicalZoneId,
            uint physicalAssemblyId,
            double stateFillFraction,
            double referenceFillFraction,
            double commandFillFraction,
            bool enabled,
            LiquidZoneModeV1 mode,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string? dataVersion,
            Digest32? dataDigest,
            double updateTimeSeconds,
            LiquidZoneQueueBindingV1? queueBinding,
            Digest32? expectedStateDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "LiquidZoneState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only liquid-zone state schema version 1 is accepted.");
            }

            if (logicalZoneId >= LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneState.LogicalZoneId.OutOfRange",
                    "logical_zone_id",
                    "LogicalZoneId must be in [0,13].");
            }

            if (physicalAssemblyId >= LiquidZoneGroupingV1.PhysicalAssemblyCount)
            {
                return Invalid(
                    "LiquidZoneState.PhysicalAssemblyId.OutOfRange",
                    "physical_assembly_id",
                    "PhysicalAssemblyId must be in [0,5].");
            }

            if (!IsCanonicalFraction(stateFillFraction) ||
                !IsCanonicalFraction(referenceFillFraction) ||
                !IsCanonicalFraction(commandFillFraction))
            {
                return Invalid(
                    "LiquidZoneState.FillFraction.Invalid",
                    "fill_fraction",
                    "Zone fill fractions must be finite, nonnegative, and in [0,1] without signed negative zero.");
            }

            if (!Enum.IsDefined(typeof(LiquidZoneModeV1), mode))
            {
                return Invalid(
                    "LiquidZoneState.Mode.Invalid",
                    "mode",
                    "Mode must be Disabled, Prescribed, or RateLimited.");
            }

            if ((mode == LiquidZoneModeV1.Disabled && enabled) ||
                (mode != LiquidZoneModeV1.Disabled && !enabled))
            {
                return Invalid(
                    "LiquidZoneState.Mode.EnabledMismatch",
                    "enabled",
                    "Disabled mode requires Enabled=false and an active mode requires Enabled=true.");
            }

            if (!IsCanonicalNonnegative(rateLimitPerSecond) || !IsCanonicalNonnegative(delaySeconds))
            {
                return Invalid(
                    "LiquidZoneState.MotionLimits.Invalid",
                    "motion_limits",
                    "RateLimit and Delay must be finite, nonnegative SI values without signed negative zero.");
            }

            if (influenceMapId.IsEmpty || !IsCanonicalText(dataVersion) || dataDigest == null)
            {
                return Invalid(
                    "LiquidZoneState.DataIdentity.Missing",
                    "data_identity",
                    "A zone requires an influence-map identity, data version, and data digest.");
            }

            if (!IsCanonicalNonnegative(updateTimeSeconds))
            {
                return Invalid(
                    "LiquidZoneState.UpdateTime.Invalid",
                    "update_time_s",
                    "UpdateTime must be finite, nonnegative SI seconds without signed negative zero.");
            }

            if (queueBinding == null)
            {
                return Invalid(
                    "LiquidZoneState.QueueBinding.Missing",
                    "command_queue",
                    "Every zone state requires an explicit owner-bound queue binding.");
            }

            Digest32 stateDigest = ComputeDigest(
                logicalZoneId,
                physicalAssemblyId,
                stateFillFraction,
                referenceFillFraction,
                commandFillFraction,
                enabled,
                mode,
                rateLimitPerSecond,
                delaySeconds,
                influenceMapId,
                dataVersion!,
                dataDigest,
                updateTimeSeconds,
                queueBinding);
            if (expectedStateDigest != null && !expectedStateDigest.Equals(stateDigest))
            {
                return Invalid(
                    "LiquidZoneState.StateDigest.Mismatch",
                    "state_digest",
                    "The supplied state digest does not equal the canonical zone state bytes.");
            }

            return ContractValidationResult<LiquidZoneStateV1>.Valid(
                new LiquidZoneStateV1(
                    logicalZoneId,
                    physicalAssemblyId,
                    stateFillFraction,
                    referenceFillFraction,
                    commandFillFraction,
                    enabled,
                    mode,
                    rateLimitPerSecond,
                    delaySeconds,
                    influenceMapId,
                    dataVersion!,
                    dataDigest,
                    updateTimeSeconds,
                    queueBinding,
                    stateDigest));
        }

        public ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> TryGetDisabledZeroAssertion()
        {
            if (Enabled || Mode != LiquidZoneModeV1.Disabled)
            {
                return ContractValidationResult<LiquidZoneDisabledZeroAssertionV1>.Invalid(
                    "LiquidZoneState.DisabledZero.NotApplicable",
                    "disabled_zero",
                    "Disabled-zero evidence is applicable only to a disabled zone state.");
            }

            return ContractValidationResult<LiquidZoneDisabledZeroAssertionV1>.Valid(
                LiquidZoneDisabledZeroAssertionV1.Create(
                    LogicalZoneId,
                    InfluenceMapId,
                    DataDigest,
                    StateDigest));
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                LogicalZoneId,
                PhysicalAssemblyId,
                StateFillFraction,
                ReferenceFillFraction,
                CommandFillFraction,
                Enabled,
                Mode,
                RateLimitPerSecond,
                DelaySeconds,
                InfluenceMapId,
                DataVersion,
                DataDigest,
                UpdateTimeSeconds,
                QueueBinding,
                StateDigest);
        }

        private static Digest32 ComputeDigest(
            uint logicalZoneId,
            uint physicalAssemblyId,
            double stateFillFraction,
            double referenceFillFraction,
            double commandFillFraction,
            bool enabled,
            LiquidZoneModeV1 mode,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            LiquidZoneQueueBindingV1 queueBinding)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        logicalZoneId,
                        physicalAssemblyId,
                        stateFillFraction,
                        referenceFillFraction,
                        commandFillFraction,
                        enabled,
                        mode,
                        rateLimitPerSecond,
                        delaySeconds,
                        influenceMapId,
                        dataVersion,
                        dataDigest,
                        updateTimeSeconds,
                        queueBinding,
                        null)));
        }

        private static byte[] BuildBytes(
            uint logicalZoneId,
            uint physicalAssemblyId,
            double stateFillFraction,
            double referenceFillFraction,
            double commandFillFraction,
            bool enabled,
            LiquidZoneModeV1 mode,
            double rateLimitPerSecond,
            double delaySeconds,
            StableId influenceMapId,
            string dataVersion,
            Digest32 dataDigest,
            double updateTimeSeconds,
            LiquidZoneQueueBindingV1 queueBinding,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-STATE-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteUInt32(writer, logicalZoneId);
                Phase5CanonicalBytesV1.WriteUInt32(writer, physicalAssemblyId);
                Phase5CanonicalBytesV1.WriteDouble(writer, stateFillFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, referenceFillFraction);
                Phase5CanonicalBytesV1.WriteDouble(writer, commandFillFraction);
                writer.Write(enabled ? (byte)1 : (byte)0);
                writer.Write((byte)mode);
                Phase5CanonicalBytesV1.WriteDouble(writer, rateLimitPerSecond);
                Phase5CanonicalBytesV1.WriteDouble(writer, delaySeconds);
                Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                Phase5CanonicalBytesV1.WriteString(writer, dataVersion);
                Phase5CanonicalBytesV1.WriteDigest(writer, dataDigest);
                Phase5CanonicalBytesV1.WriteDouble(writer, updateTimeSeconds);
                Phase5CanonicalBytesV1.WriteBytes(writer, queueBinding.ToCanonicalBytes());
                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalNonnegative(value) && value <= 1.0;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static bool IsCanonicalText(string? value)
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

        private static ContractValidationResult<LiquidZoneStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneStateV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Explicit exact-zero assertion for one disabled liquid-zone overlay. It
    /// is intentionally a zone-level assertion, not the final branch-level
    /// P2-T05 DisabledZeroV1 observable envelope.
    /// </summary>
    public sealed class LiquidZoneDisabledZeroAssertionV1
    {
        private LiquidZoneDisabledZeroAssertionV1(
            uint logicalZoneId,
            StableId influenceMapId,
            Digest32 influenceMapDigest,
            Digest32 zoneStateDigest,
            Digest32 overlayDigest)
        {
            LogicalZoneId = logicalZoneId;
            Enabled = false;
            ExactZeroOverlayMInverse = 0.0;
            InfluenceMapId = influenceMapId;
            InfluenceMapDigest = influenceMapDigest;
            ZoneStateDigest = zoneStateDigest;
            OverlayDigest = overlayDigest;
        }

        public uint LogicalZoneId { get; }

        public bool Enabled { get; }

        public double ExactZeroOverlayMInverse { get; }

        public StableId InfluenceMapId { get; }

        public Digest32 InfluenceMapDigest { get; }

        public Digest32 ZoneStateDigest { get; }

        public Digest32 OverlayDigest { get; }

        internal static LiquidZoneDisabledZeroAssertionV1 Create(
            uint logicalZoneId,
            StableId influenceMapId,
            Digest32 influenceMapDigest,
            Digest32 zoneStateDigest)
        {
            Digest32 overlayDigest = new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    Phase5CanonicalBytesV1.Build(writer =>
                    {
                        Phase5CanonicalBytesV1.WriteAscii(
                            writer,
                            "CANDU-LIQUID-ZONE-DISABLED-ZERO-OVERLAY-V1");
                        writer.Write((byte)0);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, logicalZoneId);
                        Phase5CanonicalBytesV1.WriteDouble(writer, 0.0);
                        Phase5CanonicalBytesV1.WriteStableId(writer, influenceMapId);
                        Phase5CanonicalBytesV1.WriteDigest(writer, influenceMapDigest);
                    })));
            return new LiquidZoneDisabledZeroAssertionV1(
                logicalZoneId,
                influenceMapId,
                influenceMapDigest,
                zoneStateDigest,
                overlayDigest);
        }

        public byte[] ToCanonicalBytes()
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-DISABLED-ZERO-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, 1);
                Phase5CanonicalBytesV1.WriteUInt32(writer, LogicalZoneId);
                writer.Write(Enabled ? (byte)1 : (byte)0);
                Phase5CanonicalBytesV1.WriteDouble(writer, ExactZeroOverlayMInverse);
                Phase5CanonicalBytesV1.WriteStableId(writer, InfluenceMapId);
                Phase5CanonicalBytesV1.WriteDigest(writer, InfluenceMapDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, ZoneStateDigest);
                Phase5CanonicalBytesV1.WriteDigest(writer, OverlayDigest);
            });
        }
    }

    /// <summary>
    /// Complete v1 liquid-zone branch projection. It binds exactly 14 zone
    /// states to one explicit six-assembly grouping and one branch owner; it
    /// does not implement controller, map overlay, or queue transitions.
    /// </summary>
    public sealed class LiquidZoneSystemStateV1
    {
        public const uint CurrentSchemaVersion = 1;

        private LiquidZoneSystemStateV1(
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            LiquidZoneGroupingV1 grouping,
            IEnumerable<LiquidZoneStateV1> zones,
            Digest32 stateDigest)
        {
            BranchId = branchId;
            CoreStateVersion = coreStateVersion;
            TopologyVersion = topologyVersion;
            DataPackVersion = dataPackVersion;
            Grouping = grouping;
            Zones = new ReadOnlyCollection<LiquidZoneStateV1>(zones.ToArray());
            StateDigest = stateDigest;
        }

        public StableId BranchId { get; }

        public ulong CoreStateVersion { get; }

        public string TopologyVersion { get; }

        public string DataPackVersion { get; }

        public LiquidZoneGroupingV1 Grouping { get; }

        public IReadOnlyList<LiquidZoneStateV1> Zones { get; }

        public Digest32 StateDigest { get; }

        public static ContractValidationResult<LiquidZoneSystemStateV1> TryCreate(
            uint schemaVersion,
            StableId branchId,
            ulong coreStateVersion,
            string? topologyVersion,
            string? dataPackVersion,
            LiquidZoneGroupingV1? grouping,
            IEnumerable<LiquidZoneStateV1>? zones,
            Digest32? expectedStateDigest = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                return Invalid(
                    "LiquidZoneSystemState.SchemaVersion.Unsupported",
                    "schema_version",
                    "Only liquid-zone system schema version 1 is accepted.");
            }

            if (branchId.IsEmpty)
            {
                return Invalid(
                    "LiquidZoneSystemState.BranchId.Empty",
                    "branch_id",
                    "A liquid-zone system state requires a non-empty branch identity.");
            }

            if (!IsCanonicalText(topologyVersion) || !IsCanonicalText(dataPackVersion))
            {
                return Invalid(
                    "LiquidZoneSystemState.Version.Invalid",
                    "versions",
                    "TopologyVersion and DataPackVersion must be non-empty strict UTF-8 strings.");
            }

            if (grouping == null)
            {
                return Invalid(
                    "LiquidZoneSystemState.Grouping.Missing",
                    "grouping",
                    "A liquid-zone system state requires a validated grouping.");
            }

            if (zones == null)
            {
                return Invalid(
                    "LiquidZoneSystemState.Zones.Missing",
                    "zones",
                    "A liquid-zone system state requires an explicit zone collection.");
            }

            LiquidZoneStateV1[] entries = zones.ToArray();
            if (entries.Length != (int)LiquidZoneGroupingV1.LogicalZoneCount)
            {
                return Invalid(
                    "LiquidZoneSystemState.Zones.CountMismatch",
                    "zones",
                    "A v1 liquid-zone system state requires exactly 14 logical-zone states.");
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index] == null)
                {
                    return Invalid(
                        "LiquidZoneSystemState.Zone.Null",
                        "zones[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Liquid-zone states may not be null.");
                }
            }

            LiquidZoneStateV1[] canonical = entries
                .OrderBy(entry => entry.LogicalZoneId)
                .ThenBy(entry => entry.PhysicalAssemblyId)
                .ToArray();

            for (int index = 0; index < canonical.Length; index++)
            {
                LiquidZoneStateV1 zone = canonical[index];
                if (zone.LogicalZoneId >= LiquidZoneGroupingV1.LogicalZoneCount)
                {
                    return Invalid(
                        "LiquidZoneSystemState.LogicalZoneId.OutOfRange",
                        "zones[" + index.ToString(CultureInfo.InvariantCulture) + "].logical_zone_id",
                        "LogicalZoneId must be in [0,13].");
                }

                if (zone.QueueBinding.OwnerBranchId != branchId)
                {
                    return Invalid(
                        "LiquidZoneSystemState.QueueOwner.Mismatch",
                        "zones[" + index.ToString(CultureInfo.InvariantCulture) + "].command_queue.owner_key",
                        "Every zone queue must be owned by the enclosing typed branch.");
                }

                if (!grouping.TryGetPhysicalAssemblyId(zone.LogicalZoneId, out uint expectedPhysicalAssemblyId) ||
                    expectedPhysicalAssemblyId != zone.PhysicalAssemblyId)
                {
                    return Invalid(
                        "LiquidZoneSystemState.PhysicalAssembly.Mismatch",
                        "zones[" + index.ToString(CultureInfo.InvariantCulture) + "].physical_assembly_id",
                        "Each zone state must use the physical assembly declared by the grouping.");
                }
            }

            for (int index = 1; index < canonical.Length; index++)
            {
                if (canonical[index - 1].LogicalZoneId == canonical[index].LogicalZoneId)
                {
                    return Invalid(
                        "LiquidZoneSystemState.LogicalZoneId.Duplicate",
                        "zones",
                        "Every logical-zone state identity must occur exactly once.");
                }
            }

            for (uint logicalZoneId = 0; logicalZoneId < LiquidZoneGroupingV1.LogicalZoneCount; logicalZoneId++)
            {
                if (canonical[(int)logicalZoneId].LogicalZoneId != logicalZoneId)
                {
                    return Invalid(
                        "LiquidZoneSystemState.LogicalZoneId.Missing",
                        "zones",
                        "The system state must contain every logical-zone identity from 0 through 13.");
                }
            }

            Digest32 stateDigest = ComputeDigest(
                branchId,
                coreStateVersion,
                topologyVersion!,
                dataPackVersion!,
                grouping,
                canonical);
            if (expectedStateDigest != null && !expectedStateDigest.Equals(stateDigest))
            {
                return Invalid(
                    "LiquidZoneSystemState.StateDigest.Mismatch",
                    "state_digest",
                    "The supplied system state digest does not equal the canonical branch projection.");
            }

            return ContractValidationResult<LiquidZoneSystemStateV1>.Valid(
                new LiquidZoneSystemStateV1(
                    branchId,
                    coreStateVersion,
                    topologyVersion!,
                    dataPackVersion!,
                    grouping,
                    canonical,
                    stateDigest));
        }

        public ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> TryGetDisabledZeroAssertion(
            uint logicalZoneId)
        {
            if (logicalZoneId >= Zones.Count)
            {
                return ContractValidationResult<LiquidZoneDisabledZeroAssertionV1>.Invalid(
                    "LiquidZoneSystemState.LogicalZoneId.OutOfRange",
                    "logical_zone_id",
                    "LogicalZoneId must be in [0,13].");
            }

            LiquidZoneStateV1 zone = Zones[(int)logicalZoneId];
            return zone.TryGetDisabledZeroAssertion();
        }

        public byte[] ToCanonicalBytes()
        {
            return BuildBytes(
                BranchId,
                CoreStateVersion,
                TopologyVersion,
                DataPackVersion,
                Grouping,
                Zones,
                StateDigest);
        }

        private static Digest32 ComputeDigest(
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            LiquidZoneGroupingV1 grouping,
            IReadOnlyList<LiquidZoneStateV1> zones)
        {
            return new Digest32(
                Phase5CanonicalBytesV1.Sha256(
                    BuildBytes(
                        branchId,
                        coreStateVersion,
                        topologyVersion,
                        dataPackVersion,
                        grouping,
                        zones,
                        null)));
        }

        private static byte[] BuildBytes(
            StableId branchId,
            ulong coreStateVersion,
            string topologyVersion,
            string dataPackVersion,
            LiquidZoneGroupingV1 grouping,
            IReadOnlyList<LiquidZoneStateV1> zones,
            Digest32? stateDigest)
        {
            return Phase5CanonicalBytesV1.Build(writer =>
            {
                Phase5CanonicalBytesV1.WriteAscii(writer, "CANDU-LIQUID-ZONE-SYSTEM-V1");
                writer.Write((byte)0);
                Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                Phase5CanonicalBytesV1.WriteStableId(writer, branchId);
                Phase5CanonicalBytesV1.WriteUInt64(writer, coreStateVersion);
                Phase5CanonicalBytesV1.WriteString(writer, topologyVersion);
                Phase5CanonicalBytesV1.WriteString(writer, dataPackVersion);
                Phase5CanonicalBytesV1.WriteBytes(writer, grouping.ToCanonicalBytes());
                Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)zones.Count));
                foreach (LiquidZoneStateV1 zone in zones)
                {
                    Phase5CanonicalBytesV1.WriteBytes(writer, zone.ToCanonicalBytes());
                }

                if (stateDigest != null)
                {
                    Phase5CanonicalBytesV1.WriteDigest(writer, stateDigest);
                }
            });
        }

        private static bool IsCanonicalText(string? value)
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

        private static ContractValidationResult<LiquidZoneSystemStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<LiquidZoneSystemStateV1>.Invalid(code, path, message);
        }
    }
}
