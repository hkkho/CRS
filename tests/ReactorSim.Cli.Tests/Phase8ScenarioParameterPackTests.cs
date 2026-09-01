using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ReactorSim.Cli;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class Phase8ScenarioParameterPackTests
{
    [Fact]
    public void ApprovedPackBindsTheExpectedCollectionsAndHash()
    {
        (string root, string artifactPath, string manifestPath) copy = CopyApprovedPack();
        try
        {
            Phase8ScenarioParameterPack pack =
                Phase8ScenarioParameterPack.LoadApproved(copy.artifactPath);

            Assert.Equal(3, pack.PlaybackModes.Count);
            Assert.Equal(3, pack.DifficultyProfiles.Count);
            Assert.Equal(5, pack.Scenarios.Count);
            Assert.Equal(
                "80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2",
                pack.ArtifactSha256);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void ModifiedArtifactFailsManifestBinding()
    {
        (string root, string artifactPath, string manifestPath) copy = CopyApprovedPack();
        try
        {
            File.AppendAllText(copy.artifactPath, "\n");

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScenarioParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("hash or byte length", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void WrongArtifactFormatFailsClosedEvenWhenManifestIsRebound()
    {
        (string root, string artifactPath, string manifestPath) copy = CopyApprovedPack();
        try
        {
            string artifact = File.ReadAllText(copy.artifactPath).Replace(
                "reactorsim.p8-scenario-difficulty-parameters/v1",
                "wrong.format/v1",
                StringComparison.Ordinal);
            File.WriteAllText(copy.artifactPath, artifact);
            UpdateManifestBinding(copy.manifestPath, copy.artifactPath);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScenarioParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("Expected 'reactorsim.p8-scenario-difficulty-parameters/v1'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void WrongManifestFormatFailsClosed()
    {
        (string root, string artifactPath, string manifestPath) copy = CopyApprovedPack();
        try
        {
            string manifest = File.ReadAllText(copy.manifestPath).Replace(
                "reactorsim.p8-scenario-difficulty-parameters-manifest/v1",
                "wrong.manifest/v1",
                StringComparison.Ordinal);
            File.WriteAllText(copy.manifestPath, manifest);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScenarioParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("Expected 'reactorsim.p8-scenario-difficulty-parameters-manifest/v1'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    [Fact]
    public void WrongArtifactStatusFailsClosedEvenWhenManifestIsRebound()
    {
        (string root, string artifactPath, string manifestPath) copy = CopyApprovedPack();
        try
        {
            string artifact = File.ReadAllText(copy.artifactPath).Replace(
                "Approved/ApprovedParameterAuthority",
                "Draft/NotApproved",
                StringComparison.Ordinal);
            File.WriteAllText(copy.artifactPath, artifact);
            UpdateManifestBinding(copy.manifestPath, copy.artifactPath);

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
                () => Phase8ScenarioParameterPack.LoadApproved(copy.artifactPath));
            Assert.Contains("Expected 'Approved/ApprovedParameterAuthority'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(copy.root, recursive: true);
        }
    }

    private static (string root, string artifactPath, string manifestPath) CopyApprovedPack()
    {
        string sourceArtifact = Phase8ScenarioParameterPack.FindDefaultPath();
        string sourceManifest = Path.Combine(
            Path.GetDirectoryName(sourceArtifact)!,
            Path.GetFileNameWithoutExtension(sourceArtifact) + ".manifest.json");
        string root = Path.Combine(
            Path.GetTempPath(),
            "reactorsim-p8-pack-test-" + Guid.NewGuid().ToString("N"));
        string scenarioDirectory = Path.Combine(root, "data", "scenarios");
        Directory.CreateDirectory(scenarioDirectory);

        string artifactPath = Path.Combine(scenarioDirectory, Path.GetFileName(sourceArtifact));
        string manifestPath = Path.Combine(scenarioDirectory, Path.GetFileName(sourceManifest));
        File.Copy(sourceArtifact, artifactPath);
        File.Copy(sourceManifest, manifestPath);
        return (root, artifactPath, manifestPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ReactorSim.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found for the pack test.");
    }

    private static void UpdateManifestBinding(string manifestPath, string artifactPath)
    {
        byte[] bytes = File.ReadAllBytes(artifactPath);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string manifest = File.ReadAllText(manifestPath);
        manifest = Regex.Replace(
            manifest,
            "\\\"byte_length\\\"\\s*:\\s*\\d+",
            "\"byte_length\": " + bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RegexOptions.CultureInvariant);
        manifest = Regex.Replace(
            manifest,
            "\\\"sha256\\\"\\s*:\\s*\\\"[0-9a-fA-F]+\\\"",
            "\"sha256\": \"" + hash + "\"",
            RegexOptions.CultureInvariant);
        File.WriteAllText(manifestPath, manifest);
    }
}
