using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4BAdmittedCandidateConsumerTests
{
    private const string CandidateOutputRelativePath =
        "data/comparisons/p4-t06-r5-candidate-snapshots-v1.json";

    private const string TestArtifactDirectory = "P4T06G4BData";
    private const string CandidateOutputFileName = "candidate-snapshots-v1.json";
    private const string SourceArtifactFileName = "synthetic-input-v1.json";
    private const string ReducedPackFileName = "reduced-candidate-v1.json";
    private const string ReducedManifestFileName = "reduced-candidate-v1.manifest.json";
    private const string BenchmarkManifestFileName = "static-solver-benchmark.json";

    private const string CandidateOutputSha256 =
        "13481d139c2b2f2fd1c043725c0a2ced9f396c2813a954cc604a1266bc4c8211";

    private const string SourceArtifactId = "p4-t06-r4-synthetic-input-v1";
    private const string ReducedPackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string TopologyFixtureId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string UnitsProfileId = "SI-v1";
    private const string TransformId = "identity_projection_v1";
    private const string DataVersion = "synthetic-v1";
    private const string SourceProvenance = "synthetic:P5-T05";

    private static readonly Dictionary<string, string> AdmittedScenarioClasses =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fresh_candidate"] = "fresh",
            ["equilibrium_like_candidate"] = "equilibrium_like",
            ["midcycle_interpolation_candidate"] = "interpolation_midpoint"
        };

    private static readonly Dictionary<string, double> AdmittedBurnup =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["fresh_candidate"] = 0.0,
            ["equilibrium_like_candidate"] = 50000.0,
            ["midcycle_interpolation_candidate"] = 25000.0
        };

    private static readonly HashSet<string> ExpectedCaseIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "fresh_candidate",
            "equilibrium_like_candidate",
            "refuelled_perturbation_candidate",
            "midcycle_interpolation_candidate",
            "rrs_perturbation_candidate",
            "poison_perturbation_candidate"
        };

    private static readonly Dictionary<string, string> ExpectedProfileToQuantity =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P2-T05-determinism-repeat-equal-v1"] = "determinism.repeat_equal",
            ["P2-T05-spatial-coefficient-id-v1"] = "spatial.coefficient_identity",
            ["P2-T05-spatial-convergence-v1"] = "spatial.convergence",
            ["P2-T05-spatial-fission-source-v1"] = "spatial.fission_source",
            ["P2-T05-spatial-flux-v1"] = "spatial.flux",
            ["P2-T05-spatial-iteration-count-v1"] = "spatial.iteration_count",
            ["P2-T05-spatial-k-v1"] = "spatial.k",
            ["P2-T05-spatial-normalization-scale-v1"] = "spatial.normalization_scale",
            ["P2-T05-spatial-power-v1"] = "spatial.power",
            ["P2-T05-spatial-total-power-v1"] = "spatial.total_power"
        };

    private static readonly HashSet<string> ExpectedProfileIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "P2-T05-determinism-repeat-equal-v1",
            "P2-T05-spatial-coefficient-id-v1",
            "P2-T05-spatial-convergence-v1",
            "P2-T05-spatial-fission-source-v1",
            "P2-T05-spatial-flux-v1",
            "P2-T05-spatial-iteration-count-v1",
            "P2-T05-spatial-k-v1",
            "P2-T05-spatial-normalization-scale-v1",
            "P2-T05-spatial-power-v1",
            "P2-T05-spatial-total-power-v1"
        };

    private static readonly HashSet<string> ExpectedQuantityIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "determinism.repeat_equal",
            "spatial.coefficient_identity",
            "spatial.convergence",
            "spatial.fission_source",
            "spatial.flux",
            "spatial.iteration_count",
            "spatial.k",
            "spatial.normalization_scale",
            "spatial.power",
            "spatial.total_power"
        };

    [Fact]
    public void CandidateConsumerBindsExistingArtifactAndProfileIdentities()
    {
        using JsonDocument document = ReadCandidateOutput();
        JsonElement root = document.RootElement;

        Assert.Equal("reactorsim.candidate-spatial-comparison/v1", StringValue(root, "format"));
        Assert.Equal("P4-T06-R5", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("synthetic", StringValue(root, "evidence_class"));
        Assert.Equal("CommittedSynthetic", StringValue(root, "artifact_availability"));
        Assert.Equal("Candidate", StringValue(root, "evidence_approval"));
        Assert.Equal("Runtime", StringValue(root, "validation_domain"));
        Assert.Equal(ReducedPackArtifactId, StringValue(root, "pack_artifact_id"));
        Assert.Equal(SourceArtifactId, StringValue(root, "source_artifact_id"));
        Assert.Equal(TransformId, StringValue(root, "transform_id"));
        Assert.Equal(DataVersion, StringValue(root, "data_version"));
        Assert.Equal(UnitsProfileId, StringValue(root, "units_profile_id"));
        Assert.Equal(SourceProvenance, StringValue(root, "source_provenance"));
        Assert.Equal(TopologyFixtureId, StringValue(root, "topology_fixture_id"));
        Assert.Contains("deferred G4 profiles", StringValue(root, "gate_status"), StringComparison.Ordinal);

        Assert.Equal(HashFor(SourceArtifactFileName), StringValue(root, "source_sha256"));
        Assert.Equal(HashFor(ReducedPackFileName), StringValue(root, "pack_sha256"));
        Assert.Equal(HashFor(ReducedManifestFileName), StringValue(root, "manifest_sha256"));
        Assert.Equal(HashFor(BenchmarkManifestFileName), StringValue(root, "benchmark_manifest_sha256"));
        Assert.Equal(CandidateOutputSha256, HashFor(CandidateOutputFileName));
    }

    [Fact]
    public void AdmittedCasesAreTheThreeBoundedStaticCandidateSnapshots()
    {
        using JsonDocument document = ReadCandidateOutput();
        JsonElement cases = Required(document.RootElement, "cases");
        Assert.Equal(6, cases.GetArrayLength());
        AssertExactCaseSet(cases);

        var admittedInputDigests = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement candidateCase in cases.EnumerateArray())
        {
            string caseId = StringValue(candidateCase, "case_id");
            if (!AdmittedScenarioClasses.TryGetValue(caseId, out string? scenarioClass))
            {
                continue;
            }

            Assert.Equal(scenarioClass, StringValue(candidateCase, "scenario_class"));
            Assert.Equal("CandidateSnapshot", StringValue(candidateCase, "status"));

            JsonElement burnup = Required(candidateCase, "burnup_j_per_kg_hm_by_node");
            Assert.Equal(3, burnup.GetArrayLength());
            foreach (JsonElement nodeBurnup in burnup.EnumerateArray())
            {
                Assert.Equal(AdmittedBurnup[caseId], nodeBurnup.GetDouble());
            }

            JsonElement snapshot = Required(candidateCase, "snapshot");
            string inputDigest = StringValue(snapshot, "input_digest");
            AssertHexDigest(inputDigest);
            AssertHexDigest(StringValue(snapshot, "coefficient_identity"));
            AssertHexDigest(StringValue(snapshot, "snapshot_digest"));
            Assert.True(admittedInputDigests.Add(inputDigest), $"Duplicate admitted input digest for {caseId}.");

            JsonElement comparison = Required(snapshot, "comparison");
            Assert.Equal("reduced_pack_projection", StringValue(comparison, "left_path"));
            Assert.Equal("source_contract_projection", StringValue(comparison, "right_path"));
            Assert.True(BooleanValue(comparison, "exact_bitwise_equal"));
            Assert.True(BooleanValue(comparison, "repeat_equal"));
            Assert.Equal(0.0, NumberValue(comparison, "maximum_absolute_difference"));
            Assert.Equal(0.0, NumberValue(comparison, "maximum_relative_difference"));
            Assert.Equal(
                "Diagnostic exact equality only; numeric acceptance remains deferred to G4.",
                StringValue(comparison, "acceptance_status"));
        }

        Assert.Equal(AdmittedScenarioClasses.Count, admittedInputDigests.Count);
    }

    [Fact]
    public void DeferredCasesRemainOutsideTheConsumerBoundary()
    {
        using JsonDocument document = ReadCandidateOutput();
        JsonElement cases = Required(document.RootElement, "cases");
        AssertExactCaseSet(cases);

        foreach (JsonElement candidateCase in cases.EnumerateArray())
        {
            string caseId = StringValue(candidateCase, "case_id");
            switch (caseId)
            {
                case "refuelled_perturbation_candidate":
                    Assert.Equal("Nonconverged", StringValue(candidateCase, "status"));
                    Assert.True(candidateCase.TryGetProperty("snapshot", out JsonElement refuelledSnapshot));
                    Assert.Equal(JsonValueKind.Null, refuelledSnapshot.ValueKind);
                    break;
                case "rrs_perturbation_candidate":
                case "poison_perturbation_candidate":
                    Assert.Equal("NotCovered", StringValue(candidateCase, "status"));
                    Assert.True(candidateCase.TryGetProperty("snapshot", out JsonElement uncoveredSnapshot));
                    Assert.Equal(JsonValueKind.Null, uncoveredSnapshot.ValueKind);
                    break;
            }
        }
    }

    [Fact]
    public void ConsumerBindsAllDeferredRecordsToAdmittedProfilesAndStates()
    {
        using JsonDocument document = ReadCandidateOutput();
        JsonElement root = document.RootElement;
        JsonElement cases = Required(root, "cases");
        JsonElement records = Required(root, "records");
        AssertExactCaseSet(cases);
        var admittedInputDigests = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement candidateCase in cases.EnumerateArray())
        {
            if (candidateCase.TryGetProperty("snapshot", out JsonElement snapshot) &&
                snapshot.ValueKind == JsonValueKind.Object)
            {
                admittedInputDigests.Add(StringValue(snapshot, "input_digest"));
            }
        }

        Assert.Equal(57, records.GetArrayLength());
        var recordCountByInput = new Dictionary<string, int>(StringComparer.Ordinal);
        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        var quantityIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement record in records.EnumerateArray())
        {
            string inputDigest = StringValue(record, "input_digest");
            Assert.Contains(inputDigest, admittedInputDigests);
            recordCountByInput[inputDigest] = recordCountByInput.GetValueOrDefault(inputDigest) + 1;

            string profileId = StringValue(record, "tolerance_profile_id");
            string quantityId = StringValue(record, "quantity_id");
            profileIds.Add(profileId);
            quantityIds.Add(quantityId);

            Assert.Contains(profileId, ExpectedProfileIds);
            Assert.Contains(quantityId, ExpectedQuantityIds);
            Assert.True(ExpectedProfileToQuantity.TryGetValue(profileId, out string? expectedQuantityId));
            Assert.Equal(expectedQuantityId, quantityId);
            Assert.Equal("P2-T05-rule-" + profileId, StringValue(record, "comparison_rule_id"));
            Assert.Equal(SourceArtifactId, StringValue(record, "reference_id"));
            Assert.Equal("Synthetic", StringValue(record, "coverage_class"));
            Assert.Equal("CommittedSynthetic", StringValue(record, "artifact_availability"));
            Assert.Equal("Candidate", StringValue(record, "evidence_approval"));
            Assert.Equal("Runtime", StringValue(record, "validation_domain"));
            Assert.Equal("Deferred", StringValue(record, "status"));
            Assert.Equal(CandidateOutputRelativePath, StringValue(record, "evidence_path"));
        }

        Assert.Equal(AdmittedScenarioClasses.Count, recordCountByInput.Count);
        Assert.All(recordCountByInput.Values, count => Assert.Equal(19, count));
        Assert.True(ExpectedProfileIds.SetEquals(profileIds));
        Assert.True(ExpectedQuantityIds.SetEquals(quantityIds));
    }

    private static void AssertExactCaseSet(JsonElement cases)
    {
        var actualCaseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement candidateCase in cases.EnumerateArray())
        {
            string caseId = StringValue(candidateCase, "case_id");
            Assert.True(ExpectedCaseIds.Contains(caseId), $"Unexpected case ID '{caseId}'.");
            Assert.True(actualCaseIds.Add(caseId), $"Duplicate case ID '{caseId}'.");
        }

        Assert.True(ExpectedCaseIds.SetEquals(actualCaseIds), "Candidate case set does not match the bounded G4A disposition.");
    }

    private static JsonDocument ReadCandidateOutput()
    {
        return JsonDocument.Parse(File.ReadAllBytes(TestArtifactPath(CandidateOutputFileName)));
    }

    private static string HashFor(string fileName)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(TestArtifactPath(fileName)))).ToLowerInvariant();
    }

    private static string TestArtifactPath(string fileName)
    {
        return TestDataLocator.RequireFile(Path.Combine(TestArtifactDirectory, fileName));
    }

    private static JsonElement Required(JsonElement parent, string propertyName)
    {
        if (parent.TryGetProperty(propertyName, out JsonElement value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing JSON property '{propertyName}'.");
    }

    private static string StringValue(JsonElement parent, string propertyName)
    {
        string? value = Required(parent, propertyName).GetString();
        return value ?? throw new InvalidOperationException($"JSON property '{propertyName}' is null.");
    }

    private static bool BooleanValue(JsonElement parent, string propertyName)
    {
        return Required(parent, propertyName).GetBoolean();
    }

    private static double NumberValue(JsonElement parent, string propertyName)
    {
        return Required(parent, propertyName).GetDouble();
    }

    private static void AssertHexDigest(string value)
    {
        Assert.Equal(64, value.Length);
        Assert.All(value, character => Assert.True(
            character is >= '0' and <= '9' or >= 'a' and <= 'f',
            $"'{value}' is not a lowercase SHA-256 digest."));
    }
}
