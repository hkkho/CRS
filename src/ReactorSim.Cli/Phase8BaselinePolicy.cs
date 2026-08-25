using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    internal enum Phase8BaselinePolicyActionKindV1 : byte
    {
        SetPowerTarget = 0,
        SetTiltTarget = 1
    }

    internal sealed class Phase8BaselinePolicyActionV1
    {
        public Phase8BaselinePolicyActionV1(
            ulong atWallMilliseconds,
            Phase8BaselinePolicyActionKindV1 kind,
            double targetFraction)
        {
            AtWallMilliseconds = atWallMilliseconds;
            Kind = kind;
            TargetFraction = targetFraction;
        }

        public ulong AtWallMilliseconds { get; }

        public Phase8BaselinePolicyActionKindV1 Kind { get; }

        public double TargetFraction { get; }
    }

    internal sealed class Phase8BaselinePolicyExpectedV1
    {
        public Phase8BaselinePolicyExpectedV1(
            string outcome,
            double simulationTimeSeconds,
            double wallElapsedSeconds,
            double scoreTotal,
            uint lossCount,
            int turnSummaryCount,
            string replayDigest)
        {
            Outcome = outcome;
            SimulationTimeSeconds = simulationTimeSeconds;
            WallElapsedSeconds = wallElapsedSeconds;
            ScoreTotal = scoreTotal;
            LossCount = lossCount;
            TurnSummaryCount = turnSummaryCount;
            ReplayDigest = replayDigest;
        }

        public string Outcome { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public double ScoreTotal { get; }

        public uint LossCount { get; }

        public int TurnSummaryCount { get; }

        public string ReplayDigest { get; }
    }

    internal sealed class Phase8BaselinePolicyV1
    {
        public Phase8BaselinePolicyV1(
            string policyId,
            string scenarioId,
            string difficultyId,
            string initialPlaybackModeId,
            ulong horizonWallMilliseconds,
            IReadOnlyList<Phase8BaselinePolicyActionV1> actions,
            Phase8BaselinePolicyExpectedV1 expected)
        {
            PolicyId = policyId;
            ScenarioId = scenarioId;
            DifficultyId = difficultyId;
            InitialPlaybackModeId = initialPlaybackModeId;
            HorizonWallMilliseconds = horizonWallMilliseconds;
            Actions = new ReadOnlyCollection<Phase8BaselinePolicyActionV1>(
                new List<Phase8BaselinePolicyActionV1>(actions));
            Expected = expected;
        }

        public string PolicyId { get; }

        public string ScenarioId { get; }

        public string DifficultyId { get; }

        public string InitialPlaybackModeId { get; }

        public ulong HorizonWallMilliseconds { get; }

        public IReadOnlyList<Phase8BaselinePolicyActionV1> Actions { get; }

        public Phase8BaselinePolicyExpectedV1 Expected { get; }
    }

    internal sealed class Phase8BaselinePolicyPack
    {
        private static readonly string[] ArtifactProperties =
        {
            "format",
            "task_id",
            "artifact_id",
            "status",
            "authority_class",
            "runtime_use",
            "scope",
            "scenario_parameter_sha256",
            "scoring_parameter_sha256",
            "policy_count",
            "maximum_actions_per_policy",
            "policies",
            "source_authority"
        };

        private static readonly string[] PolicyProperties =
        {
            "policy_id",
            "scenario_id",
            "difficulty_id",
            "initial_playback_mode_id",
            "horizon_wall_ms",
            "actions",
            "expected"
        };

        private static readonly string[] ActionProperties =
        {
            "at_wall_ms",
            "action",
            "target_fraction"
        };

        private static readonly string[] ExpectedProperties =
        {
            "outcome",
            "simulation_time_s",
            "wall_elapsed_s",
            "score_total",
            "loss_count",
            "turn_summary_count",
            "replay_digest"
        };

        private static readonly string[] SourceAuthorityProperties =
        {
            "kind",
            "literature_applicability",
            "coverage_reason",
            "external_source_artifacts_used",
            "production_or_external_reference_claim"
        };

        private static readonly string[] ManifestProperties =
        {
            "format",
            "task_id",
            "artifact_id",
            "status",
            "owner_decision",
            "artifact",
            "scenario_parameter_sha256",
            "scoring_parameter_sha256",
            "policy_count",
            "maximum_actions_per_policy"
        };

        private static readonly string[] ManifestArtifactProperties =
        {
            "path",
            "byte_length",
            "sha256"
        };

        public const string DefaultRelativePath = "data/scenarios/p8-t05-scripted-policy-parameters-v1.json";
        public const string ApprovedStatus = "Approved/ApprovedParameterAuthority";
        public const string ExpectedFormat = "reactorsim.p8-scripted-policy-parameters/v1";
        public const string ExpectedManifestFormat = "reactorsim.p8-scripted-policy-parameters-manifest/v1";
        public const string ApprovedPolicyParameterSha256 = "d0e6dec7199751ca0f5d5418892fe0210831e46153ff445ec13e4e84ddf49d39";
        public const string ApprovedScenarioParameterSha256 = "80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2";
        public const string ApprovedScoringParameterSha256 = "4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c";
        private const double ApprovedScoreMinimum = 0.0;
        private const double ApprovedScoreMaximum = 1000.0;
        private const double ApprovedMaximumSimulationAccelerationFactor = 10.0;
        public const int MaximumPolicyCount = 32;
        public const int MaximumActionCount = 16;
        public const ulong MaximumHorizonWallMilliseconds = 3600000;

        private Phase8BaselinePolicyPack(
            string artifactPath,
            string manifestPath,
            string artifactSha256,
            string scenarioParameterSha256,
            string scoringParameterSha256,
            uint maximumActionsPerPolicy,
            IReadOnlyDictionary<string, Phase8BaselinePolicyV1> policies)
        {
            ArtifactPath = artifactPath;
            ManifestPath = manifestPath;
            ArtifactSha256 = artifactSha256;
            ScenarioParameterSha256 = scenarioParameterSha256;
            ScoringParameterSha256 = scoringParameterSha256;
            MaximumActionsPerPolicy = maximumActionsPerPolicy;
            Policies = policies;
        }

        public string ArtifactPath { get; }

        public string ManifestPath { get; }

        public string ArtifactSha256 { get; }

        public string ScenarioParameterSha256 { get; }

        public string ScoringParameterSha256 { get; }

        public uint MaximumActionsPerPolicy { get; }

        public IReadOnlyDictionary<string, Phase8BaselinePolicyV1> Policies { get; }

        public static Phase8BaselinePolicyPack LoadApproved(string artifactPath)
        {
            string fullArtifactPath = Path.GetFullPath(artifactPath ?? string.Empty);
            if (!File.Exists(fullArtifactPath))
            {
                throw new Phase8BaselinePolicyFailure(
                    "artifact",
                    "The approved P8-T05 policy artifact does not exist: " + fullArtifactPath);
            }

            string? directory = Path.GetDirectoryName(fullArtifactPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new Phase8BaselinePolicyFailure(
                    "artifact",
                    "The approved P8-T05 policy artifact has no containing directory.");
            }

            string manifestPath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(fullArtifactPath) + ".manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new Phase8BaselinePolicyFailure(
                    "manifest",
                    "The approved P8-T05 policy manifest does not exist: " + manifestPath);
            }

            byte[] artifactBytes;
            try
            {
                artifactBytes = File.ReadAllBytes(fullArtifactPath);
            }
            catch (IOException exception)
            {
                throw new Phase8BaselinePolicyFailure(
                    "artifact",
                    "The P8-T05 policy artifact could not be read: " + exception.Message,
                    exception);
            }

            string artifactSha256 = ComputeSha256(artifactBytes);
            string artifactJson = Encoding.UTF8.GetString(artifactBytes);
            string manifestJson;
            try
            {
                manifestJson = File.ReadAllText(manifestPath);
            }
            catch (IOException exception)
            {
                throw new Phase8BaselinePolicyFailure(
                    "manifest",
                    "The P8-T05 policy manifest could not be read: " + exception.Message,
                    exception);
            }

            ManifestValues manifest = ParseManifest(manifestJson, manifestPath);
            if (!string.Equals(manifest.Format, ExpectedManifestFormat, StringComparison.Ordinal) ||
                !string.Equals(manifest.TaskId, "P8-T05", StringComparison.Ordinal) ||
                !string.Equals(manifest.ArtifactId, "p8-t05-scripted-policy-parameters-v1", StringComparison.Ordinal) ||
                !string.Equals(manifest.Status, ApprovedStatus, StringComparison.Ordinal) ||
                !string.Equals(manifest.OwnerDecision, "APPROVED", StringComparison.Ordinal) ||
                manifest.ArtifactByteLength != artifactBytes.LongLength ||
                !string.Equals(manifest.ArtifactPath, DefaultRelativePath, StringComparison.Ordinal) ||
                !string.Equals(manifest.ArtifactSha256, artifactSha256, StringComparison.Ordinal) ||
                !string.Equals(artifactSha256, ApprovedPolicyParameterSha256, StringComparison.Ordinal))
            {
                throw new Phase8BaselinePolicyFailure(
                    "manifest",
                    "The P8-T05 policy manifest does not bind the exact approved artifact, status, or task identity.");
            }

            PolicyValues values = ParseArtifact(artifactJson, fullArtifactPath, artifactSha256);
            if (values.Policies.Count != manifest.PolicyCount ||
                values.MaximumActionsPerPolicy != manifest.MaximumActionsPerPolicy ||
                !string.Equals(values.ArtifactSha256, artifactSha256, StringComparison.Ordinal) ||
                !string.Equals(values.ScenarioParameterSha256, ApprovedScenarioParameterSha256, StringComparison.Ordinal) ||
                !string.Equals(values.ScoringParameterSha256, ApprovedScoringParameterSha256, StringComparison.Ordinal) ||
                !string.Equals(manifest.ScenarioParameterSha256, values.ScenarioParameterSha256, StringComparison.Ordinal) ||
                !string.Equals(manifest.ScoringParameterSha256, values.ScoringParameterSha256, StringComparison.Ordinal))
            {
                throw new Phase8BaselinePolicyFailure(
                    "artifact",
                    "The P8-T05 policy artifact does not match its manifest counts or self-bound hash.");
            }

            return new Phase8BaselinePolicyPack(
                fullArtifactPath,
                manifestPath,
                artifactSha256,
                values.ScenarioParameterSha256,
                values.ScoringParameterSha256,
                values.MaximumActionsPerPolicy,
                new ReadOnlyDictionary<string, Phase8BaselinePolicyV1>(values.Policies));
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

            throw new Phase8BaselinePolicyFailure(
                "artifact",
                "The approved P8-T05 policy artifact could not be located from the current directory or application base directory.");
        }

        public static string FormatFailure(Exception exception)
        {
            if (exception is Phase8BaselinePolicyFailure failure)
            {
                return failure.Path + ": " + failure.Message;
            }

            return exception.Message;
        }

        private static PolicyValues ParseArtifact(
            string json,
            string artifactPath,
            string artifactSha256)
        {
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
                    "artifact",
                    ArtifactProperties);

                RequireStringEquals(root["format"], "artifact.format", ExpectedFormat);
                RequireStringEquals(root["task_id"], "artifact.task_id", "P8-T05");
                RequireStringEquals(
                    root["artifact_id"],
                    "artifact.artifact_id",
                    "p8-t05-scripted-policy-parameters-v1");
                RequireStringEquals(root["status"], "artifact.status", ApprovedStatus);
                RequireStringEquals(
                    root["authority_class"],
                    "artifact.authority_class",
                    "ProjectAuthoredSyntheticApprovedParameterAuthority");
                RequireStringEquals(
                    root["runtime_use"],
                    "artifact.runtime_use",
                    "ApprovedPhase8BalanceRegressionOnly");
                ReadNonemptyString(root["scope"], "artifact.scope");
                string scenarioParameterSha256 = ReadSha256(
                    root["scenario_parameter_sha256"],
                    "artifact.scenario_parameter_sha256");
                string scoringParameterSha256 = ReadSha256(
                    root["scoring_parameter_sha256"],
                    "artifact.scoring_parameter_sha256");
                uint policyCount = ReadUInt32(root["policy_count"], "artifact.policy_count");
                uint maximumActionsPerPolicy = ReadUInt32(
                    root["maximum_actions_per_policy"],
                    "artifact.maximum_actions_per_policy");
                if (policyCount == 0 || policyCount > MaximumPolicyCount ||
                    maximumActionsPerPolicy == 0 || maximumActionsPerPolicy > MaximumActionCount)
                {
                    throw Failure(
                        "artifact.policy_count",
                        "The P8-T05 policy and action capacities are outside the approved bounds.");
                }

                JsonElement policyElement = root["policies"];
                if (policyElement.ValueKind != JsonValueKind.Array)
                {
                    throw Failure("artifact.policies", "A policy array is required.");
                }

                var policies = new Dictionary<string, Phase8BaselinePolicyV1>(StringComparer.Ordinal);
                int policyIndex = 0;
                foreach (JsonElement element in policyElement.EnumerateArray())
                {
                    if (policyIndex >= MaximumPolicyCount)
                    {
                        throw Failure("artifact.policies", "The policy collection exceeds the approved maximum.");
                    }

                    Phase8BaselinePolicyV1 policy = ParsePolicy(
                        element,
                        "artifact.policies[" + policyIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        maximumActionsPerPolicy);
                    if (!policies.TryAdd(policy.PolicyId, policy))
                    {
                        throw Failure(
                            "artifact.policies[" + policyIndex.ToString(CultureInfo.InvariantCulture) + "].policy_id",
                            "Policy identifiers must be unique.");
                    }

                    policyIndex++;
                }

                if (policyIndex != policyCount)
                {
                    throw Failure("artifact.policy_count", "The policy count does not match the policy array.");
                }

                ParseSourceAuthority(root["source_authority"]);
                return new PolicyValues(
                    artifactSha256,
                    scenarioParameterSha256,
                    scoringParameterSha256,
                    policyCount,
                    maximumActionsPerPolicy,
                    policies);
            }
            catch (Phase8BaselinePolicyFailure)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new Phase8BaselinePolicyFailure(
                    artifactPath,
                    "The P8-T05 policy artifact is not valid JSON: " + exception.Message,
                    exception);
            }
        }

        private static Phase8BaselinePolicyV1 ParsePolicy(
            JsonElement element,
            string path,
            uint maximumActionsPerPolicy)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                path,
                PolicyProperties);
            string policyId = ReadIdentityString(properties["policy_id"], path + ".policy_id");
            string scenarioId = ReadIdentityString(properties["scenario_id"], path + ".scenario_id");
            string difficultyId = ReadIdentityString(properties["difficulty_id"], path + ".difficulty_id");
            string initialPlaybackModeId = ReadIdentityString(
                properties["initial_playback_mode_id"],
                path + ".initial_playback_mode_id");
            ulong horizonWallMilliseconds = ReadUInt64(
                properties["horizon_wall_ms"],
                path + ".horizon_wall_ms");
            if (horizonWallMilliseconds == 0 ||
                horizonWallMilliseconds > MaximumHorizonWallMilliseconds)
            {
                throw Failure(path + ".horizon_wall_ms", "A policy horizon must be positive.");
            }

            JsonElement actionsElement = properties["actions"];
            if (actionsElement.ValueKind != JsonValueKind.Array)
            {
                throw Failure(path + ".actions", "A policy action array is required.");
            }

            var actions = new List<Phase8BaselinePolicyActionV1>();
            ulong previousAtWallMilliseconds = 0;
            bool hasPrevious = false;
            int actionIndex = 0;
            foreach (JsonElement actionElement in actionsElement.EnumerateArray())
            {
                if ((uint)actionIndex >= maximumActionsPerPolicy)
                {
                    throw Failure(path + ".actions", "The policy exceeds its approved action capacity.");
                }

                string actionPath = path + ".actions[" + actionIndex.ToString(CultureInfo.InvariantCulture) + "]";
                Dictionary<string, JsonElement> actionProperties = ReadExactObject(
                    actionElement,
                    actionPath,
                    ActionProperties);
                ulong atWallMilliseconds = ReadUInt64(
                    actionProperties["at_wall_ms"],
                    actionPath + ".at_wall_ms");
                if (atWallMilliseconds > horizonWallMilliseconds ||
                    (hasPrevious && atWallMilliseconds <= previousAtWallMilliseconds))
                {
                    throw Failure(
                        actionPath + ".at_wall_ms",
                        "Policy action times must be strictly increasing and within the horizon.");
                }

                string actionText = ReadNonemptyString(
                    actionProperties["action"],
                    actionPath + ".action");
                Phase8BaselinePolicyActionKindV1 actionKind = actionText switch
                {
                    "set_power_target" => Phase8BaselinePolicyActionKindV1.SetPowerTarget,
                    "set_tilt_target" => Phase8BaselinePolicyActionKindV1.SetTiltTarget,
                    _ => throw Failure(actionPath + ".action", "The baseline policy action kind is not supported.")
                };
                double targetFraction = ReadFiniteDouble(
                    actionProperties["target_fraction"],
                    actionPath + ".target_fraction");
                if (actionKind == Phase8BaselinePolicyActionKindV1.SetTiltTarget && targetFraction < 0)
                {
                    throw Failure(
                        actionPath + ".target_fraction",
                        "A tilt target must be nonnegative.");
                }

                actions.Add(new Phase8BaselinePolicyActionV1(
                    atWallMilliseconds,
                    actionKind,
                    targetFraction));
                previousAtWallMilliseconds = atWallMilliseconds;
                hasPrevious = true;
                actionIndex++;
            }

            Phase8BaselinePolicyExpectedV1 expected = ParseExpected(
                properties["expected"],
                path + ".expected",
                horizonWallMilliseconds);
            return new Phase8BaselinePolicyV1(
                policyId,
                scenarioId,
                difficultyId,
                initialPlaybackModeId,
                horizonWallMilliseconds,
                actions,
                expected);
        }

        private static Phase8BaselinePolicyExpectedV1 ParseExpected(
            JsonElement element,
            string path,
            ulong horizonWallMilliseconds)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                path,
                ExpectedProperties);

            string outcome = ReadNonemptyString(properties["outcome"], path + ".outcome");
            if (!string.Equals(outcome, nameof(Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon), StringComparison.Ordinal) &&
                !string.Equals(outcome, nameof(Phase8ScenarioOutcomeV1.RecordLoss), StringComparison.Ordinal))
            {
                throw Failure(
                    path + ".outcome",
                    "A baseline expected outcome must be a resolved Phase 8 scenario outcome.");
            }

            double simulationTimeSeconds = ReadFiniteDouble(
                properties["simulation_time_s"],
                path + ".simulation_time_s");
            double maximumSimulationTimeSeconds =
                horizonWallMilliseconds / 1000.0 * ApprovedMaximumSimulationAccelerationFactor;
            if (simulationTimeSeconds < 0 || simulationTimeSeconds > maximumSimulationTimeSeconds)
            {
                throw Failure(
                    path + ".simulation_time_s",
                    "The expected simulation time is outside the approved wall-horizon and pacing bounds.");
            }

            double wallElapsedSeconds = ReadFiniteDouble(
                properties["wall_elapsed_s"],
                path + ".wall_elapsed_s");
            double maximumWallElapsedSeconds = horizonWallMilliseconds / 1000.0;
            if (wallElapsedSeconds < 0 || wallElapsedSeconds > maximumWallElapsedSeconds)
            {
                throw Failure(
                    path + ".wall_elapsed_s",
                    "The expected wall elapsed time is outside the policy horizon.");
            }

            double scoreTotal = ReadFiniteDouble(properties["score_total"], path + ".score_total");
            if (scoreTotal < ApprovedScoreMinimum || scoreTotal > ApprovedScoreMaximum)
            {
                throw Failure(
                    path + ".score_total",
                    "The expected score is outside the approved P8-T03 score range.");
            }

            uint lossCount = ReadUInt32(properties["loss_count"], path + ".loss_count");
            if (lossCount > 1)
            {
                throw Failure(
                    path + ".loss_count",
                    "A resolved Phase 8 baseline may contain at most one record loss.");
            }

            int turnSummaryCount = ReadInt32(
                properties["turn_summary_count"],
                path + ".turn_summary_count");
            if (string.Equals(outcome, nameof(Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon), StringComparison.Ordinal) &&
                lossCount != 0)
            {
                throw Failure(
                    path,
                    "A survived baseline must have zero record losses.");
            }

            if (string.Equals(outcome, nameof(Phase8ScenarioOutcomeV1.RecordLoss), StringComparison.Ordinal) &&
                lossCount != 1)
            {
                throw Failure(
                    path,
                    "A record-loss baseline must contain exactly one record loss.");
            }

            return new Phase8BaselinePolicyExpectedV1(
                outcome,
                simulationTimeSeconds,
                wallElapsedSeconds,
                scoreTotal,
                lossCount,
                turnSummaryCount,
                ReadSha256(properties["replay_digest"], path + ".replay_digest"));
        }

        private static void ParseSourceAuthority(JsonElement element)
        {
            Dictionary<string, JsonElement> properties = ReadExactObject(
                element,
                "artifact.source_authority",
                SourceAuthorityProperties);
            RequireStringEquals(
                properties["kind"],
                "artifact.source_authority.kind",
                "ProjectAuthoredSynthetic");
            RequireStringEquals(
                properties["literature_applicability"],
                "artifact.source_authority.literature_applicability",
                "NotApplicable");
            ReadNonemptyString(properties["coverage_reason"], "artifact.source_authority.coverage_reason");
            if (ReadBoolean(properties["external_source_artifacts_used"], "artifact.source_authority.external_source_artifacts_used") ||
                ReadBoolean(properties["production_or_external_reference_claim"], "artifact.source_authority.production_or_external_reference_claim"))
            {
                throw Failure(
                    "artifact.source_authority",
                    "P8-T05 policy authority must not claim external or production evidence.");
            }
        }

        private static ManifestValues ParseManifest(string json, string manifestPath)
        {
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
                    "manifest",
                    ManifestProperties);
                Dictionary<string, JsonElement> artifact = ReadExactObject(
                    root["artifact"],
                    "manifest.artifact",
                    ManifestArtifactProperties);
                return new ManifestValues(
                    ReadNonemptyString(root["format"], "manifest.format"),
                    ReadNonemptyString(root["task_id"], "manifest.task_id"),
                    ReadNonemptyString(root["artifact_id"], "manifest.artifact_id"),
                    ReadNonemptyString(root["status"], "manifest.status"),
                    ReadNonemptyString(root["owner_decision"], "manifest.owner_decision"),
                    ReadNonemptyString(artifact["path"], "manifest.artifact.path"),
                    ReadInt64(artifact["byte_length"], "manifest.artifact.byte_length"),
                    ReadSha256(artifact["sha256"], "manifest.artifact.sha256"),
                    ReadSha256(root["scenario_parameter_sha256"], "manifest.scenario_parameter_sha256"),
                    ReadSha256(root["scoring_parameter_sha256"], "manifest.scoring_parameter_sha256"),
                    ReadUInt32(root["policy_count"], "manifest.policy_count"),
                    ReadUInt32(root["maximum_actions_per_policy"], "manifest.maximum_actions_per_policy"));
            }
            catch (Phase8BaselinePolicyFailure)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new Phase8BaselinePolicyFailure(
                    manifestPath,
                    "The P8-T05 policy manifest is not valid JSON: " + exception.Message,
                    exception);
            }
        }

        private static Dictionary<string, JsonElement> ReadExactObject(
            JsonElement element,
            string path,
            string[] expectedProperties)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Failure(path, "A JSON object is required.");
            }

            var expected = new HashSet<string>(expectedProperties, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
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

                result.Add(property.Name, property.Value);
                propertyIndex++;
            }

            if (propertyIndex != expectedProperties.Length)
            {
                throw Failure(path, "A required JSON property is missing: " + expectedProperties[propertyIndex] + ".");
            }

            return result;
        }

        private static string ReadIdentityString(JsonElement element, string path)
        {
            string value = ReadNonemptyString(element, path);
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw Failure(path, "An identity string may not have surrounding whitespace.");
            }

            return value;
        }

        private static string ReadNonemptyString(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw Failure(path, "A JSON string is required.");
            }

            string? value = element.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Failure(path, "A nonempty JSON string is required.");
            }

            return value;
        }

        private static void RequireStringEquals(JsonElement element, string path, string expected)
        {
            string value = ReadNonemptyString(element, path);
            if (!string.Equals(value, expected, StringComparison.Ordinal))
            {
                throw Failure(path, "The value does not match the approved authority.");
            }
        }

        private static uint ReadUInt32(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint value))
            {
                throw Failure(path, "A nonnegative JSON UInt32 is required.");
            }

            return value;
        }

        private static int ReadInt32(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value) || value < 0)
            {
                throw Failure(path, "A nonnegative JSON Int32 is required.");
            }

            return value;
        }

        private static long ReadInt64(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out long value) || value < 0)
            {
                throw Failure(path, "A nonnegative JSON Int64 is required.");
            }

            return value;
        }

        private static ulong ReadUInt64(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt64(out ulong value))
            {
                throw Failure(path, "A nonnegative JSON UInt64 is required.");
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
                throw Failure(path, "A finite JSON number is required.");
            }

            return value;
        }

        private static bool ReadBoolean(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
            {
                throw Failure(path, "A JSON boolean is required.");
            }

            return element.GetBoolean();
        }

        private static string ReadSha256(JsonElement element, string path)
        {
            string value = ReadNonemptyString(element, path);
            if (value.Length != 64 || value.Any(character =>
                    !((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f'))))
            {
                throw Failure(path, "A lowercase 64-character SHA-256 digest is required.");
            }

            return value;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            byte[] digest = SHA256.HashData(bytes);
            var builder = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static Phase8BaselinePolicyFailure Failure(string path, string message)
        {
            return new Phase8BaselinePolicyFailure(path, message);
        }

        private sealed class PolicyValues
        {
            public PolicyValues(
                string artifactSha256,
                string scenarioParameterSha256,
                string scoringParameterSha256,
                uint policyCount,
                uint maximumActionsPerPolicy,
                Dictionary<string, Phase8BaselinePolicyV1> policies)
            {
                ArtifactSha256 = artifactSha256;
                ScenarioParameterSha256 = scenarioParameterSha256;
                ScoringParameterSha256 = scoringParameterSha256;
                PolicyCount = policyCount;
                MaximumActionsPerPolicy = maximumActionsPerPolicy;
                Policies = policies;
            }

            public string ArtifactSha256 { get; }

            public string ScenarioParameterSha256 { get; }

            public string ScoringParameterSha256 { get; }

            public uint PolicyCount { get; }

            public uint MaximumActionsPerPolicy { get; }

            public Dictionary<string, Phase8BaselinePolicyV1> Policies { get; }
        }

        private sealed class ManifestValues
        {
            public ManifestValues(
                string format,
                string taskId,
                string artifactId,
                string status,
                string ownerDecision,
                string artifactPath,
                long artifactByteLength,
                string artifactSha256,
                string scenarioParameterSha256,
                string scoringParameterSha256,
                uint policyCount,
                uint maximumActionsPerPolicy)
            {
                Format = format;
                TaskId = taskId;
                ArtifactId = artifactId;
                Status = status;
                OwnerDecision = ownerDecision;
                ArtifactPath = artifactPath;
                ArtifactByteLength = artifactByteLength;
                ArtifactSha256 = artifactSha256;
                ScenarioParameterSha256 = scenarioParameterSha256;
                ScoringParameterSha256 = scoringParameterSha256;
                PolicyCount = policyCount;
                MaximumActionsPerPolicy = maximumActionsPerPolicy;
            }

            public string Format { get; }

            public string TaskId { get; }

            public string ArtifactId { get; }

            public string Status { get; }

            public string OwnerDecision { get; }

            public string ArtifactPath { get; }

            public long ArtifactByteLength { get; }

            public string ArtifactSha256 { get; }

            public string ScenarioParameterSha256 { get; }

            public string ScoringParameterSha256 { get; }

            public uint PolicyCount { get; }

            public uint MaximumActionsPerPolicy { get; }
        }
    }

    internal sealed class Phase8BaselinePolicyFailure : InvalidOperationException
    {
        public Phase8BaselinePolicyFailure(string path, string message)
            : base(message)
        {
            Path = path;
        }

        public Phase8BaselinePolicyFailure(string path, string message, Exception innerException)
            : base(message, innerException)
        {
            Path = path;
        }

        public string Path { get; }
    }

    internal sealed class Phase8BaselinePolicyRunResultV1
    {
        public Phase8BaselinePolicyRunResultV1(
            string policyId,
            string scenarioId,
            string difficultyId,
            Phase8ScenarioOutcomeV1 outcome,
            double simulationTimeSeconds,
            double wallElapsedSeconds,
            double scoreTotal,
            uint lossCount,
            int turnSummaryCount,
            string replayDigest)
        {
            PolicyId = policyId;
            ScenarioId = scenarioId;
            DifficultyId = difficultyId;
            Outcome = outcome;
            SimulationTimeSeconds = simulationTimeSeconds;
            WallElapsedSeconds = wallElapsedSeconds;
            ScoreTotal = scoreTotal;
            LossCount = lossCount;
            TurnSummaryCount = turnSummaryCount;
            ReplayDigest = replayDigest;
        }

        public string PolicyId { get; }

        public string ScenarioId { get; }

        public string DifficultyId { get; }

        public Phase8ScenarioOutcomeV1 Outcome { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public double ScoreTotal { get; }

        public uint LossCount { get; }

        public int TurnSummaryCount { get; }

        public string ReplayDigest { get; }
    }
}
