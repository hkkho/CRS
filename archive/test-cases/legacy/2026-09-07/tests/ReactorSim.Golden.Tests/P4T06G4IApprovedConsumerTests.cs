using System.Text.Json;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4IApprovedConsumerTests
{
    private const string ArtifactFileName = "approved-manufactured-authority-v1.json";
    private const string ManifestFileName = "approved-manufactured-authority-v1.manifest.json";

    [Fact]
    public void ApprovedGoldenBindsOnlyTheSyntheticDomainAndItsExplicitProfiles()
    {
        using JsonDocument artifact = P4T06G4ICandidateConsumerTests.ReadJson(ArtifactFileName);
        using JsonDocument manifest = P4T06G4ICandidateConsumerTests.ReadJson(ManifestFileName);
        JsonElement root = artifact.RootElement;

        Assert.Equal("approved_golden", P4T06G4ICandidateConsumerTests.StringValue(root, "status"));
        Assert.Equal("Approved", P4T06G4ICandidateConsumerTests.StringValue(root, "evidence_approval"));
        Assert.Equal("Approved", P4T06G4ICandidateConsumerTests.StringValue(root, "comparison_status"));
        Assert.Equal("Approved", P4T06G4ICandidateConsumerTests.StringValue(root, "tolerance_status"));
        Assert.Equal("ApprovedGolden", P4T06G4ICandidateConsumerTests.StringValue(root, "golden_status"));
        Assert.Equal("Synthetic", P4T06G4ICandidateConsumerTests.StringValue(root, "coverage_class"));
        Assert.Equal("Synthetic", P4T06G4ICandidateConsumerTests.StringValue(root, "validation_domain"));
        Assert.Contains(
            "not a direct CANDU physics baseline",
            P4T06G4ICandidateConsumerTests.StringValue(root, "approval_scope"),
            StringComparison.Ordinal);
        Assert.Equal(
            "approved_golden",
            P4T06G4ICandidateConsumerTests.StringValue(manifest.RootElement, "disposition"));
        Assert.Equal(
            P4T06G4ICandidateConsumerTests.HashFor(ArtifactFileName),
            P4T06G4ICandidateConsumerTests.StringValue(manifest.RootElement, "artifact_sha256"));

        JsonElement profiles = root.GetProperty("tolerance").GetProperty("profiles");
        foreach (string quantityId in new[]
        {
            "spatial.k",
            "spatial.flux",
            "spatial.node_power",
            "spatial.total_power",
            "spatial.equation_residual"
        })
        {
            JsonElement profile = profiles.GetProperty(quantityId);
            Assert.Equal(1e-10, profile.GetProperty("absolute").GetDouble());
            Assert.Equal(1e-10, profile.GetProperty("relative").GetDouble());
        }

        JsonElement convergenceProfile = profiles.GetProperty("spatial.convergence");
        Assert.Equal("exact_discrete_and_profile", convergenceProfile.GetProperty("rule").GetString());
        Assert.Equal(0.0, convergenceProfile.GetProperty("absolute").GetDouble());
        Assert.Equal(0.0, convergenceProfile.GetProperty("relative").GetDouble());
    }

    [Fact]
    public void ApprovedGoldenConsumerMatchesCoreWithinTheApprovedSyntheticProfiles()
    {
        using JsonDocument artifact = P4T06G4ICandidateConsumerTests.ReadJson(ArtifactFileName);
        JsonElement root = artifact.RootElement;
        ReactorSim.Core.SpatialSolveResult solved =
            P4T06G4ICandidateConsumerTests.SolveCore(root);
        Assert.Equal(ReactorSim.Core.SpatialSolveStatus.Converged, solved.Status);
        Assert.True(solved.HasUsableState);

        JsonElement manufactured = root.GetProperty("manufactured_solution");
        JsonElement profiles = root.GetProperty("tolerance").GetProperty("profiles");
        double toleranceK = profiles.GetProperty("spatial.k").GetProperty("absolute").GetDouble();
        double toleranceFlux = profiles.GetProperty("spatial.flux").GetProperty("absolute").GetDouble();
        double tolerancePower = profiles.GetProperty("spatial.total_power").GetProperty("absolute").GetDouble();
        double toleranceResidual = profiles.GetProperty("spatial.equation_residual").GetProperty("relative").GetDouble();

        Assert.InRange(
            Math.Abs(solved.FinalState!.Eigenvalue - manufactured.GetProperty("exact_eigenvalue").GetDouble()),
            0.0,
            toleranceK);
        Assert.InRange(
            Math.Abs(solved.FinalState.TotalPowerW - root.GetProperty("normalization").GetProperty("target_power_w").GetDouble()),
            0.0,
            tolerancePower);
        Assert.InRange(
            solved.Diagnostics.ResidualRelativeInfinity!.Value,
            0.0,
            toleranceResidual);

        double[] expectedGroup1 = P4T06G4ICandidateConsumerTests.ReadArray(
            manufactured.GetProperty("exact_group1_flux"));
        double[] expectedGroup2 = P4T06G4ICandidateConsumerTests.ReadArray(
            manufactured.GetProperty("exact_group2_flux"));
        double maximumFluxDifference = 0.0;
        for (int index = 0; index < expectedGroup1.Length; index++)
        {
            maximumFluxDifference = Math.Max(
                maximumFluxDifference,
                Math.Abs(solved.FinalState.Group1Flux[index] - expectedGroup1[index]));
            maximumFluxDifference = Math.Max(
                maximumFluxDifference,
                Math.Abs(solved.FinalState.Group2Flux[index] - expectedGroup2[index]));
        }

        Assert.InRange(maximumFluxDifference, 0.0, toleranceFlux);
        Assert.Equal(
            root.GetProperty("convergence").GetProperty("independent_reproduction").GetProperty("outer_iterations").GetInt32(),
            solved.Diagnostics.IterationCount);
        Assert.Equal("converged", solved.Diagnostics.ConvergenceReason);
    }
}
