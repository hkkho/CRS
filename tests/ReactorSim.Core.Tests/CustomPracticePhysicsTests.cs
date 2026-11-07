using System.Collections.Generic;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class CustomPracticePhysicsTests
{
    [Fact]
    public void EditsUseTheSharedFuelTableAndKeepTheMatchingXenonKnot()
    {
        var result = FullCoreDiffusionDataPackV1.TryLoadPracticeOverrides(new Dictionary<string, double>
        {
            ["geometry.node_volume_m3"] = 0.041,
            ["coefficient_tables[0].rows[1].burnup_j_per_kg_hm"] = 14e9,
            ["coefficient_tables[0].rows[1].energy_per_fission_j"] = 3.2e-11,
            ["xenon_reference.rows[1].xe135_number_density_m3"] = 1.2e21,
        });
        Assert.True(result.IsValid, result.IsValid ? null : result.FirstDiagnostic.ToString());
        Assert.Equal(0.041, result.Value.NodeVolumeM3);
        var fuel = result.Value.CoefficientTables[0].Rows[1];
        Assert.Equal(14e9, fuel.BurnupJPerKgHm);
        Assert.Equal(3.2e-11, fuel.Coefficients.EnergyPerFissionJ);
        Assert.Equal(1.2e21, result.Value.XenonReference!.At(fuel.BurnupJPerKgHm));
    }
}
