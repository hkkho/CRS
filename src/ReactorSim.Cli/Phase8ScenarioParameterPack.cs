using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    internal sealed class Phase8ScenarioParameterPack
    {
        public const string DefaultRelativePath = "data/scenarios/p8-t02-scenario-difficulty-parameters-v1.json";
        public const string ApprovedStatus = "Approved/ApprovedParameterAuthority";

        private Phase8ScenarioParameterPack(
            string artifactPath,
            string manifestPath,
            string artifactSha256,
            Phase8TimeModelV1 timeModel,
            string defaultPlaybackModeId,
            IReadOnlyDictionary<string, Phase8PlaybackModeV1> playbackModes,
            IReadOnlyDictionary<string, Phase8DifficultyProfileV1> difficultyProfiles,
            IReadOnlyDictionary<string, Phase8ScenarioDefinitionV1> scenarios)
        {
            ArtifactPath = artifactPath;
            ManifestPath = manifestPath;
            ArtifactSha256 = artifactSha256;
            TimeModel = timeModel;
            DefaultPlaybackModeId = defaultPlaybackModeId;
            PlaybackModes = playbackModes;
            DifficultyProfiles = difficultyProfiles;
            Scenarios = scenarios;
        }

        public string ArtifactPath { get; }

        public string ManifestPath { get; }

        public string ArtifactSha256 { get; }

        public Phase8TimeModelV1 TimeModel { get; }

        public string DefaultPlaybackModeId { get; }

        public IReadOnlyDictionary<string, Phase8PlaybackModeV1> PlaybackModes { get; }

        public IReadOnlyDictionary<string, Phase8DifficultyProfileV1> DifficultyProfiles { get; }

        public IReadOnlyDictionary<string, Phase8ScenarioDefinitionV1> Scenarios { get; }

        public static Phase8ScenarioParameterPack LoadApproved(string artifactPath)
        {
            string fullArtifactPath = Path.GetFullPath(artifactPath ?? string.Empty);
            if (!File.Exists(fullArtifactPath))
            {
                throw new Phase8ScenarioPackFailure(
                    "artifact",
                    "The approved Phase 8 parameter artifact does not exist: " + fullArtifactPath);
            }

            string? directory = Path.GetDirectoryName(fullArtifactPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new Phase8ScenarioPackFailure(
                    "artifact",
                    "The approved Phase 8 parameter artifact has no containing directory.");
            }

            string manifestPath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(fullArtifactPath) + ".manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new Phase8ScenarioPackFailure(
                    "manifest",
                    "The approved Phase 8 parameter manifest does not exist: " + manifestPath);
            }

            try
            {
                using JsonDocument artifactDocument = JsonDocument.Parse(
                    File.ReadAllText(fullArtifactPath),
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow
                    });
                using JsonDocument manifestDocument = JsonDocument.Parse(
                    File.ReadAllText(manifestPath),
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow
                    });

                RejectDuplicateProperties(artifactDocument.RootElement, "artifact");
                RejectDuplicateProperties(manifestDocument.RootElement, "manifest");
                ValidateManifest(fullArtifactPath, manifestPath, manifestDocument.RootElement);

                JsonElement root = RequireObject(artifactDocument.RootElement, "artifact");
                RequireStringEquals(
                    root,
                    "format",
                    "reactorsim.p8-scenario-difficulty-parameters/v1",
                    "artifact");
                RequireStringEquals(root, "status", ApprovedStatus, "artifact");
                RequireStringEquals(root, "authority_class", "ProjectAuthoredSyntheticApprovedParameterAuthority", "artifact");
                RequireStringEquals(root, "runtime_use", "ApprovedPhase8ScenarioRuntimeOnly", "artifact");

                JsonElement sourceAuthority = RequireObject(
                    RequireProperty(root, "source_authority", "artifact"),
                    "artifact.source_authority");
                if (RequireBool(sourceAuthority, "external_source_artifacts_used", "artifact.source_authority") ||
                    RequireBool(sourceAuthority, "production_or_external_reference_claim", "artifact.source_authority"))
                {
                    throw new Phase8ScenarioPackFailure(
                        "artifact.source_authority",
                        "The approved gameplay parameter authority must remain synthetic and non-production.");
                }

                JsonElement timeModelElement = RequireObject(
                    RequireProperty(root, "time_model", "artifact"),
                    "artifact.time_model");
                uint wallControlTickMilliseconds = RequireUInt32(
                    timeModelElement,
                    "wall_control_tick_ms",
                    "artifact.time_model");
                double maximumPresentationAdvance = RequireDouble(
                    timeModelElement,
                    "maximum_presentation_advance_per_wall_tick_s",
                    "artifact.time_model");
                double defaultAccelerationFactor = RequireDouble(
                    timeModelElement,
                    "default_acceleration_factor_simulation_seconds_per_wall_second",
                    "artifact.time_model");
                double maximumAccelerationFactor = RequireDouble(
                    timeModelElement,
                    "maximum_acceleration_factor_simulation_seconds_per_wall_second",
                    "artifact.time_model");
                bool wallClockMustNotDrivePhysics = RequireBool(
                    timeModelElement,
                    "wall_clock_must_not_drive_physics",
                    "artifact.time_model");
                Phase8TimeModelV1 timeModel = RequireValid(
                    Phase8TimeModelV1.TryCreate(
                        wallControlTickMilliseconds,
                        maximumPresentationAdvance,
                        defaultAccelerationFactor,
                        maximumAccelerationFactor,
                        wallClockMustNotDrivePhysics),
                    "artifact.time_model");

                string defaultPlaybackModeId = RequireString(
                    timeModelElement,
                    "default_playback_mode_id",
                    "artifact.time_model");
                var playbackModes = new Dictionary<string, Phase8PlaybackModeV1>(StringComparer.Ordinal);
                foreach (JsonElement modeElement in RequireArray(
                             RequireProperty(timeModelElement, "modes", "artifact.time_model"),
                             "artifact.time_model.modes"))
                {
                    JsonElement mode = RequireObject(modeElement, "artifact.time_model.modes[]");
                    string modeId = RequireString(mode, "mode_id", "artifact.time_model.modes[]");
                    double factor = RequireDouble(
                        mode,
                        "acceleration_factor_simulation_seconds_per_wall_second",
                        "artifact.time_model.modes[]");
                    Phase8PlaybackModeV1 playbackMode = RequireValid(
                        Phase8PlaybackModeV1.TryCreate(modeId, factor, timeModel),
                        "artifact.time_model.modes[" + modeId + "]");
                    if (!playbackModes.TryAdd(modeId, playbackMode))
                    {
                        throw new Phase8ScenarioPackFailure(
                            "artifact.time_model.modes",
                            "Playback mode identifiers must be unique: " + modeId);
                    }
                }

                if (!playbackModes.ContainsKey(defaultPlaybackModeId))
                {
                    throw new Phase8ScenarioPackFailure(
                        "artifact.time_model.default_playback_mode_id",
                        "The default playback mode is not present in the approved mode list.");
                }

                var difficultyProfiles = new Dictionary<string, Phase8DifficultyProfileV1>(StringComparer.Ordinal);
                foreach (JsonElement profileElement in RequireArray(
                             RequireProperty(root, "difficulty_profiles", "artifact"),
                             "artifact.difficulty_profiles"))
                {
                    JsonElement profile = RequireObject(profileElement, "artifact.difficulty_profiles[]");
                    string difficultyId = RequireString(profile, "difficulty_id", "artifact.difficulty_profiles[]");
                    JsonElement envelope = RequireObject(
                        RequireProperty(profile, "operating_envelope", "artifact.difficulty_profiles[]"),
                        "artifact.difficulty_profiles[].operating_envelope");
                    double[] powerBounds = ReadBounds(
                        envelope,
                        "normalized_power_fraction",
                        "artifact.difficulty_profiles[].operating_envelope");
                    double[] tiltBounds = ReadBounds(
                        envelope,
                        "absolute_tilt_fraction",
                        "artifact.difficulty_profiles[].operating_envelope");
                    double[] marginBounds = ReadBounds(
                        envelope,
                        "control_margin_fraction",
                        "artifact.difficulty_profiles[].operating_envelope");
                    double[] deviceBounds = ReadBounds(
                        envelope,
                        "device_available_fraction",
                        "artifact.difficulty_profiles[].operating_envelope");
                    Phase8OperatingEnvelopeV1 operatingEnvelope = RequireValid(
                        Phase8OperatingEnvelopeV1.TryCreate(
                            powerBounds[0],
                            powerBounds[1],
                            tiltBounds[0],
                            tiltBounds[1],
                            marginBounds[0],
                            marginBounds[1],
                            deviceBounds[0],
                            deviceBounds[1]),
                        "artifact.difficulty_profiles[" + difficultyId + "].operating_envelope");
                    Phase8DifficultyProfileV1 difficultyProfile = RequireValid(
                        Phase8DifficultyProfileV1.TryCreate(
                            difficultyId,
                            RequireDouble(profile, "scenario_horizon_s", "artifact.difficulty_profiles[]"),
                            RequireDouble(profile, "decision_interval_s", "artifact.difficulty_profiles[]"),
                            RequireUInt32(profile, "maximum_pending_commands", "artifact.difficulty_profiles[]"),
                            RequireUInt32(profile, "maximum_refuel_requests", "artifact.difficulty_profiles[]"),
                            operatingEnvelope),
                        "artifact.difficulty_profiles[" + difficultyId + "]");

                    if (!difficultyProfiles.TryAdd(difficultyId, difficultyProfile))
                    {
                        throw new Phase8ScenarioPackFailure(
                            "artifact.difficulty_profiles",
                            "Difficulty identifiers must be unique: " + difficultyId);
                    }

                    ValidateOptionalTimingEstimates(profile, difficultyProfile, defaultAccelerationFactor, difficultyId);
                }

                var scenarios = new Dictionary<string, Phase8ScenarioDefinitionV1>(StringComparer.Ordinal);
                var seeds = new HashSet<ulong>();
                foreach (JsonElement scenarioElement in RequireArray(
                             RequireProperty(root, "scenarios", "artifact"),
                             "artifact.scenarios"))
                {
                    JsonElement scenario = RequireObject(scenarioElement, "artifact.scenarios[]");
                    string scenarioId = RequireString(scenario, "scenario_id", "artifact.scenarios[]");
                    string difficultyId = RequireString(scenario, "difficulty_id", "artifact.scenarios[]");
                    if (!difficultyProfiles.ContainsKey(difficultyId))
                    {
                        throw new Phase8ScenarioPackFailure(
                            "artifact.scenarios[" + scenarioId + "].difficulty_id",
                            "The scenario references an unknown difficulty profile.");
                    }

                    JsonElement initialState = RequireObject(
                        RequireProperty(scenario, "initial_state", "artifact.scenarios[]"),
                        "artifact.scenarios[].initial_state");
                    Phase8ScenarioInitialStateV1 state = RequireValid(
                        Phase8ScenarioInitialStateV1.TryCreate(
                            RequireDouble(initialState, "normalized_power_fraction", "artifact.scenarios[].initial_state"),
                            RequireDouble(initialState, "absolute_tilt_fraction", "artifact.scenarios[].initial_state"),
                            RequireDouble(initialState, "control_margin_fraction", "artifact.scenarios[].initial_state"),
                            RequireDouble(initialState, "device_available_fraction", "artifact.scenarios[].initial_state"),
                            RequireUInt32(initialState, "refuel_requests_remaining", "artifact.scenarios[].initial_state")),
                        "artifact.scenarios[" + scenarioId + "].initial_state");

                    var events = new List<Phase8ScenarioEventV1>();
                    foreach (JsonElement eventElement in RequireArray(
                                 RequireProperty(scenario, "scripted_events", "artifact.scenarios[]"),
                                 "artifact.scenarios[].scripted_events"))
                    {
                        JsonElement scriptedEvent = RequireObject(eventElement, "artifact.scenarios[].scripted_events[]");
                        string eventName = RequireString(
                            scriptedEvent,
                            "event",
                            "artifact.scenarios[].scripted_events[]");
                        Phase8ScenarioEventKindV1 eventKind = ParseEventKind(eventName);
                        double? powerTarget = OptionalDouble(
                            scriptedEvent,
                            "target_normalized_power_fraction",
                            "artifact.scenarios[].scripted_events[]");
                        double? tiltTarget = OptionalDouble(
                            scriptedEvent,
                            "target_absolute_tilt_fraction",
                            "artifact.scenarios[].scripted_events[]");
                        uint? channelId = OptionalUInt32(
                            scriptedEvent,
                            "channel_id",
                            "artifact.scenarios[].scripted_events[]");
                        uint? bundlePosition = OptionalUInt32(
                            scriptedEvent,
                            "bundle_position",
                            "artifact.scenarios[].scripted_events[]");
                        events.Add(
                            RequireValid(
                                Phase8ScenarioEventV1.TryCreate(
                                    RequireDouble(scriptedEvent, "at_s", "artifact.scenarios[].scripted_events[]"),
                                    eventKind,
                                    powerTarget,
                                    tiltTarget,
                                    channelId,
                                    bundlePosition),
                                "artifact.scenarios[" + scenarioId + "].scripted_events"));
                    }

                    ulong seed = RequireUInt64(scenario, "seed", "artifact.scenarios[]");
                    if (!seeds.Add(seed))
                    {
                        throw new Phase8ScenarioPackFailure(
                            "artifact.scenarios[" + scenarioId + "].seed",
                            "Scenario seeds must be unique in the approved parameter pack.");
                    }

                    Phase8ScenarioDefinitionV1 definition = RequireValid(
                        Phase8ScenarioDefinitionV1.TryCreate(
                            scenarioId,
                            difficultyId,
                            seed,
                            state,
                            events),
                        "artifact.scenarios[" + scenarioId + "]");
                    if (!scenarios.TryAdd(scenarioId, definition))
                    {
                        throw new Phase8ScenarioPackFailure(
                            "artifact.scenarios",
                            "Scenario identifiers must be unique: " + scenarioId);
                    }
                }

                if (difficultyProfiles.Count != RequireUInt32(root, "difficulty_profile_count", "artifact") ||
                    scenarios.Count != RequireUInt32(root, "scenario_count", "artifact") ||
                    playbackModes.Count != RequireUInt32(timeModelElement, "mode_count", "artifact.time_model"))
                {
                    throw new Phase8ScenarioPackFailure(
                        "artifact",
                        "The approved parameter counts do not match their parsed collections.");
                }

                ValidateManifestSemantics(
                    manifestDocument.RootElement,
                    difficultyProfiles.Count,
                    scenarios.Count,
                    playbackModes.Count,
                    defaultAccelerationFactor,
                    maximumAccelerationFactor,
                    wallControlTickMilliseconds);

                return new Phase8ScenarioParameterPack(
                    fullArtifactPath,
                    manifestPath,
                    ComputeSha256(fullArtifactPath),
                    timeModel,
                    defaultPlaybackModeId,
                    new ReadOnlyDictionary<string, Phase8PlaybackModeV1>(playbackModes),
                    new ReadOnlyDictionary<string, Phase8DifficultyProfileV1>(difficultyProfiles),
                    new ReadOnlyDictionary<string, Phase8ScenarioDefinitionV1>(scenarios));
            }
            catch (Phase8ScenarioPackFailure)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                throw new Phase8ScenarioPackFailure(
                    "document",
                    "The approved Phase 8 parameter document could not be loaded: " + exception.Message);
            }
        }

        public static string FindDefaultPath()
        {
            var starts = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };
            foreach (string start in starts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DirectoryInfo? directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, DefaultRelativePath);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }
            }

            throw new Phase8ScenarioPackFailure(
                "artifact",
                "The approved Phase 8 parameter artifact could not be found from the current application paths.");
        }

        private static void ValidateManifest(
            string artifactPath,
            string manifestPath,
            JsonElement manifestRoot)
        {
            JsonElement root = RequireObject(manifestRoot, "manifest");
            RequireStringEquals(
                root,
                "format",
                "reactorsim.p8-scenario-difficulty-parameters-manifest/v1",
                "manifest");
            RequireStringEquals(root, "status", ApprovedStatus, "manifest");
            JsonElement artifact = RequireObject(
                RequireProperty(root, "artifact", "manifest"),
                "manifest.artifact");
            string manifestArtifactPath = RequireString(artifact, "path", "manifest.artifact");
            string repositoryRoot = FindRepositoryRoot(Path.GetDirectoryName(artifactPath)!);
            string resolvedManifestArtifactPath = Path.GetFullPath(
                Path.Combine(repositoryRoot, manifestArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!PathsEqual(resolvedManifestArtifactPath, artifactPath))
            {
                throw new Phase8ScenarioPackFailure(
                    "manifest.artifact.path",
                    "The manifest artifact path does not bind the loaded approved artifact.");
            }

            ulong expectedBytes = RequireUInt64(artifact, "byte_length", "manifest.artifact");
            string expectedHash = RequireString(artifact, "sha256", "manifest.artifact");
            string actualHash = ComputeSha256(artifactPath);
            long actualBytes = new FileInfo(artifactPath).Length;
            if (expectedBytes != (ulong)actualBytes || !string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                throw new Phase8ScenarioPackFailure(
                    "manifest.artifact",
                    "The approved parameter artifact hash or byte length does not match its manifest.");
            }

            _ = manifestPath;
        }

        private static void ValidateManifestSemantics(
            JsonElement manifestRoot,
            int difficultyProfileCount,
            int scenarioCount,
            int playbackModeCount,
            double defaultAccelerationFactor,
            double maximumAccelerationFactor,
            uint wallControlTickMilliseconds)
        {
            JsonElement root = RequireObject(manifestRoot, "manifest");
            RequireStringEquals(root, "task_id", "P8-T02", "manifest");
            RequireStringEquals(root, "source_draft_task_id", "P8-T02-DRAFT", "manifest");
            RequireStringEquals(root, "artifact_id", "p8-t02-scenario-difficulty-parameters-v1", "manifest");
            RequireStringEquals(root, "schema_version", "v1", "manifest");

            if (RequireUInt32(root, "difficulty_profile_count", "manifest") != (uint)difficultyProfileCount ||
                RequireUInt32(root, "scenario_count", "manifest") != (uint)scenarioCount ||
                RequireUInt32(root, "playback_mode_count", "manifest") != (uint)playbackModeCount ||
                !NearlyEqual(
                    RequireDouble(root, "default_acceleration_factor", "manifest"),
                    defaultAccelerationFactor) ||
                !NearlyEqual(
                    RequireDouble(root, "maximum_acceleration_factor", "manifest"),
                    maximumAccelerationFactor) ||
                RequireUInt32(root, "wall_control_tick_ms", "manifest") != wallControlTickMilliseconds)
            {
                throw new Phase8ScenarioPackFailure(
                    "manifest",
                    "The approved manifest timing or collection metadata does not match the parsed artifact.");
            }
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= 1e-12;
        }

        private static string FindRepositoryRoot(string startPath)
        {
            DirectoryInfo? directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "data", "scenarios")) ||
                    File.Exists(Path.Combine(directory.FullName, "ReactorSim.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return startPath;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            byte[] digest = SHA256.HashData(File.ReadAllBytes(path));
            var builder = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void RejectDuplicateProperties(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new Phase8ScenarioPackFailure(path, "Duplicate JSON property: " + property.Name);
                    }

                    RejectDuplicateProperties(property.Value, path + "." + property.Name);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement child in element.EnumerateArray())
                {
                    RejectDuplicateProperties(child, path + "[" + index + "]");
                    index++;
                }
            }
        }

        private static JsonElement RequireProperty(JsonElement element, string name, string path)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A required JSON property is missing.");
            }

            return value;
        }

        private static JsonElement RequireObject(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new Phase8ScenarioPackFailure(path, "A JSON object is required.");
            }

            return element;
        }

        private static JsonElement[] RequireArray(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new Phase8ScenarioPackFailure(path, "A JSON array is required.");
            }

            return element.EnumerateArray().ToArray();
        }

        private static string RequireString(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.String || value.GetString() == null)
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A JSON string is required.");
            }

            return value.GetString()!;
        }

        private static void RequireStringEquals(JsonElement element, string name, string expected, string path)
        {
            string actual = RequireString(element, name, path);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new Phase8ScenarioPackFailure(
                    path + "." + name,
                    "Expected '" + expected + "' but found '" + actual + "'.");
            }
        }

        private static bool RequireBool(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A JSON boolean is required.");
            }

            return value.GetBoolean();
        }

        private static double RequireDouble(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result) ||
                double.IsNaN(result) || double.IsInfinity(result))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A finite JSON number is required.");
            }

            return result;
        }

        private static double? OptionalDouble(JsonElement element, string name, string path)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result) ||
                double.IsNaN(result) || double.IsInfinity(result))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "An optional finite JSON number is required.");
            }

            return result;
        }

        private static uint RequireUInt32(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint result))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A nonnegative JSON UInt32 is required.");
            }

            return result;
        }

        private static uint? OptionalUInt32(JsonElement element, string name, string path)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint result))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "An optional nonnegative JSON UInt32 is required.");
            }

            return result;
        }

        private static ulong RequireUInt64(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out ulong result))
            {
                throw new Phase8ScenarioPackFailure(path + "." + name, "A nonnegative JSON UInt64 is required.");
            }

            return result;
        }

        private static double[] ReadBounds(JsonElement element, string name, string path)
        {
            JsonElement bounds = RequireObject(RequireProperty(element, name, path), path + "." + name);
            return new[]
            {
                RequireDouble(bounds, "minimum", path + "." + name),
                RequireDouble(bounds, "maximum", path + "." + name)
            };
        }

        private static void ValidateOptionalTimingEstimates(
            JsonElement profile,
            Phase8DifficultyProfileV1 difficultyProfile,
            double defaultAccelerationFactor,
            string difficultyId)
        {
            double expectedWallDuration = difficultyProfile.ScenarioHorizonSeconds / defaultAccelerationFactor;
            double expectedDecisionInterval = difficultyProfile.DecisionIntervalSeconds / defaultAccelerationFactor;
            double? actualWallDuration = OptionalDouble(
                profile,
                "estimated_wall_duration_s_at_default_playback",
                "artifact.difficulty_profiles[" + difficultyId + "]");
            double? actualDecisionInterval = OptionalDouble(
                profile,
                "estimated_decision_interval_wall_s_at_default_playback",
                "artifact.difficulty_profiles[" + difficultyId + "]");
            if (actualWallDuration.HasValue && Math.Abs(actualWallDuration.Value - expectedWallDuration) > 1e-9)
            {
                throw new Phase8ScenarioPackFailure(
                    "artifact.difficulty_profiles[" + difficultyId + "]",
                    "The wall-duration estimate does not match the approved acceleration factor.");
            }

            if (actualDecisionInterval.HasValue && Math.Abs(actualDecisionInterval.Value - expectedDecisionInterval) > 1e-9)
            {
                throw new Phase8ScenarioPackFailure(
                    "artifact.difficulty_profiles[" + difficultyId + "]",
                    "The decision-interval estimate does not match the approved acceleration factor.");
            }
        }

        private static Phase8ScenarioEventKindV1 ParseEventKind(string value)
        {
            return value switch
            {
                "inspect" => Phase8ScenarioEventKindV1.Inspect,
                "set_power_target" => Phase8ScenarioEventKindV1.SetPowerTarget,
                "set_tilt_target" => Phase8ScenarioEventKindV1.SetTiltTarget,
                "refuel_request" => Phase8ScenarioEventKindV1.RefuelRequest,
                _ => throw new Phase8ScenarioPackFailure(
                    "artifact.scenarios.scripted_events.event",
                    "Unsupported scripted event: " + value)
            };
        }

        private static T RequireValid<T>(ContractValidationResult<T> result, string path)
        {
            if (!result.IsValid)
            {
                throw new Phase8ScenarioPackFailure(
                    path + "." + result.FirstDiagnostic.Path,
                    result.FirstDiagnostic.Message);
            }

            return result.Value;
        }

        private sealed class Phase8ScenarioPackFailure : InvalidOperationException
        {
            public Phase8ScenarioPackFailure(string path, string message)
                : base(message)
            {
                Path = path;
            }

            public string Path { get; }
        }

        public static string FormatFailure(Exception exception)
        {
            if (exception is Phase8ScenarioPackFailure failure)
            {
                return failure.Path + ": " + failure.Message;
            }

            return exception.Message;
        }
    }
}
