using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactorSim.Core
{
    public enum CommandReplayOperationKindV1 : byte
    {
        Enqueue = 0,
        ReleaseDue = 1
    }

    /// <summary>
    /// One deterministic scheduling transition in a command replay log. This
    /// contract replays enqueue and due-release transitions only; it does not
    /// execute event bodies or mutate simulation state.
    /// </summary>
    public sealed class CommandReplayEntryV1
    {
        private readonly ReadOnlyCollection<StableId> _expectedReleasedCommandIds;

        private CommandReplayEntryV1(
            CommandReplayOperationKindV1 operation,
            SimulationCommandV1? command,
            SimulationClockV1? releaseClock,
            IReadOnlyList<StableId> expectedReleasedCommandIds)
        {
            Operation = operation;
            Command = command;
            ReleaseClock = releaseClock;
            _expectedReleasedCommandIds = new ReadOnlyCollection<StableId>(
                expectedReleasedCommandIds.ToArray());
        }

        public CommandReplayOperationKindV1 Operation { get; }

        public SimulationCommandV1? Command { get; }

        public SimulationClockV1? ReleaseClock { get; }

        public IReadOnlyList<StableId> ExpectedReleasedCommandIds
        {
            get { return _expectedReleasedCommandIds; }
        }

        public static ContractValidationResult<CommandReplayEntryV1> TryEnqueue(
            SimulationCommandV1 command)
        {
            if (command == null)
            {
                return ContractValidationResult<CommandReplayEntryV1>.Invalid(
                    "CommandReplay.Enqueue.Command.Missing",
                    "command",
                    "An enqueue replay entry requires a command envelope.");
            }

            return ContractValidationResult<CommandReplayEntryV1>.Valid(
                new CommandReplayEntryV1(
                    CommandReplayOperationKindV1.Enqueue,
                    command,
                    null,
                    Array.Empty<StableId>()));
        }

        public static ContractValidationResult<CommandReplayEntryV1> TryReleaseDue(
            SimulationClockV1 releaseClock,
            IEnumerable<StableId> expectedReleasedCommandIds)
        {
            if (releaseClock == null)
            {
                return ContractValidationResult<CommandReplayEntryV1>.Invalid(
                    "CommandReplay.Release.Clock.Missing",
                    "release_clock",
                    "A due-release replay entry requires an explicit clock boundary.");
            }

            if (expectedReleasedCommandIds == null)
            {
                return ContractValidationResult<CommandReplayEntryV1>.Invalid(
                    "CommandReplay.Release.Ids.Missing",
                    "expected_released_command_ids",
                    "A due-release replay entry requires its exact released identity list.");
            }

            StableId[] ids = expectedReleasedCommandIds.ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsEmpty)
                {
                    return ContractValidationResult<CommandReplayEntryV1>.Invalid(
                        "CommandReplay.Release.Id.Empty",
                        "expected_released_command_ids",
                        "A released command identity may not be empty.");
                }

                if (i > 0 && ids.Take(i).Any(id => id == ids[i]))
                {
                    return ContractValidationResult<CommandReplayEntryV1>.Invalid(
                        "CommandReplay.Release.Id.Duplicate",
                        "expected_released_command_ids",
                        "A released command identity may occur only once in one release entry.");
                }
            }

            return ContractValidationResult<CommandReplayEntryV1>.Valid(
                new CommandReplayEntryV1(
                    CommandReplayOperationKindV1.ReleaseDue,
                    null,
                    releaseClock,
                    ids));
        }
    }

    /// <summary>
    /// Immutable ordered replay log for the implemented clock/queue
    /// transitions. The array order is causal log order and is not sorted by
    /// identity.
    /// </summary>
    public sealed class CommandReplayLogV1
    {
        private readonly ReadOnlyCollection<CommandReplayEntryV1> _entries;

        private CommandReplayLogV1(IReadOnlyList<CommandReplayEntryV1> entries)
        {
            _entries = new ReadOnlyCollection<CommandReplayEntryV1>(entries.ToArray());
        }

        public IReadOnlyList<CommandReplayEntryV1> Entries
        {
            get { return _entries; }
        }

        public static ContractValidationResult<CommandReplayLogV1> TryCreate(
            IEnumerable<CommandReplayEntryV1> entries)
        {
            if (entries == null)
            {
                return ContractValidationResult<CommandReplayLogV1>.Invalid(
                    "CommandReplay.Entries.Missing",
                    "entries",
                    "A command replay log requires an ordered entry collection.");
            }

            CommandReplayEntryV1[] copy = entries.ToArray();
            if (copy.Any(entry => entry == null))
            {
                return ContractValidationResult<CommandReplayLogV1>.Invalid(
                    "CommandReplay.Entry.Null",
                    "entries",
                    "A command replay entry may not be null.");
            }

            return ContractValidationResult<CommandReplayLogV1>.Valid(
                new CommandReplayLogV1(copy));
        }

        public ContractValidationResult<CommandReplayResultV1> TryReplay(
            CommandQueueV1 initialQueue)
        {
            if (initialQueue == null)
            {
                return ContractValidationResult<CommandReplayResultV1>.Invalid(
                    "CommandReplay.InitialQueue.Missing",
                    "initial_queue",
                    "A replay requires an immutable initial command queue.");
            }

            CommandQueueV1 queue = initialQueue;
            var releasedByEntry = new List<IReadOnlyList<StableId>>();
            for (int i = 0; i < _entries.Count; i++)
            {
                CommandReplayEntryV1 entry = _entries[i];
                if (entry.Operation == CommandReplayOperationKindV1.Enqueue)
                {
                    if (entry.Command == null)
                    {
                        return ContractValidationResult<CommandReplayResultV1>.Invalid(
                            "CommandReplay.Enqueue.Command.Missing",
                            "entries[" + i.ToString(CultureInfo.InvariantCulture) + "].command",
                            "An enqueue replay entry requires a command envelope.");
                    }

                    ContractValidationResult<SimulationClockV1> clockResult =
                        SimulationClockV1.TryCreate(
                            SimulationClockV1.CurrentSchemaVersion,
                            queue.CurrentSimulationTimeSeconds,
                            queue.CurrentStepIndex);
                    if (!clockResult.IsValid)
                    {
                        return ContractValidationResult<CommandReplayResultV1>.Invalid(
                            clockResult.FirstDiagnostic.Code,
                            "entries[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                            clockResult.FirstDiagnostic.Message);
                    }

                    ContractValidationResult<CommandQueueV1> enqueue = queue.TryEnqueue(
                        clockResult.Value,
                        entry.Command);
                    if (!enqueue.IsValid)
                    {
                        return ContractValidationResult<CommandReplayResultV1>.Invalid(
                            enqueue.FirstDiagnostic.Code,
                            "entries[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                            enqueue.FirstDiagnostic.Message);
                    }

                    queue = enqueue.Value;
                    releasedByEntry.Add(new ReadOnlyCollection<StableId>(Array.Empty<StableId>()));
                    continue;
                }

                if (entry.Operation != CommandReplayOperationKindV1.ReleaseDue ||
                    entry.ReleaseClock == null || entry.Command != null)
                {
                    return ContractValidationResult<CommandReplayResultV1>.Invalid(
                        "CommandReplay.Entry.Invalid",
                        "entries[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "The replay entry discriminant and payload are incoherent.");
                }

                ContractValidationResult<CommandQueueReleaseV1> release = queue.TryReleaseDue(
                    entry.ReleaseClock);
                if (!release.IsValid)
                {
                    return ContractValidationResult<CommandReplayResultV1>.Invalid(
                        release.FirstDiagnostic.Code,
                        "entries[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        release.FirstDiagnostic.Message);
                }

                StableId[] actualIds = release.Value.ReleasedCommands
                    .Select(command => command.CommandId)
                    .ToArray();
                if (!actualIds.SequenceEqual(entry.ExpectedReleasedCommandIds))
                {
                    return ContractValidationResult<CommandReplayResultV1>.Invalid(
                        "CommandReplay.Release.Ids.Mismatch",
                        "entries[" + i.ToString(CultureInfo.InvariantCulture) + "].expected_released_command_ids",
                        "The replayed due-release identity order differs from the recorded command log.");
                }

                queue = release.Value.Queue;
                releasedByEntry.Add(new ReadOnlyCollection<StableId>(actualIds));
            }

            return ContractValidationResult<CommandReplayResultV1>.Valid(
                new CommandReplayResultV1(queue, releasedByEntry));
        }
    }

    public sealed class CommandReplayResultV1
    {
        private readonly ReadOnlyCollection<IReadOnlyList<StableId>> _releasedByEntry;

        internal CommandReplayResultV1(
            CommandQueueV1 finalQueue,
            IReadOnlyList<IReadOnlyList<StableId>> releasedByEntry)
        {
            FinalQueue = finalQueue;
            _releasedByEntry = new ReadOnlyCollection<IReadOnlyList<StableId>>(
                releasedByEntry
                    .Select(ids => (IReadOnlyList<StableId>)new ReadOnlyCollection<StableId>(ids.ToArray()))
                    .ToArray());
        }

        public CommandQueueV1 FinalQueue { get; }

        public IReadOnlyList<IReadOnlyList<StableId>> ReleasedCommandIdsByEntry
        {
            get { return _releasedByEntry; }
        }
    }

    public sealed class CommandReplayArchiveV1
    {
        internal CommandReplayArchiveV1(CommandQueueV1 initialQueue, CommandReplayLogV1 log)
        {
            InitialQueue = initialQueue;
            Log = log;
        }

        public CommandQueueV1 InitialQueue { get; }

        public CommandReplayLogV1 Log { get; }
    }

    /// <summary>
    /// Manual, versioned JSON save/load for the existing state snapshot
    /// contract. The codec uses explicit named tokens and rejects unknown,
    /// missing, duplicate, reordered, incompatible, or trailing data.
    /// </summary>
    public static class StateArchiveCodecV1
    {
        public const uint CurrentSchemaVersion = 2;
        public const string SchemaId = "ReactorSim.StateArchiveV2";

        public static ContractValidationResult<string> Serialize(StateSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return ContractValidationResult<string>.Invalid(
                    "StateArchive.Snapshot.Missing",
                    "snapshot",
                    "A state archive requires an immutable state snapshot.");
            }

            try
            {
                return ContractValidationResult<string>.Valid(JsonCodec.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonCodec.WriteProperty(writer, "schema_id", SchemaId);
                    JsonCodec.WriteProperty(writer, "schema_version", CurrentSchemaVersion);
                    writer.WritePropertyName("snapshot");
                    WriteSnapshot(writer, snapshot);
                    writer.WriteEndObject();
                }));
            }
            catch (CodecFailure failure)
            {
                return ContractValidationResult<string>.Invalid(failure.Code, failure.Path, failure.Message);
            }
        }

        public static ContractValidationResult<StateSnapshotV1> Deserialize(
            string json,
            SimulationConfiguration configuration)
        {
            if (configuration == null)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateArchive.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required for state restore.");
            }

            try
            {
                JsonObjectReader root = JsonCodec.ParseRoot(json, "state_archive");
                string schemaId = JsonCodec.ReadString(root.Take("schema_id"), "state_archive.schema_id");
                uint schemaVersion = JsonCodec.ReadUInt32(root.Take("schema_version"), "state_archive.schema_version");
                if (!string.Equals(schemaId, SchemaId, StringComparison.Ordinal))
                {
                    throw CodecFailure.Unsupported("state_archive.schema_id", "The state archive schema identity is unsupported.");
                }

                if (schemaVersion != CurrentSchemaVersion)
                {
                    throw CodecFailure.Unsupported("state_archive.schema_version", "Only state archive schema version 2 is supported.");
                }

                StateSnapshotV1 snapshot = ReadSnapshot(root.Take("snapshot"), configuration, "state_archive.snapshot");
                root.Complete();
                return ContractValidationResult<StateSnapshotV1>.Valid(snapshot);
            }
            catch (CodecFailure failure)
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(failure.Code, failure.Path, failure.Message);
            }
            catch (Exception exception) when (JsonCodec.IsInputException(exception))
            {
                return ContractValidationResult<StateSnapshotV1>.Invalid(
                    "StateArchive.Input.Invalid",
                    "state_archive",
                    "The state archive is not valid canonical JSON: " + exception.Message);
            }
        }

        private static void WriteSnapshot(JsonTextWriter writer, StateSnapshotV1 snapshot)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "schema_version", snapshot.SchemaVersion);
            JsonCodec.WriteProperty(writer, "snapshot_id", snapshot.SnapshotId.ToString());
            JsonCodec.WriteProperty(writer, "snapshot_version", snapshot.SnapshotVersion);
            JsonCodec.WriteProperty(writer, "simulation_time_s", snapshot.SimulationTimeSeconds);
            writer.WritePropertyName("inventory");
            WriteInventory(writer, snapshot.Inventory);
            writer.WritePropertyName("lifecycle");
            WriteLifecycle(writer, snapshot.Lifecycle);
            writer.WritePropertyName("state_binding");
            WriteStateBinding(writer, snapshot.StateBinding);
            JsonCodec.WriteProperty(writer, "snapshot_digest", JsonCodec.ToHex(snapshot.SnapshotDigest.Bytes));
            writer.WriteEndObject();
        }

        private static StateSnapshotV1 ReadSnapshot(
            JToken token,
            SimulationConfiguration configuration,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            if (schemaVersion != StateSnapshotV1.CurrentSchemaVersion)
            {
                throw CodecFailure.Unsupported(path + ".schema_version", "Only state snapshot schema version 2 is supported.");
            }

            StableId snapshotId = JsonCodec.ReadStableId(reader.Take("snapshot_id"), path + ".snapshot_id");
            ulong snapshotVersion = JsonCodec.ReadUInt64(reader.Take("snapshot_version"), path + ".snapshot_version");
            double simulationTimeSeconds = JsonCodec.ReadDouble(reader.Take("simulation_time_s"), path + ".simulation_time_s");
            JsonCodec.RequireCanonicalNonnegative(simulationTimeSeconds, path + ".simulation_time_s");
            BundleInventory inventory = ReadInventory(reader.Take("inventory"), configuration, path + ".inventory");
            VersionLifecycleV1 lifecycle = ReadLifecycle(
                reader.Take("lifecycle"),
                configuration,
                inventory,
                path + ".lifecycle");
            StateBindingV1 serializedBinding = ReadStateBinding(
                reader.Take("state_binding"),
                path + ".state_binding");
            Digest32 snapshotDigest = JsonCodec.ReadDigest(
                reader.Take("snapshot_digest"),
                path + ".snapshot_digest");
            reader.Complete();

            ContractValidationResult<StateSnapshotV1> result = StateSnapshotV1.TryCreate(
                snapshotId,
                snapshotVersion,
                simulationTimeSeconds,
                inventory,
                lifecycle,
                snapshotDigest);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            if (!JsonCodec.BindingsEqual(serializedBinding, result.Value.StateBinding))
            {
                throw CodecFailure.Invalid(
                    path + ".state_binding",
                    "The serialized state-binding tuple does not equal the lifecycle-derived binding.");
            }

            return result.Value;
        }

        private static void WriteInventory(JsonTextWriter writer, BundleInventory inventory)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "slot_count", (uint)inventory.SlotCount);
            writer.WritePropertyName("bundles");
            writer.WriteStartArray();
            foreach (BundleState bundle in inventory.EnumerateOccupied().OrderBy(bundle => bundle.BundleId))
            {
                writer.WriteStartObject();
                JsonCodec.WriteProperty(writer, "bundle_id", bundle.BundleId.ToString());
                JsonCodec.WriteProperty(writer, "channel_id", bundle.ChannelId.Value);
                JsonCodec.WriteProperty(writer, "position", bundle.Position.Value);
                JsonCodec.WriteProperty(writer, "material_variant_id", bundle.MaterialVariantId.Value);
                JsonCodec.WriteProperty(writer, "initial_burnup_j_per_kg_hm", bundle.InitialBurnupJPerKgHm);
                JsonCodec.WriteProperty(writer, "cumulative_fission_energy_j", bundle.CumulativeFissionEnergyJ);
                JsonCodec.WriteProperty(writer, "heavy_metal_mass_kg", bundle.HeavyMetalMassKg);
                JsonCodec.WriteProperty(writer, "inserted_at_s", bundle.InsertedAtSeconds);
                JsonCodec.WriteProperty(writer, "state_version", bundle.StateVersion);
                writer.WritePropertyName("nuclide_state");
                WriteOptionalNuclideState(writer, bundle.NuclideState);
                writer.WritePropertyName("power_watts");
                WriteOptionalPower(writer, bundle.PowerWatts);
                writer.WritePropertyName("power_snapshot_id");
                JsonCodec.WriteOptionalStableId(writer, bundle.PowerSnapshotId);
                writer.WritePropertyName("power_history");
                WritePowerHistory(writer, bundle.PowerHistory);
                writer.WritePropertyName("coefficient_binding");
                WriteOptionalCoefficient(writer, bundle.CoefficientBinding);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteOptionalPower(JsonTextWriter writer, OptionalPowerWattsV1 value)
        {
            if (value == null)
            {
                throw CodecFailure.Invalid("optional_power", "An optional power wrapper may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "tag", value.IsApplicable ? "Applicable" : "NotApplicable");
            if (value.IsApplicable)
            {
                JsonCodec.WriteProperty(writer, "value_w", value.Value);
            }

            writer.WriteEndObject();
        }

        private static OptionalPowerWattsV1 ReadOptionalPower(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = JsonCodec.ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return OptionalPowerWattsV1.NotApplicable;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional power tag is unsupported.");
            }

            double value = JsonCodec.ReadDouble(reader.Take("value_w"), path + ".value_w");
            reader.Complete();
            ContractValidationResult<OptionalPowerWattsV1> result = OptionalPowerWattsV1.TryApplicable(value);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WritePowerHistory(
            JsonTextWriter writer,
            IEnumerable<PowerHistoryRecordV1> history)
        {
            writer.WriteStartArray();
            foreach (PowerHistoryRecordV1 record in history)
            {
                if (record == null)
                {
                    throw CodecFailure.Invalid("power_history", "Power history records may not be null.");
                }

                writer.WriteStartObject();
                JsonCodec.WriteProperty(writer, "snapshot_time_s", record.SnapshotTimeSeconds);
                JsonCodec.WriteProperty(writer, "power_w", record.PowerWatts);
                JsonCodec.WriteProperty(writer, "core_state_version", record.CoreStateVersion);
                JsonCodec.WriteProperty(writer, "spatial_state_version", record.SpatialStateVersion);
                JsonCodec.WriteProperty(writer, "power_snapshot_version", record.PowerSnapshotVersion);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static PowerHistoryRecordV1[] ReadPowerHistory(JToken token, string path)
        {
            JArray records = JsonCodec.ReadArray(token, path);
            var result = new List<PowerHistoryRecordV1>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                string recordPath = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                JsonObjectReader reader = new JsonObjectReader(records[i], recordPath);
                double time = JsonCodec.ReadDouble(reader.Take("snapshot_time_s"), recordPath + ".snapshot_time_s");
                double power = JsonCodec.ReadDouble(reader.Take("power_w"), recordPath + ".power_w");
                ulong core = JsonCodec.ReadUInt64(reader.Take("core_state_version"), recordPath + ".core_state_version");
                ulong spatial = JsonCodec.ReadUInt64(reader.Take("spatial_state_version"), recordPath + ".spatial_state_version");
                ulong snapshot = JsonCodec.ReadUInt64(reader.Take("power_snapshot_version"), recordPath + ".power_snapshot_version");
                reader.Complete();
                ContractValidationResult<PowerHistoryRecordV1> record = PowerHistoryRecordV1.TryCreate(
                    time,
                    power,
                    core,
                    spatial,
                    snapshot);
                if (!record.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(record.FirstDiagnostic);
                }

                result.Add(record.Value);
            }

            return result.ToArray();
        }

        private static void WriteOptionalCoefficient(
            JsonTextWriter writer,
            BundleCoefficientBindingV1? binding)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "tag", binding == null ? "NotApplicable" : "Applicable");
            if (binding != null)
            {
                JsonCodec.WriteProperty(writer, "table_id", binding.TableId.ToString());
                JsonCodec.WriteProperty(writer, "lower_index", checked((uint)binding.LowerIndex));
                JsonCodec.WriteProperty(writer, "upper_index", checked((uint)binding.UpperIndex));
                JsonCodec.WriteProperty(writer, "interpolation_fraction", binding.InterpolationFraction);
                JsonCodec.WriteProperty(writer, "table_digest", JsonCodec.ToHex(binding.TableDigest.Bytes));
            }

            writer.WriteEndObject();
        }

        private static BundleCoefficientBindingV1? ReadOptionalCoefficient(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = JsonCodec.ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return null;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional coefficient tag is unsupported.");
            }

            StableId tableId = JsonCodec.ReadStableId(reader.Take("table_id"), path + ".table_id");
            int lower = checked((int)JsonCodec.ReadUInt32(reader.Take("lower_index"), path + ".lower_index"));
            int upper = checked((int)JsonCodec.ReadUInt32(reader.Take("upper_index"), path + ".upper_index"));
            double fraction = JsonCodec.ReadDouble(
                reader.Take("interpolation_fraction"), path + ".interpolation_fraction");
            Digest32 tableDigest = JsonCodec.ReadDigest(reader.Take("table_digest"), path + ".table_digest");
            reader.Complete();
            ContractValidationResult<BundleCoefficientBindingV1> result = BundleCoefficientBindingV1.TryCreate(
                tableId,
                lower,
                upper,
                fraction,
                tableDigest);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteOptionalNuclideState(
            JsonTextWriter writer,
            NuclideStateEnvelopeV1? state)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "tag", state == null ? "NotApplicable" : "Applicable");
            if (state != null)
            {
                WriteNuclideState(writer, state);
            }

            writer.WriteEndObject();
        }

        private static NuclideStateEnvelopeV1? ReadOptionalNuclideState(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = JsonCodec.ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return null;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional nuclide-state tag is unsupported.");
            }

            NuclideStateEnvelopeV1 state = ReadNuclideState(reader, path);
            reader.Complete();
            return state;
        }

        private static void WriteNuclideData(JsonTextWriter writer, NuclideDataV1 data)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "material_variant_id", data.MaterialVariantId.Value);
            JsonCodec.WriteProperty(writer, "data_id", data.DataId);
            JsonCodec.WriteProperty(writer, "data_digest", JsonCodec.ToHex(data.DataDigest.Bytes));
            JsonCodec.WriteProperty(writer, "gamma_i", data.GammaI);
            JsonCodec.WriteProperty(writer, "gamma_xe", data.GammaXe);
            JsonCodec.WriteProperty(writer, "lambda_i", data.LambdaI);
            JsonCodec.WriteProperty(writer, "lambda_xe", data.LambdaXe);
            JsonCodec.WriteProperty(writer, "sigma_xe_group1_m2", data.SigmaXeGroup1M2);
            JsonCodec.WriteProperty(writer, "sigma_xe_group2_m2", data.SigmaXeGroup2M2);
            writer.WriteEndObject();
        }

        private static NuclideDataV1 ReadNuclideData(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            MaterialVariantId material = new MaterialVariantId(
                JsonCodec.ReadString(reader.Take("material_variant_id"), path + ".material_variant_id"));
            string dataId = JsonCodec.ReadString(reader.Take("data_id"), path + ".data_id");
            Digest32 dataDigest = JsonCodec.ReadDigest(reader.Take("data_digest"), path + ".data_digest");
            double gammaI = JsonCodec.ReadDouble(reader.Take("gamma_i"), path + ".gamma_i");
            double gammaXe = JsonCodec.ReadDouble(reader.Take("gamma_xe"), path + ".gamma_xe");
            double lambdaI = JsonCodec.ReadDouble(reader.Take("lambda_i"), path + ".lambda_i");
            double lambdaXe = JsonCodec.ReadDouble(reader.Take("lambda_xe"), path + ".lambda_xe");
            double sigmaGroup1 = JsonCodec.ReadDouble(reader.Take("sigma_xe_group1_m2"), path + ".sigma_xe_group1_m2");
            double sigmaGroup2 = JsonCodec.ReadDouble(reader.Take("sigma_xe_group2_m2"), path + ".sigma_xe_group2_m2");
            reader.Complete();
            ContractValidationResult<NuclideDataV1> result = NuclideDataV1.TryCreate(
                material,
                dataId,
                dataDigest,
                gammaI,
                gammaXe,
                lambdaI,
                lambdaXe,
                sigmaGroup1,
                sigmaGroup2);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteNuclideState(JsonTextWriter writer, NuclideStateEnvelopeV1 state)
        {
            JsonCodec.WriteProperty(writer, "schema_version", NuclideStateEnvelopeV1.CurrentSchemaVersion);
            JsonCodec.WriteProperty(writer, "bundle_id", state.BundleId.ToString());
            JsonCodec.WriteProperty(writer, "i135_atom_inventory", state.I135AtomInventory);
            JsonCodec.WriteProperty(writer, "xe135_atom_inventory", state.Xe135AtomInventory);
            JsonCodec.WriteProperty(writer, "initial_i135", state.InitialI135);
            JsonCodec.WriteProperty(writer, "initial_xe135", state.InitialXe135);
            JsonCodec.WriteProperty(writer, "node_volume_m3", state.NodeVolumeM3);
            JsonCodec.WriteProperty(writer, "i135_number_density", state.I135NumberDensity);
            JsonCodec.WriteProperty(writer, "xe135_number_density", state.Xe135NumberDensity);
            JsonCodec.WriteProperty(writer, "nuclide_state_version", state.NuclideStateVersion);
            JsonCodec.WriteProperty(writer, "nuclide_data_id", state.NuclideDataId);
            JsonCodec.WriteProperty(writer, "nuclide_data_digest", JsonCodec.ToHex(state.NuclideDataDigest.Bytes));
            writer.WritePropertyName("data");
            WriteNuclideData(writer, state.Data);
            writer.WritePropertyName("i135_xe_history");
            writer.WriteStartArray();
            foreach (NuclideTransitionRecordV1 record in state.I135XeHistory)
            {
                WriteNuclideTransition(writer, record);
            }

            writer.WriteEndArray();
            JsonCodec.WriteProperty(writer, "nuclide_history_digest", JsonCodec.ToHex(state.NuclideHistoryDigest.Bytes));
            JsonCodec.WriteProperty(writer, "nuclide_state_digest", JsonCodec.ToHex(state.NuclideStateDigest.Bytes));
        }

        private static NuclideStateEnvelopeV1 ReadNuclideState(JsonObjectReader reader, string path)
        {
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            if (schemaVersion != NuclideStateEnvelopeV1.CurrentSchemaVersion)
            {
                throw CodecFailure.Unsupported(path + ".schema_version", "Only nuclide-state schema version 1 is supported.");
            }

            StableId bundleId = JsonCodec.ReadStableId(reader.Take("bundle_id"), path + ".bundle_id");
            double iInventory = JsonCodec.ReadDouble(reader.Take("i135_atom_inventory"), path + ".i135_atom_inventory");
            double xeInventory = JsonCodec.ReadDouble(reader.Take("xe135_atom_inventory"), path + ".xe135_atom_inventory");
            double initialI = JsonCodec.ReadDouble(reader.Take("initial_i135"), path + ".initial_i135");
            double initialXe = JsonCodec.ReadDouble(reader.Take("initial_xe135"), path + ".initial_xe135");
            double nodeVolume = JsonCodec.ReadDouble(reader.Take("node_volume_m3"), path + ".node_volume_m3");
            double iDensity = JsonCodec.ReadDouble(reader.Take("i135_number_density"), path + ".i135_number_density");
            double xeDensity = JsonCodec.ReadDouble(reader.Take("xe135_number_density"), path + ".xe135_number_density");
            ulong stateVersion = JsonCodec.ReadUInt64(reader.Take("nuclide_state_version"), path + ".nuclide_state_version");
            string dataId = JsonCodec.ReadString(reader.Take("nuclide_data_id"), path + ".nuclide_data_id");
            Digest32 dataDigest = JsonCodec.ReadDigest(reader.Take("nuclide_data_digest"), path + ".nuclide_data_digest");
            NuclideDataV1 data = ReadNuclideData(reader.Take("data"), path + ".data");
            JArray historyTokens = JsonCodec.ReadArray(reader.Take("i135_xe_history"), path + ".i135_xe_history");
            var history = new List<NuclideTransitionRecordV1>(historyTokens.Count);
            for (int i = 0; i < historyTokens.Count; i++)
            {
                string recordPath = path + ".i135_xe_history[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                history.Add(ReadNuclideTransition(historyTokens[i], recordPath));
            }

            Digest32 historyDigest = JsonCodec.ReadDigest(
                reader.Take("nuclide_history_digest"), path + ".nuclide_history_digest");
            Digest32 stateDigest = JsonCodec.ReadDigest(
                reader.Take("nuclide_state_digest"), path + ".nuclide_state_digest");
            if (!string.Equals(dataId, data.DataId, StringComparison.Ordinal) ||
                !dataDigest.Equals(data.DataDigest))
            {
                throw CodecFailure.Invalid(path + ".data", "Nuclide data identity fields must match their duplicate envelope bindings.");
            }

            ContractValidationResult<NuclideStateEnvelopeV1> result = NuclideStateEnvelopeV1.TryCreate(
                bundleId,
                iInventory,
                xeInventory,
                initialI,
                initialXe,
                nodeVolume,
                stateVersion,
                data,
                history,
                historyDigest,
                stateDigest);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            if (result.Value.I135NumberDensity != iDensity ||
                result.Value.Xe135NumberDensity != xeDensity)
            {
                throw CodecFailure.Invalid(path + ".density", "Serialized nuclide number densities must equal the canonical derived values.");
            }

            return result.Value;
        }

        private static void WriteNuclideTransition(
            JsonTextWriter writer,
            NuclideTransitionRecordV1 record)
        {
            JsonCodec.WriteProperty(writer, "schema_version", NuclideTransitionRecordV1.CurrentSchemaVersion);
            JsonCodec.WriteProperty(writer, "owner_event_id", record.OwnerEventId.ToString());
            JsonCodec.WriteProperty(writer, "record_id", record.RecordId.ToString());
            JsonCodec.WriteProperty(writer, "record_sequence", record.RecordSequence);
            JsonCodec.WriteProperty(writer, "event_rank", (byte)record.EventRank);
            JsonCodec.WriteProperty(writer, "event_time_s", record.EventTimeSeconds);
            JsonCodec.WriteProperty(writer, "delta_time_s", record.DeltaTimeSeconds);
            JsonCodec.WriteProperty(writer, "bundle_id", record.BundleId.ToString());
            JsonCodec.WriteProperty(writer, "core_state_version_before", record.CoreStateVersionBefore);
            writer.WritePropertyName("core_state_version_after");
            JsonCodec.WriteOptionalUInt64(writer, record.CoreStateVersionAfterOrNA);
            JsonCodec.WriteProperty(writer, "nuclide_state_version_before", record.NuclideStateVersionBefore);
            JsonCodec.WriteProperty(writer, "nuclide_state_version_after", record.NuclideStateVersionAfter);
            JsonCodec.WriteProperty(writer, "node_volume_m3", record.NodeVolumeM3);
            JsonCodec.WriteProperty(writer, "i135_before", record.I135AtomInventoryBefore);
            JsonCodec.WriteProperty(writer, "i135_after", record.I135AtomInventoryAfter);
            JsonCodec.WriteProperty(writer, "xe135_before", record.Xe135AtomInventoryBefore);
            JsonCodec.WriteProperty(writer, "xe135_after", record.Xe135AtomInventoryAfter);
            JsonCodec.WriteProperty(writer, "i135_density_before", record.I135NumberDensityBefore);
            JsonCodec.WriteProperty(writer, "i135_density_after", record.I135NumberDensityAfter);
            JsonCodec.WriteProperty(writer, "xe135_density_before", record.Xe135NumberDensityBefore);
            JsonCodec.WriteProperty(writer, "xe135_density_after", record.Xe135NumberDensityAfter);
            JsonCodec.WriteProperty(writer, "i_direct_production_atoms_per_s", record.I135DirectProductionAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "i_decay_loss_atoms_per_s", record.I135DecayLossAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "xe_direct_production_atoms_per_s", record.Xe135DirectProductionAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "xe_from_i_decay_atoms_per_s", record.Xe135FromI135DecayAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "xe_decay_loss_atoms_per_s", record.Xe135DecayLossAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "xe_absorption_loss_atoms_per_s", record.Xe135AbsorptionLossAtomsPerSecond);
            JsonCodec.WriteProperty(writer, "nuclide_data_id", record.NuclideDataId);
            JsonCodec.WriteProperty(writer, "nuclide_data_digest", JsonCodec.ToHex(record.NuclideDataDigest.Bytes));
            JsonCodec.WriteProperty(writer, "state_binding_digest", JsonCodec.ToHex(record.StateBindingDigest.Bytes));
            JsonCodec.WriteProperty(writer, "record_digest", JsonCodec.ToHex(record.RecordDigest.Bytes));
        }

        private static NuclideTransitionRecordV1 ReadNuclideTransition(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            if (schemaVersion != NuclideTransitionRecordV1.CurrentSchemaVersion)
            {
                throw CodecFailure.Unsupported(path + ".schema_version", "Only nuclide-transition schema version 1 is supported.");
            }

            StableId ownerEventId = JsonCodec.ReadStableId(reader.Take("owner_event_id"), path + ".owner_event_id");
            StableId recordId = JsonCodec.ReadStableId(reader.Take("record_id"), path + ".record_id");
            ulong sequence = JsonCodec.ReadUInt64(reader.Take("record_sequence"), path + ".record_sequence");
            EventRankV1 eventRank = (EventRankV1)JsonCodec.ReadByte(reader.Take("event_rank"), path + ".event_rank");
            double eventTime = JsonCodec.ReadDouble(reader.Take("event_time_s"), path + ".event_time_s");
            double deltaTime = JsonCodec.ReadDouble(reader.Take("delta_time_s"), path + ".delta_time_s");
            StableId bundleId = JsonCodec.ReadStableId(reader.Take("bundle_id"), path + ".bundle_id");
            ulong coreBefore = JsonCodec.ReadUInt64(reader.Take("core_state_version_before"), path + ".core_state_version_before");
            OptionalUInt64 coreAfter = JsonCodec.ReadOptionalUInt64(
                reader.Take("core_state_version_after"), path + ".core_state_version_after");
            ulong nuclideBefore = JsonCodec.ReadUInt64(reader.Take("nuclide_state_version_before"), path + ".nuclide_state_version_before");
            ulong nuclideAfter = JsonCodec.ReadUInt64(reader.Take("nuclide_state_version_after"), path + ".nuclide_state_version_after");
            double nodeVolume = JsonCodec.ReadDouble(reader.Take("node_volume_m3"), path + ".node_volume_m3");
            double iBefore = JsonCodec.ReadDouble(reader.Take("i135_before"), path + ".i135_before");
            double iAfter = JsonCodec.ReadDouble(reader.Take("i135_after"), path + ".i135_after");
            double xeBefore = JsonCodec.ReadDouble(reader.Take("xe135_before"), path + ".xe135_before");
            double xeAfter = JsonCodec.ReadDouble(reader.Take("xe135_after"), path + ".xe135_after");
            double iDensityBefore = JsonCodec.ReadDouble(reader.Take("i135_density_before"), path + ".i135_density_before");
            double iDensityAfter = JsonCodec.ReadDouble(reader.Take("i135_density_after"), path + ".i135_density_after");
            double xeDensityBefore = JsonCodec.ReadDouble(reader.Take("xe135_density_before"), path + ".xe135_density_before");
            double xeDensityAfter = JsonCodec.ReadDouble(reader.Take("xe135_density_after"), path + ".xe135_density_after");
            double iProduction = JsonCodec.ReadDouble(reader.Take("i_direct_production_atoms_per_s"), path + ".i_direct_production_atoms_per_s");
            double iDecay = JsonCodec.ReadDouble(reader.Take("i_decay_loss_atoms_per_s"), path + ".i_decay_loss_atoms_per_s");
            double xeProduction = JsonCodec.ReadDouble(reader.Take("xe_direct_production_atoms_per_s"), path + ".xe_direct_production_atoms_per_s");
            double xeFromI = JsonCodec.ReadDouble(reader.Take("xe_from_i_decay_atoms_per_s"), path + ".xe_from_i_decay_atoms_per_s");
            double xeDecay = JsonCodec.ReadDouble(reader.Take("xe_decay_loss_atoms_per_s"), path + ".xe_decay_loss_atoms_per_s");
            double xeAbsorption = JsonCodec.ReadDouble(reader.Take("xe_absorption_loss_atoms_per_s"), path + ".xe_absorption_loss_atoms_per_s");
            string dataId = JsonCodec.ReadString(reader.Take("nuclide_data_id"), path + ".nuclide_data_id");
            Digest32 dataDigest = JsonCodec.ReadDigest(reader.Take("nuclide_data_digest"), path + ".nuclide_data_digest");
            Digest32 stateBindingDigest = JsonCodec.ReadDigest(reader.Take("state_binding_digest"), path + ".state_binding_digest");
            Digest32 recordDigest = JsonCodec.ReadDigest(reader.Take("record_digest"), path + ".record_digest");
            reader.Complete();

            ContractValidationResult<NuclideTransitionRecordV1> result = NuclideTransitionRecordV1.TryCreate(
                ownerEventId,
                sequence,
                eventRank,
                eventTime,
                deltaTime,
                bundleId,
                coreBefore,
                coreAfter,
                nuclideBefore,
                nuclideAfter,
                nodeVolume,
                iBefore,
                iAfter,
                xeBefore,
                xeAfter,
                iDensityBefore,
                iDensityAfter,
                xeDensityBefore,
                xeDensityAfter,
                iProduction,
                iDecay,
                xeProduction,
                xeFromI,
                xeDecay,
                xeAbsorption,
                dataId,
                dataDigest,
                stateBindingDigest);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            if (result.Value.RecordId != recordId || !result.Value.RecordDigest.Equals(recordDigest))
            {
                throw CodecFailure.Invalid(path, "Serialized nuclide transition identity or digest does not match its canonical reconstruction.");
            }

            return result.Value;
        }

        private static BundleInventory ReadInventory(
            JToken token,
            SimulationConfiguration configuration,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            uint slotCount = JsonCodec.ReadUInt32(reader.Take("slot_count"), path + ".slot_count");
            JArray bundleTokens = JsonCodec.ReadArray(reader.Take("bundles"), path + ".bundles");
            var bundles = new List<BundleState>(bundleTokens.Count);
            StableId previousId = StableId.Empty;
            for (int i = 0; i < bundleTokens.Count; i++)
            {
                string bundlePath = path + ".bundles[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                JsonObjectReader bundleReader = new JsonObjectReader(bundleTokens[i], bundlePath);
                StableId bundleId = JsonCodec.ReadStableId(bundleReader.Take("bundle_id"), bundlePath + ".bundle_id");
                if (i > 0 && previousId >= bundleId)
                {
                    throw CodecFailure.Invalid(bundlePath + ".bundle_id", "Bundle records must be in strictly ascending UUID order.");
                }

                previousId = bundleId;
                ChannelId channelId = new ChannelId(JsonCodec.ReadUInt32(
                    bundleReader.Take("channel_id"), bundlePath + ".channel_id"));
                BundlePosition position = new BundlePosition(JsonCodec.ReadUInt32(
                    bundleReader.Take("position"), bundlePath + ".position"));
                MaterialVariantId materialVariantId = new MaterialVariantId(JsonCodec.ReadString(
                    bundleReader.Take("material_variant_id"), bundlePath + ".material_variant_id"));
                double initialBurnup = JsonCodec.ReadDouble(
                    bundleReader.Take("initial_burnup_j_per_kg_hm"), bundlePath + ".initial_burnup_j_per_kg_hm");
                double cumulativeEnergy = JsonCodec.ReadDouble(
                    bundleReader.Take("cumulative_fission_energy_j"), bundlePath + ".cumulative_fission_energy_j");
                double heavyMetalMass = JsonCodec.ReadDouble(
                    bundleReader.Take("heavy_metal_mass_kg"), bundlePath + ".heavy_metal_mass_kg");
                double insertedAt = JsonCodec.ReadDouble(
                    bundleReader.Take("inserted_at_s"), bundlePath + ".inserted_at_s");
                ulong stateVersion = JsonCodec.ReadUInt64(
                    bundleReader.Take("state_version"), bundlePath + ".state_version");
                NuclideStateEnvelopeV1? nuclideState = ReadOptionalNuclideState(
                    bundleReader.Take("nuclide_state"), bundlePath + ".nuclide_state");
                OptionalPowerWattsV1 powerWatts = ReadOptionalPower(
                    bundleReader.Take("power_watts"), bundlePath + ".power_watts");
                OptionalStableId powerSnapshotId = JsonCodec.ReadOptionalStableId(
                    bundleReader.Take("power_snapshot_id"), bundlePath + ".power_snapshot_id");
                PowerHistoryRecordV1[] powerHistory = ReadPowerHistory(
                    bundleReader.Take("power_history"), bundlePath + ".power_history");
                BundleCoefficientBindingV1? coefficientBinding = ReadOptionalCoefficient(
                    bundleReader.Take("coefficient_binding"), bundlePath + ".coefficient_binding");
                bundleReader.Complete();
                JsonCodec.RequireCanonicalNonnegative(initialBurnup, bundlePath + ".initial_burnup_j_per_kg_hm");
                JsonCodec.RequireCanonicalNonnegative(cumulativeEnergy, bundlePath + ".cumulative_fission_energy_j");
                JsonCodec.RequireCanonicalPositive(heavyMetalMass, bundlePath + ".heavy_metal_mass_kg");
                JsonCodec.RequireCanonicalNonnegative(insertedAt, bundlePath + ".inserted_at_s");
                bundles.Add(new BundleState(
                    bundleId,
                    channelId,
                    position,
                    materialVariantId,
                    initialBurnup,
                    cumulativeEnergy,
                    heavyMetalMass,
                    insertedAt,
                    nuclideState,
                    powerWatts,
                    powerSnapshotId,
                    powerHistory,
                    coefficientBinding,
                    stateVersion));
            }

            reader.Complete();
            if (slotCount != configuration.Topology.SlotCount)
            {
                throw CodecFailure.Invalid(path + ".slot_count", "The serialized slot count does not match the validated topology.");
            }

            ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(
                configuration.Topology,
                bundles);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteLifecycle(JsonTextWriter writer, VersionLifecycleV1 lifecycle)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "schema_version", lifecycle.SchemaVersion);
            JsonCodec.WriteProperty(writer, "current_simulation_time_s", lifecycle.CurrentSimulationTimeSeconds);
            JsonCodec.WriteProperty(writer, "initial_core_state_version", lifecycle.InitialCoreStateVersion);
            JsonCodec.WriteProperty(writer, "core_state_version", lifecycle.CoreStateVersion);
            JsonCodec.WriteProperty(writer, "initial_spatial_state_version", lifecycle.InitialSpatialStateVersion);
            JsonCodec.WriteProperty(writer, "spatial_state_version", lifecycle.SpatialStateVersion);
            JsonCodec.WriteProperty(writer, "initial_power_snapshot_version", lifecycle.InitialPowerSnapshotVersion);
            JsonCodec.WriteProperty(writer, "power_snapshot_version", lifecycle.PowerSnapshotVersion);
            writer.WritePropertyName("bundle_nuclide_versions");
            writer.WriteStartArray();
            foreach (BundleNuclideVersionV1 record in lifecycle.BundleNuclideVersions.OrderBy(record => record.BundleId))
            {
                writer.WriteStartObject();
                JsonCodec.WriteProperty(writer, "bundle_id", record.BundleId.ToString());
                JsonCodec.WriteProperty(writer, "initial_nuclide_state_version", record.InitialNuclideStateVersion);
                JsonCodec.WriteProperty(writer, "nuclide_state_version", record.NuclideStateVersion);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            JsonCodec.WriteProperty(writer, "spatial_binding_status", (byte)lifecycle.SpatialBindingStatus);
            JsonCodec.WriteProperty(writer, "power_binding_status", (byte)lifecycle.PowerBindingStatus);
            writer.WritePropertyName("spatial_solve_id");
            JsonCodec.WriteOptionalStableId(writer, lifecycle.SpatialSolveId);
            writer.WritePropertyName("power_snapshot_id");
            JsonCodec.WriteOptionalStableId(writer, lifecycle.PowerSnapshotId);
            writer.WritePropertyName("state_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.StateDigest);
            writer.WritePropertyName("coefficient_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.CoefficientDigest);
            writer.WritePropertyName("topology_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.TopologyDigest);
            writer.WritePropertyName("data_pack_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.DataPackDigest);
            writer.WritePropertyName("snapshot_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.SnapshotDigest);
            writer.WritePropertyName("power_snapshot_inventory_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.PowerSnapshotInventoryDigest);
            writer.WritePropertyName("power_snapshot_accepted_inventory_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.PowerSnapshotAcceptedInventoryDigest);
            writer.WritePropertyName("power_snapshot_burnup_energy_digest");
            JsonCodec.WriteOptionalDigest(writer, lifecycle.PowerSnapshotBurnupEnergyDigest);
            JsonCodec.WriteProperty(writer, "topology_version", lifecycle.TopologyVersion);
            JsonCodec.WriteProperty(writer, "data_pack_version", lifecycle.DataPackVersion);
            writer.WritePropertyName("bundle_locations");
            writer.WriteStartArray();
            foreach (BundleLocationV1 location in lifecycle.BundleLocations.OrderBy(location => location.BundleId))
            {
                writer.WriteStartObject();
                JsonCodec.WriteProperty(writer, "bundle_id", location.BundleId.ToString());
                JsonCodec.WriteProperty(writer, "channel_id", location.ChannelId.Value);
                JsonCodec.WriteProperty(writer, "position", location.Position.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static VersionLifecycleV1 ReadLifecycle(
            JToken token,
            SimulationConfiguration configuration,
            BundleInventory inventory,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            double currentTime = JsonCodec.ReadDouble(
                reader.Take("current_simulation_time_s"), path + ".current_simulation_time_s");
            ulong initialCore = JsonCodec.ReadUInt64(reader.Take("initial_core_state_version"), path + ".initial_core_state_version");
            ulong core = JsonCodec.ReadUInt64(reader.Take("core_state_version"), path + ".core_state_version");
            ulong initialSpatial = JsonCodec.ReadUInt64(reader.Take("initial_spatial_state_version"), path + ".initial_spatial_state_version");
            ulong spatial = JsonCodec.ReadUInt64(reader.Take("spatial_state_version"), path + ".spatial_state_version");
            ulong initialPower = JsonCodec.ReadUInt64(reader.Take("initial_power_snapshot_version"), path + ".initial_power_snapshot_version");
            ulong power = JsonCodec.ReadUInt64(reader.Take("power_snapshot_version"), path + ".power_snapshot_version");
            JArray versionTokens = JsonCodec.ReadArray(reader.Take("bundle_nuclide_versions"), path + ".bundle_nuclide_versions");
            var versions = new List<BundleNuclideVersionV1>(versionTokens.Count);
            StableId previousVersionId = StableId.Empty;
            for (int i = 0; i < versionTokens.Count; i++)
            {
                string recordPath = path + ".bundle_nuclide_versions[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                JsonObjectReader recordReader = new JsonObjectReader(versionTokens[i], recordPath);
                StableId bundleId = JsonCodec.ReadStableId(recordReader.Take("bundle_id"), recordPath + ".bundle_id");
                if (i > 0 && previousVersionId >= bundleId)
                {
                    throw CodecFailure.Invalid(recordPath + ".bundle_id", "Bundle version records must be in strictly ascending UUID order.");
                }

                previousVersionId = bundleId;
                ulong initialNuclide = JsonCodec.ReadUInt64(
                    recordReader.Take("initial_nuclide_state_version"), recordPath + ".initial_nuclide_state_version");
                ulong nuclide = JsonCodec.ReadUInt64(
                    recordReader.Take("nuclide_state_version"), recordPath + ".nuclide_state_version");
                recordReader.Complete();
                versions.Add(new BundleNuclideVersionV1(bundleId, initialNuclide, nuclide));
            }

            BindingStatusV1 spatialStatus = (BindingStatusV1)JsonCodec.ReadByte(
                reader.Take("spatial_binding_status"), path + ".spatial_binding_status");
            BindingStatusV1 powerStatus = (BindingStatusV1)JsonCodec.ReadByte(
                reader.Take("power_binding_status"), path + ".power_binding_status");
            OptionalStableId spatialSolveId = JsonCodec.ReadOptionalStableId(
                reader.Take("spatial_solve_id"), path + ".spatial_solve_id");
            OptionalStableId powerSnapshotId = JsonCodec.ReadOptionalStableId(
                reader.Take("power_snapshot_id"), path + ".power_snapshot_id");
            OptionalDigest32 stateDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("state_digest"), path + ".state_digest");
            OptionalDigest32 coefficientDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("coefficient_digest"), path + ".coefficient_digest");
            OptionalDigest32 topologyDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("topology_digest"), path + ".topology_digest");
            OptionalDigest32 dataPackDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("data_pack_digest"), path + ".data_pack_digest");
            OptionalDigest32 snapshotDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("snapshot_digest"), path + ".snapshot_digest");
            OptionalDigest32 powerSnapshotInventoryDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("power_snapshot_inventory_digest"), path + ".power_snapshot_inventory_digest");
            OptionalDigest32 powerSnapshotAcceptedInventoryDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("power_snapshot_accepted_inventory_digest"), path + ".power_snapshot_accepted_inventory_digest");
            OptionalDigest32 powerSnapshotBurnupEnergyDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("power_snapshot_burnup_energy_digest"), path + ".power_snapshot_burnup_energy_digest");
            string topologyVersion = JsonCodec.ReadString(reader.Take("topology_version"), path + ".topology_version");
            string dataPackVersion = JsonCodec.ReadString(reader.Take("data_pack_version"), path + ".data_pack_version");
            JArray locationTokens = JsonCodec.ReadArray(reader.Take("bundle_locations"), path + ".bundle_locations");
            var locations = new List<BundleLocationV1>(locationTokens.Count);
            StableId previousLocationId = StableId.Empty;
            for (int i = 0; i < locationTokens.Count; i++)
            {
                string locationPath = path + ".bundle_locations[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                JsonObjectReader locationReader = new JsonObjectReader(locationTokens[i], locationPath);
                StableId bundleId = JsonCodec.ReadStableId(locationReader.Take("bundle_id"), locationPath + ".bundle_id");
                if (i > 0 && previousLocationId >= bundleId)
                {
                    throw CodecFailure.Invalid(locationPath + ".bundle_id", "Bundle locations must be in strictly ascending UUID order.");
                }

                previousLocationId = bundleId;
                ChannelId channelId = new ChannelId(JsonCodec.ReadUInt32(
                    locationReader.Take("channel_id"), locationPath + ".channel_id"));
                BundlePosition position = new BundlePosition(JsonCodec.ReadUInt32(
                    locationReader.Take("position"), locationPath + ".position"));
                locationReader.Complete();
                locations.Add(new BundleLocationV1(bundleId, channelId, position));
            }

            reader.Complete();
            ContractValidationResult<VersionLifecycleV1> result = VersionLifecycleV1.TryRestoreFromSerialization(
                configuration,
                inventory,
                schemaVersion,
                currentTime,
                initialCore,
                core,
                initialSpatial,
                spatial,
                initialPower,
                power,
                versions,
                spatialStatus,
                powerStatus,
                spatialSolveId,
                powerSnapshotId,
                stateDigest,
                coefficientDigest,
                topologyDigest,
                dataPackDigest,
                snapshotDigest,
                topologyVersion,
                dataPackVersion,
                locations,
                powerSnapshotInventoryDigest,
                powerSnapshotAcceptedInventoryDigest,
                powerSnapshotBurnupEnergyDigest);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteStateBinding(JsonTextWriter writer, StateBindingV1 binding)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "core_state_version", binding.CoreStateVersion);
            writer.WritePropertyName("spatial_state_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.SpatialStateVersion);
            writer.WritePropertyName("spatial_solve_id");
            JsonCodec.WriteOptionalStableId(writer, binding.SpatialSolveId);
            writer.WritePropertyName("power_snapshot_id");
            JsonCodec.WriteOptionalStableId(writer, binding.PowerSnapshotId);
            writer.WritePropertyName("power_snapshot_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.PowerSnapshotVersion);
            writer.WritePropertyName("kinetic_step_index");
            JsonCodec.WriteOptionalUInt64(writer, binding.KineticStepIndex);
            writer.WritePropertyName("nuclide_state_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.NuclideStateVersion);
            JsonCodec.WriteProperty(writer, "topology_version", binding.TopologyVersion);
            JsonCodec.WriteProperty(writer, "data_pack_version", binding.DataPackVersion);
            writer.WritePropertyName("coefficient_digest");
            JsonCodec.WriteOptionalDigest(writer, binding.CoefficientDigest);
            writer.WritePropertyName("snapshot_digest");
            JsonCodec.WriteOptionalDigest(writer, binding.SnapshotDigest);
            writer.WriteEndObject();
        }

        private static StateBindingV1 ReadStateBinding(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            ulong coreStateVersion = JsonCodec.ReadUInt64(reader.Take("core_state_version"), path + ".core_state_version");
            OptionalUInt64 spatialStateVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("spatial_state_version"), path + ".spatial_state_version");
            OptionalStableId spatialSolveId = JsonCodec.ReadOptionalStableId(
                reader.Take("spatial_solve_id"), path + ".spatial_solve_id");
            OptionalStableId powerSnapshotId = JsonCodec.ReadOptionalStableId(
                reader.Take("power_snapshot_id"), path + ".power_snapshot_id");
            OptionalUInt64 powerSnapshotVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("power_snapshot_version"), path + ".power_snapshot_version");
            OptionalUInt64 kineticStepIndex = JsonCodec.ReadOptionalUInt64(
                reader.Take("kinetic_step_index"), path + ".kinetic_step_index");
            OptionalUInt64 nuclideStateVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("nuclide_state_version"), path + ".nuclide_state_version");
            string topologyVersion = JsonCodec.ReadString(reader.Take("topology_version"), path + ".topology_version");
            string dataPackVersion = JsonCodec.ReadString(reader.Take("data_pack_version"), path + ".data_pack_version");
            OptionalDigest32 coefficientDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("coefficient_digest"), path + ".coefficient_digest");
            OptionalDigest32 snapshotDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("snapshot_digest"), path + ".snapshot_digest");
            reader.Complete();
            return new StateBindingV1(
                coreStateVersion,
                spatialStateVersion,
                spatialSolveId,
                powerSnapshotId,
                powerSnapshotVersion,
                kineticStepIndex,
                nuclideStateVersion,
                topologyVersion,
                dataPackVersion,
                coefficientDigest,
                snapshotDigest);
        }
    }

    /// <summary>
    /// Manual JSON codec for the explicit queue state and its causal replay
    /// log. Replay validates the exact released identity sequence and returns
    /// immutable queue transitions; it never applies a command body.
    /// </summary>
    public static class CommandReplayCodecV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string SchemaId = "ReactorSim.CommandReplayV1";
        private const string QueueSchemaId = "ReactorSim.CommandQueueV1";
        private const string ClockSchemaId = "ReactorSim.SimulationClockV1";

        public static ContractValidationResult<string> Serialize(
            CommandQueueV1 initialQueue,
            CommandReplayLogV1 log,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            if (initialQueue == null)
            {
                return ContractValidationResult<string>.Invalid(
                    "CommandReplayArchive.InitialQueue.Missing",
                    "initial_queue",
                    "A replay archive requires an initial command queue.");
            }

            if (log == null)
            {
                return ContractValidationResult<string>.Invalid(
                    "CommandReplayArchive.Log.Missing",
                    "entries",
                    "A replay archive requires an immutable command replay log.");
            }

            ContractValidationResult<bool> context = ValidateReplayContext(
                initialQueue,
                log,
                configuration,
                expectedStateBinding);
            if (!context.IsValid)
            {
                return ContractValidationResult<string>.Invalid(
                    context.FirstDiagnostic.Code,
                    context.FirstDiagnostic.Path,
                    context.FirstDiagnostic.Message);
            }

            ContractValidationResult<CommandReplayResultV1> replay = log.TryReplay(initialQueue);
            if (!replay.IsValid)
            {
                return ContractValidationResult<string>.Invalid(
                    replay.FirstDiagnostic.Code,
                    replay.FirstDiagnostic.Path,
                    replay.FirstDiagnostic.Message);
            }

            try
            {
                return ContractValidationResult<string>.Valid(JsonCodec.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonCodec.WriteProperty(writer, "schema_id", SchemaId);
                    JsonCodec.WriteProperty(writer, "schema_version", CurrentSchemaVersion);
                    JsonCodec.WriteProperty(
                        writer,
                        "topology_digest",
                        JsonCodec.ToHex(configuration.DataPack.TopologyDigest));
                    JsonCodec.WriteProperty(
                        writer,
                        "data_pack_digest",
                        JsonCodec.ToHex(configuration.DataPack.ContentDigest));
                    writer.WritePropertyName("initial_queue");
                    WriteQueue(writer, initialQueue);
                    writer.WritePropertyName("entries");
                    writer.WriteStartArray();
                    foreach (CommandReplayEntryV1 entry in log.Entries)
                    {
                        WriteReplayEntry(writer, entry);
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            catch (CodecFailure failure)
            {
                return ContractValidationResult<string>.Invalid(failure.Code, failure.Path, failure.Message);
            }
        }

        public static ContractValidationResult<CommandReplayArchiveV1> Deserialize(
            string json,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            if (configuration == null)
            {
                return ContractValidationResult<CommandReplayArchiveV1>.Invalid(
                    "CommandReplayArchive.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required for replay restore.");
            }

            if (expectedStateBinding == null)
            {
                return ContractValidationResult<CommandReplayArchiveV1>.Invalid(
                    "CommandReplayArchive.StateBinding.Missing",
                    "expected_state_binding",
                    "A replay restore requires the current state-binding authority.");
            }

            try
            {
                JsonObjectReader root = JsonCodec.ParseRoot(json, "command_replay_archive");
                string schemaId = JsonCodec.ReadString(root.Take("schema_id"), "command_replay_archive.schema_id");
                uint schemaVersion = JsonCodec.ReadUInt32(root.Take("schema_version"), "command_replay_archive.schema_version");
                if (!string.Equals(schemaId, SchemaId, StringComparison.Ordinal))
                {
                    throw CodecFailure.Unsupported("command_replay_archive.schema_id", "The command replay schema identity is unsupported.");
                }

                if (schemaVersion != CurrentSchemaVersion)
                {
                    throw CodecFailure.Unsupported("command_replay_archive.schema_version", "Only command replay schema version 1 is supported.");
                }

                Digest32 topologyDigest = JsonCodec.ReadDigest(
                    root.Take("topology_digest"),
                    "command_replay_archive.topology_digest");
                Digest32 dataPackDigest = JsonCodec.ReadDigest(
                    root.Take("data_pack_digest"),
                    "command_replay_archive.data_pack_digest");
                ValidateArchiveDigests(configuration, topologyDigest, dataPackDigest);

                CommandQueueV1 initialQueue = ReadQueue(
                    root.Take("initial_queue"),
                    configuration,
                    expectedStateBinding,
                    "command_replay_archive.initial_queue");
                JArray entryTokens = JsonCodec.ReadArray(root.Take("entries"), "command_replay_archive.entries");
                var entries = new List<CommandReplayEntryV1>(entryTokens.Count);
                for (int i = 0; i < entryTokens.Count; i++)
                {
                    entries.Add(ReadReplayEntry(
                        entryTokens[i],
                        configuration,
                        expectedStateBinding,
                        "command_replay_archive.entries[" + i.ToString(CultureInfo.InvariantCulture) + "]"));
                }

                root.Complete();
                ContractValidationResult<CommandReplayLogV1> logResult = CommandReplayLogV1.TryCreate(entries);
                if (!logResult.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(logResult.FirstDiagnostic);
                }

                ContractValidationResult<bool> context = ValidateReplayContext(
                    initialQueue,
                    logResult.Value,
                    configuration,
                    expectedStateBinding);
                if (!context.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(context.FirstDiagnostic);
                }

                ContractValidationResult<CommandReplayResultV1> replay = logResult.Value.TryReplay(initialQueue);
                if (!replay.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(replay.FirstDiagnostic);
                }

                return ContractValidationResult<CommandReplayArchiveV1>.Valid(
                    new CommandReplayArchiveV1(initialQueue, logResult.Value));
            }
            catch (CodecFailure failure)
            {
                return ContractValidationResult<CommandReplayArchiveV1>.Invalid(failure.Code, failure.Path, failure.Message);
            }
            catch (Exception exception) when (JsonCodec.IsInputException(exception))
            {
                return ContractValidationResult<CommandReplayArchiveV1>.Invalid(
                    "CommandReplayArchive.Input.Invalid",
                    "command_replay_archive",
                    "The command replay archive is not valid canonical JSON: " + exception.Message);
            }
        }

        private static void WriteQueue(JsonTextWriter writer, CommandQueueV1 queue)
        {
            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "schema_id", QueueSchemaId);
            JsonCodec.WriteProperty(writer, "schema_version", 1U);
            JsonCodec.WriteProperty(writer, "initial_next_sequence", queue.InitialNextSequence);
            JsonCodec.WriteProperty(writer, "next_sequence", queue.NextSequence);
            JsonCodec.WriteProperty(writer, "current_simulation_time_s", queue.CurrentSimulationTimeSeconds);
            JsonCodec.WriteProperty(writer, "current_step_index", queue.CurrentStepIndex);
            writer.WritePropertyName("allocated_command_ids");
            writer.WriteStartArray();
            foreach (StableId commandId in queue.AllocatedCommandIds.OrderBy(id => id))
            {
                writer.WriteValue(commandId.ToString());
            }

            writer.WriteEndArray();
            writer.WritePropertyName("pending_commands");
            writer.WriteStartArray();
            foreach (SimulationCommandV1 command in queue.PendingCommands)
            {
                WriteCommand(writer, command);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static CommandQueueV1 ReadQueue(
            JToken token,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string schemaId = JsonCodec.ReadString(reader.Take("schema_id"), path + ".schema_id");
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            if (!string.Equals(schemaId, QueueSchemaId, StringComparison.Ordinal) || schemaVersion != 1)
            {
                throw CodecFailure.Unsupported(path + ".schema_version", "Only command queue schema version 1 is supported.");
            }

            ulong initialNextSequence = JsonCodec.ReadUInt64(reader.Take("initial_next_sequence"), path + ".initial_next_sequence");
            ulong nextSequence = JsonCodec.ReadUInt64(reader.Take("next_sequence"), path + ".next_sequence");
            double currentTime = JsonCodec.ReadDouble(reader.Take("current_simulation_time_s"), path + ".current_simulation_time_s");
            ulong currentStep = JsonCodec.ReadUInt64(reader.Take("current_step_index"), path + ".current_step_index");
            JsonCodec.RequireCanonicalNonnegative(currentTime, path + ".current_simulation_time_s");
            ContractValidationResult<SimulationClockV1> clockResult = SimulationClockV1.TryCreate(
                SimulationClockV1.CurrentSchemaVersion,
                currentTime,
                currentStep);
            if (!clockResult.IsValid)
            {
                throw CodecFailure.FromDiagnostic(clockResult.FirstDiagnostic);
            }

            JArray idTokens = JsonCodec.ReadArray(reader.Take("allocated_command_ids"), path + ".allocated_command_ids");
            var allocatedIds = new List<StableId>(idTokens.Count);
            StableId previousId = StableId.Empty;
            for (int i = 0; i < idTokens.Count; i++)
            {
                StableId id = JsonCodec.ReadStableId(
                    idTokens[i],
                    path + ".allocated_command_ids[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (i > 0 && previousId >= id)
                {
                    throw CodecFailure.Invalid(
                        path + ".allocated_command_ids[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "Allocated command identities must be in strictly ascending UUID order.");
                }

                previousId = id;
                allocatedIds.Add(id);
            }

            JArray commandTokens = JsonCodec.ReadArray(reader.Take("pending_commands"), path + ".pending_commands");
            var commands = new List<SimulationCommandV1>(commandTokens.Count);
            SimulationCommandV1? previousCommand = null;
            for (int i = 0; i < commandTokens.Count; i++)
            {
                SimulationCommandV1 command = ReadCommand(
                    commandTokens[i],
                    configuration,
                    expectedStateBinding,
                    path + ".pending_commands[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (previousCommand != null && previousCommand.CompareCanonical(command) > 0)
                {
                    throw CodecFailure.Invalid(
                        path + ".pending_commands[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "Pending commands must be in canonical due-time, rank, sequence, UUID order.");
                }

                previousCommand = command;
                commands.Add(command);
            }

            reader.Complete();
            ContractValidationResult<CommandQueueV1> result = CommandQueueV1.TryCreate(
                initialNextSequence,
                nextSequence,
                allocatedIds,
                commands,
                clockResult.Value);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteReplayEntry(JsonTextWriter writer, CommandReplayEntryV1 entry)
        {
            if (entry == null)
            {
                throw CodecFailure.Invalid("entries", "A replay entry may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "operation", (byte)entry.Operation);
            if (entry.Operation == CommandReplayOperationKindV1.Enqueue)
            {
                if (entry.Command == null)
                {
                    throw CodecFailure.Invalid("entries.command", "An enqueue replay entry requires a command envelope.");
                }

                writer.WritePropertyName("command");
                WriteCommand(writer, entry.Command);
            }
            else if (entry.Operation == CommandReplayOperationKindV1.ReleaseDue)
            {
                if (entry.ReleaseClock == null)
                {
                    throw CodecFailure.Invalid("entries.release_clock", "A release replay entry requires a clock boundary.");
                }

                writer.WritePropertyName("release_clock");
                WriteClock(writer, entry.ReleaseClock);
                writer.WritePropertyName("expected_released_command_ids");
                writer.WriteStartArray();
                foreach (StableId commandId in entry.ExpectedReleasedCommandIds)
                {
                    writer.WriteValue(commandId.ToString());
                }

                writer.WriteEndArray();
            }
            else
            {
                throw CodecFailure.Invalid("entries.operation", "The replay operation is not part of the approved closed enum.");
            }

            writer.WriteEndObject();
        }

        private static CommandReplayEntryV1 ReadReplayEntry(
            JToken token,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            byte operation = JsonCodec.ReadByte(reader.Take("operation"), path + ".operation");
            if (operation == (byte)CommandReplayOperationKindV1.Enqueue)
            {
                SimulationCommandV1 command = ReadCommand(
                    reader.Take("command"),
                    configuration,
                    expectedStateBinding,
                    path + ".command");
                reader.Complete();
                ContractValidationResult<CommandReplayEntryV1> result = CommandReplayEntryV1.TryEnqueue(command);
                if (!result.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
                }

                return result.Value;
            }

            if (operation == (byte)CommandReplayOperationKindV1.ReleaseDue)
            {
                SimulationClockV1 clock = ReadClock(reader.Take("release_clock"), path + ".release_clock");
                JArray idTokens = JsonCodec.ReadArray(
                    reader.Take("expected_released_command_ids"),
                    path + ".expected_released_command_ids");
                var ids = new List<StableId>(idTokens.Count);
                for (int i = 0; i < idTokens.Count; i++)
                {
                    ids.Add(JsonCodec.ReadStableId(
                        idTokens[i],
                        path + ".expected_released_command_ids[" + i.ToString(CultureInfo.InvariantCulture) + "]"));
                }

                reader.Complete();
                ContractValidationResult<CommandReplayEntryV1> result = CommandReplayEntryV1.TryReleaseDue(clock, ids);
                if (!result.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
                }

                return result.Value;
            }

            throw CodecFailure.Invalid(path + ".operation", "The replay operation is not part of the approved closed enum.");
        }

        private static void WriteClock(JsonTextWriter writer, SimulationClockV1 clock)
        {
            if (clock == null)
            {
                throw CodecFailure.Invalid("release_clock", "A replay release clock may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "schema_id", ClockSchemaId);
            JsonCodec.WriteProperty(writer, "schema_version", clock.SchemaVersion);
            JsonCodec.WriteProperty(writer, "current_simulation_time_s", clock.CurrentSimulationTimeSeconds);
            JsonCodec.WriteProperty(writer, "step_index", clock.StepIndex);
            writer.WriteEndObject();
        }

        private static SimulationClockV1 ReadClock(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string schemaId = JsonCodec.ReadString(reader.Take("schema_id"), path + ".schema_id");
            uint schemaVersion = JsonCodec.ReadUInt32(reader.Take("schema_version"), path + ".schema_version");
            double time = JsonCodec.ReadDouble(reader.Take("current_simulation_time_s"), path + ".current_simulation_time_s");
            ulong step = JsonCodec.ReadUInt64(reader.Take("step_index"), path + ".step_index");
            reader.Complete();
            if (!string.Equals(schemaId, ClockSchemaId, StringComparison.Ordinal) || schemaVersion != SimulationClockV1.CurrentSchemaVersion)
            {
                throw CodecFailure.Unsupported(path + ".schema_version", "Only simulation clock schema version 1 is supported.");
            }

            ContractValidationResult<SimulationClockV1> result = SimulationClockV1.TryCreate(schemaVersion, time, step);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteCommand(JsonTextWriter writer, SimulationCommandV1 command)
        {
            if (command == null)
            {
                throw CodecFailure.Invalid("command", "A serialized command may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "event_rank", (ushort)command.EventRank);
            JsonCodec.WriteProperty(writer, "sequence", command.Sequence);
            JsonCodec.WriteProperty(writer, "command_id", command.CommandId.ToString());
            writer.WritePropertyName("owner");
            WriteOwner(writer, command.Owner);
            writer.WritePropertyName("body");
            WriteBody(writer, command.Body);
            JsonCodec.WriteProperty(writer, "enqueue_time_s", command.EnqueueTimeSeconds);
            JsonCodec.WriteProperty(writer, "due_time_s", command.DueTimeSeconds);
            writer.WritePropertyName("state_binding");
            WriteStateBinding(writer, command.StateBinding);
            writer.WriteEndObject();
        }

        private static SimulationCommandV1 ReadCommand(
            JToken token,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding,
            string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            ushort eventRankValue = JsonCodec.ReadUInt16(reader.Take("event_rank"), path + ".event_rank");
            EventRankV1 eventRank = (EventRankV1)eventRankValue;
            ulong sequence = JsonCodec.ReadUInt64(reader.Take("sequence"), path + ".sequence");
            StableId commandId = JsonCodec.ReadStableId(reader.Take("command_id"), path + ".command_id");
            EventOwnerV1 owner = ReadOwner(
                reader.Take("owner"),
                configuration.Topology,
                path + ".owner");
            EventBodyV1 body = ReadBody(reader.Take("body"), path + ".body");
            double enqueueTime = JsonCodec.ReadDouble(reader.Take("enqueue_time_s"), path + ".enqueue_time_s");
            double dueTime = JsonCodec.ReadDouble(reader.Take("due_time_s"), path + ".due_time_s");
            StateBindingV1 binding = ReadStateBinding(reader.Take("state_binding"), path + ".state_binding");
            reader.Complete();
            if (!JsonCodec.BindingsEqual(binding, expectedStateBinding))
            {
                throw CodecFailure.Invalid(
                    path + ".state_binding",
                    "The command state binding does not equal the supplied replay state authority.");
            }

            JsonCodec.RequireCanonicalNonnegative(enqueueTime, path + ".enqueue_time_s");
            JsonCodec.RequireCanonicalNonnegative(dueTime, path + ".due_time_s");
            ContractValidationResult<SimulationCommandV1> result = SimulationCommandV1.TryCreate(
                eventRank,
                sequence,
                commandId,
                owner,
                body,
                enqueueTime,
                dueTime,
                binding);
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static void WriteOwner(JsonTextWriter writer, EventOwnerV1 owner)
        {
            if (owner == null)
            {
                throw CodecFailure.Invalid("owner", "A serialized event owner may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "kind", (byte)owner.Kind);
            JsonCodec.WriteProperty(writer, "channel_id", owner.ChannelId.Value);
            JsonCodec.WriteProperty(writer, "key_hex", JsonCodec.ToHex(owner.KeyBytes));
            writer.WriteEndObject();
        }

        private static EventOwnerV1 ReadOwner(JToken token, CoreTopology topology, string path)
        {
            if (topology == null)
            {
                throw CodecFailure.Invalid(path, "A validated topology is required for owner restore.");
            }

            JsonObjectReader reader = new JsonObjectReader(token, path);
            EventOwnerKindV1 kind = (EventOwnerKindV1)JsonCodec.ReadByte(reader.Take("kind"), path + ".kind");
            ChannelId channelId = new ChannelId(JsonCodec.ReadUInt32(reader.Take("channel_id"), path + ".channel_id"));
            byte[] keyBytes = JsonCodec.ReadHex(reader.Take("key_hex"), path + ".key_hex");
            reader.Complete();
            ContractValidationResult<EventOwnerV1> result;
            if (kind == EventOwnerKindV1.Channel)
            {
                ContractValidationResult<EventOwnerV1> shape = EventOwnerV1.TryRestoreSerialized(
                    kind,
                    channelId,
                    keyBytes);
                if (!shape.IsValid)
                {
                    throw CodecFailure.FromDiagnostic(shape.FirstDiagnostic);
                }

                result = EventOwnerV1.TryForChannel(topology, channelId);
            }
            else
            {
                result = EventOwnerV1.TryRestoreSerialized(kind, channelId, keyBytes);
            }
            if (!result.IsValid)
            {
                throw CodecFailure.FromDiagnostic(result.FirstDiagnostic);
            }

            return result.Value;
        }

        private static ContractValidationResult<bool> ValidateReplayContext(
            CommandQueueV1 initialQueue,
            CommandReplayLogV1 log,
            SimulationConfiguration configuration,
            StateBindingV1 expectedStateBinding)
        {
            if (configuration == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandReplayArchive.Configuration.Missing",
                    "configuration",
                    "A validated simulation configuration is required for replay serialization.");
            }

            if (expectedStateBinding == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "CommandReplayArchive.StateBinding.Missing",
                    "expected_state_binding",
                    "A replay serialization requires the current state-binding authority.");
            }

            if (configuration.Topology == null || configuration.DataPack == null ||
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
                    "CommandReplayArchive.StateBinding.ProvenanceMismatch",
                    "expected_state_binding",
                    "The replay state-binding authority must use the validated topology and data-pack versions.");
            }

            var commands = new List<SimulationCommandV1>();
            commands.AddRange(initialQueue.PendingCommands);
            foreach (CommandReplayEntryV1 entry in log.Entries)
            {
                if (entry.Operation == CommandReplayOperationKindV1.Enqueue && entry.Command != null)
                {
                    commands.Add(entry.Command);
                }
            }

            for (int i = 0; i < commands.Count; i++)
            {
                SimulationCommandV1 command = commands[i];
                ContractValidationResult<EventOwnerV1> ownerShape = EventOwnerV1.TryRestoreSerialized(
                    command.Owner.Kind,
                    command.Owner.ChannelId,
                    command.Owner.KeyBytes.ToArray());
                ContractValidationResult<EventOwnerV1> owner = ownerShape.IsValid &&
                    command.Owner.Kind == EventOwnerKindV1.Channel
                    ? EventOwnerV1.TryForChannel(configuration.Topology, command.Owner.ChannelId)
                    : ownerShape;
                if (!owner.IsValid)
                {
                    return ContractValidationResult<bool>.Invalid(
                        owner.FirstDiagnostic.Code,
                        "commands[" + i.ToString(CultureInfo.InvariantCulture) + "].owner",
                        owner.FirstDiagnostic.Message);
                }

                if (!JsonCodec.BindingsEqual(command.StateBinding, expectedStateBinding))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "CommandReplayArchive.StateBinding.Mismatch",
                        "commands[" + i.ToString(CultureInfo.InvariantCulture) + "].state_binding",
                        "Every replay command must bind the supplied immutable state authority.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static void ValidateArchiveDigests(
            SimulationConfiguration configuration,
            Digest32 topologyDigest,
            Digest32 dataPackDigest)
        {
            if (configuration.DataPack == null)
            {
                throw CodecFailure.Invalid(
                    "configuration.data_pack",
                    "A validated data-pack descriptor is required for replay restore.");
            }

            Digest32 expectedTopologyDigest = new Digest32(configuration.DataPack.TopologyDigest.ToArray());
            if (!expectedTopologyDigest.Equals(topologyDigest))
            {
                throw CodecFailure.Invalid(
                    "command_replay_archive.topology_digest",
                    "The replay archive topology digest does not match the validated data-pack authority.");
            }

            Digest32 expectedDataPackDigest = new Digest32(configuration.DataPack.ContentDigest.ToArray());
            if (!expectedDataPackDigest.Equals(dataPackDigest))
            {
                throw CodecFailure.Invalid(
                    "command_replay_archive.data_pack_digest",
                    "The replay archive data-pack digest does not match the validated data-pack authority.");
            }
        }

        private static void WriteBody(JsonTextWriter writer, EventBodyV1 body)
        {
            if (body == null)
            {
                throw CodecFailure.Invalid("body", "A serialized event body may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "kind", (byte)body.Kind);
            JsonCodec.WriteProperty(writer, "schema_id", body.SchemaId);
            writer.WriteEndObject();
        }

        private static EventBodyV1 ReadBody(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            byte kind = JsonCodec.ReadByte(reader.Take("kind"), path + ".kind");
            string schemaId = JsonCodec.ReadString(reader.Take("schema_id"), path + ".schema_id");
            reader.Complete();
            if (kind != (byte)EventBodyKindV1.NotApplicable ||
                !string.Equals(schemaId, "NotApplicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path, "Only the approved NotApplicable event body is implemented in this bounded task.");
            }

            return EventBodyV1.NotApplicable;
        }

        private static void WriteStateBinding(JsonTextWriter writer, StateBindingV1 binding)
        {
            if (binding == null)
            {
                throw CodecFailure.Invalid("state_binding", "A serialized state binding may not be null.");
            }

            writer.WriteStartObject();
            JsonCodec.WriteProperty(writer, "core_state_version", binding.CoreStateVersion);
            writer.WritePropertyName("spatial_state_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.SpatialStateVersion);
            writer.WritePropertyName("spatial_solve_id");
            JsonCodec.WriteOptionalStableId(writer, binding.SpatialSolveId);
            writer.WritePropertyName("power_snapshot_id");
            JsonCodec.WriteOptionalStableId(writer, binding.PowerSnapshotId);
            writer.WritePropertyName("power_snapshot_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.PowerSnapshotVersion);
            writer.WritePropertyName("kinetic_step_index");
            JsonCodec.WriteOptionalUInt64(writer, binding.KineticStepIndex);
            writer.WritePropertyName("nuclide_state_version");
            JsonCodec.WriteOptionalUInt64(writer, binding.NuclideStateVersion);
            JsonCodec.WriteProperty(writer, "topology_version", binding.TopologyVersion);
            JsonCodec.WriteProperty(writer, "data_pack_version", binding.DataPackVersion);
            writer.WritePropertyName("coefficient_digest");
            JsonCodec.WriteOptionalDigest(writer, binding.CoefficientDigest);
            writer.WritePropertyName("snapshot_digest");
            JsonCodec.WriteOptionalDigest(writer, binding.SnapshotDigest);
            writer.WriteEndObject();
        }

        private static StateBindingV1 ReadStateBinding(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            ulong coreStateVersion = JsonCodec.ReadUInt64(reader.Take("core_state_version"), path + ".core_state_version");
            OptionalUInt64 spatialStateVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("spatial_state_version"), path + ".spatial_state_version");
            OptionalStableId spatialSolveId = JsonCodec.ReadOptionalStableId(
                reader.Take("spatial_solve_id"), path + ".spatial_solve_id");
            OptionalStableId powerSnapshotId = JsonCodec.ReadOptionalStableId(
                reader.Take("power_snapshot_id"), path + ".power_snapshot_id");
            OptionalUInt64 powerSnapshotVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("power_snapshot_version"), path + ".power_snapshot_version");
            OptionalUInt64 kineticStepIndex = JsonCodec.ReadOptionalUInt64(
                reader.Take("kinetic_step_index"), path + ".kinetic_step_index");
            OptionalUInt64 nuclideStateVersion = JsonCodec.ReadOptionalUInt64(
                reader.Take("nuclide_state_version"), path + ".nuclide_state_version");
            string topologyVersion = JsonCodec.ReadString(reader.Take("topology_version"), path + ".topology_version");
            string dataPackVersion = JsonCodec.ReadString(reader.Take("data_pack_version"), path + ".data_pack_version");
            OptionalDigest32 coefficientDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("coefficient_digest"), path + ".coefficient_digest");
            OptionalDigest32 snapshotDigest = JsonCodec.ReadOptionalDigest(
                reader.Take("snapshot_digest"), path + ".snapshot_digest");
            reader.Complete();
            return new StateBindingV1(
                coreStateVersion,
                spatialStateVersion,
                spatialSolveId,
                powerSnapshotId,
                powerSnapshotVersion,
                kineticStepIndex,
                nuclideStateVersion,
                topologyVersion,
                dataPackVersion,
                coefficientDigest,
                snapshotDigest);
        }
    }

    internal sealed class CodecFailure : Exception
    {
        private CodecFailure(string code, string path, string message)
        {
            Code = code;
            Path = path;
            Message = message;
        }

        public string Code { get; }

        public string Path { get; }

        public new string Message { get; }

        public static CodecFailure Invalid(string path, string message)
        {
            return new CodecFailure("Serialization.Input.Invalid", path, message);
        }

        public static CodecFailure Unsupported(string path, string message)
        {
            return new CodecFailure("Serialization.Schema.Unsupported", path, message);
        }

        public static CodecFailure FromDiagnostic(ContractDiagnostic diagnostic)
        {
            return new CodecFailure(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }
    }

    internal sealed class JsonObjectReader
    {
        private readonly JProperty[] _properties;
        private readonly string _path;
        private int _index;

        public JsonObjectReader(JToken token, string path)
        {
            JObject? value = token as JObject;
            if (value == null)
            {
                throw CodecFailure.Invalid(path, "An object token is required.");
            }

            _properties = value.Properties().ToArray();
            _path = path;
        }

        public JToken Take(string expectedName)
        {
            if (_index >= _properties.Length)
            {
                throw CodecFailure.Invalid(
                    _path + "." + expectedName,
                    "A required field is missing or the object ended before the canonical field order completed.");
            }

            JProperty property = _properties[_index++];
            if (!string.Equals(property.Name, expectedName, StringComparison.Ordinal))
            {
                throw CodecFailure.Invalid(
                    _path + "." + expectedName,
                    "Fields must be present exactly once in the approved canonical order.");
            }

            return property.Value;
        }

        public void Complete()
        {
            if (_index != _properties.Length)
            {
                throw CodecFailure.Invalid(
                    _path,
                    "The object contains an unknown, duplicate, or reordered field.");
            }
        }
    }

    internal static class JsonCodec
    {
        public static string Write(Action<JsonTextWriter> write)
        {
            var builder = new StringBuilder();
            using (var textWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(textWriter))
            {
                writer.Formatting = Formatting.None;
                writer.Culture = CultureInfo.InvariantCulture;
                writer.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                write(writer);
                writer.Flush();
            }

            return builder.ToString();
        }

        public static void WriteProperty(JsonTextWriter writer, string name, string value)
        {
            if (value == null)
            {
                throw CodecFailure.Invalid(name, "A required string field may not be null.");
            }

            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static void WriteProperty(JsonTextWriter writer, string name, uint value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static void WriteProperty(JsonTextWriter writer, string name, ushort value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static void WriteProperty(JsonTextWriter writer, string name, byte value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static void WriteProperty(JsonTextWriter writer, string name, ulong value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static void WriteProperty(JsonTextWriter writer, string name, double value)
        {
            RequireCanonicalNonnegative(value, name);
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public static JsonObjectReader ParseRoot(string json, string path)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw CodecFailure.Invalid(path, "A nonempty JSON document is required.");
            }

            RejectSignedNegativeZero(json, path);
            RejectComments(json, path);

            using (var textReader = new StringReader(json))
            using (var reader = new JsonTextReader(textReader))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                reader.Culture = CultureInfo.InvariantCulture;
                if (!reader.Read())
                {
                    throw CodecFailure.Invalid(path, "The JSON document is empty.");
                }

                if (reader.TokenType == JsonToken.Comment)
                {
                    throw CodecFailure.Invalid(path, "Comments are not part of the canonical JSON document.");
                }

                JToken token = JToken.ReadFrom(
                    reader,
                    new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                if (reader.Read())
                {
                    throw CodecFailure.Invalid(path, "Trailing JSON tokens are not permitted.");
                }

                return new JsonObjectReader(token, path);
            }
        }

        public static string ReadString(JToken token, string path)
        {
            if (token == null || token.Type != JTokenType.String)
            {
                throw CodecFailure.Invalid(path, "A JSON string token is required.");
            }

            string? value = token.Value<string>();
            if (value == null)
            {
                throw CodecFailure.Invalid(path, "A JSON string value may not be null.");
            }

            return value;
        }

        public static uint ReadUInt32(JToken token, string path)
        {
            ulong value = ReadInteger(token, path);
            if (value > uint.MaxValue)
            {
                throw CodecFailure.Invalid(path, "The integer exceeds UInt32 range.");
            }

            return (uint)value;
        }

        public static ushort ReadUInt16(JToken token, string path)
        {
            ulong value = ReadInteger(token, path);
            if (value > ushort.MaxValue)
            {
                throw CodecFailure.Invalid(path, "The integer exceeds UInt16 range.");
            }

            return (ushort)value;
        }

        public static byte ReadByte(JToken token, string path)
        {
            ulong value = ReadInteger(token, path);
            if (value > byte.MaxValue)
            {
                throw CodecFailure.Invalid(path, "The integer exceeds byte range.");
            }

            return (byte)value;
        }

        public static ulong ReadUInt64(JToken token, string path)
        {
            return ReadInteger(token, path);
        }

        private static ulong ReadInteger(JToken token, string path)
        {
            if (token == null || token.Type != JTokenType.Integer)
            {
                throw CodecFailure.Invalid(path, "A nonnegative JSON integer token is required.");
            }

            string text = token.ToString(Formatting.None);
            ulong value;
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                throw CodecFailure.Invalid(path, "The integer is outside UInt64 range or is not canonical.");
            }

            return value;
        }

        public static double ReadDouble(JToken token, string path)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            {
                throw CodecFailure.Invalid(path, "A JSON numeric token is required.");
            }

            string tokenText = token.ToString(Formatting.None);
            double value;
            try
            {
                value = token.Value<double>();
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException || exception is InvalidCastException)
            {
                throw CodecFailure.Invalid(path, "The numeric token is not a finite binary64 value.");
            }

            if (!ContractValidation.IsFinite(value))
            {
                throw CodecFailure.Invalid(path, "NaN and infinity are not valid serialized values.");
            }

            if (value == 0.0 && tokenText.StartsWith('-'))
            {
                throw CodecFailure.Invalid(path, "Signed negative zero is not a canonical serialized value.");
            }

            return value;
        }

        public static JArray ReadArray(JToken token, string path)
        {
            JArray? array = token as JArray;
            if (array == null)
            {
                throw CodecFailure.Invalid(path, "A JSON array token is required.");
            }

            return array!;
        }

        public static StableId ReadStableId(JToken token, string path)
        {
            string text = ReadString(token, path);
            StableId value;
            if (!StableId.TryParse(text, out value) || value.IsEmpty)
            {
                throw CodecFailure.Invalid(path, "The value must be a nonempty canonical lowercase UUID.");
            }

            return value;
        }

        public static Digest32 ReadDigest(JToken token, string path)
        {
            return new Digest32(ReadHex(token, path, 32));
        }

        public static byte[] ReadHex(JToken token, string path)
        {
            return ReadHex(token, path, -1);
        }

        private static byte[] ReadHex(JToken token, string path, int expectedByteCount)
        {
            string text = ReadString(token, path);
            if ((text.Length & 1) != 0 || text.Any(character => !IsLowerHex(character)) ||
                (expectedByteCount >= 0 && text.Length != expectedByteCount * 2))
            {
                throw CodecFailure.Invalid(path, "The value must be canonical lowercase hexadecimal bytes of the expected length.");
            }

            byte[] bytes = new byte[text.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)((HexValue(text[i * 2]) << 4) | HexValue(text[(i * 2) + 1]));
            }

            return bytes;
        }

        public static void WriteOptionalStableId(JsonTextWriter writer, OptionalStableId value)
        {
            if (value == null)
            {
                throw CodecFailure.Invalid("optional_stable_id", "An optional stable identity wrapper may not be null.");
            }

            writer.WriteStartObject();
            WriteProperty(writer, "tag", value.IsApplicable ? "Applicable" : "NotApplicable");
            if (value.IsApplicable)
            {
                WriteProperty(writer, "value", value.Value.ToString());
            }

            writer.WriteEndObject();
        }

        public static OptionalStableId ReadOptionalStableId(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return OptionalStableId.NotApplicable;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional identity tag is unsupported.");
            }

            StableId value = ReadStableId(reader.Take("value"), path + ".value");
            reader.Complete();
            return OptionalStableId.Applicable(value);
        }

        public static void WriteOptionalUInt64(JsonTextWriter writer, OptionalUInt64 value)
        {
            if (value == null)
            {
                throw CodecFailure.Invalid("optional_uint64", "An optional UInt64 wrapper may not be null.");
            }

            writer.WriteStartObject();
            WriteProperty(writer, "tag", value.IsApplicable ? "Applicable" : "NotApplicable");
            if (value.IsApplicable)
            {
                WriteProperty(writer, "value", value.Value);
            }

            writer.WriteEndObject();
        }

        public static OptionalUInt64 ReadOptionalUInt64(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return OptionalUInt64.NotApplicable;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional UInt64 tag is unsupported.");
            }

            ulong value = ReadUInt64(reader.Take("value"), path + ".value");
            reader.Complete();
            return OptionalUInt64.Applicable(value);
        }

        public static void WriteOptionalDigest(JsonTextWriter writer, OptionalDigest32 value)
        {
            if (value == null)
            {
                throw CodecFailure.Invalid("optional_digest", "An optional digest wrapper may not be null.");
            }

            writer.WriteStartObject();
            WriteProperty(writer, "tag", value.IsApplicable ? "Applicable" : "NotApplicable");
            if (value.IsApplicable)
            {
                if (value.Value == null)
                {
                    throw CodecFailure.Invalid("optional_digest.value", "An applicable digest must have a value.");
                }

                WriteProperty(writer, "value", ToHex(value.Value.Bytes));
            }

            writer.WriteEndObject();
        }

        public static OptionalDigest32 ReadOptionalDigest(JToken token, string path)
        {
            JsonObjectReader reader = new JsonObjectReader(token, path);
            string tag = ReadString(reader.Take("tag"), path + ".tag");
            if (string.Equals(tag, "NotApplicable", StringComparison.Ordinal))
            {
                reader.Complete();
                return OptionalDigest32.NotApplicable;
            }

            if (!string.Equals(tag, "Applicable", StringComparison.Ordinal))
            {
                throw CodecFailure.Unsupported(path + ".tag", "The optional digest tag is unsupported.");
            }

            Digest32 value = ReadDigest(reader.Take("value"), path + ".value");
            reader.Complete();
            return OptionalDigest32.Applicable(value);
        }

        public static string ToHex(IReadOnlyList<byte> bytes)
        {
            if (bytes == null)
            {
                throw CodecFailure.Invalid("digest", "A byte sequence may not be null.");
            }

            var builder = new StringBuilder(bytes.Count * 2);
            for (int i = 0; i < bytes.Count; i++)
            {
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static void RequireCanonicalNonnegative(double value, string path)
        {
            if (!ContractValidation.IsFinite(value) || value < 0 || BitConverter.DoubleToInt64Bits(value) < 0)
            {
                throw CodecFailure.Invalid(path, "The value must be finite, nonnegative, and must not be signed negative zero.");
            }
        }

        public static void RequireCanonicalPositive(double value, string path)
        {
            RequireCanonicalNonnegative(value, path);
            if (value <= 0)
            {
                throw CodecFailure.Invalid(path, "The value must be strictly positive.");
            }
        }

        public static bool BindingsEqual(StateBindingV1 left, StateBindingV1 right)
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

        public static bool IsInputException(Exception exception)
        {
            return exception is JsonException || exception is FormatException ||
                   exception is OverflowException || exception is InvalidCastException ||
                   exception is ArgumentException || exception is InvalidOperationException;
        }

        private static bool IsLowerHex(char value)
        {
            return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
        }

        private static void RejectSignedNegativeZero(string json, string path)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '-' && i + 1 < json.Length && json[i + 1] == '0' &&
                    (i == 0 || char.IsWhiteSpace(json[i - 1]) || json[i - 1] == '[' ||
                     json[i - 1] == '{' || json[i - 1] == ',' || json[i - 1] == ':'))
                {
                    throw CodecFailure.Invalid(path, "Signed negative zero is not a canonical serialized value.");
                }
            }
        }

        private static void RejectComments(string json, string path)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i + 1 < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '/' && (json[i + 1] == '/' || json[i + 1] == '*'))
                {
                    throw CodecFailure.Invalid(path, "Comments are not part of the canonical JSON document.");
                }
            }
        }

        private static int HexValue(char value)
        {
            return value <= '9' ? value - '0' : value - 'a' + 10;
        }
    }
}
