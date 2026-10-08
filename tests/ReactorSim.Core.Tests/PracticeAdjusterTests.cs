using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeAdjusterTests
{
    [Fact]
    public void TwentyOneInterstitialRodsPreserveVolumeAndSplitTheMiddleAxialPlane()
    {
        Assert.Equal(21, PracticeAdjustersV1.Rods.Count);
        Assert.Equal(3, PracticeAdjustersV1.Rods.Select(r => r.AxialCentreM).Distinct().Count());
        Assert.Equal(7, PracticeAdjustersV1.Rods.Select(r => r.HorizontalCentreM).Distinct().Count());
        Assert.Equal(4.5 * .4953, PracticeAdjustersV1.Rods[0].AxialCentreM, 12);
        Assert.Equal(7.5 * .4953, PracticeAdjustersV1.Rods[20].AxialCentreM, 12);
        foreach (var rod in PracticeAdjustersV1.Rods)
        {
            var cells = PracticeAdjustersV1.Cells.Where(c => c.RodId == rod.Id).ToArray();
            Assert.Equal(12.0, cells.Sum(c => c.VolumeFraction), 11);
            Assert.All(cells, c => Assert.InRange(c.VolumeFraction, 0.0, .50000000001));
            Assert.All(cells, c => Assert.InRange(c.AbsorptionWeight, 0.0, c.VolumeFraction));
            Assert.Equal(6 + 6 * PracticeAdjustersV1.OuterToInnerSteelAreaRatio,
                cells.Sum(c => c.AbsorptionWeight), 11);
            uint[] axial = cells.Select(c => c.Node.Position.Value).Distinct().OrderBy(p => p).ToArray();
            Assert.Equal(rod.Id <= 7 ? new uint[] { 4 } : rod.Id <= 14 ? new uint[] { 5, 6 } : new uint[] { 7 }, axial);
            if (rod.Id is >= 8 and <= 14)
            {
                Assert.Equal(6.0, cells.Where(c => c.Node.Position.Value == 5).Sum(c => c.VolumeFraction), 11);
                Assert.Equal(6.0, cells.Where(c => c.Node.Position.Value == 6).Sum(c => c.VolumeFraction), 11);
            }
        }
        Assert.Equal(672, PracticeAdjustersV1.Cells.Count);
    }

    [Theory]
    [InlineData("unsupported", 0.001)]
    [InlineData(PracticeAdjustersV1.LayoutId, -0.001)]
    public void InvalidDeviceConfigurationFailsAtPackBoundary(string layout, double strength)
    {
        var json = EmbeddedJson();
        json["adjusters"] = new JObject
        {
            ["layout_id"] = layout,
            ["inner_absorption_group1_per_m"] = strength,
            ["inner_absorption_group2_per_m"] = strength
        };
        var result = FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString());
        Assert.False(result.IsValid);
        Assert.Equal("FullCoreDiffusionDataPack.Adjusters.Invalid", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void InsertedRodsHaveSeventeenMkWorthAtFixedInventoryAndHalfZones()
    {
        var json = EmbeddedJson();
        var inserted = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.NotNull(inserted.Adjusters);
        Assert.False(inserted.Adjusters!.IsZero);
        json.Remove("adjusters");
        var withdrawn = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.Null(withdrawn.Adjusters);
        var state = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var overlay = Require(Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6())
            .TryBuildOverlay(Enumerable.Repeat(.5, 14).ToArray()));
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(inserted));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, state.EnumerateBundles(), 2_064_000_000));
        var settled = Require(PracticeXenonEquilibriumV1.TryCreate(solver, state, Enumerable.Repeat(.5, 14).ToArray()));
        var xe = settled.Poison.Overlay;
        overlay = Require(StaticAbsorptionOverlayV1.TryCreate("fixed-xe-adjuster-worth-test",
            overlay.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node, e.DeltaAbsorptionGroup1PerM,
                e.DeltaAbsorptionGroup2PerM + xe.GetDeltaAbsorptionGroup2PerM(e.Node)))));
        var allIn = Require(model.TrySolve(state.EnumerateBundles(), overlay, 2_064_000_000));
        var allOut = Require(Require(FullCoreDiffusionModelV1.TryCreateCandu6(withdrawn))
            .TrySolve(state.EnumerateBundles(), overlay, 2_064_000_000));
        Assert.InRange(1000 * (1 / allIn.EffectiveK - 1 / allOut.EffectiveK), 16.95, 17.05);
        Assert.InRange(allIn.EffectiveK, .99995, 1.00005);
        Assert.Equal(2_064_000_000, allIn.TotalPowerWatts, 3);
        Assert.Equal(2_064_000_000, allOut.TotalPowerWatts, 3);
        Assert.NotEqual(allIn.CoefficientBindingDigest, allOut.CoefficientBindingDigest);
        Assert.Empty(Require(FullCoreDiffusionModelV1.TryCreateCandu6(inserted)).NonfuelNodes);
    }

    [Fact]
    public void ChannelReferenceIncludesAdjustersAndKeepsFixedThermalTotal()
    {
        var json = EmbeddedJson();
        var reference = PracticeChannelPowerReference.Create(
            Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString())), 2_064_000_000);
        json.Remove("adjusters");
        var noAdjusters = PracticeChannelPowerReference.Create(
            Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString())), 2_064_000_000);
        Assert.Equal(380, reference.ChannelPowerWatts.Count);
        Assert.Equal(2_064_000_000, reference.ThermalPowerWatts, 3);
        Assert.True(reference.ChannelPowerWatts.Zip(noAdjusters.ChannelPowerWatts, (a, b) => Math.Abs(a - b)).Max() > 10_000);
        Assert.NotEqual(reference.CoefficientBindingDigest, noAdjusters.CoefficientBindingDigest);
        var repeat = PracticeChannelPowerReference.Create(
            Require(FullCoreDiffusionDataPackV1.TryLoadJson(EmbeddedJson().ToString())), 2_064_000_000);
        Assert.Equal(reference.ChannelPowerWatts, repeat.ChannelPowerWatts);
    }

    private static JObject EmbeddedJson()
    {
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd());
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
