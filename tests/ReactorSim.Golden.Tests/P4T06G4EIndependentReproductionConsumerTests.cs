using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4EIndependentReproductionConsumerTests
{
    private const string TestArtifactDirectory = "P4T06G4EData";
    private const string ArtifactFileName = "independent-reproduction-v1.json";
    private const string ManifestFileName = "independent-reproduction-v1.manifest.json";
    private const string SourceArtifactFileName = "synthetic-input-v1.json";
    private const string ReducedPackFileName = "reduced-candidate-v1.json";
    private const string ReducedManifestFileName = "reduced-candidate-v1.manifest.json";
    private const string BenchmarkManifestFileName = "static-solver-benchmark.json";

    private const string ArtifactRelativePath =
        "data/comparisons/p4-t06-g4d-independent-reproduction-v1.json";

    private const string ManifestRelativePath =
        "data/comparisons/p4-t06-g4d-independent-reproduction-v1.manifest.json";

    private const string ArtifactSha256 =
        "ab456ca38fbd434d2c42af2ffb2f115af964273115e04c09f14945b74e2f5be3";

    private const string ManifestSha256 =
        "c9c5466f25cc8a0690fcf9f4307eb1948257d5efabde2580fd6402f765d0749b";

    private const string SourceSha256 =
        "1680a075cd34867b603e1e786002815b19be360d4e8557cd5485978dc32c1694";

    private const string PackSha256 =
        "0de429b27f78b5e2c5fe724916b14ac8ce45ba05560ab73f3fa4805befd11a0e";

    private const string ReducedManifestSha256 =
        "6e3c19d4f596dfd930b339f983a54b46a6a27b9e83b6652f8e40a058c19e2a8b";

    private const string BenchmarkManifestSha256 =
        "8a53f7519a3b6597d3382c9e91df356474d7d1e72e8ce5d0055e30ef635a1045";

    private const string SourceArtifactId = "p4-t06-r4-synthetic-input-v1";
    private const string PackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string TopologyFixtureId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string UnitsProfileId = "SI-v1";

    private static readonly Dictionary<string, string> ExpectedProfileToQuantity =
        new(StringComparer.Ordinal)
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

    private static readonly Dictionary<string, int> ExpectedProfileCounts =
        new(StringComparer.Ordinal)
        {
            ["P2-T05-determinism-repeat-equal-v1"] = 3,
            ["P2-T05-spatial-coefficient-id-v1"] = 18,
            ["P2-T05-spatial-convergence-v1"] = 3,
            ["P2-T05-spatial-fission-source-v1"] = 9,
            ["P2-T05-spatial-flux-v1"] = 3,
            ["P2-T05-spatial-iteration-count-v1"] = 3,
            ["P2-T05-spatial-k-v1"] = 3,
            ["P2-T05-spatial-normalization-scale-v1"] = 3,
            ["P2-T05-spatial-power-v1"] = 9,
            ["P2-T05-spatial-total-power-v1"] = 3
        };

    private static readonly Dictionary<string, (string Kind, string Unit, string Schema, string Scope)> QuantityContract =
        new(StringComparer.Ordinal)
        {
            ["determinism.repeat_equal"] = ("bytes", "bytes", "BytesV1", "RunPair"),
            ["spatial.coefficient_identity"] = ("bytes", "bytes", "BytesV1", "Lookup"),
            ["spatial.convergence"] = ("bytes", "bytes", "BytesV1", "Solve"),
            ["spatial.fission_source"] = ("scalar", "m^-3 s^-1", "ScalarV1", "Entity"),
            ["spatial.flux"] = ("vector", "m^-2 s^-1", "VectorV1", "Vector"),
            ["spatial.iteration_count"] = ("integer", "integer", "IntegerV1", "Solve"),
            ["spatial.k"] = ("scalar", "1", "ScalarV1", "Solve"),
            ["spatial.normalization_scale"] = ("scalar", "1", "ScalarV1", "Solve"),
            ["spatial.power"] = ("scalar", "W", "ScalarV1", "Entity"),
            ["spatial.total_power"] = ("scalar", "W", "ScalarV1", "Global")
        };

    private static readonly Dictionary<string, int> ScopeKindRanks =
        new(StringComparer.Ordinal)
        {
            ["Global"] = 0,
            ["Entity"] = 1,
            ["Vector"] = 2,
            ["Solve"] = 6,
            ["RunPair"] = 10,
            ["Lookup"] = 15
        };

    [Fact]
    public void IndependentArtifactBindsFrozenInputsAndManifest()
    {
        using JsonDocument artifact = ReadArtifact(ArtifactFileName);
        using JsonDocument manifest = ReadArtifact(ManifestFileName);
        JsonElement root = artifact.RootElement;
        JsonElement manifestRoot = manifest.RootElement;

        Assert.Equal("reactorsim.independent-spatial-reproduction/v1", StringValue(root, "format"));
        Assert.Equal("P4-T06-G4D", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("synthetic", StringValue(root, "evidence_class"));
        Assert.Equal("CommittedSynthetic", StringValue(root, "artifact_availability"));
        Assert.Equal("Candidate", StringValue(root, "evidence_approval"));
        Assert.Equal("Runtime", StringValue(root, "validation_domain"));
        Assert.Equal("p4-t06-g4d-independent-spatial-reproduction", StringValue(root, "generator_id"));
        Assert.Equal("v1", StringValue(root, "generator_version"));
        Assert.Equal("standalone_scalar_p2_t02_reproduction", StringValue(root, "model_contract"));
        Assert.Equal("independent_candidate_reproduction", StringValue(root, "independence_claim"));
        Assert.Equal("Deferred", StringValue(root, "comparison_status"));
        Assert.Contains("Deferred", StringValue(root, "gate_status"), StringComparison.Ordinal);
        Assert.Equal(SourceArtifactId, StringValue(root, "source_artifact_id"));
        Assert.Equal(PackArtifactId, StringValue(root, "pack_artifact_id"));
        Assert.Equal("identity_projection_v1", StringValue(root, "transform_id"));
        Assert.Equal("synthetic-v1", StringValue(root, "data_version"));
        Assert.Equal(UnitsProfileId, StringValue(root, "units_profile_id"));
        Assert.Equal("synthetic:P5-T05", StringValue(root, "source_provenance"));
        Assert.Equal(TopologyFixtureId, StringValue(root, "topology_fixture_id"));
        Assert.Equal(TopologyFixtureId, StringValue(root, "benchmark_scenario_id"));
        Assert.Contains("SI units", StringValue(root, "unit_and_ordering_boundary"), StringComparison.Ordinal);
        Assert.Contains("explicit node/channel/position order", StringValue(root, "unit_and_ordering_boundary"), StringComparison.Ordinal);
        Assert.Contains("no runtime schema", StringValue(root, "runtime_boundary"), StringComparison.Ordinal);

        Assert.Equal(SourceSha256, StringValue(root, "source_sha256"));
        Assert.Equal(PackSha256, StringValue(root, "pack_sha256"));
        Assert.Equal(ReducedManifestSha256, StringValue(root, "reduced_manifest_sha256"));
        Assert.Equal(BenchmarkManifestSha256, StringValue(root, "benchmark_manifest_sha256"));
        Assert.Equal(SourceSha256, HashFor(SourceArtifactFileName));
        Assert.Equal(PackSha256, HashFor(ReducedPackFileName));
        Assert.Equal(ReducedManifestSha256, HashFor(ReducedManifestFileName));
        Assert.Equal(BenchmarkManifestSha256, HashFor(BenchmarkManifestFileName));
        Assert.Equal(ArtifactSha256, HashFor(ArtifactFileName));
        Assert.Equal(ManifestSha256, HashFor(ManifestFileName));

        Assert.Equal("reactorsim.independent-spatial-reproduction-manifest/v1", StringValue(manifestRoot, "format"));
        Assert.Equal(1, IntValue(manifestRoot, "manifest_schema_version"));
        Assert.Equal("p4-t06-g4d-independent-reproduction-manifest-v1", StringValue(manifestRoot, "manifest_artifact_id"));
        Assert.Equal("p4-t06-g4d-independent-reproduction-v1", StringValue(manifestRoot, "artifact_id"));
        Assert.Equal(ArtifactSha256, StringValue(manifestRoot, "artifact_sha256"));
        Assert.Equal("candidate", StringValue(manifestRoot, "approval_status"));
        Assert.Equal("synthetic", StringValue(manifestRoot, "evidence_class"));
        Assert.Equal("Candidate", StringValue(manifestRoot, "evidence_approval"));
        Assert.Equal("Runtime", StringValue(manifestRoot, "validation_domain"));
        Assert.Equal("p4-t06-g4d-independent-spatial-reproduction", StringValue(manifestRoot, "generator_id"));
        Assert.Equal("standalone_scalar_p2_t02_reproduction", StringValue(manifestRoot, "model_contract"));
        Assert.Equal(SourceSha256, StringValue(manifestRoot, "source_sha256"));
        Assert.Equal(PackSha256, StringValue(manifestRoot, "pack_sha256"));
        Assert.Equal(ReducedManifestSha256, StringValue(manifestRoot, "reduced_manifest_sha256"));
        Assert.Equal(BenchmarkManifestSha256, StringValue(manifestRoot, "benchmark_manifest_sha256"));
        Assert.Equal(SourceArtifactId, StringValue(manifestRoot, "source_artifact_id"));
        Assert.Equal(PackArtifactId, StringValue(manifestRoot, "pack_artifact_id"));
        Assert.Equal(TopologyFixtureId, StringValue(manifestRoot, "topology_fixture_id"));
        Assert.Equal(TopologyFixtureId, StringValue(manifestRoot, "benchmark_scenario_id"));
        Assert.Equal(UnitsProfileId, StringValue(manifestRoot, "units_profile_id"));
        Assert.Equal(3, IntValue(manifestRoot, "case_count"));
        Assert.Equal(57, IntValue(manifestRoot, "record_count"));
        Assert.Equal("Deferred", StringValue(manifestRoot, "comparison_status"));
        Assert.Contains("No host-local paths", StringValue(manifestRoot, "path_boundary"), StringComparison.Ordinal);
    }

    [Fact]
    public void IndependentArtifactBindsThreeNormalizedCandidateSnapshots()
    {
        using JsonDocument document = ReadArtifact(ArtifactFileName);
        JsonElement cases = Required(document.RootElement, "cases");
        Assert.Equal(3, cases.GetArrayLength());

        var expectedCases = new Dictionary<string, (string ScenarioClass, double Burnup)>(StringComparer.Ordinal)
        {
            ["fresh_candidate"] = ("fresh", 0.0),
            ["equilibrium_like_candidate"] = ("equilibrium_like", 50000.0),
            ["midcycle_interpolation_candidate"] = ("interpolation_midpoint", 25000.0)
        };
        var actualCaseIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement candidateCase in cases.EnumerateArray())
        {
            string caseId = StringValue(candidateCase, "case_id");
            Assert.True(expectedCases.TryGetValue(caseId, out (string ScenarioClass, double Burnup) expected));
            Assert.True(actualCaseIds.Add(caseId), $"Duplicate case ID '{caseId}'.");
            Assert.Equal(expected.ScenarioClass, StringValue(candidateCase, "scenario_class"));
            Assert.Equal("CandidateSnapshot", StringValue(candidateCase, "status"));
            Assert.True(BooleanValue(candidateCase, "repeat_equal"));
            AssertHexDigest(StringValue(candidateCase, "input_digest"));
            AssertHexDigest(StringValue(candidateCase, "coefficient_identity"));
            AssertHexDigest(StringValue(candidateCase, "snapshot_digest"));

            JsonElement burnup = Required(candidateCase, "burnup_j_per_kg_hm_by_node");
            Assert.Equal(3, burnup.GetArrayLength());
            foreach (JsonElement nodeBurnup in burnup.EnumerateArray())
            {
                Assert.Equal(expected.Burnup, nodeBurnup.GetDouble());
            }

            JsonElement lookups = Required(candidateCase, "lookups");
            Assert.Equal(3, lookups.GetArrayLength());
            JsonElement snapshot = Required(candidateCase, "snapshot");
            AssertPositiveFinite(snapshot, "target_power_w");
            AssertPositiveFinite(snapshot, "initial_normalization_scale");
            AssertPositiveFinite(snapshot, "normalization_scale");
            AssertFiniteArray(snapshot, "flux_by_node_group", 6);
            AssertFiniteArray(snapshot, "node_power_w", 3);
            AssertFiniteArray(snapshot, "fission_source_rate_density", 3);

            JsonElement diagnostics = Required(snapshot, "diagnostics");
            Assert.Equal("Converged", StringValue(diagnostics, "status"));
            Assert.Equal("converged", StringValue(diagnostics, "convergence_reason"));
            Assert.True(IntValue(diagnostics, "iteration_count") > 0);
            Assert.Equal(0, IntValue(diagnostics, "failed_inner_solve_count"));
            Assert.Equal(0, IntValue(diagnostics, "invalid_coefficient_count"));
            Assert.Equal(0, IntValue(diagnostics, "negative_flux_count"));
            Assert.Equal(0, IntValue(diagnostics, "non_finite_value_count"));
            Assert.Equal(0, IntValue(diagnostics, "forbidden_clamp_count"));
        }

        Assert.True(expectedCases.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actualCaseIds));
    }

    [Fact]
    public void IndependentRecordsBindTypedP2T05ScopesUnitsStatesAndDeferredStatus()
    {
        using JsonDocument document = ReadArtifact(ArtifactFileName);
        JsonElement root = document.RootElement;
        JsonElement cases = Required(root, "cases");
        JsonElement records = Required(root, "records");
        Assert.Equal(57, records.GetArrayLength());

        var admittedInputDigests = cases.EnumerateArray()
            .Select(candidateCase => StringValue(candidateCase, "input_digest"))
            .ToHashSet(StringComparer.Ordinal);
        var recordCountByInput = new Dictionary<string, int>(StringComparer.Ordinal);
        var profileCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (JsonElement record in records.EnumerateArray())
        {
            string inputDigest = StringValue(record, "input_digest");
            Assert.Contains(inputDigest, admittedInputDigests);
            recordCountByInput[inputDigest] = recordCountByInput.GetValueOrDefault(inputDigest) + 1;

            string profileId = StringValue(record, "tolerance_profile_id");
            string quantityId = StringValue(record, "quantity_id");
            Assert.True(ExpectedProfileToQuantity.TryGetValue(profileId, out string? expectedQuantity));
            Assert.Equal(expectedQuantity, quantityId);
            Assert.True(QuantityContract.TryGetValue(quantityId, out (string Kind, string Unit, string Schema, string Scope) contract));
            profileCounts[profileId] = profileCounts.GetValueOrDefault(profileId) + 1;

            Assert.Equal("P2-T05-rule-" + profileId, StringValue(record, "comparison_rule_id"));
            Assert.Equal(SourceArtifactId, StringValue(record, "reference_id"));
            Assert.Equal("Synthetic", StringValue(record, "coverage_class"));
            Assert.Equal("CommittedSynthetic", StringValue(record, "artifact_availability"));
            Assert.Equal("Candidate", StringValue(record, "evidence_approval"));
            Assert.Equal("Runtime", StringValue(record, "validation_domain"));
            Assert.Equal("Deferred", StringValue(record, "status"));
            Assert.Equal(ArtifactRelativePath, StringValue(record, "evidence_path"));
            Assert.True(Guid.TryParse(StringValue(record, "observable_id"), out _));

            JsonElement simulationTime = Required(record, "simulation_time");
            Assert.Equal("Available", StringValue(simulationTime, "status"));
            Assert.Equal(0.0, NumberValue(simulationTime, "seconds"));

            JsonElement state = Required(record, "state_binding");
            Assert.Equal("synthetic_fixture", StringValue(state, "binding_kind"));
            Assert.Equal(TopologyFixtureId, StringValue(state, "topology_fixture_id"));
            Assert.Equal(PackArtifactId, StringValue(state, "data_pack_artifact_id"));
            Assert.Equal(inputDigest, StringValue(state, "input_digest"));
            Assert.Equal(BenchmarkManifestSha256, StringValue(state, "benchmark_manifest_sha256"));
            Assert.Equal("0", StringValue(state, "core_state_version"));
            Assert.Equal("0", StringValue(state, "spatial_state_version"));
            Assert.Equal("0", StringValue(state, "power_snapshot_version"));
            Assert.Equal("NotApplicable", StringValue(state, "kinetic_step_index"));
            Assert.Equal("NotApplicable", StringValue(state, "nuclide_state_version"));
            Assert.Equal(TopologyFixtureId, StringValue(state, "topology_version"));
            Assert.Equal(PackArtifactId, StringValue(state, "data_pack_version"));
            AssertHexDigest(StringValue(state, "coefficient_digest"));
            AssertHexDigest(StringValue(state, "snapshot_digest"));
            Assert.True(Guid.TryParse(StringValue(state, "spatial_solve_id"), out _));
            Assert.True(Guid.TryParse(StringValue(state, "power_snapshot_id"), out _));

            JsonElement value = Required(record, "value");
            Assert.Equal("available", StringValue(value, "status"));
            Assert.Equal(contract.Kind, StringValue(value, "kind"));
            Assert.Equal(contract.Unit, StringValue(value, "unit"));
            Assert.Equal(contract.Schema, StringValue(value, "payload_schema_id"));
            if (quantityId == "spatial.flux")
            {
                JsonElement order = Required(value, "component_order_spec");
                Assert.Equal(0, IntValue(order, "order_kind_ordinal"));
                Assert.Equal("NodeGroupKeyV1", StringValue(order, "component_key_schema_id"));
                Assert.Equal(0, IntValue(order, "comparator_ordinal"));
                Assert.Equal("ChannelPositionGroupV1", StringValue(order, "tie_break_schema_id"));
            }
            else
            {
                Assert.Equal("NotApplicable", StringValue(value, "component_order_spec"));
            }

            JsonElement scope = Required(record, "scope");
            Assert.Equal(contract.Scope, StringValue(scope, "kind"));
            AssertTypedScope(scope, contract.Scope, inputDigest);

            JsonElement orderKey = Required(record, "order_key");
            Assert.Equal("Runtime", StringValue(orderKey, "validation_domain"));
            Assert.Equal(1, IntValue(orderKey, "validation_domain_rank"));
            Assert.Equal(0.0, NumberValue(orderKey, "simulation_time_seconds"));
            Assert.Equal(0, IntValue(orderKey, "core_state_version"));
            Assert.Equal("NotApplicable", StringValue(orderKey, "event_rank_or_not_applicable"));
            Assert.Equal("NotApplicable", StringValue(orderKey, "sequence_or_not_applicable"));
            Assert.Equal("NotApplicable", StringValue(orderKey, "event_id_or_not_applicable"));
            Assert.Equal(StringValue(scope, "kind"), StringValue(orderKey, "scope_kind"));
            Assert.Equal(ScopeKindRanks[contract.Scope], IntValue(orderKey, "scope_kind_rank"));
            Assert.Equal(scope.GetProperty("key").GetRawText(), orderKey.GetProperty("scope_key").GetRawText());
            Assert.Equal(quantityId, StringValue(orderKey, "quantity_id"));
            Assert.Equal("not_applicable", StringValue(orderKey, "component_key"));
            Assert.Equal(StringValue(record, "observable_id"), StringValue(orderKey, "observable_id"));
        }

        Assert.Equal(3, recordCountByInput.Count);
        Assert.All(recordCountByInput.Values, count => Assert.Equal(19, count));
        Assert.Equal(ExpectedProfileToQuantity.Keys.ToHashSet(StringComparer.Ordinal), profileCounts.Keys.ToHashSet(StringComparer.Ordinal));
        foreach ((string profileId, int expectedCount) in ExpectedProfileCounts)
        {
            Assert.Equal(expectedCount, profileCounts[profileId]);
        }
    }

    [Fact]
    public void IndependentFluxConsumerBindsCanonicalNodeGroupVectorOrder()
    {
        using JsonDocument document = ReadArtifact(ArtifactFileName);
        JsonElement records = Required(document.RootElement, "records");
        List<JsonElement> fluxRecords = records.EnumerateArray()
            .Where(record => StringValue(record, "quantity_id") == "spatial.flux")
            .ToList();
        Assert.Equal(3, fluxRecords.Count);
        JsonElement flux = fluxRecords[0];

        JsonElement scopeGroups = Required(Required(flux, "scope"), "key").GetProperty("node_groups");
        JsonElement payload = Required(Required(flux, "value"), "payload");
        Assert.Equal(6, scopeGroups.GetArrayLength());
        Assert.Equal(6, payload.GetArrayLength());

        for (int index = 0; index < 6; index++)
        {
            int expectedBundlePosition = index / 2;
            int expectedGroupIndex = (index % 2) + 1;
            AssertNodeGroup(scopeGroups[index], expectedBundlePosition, expectedGroupIndex);
            JsonElement component = payload[index];
            AssertNodeGroup(Required(component, "component_key"), expectedBundlePosition, expectedGroupIndex);
            AssertFinite(NumberValue(component, "value"));
        }
    }

    private static void AssertTypedScope(JsonElement scope, string expectedKind, string inputDigest)
    {
        JsonElement key = Required(scope, "key");
        switch (expectedKind)
        {
            case "Global":
                Assert.Equal("NodeSet", StringValue(key, "key_kind"));
                JsonElement nodeSet = Required(key, "node_set");
                Assert.Equal(3, nodeSet.GetArrayLength());
                for (int index = 0; index < nodeSet.GetArrayLength(); index++)
                {
                    AssertNode(nodeSet[index], index);
                }
                break;
            case "Entity":
                Assert.Equal("Node", StringValue(key, "entity_kind"));
                AssertNode(Required(key, "node"), null);
                break;
            case "Vector":
                Assert.Equal("NodeGroup", StringValue(key, "vector_kind"));
                Assert.Equal(6, Required(key, "node_groups").GetArrayLength());
                break;
            case "Solve":
                Assert.True(Guid.TryParse(StringValue(key, "solve_id"), out _));
                Assert.Equal("0", StringValue(key, "spatial_state_version"));
                Assert.Equal("NotApplicable", StringValue(key, "group_index"));
                break;
            case "Lookup":
                Assert.Equal("NodeGroup", StringValue(key, "owner_kind"));
                AssertNodeGroup(Required(key, "node_group"), null, null);
                Assert.Equal("00000000-0000-0000-0000-0000000005a1", StringValue(key, "table_id"));
                AssertFinite(NumberValue(key, "input_burnup_j_per_kg_hm"));
                JsonElement bracket = Required(key, "bracket");
                Assert.True(IntValue(bracket, "lower_index") >= 0);
                Assert.True(IntValue(bracket, "upper_index") >= 0);
                AssertFinite(NumberValue(bracket, "alpha"));
                Assert.Equal("InRange", StringValue(bracket, "result_status"));
                break;
            case "RunPair":
                Assert.True(Guid.TryParse(StringValue(key, "run_id_a"), out _));
                Assert.True(Guid.TryParse(StringValue(key, "run_id_b"), out _));
                break;
            default:
                throw new InvalidOperationException($"Unexpected typed scope '{expectedKind}' for {inputDigest}.");
        }
    }

    private static void AssertNode(JsonElement node, int? expectedBundlePosition)
    {
        Assert.Equal(0, IntValue(node, "channel_id"));
        if (expectedBundlePosition.HasValue)
        {
            Assert.Equal(expectedBundlePosition.Value, IntValue(node, "bundle_position"));
        }
        else
        {
            Assert.InRange(IntValue(node, "bundle_position"), 0, 2);
        }
    }

    private static void AssertNodeGroup(JsonElement nodeGroup, int? expectedBundlePosition, int? expectedGroupIndex)
    {
        Assert.Equal(0, IntValue(nodeGroup, "channel_id"));
        if (expectedBundlePosition.HasValue)
        {
            Assert.Equal(expectedBundlePosition.Value, IntValue(nodeGroup, "bundle_position"));
        }
        else
        {
            Assert.InRange(IntValue(nodeGroup, "bundle_position"), 0, 2);
        }

        if (expectedGroupIndex.HasValue)
        {
            Assert.Equal(expectedGroupIndex.Value, IntValue(nodeGroup, "group_index"));
        }
        else
        {
            Assert.InRange(IntValue(nodeGroup, "group_index"), 1, 2);
        }
    }

    private static JsonDocument ReadArtifact(string fileName)
    {
        return JsonDocument.Parse(File.ReadAllBytes(TestArtifactPath(fileName)));
    }

    private static string HashFor(string fileName)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(TestArtifactPath(fileName)))).ToLowerInvariant();
    }

    private static string TestArtifactPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, TestArtifactDirectory, fileName);
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

    private static int IntValue(JsonElement parent, string propertyName)
    {
        return Required(parent, propertyName).GetInt32();
    }

    private static bool BooleanValue(JsonElement parent, string propertyName)
    {
        return Required(parent, propertyName).GetBoolean();
    }

    private static double NumberValue(JsonElement parent, string propertyName)
    {
        return Required(parent, propertyName).GetDouble();
    }

    private static void AssertPositiveFinite(JsonElement parent, string propertyName)
    {
        double value = NumberValue(parent, propertyName);
        AssertFinite(value);
        Assert.True(value > 0.0, $"Expected positive value for '{propertyName}'.");
    }

    private static void AssertFiniteArray(JsonElement parent, string propertyName, int expectedCount)
    {
        JsonElement values = Required(parent, propertyName);
        Assert.Equal(expectedCount, values.GetArrayLength());
        foreach (JsonElement value in values.EnumerateArray())
        {
            AssertFinite(value.GetDouble());
        }
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
    }

    private static void AssertHexDigest(string value)
    {
        Assert.Equal(64, value.Length);
        Assert.All(value, character => Assert.True(
            character is >= '0' and <= '9' or >= 'a' and <= 'f',
            $"'{value}' is not a lowercase SHA-256 digest."));
    }
}
