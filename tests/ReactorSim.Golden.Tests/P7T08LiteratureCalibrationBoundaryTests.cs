using System.Text.Json;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P7T08LiteratureCalibrationBoundaryTests
{
    [Fact]
    public void ApprovedArtifactIsSyntheticOnlyAndBindsFourCases()
    {
        using JsonDocument artifact = Read("approved-synthetic-v1.json");
        JsonElement root = artifact.RootElement;

        Assert.Equal("reactorsim.p7-t08-literature-calibrated-synthetic/v1", root.GetProperty("format").GetString());
        Assert.Equal("P7-T08", root.GetProperty("task_id").GetString());
        Assert.Equal("Approved/ApprovedGolden", root.GetProperty("artifact_status").GetString());
        Assert.Equal("SyntheticLiteratureCalibrated", root.GetProperty("coverage_class").GetString());
        Assert.Equal("ApprovedSyntheticOnly", root.GetProperty("evidence_approval").GetString());
        Assert.Equal("ApprovedSyntheticOnly", root.GetProperty("comparison_status").GetString());
        Assert.Equal("ApprovedGolden", root.GetProperty("golden_status").GetString());
        Assert.Equal("GoldenDataAuditOnly;CoreMappingProhibited", root.GetProperty("runtime_use").GetString());
        Assert.Equal(4, root.GetProperty("cases").GetArrayLength());
        Assert.Contains("not a direct CANDU physics baseline", root.GetProperty("approval_scope").GetString(), StringComparison.OrdinalIgnoreCase);
        AssertFiniteNumbers(root, "$artifact");
    }

    [Fact]
    public void LiteratureCalibratedOutputsPreserveTheoryAndScheduleInvariants()
    {
        using JsonDocument artifact = Read("approved-synthetic-v1.json");
        Dictionary<string, JsonElement> cases = artifact.RootElement.GetProperty("cases")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("case_id").GetString()!, StringComparer.Ordinal);

        JsonElement lattice = cases["p7-t08-s4-lattice-37-bundle-kcross-v1"];
        double dragon = lattice.GetProperty("input_snapshot").GetProperty("reported_dragon_k_effective").GetDouble();
        double serpent = lattice.GetProperty("input_snapshot").GetProperty("reported_serpent_k_effective").GetDouble();
        Assert.Equal((dragon + serpent) / 2.0, lattice.GetProperty("outputs").GetProperty("synthetic_k_effective").GetDouble());

        JsonElement depletion = cases["p7-t08-s5-lattice-depletion-300d-v1"];
        JsonElement depletionHistory = depletion.GetProperty("outputs").GetProperty("history");
        Assert.Equal(301, depletionHistory.GetArrayLength());
        Assert.Equal(9.59139, depletion.GetProperty("outputs").GetProperty("synthetic_exit_burnup_gwd_per_t").GetDouble(), 10);
        int expectedDepletionDay = 0;
        foreach (JsonElement row in depletionHistory.EnumerateArray())
        {
            int day = row.GetProperty("day").GetInt32();
            Assert.Equal(expectedDepletionDay, day);
            Assert.InRange(day, 0, 300);
            Assert.True(double.IsFinite(row.GetProperty("power_density_kw_per_kg").GetDouble()));
            Assert.True(double.IsFinite(row.GetProperty("burnup_gwd_per_t").GetDouble()));
            expectedDepletionDay++;
        }
        Assert.Equal(301, expectedDepletionDay);

        JsonElement atf = cases["p7-t08-s1-fullcore-atf-summary-v1"];
        Assert.Equal(1200, atf.GetProperty("outputs").GetProperty("total_refueled_channels").GetInt32());
        Assert.Equal(9600, atf.GetProperty("outputs").GetProperty("total_shifted_bundles").GetInt32());

        JsonElement refueling = cases["p7-t08-s4-fullcore-refueling-summary-v1"];
        Assert.Equal(408, refueling.GetProperty("outputs").GetProperty("synthetic_total_refueling_events").GetInt32());
        Assert.Equal(4.08, refueling.GetProperty("outputs").GetProperty("synthetic_average_refueling_rate_channels_per_day").GetDouble(), 12);

        int expectedAtfDay = 0;
        int previousChannels = 0;
        foreach (JsonElement row in atf.GetProperty("outputs").GetProperty("history").EnumerateArray())
        {
            int day = row.GetProperty("full_power_day").GetInt32();
            Assert.Equal(expectedAtfDay, day);
            Assert.InRange(day, 0, 300);
            int cumulative = row.GetProperty("cumulative_refueled_channels").GetInt32();
            Assert.Equal(day * 4, cumulative);
            Assert.Equal(cumulative * 8, row.GetProperty("cumulative_shifted_bundles").GetInt32());
            Assert.True(cumulative >= previousChannels);
            Assert.True(double.IsFinite(row.GetProperty("synthetic_reactivity_delta_mk").GetDouble()));
            Assert.True(double.IsFinite(row.GetProperty("synthetic_enriched_boron_delta_ppm").GetDouble()));
            Assert.True(double.IsFinite(row.GetProperty("synthetic_enriched_channel_power_delta_kw").GetDouble()));
            previousChannels = cumulative;
            expectedAtfDay++;
        }
        Assert.Equal(301, expectedAtfDay);

        int expectedRefuelingDay = 0;
        int previousEvents = 0;
        foreach (JsonElement row in refueling.GetProperty("outputs").GetProperty("history").EnumerateArray())
        {
            int day = row.GetProperty("day").GetInt32();
            Assert.Equal(expectedRefuelingDay, day);
            Assert.InRange(day, 0, 100);
            previousEvents += row.GetProperty("synthetic_refueling_events_today").GetInt32();
            Assert.Equal(previousEvents, row.GetProperty("cumulative_refueling_events").GetInt32());
            Assert.Equal(previousEvents * 8, row.GetProperty("cumulative_shifted_bundles").GetInt32());
            expectedRefuelingDay++;
        }
        Assert.Equal(101, expectedRefuelingDay);

        using JsonDocument manifest = Read("approved-synthetic-v1.manifest.json");
        Assert.Equal(0d, manifest.RootElement.GetProperty("maximum_schedule_balance_absolute").GetDouble());
        Assert.Equal(0d, manifest.RootElement.GetProperty("minimum_nonnegative_value").GetDouble());
    }

    private static JsonDocument Read(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "P7T08Data", fileName);
        Assert.True(File.Exists(path), $"Missing P7-T08 artifact: {path}");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static void AssertFiniteNumbers(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                Assert.True(double.IsFinite(element.GetDouble()), $"Non-finite number at {path}");
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement child in element.EnumerateArray())
                {
                    AssertFiniteNumbers(child, $"{path}[{index}]");
                    index++;
                }

                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertFiniteNumbers(property.Value, $"{path}.{property.Name}");
                }

                break;
        }
    }
}
