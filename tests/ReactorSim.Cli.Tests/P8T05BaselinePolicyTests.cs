using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class P8T05BaselinePolicyTests
{
    [Fact]
    public void ApprovedPackBindsTheFrozenScenarioAndScoringAuthorities()
    {
        Phase8BaselinePolicyPack pack = Phase8BaselinePolicyPack.LoadApproved(
            Phase8BaselinePolicyPack.FindDefaultPath());

        Assert.Equal(4, pack.Policies.Count);
        Assert.Equal(
            Phase8BaselinePolicyPack.ApprovedPolicyParameterSha256,
            pack.ArtifactSha256);
        Assert.Equal(
            Phase8BaselinePolicyPack.ApprovedScenarioParameterSha256,
            pack.ScenarioParameterSha256);
        Assert.Equal(
            Phase8BaselinePolicyPack.ApprovedScoringParameterSha256,
            pack.ScoringParameterSha256);
        Assert.All(pack.Policies.Values, policy =>
        {
            Assert.NotEqual("PENDING", policy.Expected.Outcome);
            Assert.NotEmpty(policy.Expected.ReplayDigest);
        });
    }

    [Fact]
    public void BaselinePoliciesAreDeterministicAndMatchFrozenExpectedObservables()
    {
        Phase8BaselinePolicyPack pack = Phase8BaselinePolicyPack.LoadApproved(
            Phase8BaselinePolicyPack.FindDefaultPath());
        var pendingResults = new StringBuilder();
        foreach (Phase8BaselinePolicyV1 policy in pack.Policies.Values)
        {
            ContractValidationResult<Phase8BaselinePolicyRunResultV1> first =
                CliApplication.RunBaselinePolicy(policy.PolicyId);
            ContractValidationResult<Phase8BaselinePolicyRunResultV1> second =
                CliApplication.RunBaselinePolicy(policy.PolicyId);
            Assert.True(
                first.IsValid,
                first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
            Assert.True(
                second.IsValid,
                second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
            Assert.Equal(first.Value.Outcome, second.Value.Outcome);
            Assert.Equal(first.Value.SimulationTimeSeconds, second.Value.SimulationTimeSeconds);
            Assert.Equal(first.Value.WallElapsedSeconds, second.Value.WallElapsedSeconds);
            Assert.Equal(first.Value.ScoreTotal, second.Value.ScoreTotal);
            Assert.Equal(first.Value.LossCount, second.Value.LossCount);
            Assert.Equal(first.Value.TurnSummaryCount, second.Value.TurnSummaryCount);
            Assert.Equal(first.Value.ReplayDigest, second.Value.ReplayDigest);

            if (string.Equals(policy.Expected.Outcome, "PENDING", StringComparison.Ordinal))
            {
                pendingResults.Append(policy.PolicyId)
                    .Append('|')
                    .Append(first.Value.Outcome)
                    .Append('|')
                    .Append(first.Value.SimulationTimeSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(first.Value.WallElapsedSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(first.Value.ScoreTotal.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(first.Value.LossCount)
                    .Append('|')
                    .Append(first.Value.TurnSummaryCount)
                    .Append('|')
                    .Append(first.Value.ReplayDigest)
                    .AppendLine();
                continue;
            }

            Assert.Equal(policy.Expected.Outcome, first.Value.Outcome.ToString());
            Assert.Equal(policy.Expected.SimulationTimeSeconds, first.Value.SimulationTimeSeconds);
            Assert.Equal(policy.Expected.WallElapsedSeconds, first.Value.WallElapsedSeconds);
            Assert.Equal(policy.Expected.ScoreTotal, first.Value.ScoreTotal);
            Assert.Equal(policy.Expected.LossCount, first.Value.LossCount);
            Assert.Equal(policy.Expected.TurnSummaryCount, first.Value.TurnSummaryCount);
            Assert.Equal(policy.Expected.ReplayDigest, first.Value.ReplayDigest);
        }

        Assert.True(pendingResults.Length == 0, pendingResults.ToString());
    }

    [Fact]
    public void UnknownPolicyIsRejectedWithoutRunningACommandStream()
    {
        ContractValidationResult<Phase8BaselinePolicyRunResultV1> result =
            CliApplication.RunBaselinePolicy("not-an-approved-policy");

        Assert.False(result.IsValid);
        Assert.Equal("Phase8BaselinePolicy.NotFound", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void MalformedOrTamperedPolicyArtifactsFailClosed()
    {
        string sourceArtifact = Phase8BaselinePolicyPack.FindDefaultPath();
        string sourceManifest = Path.Combine(
            Path.GetDirectoryName(sourceArtifact)!,
            Path.GetFileNameWithoutExtension(sourceArtifact) + ".manifest.json");
        string artifact = File.ReadAllText(sourceArtifact);
        string manifest = File.ReadAllText(sourceManifest);
        var malformedDocuments = new List<string>
        {
            artifact.Replace(
                "\"format\": \"reactorsim.p8-scripted-policy-parameters/v1\"",
                "\"format\": \"reactorsim.p8-scripted-policy-parameters/v1\", \"format\": \"reactorsim.p8-scripted-policy-parameters/v1\"",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"policies\": [",
                "\"unknown\": [], \"policies\": [",
                StringComparison.Ordinal),
            artifact[..^1] + ",}",
            "/* rejected comment */" + artifact,
            artifact.Replace(
                "\"at_wall_ms\": 1000, \"action\": \"set_power_target\"",
                "\"at_wall_ms\": 2000, \"action\": \"set_power_target\"",
                StringComparison.Ordinal),
            artifact.Replace(
                "80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2",
                new string('0', 64),
                StringComparison.Ordinal),
            artifact.Replace(
                "4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c",
                new string('0', 64),
                StringComparison.Ordinal)
        };

        foreach (string malformedArtifact in malformedDocuments)
        {
            Assert.NotEqual(artifact, malformedArtifact);
            using IDisposable temporary = TemporaryPolicyPack.Create(
                malformedArtifact,
                manifest);
            Assert.ThrowsAny<InvalidOperationException>(() =>
                Phase8BaselinePolicyPack.LoadApproved(TemporaryPolicyPack.ArtifactPath));
        }
    }

    [Fact]
    public void ExpectedObservableTamperingFailsClosed()
    {
        string sourceArtifact = Phase8BaselinePolicyPack.FindDefaultPath();
        string sourceManifest = Path.Combine(
            Path.GetDirectoryName(sourceArtifact)!,
            Path.GetFileNameWithoutExtension(sourceArtifact) + ".manifest.json");
        string artifact = File.ReadAllText(sourceArtifact);
        string manifest = File.ReadAllText(sourceManifest);
        var malformedArtifacts = new List<string>
        {
            artifact.Replace(
                "\"outcome\": \"SurvivedScenarioHorizon\"",
                "\"outcome\": \"PENDING\"",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"simulation_time_s\": 600.0",
                "\"simulation_time_s\": -1.0",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"wall_elapsed_s\": 60.0",
                "\"wall_elapsed_s\": -1.0",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"score_total\": 913.667",
                "\"score_total\": 1001.0",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"loss_count\": 0",
                "\"loss_count\": 2",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"turn_summary_count\": 3",
                "\"turn_summary_count\": -1",
                StringComparison.Ordinal),
            artifact.Replace(
                "\"replay_digest\": \"693bf90a4bef70df3bb9c608057c9efc30e126c4671c6f3a9cc651b1fc4112a9\"",
                "\"replay_digest\": \"000000000000000000000000000000000000000000000000000000000000000g\"",
                StringComparison.Ordinal)
        };

        foreach (string malformedArtifact in malformedArtifacts)
        {
            Assert.NotEqual(artifact, malformedArtifact);
            using IDisposable temporary = TemporaryPolicyPack.Create(
                malformedArtifact,
                manifest);
            Assert.ThrowsAny<InvalidOperationException>(() =>
                Phase8BaselinePolicyPack.LoadApproved(TemporaryPolicyPack.ArtifactPath));
        }
    }

    [Fact]
    public void ManifestIdentityAndBindingTamperingFailsClosed()
    {
        string sourceArtifact = Phase8BaselinePolicyPack.FindDefaultPath();
        string sourceManifest = Path.Combine(
            Path.GetDirectoryName(sourceArtifact)!,
            Path.GetFileNameWithoutExtension(sourceArtifact) + ".manifest.json");
        string artifact = File.ReadAllText(sourceArtifact);
        string manifest = File.ReadAllText(sourceManifest);
        var malformedManifests = new List<string>
        {
            manifest.Replace(
                "\"format\": \"reactorsim.p8-scripted-policy-parameters-manifest/v1\"",
                "\"format\": \"reactorsim.p8-scripted-policy-parameters-manifest/v0\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"status\": \"Approved/ApprovedParameterAuthority\"",
                "\"status\": \"Draft\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"owner_decision\": \"APPROVED\"",
                "\"owner_decision\": \"REJECTED\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"path\": \"data/scenarios/p8-t05-scripted-policy-parameters-v1.json\"",
                "\"path\": \"data/scenarios/other.json\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"sha256\": \"d0e6dec7199751ca0f5d5418892fe0210831e46153ff445ec13e4e84ddf49d39\"",
                "\"sha256\": \"0000000000000000000000000000000000000000000000000000000000000000\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"scenario_parameter_sha256\": \"80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2\"",
                "\"scenario_parameter_sha256\": \"0000000000000000000000000000000000000000000000000000000000000000\"",
                StringComparison.Ordinal),
            manifest.Replace(
                "\"scoring_parameter_sha256\": \"4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c\"",
                "\"scoring_parameter_sha256\": \"0000000000000000000000000000000000000000000000000000000000000000\"",
                StringComparison.Ordinal)
        };

        foreach (string malformedManifest in malformedManifests)
        {
            Assert.NotEqual(manifest, malformedManifest);
            using IDisposable temporary = TemporaryPolicyPack.Create(
                artifact,
                malformedManifest,
                updateArtifactBinding: false);
            Assert.ThrowsAny<InvalidOperationException>(() =>
                Phase8BaselinePolicyPack.LoadApproved(TemporaryPolicyPack.ArtifactPath));
        }
    }

    private sealed class TemporaryPolicyPack : IDisposable
    {
        private readonly string _directory;

        private TemporaryPolicyPack(string directory)
        {
            _directory = directory;
        }

        public static string ArtifactPath { get; private set; } = string.Empty;

        public static TemporaryPolicyPack Create(
            string artifact,
            string manifest,
            bool updateArtifactBinding = true)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "reactorsim-p8-t05-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            ArtifactPath = Path.Combine(
                directory,
                "p8-t05-scripted-policy-parameters-v1.json");
            string manifestPath = Path.Combine(
                directory,
                "p8-t05-scripted-policy-parameters-v1.manifest.json");
            File.WriteAllText(
                ArtifactPath,
                artifact,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                manifestPath,
                updateArtifactBinding ? UpdateManifest(manifest, ArtifactPath) : manifest,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new TemporaryPolicyPack(directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static string UpdateManifest(string manifest, string artifactPath)
        {
            byte[] bytes = File.ReadAllBytes(artifactPath);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string updated = manifest;
            int byteLengthStart = updated.IndexOf("\"byte_length\":", StringComparison.Ordinal) + "\"byte_length\":".Length;
            int byteLengthEnd = updated.IndexOf(',', byteLengthStart);
            updated = updated[..byteLengthStart] + bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + updated[byteLengthEnd..];
            int hashStart = updated.IndexOf("\"sha256\":\"", StringComparison.Ordinal) + "\"sha256\":\"".Length;
            int hashEnd = updated.IndexOf('"', hashStart);
            return updated[..hashStart] + hash + updated[hashEnd..];
        }
    }
}
