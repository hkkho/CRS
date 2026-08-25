using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ReactorSim.Cli;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class Phase8ScoringParameterPackTests
{
    [Fact]
    public void ApprovedScoringPackLoadsWithItsExpectedAuthority()
    {
        string artifactPath = Phase8ScoringParameterPack.FindDefaultPath();
        Phase8ScoringParameterPack pack = Phase8ScoringParameterPack.LoadApproved(artifactPath);

        Assert.Equal(
            "4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c",
            pack.ArtifactSha256);
        Assert.Equal(500.0, pack.Parameters.SurvivalPoints);
        Assert.Equal(250.0, pack.Parameters.EnergyQualityPoints);
        Assert.Equal(150.0, pack.Parameters.StabilityQualityPoints);
        Assert.Equal(50.0, pack.Parameters.FuellingEfficiencyPoints);
        Assert.Equal(10.0, pack.Parameters.ControlActionPenaltyPoints);
        Assert.Equal(250.0, pack.Parameters.RecordLossPenaltyPoints);
        Assert.Equal(16u, pack.Parameters.MaximumSummaryEventsPerTurn);
    }

    [Fact]
    public void ModifiedScoringArtifactFailsManifestBinding()
    {
        (string root, string artifactPath) copy = CopyApprovedPack();
        try
        {
            File.AppendAllText(copy.artifactPath, "\n");

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScoringParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("hash or byte length", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void WrongScoringManifestFormatFailsClosed()
    {
        (string root, string artifactPath) copy = CopyApprovedPack();
        try
        {
            string manifestPath = Path.Combine(
                Path.GetDirectoryName(copy.artifactPath)!,
                Path.GetFileNameWithoutExtension(copy.artifactPath) + ".manifest.json");
            string manifest = File.ReadAllText(manifestPath).Replace(
                "reactorsim.p8-scoring-parameters-manifest/v1",
                "wrong.manifest/v1",
                StringComparison.Ordinal);
            File.WriteAllText(manifestPath, manifest);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScoringParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("Expected 'reactorsim.p8-scoring-parameters-manifest/v1'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void ReboundArtifactFormatTamperingFailsClosed()
    {
        AssertReboundArtifactFails(
            "reactorsim.p8-scoring-parameters/v1",
            "wrong.scoring/v1",
            "Expected 'reactorsim.p8-scoring-parameters/v1'");
    }

    [Fact]
    public void ReboundArtifactStatusTamperingFailsClosed()
    {
        AssertReboundArtifactFails(
            "Approved/ApprovedParameterAuthority",
            "Draft/DraftParameterAuthority",
            "Expected 'Approved/ApprovedParameterAuthority'");
    }

    [Fact]
    public void ReboundArtifactFormulaTamperingFailsClosed()
    {
        AssertReboundArtifactFails(
            "clamp(sum(state_segment_normalized_power_fraction * segment_duration_s) / scenario_horizon_s / nominal_power_fraction, 0, 1)",
            "tampered_formula",
            "Expected 'clamp(sum(state_segment_normalized_power_fraction * segment_duration_s) / scenario_horizon_s / nominal_power_fraction, 0, 1)'");
    }

    [Fact]
    public void ReboundSummaryCausePolicyTamperingFailsClosed()
    {
        AssertReboundArtifactFails(
            "Prefer loss identifiers, then scripted event kinds, then committed player action kinds, otherwise report elapsed play.",
            "tampered cause policy",
            "Expected 'Prefer loss identifiers, then scripted event kinds, then committed player action kinds, otherwise report elapsed play.'");
    }

    [Fact]
    public void ReboundSummaryFieldTamperingFailsClosed()
    {
        AssertReboundArtifactFails(
            "\"score_total\"",
            "\"tampered_score_total\"",
            "Expected the exact approved JSON string array.");
    }

    [Fact]
    public void ReboundArtifactDuplicatePropertyFailsClosed()
    {
        (string root, string artifactPath) copy = CopyApprovedPack();
        try
        {
            string artifact = File.ReadAllText(copy.artifactPath).Replace(
                "  \"status\": \"Approved/ApprovedParameterAuthority\",",
                "  \"status\": \"Approved/ApprovedParameterAuthority\",\n  \"status\": \"Approved/ApprovedParameterAuthority\",",
                StringComparison.Ordinal);
            File.WriteAllText(copy.artifactPath, artifact);
            RebindManifest(copy.artifactPath);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScoringParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("Duplicate JSON property: status", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void ReboundArtifactMissingRequiredFieldFailsClosed()
    {
        (string root, string artifactPath) copy = CopyApprovedPack();
        try
        {
            string artifact = File.ReadAllText(copy.artifactPath).Replace(
                "  \"authority_class\": \"ProjectAuthoredSyntheticApprovedParameterAuthority\",\n",
                string.Empty,
                StringComparison.Ordinal);
            File.WriteAllText(copy.artifactPath, artifact);
            RebindManifest(copy.artifactPath);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScoringParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("A required JSON property is missing.", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    private static void AssertReboundArtifactFails(
        string original,
        string replacement,
        string expectedMessage)
    {
        (string root, string artifactPath) copy = CopyApprovedPack();
        try
        {
            string artifact = File.ReadAllText(copy.artifactPath).Replace(
                original,
                replacement,
                StringComparison.Ordinal);
            Assert.NotEqual(File.ReadAllText(Phase8ScoringParameterPack.FindDefaultPath()), artifact);
            File.WriteAllText(copy.artifactPath, artifact);
            RebindManifest(copy.artifactPath);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScoringParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    private static void RebindManifest(string artifactPath)
    {
        string manifestPath = Path.Combine(
            Path.GetDirectoryName(artifactPath)!,
            Path.GetFileNameWithoutExtension(artifactPath) + ".manifest.json");
        byte[] bytes = File.ReadAllBytes(artifactPath);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string manifest = File.ReadAllText(manifestPath);
        manifest = Regex.Replace(
            manifest,
            "(\"byte_length\"\\s*:\\s*)\\d+",
            match => match.Groups[1].Value + bytes.Length);
        manifest = Regex.Replace(
            manifest,
            "(\"sha256\"\\s*:\\s*)\"[0-9a-f]+\"",
            match => match.Groups[1].Value + "\"" + hash + "\"");
        File.WriteAllText(manifestPath, manifest);
    }

    private static (string root, string artifactPath) CopyApprovedPack()
    {
        string sourceArtifact = Phase8ScoringParameterPack.FindDefaultPath();
        string sourceManifest = Path.Combine(
            Path.GetDirectoryName(sourceArtifact)!,
            Path.GetFileNameWithoutExtension(sourceArtifact) + ".manifest.json");
        string repositoryRoot = FindRepositoryRoot();
        string root = Path.Combine(
            Path.GetTempPath(),
            "reactorsim-p8-t03-pack-test-" + Guid.NewGuid().ToString("N"));
        string scenarioDirectory = Path.Combine(root, "data", "scenarios");
        string approvalDirectory = Path.Combine(root, "docs", "tasks");
        Directory.CreateDirectory(scenarioDirectory);
        Directory.CreateDirectory(approvalDirectory);
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "test root");

        string artifactPath = Path.Combine(scenarioDirectory, Path.GetFileName(sourceArtifact));
        File.Copy(sourceArtifact, artifactPath);
        File.Copy(sourceManifest, Path.Combine(scenarioDirectory, Path.GetFileName(sourceManifest)));
        File.Copy(
            Path.Combine(repositoryRoot, "docs", "tasks", "P8-T03-OWNER-APPROVAL.md"),
            Path.Combine(approvalDirectory, "P8-T03-OWNER-APPROVAL.md"));
        return (root, artifactPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found for the P8-T03 pack test.");
    }
}
