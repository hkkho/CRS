using System.Text.Json;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P7T07LiteratureCaseBoundaryTests
{
    [Fact]
    public void LiteratureCasesRemainCandidateAndDeferred()
    {
        using JsonDocument document = ReadCases();
        JsonElement root = document.RootElement;

        Assert.Equal("reactorsim.p7-t07-literature-candidate-cases/v1", root.GetProperty("format").GetString());
        Assert.Equal("P7-T07", root.GetProperty("task_id").GetString());
        Assert.Equal("Candidate/Deferred/NoGolden", root.GetProperty("artifact_status").GetString());
        Assert.Equal("LiteratureCandidate", root.GetProperty("coverage_class").GetString());
        Assert.Equal("Candidate", root.GetProperty("evidence_approval").GetString());
        Assert.Equal("Deferred", root.GetProperty("comparison_status").GetString());
        Assert.Equal("NoGolden", root.GetProperty("golden_status").GetString());
        Assert.Contains("Prohibited", root.GetProperty("runtime_use").GetString());
        Assert.Equal(3, root.GetProperty("cases").GetArrayLength());

        foreach (JsonElement candidate in root.GetProperty("cases").EnumerateArray())
        {
            Assert.Equal("CandidateCaseDesign", candidate.GetProperty("case_status").GetString());
            Assert.Equal("BlockedUntilAdmissionProof", candidate.GetProperty("execution_status").GetString());
            Assert.False(candidate.GetProperty("source").GetProperty("artifact_committed").GetBoolean());
            Assert.All(candidate.GetProperty("comparison_observables").EnumerateArray(), observable =>
            {
                Assert.Equal("NotProven", observable.GetProperty("mapping_status").GetString());
                Assert.Equal("Deferred", observable.GetProperty("comparison_status").GetString());
            });
        }
    }

    [Fact]
    public void LiteratureCasesContainRequiredLatticeAndFullCoreShapes()
    {
        using JsonDocument document = ReadCases();
        Dictionary<string, JsonElement> cases = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .ToDictionary(candidate => candidate.GetProperty("case_id").GetString()!, StringComparer.Ordinal);

        Assert.Contains("p7-t07-s5-lattice-37-bundle-crosscheck-v1", cases.Keys);
        Assert.Contains("p7-t07-s5-lattice-depletion-300d-v1", cases.Keys);
        Assert.Contains("p7-t07-s1-fullcore-380-channel-300fpd-v1", cases.Keys);

        JsonElement fullCore = cases["p7-t07-s1-fullcore-380-channel-300fpd-v1"];
        Assert.Equal(380, fullCore.GetProperty("candidate_inputs").GetProperty("channel_count").GetProperty("value").GetInt32());
        Assert.Equal(12, fullCore.GetProperty("candidate_inputs").GetProperty("bundles_per_channel").GetProperty("value").GetInt32());
        Assert.Equal(8, fullCore.GetProperty("candidate_inputs").GetProperty("refuelling_shift").GetProperty("value").GetInt32());
        Assert.Equal(4, fullCore.GetProperty("candidate_inputs").GetProperty("refuelling_rate").GetProperty("value").GetInt32());

        JsonElement lattice = cases["p7-t07-s5-lattice-37-bundle-crosscheck-v1"];
        Assert.Equal("37-element CANDU bundle", lattice.GetProperty("model").GetProperty("geometry").GetProperty("fuel_bundle").GetString());
        Assert.Equal("SERPENT", lattice.GetProperty("model").GetProperty("independent_cross_check").GetProperty("program").GetString());
    }

    private static JsonDocument ReadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "P7T07Data", "literature-cases-v1.json");
        Assert.True(File.Exists(path), $"Missing P7-T07 candidate case artifact: {path}");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }
}
