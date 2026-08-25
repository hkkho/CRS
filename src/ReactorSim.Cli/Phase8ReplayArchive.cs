using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    internal enum Phase8ReplayCommandKindV1 : byte
    {
        AdvanceWall = 0,
        SetPowerTarget = 1,
        SetTiltTarget = 2,
        SetPlaybackMode = 3,
        Pause = 4,
        Resume = 5
    }

    internal sealed class Phase8ReplayCommandV1
    {
        private Phase8ReplayCommandV1(
            Phase8ReplayCommandKindV1 kind,
            ulong wallMilliseconds,
            double targetFraction,
            string playbackModeId)
        {
            Kind = kind;
            WallMilliseconds = wallMilliseconds;
            TargetFraction = targetFraction;
            PlaybackModeId = playbackModeId;
        }

        public Phase8ReplayCommandKindV1 Kind { get; }

        public ulong WallMilliseconds { get; }

        public double TargetFraction { get; }

        public string PlaybackModeId { get; }

        public static Phase8ReplayCommandV1 Advance(ulong wallMilliseconds)
        {
            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.AdvanceWall,
                wallMilliseconds,
                0.0,
                string.Empty);
        }

        public static Phase8ReplayCommandV1 SetPowerTarget(double targetFraction)
        {
            ValidateFiniteTarget(targetFraction);
            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.SetPowerTarget,
                0,
                targetFraction,
                string.Empty);
        }

        public static Phase8ReplayCommandV1 SetTiltTarget(double targetFraction)
        {
            ValidateFiniteTarget(targetFraction);
            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.SetTiltTarget,
                0,
                targetFraction,
                string.Empty);
        }

        public static Phase8ReplayCommandV1 SetPlaybackMode(string playbackModeId)
        {
            if (string.IsNullOrWhiteSpace(playbackModeId))
            {
                throw new Phase8ReplayArchiveFailure(
                    "command.playback_mode_id",
                    "A playback command requires a nonempty mode identifier.");
            }

            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.SetPlaybackMode,
                0,
                0.0,
                playbackModeId);
        }

        public static Phase8ReplayCommandV1 Pause()
        {
            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.Pause,
                0,
                0.0,
                string.Empty);
        }

        public static Phase8ReplayCommandV1 Resume()
        {
            return new Phase8ReplayCommandV1(
                Phase8ReplayCommandKindV1.Resume,
                0,
                0.0,
                string.Empty);
        }

        private static void ValidateFiniteTarget(double targetFraction)
        {
            if (double.IsNaN(targetFraction) || double.IsInfinity(targetFraction))
            {
                throw new Phase8ReplayArchiveFailure(
                    "command.target_fraction",
                    "A replay target must be finite.");
            }
        }
    }

    internal sealed class Phase8ReplayArchiveV1
    {
        public const string Format = "reactorsim.p8-replay-archive/v1";
        public const uint SchemaVersion = 1;
        public const int MaximumCommandCount = 4096;

        public Phase8ReplayArchiveV1(
            string scenarioId,
            string difficultyId,
            ulong seed,
            string initialPlaybackModeId,
            string scenarioParameterSha256,
            string scoringParameterSha256,
            IReadOnlyList<Phase8ReplayCommandV1> commands,
            string expectedFinalStateDigest)
        {
            ScenarioId = scenarioId ?? throw new ArgumentNullException(nameof(scenarioId));
            DifficultyId = difficultyId ?? throw new ArgumentNullException(nameof(difficultyId));
            InitialPlaybackModeId = initialPlaybackModeId ?? throw new ArgumentNullException(nameof(initialPlaybackModeId));
            ScenarioParameterSha256 = scenarioParameterSha256 ?? throw new ArgumentNullException(nameof(scenarioParameterSha256));
            ScoringParameterSha256 = scoringParameterSha256 ?? throw new ArgumentNullException(nameof(scoringParameterSha256));
            ExpectedFinalStateDigest = expectedFinalStateDigest ?? throw new ArgumentNullException(nameof(expectedFinalStateDigest));
            ArgumentNullException.ThrowIfNull(commands);

            Commands = new ReadOnlyCollection<Phase8ReplayCommandV1>(
                new List<Phase8ReplayCommandV1>(commands));
            Seed = seed;
        }

        public string ScenarioId { get; }

        public string DifficultyId { get; }

        public ulong Seed { get; }

        public string InitialPlaybackModeId { get; }

        public string ScenarioParameterSha256 { get; }

        public string ScoringParameterSha256 { get; }

        public IReadOnlyList<Phase8ReplayCommandV1> Commands { get; }

        public string ExpectedFinalStateDigest { get; }
    }

    internal static class Phase8ReplayArchiveCodecV1
    {
        private static readonly string[] KindOnlyProperties = { "kind" };
        private static readonly string[] AdvanceProperties = { "kind", "wall_milliseconds" };
        private static readonly string[] PowerTargetProperties = { "kind", "target_fraction" };
        private static readonly string[] TiltTargetProperties = { "kind", "target_fraction" };
        private static readonly string[] PlaybackProperties = { "kind", "playback_mode_id" };

        private static readonly string[] RootProperties =
        {
            "format",
            "schema_version",
            "scenario_id",
            "difficulty_id",
            "seed",
            "initial_playback_mode_id",
            "scenario_parameter_sha256",
            "scoring_parameter_sha256",
            "commands",
            "expected_final_state_digest"
        };

        public static string Serialize(Phase8ReplayArchiveV1 archive)
        {
            ArgumentNullException.ThrowIfNull(archive);

            ValidateArchive(archive);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            writer.WriteString("format", Phase8ReplayArchiveV1.Format);
            writer.WriteNumber("schema_version", Phase8ReplayArchiveV1.SchemaVersion);
            writer.WriteString("scenario_id", archive.ScenarioId);
            writer.WriteString("difficulty_id", archive.DifficultyId);
            writer.WriteNumber("seed", archive.Seed);
            writer.WriteString("initial_playback_mode_id", archive.InitialPlaybackModeId);
            writer.WriteString("scenario_parameter_sha256", archive.ScenarioParameterSha256);
            writer.WriteString("scoring_parameter_sha256", archive.ScoringParameterSha256);
            writer.WriteStartArray("commands");
            foreach (Phase8ReplayCommandV1 command in archive.Commands)
            {
                WriteCommand(writer, command);
            }

            writer.WriteEndArray();
            writer.WriteString("expected_final_state_digest", archive.ExpectedFinalStateDigest);
            writer.WriteEndObject();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static void Save(string path, Phase8ReplayArchiveV1 archive)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "A replay archive path is required.");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (ArgumentException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is invalid: " + exception.Message,
                    exception);
            }

            string content = Serialize(archive);
            try
            {
                File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (IOException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive could not be written: " + exception.Message,
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive could not be written: " + exception.Message,
                    exception);
            }
            catch (ArgumentException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is invalid: " + exception.Message,
                    exception);
            }
            catch (NotSupportedException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is not supported: " + exception.Message,
                    exception);
            }
        }

        public static Phase8ReplayArchiveV1 Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "A replay archive path is required.");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (ArgumentException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is invalid: " + exception.Message,
                    exception);
            }

            try
            {
                return Parse(File.ReadAllText(fullPath));
            }
            catch (Phase8ReplayArchiveFailure)
            {
                throw;
            }
            catch (IOException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive could not be read: " + exception.Message,
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive could not be read: " + exception.Message,
                    exception);
            }
            catch (ArgumentException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is invalid: " + exception.Message,
                    exception);
            }
            catch (NotSupportedException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "path",
                    "The replay archive path is not supported: " + exception.Message,
                    exception);
            }
        }

        public static Phase8ReplayArchiveV1 Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow
                    });
                Dictionary<string, JsonElement> root = ReadExactObject(
                    document.RootElement,
                    "root",
                    RootProperties);

                string format = ReadString(root["format"], "format", requireNonempty: true);
                if (!string.Equals(format, Phase8ReplayArchiveV1.Format, StringComparison.Ordinal))
                {
                    throw Failure("format", "The replay archive format identifier is not supported.");
                }

                uint schemaVersion = ReadUInt32(root["schema_version"], "schema_version");
                if (schemaVersion != Phase8ReplayArchiveV1.SchemaVersion)
                {
                    throw Failure("schema_version", "The replay archive schema version is not supported.");
                }

                string scenarioId = ReadString(root["scenario_id"], "scenario_id", requireNonempty: true);
                string difficultyId = ReadString(root["difficulty_id"], "difficulty_id", requireNonempty: true);
                ulong seed = ReadUInt64(root["seed"], "seed");
                string initialPlaybackModeId = ReadString(
                    root["initial_playback_mode_id"],
                    "initial_playback_mode_id",
                    requireNonempty: true);
                string scenarioParameterSha256 = ReadSha256(
                    root["scenario_parameter_sha256"],
                    "scenario_parameter_sha256");
                string scoringParameterSha256 = ReadSha256(
                    root["scoring_parameter_sha256"],
                    "scoring_parameter_sha256");
                JsonElement commandElement = root["commands"];
                if (commandElement.ValueKind != JsonValueKind.Array)
                {
                    throw Failure("commands", "The replay command collection must be a JSON array.");
                }

                var commands = new List<Phase8ReplayCommandV1>();
                int commandIndex = 0;
                foreach (JsonElement element in commandElement.EnumerateArray())
                {
                    if (commandIndex >= Phase8ReplayArchiveV1.MaximumCommandCount)
                    {
                        throw Failure(
                            "commands",
                            "The replay command collection exceeds the approved maximum of " +
                            Phase8ReplayArchiveV1.MaximumCommandCount.ToString(CultureInfo.InvariantCulture) + ".");
                    }

                    commands.Add(ParseCommand(element, commandIndex));
                    commandIndex++;
                }

                string expectedFinalStateDigest = ReadSha256(
                    root["expected_final_state_digest"],
                    "expected_final_state_digest");
                var archive = new Phase8ReplayArchiveV1(
                    scenarioId,
                    difficultyId,
                    seed,
                    initialPlaybackModeId,
                    scenarioParameterSha256,
                    scoringParameterSha256,
                    commands,
                    expectedFinalStateDigest);
                ValidateArchive(archive);
                return archive;
            }
            catch (Phase8ReplayArchiveFailure)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new Phase8ReplayArchiveFailure(
                    "document",
                    "The replay archive is not valid JSON: " + exception.Message,
                    exception);
            }
        }

        internal static string FormatFailure(Phase8ReplayArchiveFailure exception)
        {
            return exception.Path + ": " + exception.Message;
        }

        private static Phase8ReplayCommandV1 ParseCommand(JsonElement element, int commandIndex)
        {
            string path = "commands[" + commandIndex.ToString(CultureInfo.InvariantCulture) + "]";
            string kind = ReadCommandKind(element, path);
            switch (kind)
            {
                case "advance_wall":
                    return ParseAdvanceCommand(element, path);

                case "set_power_target":
                    return ParseTargetCommand(element, path, Phase8ReplayCommandKindV1.SetPowerTarget);

                case "set_tilt_target":
                    return ParseTargetCommand(element, path, Phase8ReplayCommandKindV1.SetTiltTarget);

                case "set_playback_mode":
                    return ParsePlaybackCommand(element, path);

                case "pause":
                    ReadExactObject(element, path, KindOnlyProperties);
                    return Phase8ReplayCommandV1.Pause();

                case "resume":
                    ReadExactObject(element, path, KindOnlyProperties);
                    return Phase8ReplayCommandV1.Resume();

                default:
                    throw Failure(path + ".kind", "The replay command kind is not supported.");
            }
        }

        private static string ReadCommandKind(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Failure(path, "The replay command must be a JSON object.");
            }

            using JsonElement.ObjectEnumerator properties = element.EnumerateObject();
            if (!properties.MoveNext())
            {
                throw Failure(path, "A replay command requires a kind property.");
            }

            JsonProperty first = properties.Current;
            if (!string.Equals(first.Name, "kind", StringComparison.Ordinal))
            {
                throw Failure(path + "." + first.Name, "The replay command kind must be the first property.");
            }

            return ReadString(first.Value, path + ".kind", requireNonempty: true);
        }

        private static Phase8ReplayCommandV1 ParseAdvanceCommand(JsonElement element, string path)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                path,
                AdvanceProperties);
            string kind = ReadString(properties["kind"], path + ".kind", requireNonempty: true);
            if (!string.Equals(kind, "advance_wall", StringComparison.Ordinal))
            {
                throw Failure(path + ".kind", "The replay command kind does not match its payload.");
            }

            return Phase8ReplayCommandV1.Advance(
                ReadUInt64(properties["wall_milliseconds"], path + ".wall_milliseconds"));
        }

        private static Phase8ReplayCommandV1 ParseTargetCommand(
            JsonElement element,
            string path,
            Phase8ReplayCommandKindV1 expectedKind)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                path,
                expectedKind == Phase8ReplayCommandKindV1.SetPowerTarget
                    ? PowerTargetProperties
                    : TiltTargetProperties);
            string kind = ReadString(properties["kind"], path + ".kind", requireNonempty: true);
            string expectedKindText = expectedKind == Phase8ReplayCommandKindV1.SetPowerTarget
                ? "set_power_target"
                : "set_tilt_target";
            if (!string.Equals(kind, expectedKindText, StringComparison.Ordinal))
            {
                throw Failure(path + ".kind", "The replay command kind does not match its payload.");
            }

            double target = ReadFiniteDouble(properties["target_fraction"], path + ".target_fraction");
            return expectedKind == Phase8ReplayCommandKindV1.SetPowerTarget
                ? Phase8ReplayCommandV1.SetPowerTarget(target)
                : Phase8ReplayCommandV1.SetTiltTarget(target);
        }

        private static Phase8ReplayCommandV1 ParsePlaybackCommand(JsonElement element, string path)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                path,
                PlaybackProperties);
            string kind = ReadString(properties["kind"], path + ".kind", requireNonempty: true);
            if (!string.Equals(kind, "set_playback_mode", StringComparison.Ordinal))
            {
                throw Failure(path + ".kind", "The replay command kind does not match its payload.");
            }

            return Phase8ReplayCommandV1.SetPlaybackMode(
                ReadString(properties["playback_mode_id"], path + ".playback_mode_id", requireNonempty: true));
        }

        private static Dictionary<string, JsonElement> ReadExactObject(
            JsonElement element,
            string path,
            string[] expectedProperties)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Failure(path, "The replay archive value must be a JSON object.");
            }

            var expected = new HashSet<string>(expectedProperties, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            int propertyIndex = 0;
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    throw Failure(path + "." + property.Name, "Duplicate JSON properties are not accepted.");
                }

                if (!expected.Contains(property.Name))
                {
                    throw Failure(path + "." + property.Name, "Unknown JSON property.");
                }

                if (propertyIndex >= expectedProperties.Length ||
                    !string.Equals(property.Name, expectedProperties[propertyIndex], StringComparison.Ordinal))
                {
                    throw Failure(path + "." + property.Name, "JSON properties must use the approved order.");
                }

                properties.Add(property.Name, property.Value);
                propertyIndex++;
            }

            if (propertyIndex != expectedProperties.Length)
            {
                throw Failure(
                    path,
                    "The replay archive object is missing property '" + expectedProperties[propertyIndex] + "'.");
            }

            return properties;
        }

        private static string ReadString(JsonElement element, string path, bool requireNonempty)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw Failure(path, "The replay archive value must be a JSON string.");
            }

            string? value = element.GetString();
            if (requireNonempty && string.IsNullOrWhiteSpace(value))
            {
                throw Failure(path, "The replay archive string must be nonempty.");
            }

            return value ?? string.Empty;
        }

        private static uint ReadUInt32(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint value))
            {
                throw Failure(path, "The replay archive value must be a nonnegative UInt32 JSON number.");
            }

            return value;
        }

        private static ulong ReadUInt64(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt64(out ulong value))
            {
                throw Failure(path, "The replay archive value must be a nonnegative UInt64 JSON number.");
            }

            return value;
        }

        private static double ReadFiniteDouble(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number ||
                !element.TryGetDouble(out double value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                throw Failure(path, "The replay archive value must be a finite JSON number.");
            }

            return value;
        }

        private static string ReadSha256(JsonElement element, string path)
        {
            string value = ReadString(element, path, requireNonempty: true);
            if (!IsLowerHexSha256(value))
            {
                throw Failure(path, "The replay archive value must be a lowercase 64-character SHA-256 digest.");
            }

            return value;
        }

        private static bool IsLowerHexSha256(string value)
        {
            if (value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateArchive(Phase8ReplayArchiveV1 archive)
        {
            if (!string.Equals(archive.ScenarioId, archive.ScenarioId.Trim(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(archive.ScenarioId) ||
                !string.Equals(archive.DifficultyId, archive.DifficultyId.Trim(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(archive.DifficultyId) ||
                !string.Equals(archive.InitialPlaybackModeId, archive.InitialPlaybackModeId.Trim(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(archive.InitialPlaybackModeId))
            {
                throw Failure("identity", "Replay identity values must be nonempty and must not have surrounding whitespace.");
            }

            if (!IsLowerHexSha256(archive.ScenarioParameterSha256) ||
                !IsLowerHexSha256(archive.ScoringParameterSha256) ||
                !IsLowerHexSha256(archive.ExpectedFinalStateDigest))
            {
                throw Failure("digest", "Replay archive digests must be lowercase 64-character SHA-256 values.");
            }

            if (archive.Commands.Count > Phase8ReplayArchiveV1.MaximumCommandCount)
            {
                throw Failure("commands", "The replay command collection exceeds the approved maximum.");
            }

            foreach (Phase8ReplayCommandV1 command in archive.Commands)
            {
                if (command == null)
                {
                    throw Failure("commands", "Replay command entries may not be null.");
                }

                switch (command.Kind)
                {
                    case Phase8ReplayCommandKindV1.AdvanceWall:
                        break;

                    case Phase8ReplayCommandKindV1.SetPowerTarget:
                    case Phase8ReplayCommandKindV1.SetTiltTarget:
                        if (double.IsNaN(command.TargetFraction) || double.IsInfinity(command.TargetFraction))
                        {
                            throw Failure("commands.target_fraction", "Replay targets must be finite.");
                        }

                        break;

                    case Phase8ReplayCommandKindV1.SetPlaybackMode:
                        if (string.IsNullOrWhiteSpace(command.PlaybackModeId))
                        {
                            throw Failure("commands.playback_mode_id", "Playback commands require a mode identifier.");
                        }

                        break;

                    case Phase8ReplayCommandKindV1.Pause:
                    case Phase8ReplayCommandKindV1.Resume:
                        break;

                    default:
                        throw Failure("commands.kind", "The replay command kind is not supported.");
                }
            }
        }

        private static void WriteCommand(Utf8JsonWriter writer, Phase8ReplayCommandV1 command)
        {
            writer.WriteStartObject();
            switch (command.Kind)
            {
                case Phase8ReplayCommandKindV1.AdvanceWall:
                    writer.WriteString("kind", "advance_wall");
                    writer.WriteNumber("wall_milliseconds", command.WallMilliseconds);
                    break;

                case Phase8ReplayCommandKindV1.SetPowerTarget:
                    writer.WriteString("kind", "set_power_target");
                    writer.WriteNumber("target_fraction", command.TargetFraction);
                    break;

                case Phase8ReplayCommandKindV1.SetTiltTarget:
                    writer.WriteString("kind", "set_tilt_target");
                    writer.WriteNumber("target_fraction", command.TargetFraction);
                    break;

                case Phase8ReplayCommandKindV1.SetPlaybackMode:
                    writer.WriteString("kind", "set_playback_mode");
                    writer.WriteString("playback_mode_id", command.PlaybackModeId);
                    break;

                case Phase8ReplayCommandKindV1.Pause:
                    writer.WriteString("kind", "pause");
                    break;

                case Phase8ReplayCommandKindV1.Resume:
                    writer.WriteString("kind", "resume");
                    break;

                default:
                    throw Failure("commands.kind", "The replay command kind is not supported.");
            }

            writer.WriteEndObject();
        }

        private static Phase8ReplayArchiveFailure Failure(string path, string message)
        {
            return new Phase8ReplayArchiveFailure(path, message);
        }
    }

    internal sealed class Phase8ReplayArchiveFailure : InvalidOperationException
    {
        public Phase8ReplayArchiveFailure(string path, string message)
            : base(message)
        {
            Path = path;
        }

        public Phase8ReplayArchiveFailure(string path, string message, Exception innerException)
            : base(message, innerException)
        {
            Path = path;
        }

        public string Path { get; }
    }

    internal static class Phase8ReplayStateDigest
    {
        public static string Compute(
            Phase8ScoredScenarioRuntimeV1 runtime,
            string scenarioParameterSha256,
            string scoringParameterSha256,
            string initialPlaybackModeId,
            IReadOnlyList<Phase8ReplayCommandV1> commands)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(scenarioParameterSha256);
            ArgumentNullException.ThrowIfNull(scoringParameterSha256);
            ArgumentNullException.ThrowIfNull(initialPlaybackModeId);
            ArgumentNullException.ThrowIfNull(commands);
            if (commands.Count > Phase8ReplayArchiveV1.MaximumCommandCount)
            {
                throw new InvalidOperationException("The replay digest command collection exceeded the approved maximum.");
            }

            var builder = new StringBuilder();
            AppendString(builder, "scenario_parameter_sha256", scenarioParameterSha256);
            AppendString(builder, "scoring_parameter_sha256", scoringParameterSha256);
            AppendString(builder, "initial_playback_mode_id", initialPlaybackModeId);
            AppendInt(builder, "command_count", commands.Count);
            for (int index = 0; index < commands.Count; index++)
            {
                AppendCommand(builder, commands[index], index);
            }

            AppendString(builder, "scenario_id", runtime.ScenarioId);
            AppendString(builder, "difficulty_id", runtime.DifficultyId);
            AppendUInt64(builder, "seed", runtime.Seed);
            AppendUInt64(builder, "replay_seed", runtime.ReplaySeed);
            AppendString(builder, "playback_mode_id", runtime.PlaybackModeId);
            AppendDouble(builder, "acceleration_factor", runtime.AccelerationFactor);
            AppendDouble(builder, "scenario_horizon_s", runtime.ScenarioHorizonSeconds);
            AppendDouble(builder, "simulation_time_s", runtime.SimulationTimeSeconds);
            AppendUInt64(builder, "simulation_step_index", runtime.SimulationStepIndex);
            AppendDouble(builder, "wall_elapsed_s", runtime.WallElapsedSeconds);
            AppendUInt32(builder, "wall_control_tick_ms", runtime.Runtime.WallControlTickMilliseconds);
            AppendDouble(builder, "normalized_power_fraction", runtime.NormalizedPowerFraction);
            AppendDouble(builder, "absolute_tilt_fraction", runtime.AbsoluteTiltFraction);
            AppendDouble(builder, "control_margin_fraction", runtime.ControlMarginFraction);
            AppendDouble(builder, "device_available_fraction", runtime.DeviceAvailableFraction);
            AppendUInt32(builder, "refuel_requests_remaining", runtime.RefuelRequestsRemaining);
            AppendUInt32(builder, "pending_action_count", runtime.PendingActionCount);
            AppendUInt32(builder, "processed_scripted_event_count", runtime.ProcessedScriptedEventCount);
            AppendBool(builder, "is_paused", runtime.IsPaused);
            AppendString(builder, "outcome", runtime.Outcome.ToString());

            Phase8ScoreSnapshotV1 score = runtime.Score;
            AppendDouble(builder, "score.total_points", score.TotalPoints);
            AppendDouble(builder, "score.survival_points", score.SurvivalPoints);
            AppendDouble(builder, "score.energy_quality", score.EnergyQuality);
            AppendDouble(builder, "score.energy_points", score.EnergyPoints);
            AppendDouble(builder, "score.stability_quality", score.StabilityQuality);
            AppendDouble(builder, "score.stability_points", score.StabilityPoints);
            AppendDouble(builder, "score.fuelling_efficiency", score.FuellingEfficiency);
            AppendDouble(builder, "score.fuelling_efficiency_points", score.FuellingEfficiencyPoints);
            AppendDouble(builder, "score.control_penalty_points", score.ControlPenaltyPoints);
            AppendDouble(builder, "score.loss_penalty_points", score.LossPenaltyPoints);
            AppendUInt32(builder, "score.committed_action_count", score.CommittedActionCount);
            AppendUInt32(builder, "score.recorded_loss_count", score.RecordedLossCount);

            IReadOnlyList<Phase8LossRecordV1> lossRecords = runtime.Runtime.LossRecords;
            AppendInt(builder, "loss_record_count", lossRecords.Count);
            for (int index = 0; index < lossRecords.Count; index++)
            {
                Phase8LossRecordV1 loss = lossRecords[index];
                string prefix = "loss[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                AppendString(builder, prefix + ".loss_id", loss.LossId);
                AppendString(builder, prefix + ".metric", loss.Metric);
                AppendDouble(builder, prefix + ".value", loss.Value);
                AppendDouble(builder, prefix + ".simulation_time_s", loss.SimulationTimeSeconds);
            }

            IReadOnlyList<Phase8TurnSummaryV1> summaries = runtime.TurnSummaries;
            AppendInt(builder, "turn_summary_count", summaries.Count);
            for (int index = 0; index < summaries.Count; index++)
            {
                Phase8TurnSummaryV1 summary = summaries[index];
                string prefix = "turn[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                AppendUInt64(builder, prefix + ".turn_id", summary.TurnId);
                AppendUInt64(builder, prefix + ".wall_milliseconds_requested", summary.WallMillisecondsRequested);
                AppendDouble(builder, prefix + ".simulation_time_start_s", summary.SimulationTimeStartSeconds);
                AppendDouble(builder, prefix + ".simulation_time_end_s", summary.SimulationTimeEndSeconds);
                AppendUInt32(builder, prefix + ".committed_action_count", summary.CommittedActionCount);
                AppendUInt32(builder, prefix + ".scripted_event_count", summary.ScriptedEventCount);
                AppendUInt32(builder, prefix + ".loss_count", summary.LossCount);
                AppendDouble(builder, prefix + ".normalized_power_before_fraction", summary.NormalizedPowerBeforeFraction);
                AppendDouble(builder, prefix + ".normalized_power_after_fraction", summary.NormalizedPowerAfterFraction);
                AppendDouble(builder, prefix + ".absolute_tilt_before_fraction", summary.AbsoluteTiltBeforeFraction);
                AppendDouble(builder, prefix + ".absolute_tilt_after_fraction", summary.AbsoluteTiltAfterFraction);
                AppendString(builder, prefix + ".outcome", summary.Outcome.ToString());
                AppendString(builder, prefix + ".cause", summary.Cause);
                AppendString(builder, prefix + ".effect", summary.Effect);
                AppendDouble(builder, prefix + ".score_total", summary.ScoreTotal);
            }

            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static void AppendCommand(
            StringBuilder builder,
            Phase8ReplayCommandV1 command,
            int index)
        {
            ArgumentNullException.ThrowIfNull(command);
            string prefix = "command[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            AppendString(builder, prefix + ".kind", CommandKindText(command.Kind));
            switch (command.Kind)
            {
                case Phase8ReplayCommandKindV1.AdvanceWall:
                    AppendUInt64(builder, prefix + ".wall_milliseconds", command.WallMilliseconds);
                    break;

                case Phase8ReplayCommandKindV1.SetPowerTarget:
                case Phase8ReplayCommandKindV1.SetTiltTarget:
                    AppendDouble(builder, prefix + ".target_fraction", command.TargetFraction);
                    break;

                case Phase8ReplayCommandKindV1.SetPlaybackMode:
                    AppendString(builder, prefix + ".playback_mode_id", command.PlaybackModeId);
                    break;

                case Phase8ReplayCommandKindV1.Pause:
                case Phase8ReplayCommandKindV1.Resume:
                    break;

                default:
                    throw new InvalidOperationException("The replay digest encountered an unsupported command kind.");
            }
        }

        private static string CommandKindText(Phase8ReplayCommandKindV1 kind)
        {
            return kind switch
            {
                Phase8ReplayCommandKindV1.AdvanceWall => "advance_wall",
                Phase8ReplayCommandKindV1.SetPowerTarget => "set_power_target",
                Phase8ReplayCommandKindV1.SetTiltTarget => "set_tilt_target",
                Phase8ReplayCommandKindV1.SetPlaybackMode => "set_playback_mode",
                Phase8ReplayCommandKindV1.Pause => "pause",
                Phase8ReplayCommandKindV1.Resume => "resume",
                _ => throw new InvalidOperationException("The replay digest encountered an unsupported command kind.")
            };
        }

        private static void AppendString(StringBuilder builder, string label, string value)
        {
            if (value == null)
            {
                throw new InvalidOperationException("The replay digest encountered a null string at " + label + ".");
            }

            builder.Append(label)
                .Append('=')
                .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value)
                .Append('\n');
        }

        private static void AppendDouble(StringBuilder builder, string label, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException("The replay digest encountered a nonfinite value at " + label + ".");
            }

            builder.Append(label)
                .Append('=')
                .Append(value.ToString("R", CultureInfo.InvariantCulture))
                .Append('\n');
        }

        private static void AppendUInt64(StringBuilder builder, string label, ulong value)
        {
            builder.Append(label)
                .Append('=')
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        private static void AppendUInt32(StringBuilder builder, string label, uint value)
        {
            builder.Append(label)
                .Append('=')
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        private static void AppendInt(StringBuilder builder, string label, int value)
        {
            builder.Append(label)
                .Append('=')
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        private static void AppendBool(StringBuilder builder, string label, bool value)
        {
            builder.Append(label)
                .Append('=')
                .Append(value ? "true" : "false")
                .Append('\n');
        }
    }
}
