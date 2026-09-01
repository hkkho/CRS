using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    internal sealed class Phase8ScoringParameterPack
    {
        public const string DefaultRelativePath =
            "data/scenarios/p8-t03-scoring-parameters-v1.json";
        public const string ApprovedStatus = "Approved/ApprovedParameterAuthority";
        private static readonly string[] ApprovedCauseEffectFields =
        {
            "turn_id",
            "simulation_time_start_s",
            "simulation_time_end_s",
            "committed_action_count",
            "scripted_event_count",
            "loss_count",
            "normalized_power_before_fraction",
            "normalized_power_after_fraction",
            "absolute_tilt_before_fraction",
            "absolute_tilt_after_fraction",
            "outcome",
            "cause",
            "effect",
            "score_total"
        };

        private Phase8ScoringParameterPack(
            string artifactPath,
            string manifestPath,
            string artifactSha256,
            Phase8ScoringParametersV1 parameters)
        {
            ArtifactPath = artifactPath;
            ManifestPath = manifestPath;
            ArtifactSha256 = artifactSha256;
            Parameters = parameters;
        }

        public string ArtifactPath { get; }

        public string ManifestPath { get; }

        public string ArtifactSha256 { get; }

        public Phase8ScoringParametersV1 Parameters { get; }

        public static Phase8ScoringParameterPack LoadApproved(string artifactPath)
        {
            string fullArtifactPath = Path.GetFullPath(artifactPath ?? string.Empty);
            if (!File.Exists(fullArtifactPath))
            {
                throw new Phase8ScoringPackFailure(
                    "artifact",
                    "The approved P8-T03 scoring parameter artifact does not exist: " + fullArtifactPath);
            }

            string directory = Path.GetDirectoryName(fullArtifactPath) ?? string.Empty;
            string manifestPath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(fullArtifactPath) + ".manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new Phase8ScoringPackFailure(
                    "manifest",
                    "The approved P8-T03 scoring manifest does not exist: " + manifestPath);
            }

            try
            {
                using JsonDocument artifactDocument = Parse(fullArtifactPath);
                using JsonDocument manifestDocument = Parse(manifestPath);
                RejectDuplicateProperties(artifactDocument.RootElement, "artifact");
                RejectDuplicateProperties(manifestDocument.RootElement, "manifest");
                ValidateManifest(fullArtifactPath, manifestPath, manifestDocument.RootElement);

                JsonElement root = RequireObject(artifactDocument.RootElement, "artifact");
                RequireStringEquals(root, "format", "reactorsim.p8-scoring-parameters/v1", "artifact");
                RequireStringEquals(root, "task_id", "P8-T03", "artifact");
                RequireStringEquals(root, "artifact_id", "p8-t03-scoring-parameters-v1", "artifact");
                RequireStringEquals(root, "status", ApprovedStatus, "artifact");
                RequireStringEquals(
                    root,
                    "authority_class",
                    "ProjectAuthoredSyntheticApprovedParameterAuthority",
                    "artifact");
                RequireStringEquals(
                    root,
                    "runtime_use",
                    "ApprovedPhase8ScoringSummaryRuntimeOnly",
                    "artifact");

                JsonElement sourceAuthority = RequireObject(
                    RequireProperty(root, "source_authority", "artifact"),
                    "artifact.source_authority");
                if (RequireBool(sourceAuthority, "external_source_artifacts_used", "artifact.source_authority") ||
                    RequireBool(sourceAuthority, "production_or_external_reference_claim", "artifact.source_authority"))
                {
                    throw new Phase8ScoringPackFailure(
                        "artifact.source_authority",
                        "The approved P8-T03 scoring authority must remain synthetic and non-production.");
                }

                JsonElement scoreModel = RequireObject(
                    RequireProperty(root, "score_model", "artifact"),
                    "artifact.score_model");
                Phase8ScoringParametersV1 parameters = RequireValid(
                    Phase8ScoringParametersV1.TryCreate(
                        RequireDouble(scoreModel, "score_minimum", "artifact.score_model"),
                        RequireDouble(scoreModel, "score_maximum", "artifact.score_model"),
                        RequireDouble(scoreModel, "survival_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "energy_quality_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "stability_quality_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "fuelling_efficiency_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "control_action_penalty_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "record_loss_penalty_points", "artifact.score_model"),
                        RequireDouble(scoreModel, "nominal_power_fraction", "artifact.score_model"),
                        RequireUInt32(scoreModel, "rounding_decimal_places", "artifact.score_model"),
                        RequireUInt32(
                            RequireObject(
                                RequireProperty(root, "summary_model", "artifact"),
                                "artifact.summary_model"),
                            "max_summary_events_per_turn",
                            "artifact.summary_model")),
                    "artifact.score_model");

                RequireStringEquals(
                    scoreModel,
                    "integration_method",
                    "ExplicitStateSegmentIntegralPerControlTick",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "energy_quality_formula",
                    "clamp(sum(state_segment_normalized_power_fraction * segment_duration_s) / scenario_horizon_s / nominal_power_fraction, 0, 1)",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "stability_quality_formula",
                    "clamp(sum(state_segment_stability_quality * segment_duration_s) / scenario_horizon_s, 0, 1); state_segment_stability_quality = clamp(1 - abs(state_segment_normalized_power_fraction - nominal_power_fraction) - state_segment_absolute_tilt_fraction - abs(state_segment_control_margin_fraction), 0, 1)",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "fuelling_efficiency_formula",
                    "clamp(refuel_requests_remaining / initial_refuel_requests_remaining, 0, 1); if the initial budget is zero, use 1",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "survival_formula",
                    "1 when the scenario reaches its approved horizon; otherwise 0",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "control_penalty_formula",
                    "committed_action_count * control_action_penalty_points",
                    "artifact.score_model");
                RequireStringEquals(
                    scoreModel,
                    "loss_penalty_formula",
                    "record_loss_count * record_loss_penalty_points",
                    "artifact.score_model");

                JsonElement summaryModel = RequireObject(
                    RequireProperty(root, "summary_model", "artifact"),
                    "artifact.summary_model");
                RequireStringEquals(
                    summaryModel,
                    "summary_schema_version",
                    "p8-turn-summary/v1",
                    "artifact.summary_model");
                RequireStringEquals(
                    summaryModel,
                    "cause_policy",
                    "Prefer loss identifiers, then scripted event kinds, then committed player action kinds, otherwise report elapsed play.",
                    "artifact.summary_model");
                RequireStringEquals(
                    summaryModel,
                    "effect_policy",
                    "Describe only approved proxy deltas, score movement, or scenario outcome; never claim a physical plant response.",
                    "artifact.summary_model");
                RequireStringArrayEquals(
                    summaryModel,
                    "cause_effect_fields",
                    ApprovedCauseEffectFields,
                    "artifact.summary_model");

                return new Phase8ScoringParameterPack(
                    fullArtifactPath,
                    manifestPath,
                    ComputeSha256(fullArtifactPath),
                    parameters);
            }
            catch (Phase8ScoringPackFailure)
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
                throw new Phase8ScoringPackFailure(
                    "document",
                    "The approved P8-T03 scoring document could not be loaded: " + exception.Message);
            }
        }

        public static string FindDefaultPath()
        {
            string[] starts =
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

            throw new Phase8ScoringPackFailure(
                "artifact",
                "The approved P8-T03 scoring parameter artifact could not be found from the application paths.");
        }

        private static JsonDocument Parse(string path)
        {
            return JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
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
                "reactorsim.p8-scoring-parameters-manifest/v1",
                "manifest");
            RequireStringEquals(root, "task_id", "P8-T03", "manifest");
            RequireStringEquals(root, "artifact_id", "p8-t03-scoring-parameters-v1", "manifest");
            RequireStringEquals(root, "schema_version", "v1", "manifest");
            RequireStringEquals(root, "status", ApprovedStatus, "manifest");
            JsonElement artifact = RequireObject(
                RequireProperty(root, "artifact", "manifest"),
                "manifest.artifact");
            string manifestArtifactPath = RequireString(artifact, "path", "manifest.artifact");
            string repositoryRoot = FindRepositoryRoot(Path.GetDirectoryName(artifactPath)!);
            string resolvedArtifactPath = Path.GetFullPath(
                Path.Combine(repositoryRoot, manifestArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!PathsEqual(resolvedArtifactPath, artifactPath))
            {
                throw new Phase8ScoringPackFailure(
                    "manifest.artifact.path",
                    "The P8-T03 manifest artifact path does not bind the loaded artifact.");
            }

            ulong expectedBytes = RequireUInt64(artifact, "byte_length", "manifest.artifact");
            string expectedHash = RequireString(artifact, "sha256", "manifest.artifact");
            string actualHash = ComputeSha256(artifactPath);
            long actualBytes = new FileInfo(artifactPath).Length;
            if (expectedBytes != (ulong)actualBytes ||
                !string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                throw new Phase8ScoringPackFailure(
                    "manifest.artifact",
                    "The approved P8-T03 scoring artifact hash or byte length does not match its manifest.");
            }

            _ = manifestPath;
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
                        throw new Phase8ScoringPackFailure(path, "Duplicate JSON property: " + property.Name);
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
                throw new Phase8ScoringPackFailure(path + "." + name, "A required JSON property is missing.");
            }

            return value;
        }

        private static JsonElement RequireObject(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new Phase8ScoringPackFailure(path, "A JSON object is required.");
            }

            return element;
        }

        private static string RequireString(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.String || value.GetString() == null)
            {
                throw new Phase8ScoringPackFailure(path + "." + name, "A JSON string is required.");
            }

            return value.GetString()!;
        }

        private static void RequireStringArrayEquals(
            JsonElement element,
            string name,
            string[] expected,
            string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new Phase8ScoringPackFailure(
                    path + "." + name,
                    "An exact JSON string array is required.");
            }

            var actual = new List<string>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || item.GetString() == null)
                {
                    throw new Phase8ScoringPackFailure(
                        path + "." + name,
                        "An exact JSON string array is required.");
                }

                actual.Add(item.GetString()!);
            }

            if (actual.Count != expected.Length ||
                actual.Where((item, index) => !string.Equals(item, expected[index], StringComparison.Ordinal)).Any())
            {
                throw new Phase8ScoringPackFailure(
                    path + "." + name,
                    "Expected the exact approved JSON string array.");
            }
        }

        private static void RequireStringEquals(JsonElement element, string name, string expected, string path)
        {
            string actual = RequireString(element, name, path);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new Phase8ScoringPackFailure(
                    path + "." + name,
                    "Expected '" + expected + "' but found '" + actual + "'.");
            }
        }

        private static bool RequireBool(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                throw new Phase8ScoringPackFailure(path + "." + name, "A JSON boolean is required.");
            }

            return value.GetBoolean();
        }

        private static double RequireDouble(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result) ||
                double.IsNaN(result) || double.IsInfinity(result))
            {
                throw new Phase8ScoringPackFailure(path + "." + name, "A finite JSON number is required.");
            }

            return result;
        }

        private static uint RequireUInt32(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint result))
            {
                throw new Phase8ScoringPackFailure(path + "." + name, "A nonnegative JSON UInt32 is required.");
            }

            return result;
        }

        private static ulong RequireUInt64(JsonElement element, string name, string path)
        {
            JsonElement value = RequireProperty(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out ulong result))
            {
                throw new Phase8ScoringPackFailure(path + "." + name, "A nonnegative JSON UInt64 is required.");
            }

            return result;
        }

        private static T RequireValid<T>(ContractValidationResult<T> result, string path)
        {
            if (!result.IsValid)
            {
                throw new Phase8ScoringPackFailure(
                    path + "." + result.FirstDiagnostic.Path,
                    result.FirstDiagnostic.Message);
            }

            return result.Value;
        }

        private sealed class Phase8ScoringPackFailure : InvalidOperationException
        {
            public Phase8ScoringPackFailure(string path, string message)
                : base(message)
            {
                Path = path;
            }

            public string Path { get; }
        }
    }
}
