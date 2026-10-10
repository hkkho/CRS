using Newtonsoft.Json.Linq;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class AxialVacuumBoundaryTests
{
    [Fact]
    public void RefitRetainsHeatingAndBurnupKnotsAndAppliesOnlyTheRecordedProductionNormalization()
    {
        var source = Require(FullCoreDiffusionDataPackV1.TryLoadJson(File.ReadAllText(TestDataLocator.RequireRepositoryFile(
            "data/calibration/axial-marshak-v8/source-pack.json"))));
        var active = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        Assert.Equal("zero-incoming-current-v1", active.AxialBoundaryConditionId);
        Assert.Equal(source.VacuumBoundaryConductance.Group1M2, active.VacuumBoundaryConductance.Group1M2);
        var before = Assert.Single(source.CoefficientTables).Rows;
        var after = Assert.Single(active.CoefficientTables).Rows;
        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].BurnupJPerKgHm, after[i].BurnupJPerKgHm);
            Assert.Equal(before[i].Coefficients.FissionGroup1PerM, after[i].Coefficients.FissionGroup1PerM);
            Assert.Equal(before[i].Coefficients.FissionGroup2PerM, after[i].Coefficients.FissionGroup2PerM);
            Assert.Equal(before[i].Coefficients.AbsorptionGroup2PerM, after[i].Coefficients.AbsorptionGroup2PerM);
            Assert.Equal(before[i].Coefficients.EnergyPerFissionJ, after[i].Coefficients.EnergyPerFissionJ);
            Assert.Equal(1.0339620328061294, after[i].Coefficients.NuFissionGroup1PerM / before[i].Coefficients.NuFissionGroup1PerM, 13);
            Assert.Equal(1.0339620328061294, after[i].Coefficients.NuFissionGroup2PerM / before[i].Coefficients.NuFissionGroup2PerM, 13);
        }
    }
    [Fact]
    public void MarshakConductanceUsesTheHalfCellAndGroupDiffusionDistance()
    {
        var json = EmbeddedJson();
        json["geometry"]!["axial_boundary"] = new JObject { ["condition_id"] = "zero-incoming-current-v1", ["cell_length_m"] = .4953 };
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        double area = pack.NodeVolumeM3 / .4953;
        double d1 = pack.AxialConductance.Group1M2 * .4953 / area;
        double d2 = pack.AxialConductance.Group2M2 * .4953 / area;
        Assert.Equal(d1 * area / (.4953 / 2 + 2 * d1), pack.AxialVacuumBoundaryConductance.Group1M2, 14);
        Assert.Equal(d2 * area / (.4953 / 2 + 2 * d2), pack.AxialVacuumBoundaryConductance.Group2M2, 14);
        foreach (var group in new[] { (D: d1, C: pack.AxialVacuumBoundaryConductance.Group1M2),
            (D: d2, C: pack.AxialVacuumBoundaryConductance.Group2M2) })
        {
            // For unit centre flux, Fick's half-cell drop gives the surface
            // flux. J_in = phi_surface/4 - J_outward/2 must vanish.
            double outwardCurrent = group.C / area;
            double surfaceFlux = 1 - outwardCurrent * (.4953 / 2) / group.D;
            Assert.True(outwardCurrent > 0);
            Assert.Equal(0, surfaceFlux / 4 - outwardCurrent / 2, 14);
        }
        Assert.True(pack.AxialVacuumBoundaryConductance.Group1M2 > pack.VacuumBoundaryConductance.Group1M2);
        Assert.Equal("zero-incoming-current-v1", pack.AxialBoundaryConditionId);
        ((JObject)json["geometry"]!).Remove("axial_boundary");
        var legacy = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.Same(legacy.VacuumBoundaryConductance, legacy.AxialVacuumBoundaryConductance);
        Assert.Equal("legacy-fitted-conductance-v1", legacy.AxialBoundaryConditionId);
    }

    [Theory]
    [InlineData("reflective", .4953)]
    [InlineData("zero-incoming-current-v1", 0)]
    [InlineData("zero-incoming-current-v1", -1)]
    public void InvalidAxialContractsAreRejected(string mode, double length)
    {
        var json = EmbeddedJson();
        json["geometry"]!["axial_boundary"] = new JObject { ["condition_id"] = mode, ["cell_length_m"] = length };
        var result = FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString());
        Assert.False(result.IsValid);
        Assert.Equal("FullCoreDiffusionDataPack.AxialBoundary.Invalid", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void OnlyVacuumEndFacesUseMarshakAndReflectiveFacesStillHaveZeroLeakage()
    {
        var json = EmbeddedJson();
        json["geometry"]!["axial_boundary"] = new JObject { ["condition_id"] = "zero-incoming-current-v1", ["cell_length_m"] = .4953 };
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        var end = new NodeKey(new ChannelId(200), new BundlePosition(0));
        var topology = Require(Candu6CoreTopologyFactoryV1.TryCreate());
        var model = Require(FullCoreDiffusionModelV1.TryCreate(pack, topology, Require(SpatialStencil.TryCreate(topology))));
        var state = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var coefficients = Require(model.TryPrepareSolve(state.EnumerateBundles())).BaseCoefficients;
        Assert.True(coefficients.TryGetBoundary(new SpatialBoundaryKey(end, TopologyFace.EndA), out var axial));
        Assert.Equal(pack.AxialVacuumBoundaryConductance.Group1M2, axial.Group1);
        var radialFace = model.Stencil.Nodes.SelectMany(n => n.BoundaryTerms.Select(b => (Node: n.Node, Term: b)))
            .First(b => b.Term.Face != TopologyFace.EndA && b.Term.Face != TopologyFace.EndB);
        Assert.True(coefficients.TryGetBoundary(new SpatialBoundaryKey(radialFace.Node, radialFace.Term.Face), out var radial));
        Assert.Equal(pack.VacuumBoundaryConductance.Group1M2, radial.Group1);
        Assert.Equal(pack.VacuumBoundaryConductance.Group2M2, radial.Group2);
        var reflected = Require(Candu6CoreTopologyFactoryV1.TryCreate(new[] { new ReflectiveFaceOverrideV1(end, TopologyFace.EndA) }));
        var reflectedModel = Require(FullCoreDiffusionModelV1.TryCreate(pack, reflected, Require(SpatialStencil.TryCreate(reflected))));
        var reflectedCoefficients = Require(reflectedModel.TryPrepareSolve(state.EnumerateBundles())).BaseCoefficients;
        Assert.True(reflectedCoefficients.TryGetBoundary(new SpatialBoundaryKey(end, TopologyFace.EndA), out var mirror));
        Assert.Equal(0, mirror.Group1); Assert.Equal(0, mirror.Group2);
        Assert.Equal(coefficients.Nodes.Select(n => n.AbsorptionGroup2PerM), reflectedCoefficients.Nodes.Select(n => n.AbsorptionGroup2PerM));
    }

    private static JObject EmbeddedJson()
    {
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd());
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
