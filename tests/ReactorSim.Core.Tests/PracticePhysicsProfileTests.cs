using System;
using System.Collections.Generic;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticePhysicsProfileTests
{
    private const string Header = """
        {"format":"crs-physics-profile","version":1,
         "model":"candu6-two-group-full-core-diffusion-v1",
         "basePack":"candu6-two-group-diffusion-v1-xenon-v11","units":"SI-v1","parameters":
        """;

    [Fact]
    public void ProfileAndLegacyEditsProduceTheSamePackAndLinkedXenonKnot()
    {
        var profile = FullCoreDiffusionDataPackV1.TryLoadPracticeProfile(Header + """
            {"xenon_rows":{"1":{"xe135_number_density_m3":2e20}},
             "practice":{"thermal_power_multiplier":0.98},
             "fuel_rows":{"1":{"burnup_j_per_kg_hm":14000000000,"energy_per_fission_j":3.2e-11}},
             "geometry":{"axial_boundary":{"cell_length_m":0.49}}}}
            """);
        var legacy = FullCoreDiffusionDataPackV1.TryLoadPracticeOverrides(new Dictionary<string, double>
        {
            ["geometry.axial_boundary.cell_length_m"] = 0.49,
            ["coefficient_tables[0].rows[1].burnup_j_per_kg_hm"] = 14e9,
            ["coefficient_tables[0].rows[1].energy_per_fission_j"] = 3.2e-11,
            ["practice.thermal_power_multiplier"] = 0.98,
            ["xenon_reference.rows[1].xe135_number_density_m3"] = 2e20,
        });
        Assert.True(profile.IsValid, profile.IsValid ? null : profile.FirstDiagnostic.ToString());
        Assert.Equal(legacy.Value.Descriptor.DataPackVersion, profile.Value.Descriptor.DataPackVersion);
        Assert.Equal(2e20, profile.Value.XenonReference!.At(14e9));
    }

    [Theory]
    [InlineData("{}", "xenon-v11", "unknown-revision", "Profile.BasePack")]
    [InlineData("{}", "SI-v1", "other-units", "Profile.Model")]
    [InlineData("{}", "\"version\":1", "\"version\":2", "Profile.Version")]
    [InlineData("""{"future":{}}""", "", "", "Profile.Section")]
    [InlineData("""{"fuel_rows":{"01":{"energy_per_fission_j":1}}}""", "", "", "Profile.Parameter")]
    [InlineData("""{"geometry":{"node_volume_m3":1,"node_volume_m3":2}}""", "", "", "Profile.Json")]
    [InlineData("""{"fuel_rows":{"0":{"burnup_j_per_kg_hm":1}}}""", "", "", "Parameter.Invalid")]
    [InlineData("""{"solver":{"maximum_iterations":1601}}""", "", "", "Iterations.Invalid")]
    public void UnsupportedProfilesFailClosed(string parameters, string oldValue, string newValue, string errorCode)
    {
        var header = oldValue.Length == 0 ? Header : Header.Replace(oldValue, newValue, StringComparison.Ordinal);
        var result = FullCoreDiffusionDataPackV1.TryLoadPracticeProfile(header + parameters + "}");
        Assert.False(result.IsValid);
        Assert.EndsWith(errorCode, result.FirstDiagnostic.Code);
    }
}
